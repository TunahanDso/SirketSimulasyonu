using System.Reflection;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

/// <summary>
/// Eski ekonomi tickini çalıştırmadan işletim saatini, SLA tekliflerini ve
/// sözleşme ödemelerini yönetir.
/// </summary>
public sealed class V9IsletimKoordinatoru
{
    private static readonly string[] Kategoriler =
    [
        "api", "analitik", "eposta", "sosyal-medya",
        "guvenlik", "platform", "isletim-sistemi", "altyapi"
    ];

    private readonly SirketYoneticisi _sirketler;
    private readonly object _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _kilitAlani;
    private readonly FieldInfo _tickAlani;
    private readonly MethodInfo _kaydetMetodu;
    private readonly Random _rastgele = new(20260727);

    public V9IsletimKoordinatoru(
        SirketYoneticisi sirketler,
        KodTabanliSirketIsletimYoneticisi isletim)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        ArgumentNullException.ThrowIfNull(isletim);
        FieldInfo temelAlani = typeof(KodTabanliSirketIsletimYoneticisi)
            .GetField("_temel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.1 işletim koordinatörü temel alanı bulamadı.");
        _temel = temelAlani.GetValue(isletim)
            ?? throw new InvalidOperationException("V9.1 temel işletim yöneticisi boş.");
        Type tur = _temel.GetType();
        _veriAlani = tur.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.1 işletim verisi bulunamadı.");
        _kilitAlani = tur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.1 işletim kilidi bulunamadı.");
        _tickAlani = tur.GetField("_tick", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.1 işletim tick alanı bulunamadı.");
        _kaydetMetodu = tur.GetMethod("TumunuKaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.1 işletim kayıt metodu bulunamadı.");
    }

    public async Task TickCalistirAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = (SemaphoreSlim)(_kilitAlani.GetValue(_temel)
            ?? throw new InvalidOperationException("V9.1 işletim kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try
        {
            SirketIsletimDosyasi veri = (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel)
                ?? throw new InvalidOperationException("V9.1 işletim verisi boş."));
            _tickAlani.SetValue(_temel, tickNumarasi);
            veri.SonIslenenTick = tickNumarasi;
            TeklifleriTemizle(veri, tickNumarasi);
            GerekirseTeklifUret(veri, tickNumarasi);
            SozlesmeleriIsle(veri, tickNumarasi);
            object? sonuc = _kaydetMetodu.Invoke(_temel, [cancellationToken]);
            if (sonuc is Task gorev) await gorev;
        }
        finally { kilit.Release(); }
    }

    public static IReadOnlyList<string> GerekliHizmetAileleri(string kategori) =>
        kategori?.Trim().ToLowerInvariant() switch
        {
            "analitik" => ["veri", "analitik"],
            "eposta" => ["eposta"],
            "sosyal-medya" => ["sosyal"],
            "guvenlik" => ["guvenlik"],
            "platform" or "isletim-sistemi" => ["isletim"],
            "altyapi" => ["isletim", "veritabani"],
            _ => ["herhangi-bir-aktif-hizmet"]
        };

    public static bool SozlesmeyeUygunMu(
        SirketKaydi sirket,
        SozlesmeTeklifi teklif,
        out List<string> eksikler)
    {
        eksikler = [];
        if (!sirket.BagliMi) eksikler.Add("şirket sunucusu çevrimdışı");
        if (sirket.KodKalitesiPuani < teklif.AsgariKalite) eksikler.Add($"kalite {teklif.AsgariKalite:N0}");
        if (sirket.PerformansPuani < teklif.AsgariPerformans) eksikler.Add($"performans {teklif.AsgariPerformans:N0}");
        if (sirket.GuvenlikPuani < teklif.AsgariGuvenlik) eksikler.Add($"güvenlik {teklif.AsgariGuvenlik:N0}");
        int kapasite = sirket.Hizmetler.Where(x => x.Aktif).Sum(x => x.AzamiEszamanliIs);
        if (kapasite < teklif.GerekliKapasite) eksikler.Add($"kapasite {teklif.GerekliKapasite:N0}");
        if (!KategoriHizmetiVarMi(sirket, teklif.Kategori))
            eksikler.Add("gerekli hizmet ailesi: " + string.Join("/", GerekliHizmetAileleri(teklif.Kategori)));
        return eksikler.Count == 0;
    }

    private void TeklifleriTemizle(SirketIsletimDosyasi veri, long tick)
    {
        veri.SozlesmeTeklifleri.RemoveAll(x =>
            (!x.Aktif && string.IsNullOrWhiteSpace(x.KabulEdenSirketKimligi) && x.SonKabulTicki < tick - 20) ||
            (x.Aktif && x.SonKabulTicki < tick));
        foreach (SozlesmeTeklifi teklif in veri.SozlesmeTeklifleri.Where(x => x.SonKabulTicki < tick))
            teklif.Aktif = false;
    }

