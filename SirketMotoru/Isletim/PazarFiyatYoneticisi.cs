using System.Reflection;
using System.Text;
using System.Text.Json;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class PazarFiyatDosyasi
{
    public int Surum { get; set; } = 1;
    public long SonTick { get; set; }
    public DateTimeOffset GuncellenmeZamani { get; set; } = DateTimeOffset.UtcNow;
    public List<PazarKategoriDurumu> Kategoriler { get; set; } = [];
    public List<UrunFiyatDurumu> Urunler { get; set; } = [];
}

public sealed class PazarKategoriDurumu
{
    public string PazarKimligi { get; set; } = string.Empty;
    public string Kategori { get; set; } = string.Empty;
    public string UrunTuru { get; set; } = string.Empty;
    public int AktifUrunSayisi { get; set; }
    public int ToplamKullanici { get; set; }
    public decimal ReferansFiyat { get; set; }
    public decimal ErisilebilirAltFiyat { get; set; }
    public decimal ErisilebilirUstFiyat { get; set; }
    public decimal EnUcuzFiyat { get; set; }
    public decimal EnPahaliFiyat { get; set; }
    public int BuTickKacanKullanici { get; set; }
    public int BuTickRakibeGecenKullanici { get; set; }
}

public sealed class UrunFiyatDurumu
{
    public string SirketKimligi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public string UrunKimligi { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public string PazarKimligi { get; set; } = string.Empty;
    public decimal EtkinFiyat { get; set; }
    public decimal AdilFiyat { get; set; }
    public double FiyatOrani { get; set; }
    public double TalepCarpani { get; set; } = 1;
    public double FiyatKayipOrani { get; set; }
    public int BuTickEngellenenYeniKullanici { get; set; }
    public int BuTickFiyatKaybi { get; set; }
    public int BuTickRakiptenGelenKullanici { get; set; }
    public int SonAktifKullaniciSayisi { get; set; }
    public decimal SonEtkinFiyat { get; set; }
    public int FiyatSokuSerisi { get; set; }
    public string PazarDurumu { get; set; } = "normal";
    public long SonTick { get; set; }
}

public static class PazarFiyatDeposu
{
    private static readonly object Kilit = new();
    private static PazarFiyatDosyasi _durum = new();

    public static void Guncelle(PazarFiyatDosyasi durum)
    {
        lock (Kilit)
        {
            string json = JsonSerializer.Serialize(durum);
            _durum = JsonSerializer.Deserialize<PazarFiyatDosyasi>(json) ?? new();
        }
    }

    public static PazarFiyatDosyasi Getir()
    {
        lock (Kilit)
        {
            string json = JsonSerializer.Serialize(_durum);
            return JsonSerializer.Deserialize<PazarFiyatDosyasi>(json) ?? new();
        }
    }

