using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class SirketIsletimYoneticisi : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly YatirimPaketi[] Yatirimlar =
    [
        new() { YatirimTuru = "cpu", Ad = "İşlemci Kümesi", Aciklama = "Hizmet eşzamanlı kapasitesini ve uygulama işlem gücünü artırır.", AzamiSeviye = 20, TabanMaliyet = 4_500 },
        new() { YatirimTuru = "ram", Ad = "Bellek Havuzu", Aciklama = "Hizmet ve aktif kullanıcı kapasitesini artırır.", AzamiSeviye = 20, TabanMaliyet = 3_800 },
        new() { YatirimTuru = "ag", Ad = "Ağ Omurgası", Aciklama = "Eşzamanlı iş, kullanıcı ve dış bağlantı kapasitesini artırır.", AzamiSeviye = 20, TabanMaliyet = 4_200 },
        new() { YatirimTuru = "depolama", Ad = "Depolama Kümesi", Aciklama = "Veri ağırlıklı ürünlerin kullanıcı kapasitesini artırır.", AzamiSeviye = 20, TabanMaliyet = 3_200 },
        new() { YatirimTuru = "guvenlik", Ad = "Güvenlik Operasyonu", Aciklama = "Olay olasılığı ve güvenlik kaybını azaltır; sıfırlamaz.", AzamiSeviye = 12, TabanMaliyet = 6_500 },
        new() { YatirimTuru = "yedek", Ad = "Yedek Sunucu", Aciklama = "Kesinti ve bağımlılık arızalarının etkisini azaltır.", AzamiSeviye = 8, TabanMaliyet = 12_000 },
        new() { YatirimTuru = "destek", Ad = "Müşteri Destek Ekibi", Aciklama = "Kullanıcı kaybını ve memnuniyet çöküşünü azaltır.", AzamiSeviye = 12, TabanMaliyet = 5_500 },
        new() { YatirimTuru = "pazarlama", Ad = "Pazarlama Departmanı", Aciklama = "Talebi artırır; gider ve operasyon baskısı da üretir.", AzamiSeviye = 12, TabanMaliyet = 7_000 },
        new() { YatirimTuru = "satis", Ad = "Kurumsal Satış", Aciklama = "SLA ve kurumsal müşteri kazanımını artırır.", AzamiSeviye = 12, TabanMaliyet = 8_000 },
        new() { YatirimTuru = "arge", Ad = "Ar-Ge Laboratuvarı", Aciklama = "Ürün kalitesi ve teknik borç yönetimine katkı sağlar.", AzamiSeviye = 12, TabanMaliyet = 9_500 }
    ];

    private static readonly KrediPaketi[] Krediler =
    [
        new() { KrediTuru = "isletme", Ad = "İşletme Kredisi", AsgariTutar = 2_000, AzamiTutar = 100_000, TickFaizOrani = 0.009m, TaksitSayisi = 12, OdemeAraligiTick = 5, AsgariKrediNotu = 450 },
        new() { KrediTuru = "altyapi", Ad = "Altyapı Kredisi", AsgariTutar = 10_000, AzamiTutar = 300_000, TickFaizOrani = 0.007m, TaksitSayisi = 18, OdemeAraligiTick = 5, AsgariKrediNotu = 550 },
        new() { KrediTuru = "arge", Ad = "Ar-Ge Kredisi", AsgariTutar = 20_000, AzamiTutar = 400_000, TickFaizOrani = 0.006m, TaksitSayisi = 22, OdemeAraligiTick = 5, AsgariKrediNotu = 650 },
        new() { KrediTuru = "acil", Ad = "Acil Likidite", AsgariTutar = 1_000, AzamiTutar = 40_000, TickFaizOrani = 0.018m, TaksitSayisi = 8, OdemeAraligiTick = 3, AsgariKrediNotu = 300 }
    ];

    private readonly SirketYoneticisi _sirketler;
    private readonly string _dosyaYolu;
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private readonly Random _rastgele = new(20260719);
    private SirketIsletimDosyasi _veri = new();
    private long _tick;
    private bool _baslatildi;
    private bool _disposed;

    public string DosyaYolu => _dosyaYolu;

    public SirketIsletimYoneticisi(SirketYoneticisi sirketler, string motorVerileriKlasoru)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _dosyaYolu = Path.Combine(Path.GetFullPath(motorVerileriKlasoru), "sirket-isletim.json");
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            if (_baslatildi) return;
            if (File.Exists(_dosyaYolu))
            {
                string json = await File.ReadAllTextAsync(_dosyaYolu, cancellationToken);
                _veri = JsonSerializer.Deserialize<SirketIsletimDosyasi>(json, JsonAyarlari) ?? new();
            }

            _veri.Surum = 5;
            _veri.Hesaplar ??= [];
            _veri.Sirketler ??= [];
            _veri.Protokoller ??= [];
            _veri.SozlesmeTeklifleri ??= [];
            _veri.PiyasaOlaylari ??= [];
            _tick = Math.Max(0, _veri.SonIslenenTick);
            KayitlariTamamla();
            HizmetleriKaliciKayitlaVeUygula();
            UrunKapasiteleriniYenile();
            await KaydetKilitsizAsync(cancellationToken);
            _baslatildi = true;
            KonsolKayitcisi.Basari("Kalıcı otoriteli işletim sistemi hazır | Fiyat, aktiflik ve kapasite motor kaydından uygulanacak.");
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> GirisDogrulaAsync(GirisIstegi istek, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            SirketHesabi? hesap = _veri.Hesaplar.FirstOrDefault(h =>
                string.Equals(h.KullaniciAdi, istek.KullaniciAdi?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (hesap is null || !SabitZamanliEsit(hesap.ParolaOzeti, Ozetle(hesap.ParolaTuzu, istek.Parola ?? string.Empty)))
                return IslemSonucu.Hata("Kullanıcı adı veya parola hatalı.");
            hesap.SonGirisZamani = DateTimeOffset.UtcNow;
            await KaydetKilitsizAsync(cancellationToken);
            SirketKaydi? sirket = SirketBul(hesap.SirketKimligi);
            return IslemSonucu.Basari("Giriş başarılı.", new
            {
                hesap.SirketKimligi,
                sirketAdi = sirket?.SirketAdi ?? hesap.SirketKimligi,
                hesap.ParolaDegistirilmeli
            });
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> ParolaDegistirAsync(string sirketKimligi, ParolaDegistirIstegi istek, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            SirketHesabi? hesap = _veri.Hesaplar.FirstOrDefault(h => string.Equals(h.SirketKimligi, sirketKimligi, StringComparison.OrdinalIgnoreCase));
            if (hesap is null || !SabitZamanliEsit(hesap.ParolaOzeti, Ozetle(hesap.ParolaTuzu, istek.EskiParola ?? string.Empty)))
                return IslemSonucu.Hata("Mevcut parola hatalı.");
            if (istek.YeniParola is null || istek.YeniParola.Length is < 8 or > 128)
                return IslemSonucu.Hata("Yeni parola 8-128 karakter olmalıdır.");
            hesap.ParolaTuzu = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            hesap.ParolaOzeti = Ozetle(hesap.ParolaTuzu, istek.YeniParola);
            hesap.ParolaDegistirilmeli = false;
            await KaydetKilitsizAsync(cancellationToken);
            return IslemSonucu.Basari("Parola değiştirildi.");
        }
        finally { _kilit.Release(); }
    }

    public Task<IslemSonucu> YatirimSatinAlAsync(string sirketKimligi, YatirimIstegi istek, CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            YatirimPaketi? paket = Yatirimlar.FirstOrDefault(p => string.Equals(p.YatirimTuru, istek.YatirimTuru?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (paket is null) return IslemSonucu.Hata("Bilinmeyen yatırım.");
            int seviye = Seviye(durum, paket.YatirimTuru);
            if (seviye >= paket.AzamiSeviye) return IslemSonucu.Hata("Azami seviyeye ulaşıldı.");
            decimal maliyet = Maliyet(paket, seviye);
            if (sirket.Kasa < maliyet) return IslemSonucu.Hata($"Yetersiz kasa. Gerekli: {maliyet:N2} TL.");
            sirket.Kasa -= maliyet;
            durum.YatirimSeviyeleri[paket.YatirimTuru] = seviye + 1;
            durum.ToplamYatirimHarcamasi += maliyet;
            durum.SonYonetimIslemiTicki = _tick;
            durum.TeknikBorc = Math.Max(0, durum.TeknikBorc - (paket.YatirimTuru == "arge" ? 2.2 : 0.25));
            IslemEkle(durum, "yatirim", $"{paket.Ad} seviyesi {seviye + 1} oldu.", -maliyet);
            HizmetleriKaliciKayitlaVeUygula();
            UrunKapasiteleriniYenile();
            Degerle(sirket, durum);
            return IslemSonucu.Basari($"Yatırım tamamlandı. Etkin kapasite ve işletme davranışı hemen güncellendi.");
        });

    public Task<IslemSonucu> HizmetFiyatiGuncelleAsync(string sirketKimligi, FiyatGuncelleIstegi istek, CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            if (istek.YeniFiyat is <= 0 or > 1_000_000) return IslemSonucu.Hata("Fiyat geçersiz.");
            string anahtar = Anahtar(istek.HizmetKimligi, istek.HizmetSurumu);
            HizmetKaliciAyari? ayar = HizmetAyariniBul(durum, anahtar);
            if (ayar is null || !sirket.Hizmetler.Any(h => Anahtar(h) == anahtar)) return IslemSonucu.Hata("Hizmet ilan edilmiyor.");
            ayar.YonetilenFiyat = decimal.Round(istek.YeniFiyat, 2);
            ayar.FiyatYonetildi = true;
            durum.HizmetFiyatEzmeDegerleri[anahtar] = ayar.YonetilenFiyat;
            durum.SonYonetimIslemiTicki = _tick;
            HizmetleriKaliciKayitlaVeUygula();
            IslemEkle(durum, "hizmet-fiyat", $"{anahtar} kalıcı fiyatı {ayar.YonetilenFiyat:N2} TL oldu.", 0);
            return IslemSonucu.Basari("Fiyat motorun kalıcı kaydına işlendi; yeniden bağlantıda da korunacak.");
        });

    public Task<IslemSonucu> HizmetYayinDurumuGuncelleAsync(string sirketKimligi, HizmetYayinDurumuIstegi istek, CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            string anahtar = Anahtar(istek.HizmetKimligi, istek.HizmetSurumu);
            HizmetKaliciAyari? ayar = HizmetAyariniBul(durum, anahtar);
            if (ayar is null || !sirket.Hizmetler.Any(h => Anahtar(h) == anahtar)) return IslemSonucu.Hata("Hizmet ilan edilmiyor.");
            ayar.YonetilenAktiflik = istek.Aktif;
            ayar.AktiflikYonetildi = true;
            HizmetleriKaliciKayitlaVeUygula();
            IslemEkle(durum, "hizmet-durum", $"{anahtar} {(istek.Aktif ? "aktif" : "pasif")} yapıldı.", 0);
            return IslemSonucu.Basari(istek.Aktif ? "Hizmet kalıcı olarak yayına alındı." : "Hizmet kalıcı olarak durduruldu.");
        });

    public Task<IslemSonucu> HizmetKapasitesiArtirAsync(string sirketKimligi, HizmetKapasiteIstegi istek, CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            if (istek.EklenecekKapasite is < 1 or > 500) return IslemSonucu.Hata("Kapasite artışı 1-500 arasında olmalıdır.");
            string anahtar = Anahtar(istek.HizmetKimligi, istek.HizmetSurumu);
            HizmetKaliciAyari? ayar = HizmetAyariniBul(durum, anahtar);
            if (ayar is null || !sirket.Hizmetler.Any(h => Anahtar(h) == anahtar)) return IslemSonucu.Hata("Hizmet ilan edilmiyor.");
            decimal birimMaliyet = 850m + (ayar.SunucudanGelenIlkKapasite + ayar.SatinAlinanKapasite) * 38m;
            decimal maliyet = decimal.Round(istek.EklenecekKapasite * birimMaliyet, 2);
            if (sirket.Kasa < maliyet) return IslemSonucu.Hata($"Yetersiz kasa. Gerekli: {maliyet:N2} TL.");
            sirket.Kasa -= maliyet;
            ayar.SatinAlinanKapasite += istek.EklenecekKapasite;
            durum.ToplamYatirimHarcamasi += maliyet;
            HizmetleriKaliciKayitlaVeUygula();
            IslemEkle(durum, "hizmet-kapasite", $"{anahtar} için +{istek.EklenecekKapasite} kalıcı kapasite alındı.", -maliyet);
            return IslemSonucu.Basari("Hizmet kapasitesi motor kaydında artırıldı.");
        });

    public Task<IslemSonucu> KrediCekAsync(string sirketKimligi, KrediIstegi istek, CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            KrediPaketi? paket = Krediler.FirstOrDefault(p => string.Equals(p.KrediTuru, istek.KrediTuru, StringComparison.OrdinalIgnoreCase));
            if (paket is null) return IslemSonucu.Hata("Kredi paketi bulunamadı.");
            if (durum.KrediNotu < paket.AsgariKrediNotu) return IslemSonucu.Hata("Kredi notu yetersiz.");
            if (istek.Tutar < paket.AsgariTutar || istek.Tutar > paket.AzamiTutar) return IslemSonucu.Hata("Kredi tutarı paket sınırlarının dışında.");
            decimal mevcutBorc = durum.Krediler.Where(k => k.Aktif).Sum(k => k.KalanBorc);
            decimal limit = Math.Max(20_000, durum.SirketDegeri * 0.55m + Math.Max(0, sirket.Kasa) * 0.5m);
            if (mevcutBorc + istek.Tutar > limit) return IslemSonucu.Hata($"Borç limiti aşılıyor. Limit: {limit:N2} TL.");
            decimal toplam = decimal.Round(istek.Tutar * (1 + paket.TickFaizOrani * paket.TaksitSayisi), 2);
            KrediKaydi kredi = new()
            {
                KrediKimligi = $"kredi-{Guid.NewGuid():N}", KrediTuru = paket.KrediTuru,
                AnaPara = decimal.Round(istek.Tutar, 2), KalanBorc = toplam,
                TickFaizOrani = paket.TickFaizOrani, TaksitTutari = decimal.Round(toplam / paket.TaksitSayisi, 2),
                KalanTaksit = paket.TaksitSayisi, SonrakiOdemeTicki = _tick + paket.OdemeAraligiTick,
                OdemeAraligiTick = paket.OdemeAraligiTick
            };
            durum.Krediler.Add(kredi);
            sirket.Kasa += kredi.AnaPara;
            durum.KrediNotu = Math.Clamp(durum.KrediNotu - 4, 300, 900);
            IslemEkle(durum, "kredi", $"{paket.Ad}: {kredi.AnaPara:N2} TL.", kredi.AnaPara);
            Degerle(sirket, durum);
            return IslemSonucu.Basari("Kredi kasaya aktarıldı.", kredi);
        });

    public Task<IslemSonucu> UrunOlusturAsync(string sirketKimligi, UrunOlusturIstegi istek, CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            string ad = istek.UrunAdi?.Trim() ?? string.Empty;
            string kimlik = istek.UygulamaKimligi?.Trim() ?? string.Empty;
            string kategori = Normal(istek.Kategori, "diger");
            string tur = Normal(istek.UrunTuru, "uygulama");
            string model = Normal(istek.FiyatlandirmaModeli, "abonelik");
            if (ad.Length is < 2 or > 100) return IslemSonucu.Hata("Ürün adı geçersiz.");
            if (model is not ("abonelik" or "kullanim" or "freemium" or "lisans" or "tek-seferlik")) return IslemSonucu.Hata("Fiyatlandırma modeli geçersiz.");
            if (durum.Urunler.Any(u => (!string.IsNullOrWhiteSpace(kimlik) && string.Equals(u.UygulamaKimligi, kimlik, StringComparison.OrdinalIgnoreCase)) || string.Equals(u.UrunAdi, ad, StringComparison.OrdinalIgnoreCase)))
                return IslemSonucu.Hata("Bu uygulama veya ürün zaten piyasada.");
            if (string.IsNullOrWhiteSpace(istek.ArkaUcHizmetKimligi) || !sirket.Hizmetler.Any(h =>
                    string.Equals(h.HizmetKimligi, istek.ArkaUcHizmetKimligi, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(h.HizmetSurumu, istek.ArkaUcHizmetSurumu, StringComparison.OrdinalIgnoreCase)))
                return IslemSonucu.Hata("Ürün gerçek ve ilan edilmiş bir arka uç hizmetine bağlanmalıdır.");

            decimal yayinMaliyeti = YayinMaliyeti(tur, kategori, istek.DesteklenenPlatformlar.Count, istek.Bagimliliklar.Count);
            yayinMaliyeti *= Math.Max(0.72m, 1 - Seviye(durum, "arge") * 0.02m);
            yayinMaliyeti = decimal.Round(yayinMaliyeti, 2);
            if (sirket.Kasa < yayinMaliyeti) return IslemSonucu.Hata($"Ticari yayın ve operasyon hazırlık maliyeti: {yayinMaliyeti:N2} TL.");

            int taban = tur switch
            {
                "isletim-sistemi" => 300,
                "platform" => 250,
                "altyapi" => 180,
                "oyun" => 220,
                _ => 150
            };
            UrunKaydi urun = new()
            {
                UrunKimligi = $"urun-{Guid.NewGuid():N}", UygulamaKimligi = kimlik,
                UrunAdi = ad, Kategori = kategori, UrunTuru = tur, FiyatlandirmaModeli = model,
                AbonelikUcreti = decimal.Round(Math.Max(0, istek.AbonelikUcreti), 2),
                KullanimBasinaUcret = decimal.Round(Math.Max(0, istek.KullanimBasinaUcret), 4),
                TabanKullaniciKapasitesi = taban, KullaniciKapasitesi = taban,
                UrunKalitesi = Math.Clamp(42 + sirket.KodKalitesiPuani * 0.32 + Seviye(durum, "arge") * 1.2 - durum.TeknikBorc * 0.15, 20, 94),
                UrunMemnuniyeti = 52, ArkaUcHizmetKimligi = istek.ArkaUcHizmetKimligi.Trim(),
                ArkaUcHizmetSurumu = string.IsNullOrWhiteSpace(istek.ArkaUcHizmetSurumu) ? "1.0" : istek.ArkaUcHizmetSurumu.Trim(),
                ProtokolKimligi = istek.ProtokolKimligi?.Trim() ?? string.Empty,
                DesteklenenPlatformlar = istek.DesteklenenPlatformlar.Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList(),
                GerekliPlatformlar = istek.GerekliPlatformlar.Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList(),
                Bagimliliklar = istek.Bagimliliklar.Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToList(),
                YayinTicki = _tick
            };
            sirket.Kasa -= yayinMaliyeti;
            durum.ToplamYatirimHarcamasi += yayinMaliyeti;
            durum.Urunler.Add(urun);
            durum.TeknikBorc += 1.2 + urun.Bagimliliklar.Count * 0.2;
            UrunKapasitesiniYenile(durum, urun);
            IslemEkle(durum, "urun-yayin", $"{urun.UrunAdi} ({urun.UrunTuru}) piyasaya çıktı.", -yayinMaliyeti);
            Degerle(sirket, durum);
            return IslemSonucu.Basari("Kodlanmış ürün piyasaya çıkarıldı.", urun);
        });

    public Task<IslemSonucu> UrunGuncelleAsync(string sirketKimligi, UrunGuncelleIstegi istek, CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (_, durum) =>
        {
            UrunKaydi? urun = durum.Urunler.FirstOrDefault(u => string.Equals(u.UrunKimligi, istek.UrunKimligi, StringComparison.OrdinalIgnoreCase));
            if (urun is null) return IslemSonucu.Hata("Ürün bulunamadı.");
            if (istek.AbonelikUcreti is < 0 or > 1_000_000 || istek.KullanimBasinaUcret is < 0 or > 100_000) return IslemSonucu.Hata("Ürün fiyatı geçersiz.");
            urun.AbonelikUcreti = decimal.Round(istek.AbonelikUcreti, 2);
            urun.KullanimBasinaUcret = decimal.Round(istek.KullanimBasinaUcret, 4);
            urun.Aktif = istek.Aktif;
            IslemEkle(durum, "urun-guncelle", $"{urun.UrunAdi} fiyat/yayın ayarları güncellendi.", 0);
            return IslemSonucu.Basari("Ürün ayarları kalıcı olarak güncellendi.");
        });

    public Task<IslemSonucu> UrunKapasitesiArtirAsync(string sirketKimligi, UrunKapasiteIstegi istek, CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            UrunKaydi? urun = durum.Urunler.FirstOrDefault(u => string.Equals(u.UrunKimligi, istek.UrunKimligi, StringComparison.OrdinalIgnoreCase));
            if (urun is null) return IslemSonucu.Hata("Ürün bulunamadı.");
            if (istek.EklenecekKapasite is < 50 or > 100_000) return IslemSonucu.Hata("Kapasite artışı 50-100.000 arasında olmalıdır.");
            decimal katsayi = urun.UrunTuru is "isletim-sistemi" or "platform" ? 1.5m : 1m;
            decimal maliyet = decimal.Round(istek.EklenecekKapasite * (8m + urun.KullaniciKapasitesi / 2_000m) * katsayi, 2);
            if (sirket.Kasa < maliyet) return IslemSonucu.Hata($"Yetersiz kasa. Gerekli: {maliyet:N2} TL.");
            sirket.Kasa -= maliyet;
            urun.SatinAlinanKullaniciKapasitesi += istek.EklenecekKapasite;
            durum.ToplamYatirimHarcamasi += maliyet;
            UrunKapasitesiniYenile(durum, urun);
            IslemEkle(durum, "urun-kapasite", $"{urun.UrunAdi} için +{istek.EklenecekKapasite} kapasite alındı.", -maliyet);
            return IslemSonucu.Basari($"Uygulama kapasitesi {urun.KullaniciKapasitesi:N0} oldu.");
        });

    public Task<IslemSonucu> ProtokolOlusturAsync(string sirketKimligi, ProtokolOlusturIstegi istek, CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            string ad = istek.ProtokolAdi?.Trim() ?? string.Empty;
            if (ad.Length is < 2 or > 100) return IslemSonucu.Hata("Protokol adı geçersiz.");
            if (_veri.Protokoller.Any(p => string.Equals(p.SahipSirketKimligi, sirketKimligi, StringComparison.OrdinalIgnoreCase) && string.Equals(p.ProtokolAdi, ad, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Surum, istek.Surum, StringComparison.OrdinalIgnoreCase)))
                return IslemSonucu.Hata("Bu protokol sürümü zaten piyasada.");
            decimal maliyet = 6_000m + Seviye(durum, "arge") * 650m;
            if (sirket.Kasa < maliyet) return IslemSonucu.Hata($"Protokol yayın maliyeti: {maliyet:N2} TL.");
            OzelProtokolKaydi protokol = new()
            {
                ProtokolKimligi = $"protokol-{Guid.NewGuid():N}", SahipSirketKimligi = sirketKimligi,
                ProtokolAdi = ad, Surum = string.IsNullOrWhiteSpace(istek.Surum) ? "1.0" : istek.Surum.Trim(),
                Aciklama = istek.Aciklama?.Trim() ?? string.Empty, LisansModeli = Normal(istek.LisansModeli, "acik"),
                BenimsemeBedeli = Math.Max(0, istek.BenimsemeBedeli), TickLisansBedeli = Math.Max(0, istek.TickLisansBedeli),
                YayinTicki = _tick
            };
            sirket.Kasa -= maliyet;
            durum.ToplamYatirimHarcamasi += maliyet;
            _veri.Protokoller.Add(protokol);
            IslemEkle(durum, "protokol-yayin", $"{protokol.ProtokolAdi} v{protokol.Surum} yayınlandı.", -maliyet);
            return IslemSonucu.Basari("Protokol piyasaya açıldı.", protokol);
        });

    public Task<IslemSonucu> ProtokolBenimseAsync(string sirketKimligi, ProtokolBenimseIstegi istek, CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            OzelProtokolKaydi? protokol = _veri.Protokoller.FirstOrDefault(p => p.Aktif && string.Equals(p.ProtokolKimligi, istek.ProtokolKimligi, StringComparison.OrdinalIgnoreCase));
            if (protokol is null) return IslemSonucu.Hata("Protokol bulunamadı.");
            if (string.Equals(protokol.SahipSirketKimligi, sirketKimligi, StringComparison.OrdinalIgnoreCase)) return IslemSonucu.Hata("Kendi protokolünüz zaten kullanılabilir.");
            if (protokol.BenimseyenSirketler.Contains(sirketKimligi, StringComparer.OrdinalIgnoreCase)) return IslemSonucu.Hata("Protokol zaten benimsenmiş.");
            if (sirket.Kasa < protokol.BenimsemeBedeli) return IslemSonucu.Hata("Benimseme bedeli için kasa yetersiz.");
            sirket.Kasa -= protokol.BenimsemeBedeli;
            durum.ToplamIsletmeGideri += protokol.BenimsemeBedeli;
            protokol.BenimseyenSirketler.Add(sirketKimligi);
            durum.BenimsenenProtokoller.Add(protokol.ProtokolKimligi);
            SirketKaydi? sahip = SirketBul(protokol.SahipSirketKimligi);
            if (sahip is not null) { sahip.Kasa += protokol.BenimsemeBedeli; sahip.ToplamGelir += protokol.BenimsemeBedeli; }
            IslemEkle(durum, "protokol-benimse", $"{protokol.ProtokolAdi} benimsendi.", -protokol.BenimsemeBedeli);
            return IslemSonucu.Basari("Protokol benimsendi.");
        });

    public Task<IslemSonucu> SozlesmeKabulEtAsync(string sirketKimligi, SozlesmeKabulIstegi istek, CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (_, durum) =>
        {
            SozlesmeTeklifi? teklif = _veri.SozlesmeTeklifleri.FirstOrDefault(t => t.Aktif && string.Equals(t.TeklifKimligi, istek.TeklifKimligi, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(t.KabulEdenSirketKimligi) && t.SonKabulTicki >= _tick);
            if (teklif is null) return IslemSonucu.Hata("Teklif artık açık değil.");
            teklif.KabulEdenSirketKimligi = sirketKimligi;
            teklif.Aktif = false;
            durum.Sozlesmeler.Add(new SozlesmeKaydi
            {
                SozlesmeKimligi = $"sozlesme-{Guid.NewGuid():N}", TeklifKimligi = teklif.TeklifKimligi,
                Baslik = teklif.Baslik, Kategori = teklif.Kategori, BaslangicTicki = _tick,
                BitisTicki = _tick + teklif.SureTick, TickOdemesi = teklif.TickOdemesi,
                IhlalCezasi = teklif.IhlalCezasi, AsgariKalite = teklif.AsgariKalite,
                AsgariPerformans = teklif.AsgariPerformans, AsgariGuvenlik = teklif.AsgariGuvenlik,
                GerekliKapasite = teklif.GerekliKapasite
            });
            IslemEkle(durum, "sla", $"{teklif.Baslik} kabul edildi.", 0);
            return IslemSonucu.Basari("SLA sözleşmesi kabul edildi.");
        });

    public async Task<string> PanelJsonuOlusturAsync(string sirketKimligi, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            HizmetleriKaliciKayitlaVeUygula();
            UrunKapasiteleriniYenile();
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            SirketIsletimDurumu durum = Durum(sirketKimligi);
            Degerle(sirket, durum);
            SirketHesabi? hesap = _veri.Hesaplar.FirstOrDefault(h => string.Equals(h.SirketKimligi, sirketKimligi, StringComparison.OrdinalIgnoreCase));
            object cevap = new
            {
                tickNumarasi = _tick,
                sirket = new
                {
                    sirket.SirketKimligi, sirket.SirketAdi, sirket.Kasa, sirket.NetGelir, sirket.BagliMi,
                    sirket.KodKalitesiPuani, sirket.PerformansPuani, sirket.GuvenlikPuani,
                    sirket.ItibarPuani, sirket.GuvenilirlikPuani, sirket.OrtalamaMusteriMemnuniyeti,
                    sirket.TamamlananIsSayisi, sirket.BasarisizIsSayisi, hizmetler = sirket.Hizmetler
                },
                isletim = new
                {
                    durum.KrediNotu, durum.EkosistemPuani, durum.OperasyonRiski, durum.TeknikBorc, durum.BakimBaskisi,
                    durum.ToplamAboneSayisi, durum.ToplamAbonelikGeliri, durum.ToplamUrunGeliri,
                    durum.ToplamIsletmeGideri, durum.ToplamFinansmanGideri, durum.ToplamYatirimHarcamasi,
                    durum.OdenemeyenGider, durum.SirketDegeri, durum.TahminiHisseFiyati,
                    toplamBorc = durum.Krediler.Where(k => k.Aktif).Sum(k => k.KalanBorc),
                    durum.TemerrutSayisi, durum.YatirimSeviyeleri, durum.HizmetAyarlari,
                    durum.Krediler, durum.Urunler, durum.Sozlesmeler,
                    sonOlaylar = durum.SonOlaylar.OrderByDescending(o => o.TickNumarasi).Take(30),
                    sonIslemler = durum.SonIslemler.OrderByDescending(i => i.Zaman).Take(60)
                },
                yatirimMagazasi = Yatirimlar.Select(p =>
                {
                    int seviye = Seviye(durum, p.YatirimTuru);
                    return new { p.YatirimTuru, p.Ad, p.Aciklama, p.AzamiSeviye, seviye, sonrakiMaliyet = Maliyet(p, seviye), tickBakimGideri = YatirimBakimGideri(p.YatirimTuru, seviye + 1) };
                }),
                krediPaketleri = Krediler,
                protokoller = _veri.Protokoller.Where(p => p.Aktif),
                sozlesmeTeklifleri = _veri.SozlesmeTeklifleri.Where(t => t.Aktif && string.IsNullOrWhiteSpace(t.KabulEdenSirketKimligi) && t.SonKabulTicki >= _tick),
                piyasaOlaylari = AktifOlaylar(),
                parolaDegistirilmeli = hesap?.ParolaDegistirilmeli ?? false,
                kaliciOtoriteKurali = "Sunucu değerleri yalnız ilk varsayılandır. 8090 değişiklikleri motor kaydında üstündür."
            };
            return JsonSerializer.Serialize(cevap, JsonAyarlari);
        }
        finally { _kilit.Release(); }
    }

    public async Task TickCalistirAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            _tick = tickNumarasi;
            _veri.SonIslenenTick = tickNumarasi;
            HizmetleriKaliciKayitlaVeUygula();
            UrunKapasiteleriniYenile();
            OlaylariGuncelle();
            SozlesmeTeklifleriniGuncelle();

            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
            {
                SirketIsletimDurumu durum = Durum(sirket.SirketKimligi);
                DinamikRiskleriHesapla(sirket, durum);
                SirketOlayiUret(sirket, durum);
                GiderleriIsle(sirket, durum);
                KredileriIsle(sirket, durum);
                UrunleriIsle(sirket, durum);
                SozlesmeleriIsle(sirket, durum);
                PuanlariGercekcilestir(sirket, durum);
                Degerle(sirket, durum);
            }

            LisanslariIsle();
            HizmetleriKaliciKayitlaVeUygula();
            await TumunuKaydetAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    private void HizmetleriKaliciKayitlaVeUygula()
    {
        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
        {
            SirketIsletimDurumu durum = Durum(sirket.SirketKimligi);
            durum.HizmetAyarlari ??= new(StringComparer.OrdinalIgnoreCase);
            durum.HizmetFiyatEzmeDegerleri ??= new(StringComparer.OrdinalIgnoreCase);
            durum.HizmetBazKapasiteleri ??= new(StringComparer.OrdinalIgnoreCase);
            int yatirimBonusu = HizmetYatirimKapasiteBonusu(durum);

            foreach (SunulanHizmet hizmet in sirket.Hizmetler)
            {
                string anahtar = Anahtar(hizmet);
                if (!durum.HizmetAyarlari.TryGetValue(anahtar, out HizmetKaliciAyari? ayar))
                {
                    int ilkKapasite = durum.HizmetBazKapasiteleri.TryGetValue(anahtar, out int eskiBaz) ? Math.Max(1, eskiBaz) : Math.Max(1, hizmet.AzamiEszamanliIs);
                    decimal ilkFiyat = durum.HizmetFiyatEzmeDegerleri.TryGetValue(anahtar, out decimal eskiFiyat) ? eskiFiyat : Math.Max(0.01m, hizmet.BirimFiyat);
                    ayar = new HizmetKaliciAyari
                    {
                        HizmetKimligi = hizmet.HizmetKimligi, HizmetSurumu = hizmet.HizmetSurumu,
                        SunucudanGelenIlkFiyat = Math.Max(0.01m, hizmet.BirimFiyat), SunucudanGelenIlkKapasite = ilkKapasite,
                        SunucudanGelenIlkAktiflik = hizmet.Aktif, YonetilenFiyat = ilkFiyat,
                        YonetilenAktiflik = hizmet.Aktif, FiyatYonetildi = durum.HizmetFiyatEzmeDegerleri.ContainsKey(anahtar),
                        IlkGorulmeTicki = _tick, SonGorulmeTicki = _tick
                    };
                    durum.HizmetAyarlari[anahtar] = ayar;
                }
                ayar.SonGorulmeTicki = _tick;
                hizmet.BirimFiyat = ayar.FiyatYonetildi ? ayar.YonetilenFiyat : ayar.SunucudanGelenIlkFiyat;
                hizmet.Aktif = ayar.AktiflikYonetildi ? ayar.YonetilenAktiflik : ayar.SunucudanGelenIlkAktiflik;
                hizmet.AzamiEszamanliIs = Math.Max(1, ayar.SunucudanGelenIlkKapasite + ayar.SatinAlinanKapasite + yatirimBonusu);
                durum.HizmetFiyatEzmeDegerleri[anahtar] = hizmet.BirimFiyat;
                durum.HizmetBazKapasiteleri[anahtar] = ayar.SunucudanGelenIlkKapasite;
            }
        }
    }

    private void UrunKapasiteleriniYenile()
    {
        foreach (SirketIsletimDurumu durum in _veri.Sirketler)
            foreach (UrunKaydi urun in durum.Urunler)
                UrunKapasitesiniYenile(durum, urun);
    }

    private static void UrunKapasitesiniYenile(SirketIsletimDurumu durum, UrunKaydi urun)
    {
        if (urun.TabanKullaniciKapasitesi <= 0) urun.TabanKullaniciKapasitesi = Math.Max(100, urun.KullaniciKapasitesi - urun.SatinAlinanKullaniciKapasitesi);
        int bonus = Seviye(durum, "ram") * 180 + Seviye(durum, "ag") * 220 + Seviye(durum, "depolama") * 260 + Seviye(durum, "cpu") * 90 + Seviye(durum, "yedek") * 120;
        if (urun.UrunTuru is "isletim-sistemi" or "platform") bonus = (int)Math.Round(bonus * 1.25);
        urun.AltyapiKapasiteBonusu = bonus;
        urun.KullaniciKapasitesi = Math.Max(50, urun.TabanKullaniciKapasitesi + urun.SatinAlinanKullaniciKapasitesi + bonus);
        urun.AktifKullaniciSayisi = Math.Min(urun.AktifKullaniciSayisi, urun.KullaniciKapasitesi);
    }

    private void OlaylariGuncelle()
    {
        _veri.PiyasaOlaylari.RemoveAll(o => o.BitisTicki < _tick - 20);
        if (_veri.PiyasaOlaylari.Any(o => o.BitisTicki >= _tick) || _rastgele.NextDouble() > 0.32) return;
        (string baslik, string aciklama, string kategori, double talep, double gider, double gelir, double kayip, double ariza)[] olaylar =
        [
            ("Kurumsal dijitalleşme dalgası", "Kurumsal yazılım talebi hızlandı; operasyon ekipleri zorlanıyor.", "tum", 1.75, 1.18, 1.08, 1.05, 1.12),
            ("Bulut enerji maliyeti artışı", "Sunucu, ağ ve depolama giderleri yükseldi.", "tum", 0.95, 1.55, 0.98, 1.02, 1.08),
            ("Küresel güvenlik açığı", "Tüm şirketlerde yama ve olay müdahalesi baskısı oluştu.", "guvenlik", 1.20, 1.30, 1.00, 1.12, 1.85),
            ("Viral tüketici dalgası", "Sosyal, mesajlaşma ve medya ürünlerinde ani kullanıcı akını oluştu.", "sosyal-medya", 2.25, 1.28, 1.16, 1.18, 1.35),
            ("Piyasa durgunluğu", "Müşteriler harcamalarını azalttı; fiyat hassasiyeti arttı.", "tum", 0.64, 0.95, 0.82, 1.28, 0.95),
            ("Açık kaynak ekosistem sıçraması", "Uyumlu platform ve protokol kullanan ürünler avantaj kazandı.", "platform", 1.42, 0.92, 1.10, 0.86, 0.88),
            ("Veri merkezi bölgesel kesintisi", "Yedeklilik yatırımı olmayan sistemlerde kesinti riski arttı.", "tum", 0.90, 1.20, 0.92, 1.25, 2.10)
        ];
        var secilen = olaylar[_rastgele.Next(olaylar.Length)];
        int sure = _rastgele.Next(3, 8);
        PiyasaOlayi olay = new()
        {
            OlayKimligi = $"olay-{Guid.NewGuid():N}", Baslik = secilen.baslik, Aciklama = secilen.aciklama,
            EtkilenenKategori = secilen.kategori, TalepCarpani = secilen.talep, GiderCarpani = secilen.gider,
            GelirCarpani = secilen.gelir, KullaniciKaybiCarpani = secilen.kayip, ArizaRiskiCarpani = secilen.ariza,
            BaslangicTicki = _tick, BitisTicki = _tick + sure
        };
        _veri.PiyasaOlaylari.Add(olay);
        KonsolKayitcisi.Uyari($"PİYASA OLAYI | {olay.Baslik} | Tick {_tick}-{olay.BitisTicki}");
    }

    private void DinamikRiskleriHesapla(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        int aktifUrun = durum.Urunler.Count(u => u.Aktif);
        int aktifHizmet = sirket.Hizmetler.Count(h => h.Aktif);
        int kullanici = durum.Urunler.Sum(u => u.AktifKullaniciSayisi);
        int kapasite = Math.Max(1, durum.Urunler.Sum(u => u.KullaniciKapasitesi));
        double doluluk = Math.Clamp((double)kullanici / kapasite, 0, 2);
        double karmasiklik = aktifUrun * 2.1 + aktifHizmet * 0.42 + durum.Urunler.Sum(u => u.Bagimliliklar.Count) * 0.9;
        double azaltim = Seviye(durum, "yedek") * 2.2 + Seviye(durum, "guvenlik") * 1.8 + Seviye(durum, "destek") * 0.7 + Seviye(durum, "arge") * 0.8;
        double hedefRisk = 5 + karmasiklik + doluluk * 26 + durum.TeknikBorc * 0.45 - azaltim;
        durum.OperasyonRiski += (Math.Clamp(hedefRisk, 2, 95) - durum.OperasyonRiski) * 0.18;
        double borcArtisi = aktifUrun * 0.045 + aktifHizmet * 0.008 + Math.Max(0, doluluk - 0.72) * 0.5;
        double borcAzalisi = Seviye(durum, "arge") * 0.055 + Seviye(durum, "destek") * 0.012;
        durum.TeknikBorc = Math.Clamp(durum.TeknikBorc + borcArtisi - borcAzalisi, 0, 100);
        durum.BakimBaskisi = Math.Clamp(karmasiklik + durum.TeknikBorc * 0.7 + doluluk * 20, 0, 100);
    }

    private void SirketOlayiUret(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        double p = 0.012 + durum.OperasyonRiski / 1_350d + durum.TeknikBorc / 2_800d;
        p *= AktifOlaylar().Select(o => o.ArizaRiskiCarpani).DefaultIfEmpty(1).Aggregate(1d, (a, b) => a * b);
        p = Math.Clamp(p, 0.01, 0.22);
        if (_rastgele.NextDouble() > p) return;

        bool olumlu = _rastgele.NextDouble() < Math.Clamp(0.22 + Seviye(durum, "arge") * 0.015 + Seviye(durum, "destek") * 0.008, 0.18, 0.45);
        if (olumlu) OlumluOlay(sirket, durum); else OlumsuzOlay(sirket, durum);
    }

    private void OlumluOlay(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        int secim = _rastgele.Next(4);
        decimal kazanc = decimal.Round(1_000m + (decimal)_rastgele.NextDouble() * 8_000m + durum.ToplamAboneSayisi * 0.7m, 2);
        string baslik;
        string aciklama;
        switch (secim)
        {
            case 0: baslik = "Optimizasyon atılımı"; aciklama = "Ekip kritik bir darboğazı giderdi."; sirket.PerformansPuani += 0.8; durum.TeknikBorc = Math.Max(0, durum.TeknikBorc - 3.5); break;
            case 1: baslik = "Topluluk desteği"; aciklama = "Ürün toplulukta beklenmedik görünürlük kazandı."; sirket.ItibarPuani += 0.7; break;
            case 2: baslik = "Hata avı başarısı"; aciklama = "Kritik açık yayına çıkmadan bulundu."; sirket.GuvenlikPuani += 0.65; durum.OperasyonRiski = Math.Max(0, durum.OperasyonRiski - 2.5); break;
            default: baslik = "Kurumsal ön ödeme"; aciklama = "Bir müşteri gelecek dönem için ön ödeme yaptı."; sirket.Kasa += kazanc; sirket.ToplamGelir += kazanc; break;
        }
        OlayEkle(durum, true, baslik, aciklama, secim == 3 ? kazanc : 0, 0.4, 0.35, 0.35);
    }

    private void OlumsuzOlay(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        int secim = _rastgele.Next(7);
        double azaltim = Math.Clamp(1 - Seviye(durum, "yedek") * 0.06 - Seviye(durum, "guvenlik") * 0.035 - Seviye(durum, "destek") * 0.018, 0.35, 1);
        decimal zarar = decimal.Round((1_500m + (decimal)_rastgele.NextDouble() * 16_000m + durum.ToplamAboneSayisi * 1.3m) * (decimal)azaltim, 2);
        string baslik;
        string aciklama;
        string varlik = string.Empty;
        double itibar = -0.5, guvenlik = 0, performans = -0.45;
        switch (secim)
        {
            case 0: baslik = "Beklenmedik veri merkezi kesintisi"; aciklama = "Sağlıklı kod bulunmasına rağmen altyapı bölgesi kısa süre erişilemedi."; performans = -1.2; break;
            case 1: baslik = "Üçüncü taraf bağımlılık arızası"; aciklama = "Bağlı bir platform veya protokol beklenmedik yanıt verdi."; performans = -0.8; break;
            case 2: baslik = "Kritik üretim hatası"; aciklama = "Nadir bir uç durum gerçek kullanıcı trafiğinde ortaya çıktı."; durum.TeknikBorc += 3.8; itibar = -0.9; break;
            case 3: baslik = "Yanlış pozitif güvenlik engeli"; aciklama = "Koruma katmanı bazı gerçek müşterileri reddetti."; guvenlik = -0.4; itibar = -0.65; break;
            case 4: baslik = "Ani trafik patlaması"; aciklama = "Kapasite planını aşan talep gecikme ve kullanıcı kaybı oluşturdu."; performans = -1.0; break;
            case 5: baslik = "Uyumluluk denetimi"; aciklama = "Yeni düzenleme nedeniyle ek operasyon ve raporlama maliyeti oluştu."; performans = -0.25; break;
            default: baslik = "Kilit personel kaybı"; aciklama = "Ekipteki bilgi yoğunluğu bakım baskısını artırdı."; durum.TeknikBorc += 2.6; itibar = -0.35; break;
        }
        decimal odenen = Math.Min(Math.Max(0, sirket.Kasa), zarar);
        sirket.Kasa -= odenen;
        durum.ToplamIsletmeGideri += odenen;
        durum.OdenemeyenGider += zarar - odenen;
        sirket.ItibarPuani += itibar * azaltim;
        sirket.GuvenlikPuani += guvenlik * azaltim;
        sirket.PerformansPuani += performans * azaltim;
        int kayip = (int)Math.Round(durum.Urunler.Sum(u => u.AktifKullaniciSayisi) * (0.0015 + _rastgele.NextDouble() * 0.006) * azaltim);
        KullaniciKaybettir(durum, kayip);
        OlayEkle(durum, false, baslik, aciklama, -zarar, itibar, guvenlik, performans, varlik);
        KonsolKayitcisi.Uyari($"ŞİRKET OLAYI | {sirket.SirketAdi} | {baslik} | Etki: {-zarar:N2} TL");
    }

    private void GiderleriIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        int hizmet = sirket.Hizmetler.Count(h => h.Aktif);
        int hizmetKapasite = sirket.Hizmetler.Where(h => h.Aktif).Sum(h => h.AzamiEszamanliIs);
        int urun = durum.Urunler.Count(u => u.Aktif);
        int kullanici = durum.Urunler.Where(u => u.Aktif).Sum(u => u.AktifKullaniciSayisi);
        decimal yatirimBakim = Yatirimlar.Sum(p => YatirimBakimGideri(p.YatirimTuru, Seviye(durum, p.YatirimTuru)));
        decimal tutar = 420m + hizmet * 42m + hizmetKapasite * 10m + urun * 180m + kullanici * 0.48m + yatirimBakim;
        tutar += durum.TeknikBorc * 14m + durum.BakimBaskisi * 8m;
        foreach (UrunKaydi u in durum.Urunler.Where(u => u.Aktif))
        {
            tutar += u.UrunTuru switch { "isletim-sistemi" => 480m, "platform" => 390m, "altyapi" => 310m, _ => 120m };
            tutar += u.Bagimliliklar.Count * 28m + u.DesteklenenPlatformlar.Count * 12m;
        }
        double olay = AktifOlaylar().Select(o => o.GiderCarpani).DefaultIfEmpty(1).Aggregate(1d, (a, b) => a * b);
        Ode(sirket, durum, decimal.Round(tutar * (decimal)Math.Clamp(olay, 0.55, 4), 2));
    }

    private void KredileriIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        foreach (KrediKaydi kredi in durum.Krediler.Where(k => k.Aktif && _tick >= k.SonrakiOdemeTicki))
        {
            decimal odeme = Math.Min(kredi.TaksitTutari, kredi.KalanBorc);
            if (sirket.Kasa >= odeme)
            {
                sirket.Kasa -= odeme; kredi.KalanBorc = Math.Max(0, kredi.KalanBorc - odeme);
                kredi.KalanTaksit = Math.Max(0, kredi.KalanTaksit - 1); kredi.SonrakiOdemeTicki = _tick + kredi.OdemeAraligiTick;
                durum.ToplamFinansmanGideri += odeme; durum.KrediNotu = Math.Clamp(durum.KrediNotu + 2, 300, 900);
                if (kredi.KalanBorc <= 0 || kredi.KalanTaksit <= 0) kredi.Aktif = false;
            }
            else
            {
                kredi.GecikmeSayisi++; kredi.KalanBorc = decimal.Round(kredi.KalanBorc * (1 + kredi.TickFaizOrani * 2.2m), 2);
                kredi.SonrakiOdemeTicki = _tick + kredi.OdemeAraligiTick; durum.TemerrutSayisi++;
                durum.KrediNotu = Math.Clamp(durum.KrediNotu - 38, 300, 900); sirket.GuvenilirlikPuani -= 1.4;
            }
        }
    }

    private void UrunleriIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        int toplam = 0;
        foreach (UrunKaydi urun in durum.Urunler.Where(u => u.Aktif))
        {
            bool arkaUc = sirket.Hizmetler.Any(h => h.Aktif && string.Equals(h.HizmetKimligi, urun.ArkaUcHizmetKimligi, StringComparison.OrdinalIgnoreCase) && string.Equals(h.HizmetSurumu, urun.ArkaUcHizmetSurumu, StringComparison.OrdinalIgnoreCase));
            bool platformUygun = PlatformlarUygunMu(urun);
            double olayTalep = KategoriOlayCarpani(urun.Kategori, o => o.TalepCarpani);
            double fiyat = FiyatCarpani(urun);
            double kalite = Math.Clamp((sirket.KodKalitesiPuani + sirket.PerformansPuani + sirket.GuvenlikPuani + urun.UrunKalitesi) / 360d, 0.16, 1.18);
            double ekosistem = 1 + Math.Min(0.32, (urun.DesteklenenPlatformlar.Count + urun.Bagimliliklar.Count) * 0.025);
            double talep = TabanTalep(urun) * olayTalep * (1 + Seviye(durum, "pazarlama") * 0.09) * kalite * fiyat * ekosistem;
            talep *= sirket.BagliMi && arkaUc && platformUygun ? 1 : Seviye(durum, "yedek") > 0 ? 0.42 : 0.06;
            talep *= 0.55 + _rastgele.NextDouble() * 0.9;
            urun.SonTalepCarpani = talep;
            int yeni = Math.Max(0, (int)Math.Floor(talep));
            int alinan = Math.Min(yeni, Math.Max(0, urun.KullaniciKapasitesi - urun.AktifKullaniciSayisi));
            urun.AktifKullaniciSayisi += alinan; urun.ToplamEdinilenKullanici += alinan;

            double kayipOrani = 0.008 + Math.Max(0, 72 - urun.UrunMemnuniyeti) / 1_250d + durum.OperasyonRiski / 8_500d;
            kayipOrani *= Math.Max(0.38, 1 - Seviye(durum, "destek") * 0.047);
            kayipOrani *= KategoriOlayCarpani(urun.Kategori, o => o.KullaniciKaybiCarpani);
            if (!sirket.BagliMi || !arkaUc || !platformUygun) kayipOrani += 0.12;
            if (urun.AktifKullaniciSayisi >= urun.KullaniciKapasitesi * 0.94) kayipOrani += 0.055;
            int kayip = Math.Min(urun.AktifKullaniciSayisi, (int)Math.Ceiling(urun.AktifKullaniciSayisi * Math.Clamp(kayipOrani, 0, 0.65)));
            urun.AktifKullaniciSayisi -= kayip; urun.ToplamKaybedilenKullanici += kayip;

            decimal gelir = Gelir(urun, alinan) * (decimal)KategoriOlayCarpani(urun.Kategori, o => o.GelirCarpani);
            gelir = decimal.Round(gelir, 2);
            decimal gider = decimal.Round(urun.AktifKullaniciSayisi * KullaniciGideri(urun) + urun.KullaniciKapasitesi * 0.018m + urun.Bagimliliklar.Count * 35m, 2);
            sirket.Kasa += gelir; sirket.ToplamGelir += gelir; urun.ToplamGelir += gelir; durum.ToplamUrunGeliri += gelir;
            if (urun.FiyatlandirmaModeli is "abonelik" or "freemium") durum.ToplamAbonelikGeliri += gelir;
            Ode(sirket, durum, gider); urun.ToplamGider += gider;

            double gozlem = Math.Clamp(sirket.KodKalitesiPuani * 0.34 + sirket.PerformansPuani * 0.27 + sirket.GuvenlikPuani * 0.19 + (arkaUc ? 12 : 0) + (platformUygun ? 6 : -12) - durum.TeknikBorc * 0.15, 0, 96);
            urun.UrunKalitesi += (gozlem - urun.UrunKalitesi) * 0.045;
            double memHedef = urun.UrunKalitesi - durum.OperasyonRiski * 0.06 - (kayipOrani * 100) * 0.25;
            urun.UrunMemnuniyeti += (memHedef - urun.UrunMemnuniyeti) * 0.055;
            urun.UrunKalitesi = Math.Clamp(urun.UrunKalitesi, 0, 98);
            urun.UrunMemnuniyeti = Math.Clamp(urun.UrunMemnuniyeti, 0, 98);
            toplam += urun.AktifKullaniciSayisi;
        }
        durum.ToplamAboneSayisi = toplam;
    }

    private bool PlatformlarUygunMu(UrunKaydi urun)
    {
        if (urun.GerekliPlatformlar.Count == 0) return true;
        HashSet<string> aktifPlatformlar = _veri.Sirketler.SelectMany(d => d.Urunler)
            .Where(u => u.Aktif && u.UrunTuru is "isletim-sistemi" or "platform")
            .SelectMany(u => new[] { u.UygulamaKimligi, u.UrunAdi }.Concat(u.DesteklenenPlatformlar))
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return urun.GerekliPlatformlar.All(aktifPlatformlar.Contains);
    }

    private void SozlesmeleriIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        foreach (SozlesmeKaydi sozlesme in durum.Sozlesmeler.Where(s => s.Aktif))
        {
            if (_tick > sozlesme.BitisTicki) { sozlesme.Aktif = false; continue; }
            bool olayBasarisi = _rastgele.NextDouble() > Math.Clamp(0.015 + durum.OperasyonRiski / 1_200d - Seviye(durum, "yedek") * 0.006, 0.01, 0.18);
            bool tamam = olayBasarisi && (sirket.BagliMi || Seviye(durum, "yedek") > 1) &&
                sirket.KodKalitesiPuani >= sozlesme.AsgariKalite && sirket.PerformansPuani >= sozlesme.AsgariPerformans &&
                sirket.GuvenlikPuani >= sozlesme.AsgariGuvenlik && EtkinKapasite(sirket) >= sozlesme.GerekliKapasite;
            if (tamam)
            {
                sirket.Kasa += sozlesme.TickOdemesi; sirket.ToplamGelir += sozlesme.TickOdemesi; sozlesme.BasariliTickSayisi++;
            }
            else
            {
                decimal ceza = Math.Min(Math.Max(0, sirket.Kasa), sozlesme.IhlalCezasi);
                sirket.Kasa -= ceza; durum.ToplamIsletmeGideri += ceza; sozlesme.IhlalSayisi++;
                sirket.ItibarPuani -= 0.42; sirket.GuvenilirlikPuani -= 0.62;
                if (sozlesme.IhlalSayisi >= 5) sozlesme.Aktif = false;
            }
        }
    }

    private void LisanslariIsle()
    {
        foreach (OzelProtokolKaydi protokol in _veri.Protokoller.Where(p => p.Aktif && p.TickLisansBedeli > 0))
        {
            SirketKaydi? sahip = SirketBul(protokol.SahipSirketKimligi);
            if (sahip is null) continue;
            foreach (string kimlik in protokol.BenimseyenSirketler.ToList())
            {
                SirketKaydi? kullanan = SirketBul(kimlik); if (kullanan is null) continue;
                SirketIsletimDurumu durum = Durum(kimlik);
                if (kullanan.Kasa >= protokol.TickLisansBedeli)
                {
                    kullanan.Kasa -= protokol.TickLisansBedeli; durum.ToplamIsletmeGideri += protokol.TickLisansBedeli;
                    sahip.Kasa += protokol.TickLisansBedeli; sahip.ToplamGelir += protokol.TickLisansBedeli;
                    protokol.ToplamLisansGeliri += protokol.TickLisansBedeli;
                }
                else { durum.OdenemeyenGider += protokol.TickLisansBedeli; durum.EkosistemPuani = Math.Max(0, durum.EkosistemPuani - 0.15); }
            }
        }
    }

    private void PuanlariGercekcilestir(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        double olcek = Math.Log10(1 + durum.ToplamAboneSayisi + sirket.TamamlananIsSayisi) * 1.5;
        double yonetim = Seviye(durum, "arge") * 0.28 + Seviye(durum, "guvenlik") * 0.25 + Seviye(durum, "yedek") * 0.2;
        double tavan = Math.Clamp(94.5 + yonetim - olcek - durum.TeknikBorc * 0.045 - durum.OperasyonRiski * 0.025, 84, 98.2);
        sirket.KodKalitesiPuani = GercekciPuan(sirket.KodKalitesiPuani, tavan, 0.08);
        sirket.PerformansPuani = GercekciPuan(sirket.PerformansPuani, tavan - durum.BakimBaskisi * 0.018, 0.14);
        sirket.GuvenlikPuani = GercekciPuan(sirket.GuvenlikPuani, tavan + Seviye(durum, "guvenlik") * 0.12, 0.11);
        sirket.ItibarPuani = GercekciPuan(sirket.ItibarPuani, 96 - durum.OperasyonRiski * 0.02, 0.07);
        sirket.GuvenilirlikPuani = GercekciPuan(sirket.GuvenilirlikPuani, 95.5 - durum.OperasyonRiski * 0.035, 0.09);
        sirket.OrtalamaMusteriMemnuniyeti = GercekciPuan(sirket.OrtalamaMusteriMemnuniyeti, 95 - durum.OperasyonRiski * 0.03, 0.08);
    }

    private double GercekciPuan(double mevcut, double tavan, double oynaklik)
    {
        if (mevcut > tavan) mevcut -= (mevcut - tavan) * 0.075 + 0.018;
        mevcut += (_rastgele.NextDouble() - 0.5) * oynaklik;
        return Math.Clamp(mevcut, 0, 99.2);
    }

    private void SozlesmeTeklifleriniGuncelle()
    {
        if (_veri.SozlesmeTeklifleri.Count(t => t.Aktif && t.SonKabulTicki >= _tick) >= 8 || _tick % 3 != 0) return;
        string[] kategoriler = ["api", "analitik", "eposta", "sosyal-medya", "guvenlik", "platform", "isletim-sistemi", "altyapi"];
        string kategori = kategoriler[_rastgele.Next(kategoriler.Length)];
        int sure = _rastgele.Next(6, 20);
        _veri.SozlesmeTeklifleri.Add(new SozlesmeTeklifi
        {
            TeklifKimligi = $"teklif-{Guid.NewGuid():N}", Baslik = $"{kategori} kurumsal işletim sözleşmesi",
            Kategori = kategori, SureTick = sure, TickOdemesi = _rastgele.Next(1_500, 8_500),
            IhlalCezasi = _rastgele.Next(5_000, 28_000), AsgariKalite = _rastgele.Next(68, 92),
            AsgariPerformans = _rastgele.Next(65, 91), AsgariGuvenlik = _rastgele.Next(68, 94),
            GerekliKapasite = _rastgele.Next(8, 80), SonKabulTicki = _tick + _rastgele.Next(3, 8)
        });
    }

    private void KayitlariTamamla()
    {
        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
        {
            SirketIsletimDurumu durum = Durum(sirket.SirketKimligi);
            durum.YatirimSeviyeleri ??= new(StringComparer.OrdinalIgnoreCase);
            durum.HizmetAyarlari ??= new(StringComparer.OrdinalIgnoreCase);
            durum.HizmetFiyatEzmeDegerleri ??= new(StringComparer.OrdinalIgnoreCase);
            durum.HizmetBazKapasiteleri ??= new(StringComparer.OrdinalIgnoreCase);
            durum.Krediler ??= []; durum.Urunler ??= []; durum.BenimsenenProtokoller ??= [];
            durum.Sozlesmeler ??= []; durum.SonIslemler ??= []; durum.SonOlaylar ??= [];
            foreach (UrunKaydi urun in durum.Urunler)
            {
                urun.DesteklenenPlatformlar ??= []; urun.GerekliPlatformlar ??= []; urun.Bagimliliklar ??= [];
                if (urun.TabanKullaniciKapasitesi <= 0) urun.TabanKullaniciKapasitesi = Math.Max(100, urun.KullaniciKapasitesi);
            }

            if (_veri.Hesaplar.Any(h => string.Equals(h.SirketKimligi, sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase))) continue;
            (string kullanici, string parola) = VarsayilanGiris(sirket);
            string tuz = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            _veri.Hesaplar.Add(new SirketHesabi { SirketKimligi = sirket.SirketKimligi, KullaniciAdi = kullanici, ParolaTuzu = tuz, ParolaOzeti = Ozetle(tuz, parola), ParolaDegistirilmeli = true });
            KonsolKayitcisi.Uyari($"Yönetim hesabı | {sirket.SirketAdi} | Kullanıcı: {kullanici} | Geçici parola: {parola}");
        }
    }

    private async Task<IslemSonucu> DegistirAsync(string sirketKimligi, CancellationToken cancellationToken, Func<SirketKaydi, SirketIsletimDurumu, IslemSonucu> islem)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            HizmetleriKaliciKayitlaVeUygula();
            IslemSonucu sonuc = islem(SirketZorunlu(sirketKimligi), Durum(sirketKimligi));
            if (sonuc.Basarili) await TumunuKaydetAsync(cancellationToken);
            return sonuc;
        }
        finally { _kilit.Release(); }
    }

    private IEnumerable<PiyasaOlayi> AktifOlaylar() => _veri.PiyasaOlaylari.Where(o => o.BaslangicTicki <= _tick && o.BitisTicki >= _tick);
    private double KategoriOlayCarpani(string kategori, Func<PiyasaOlayi, double> secici) => AktifOlaylar().Where(o => o.EtkilenenKategori == "tum" || string.Equals(o.EtkilenenKategori, kategori, StringComparison.OrdinalIgnoreCase) || (o.EtkilenenKategori == "platform" && kategori is "platform" or "isletim-sistemi")).Select(secici).DefaultIfEmpty(1).Aggregate(1d, (a, b) => a * b);
    private static int HizmetYatirimKapasiteBonusu(SirketIsletimDurumu d) => Seviye(d, "cpu") * 2 + Seviye(d, "ram") + Seviye(d, "ag") * 2 + Seviye(d, "yedek");
    private static int EtkinKapasite(SirketKaydi s) => s.Hizmetler.Where(h => h.Aktif).Sum(h => h.AzamiEszamanliIs);
    private static int Seviye(SirketIsletimDurumu d, string tur) => d.YatirimSeviyeleri.TryGetValue(tur, out int x) ? Math.Max(0, x) : 0;
    private static decimal Maliyet(YatirimPaketi p, int seviye) => decimal.Round(p.TabanMaliyet * (seviye + 1) * (seviye + 1) * (1 + seviye * 0.06m), 2);
    private static decimal YatirimBakimGideri(string tur, int seviye) => seviye <= 0 ? 0 : tur switch { "cpu" => seviye * 95m, "ram" => seviye * 72m, "ag" => seviye * 88m, "depolama" => seviye * 64m, "guvenlik" => seviye * 125m, "yedek" => seviye * 180m, "destek" => seviye * 150m, "pazarlama" => seviye * 190m, "satis" => seviye * 210m, "arge" => seviye * 230m, _ => 0 };
    private static decimal YayinMaliyeti(string tur, string kategori, int platform, int bagimlilik) => (tur switch { "isletim-sistemi" => 48_000m, "platform" => 32_000m, "altyapi" => 24_000m, "veritabani" => 20_000m, "oyun" => 18_000m, _ => kategori switch { "sosyal-medya" => 16_000m, "eposta" => 12_000m, "guvenlik" => 18_000m, _ => 9_000m } }) + platform * 1_200m + bagimlilik * 900m;
    private static string Normal(string? s, string varsayilan) => string.IsNullOrWhiteSpace(s) ? varsayilan : s.Trim().ToLowerInvariant();
    private static string Anahtar(SunulanHizmet h) => Anahtar(h.HizmetKimligi, h.HizmetSurumu);
    private static string Anahtar(string kimlik, string surum) => $"{kimlik.Trim()}@{surum.Trim()}";
    private static decimal KullaniciGideri(UrunKaydi u) => u.UrunTuru switch { "isletim-sistemi" => 0.42m, "platform" => 0.34m, "altyapi" => 0.28m, _ => u.Kategori switch { "eposta" => 0.19m, "sosyal-medya" => 0.24m, "bulut-depolama" => 0.31m, "guvenlik" => 0.27m, _ => 0.14m } };
    private static double TabanTalep(UrunKaydi u) => u.UrunTuru switch { "isletim-sistemi" => 5.5, "platform" => 7.5, "altyapi" => 6.2, "oyun" => 11.0, _ => u.Kategori switch { "sosyal-medya" => 13, "eposta" => 10, "mesajlasma" => 12, "guvenlik" => 6, _ => 7 } };
    private static double FiyatCarpani(UrunKaydi u) { double fiyat = (double)(u.AbonelikUcreti + u.KullanimBasinaUcret * 20); return Math.Clamp(1.45 - fiyat / 180d, 0.12, 1.55); }
    private static decimal Gelir(UrunKaydi u, int yeni) => u.FiyatlandirmaModeli switch { "abonelik" => u.AktifKullaniciSayisi * u.AbonelikUcreti / 30m, "freemium" => u.AktifKullaniciSayisi * u.AbonelikUcreti / 80m + yeni * u.KullanimBasinaUcret * 3, "kullanim" => u.AktifKullaniciSayisi * u.KullanimBasinaUcret * 2, "lisans" or "tek-seferlik" => yeni * Math.Max(u.AbonelikUcreti, u.KullanimBasinaUcret), _ => 0 };

    private static void KullaniciKaybettir(SirketIsletimDurumu durum, int toplamKayip)
    {
        foreach (UrunKaydi u in durum.Urunler.Where(u => u.Aktif && u.AktifKullaniciSayisi > 0).OrderByDescending(u => u.AktifKullaniciSayisi))
        {
            if (toplamKayip <= 0) break;
            int kayip = Math.Min(u.AktifKullaniciSayisi, Math.Max(1, toplamKayip / 2));
            u.AktifKullaniciSayisi -= kayip; u.ToplamKaybedilenKullanici += kayip; toplamKayip -= kayip;
        }
    }

    private void OlayEkle(SirketIsletimDurumu durum, bool olumlu, string baslik, string aciklama, decimal finans, double itibar, double guvenlik, double performans, string varlik = "")
    {
        durum.SonOlaylar.Add(new SirketOlayKaydi { OlayKimligi = $"sirket-olay-{Guid.NewGuid():N}", TickNumarasi = _tick, Tur = olumlu ? "olumlu" : "olumsuz", Baslik = baslik, Aciklama = aciklama, EtkilenenVarlik = varlik, FinansalEtki = finans, ItibarEtkisi = itibar, GuvenlikEtkisi = guvenlik, PerformansEtkisi = performans, Olumlu = olumlu });
        if (durum.SonOlaylar.Count > 100) durum.SonOlaylar = durum.SonOlaylar.TakeLast(100).ToList();
    }

    private static void Ode(SirketKaydi s, SirketIsletimDurumu d, decimal tutar)
    {
        if (tutar <= 0) return;
        decimal odenen = Math.Min(Math.Max(0, s.Kasa), tutar); s.Kasa -= odenen; d.ToplamIsletmeGideri += odenen; d.OdenemeyenGider += tutar - odenen;
        if (tutar > odenen) { d.KrediNotu = Math.Clamp(d.KrediNotu - 5, 300, 900); s.GuvenilirlikPuani -= 0.12; }
    }

    private static void Degerle(SirketKaydi s, SirketIsletimDurumu d)
    {
        decimal borc = d.Krediler.Where(k => k.Aktif).Sum(k => k.KalanBorc);
        decimal urunKari = d.Urunler.Sum(u => Math.Max(0, u.ToplamGelir - u.ToplamGider));
        decimal duzenli = d.Urunler.Where(u => u.Aktif).Sum(u => u.AktifKullaniciSayisi * u.AbonelikUcreti / 30m);
        decimal kapasite = d.Urunler.Sum(u => u.KullaniciKapasitesi) * 3m + s.Hizmetler.Sum(h => h.AzamiEszamanliIs) * 180m;
        decimal gecmis = s.TamamlananIsSayisi * 28m + Math.Max(0, s.NetGelir) * 0.07m;
        decimal kalite = (decimal)(s.KodKalitesiPuani + s.PerformansPuani + s.GuvenlikPuani + s.ItibarPuani + s.GuvenilirlikPuani + s.OrtalamaMusteriMemnuniyeti) * 185m;
        decimal ekosistem = d.Urunler.Count(u => u.Aktif) * 6_000m + d.Urunler.Count(u => u.UrunTuru is "isletim-sistemi" or "platform") * 18_000m + d.BenimsenenProtokoller.Count * 2_500m + (decimal)d.EkosistemPuani * 250m;
        decimal yatirim = d.YatirimSeviyeleri.Values.Sum() * 4_000m;
        decimal risk = (decimal)(d.OperasyonRiski * 650 + d.TeknikBorc * 450 + d.BakimBaskisi * 250);
        d.SirketDegeri = decimal.Round(Math.Max(0, Math.Max(0, s.Kasa) * 0.15m + urunKari * 5m + duzenli * 34m + d.ToplamAboneSayisi * 58m + kapasite + gecmis + kalite + ekosistem + yatirim - borc * 1.15m - d.OdenemeyenGider * 1.5m - risk), 2);
        d.TahminiHisseFiyati = decimal.Round(d.SirketDegeri / 10_000m, 4);
    }

    private HizmetKaliciAyari? HizmetAyariniBul(SirketIsletimDurumu d, string anahtar) => d.HizmetAyarlari.TryGetValue(anahtar, out HizmetKaliciAyari? x) ? x : null;
    private SirketIsletimDurumu Durum(string kimlik) { SirketIsletimDurumu? d = _veri.Sirketler.FirstOrDefault(x => string.Equals(x.SirketKimligi, kimlik, StringComparison.OrdinalIgnoreCase)); if (d is not null) return d; d = new() { SirketKimligi = kimlik }; _veri.Sirketler.Add(d); return d; }
    private SirketKaydi SirketZorunlu(string kimlik) => SirketBul(kimlik) ?? throw new InvalidOperationException("Şirket bulunamadı.");
    private SirketKaydi? SirketBul(string kimlik) => _sirketler.SirketKayitlari.FirstOrDefault(s => string.Equals(s.SirketKimligi, kimlik, StringComparison.OrdinalIgnoreCase));

    private void IslemEkle(SirketIsletimDurumu d, string tur, string aciklama, decimal tutar)
    {
        d.SonIslemler.Add(new IsletimIslemKaydi { IslemKimligi = $"islem-{Guid.NewGuid():N}", TickNumarasi = _tick, IslemTuru = tur, Aciklama = aciklama, Tutar = tutar });
        if (d.SonIslemler.Count > 250) d.SonIslemler = d.SonIslemler.TakeLast(250).ToList();
    }

    private async Task TumunuKaydetAsync(CancellationToken cancellationToken) { await KaydetKilitsizAsync(cancellationToken); await _sirketler.BilancolariKaydetAsync(cancellationToken); }
    private async Task KaydetKilitsizAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!); _veri.GuncellenmeZamani = DateTimeOffset.UtcNow;
        string gecici = _dosyaYolu + ".tmp"; await File.WriteAllTextAsync(gecici, JsonSerializer.Serialize(_veri, JsonAyarlari), new UTF8Encoding(false), cancellationToken); File.Move(gecici, _dosyaYolu, true);
    }

    private static (string, string) VarsayilanGiris(SirketKaydi s)
    {
        string k = s.SirketKimligi.ToLowerInvariant();
        if (k.Contains("tunix")) return ("tunix", "tunix123"); if (k.Contains("mudaf")) return ("mudaf", "mudaf123"); if (k.Contains("ugax")) return ("ugax", "ugax123"); if (k.Contains("ilos")) return ("ilos", "ilos123");
        string u = new(s.SirketAdi.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()); if (string.IsNullOrWhiteSpace(u)) u = "sirket"; return (u, $"{u}123");
    }
    private static string Ozetle(string tuz, string parola) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{tuz}:{parola}")));
    private static bool SabitZamanliEsit(string a, string b) { try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(a), Convert.FromHexString(b)); } catch { return false; } }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try { if (_baslatildi) { await _kilit.WaitAsync(); try { await TumunuKaydetAsync(CancellationToken.None); } finally { _kilit.Release(); } } }
        catch (Exception e) { KonsolKayitcisi.Hata($"İşletim verileri kaydedilemedi: {e.Message}"); }
        finally { _disposed = true; _kilit.Dispose(); }
    }
}
