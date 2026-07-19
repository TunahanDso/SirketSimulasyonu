using System.Reflection;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class EkonomiDengeV7Yoneticisi
{
    private sealed record Baslangic(decimal OdenmisGider, decimal OdenemeyenGider);

    private readonly SirketYoneticisi _sirketler;
    private readonly SirketIsletimYoneticisi _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _kilitAlani;
    private readonly MethodInfo _kaydetMetodu;
    private readonly Dictionary<string, Baslangic> _baslangiclar = new(StringComparer.OrdinalIgnoreCase);

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
            _baslangiclar.Clear();
            SirketIsletimDosyasi veri = Veri();
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
            {
                SirketIsletimDurumu durum = Durum(veri, sirket.SirketKimligi);
                _baslangiclar[sirket.SirketKimligi] = new Baslangic(
                    durum.ToplamIsletmeGideri,
                    durum.OdenemeyenGider);
            }
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
                Baslangic onceki = _baslangiclar.TryGetValue(sirket.SirketKimligi, out Baslangic? x)
                    ? x
                    : new Baslangic(durum.ToplamIsletmeGideri, durum.OdenemeyenGider);

                decimal odenmisArtis = Math.Max(0, durum.ToplamIsletmeGideri - onceki.OdenmisGider);
                decimal odenemeyenArtis = Math.Max(0, durum.OdenemeyenGider - onceki.OdenemeyenGider);
                decimal toplamYukumluluk = odenmisArtis + odenemeyenArtis;

                // Yalnız eski formülün hizmet/kapasite/ürün ölçeklemesinden doğan
                // yapay fark düzeltilir. Yatırım bakım gideri, aktif piyasa olayı,
                // SLA cezası, güvenlik kaybı ve diğer gerçek olaylar bu hesaba
                // dahil edilmez ve bu nedenle iade edilmez.
                decimal eskiYapayTaban = EskiYapaySabitMaliyet(sirket, durum);
                decimal v7Karsiligi = V7SabitMaliyetKarsiligi(sirket, durum);
                decimal azamiDuzeltme = Math.Max(0, eskiYapayTaban - v7Karsiligi);
                decimal toplamDuzeltme = decimal.Round(Math.Min(toplamYukumluluk, azamiDuzeltme), 2);
                if (toplamDuzeltme <= 0) continue;

                // Önce henüz ödenmemiş yapay yükümlülük silinir; kalan düzeltme
                // gerçekten kasadan çıktıysa nakit olarak geri verilir.
                decimal odenemeyenDuzeltme = Math.Min(odenemeyenArtis, toplamDuzeltme);
                durum.OdenemeyenGider = Math.Max(
                    onceki.OdenemeyenGider,
                    durum.OdenemeyenGider - odenemeyenDuzeltme);

                decimal nakitDuzeltmesi = Math.Min(
                    odenmisArtis,
                    Math.Max(0, toplamDuzeltme - odenemeyenDuzeltme));
                if (nakitDuzeltmesi > 0)
                {
                    durum.ToplamIsletmeGideri = Math.Max(
                        onceki.OdenmisGider,
                        durum.ToplamIsletmeGideri - nakitDuzeltmesi);
                    sirket.Kasa += nakitDuzeltmesi;
                }

                durum.SonIslemler.Add(new IsletimIslemKaydi
                {
                    IslemKimligi = $"v7-gider-dengeleme-{Guid.NewGuid():N}",
                    TickNumarasi = tickNumarasi,
                    IslemTuru = "operasyon-maliyet-normalizasyonu",
                    Aciklama = $"Eski hizmet/kapasite sabit maliyetinin {toplamDuzeltme:N2} TL yapay kısmı V7 fiziksel kaynak modeline göre kaldırıldı. Ödenemeyen düzeltme: {odenemeyenDuzeltme:N2} TL; nakit iadesi: {nakitDuzeltmesi:N2} TL. Yatırım bakımı, olaylar, ceza ve finansman giderleri korunur.",
                    Tutar = 0
                });
                if (durum.SonIslemler.Count > 200)
                    durum.SonIslemler = durum.SonIslemler.TakeLast(200).ToList();

                KonsolKayitcisi.Bilgi(
                    $"V7 GİDER DENGESİ | {sirket.SirketAdi} | " +
                    $"Eski yapay taban: {eskiYapayTaban:N2} | V7 karşılığı: {v7Karsiligi:N2} | " +
                    $"Düzeltme: {toplamDuzeltme:N2} | Ödenemeyen silindi: {odenemeyenDuzeltme:N2} | " +
                    $"Nakit iadesi: {nakitDuzeltmesi:N2}");
            }
            await KaydetAsync(cancellationToken);
        }
        finally { kilit.Release(); }
    }

    private static decimal EskiYapaySabitMaliyet(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        int hizmet = sirket.Hizmetler.Count(h => h.Aktif);
        int kapasite = sirket.Hizmetler.Where(h => h.Aktif).Sum(h => h.AzamiEszamanliIs);
        int urun = durum.Urunler.Count(u => u.Aktif);
        int kullanici = durum.Urunler.Where(u => u.Aktif).Sum(u => u.AktifKullaniciSayisi);

        decimal tutar =
            420m +
            hizmet * 42m +
            kapasite * 10m +
            urun * 180m +
            kullanici * 0.48m +
            (decimal)Math.Max(0, durum.TeknikBorc) * 14m +
            (decimal)Math.Max(0, durum.BakimBaskisi) * 8m;

        foreach (UrunKaydi u in durum.Urunler.Where(u => u.Aktif))
        {
            tutar += u.UrunTuru switch
            {
                "isletim-sistemi" => 480m,
                "platform" => 390m,
                "altyapi" => 310m,
                _ => 120m
            };
            tutar += u.Bagimliliklar.Count * 28m + u.DesteklenenPlatformlar.Count * 12m;
        }

        return decimal.Round(Math.Max(0, tutar), 2);
    }

    private static decimal V7SabitMaliyetKarsiligi(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        int aktifHizmet = sirket.Hizmetler.Count(h => h.Aktif);
        int hizmetKapasitesi = sirket.Hizmetler.Where(h => h.Aktif).Sum(h => h.AzamiEszamanliIs);
        int aktifUrun = durum.Urunler.Count(u => u.Aktif);
        int kullanici = durum.Urunler.Where(u => u.Aktif).Sum(u => u.AktifKullaniciSayisi);

        decimal gider =
            35m +
            aktifHizmet * 1.15m +
            hizmetKapasitesi * 0.035m +
            aktifUrun * 18m +
            kullanici * 0.008m +
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