    public static IReadOnlyList<UrunFiyatDurumu> SirketUrunleri(string sirketKimligi) =>
        Getir().Urunler.Where(x => string.Equals(x.SirketKimligi, sirketKimligi, StringComparison.OrdinalIgnoreCase)).ToList().AsReadOnly();
}

public sealed class PazarFiyatYoneticisi : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, decimal> PazarCipalari =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["sosyal-medya"] = 200m,
            ["eposta"] = 120m,
            ["mesajlasma"] = 100m,
            ["bulut-depolama"] = 280m,
            ["api"] = 320m,
            ["analitik"] = 420m,
            ["guvenlik"] = 650m,
            ["e-ticaret"] = 350m,
            ["gelistirici-araci"] = 480m,
            ["oyun"] = 280m,
            ["isletim-sistemi"] = 700m,
            ["platform"] = 450m,
            ["altyapi"] = 1_100m,
            ["veritabani"] = 800m,
            ["diger"] = 250m
        };

    private readonly KodTabanliSirketIsletimYoneticisi _isletim;
    private readonly SirketYoneticisi _sirketler;
    private readonly string _dosyaYolu;
    private readonly Random _rastgele = new(20260722);
    private readonly object _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _kilitAlani;
    private readonly MethodInfo _kaydetMetodu;
    private readonly MethodInfo _degerleMetodu;
    private PazarFiyatDosyasi _durum = new();
    private bool _baslatildi;

    public PazarFiyatYoneticisi(
        KodTabanliSirketIsletimYoneticisi isletim,
        SirketYoneticisi sirketler,
        string motorVerileriKlasoru)
    {
        _isletim = isletim ?? throw new ArgumentNullException(nameof(isletim));
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _dosyaYolu = Path.Combine(Path.GetFullPath(motorVerileriKlasoru), "pazar-fiyat.json");

        FieldInfo temelAlani = typeof(KodTabanliSirketIsletimYoneticisi).GetField("_temel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("İşletim fiyat köprüsü kurulamadı: _temel alanı bulunamadı.");
        _temel = temelAlani.GetValue(_isletim)
            ?? throw new InvalidOperationException("İşletim fiyat köprüsü kurulamadı: temel yönetici boş.");
        Type temelTur = _temel.GetType();
        _veriAlani = temelTur.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("İşletim fiyat köprüsü kurulamadı: _veri alanı bulunamadı.");
        _kilitAlani = temelTur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("İşletim fiyat köprüsü kurulamadı: _kilit alanı bulunamadı.");
        _kaydetMetodu = temelTur.GetMethod("TumunuKaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("İşletim fiyat köprüsü kurulamadı: kayıt metodu bulunamadı.");
        _degerleMetodu = temelTur.GetMethod("Degerle", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("İşletim fiyat köprüsü kurulamadı: değerleme metodu bulunamadı.");
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        if (_baslatildi) return;
        if (File.Exists(_dosyaYolu))
        {
            string json = await File.ReadAllTextAsync(_dosyaYolu, cancellationToken);
            _durum = JsonSerializer.Deserialize<PazarFiyatDosyasi>(json, JsonAyarlari) ?? new();
        }
        _durum.Kategoriler ??= [];
        _durum.Urunler ??= [];
        PazarFiyatDeposu.Guncelle(_durum);
        _baslatildi = true;
        KonsolKayitcisi.Basari("Kategori bazlı fiyat pazarı hazır | Aşırı fiyat kullanıcı kaçışı ve rakibe göç üretir.");
    }

    public async Task TickCalistirAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        if (!_baslatildi) throw new InvalidOperationException("Fiyat pazarı başlatılmadı.");
        SemaphoreSlim temelKilit = (SemaphoreSlim)(_kilitAlani.GetValue(_temel)
            ?? throw new InvalidOperationException("İşletim kilidi bulunamadı."));
        await temelKilit.WaitAsync(cancellationToken);
        try
        {
            SirketIsletimDosyasi veri = (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel)
                ?? throw new InvalidOperationException("İşletim verisi bulunamadı."));
            PazarTickiniIsle(veri, tickNumarasi);
            await KaydetTemelAsync(cancellationToken);
            await KaydetAsync(cancellationToken);
            PazarFiyatDeposu.Guncelle(_durum);
        }
        finally
        {
            temelKilit.Release();
        }
    }

    private void PazarTickiniIsle(SirketIsletimDosyasi veri, long tickNumarasi)
    {
        Dictionary<string, UrunFiyatDurumu> eskiKayitlar = _durum.Urunler
            .ToDictionary(x => x.UrunKimligi, StringComparer.OrdinalIgnoreCase);
        List<UrunBaglami> tumUrunler = [];

        foreach (SirketIsletimDurumu sirketDurumu in veri.Sirketler)
        {
            SirketKaydi? sirket = SirketBul(sirketDurumu.SirketKimligi);
            if (sirket is null) continue;
            foreach (UrunKaydi urun in sirketDurumu.Urunler)
            {
                string pazarKimligi = PazarKimligi(urun);
                tumUrunler.Add(new UrunBaglami
                {
                    Sirket = sirket,
                    Durum = sirketDurumu,
                    Urun = urun,
                    PazarKimligi = pazarKimligi,
                    EtkinFiyat = EtkinFiyat(urun),
                    KaliteSkoru = KaliteSkoru(sirket, urun)
                });
            }
        }

        Dictionary<string, PazarKategoriDurumu> pazarlar = tumUrunler
            .Where(x => x.Urun.Aktif)
            .GroupBy(x => x.PazarKimligi, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => PazarHesapla(g.Key, g.ToList()), StringComparer.OrdinalIgnoreCase);

        List<UrunFiyatDurumu> yeniKayitlar = [];
        List<KacanKullanici> kacislar = [];

        foreach (UrunBaglami baglam in tumUrunler)
        {
            if (!pazarlar.TryGetValue(baglam.PazarKimligi, out PazarKategoriDurumu? pazar))
                pazar = PazarHesapla(baglam.PazarKimligi, [baglam]);

            eskiKayitlar.TryGetValue(baglam.Urun.UrunKimligi, out UrunFiyatDurumu? eski);
            UrunFiyatDurumu kayit = FiyatDurumuHesapla(baglam, pazar, eski, tickNumarasi);

            if (baglam.Urun.Aktif)
                FiyatCezasiniUygula(baglam, kayit, eski, kacislar);
            else
                kayit.PazarDurumu = "pasif";

            kayit.SonAktifKullaniciSayisi = baglam.Urun.AktifKullaniciSayisi;
            kayit.SonEtkinFiyat = baglam.EtkinFiyat;
            yeniKayitlar.Add(kayit);
        }

        RakiplereGocuUygula(tumUrunler, yeniKayitlar, kacislar, pazarlar);

        foreach (UrunFiyatDurumu kayit in yeniKayitlar)
        {
            UrunBaglami? baglam = tumUrunler.FirstOrDefault(x => string.Equals(x.Urun.UrunKimligi, kayit.UrunKimligi, StringComparison.OrdinalIgnoreCase));
            if (baglam is not null) kayit.SonAktifKullaniciSayisi = baglam.Urun.AktifKullaniciSayisi;
        }

        foreach (SirketIsletimDurumu sirketDurumu in veri.Sirketler)
        {
            sirketDurumu.ToplamAboneSayisi = sirketDurumu.Urunler.Where(x => x.Aktif).Sum(x => x.AktifKullaniciSayisi);
            SirketKaydi? sirket = SirketBul(sirketDurumu.SirketKimligi);
            if (sirket is not null)
                _degerleMetodu.Invoke(null, [sirket, sirketDurumu]);
        }

        foreach (PazarKategoriDurumu pazar in pazarlar.Values)
        {
            pazar.ToplamKullanici = tumUrunler.Where(x => x.PazarKimligi.Equals(pazar.PazarKimligi, StringComparison.OrdinalIgnoreCase) && x.Urun.Aktif).Sum(x => x.Urun.AktifKullaniciSayisi);
            pazar.BuTickKacanKullanici = kacislar.Where(x => x.PazarKimligi.Equals(pazar.PazarKimligi, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Kayip);
            pazar.BuTickRakibeGecenKullanici = yeniKayitlar.Where(x => x.PazarKimligi.Equals(pazar.PazarKimligi, StringComparison.OrdinalIgnoreCase)).Sum(x => x.BuTickRakiptenGelenKullanici);
        }

        _durum.Surum = 1;
        _durum.SonTick = tickNumarasi;
        _durum.GuncellenmeZamani = DateTimeOffset.UtcNow;
        _durum.Kategoriler = pazarlar.Values.OrderBy(x => x.PazarKimligi, StringComparer.OrdinalIgnoreCase).ToList();
        _durum.Urunler = yeniKayitlar.OrderBy(x => x.PazarKimligi, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.EtkinFiyat).ToList();
    }

    private UrunFiyatDurumu FiyatDurumuHesapla(
        UrunBaglami baglam,
        PazarKategoriDurumu pazar,
        UrunFiyatDurumu? eski,
        long tickNumarasi)
    {
        double kaliteCarpani = Math.Clamp(0.62 + baglam.KaliteSkoru / 100d * 0.78, 0.62, 1.40);
        double markaCarpani = Math.Clamp(0.76 + baglam.Sirket.ItibarPuani / 100d * 0.46, 0.76, 1.22);
        decimal adilFiyat = decimal.Round(pazar.ReferansFiyat * (decimal)(kaliteCarpani * markaCarpani), 2);
        adilFiyat = Math.Max(0.01m, adilFiyat);
        double oran = (double)(baglam.EtkinFiyat / adilFiyat);
        double talep = TalepCarpani(oran);
        double kayip = FiyatKayipOrani(oran);
        decimal oncekiFiyat = eski?.SonEtkinFiyat ?? baglam.EtkinFiyat;
        double fiyatArtisi = oncekiFiyat <= 0 ? 1 : (double)(baglam.EtkinFiyat / Math.Max(0.01m, oncekiFiyat));
        if (fiyatArtisi > 1.5)
            kayip += Math.Clamp((fiyatArtisi - 1.5) * 0.035, 0, 0.28);

        int seri = eski?.FiyatSokuSerisi ?? 0;
        seri = oran > 1.75 || fiyatArtisi > 1.8 ? Math.Min(100, seri + 1) : Math.Max(0, seri - 1);
        kayip = Math.Clamp(kayip + Math.Min(0.16, seri * 0.012), 0, 0.98);

        return new UrunFiyatDurumu
        {
            SirketKimligi = baglam.Sirket.SirketKimligi,
            SirketAdi = baglam.Sirket.SirketAdi,
            UrunKimligi = baglam.Urun.UrunKimligi,
            UrunAdi = baglam.Urun.UrunAdi,
            PazarKimligi = baglam.PazarKimligi,
            EtkinFiyat = baglam.EtkinFiyat,
            AdilFiyat = adilFiyat,
            FiyatOrani = oran,
            TalepCarpani = talep,
            FiyatKayipOrani = kayip,
            FiyatSokuSerisi = seri,
            PazarDurumu = PazarDurumu(oran),
            SonTick = tickNumarasi
        };
    }

    private void FiyatCezasiniUygula(
        UrunBaglami baglam,
        UrunFiyatDurumu kayit,
        UrunFiyatDurumu? eski,
        List<KacanKullanici> kacislar)
    {
        int onceki = eski?.SonAktifKullaniciSayisi ?? baglam.Urun.AktifKullaniciSayisi;
        int mevcut = baglam.Urun.AktifKullaniciSayisi;
        int buTickBuyume = Math.Max(0, mevcut - onceki);
        int engellenenYeni = (int)Math.Ceiling(buTickBuyume * Math.Clamp(1 - kayit.TalepCarpani, 0, 1));
        engellenenYeni = Math.Min(mevcut, engellenenYeni);
        baglam.Urun.AktifKullaniciSayisi -= engellenenYeni;
        baglam.Urun.ToplamKaybedilenKullanici += engellenenYeni;
        kayit.BuTickEngellenenYeniKullanici = engellenenYeni;

        int fiyatKaybi = (int)Math.Ceiling(
            baglam.Urun.AktifKullaniciSayisi *
            Math.Clamp(kayit.FiyatKayipOrani * (0.86 + _rastgele.NextDouble() * 0.28), 0, 0.98));
        fiyatKaybi = Math.Min(baglam.Urun.AktifKullaniciSayisi, fiyatKaybi);
        baglam.Urun.AktifKullaniciSayisi -= fiyatKaybi;
        baglam.Urun.ToplamKaybedilenKullanici += fiyatKaybi;
        kayit.BuTickFiyatKaybi = fiyatKaybi;

        if (fiyatKaybi > 0)
            kacislar.Add(new KacanKullanici(baglam.PazarKimligi, baglam.Urun.UrunKimligi, fiyatKaybi));

        if (kayit.FiyatOrani > 1.35)
        {
            double agirlik = Math.Clamp(Math.Log10(Math.Max(1.01, kayit.FiyatOrani)) * 0.55, 0.04, 2.8);
            baglam.Sirket.ItibarPuani = Math.Max(0, baglam.Sirket.ItibarPuani - agirlik);
            baglam.Sirket.OrtalamaMusteriMemnuniyeti = Math.Max(0, baglam.Sirket.OrtalamaMusteriMemnuniyeti - agirlik * 0.75);
            baglam.Urun.UrunMemnuniyeti = Math.Max(0, baglam.Urun.UrunMemnuniyeti - Math.Min(24, 1.5 + agirlik * 5));
        }
        else if (kayit.FiyatOrani is >= 0.65 and <= 1.10)
        {
            baglam.Urun.UrunMemnuniyeti = Math.Min(98, baglam.Urun.UrunMemnuniyeti + 0.08);
        }

        if (kayit.FiyatOrani > 2.25 && (kayit.FiyatSokuSerisi == 1 || kayit.FiyatSokuSerisi % 5 == 0))
        {
            string aciklama = $"{baglam.Urun.UrunAdi} fiyatı pazarın adil seviyesinin {kayit.FiyatOrani:N1} katına çıktı. " +
                              $"Bu tick {engellenenYeni} yeni kullanıcı reddetti, {fiyatKaybi} kullanıcı ayrıldı.";
            baglam.Durum.SonOlaylar.Add(new SirketOlayKaydi
            {
                OlayKimligi = $"fiyat-soku-{Guid.NewGuid():N}",
                TickNumarasi = kayit.SonTick,
                Tur = "olumsuz",
                Baslik = "Fiyat şoku ve müşteri kaçışı",
                Aciklama = aciklama,
                EtkilenenVarlik = baglam.Urun.UrunKimligi,
                ItibarEtkisi = -Math.Min(3, Math.Log10(Math.Max(2, kayit.FiyatOrani))),
                Olumlu = false
            });
            if (baglam.Durum.SonOlaylar.Count > 100)
                baglam.Durum.SonOlaylar = baglam.Durum.SonOlaylar.TakeLast(100).ToList();
            KonsolKayitcisi.Uyari($"FİYAT ŞOKU | {baglam.Sirket.SirketAdi} | {baglam.Urun.UrunAdi} | Oran ×{kayit.FiyatOrani:N1} | Kayıp {fiyatKaybi}");
        }
    }

    private void RakiplereGocuUygula(
        IReadOnlyList<UrunBaglami> urunler,
        IReadOnlyList<UrunFiyatDurumu> kayitlar,
        IReadOnlyList<KacanKullanici> kacislar,
        IReadOnlyDictionary<string, PazarKategoriDurumu> pazarlar)
    {
        Dictionary<string, UrunFiyatDurumu> kayitIndeksi = kayitlar.ToDictionary(x => x.UrunKimligi, StringComparer.OrdinalIgnoreCase);
        foreach (KacanKullanici kacis in kacislar)
        {
            if (!pazarlar.TryGetValue(kacis.PazarKimligi, out PazarKategoriDurumu? pazar)) continue;
            List<(UrunBaglami Urun, UrunFiyatDurumu Fiyat, double Agirlik)> adaylar = [];
            foreach (UrunBaglami aday in urunler.Where(x => x.Urun.Aktif && x.PazarKimligi.Equals(kacis.PazarKimligi, StringComparison.OrdinalIgnoreCase) && !x.Urun.UrunKimligi.Equals(kacis.KaynakUrunKimligi, StringComparison.OrdinalIgnoreCase)))
            {
                if (!kayitIndeksi.TryGetValue(aday.Urun.UrunKimligi, out UrunFiyatDurumu? fiyat)) continue;
                int bosluk = aday.Urun.KullaniciKapasitesi - aday.Urun.AktifKullaniciSayisi;
                if (bosluk <= 0 || fiyat.FiyatOrani > 1.75 || fiyat.TalepCarpani <= 0.05) continue;
                double deger = aday.KaliteSkoru / 100d * Math.Clamp(1.35 - fiyat.FiyatOrani * 0.35, 0.08, 1.3);
                double kapasite = Math.Clamp((double)bosluk / Math.Max(1, aday.Urun.KullaniciKapasitesi), 0.05, 1);
                adaylar.Add((aday, fiyat, Math.Max(0.001, deger * Math.Sqrt(kapasite))));
            }
            if (adaylar.Count == 0) continue;

            int gocHavuzu = (int)Math.Floor(kacis.Kayip * Math.Clamp(0.48 + adaylar.Count * 0.07, 0.48, 0.82));
            for (int i = 0; i < gocHavuzu; i++)
            {
                double toplamAgirlik = adaylar.Sum(x => x.Agirlik);
                if (toplamAgirlik <= 0) break;
                double secim = _rastgele.NextDouble() * toplamAgirlik;
                int secilenIndex = adaylar.Count - 1;
                for (int a = 0; a < adaylar.Count; a++)
                {
                    secim -= adaylar[a].Agirlik;
                    if (secim <= 0) { secilenIndex = a; break; }
                }
                var secilen = adaylar[secilenIndex];
                if (secilen.Urun.Urun.AktifKullaniciSayisi >= secilen.Urun.Urun.KullaniciKapasitesi)
                {
                    adaylar.RemoveAt(secilenIndex);
                    i--;
                    continue;
                }
                secilen.Urun.Urun.AktifKullaniciSayisi++;
                secilen.Urun.Urun.ToplamEdinilenKullanici++;
                secilen.Fiyat.BuTickRakiptenGelenKullanici++;
            }
        }
    }

    private PazarKategoriDurumu PazarHesapla(string pazarKimligi, IReadOnlyList<UrunBaglami> urunler)
    {
        string[] parcalar = pazarKimligi.Split('|', 2);
        string tur = parcalar[0];
        string kategori = parcalar.Length > 1 ? parcalar[1] : "diger";
        decimal cipa = CipaFiyati(tur, kategori);
        List<decimal> fiyatlar = urunler.Where(x => x.Urun.Aktif).Select(x => x.EtkinFiyat).Where(x => x > 0).OrderBy(x => x).ToList();
        List<decimal> kirpilmis = fiyatlar.Select(x => Math.Clamp(x, Math.Max(0.01m, cipa * 0.04m), cipa * 8m)).OrderBy(x => x).ToList();
        decimal p40 = kirpilmis.Count == 0 ? cipa : Yuzdelik(kirpilmis, 0.40);
        decimal pazarAgirligi = kirpilmis.Count switch { 0 => 0m, 1 => 0.25m, 2 => 0.42m, _ => 0.68m };
        decimal referans = decimal.Round(cipa * (1 - pazarAgirligi) + p40 * pazarAgirligi, 2);
        referans = Math.Clamp(referans, cipa * 0.25m, cipa * 3m);
        return new PazarKategoriDurumu
        {
            PazarKimligi = pazarKimligi,
            Kategori = kategori,
            UrunTuru = tur,
            AktifUrunSayisi = urunler.Count(x => x.Urun.Aktif),
            ToplamKullanici = urunler.Where(x => x.Urun.Aktif).Sum(x => x.Urun.AktifKullaniciSayisi),
            ReferansFiyat = referans,
            ErisilebilirAltFiyat = decimal.Round(referans * 0.55m, 2),
            ErisilebilirUstFiyat = decimal.Round(referans * 1.45m, 2),
            EnUcuzFiyat = fiyatlar.Count == 0 ? 0 : fiyatlar[0],
            EnPahaliFiyat = fiyatlar.Count == 0 ? 0 : fiyatlar[^1]
        };
    }

    private static decimal EtkinFiyat(UrunKaydi urun) => urun.FiyatlandirmaModeli switch
    {
        "abonelik" => Math.Max(0, urun.AbonelikUcreti),
        "freemium" => Math.Max(0, urun.AbonelikUcreti * 0.28m + urun.KullanimBasinaUcret * 30m),
        "kullanim" => Math.Max(0, urun.KullanimBasinaUcret * 60m),
        "lisans" or "tek-seferlik" => Math.Max(urun.AbonelikUcreti, urun.KullanimBasinaUcret) / 18m,
        _ => Math.Max(0, urun.AbonelikUcreti + urun.KullanimBasinaUcret * 20m)
    };

    private static double KaliteSkoru(SirketKaydi sirket, UrunKaydi urun) => Math.Clamp(
        urun.UrunKalitesi * 0.35 + sirket.KodKalitesiPuani * 0.20 + sirket.PerformansPuani * 0.16 +
        sirket.GuvenlikPuani * 0.14 + sirket.GuvenilirlikPuani * 0.08 + urun.UrunMemnuniyeti * 0.07,
        1, 100);

    private static double TalepCarpani(double oran) => oran switch
    {
        <= 0.35 => 1.34,
        <= 0.65 => 1.20,
        <= 0.90 => 1.08,
        <= 1.10 => 1.00,
        <= 1.30 => 0.78,
        <= 1.55 => 0.48,
        <= 1.85 => 0.20,
        <= 2.25 => 0.055,
        <= 3.00 => 0.008,
        _ => 0
    };

    private static double FiyatKayipOrani(double oran) => oran switch
    {
        <= 1.10 => 0,
        <= 1.30 => 0.008 + (oran - 1.10) * 0.05,
        <= 1.55 => 0.018 + (oran - 1.30) * 0.12,
        <= 1.85 => 0.06 + (oran - 1.55) * 0.30,
        <= 2.25 => 0.16 + (oran - 1.85) * 0.55,
        <= 3.00 => 0.38 + (oran - 2.25) * 0.48,
        <= 5.00 => 0.74 + (oran - 3.00) * 0.08,
        <= 20.0 => 0.90,
        _ => 0.97
    };

    private static string PazarDurumu(double oran) => oran switch
    {
        <= 0.35 => "çok-ucuz",
        <= 0.65 => "ucuz",
        <= 1.10 => "rekabetçi",
        <= 1.30 => "pahalı",
        <= 1.85 => "çok-pahalı",
        <= 3.00 => "fiyat-şoku",
        _ => "pazar-dışı"
    };

    private static string PazarKimligi(UrunKaydi urun)
    {
        string tur = Normal(urun.UrunTuru, "uygulama");
        string kategori = Normal(urun.Kategori, "diger");
        return $"{tur}|{kategori}";
    }

    private static decimal CipaFiyati(string tur, string kategori)
    {
        if (PazarCipalari.TryGetValue(tur, out decimal turCipasi) && tur is "isletim-sistemi" or "platform" or "altyapi" or "veritabani" or "oyun")
            return turCipasi;
        return PazarCipalari.TryGetValue(kategori, out decimal kategoriCipasi) ? kategoriCipasi : PazarCipalari["diger"];
    }

    private static decimal Yuzdelik(IReadOnlyList<decimal> sirali, double oran)
    {
        if (sirali.Count == 0) return 0;
        if (sirali.Count == 1) return sirali[0];
        double konum = Math.Clamp(oran, 0, 1) * (sirali.Count - 1);
        int alt = (int)Math.Floor(konum), ust = (int)Math.Ceiling(konum);
        if (alt == ust) return sirali[alt];
        decimal pay = (decimal)(konum - alt);
        return sirali[alt] * (1 - pay) + sirali[ust] * pay;
    }

    private SirketKaydi? SirketBul(string kimlik) =>
        _sirketler.SirketKayitlari.FirstOrDefault(x => string.Equals(x.SirketKimligi, kimlik, StringComparison.OrdinalIgnoreCase));

    private async Task KaydetTemelAsync(CancellationToken cancellationToken)
    {
        try
        {
            object? sonuc = _kaydetMetodu.Invoke(_temel, [cancellationToken]);
            if (sonuc is Task task) await task;
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            throw e.InnerException;
        }
    }

    private async Task KaydetAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        string gecici = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(gecici, JsonSerializer.Serialize(_durum, JsonAyarlari), new UTF8Encoding(false), cancellationToken);
        File.Move(gecici, _dosyaYolu, true);
    }

    private static string Normal(string? deger, string varsayilan) =>
        string.IsNullOrWhiteSpace(deger) ? varsayilan : deger.Trim().ToLowerInvariant();

    public async ValueTask DisposeAsync()
    {
        if (!_baslatildi) return;
        await KaydetAsync(CancellationToken.None);
    }

    private sealed class UrunBaglami
    {
        public required SirketKaydi Sirket { get; init; }
        public required SirketIsletimDurumu Durum { get; init; }
        public required UrunKaydi Urun { get; init; }
        public required string PazarKimligi { get; init; }
        public decimal EtkinFiyat { get; init; }
        public double KaliteSkoru { get; init; }
    }

    private sealed record KacanKullanici(string PazarKimligi, string KaynakUrunKimligi, int Kayip);
}