    private void GerekirseTeklifUret(SirketIsletimDosyasi veri, long tick)
    {
        int acik = veri.SozlesmeTeklifleri.Count(x => x.Aktif && x.SonKabulTicki >= tick);
        if (acik >= 8 || tick % 3 != 0) return;
        int adet = Math.Min(3, 8 - acik);
        for (int i = 0; i < adet; i++)
        {
            string kategori = Kategoriler[_rastgele.Next(Kategoriler.Length)];
            int sure = _rastgele.Next(10, 31);
            veri.SozlesmeTeklifleri.Add(new SozlesmeTeklifi
            {
                TeklifKimligi = $"v9-sla-{Guid.NewGuid():N}",
                Baslik = $"{kategori} hizmet seviyesi sözleşmesi",
                Kategori = kategori,
                SureTick = sure,
                TickOdemesi = _rastgele.Next(500, 2_501),
                IhlalCezasi = _rastgele.Next(750, 5_001),
                AsgariKalite = _rastgele.Next(58, 83),
                AsgariPerformans = _rastgele.Next(58, 83),
                AsgariGuvenlik = _rastgele.Next(58, 86),
                GerekliKapasite = _rastgele.Next(2, 26),
                SonKabulTicki = tick + _rastgele.Next(4, 10),
                Aktif = true
            });
        }
    }

    private void SozlesmeleriIsle(SirketIsletimDosyasi veri, long tick)
    {
        foreach (SirketIsletimDurumu durum in veri.Sirketler)
        {
            SirketKaydi? sirket = _sirketler.SirketKayitlari.FirstOrDefault(x =>
                x.SirketKimligi.Equals(durum.SirketKimligi, StringComparison.OrdinalIgnoreCase));
            if (sirket is null) continue;
            foreach (SozlesmeKaydi sozlesme in durum.Sozlesmeler.Where(x => x.Aktif).ToList())
            {
                if (tick > sozlesme.BitisTicki)
                {
                    sozlesme.Aktif = false;
                    continue;
                }
                if (!sirket.BagliMi)
                {
                    // Çevrimdışı şirketin sözleşme saati ve finansı donar.
                    sozlesme.BitisTicki++;
                    continue;
                }

                bool uygun = sirket.KodKalitesiPuani >= sozlesme.AsgariKalite &&
                             sirket.PerformansPuani >= sozlesme.AsgariPerformans &&
                             sirket.GuvenlikPuani >= sozlesme.AsgariGuvenlik &&
                             sirket.Hizmetler.Where(x => x.Aktif).Sum(x => x.AzamiEszamanliIs) >= sozlesme.GerekliKapasite &&
                             KategoriHizmetiVarMi(sirket, sozlesme.Kategori);
                if (uygun)
                {
                    decimal odeme = Math.Clamp(sozlesme.TickOdemesi, 0, 3_000m);
                    sirket.Kasa += odeme;
                    sirket.ToplamGelir += odeme;
                    sozlesme.BasariliTickSayisi++;
                    IslemEkle(durum, tick, "sla-gelir", $"{sozlesme.Baslik} şartları sağlandı.", odeme);
                }
                else
                {
                    decimal ceza = Math.Clamp(sozlesme.IhlalCezasi * 0.02m, 10m, 150m);
                    decimal kesilen = Math.Min(Math.Max(0, sirket.Kasa), ceza);
                    sirket.Kasa -= kesilen;
                    durum.ToplamIsletmeGideri += kesilen;
                    durum.OdenemeyenGider = Math.Min(20_000m, durum.OdenemeyenGider + ceza - kesilen);
                    sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - 0.03);
                    sozlesme.IhlalSayisi++;
                    IslemEkle(durum, tick, "sla-ihlal", $"{sozlesme.Baslik} şartları karşılanamadı.", -ceza);
                }
            }
        }
    }

    private static bool KategoriHizmetiVarMi(SirketKaydi sirket, string kategori)
    {
        IReadOnlyList<string> aileler = GerekliHizmetAileleri(kategori);
        if (aileler.Contains("herhangi-bir-aktif-hizmet")) return sirket.Hizmetler.Any(x => x.Aktif);
        return sirket.Hizmetler.Any(h => h.Aktif && aileler.Any(a =>
            h.HizmetKimligi.StartsWith(a + ".", StringComparison.OrdinalIgnoreCase)));
    }

    private static void IslemEkle(SirketIsletimDurumu durum, long tick, string tur, string aciklama, decimal tutar)
    {
        durum.SonIslemler.Add(new IsletimIslemKaydi
        {
            IslemKimligi = $"v9-sla-islem-{Guid.NewGuid():N}",
            TickNumarasi = tick,
            IslemTuru = tur,
            Aciklama = aciklama,
            Tutar = tutar
        });
        if (durum.SonIslemler.Count > 200) durum.SonIslemler = durum.SonIslemler.TakeLast(200).ToList();
    }
}
