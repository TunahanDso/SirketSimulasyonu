using System.Reflection;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class EkonomiDengeV7Yoneticisi
{
    private readonly SirketYoneticisi _sirketler;
    private readonly SirketIsletimYoneticisi _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _kilitAlani;
    private readonly MethodInfo _kaydetMetodu;
    private readonly Dictionary<string, decimal> _oncekiIsletmeGideri = new(StringComparer.OrdinalIgnoreCase);

    public EkonomiDengeV7Yoneticisi(
        SirketYoneticisi sirketler,
        KodTabanliSirketIsletimYoneticisi isletim)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        ArgumentNullException.ThrowIfNull(isletim);
        FieldInfo temelAlani = typeof(KodTabanliSirketIsletimYoneticisi)
            .GetField("_temel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Ekonomi V7 işletim köprüsü kurulamadı.");
        _temel = (SirketIsletimYoneticisi)(temelAlani.GetValue(isletim)
            ?? throw new InvalidOperationException("Ekonomi V7 temel işletim yöneticisi boş."));
        Type tur = _temel.GetType();
        _veriAlani = tur.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Ekonomi V7 veri alanı bulunamadı.");
        _kilitAlani = tur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Ekonomi V7 kilidi bulunamadı.");
        _kaydetMetodu = tur.GetMethod("TumunuKaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Ekonomi V7 kayıt metodu bulunamadı.");
    }

    public async Task TickOncesiAsync(CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = Kilit();
        await kilit.WaitAsync(cancellationToken);
        try
        {
            _oncekiIsletmeGideri.Clear();
            SirketIsletimDosyasi veri = Veri();
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
                _oncekiIsletmeGideri[sirket.SirketKimligi] = Durum(veri, sirket.SirketKimligi).ToplamIsletmeGideri;
        }
        finally { kilit.Release(); }
    }

    public async Task TickSonuAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = Kilit();
        await kilit.WaitAsync(cancellationToken);
        try
        {
            SirketIsletimDosyasi veri = Veri();
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
            {
                SirketIsletimDurumu durum = Durum(veri, sirket.SirketKimligi);
                decimal onceki = _oncekiIsletmeGideri.TryGetValue(sirket.SirketKimligi, out decimal x) ? x : durum.ToplamIsletmeGideri;
                decimal buTick = Math.Max(0, durum.ToplamIsletmeGideri - onceki);
                decimal hedef = SürdürülebilirTickGideri(sirket, durum);
                if (buTick <= hedef * 1.10m) continue;

                decimal duzeltme = decimal.Round(buTick - hedef, 2);
                durum.ToplamIsletmeGideri = Math.Max(onceki, durum.ToplamIsletmeGideri - duzeltme);
                sirket.Kasa += duzeltme;
                durum.SonIslemler.Add(new IsletimIslemKaydi
                {
                    IslemKimligi = $"v7-gider-dengeleme-{Guid.NewGuid():N}",
                    TickNumarasi = tickNumarasi,
                    IslemTuru = "operasyon-maliyet-normalizasyonu",
                    Aciklama = $"Eski ölçek formülünün aşırı sabit maliyeti V7 fiziksel kaynak modeline göre {duzeltme:N2} TL azaltıldı. Gerçek yatırım, kullanıcı, finansman, ceza ve piyasa giderleri korunur.",
                    Tutar = 0
                });
                if (durum.SonIslemler.Count > 200)
                    durum.SonIslemler = durum.SonIslemler.TakeLast(200).ToList();
                KonsolKayitcisi.Bilgi($"V7 GİDER DENGESİ | {sirket.SirketAdi} | Eski tick gideri: {buTick:N2} | Etkin gider: {hedef:N2}");
            }
            await KaydetAsync(cancellationToken);
        }
        finally { kilit.Release(); }
    }

    private static decimal SürdürülebilirTickGideri(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        int aktifHizmet = sirket.Hizmetler.Count(h => h.Aktif);
        int hizmetKapasitesi = sirket.Hizmetler.Where(h => h.Aktif).Sum(h => h.AzamiEszamanliIs);
        int aktifUrun = durum.Urunler.Count(u => u.Aktif);
        int kullanici = durum.Urunler.Where(u => u.Aktif).Sum(u => u.AktifKullaniciSayisi);
        int yatirimSeviyesi = durum.YatirimSeviyeleri.Values.Sum();

        decimal gider =
            35m +
            aktifHizmet * 1.15m +
            hizmetKapasitesi * 0.035m +
            aktifUrun * 18m +
            kullanici * 0.008m +
            yatirimSeviyesi * 4m +
            (decimal)Math.Max(0, durum.TeknikBorc) * 0.45m +
            (decimal)Math.Max(0, durum.BakimBaskisi) * 0.40m;

        return decimal.Round(Math.Clamp(gider, 45m, 250_000m), 2);
    }

    private SemaphoreSlim Kilit() => (SemaphoreSlim)(_kilitAlani.GetValue(_temel)
        ?? throw new InvalidOperationException("Ekonomi V7 işletim kilidi boş."));

    private SirketIsletimDosyasi Veri() =>
        (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel) ?? new SirketIsletimDosyasi());

    private static SirketIsletimDurumu Durum(SirketIsletimDosyasi veri, string kimlik)
    {
        SirketIsletimDurumu? durum = veri.Sirketler.FirstOrDefault(x =>
            x.SirketKimligi.Equals(kimlik, StringComparison.OrdinalIgnoreCase));
        if (durum is not null) return durum;
        durum = new SirketIsletimDurumu { SirketKimligi = kimlik };
        veri.Sirketler.Add(durum);
        return durum;
    }

    private async Task KaydetAsync(CancellationToken cancellationToken)
    {
        object? sonuc = _kaydetMetodu.Invoke(_temel, [cancellationToken]);
        if (sonuc is Task task) await task;
    }
}
