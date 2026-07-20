using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SirketMotoru.Kayit;
using SirketMotoru.Protokol;
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
        new() { YatirimTuru = "cpu", Ad = "İşlemci Kümesi", Aciklama = "Ham hizmet kapasitesini artırır.", AzamiSeviye = 20, TabanMaliyet = 2_500 },
        new() { YatirimTuru = "ram", Ad = "Bellek Havuzu", Aciklama = "Hizmet ve ürün kapasitesini artırır.", AzamiSeviye = 20, TabanMaliyet = 2_000 },
        new() { YatirimTuru = "ag", Ad = "Ağ Omurgası", Aciklama = "Eş zamanlı iş ve kullanıcı kapasitesini artırır.", AzamiSeviye = 20, TabanMaliyet = 2_250 },
        new() { YatirimTuru = "depolama", Ad = "Depolama Kümesi", Aciklama = "E-posta, sosyal medya ve bulut kapasitesini artırır.", AzamiSeviye = 20, TabanMaliyet = 1_500 },
        new() { YatirimTuru = "guvenlik", Ad = "Güvenlik Operasyonu", Aciklama = "Güvenlik olaylarının ekonomik etkisini azaltır.", AzamiSeviye = 10, TabanMaliyet = 3_500 },
        new() { YatirimTuru = "yedek", Ad = "Yedek Sunucu", Aciklama = "Kesintilerde sözleşme ihlalini azaltır.", AzamiSeviye = 5, TabanMaliyet = 8_000 },
        new() { YatirimTuru = "destek", Ad = "Müşteri Destek Ekibi", Aciklama = "Abone kaybını azaltır.", AzamiSeviye = 10, TabanMaliyet = 3_000 },
        new() { YatirimTuru = "pazarlama", Ad = "Pazarlama Departmanı", Aciklama = "Yeni kullanıcı kazanımını artırır.", AzamiSeviye = 10, TabanMaliyet = 4_000 },
        new() { YatirimTuru = "satis", Ad = "Kurumsal Satış", Aciklama = "Sözleşme ve şirket değerini artırır.", AzamiSeviye = 10, TabanMaliyet = 4_500 },
        new() { YatirimTuru = "arge", Ad = "Ar-Ge Laboratuvarı", Aciklama = "Yeni ürün başlangıç kalitesini artırır.", AzamiSeviye = 10, TabanMaliyet = 5_000 }
    ];

    private static readonly KrediPaketi[] Krediler =
    [
        new() { KrediTuru = "isletme", Ad = "İşletme Kredisi", AsgariTutar = 1_000, AzamiTutar = 50_000, TickFaizOrani = 0.008m, TaksitSayisi = 12, OdemeAraligiTick = 5, AsgariKrediNotu = 450 },
        new() { KrediTuru = "altyapi", Ad = "Altyapı Kredisi", AsgariTutar = 5_000, AzamiTutar = 150_000, TickFaizOrani = 0.006m, TaksitSayisi = 16, OdemeAraligiTick = 5, AsgariKrediNotu = 550 },
        new() { KrediTuru = "arge", Ad = "Ar-Ge Kredisi", AsgariTutar = 10_000, AzamiTutar = 250_000, TickFaizOrani = 0.005m, TaksitSayisi = 20, OdemeAraligiTick = 5, AsgariKrediNotu = 650 },
        new() { KrediTuru = "acil", Ad = "Acil Likidite", AsgariTutar = 500, AzamiTutar = 20_000, TickFaizOrani = 0.015m, TaksitSayisi = 8, OdemeAraligiTick = 3, AsgariKrediNotu = 300 }
    ];

    private static readonly string[] Kategoriler =
    [
        "eposta", "sosyal-medya", "mesajlasma", "bulut-depolama",
        "api", "analitik", "guvenlik", "e-ticaret", "gelistirici-araci", "diger"
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

    public SirketIsletimYoneticisi(
        SirketYoneticisi sirketler,
        string motorVerileriKlasoru)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _dosyaYolu = Path.Combine(
            Path.GetFullPath(motorVerileriKlasoru),
            "sirket-isletim.json");
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
                _veri = JsonSerializer.Deserialize<SirketIsletimDosyasi>(
                            json,
                            JsonAyarlari)
                        ?? new SirketIsletimDosyasi();
            }

            KayitlariTamamla();
            YatirimlariUygula();
            await KaydetKilitsizAsync(cancellationToken);
            _baslatildi = true;

            KonsolKayitcisi.Basari(
                $"Şirket işletim sistemi hazır | Portala kayıtlı hesap: {_veri.Hesaplar.Count}");
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async Task<IslemSonucu> GirisDogrulaAsync(
        GirisIstegi istek,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            SirketHesabi? hesap = _veri.Hesaplar.FirstOrDefault(
                h => string.Equals(
                    h.KullaniciAdi,
                    istek.KullaniciAdi?.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (hesap is null ||
                !SabitZamanliEsit(
                    hesap.ParolaOzeti,
                    Ozetle(hesap.ParolaTuzu, istek.Parola ?? string.Empty)))
            {
                return IslemSonucu.Hata("Kullanıcı adı veya parola hatalı.");
            }

            hesap.SonGirisZamani = DateTimeOffset.UtcNow;
            await KaydetKilitsizAsync(cancellationToken);
            SirketKaydi? sirket = SirketBul(hesap.SirketKimligi);

            return IslemSonucu.Basari(
                "Giriş başarılı.",
                new
                {
                    hesap.SirketKimligi,
                    sirketAdi = sirket?.SirketAdi ?? hesap.SirketKimligi,
                    hesap.ParolaDegistirilmeli
                });
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async Task<IslemSonucu> ParolaDegistirAsync(
        string sirketKimligi,
        ParolaDegistirIstegi istek,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            SirketHesabi? hesap = _veri.Hesaplar.FirstOrDefault(
                h => string.Equals(
                    h.SirketKimligi,
                    sirketKimligi,
                    StringComparison.OrdinalIgnoreCase));

            if (hesap is null ||
                !SabitZamanliEsit(
                    hesap.ParolaOzeti,
                    Ozetle(hesap.ParolaTuzu, istek.EskiParola ?? string.Empty)))
            {
                return IslemSonucu.Hata("Mevcut parola hatalı.");
            }

            if (istek.YeniParola is null ||
                istek.YeniParola.Length is < 8 or > 128)
            {
                return IslemSonucu.Hata("Yeni parola 8-128 karakter olmalıdır.");
            }

            hesap.ParolaTuzu = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(16));
            hesap.ParolaOzeti = Ozetle(hesap.ParolaTuzu, istek.YeniParola);
            hesap.ParolaDegistirilmeli = false;
            await KaydetKilitsizAsync(cancellationToken);
            return IslemSonucu.Basari("Parola değiştirildi.");
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async Task<string> PanelJsonuOlusturAsync(
        string sirketKimligi,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            YatirimlariUygula();
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            SirketIsletimDurumu durum = Durum(sirketKimligi);
            SirketHesabi? hesap = _veri.Hesaplar.FirstOrDefault(
                h => string.Equals(
                    h.SirketKimligi,
                    sirketKimligi,
                    StringComparison.OrdinalIgnoreCase));

            object cevap = new
            {
                tickNumarasi = _tick,
                sirket = new
                {
                    sirket.SirketKimligi,
                    sirket.SirketAdi,
                    sirket.Kasa,
                    sirket.NetGelir,
                    sirket.BagliMi,
                    sirket.KodKalitesiPuani,
                    sirket.PerformansPuani,
                    sirket.GuvenlikPuani,
                    sirket.ItibarPuani,
                    sirket.GuvenilirlikPuani,
                    sirket.OrtalamaMusteriMemnuniyeti,
                    sirket.TamamlananIsSayisi,
                    sirket.BasarisizIsSayisi,
                    hizmetler = sirket.Hizmetler
                },
                isletim = new
                {
                    durum.KrediNotu,
                    durum.EkosistemPuani,
                    durum.ToplamAboneSayisi,
                    durum.ToplamAbonelikGeliri,
                    durum.ToplamUrunGeliri,
                    durum.ToplamIsletmeGideri,
                    durum.ToplamFinansmanGideri,
                    durum.ToplamYatirimHarcamasi,
                    durum.OdenemeyenGider,
                    durum.SirketDegeri,
                    durum.TahminiHisseFiyati,
                    toplamBorc = durum.Krediler.Where(k => k.Aktif).Sum(k => k.KalanBorc),
                    durum.TemerrutSayisi,
                    durum.YatirimSeviyeleri,
                    durum.Krediler,
                    durum.Urunler,
                    durum.Sozlesmeler,
                    sonIslemler = durum.SonIslemler.OrderByDescending(i => i.Zaman).Take(50)
                },
                yatirimMagazasi = Yatirimlar.Select(p =>
                {
                    int seviye = Seviye(durum, p.YatirimTuru);
                    return new
                    {
                        p.YatirimTuru,
                        p.Ad,
                        p.Aciklama,
                        p.AzamiSeviye,
                        seviye,
                        sonrakiMaliyet = Maliyet(p, seviye)
                    };
                }),
                krediPaketleri = Krediler,
                urunKategorileri = Kategoriler,
                protokoller = _veri.Protokoller.Where(p => p.Aktif),
                sozlesmeTeklifleri = _veri.SozlesmeTeklifleri.Where(
                    t => t.Aktif &&
                         string.IsNullOrWhiteSpace(t.KabulEdenSirketKimligi) &&
                         t.SonKabulTicki >= _tick),
                piyasaOlaylari = _veri.PiyasaOlaylari.Where(o => o.BitisTicki >= _tick),
                parolaDegistirilmeli = hesap?.ParolaDegistirilmeli ?? false
            };

            return JsonSerializer.Serialize(cevap, JsonAyarlari);
        }
        finally
        {
            _kilit.Release();
        }
    }

    public Task<IslemSonucu> YatirimSatinAlAsync(
        string sirketKimligi,
        YatirimIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            YatirimPaketi? paket = Yatirimlar.FirstOrDefault(
                p => string.Equals(
                    p.YatirimTuru,
                    istek.YatirimTuru?.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (paket is null) return IslemSonucu.Hata("Bilinmeyen yatırım.");
            int seviye = Seviye(durum, paket.YatirimTuru);
            if (seviye >= paket.AzamiSeviye)
                return IslemSonucu.Hata("Azami seviyeye ulaşıldı.");

            decimal maliyet = Maliyet(paket, seviye);
            if (sirket.Kasa < maliyet)
                return IslemSonucu.Hata($"Yetersiz kasa. Gerekli: {maliyet:N2} TL.");

            sirket.Kasa -= maliyet;
            durum.YatirimSeviyeleri[paket.YatirimTuru] = seviye + 1;
            durum.ToplamYatirimHarcamasi += maliyet;
            IslemEkle(durum, "yatirim", $"{paket.Ad} seviyesi {seviye + 1} oldu.", -maliyet);
            YatirimlariUygula();
            Degerle(sirket, durum);
            return IslemSonucu.Basari("Yatırım tamamlandı.");
        });

    public Task<IslemSonucu> HizmetFiyatiGuncelleAsync(
        string sirketKimligi,
        FiyatGuncelleIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            if (istek.YeniFiyat is <= 0 or > 1_000_000)
                return IslemSonucu.Hata("Fiyat geçersiz.");

            SunulanHizmet? hizmet = sirket.Hizmetler.FirstOrDefault(h =>
                string.Equals(h.HizmetKimligi, istek.HizmetKimligi, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(h.HizmetSurumu, istek.HizmetSurumu, StringComparison.OrdinalIgnoreCase));

            if (hizmet is null) return IslemSonucu.Hata("Hizmet ilan edilmiyor.");
            hizmet.BirimFiyat = decimal.Round(istek.YeniFiyat, 2);
            durum.HizmetFiyatEzmeDegerleri[Anahtar(hizmet)] = hizmet.BirimFiyat;
            IslemEkle(durum, "fiyat", $"{Anahtar(hizmet)} fiyatı güncellendi.", 0);
            return IslemSonucu.Basari("Fiyat güncellendi.");
        });

    public Task<IslemSonucu> KrediCekAsync(
        string sirketKimligi,
        KrediIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            KrediPaketi? paket = Krediler.FirstOrDefault(
                p => string.Equals(p.KrediTuru, istek.KrediTuru, StringComparison.OrdinalIgnoreCase));

            if (paket is null) return IslemSonucu.Hata("Kredi paketi bulunamadı.");
            if (durum.KrediNotu < paket.AsgariKrediNotu)
                return IslemSonucu.Hata("Kredi notu yetersiz.");
            if (istek.Tutar < paket.AsgariTutar || istek.Tutar > paket.AzamiTutar)
                return IslemSonucu.Hata("Kredi tutarı paket sınırlarının dışında.");

            decimal mevcutBorc = durum.Krediler.Where(k => k.Aktif).Sum(k => k.KalanBorc);
            decimal limit = Math.Max(10_000, durum.SirketDegeri * 0.70m + sirket.Kasa);
            if (mevcutBorc + istek.Tutar > limit)
                return IslemSonucu.Hata($"Borç limiti aşılıyor. Limit: {limit:N2} TL.");

            decimal toplam = decimal.Round(
                istek.Tutar * (1 + paket.TickFaizOrani * paket.TaksitSayisi),
                2);
            KrediKaydi kredi = new()
            {
                KrediKimligi = $"kredi-{Guid.NewGuid():N}",
                KrediTuru = paket.KrediTuru,
                AnaPara = decimal.Round(istek.Tutar, 2),
                KalanBorc = toplam,
                TickFaizOrani = paket.TickFaizOrani,
                TaksitTutari = decimal.Round(toplam / paket.TaksitSayisi, 2),
                KalanTaksit = paket.TaksitSayisi,
                SonrakiOdemeTicki = _tick + paket.OdemeAraligiTick,
                OdemeAraligiTick = paket.OdemeAraligiTick
            };
            durum.Krediler.Add(kredi);
            sirket.Kasa += kredi.AnaPara;
            durum.KrediNotu = Math.Clamp(durum.KrediNotu - 3, 300, 900);
            IslemEkle(durum, "kredi", $"{paket.Ad}: {kredi.AnaPara:N2} TL.", kredi.AnaPara);
            Degerle(sirket, durum);
            return IslemSonucu.Basari("Kredi kasaya aktarıldı.", kredi);
        });

    public Task<IslemSonucu> UrunOlusturAsync(
        string sirketKimligi,
        UrunOlusturIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            string ad = istek.UrunAdi?.Trim() ?? string.Empty;
            string kategori = (istek.Kategori?.Trim() ?? string.Empty).ToLowerInvariant();
            string model = (istek.FiyatlandirmaModeli?.Trim() ?? "abonelik").ToLowerInvariant();

            if (ad.Length is < 3 or > 80) return IslemSonucu.Hata("Ürün adı geçersiz.");
            if (!Kategoriler.Contains(kategori, StringComparer.OrdinalIgnoreCase))
                return IslemSonucu.Hata("Kategori geçersiz.");
            if (model is not ("abonelik" or "kullanim" or "freemium" or "lisans"))
                return IslemSonucu.Hata("Fiyatlandırma modeli geçersiz.");
            if (durum.Urunler.Any(u => string.Equals(u.UrunAdi, ad, StringComparison.OrdinalIgnoreCase)))
                return IslemSonucu.Hata("Aynı adlı ürün var.");

            decimal maliyet = kategori switch
            {
                "sosyal-medya" => 12_000,
                "eposta" => 8_000,
                "bulut-depolama" => 10_000,
                "mesajlasma" => 9_000,
                "guvenlik" => 11_000,
                _ => 6_000
            };
            maliyet = decimal.Round(
                maliyet * Math.Max(0.65m, 1 - Seviye(durum, "arge") * 0.025m),
                2);
            if (sirket.Kasa < maliyet)
                return IslemSonucu.Hata($"Ürün çıkarma maliyeti: {maliyet:N2} TL.");

            UrunKaydi urun = new()
            {
                UrunKimligi = $"urun-{Guid.NewGuid():N}",
                UrunAdi = ad,
                Kategori = kategori,
                FiyatlandirmaModeli = model,
                AbonelikUcreti = decimal.Round(Math.Max(0, istek.AbonelikUcreti), 2),
                KullanimBasinaUcret = decimal.Round(Math.Max(0, istek.KullanimBasinaUcret), 4),
                KullaniciKapasitesi = 100 +
                    Seviye(durum, "depolama") * 150 +
                    Seviye(durum, "ram") * 75 +
                    Seviye(durum, "ag") * 75,
                UrunKalitesi = Math.Clamp(
                    45 + Seviye(durum, "arge") * 3 + sirket.KodKalitesiPuani * 0.15,
                    0,
                    100),
                ArkaUcHizmetKimligi = istek.ArkaUcHizmetKimligi?.Trim() ?? string.Empty,
                ArkaUcHizmetSurumu = string.IsNullOrWhiteSpace(istek.ArkaUcHizmetSurumu)
                    ? "1.0"
                    : istek.ArkaUcHizmetSurumu.Trim(),
                ProtokolKimligi = istek.ProtokolKimligi?.Trim() ?? string.Empty,
                YayinTicki = _tick
            };

            sirket.Kasa -= maliyet;
            durum.ToplamYatirimHarcamasi += maliyet;
            durum.Urunler.Add(urun);
            IslemEkle(durum, "urun", $"{urun.UrunAdi} piyasaya çıktı.", -maliyet);
            Degerle(sirket, durum);
            return IslemSonucu.Basari("Ürün piyasaya çıkarıldı.", urun);
        });

    public Task<IslemSonucu> UrunGuncelleAsync(
        string sirketKimligi,
        UrunGuncelleIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (_, durum) =>
        {
            UrunKaydi? urun = durum.Urunler.FirstOrDefault(
                u => string.Equals(u.UrunKimligi, istek.UrunKimligi, StringComparison.OrdinalIgnoreCase));
            if (urun is null) return IslemSonucu.Hata("Ürün bulunamadı.");

            urun.AbonelikUcreti = decimal.Round(Math.Max(0, istek.AbonelikUcreti), 2);
            urun.KullanimBasinaUcret = decimal.Round(Math.Max(0, istek.KullanimBasinaUcret), 4);
            urun.Aktif = istek.Aktif;
            IslemEkle(durum, "urun-guncelleme", $"{urun.UrunAdi} güncellendi.", 0);
            return IslemSonucu.Basari("Ürün güncellendi.");
        });

    public Task<IslemSonucu> UrunKapasitesiArtirAsync(
        string sirketKimligi,
        UrunKapasiteIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            UrunKaydi? urun = durum.Urunler.FirstOrDefault(
                u => string.Equals(u.UrunKimligi, istek.UrunKimligi, StringComparison.OrdinalIgnoreCase));
            if (urun is null) return IslemSonucu.Hata("Ürün bulunamadı.");
            if (istek.EklenecekKapasite is < 100 or > 100_000)
                return IslemSonucu.Hata("Kapasite artışı 100-100.000 arasında olmalıdır.");

            decimal maliyet = istek.EklenecekKapasite * (
                urun.Kategori == "bulut-depolama" ? 3m :
                urun.Kategori == "sosyal-medya" ? 2m : 1.25m);
            if (sirket.Kasa < maliyet)
                return IslemSonucu.Hata($"Gerekli tutar: {maliyet:N2} TL.");

            sirket.Kasa -= maliyet;
            urun.KullaniciKapasitesi += istek.EklenecekKapasite;
            durum.ToplamYatirimHarcamasi += maliyet;
            IslemEkle(durum, "urun-kapasite", $"{urun.UrunAdi} kapasitesi artırıldı.", -maliyet);
            return IslemSonucu.Basari("Ürün kapasitesi artırıldı.");
        });

    public Task<IslemSonucu> ProtokolOlusturAsync(
        string sirketKimligi,
        ProtokolOlusturIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            string ad = istek.ProtokolAdi?.Trim() ?? string.Empty;
            if (ad.Length is < 3 or > 80) return IslemSonucu.Hata("Protokol adı geçersiz.");
            if (_veri.Protokoller.Any(p =>
                    string.Equals(p.ProtokolAdi, ad, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.Surum, istek.Surum, StringComparison.OrdinalIgnoreCase)))
                return IslemSonucu.Hata("Bu protokol sürümü zaten var.");

            decimal maliyet = Math.Max(2_500, 8_000 - Seviye(durum, "arge") * 450);
            if (sirket.Kasa < maliyet)
                return IslemSonucu.Hata($"Protokol yayın maliyeti: {maliyet:N2} TL.");

            string lisans = (istek.LisansModeli?.Trim() ?? "acik").ToLowerInvariant();
            OzelProtokolKaydi protokol = new()
            {
                ProtokolKimligi = $"protokol-{Guid.NewGuid():N}",
                SahipSirketKimligi = sirketKimligi,
                ProtokolAdi = ad,
                Surum = string.IsNullOrWhiteSpace(istek.Surum) ? "1.0" : istek.Surum.Trim(),
                Aciklama = istek.Aciklama?.Trim() ?? string.Empty,
                LisansModeli = lisans,
                BenimsemeBedeli = lisans == "acik" ? 0 : Math.Max(0, istek.BenimsemeBedeli),
                TickLisansBedeli = lisans is "abonelik" or "karma"
                    ? Math.Max(0, istek.TickLisansBedeli)
                    : 0,
                YayinTicki = _tick
            };

            sirket.Kasa -= maliyet;
            durum.ToplamYatirimHarcamasi += maliyet;
            durum.EkosistemPuani = Math.Clamp(durum.EkosistemPuani + 1.5, 0, 100);
            _veri.Protokoller.Add(protokol);
            IslemEkle(durum, "protokol", $"{ad}@{protokol.Surum} yayımlandı.", -maliyet);
            return IslemSonucu.Basari("Protokol yayımlandı.", protokol);
        });

    public Task<IslemSonucu> ProtokolBenimseAsync(
        string sirketKimligi,
        ProtokolBenimseIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            OzelProtokolKaydi? protokol = _veri.Protokoller.FirstOrDefault(
                p => p.Aktif &&
                     string.Equals(p.ProtokolKimligi, istek.ProtokolKimligi, StringComparison.OrdinalIgnoreCase));
            if (protokol is null) return IslemSonucu.Hata("Protokol bulunamadı.");
            if (string.Equals(protokol.SahipSirketKimligi, sirketKimligi, StringComparison.OrdinalIgnoreCase))
                return IslemSonucu.Hata("Şirket protokolün sahibidir.");
            if (protokol.BenimseyenSirketler.Contains(sirketKimligi, StringComparer.OrdinalIgnoreCase))
                return IslemSonucu.Hata("Protokol zaten benimsenmiş.");
            if (sirket.Kasa < protokol.BenimsemeBedeli)
                return IslemSonucu.Hata("Benimseme bedeli için kasa yetersiz.");

            sirket.Kasa -= protokol.BenimsemeBedeli;
            SirketKaydi? sahip = SirketBul(protokol.SahipSirketKimligi);
            if (sahip is not null)
            {
                sahip.Kasa += protokol.BenimsemeBedeli;
                sahip.ToplamGelir += protokol.BenimsemeBedeli;
            }

            protokol.ToplamLisansGeliri += protokol.BenimsemeBedeli;
            protokol.BenimseyenSirketler.Add(sirketKimligi);
            durum.BenimsenenProtokoller.Add(protokol.ProtokolKimligi);
            durum.EkosistemPuani = Math.Clamp(durum.EkosistemPuani + 0.75, 0, 100);
            IslemEkle(durum, "protokol-benimseme", $"{protokol.ProtokolAdi} benimsendi.", -protokol.BenimsemeBedeli);
            return IslemSonucu.Basari("Protokol benimsendi.");
        });

    public Task<IslemSonucu> SozlesmeKabulEtAsync(
        string sirketKimligi,
        SozlesmeKabulIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            SozlesmeTeklifi? teklif = _veri.SozlesmeTeklifleri.FirstOrDefault(t =>
                t.Aktif &&
                string.IsNullOrWhiteSpace(t.KabulEdenSirketKimligi) &&
                t.SonKabulTicki >= _tick &&
                string.Equals(t.TeklifKimligi, istek.TeklifKimligi, StringComparison.OrdinalIgnoreCase));

            if (teklif is null) return IslemSonucu.Hata("Teklif bulunamadı.");
            if (sirket.KodKalitesiPuani < teklif.AsgariKalite ||
                sirket.PerformansPuani < teklif.AsgariPerformans ||
                sirket.GuvenlikPuani < teklif.AsgariGuvenlik ||
                EtkinKapasite(sirket, durum) < teklif.GerekliKapasite)
                return IslemSonucu.Hata("SLA koşulları karşılanmıyor.");

            teklif.KabulEdenSirketKimligi = sirketKimligi;
            teklif.Aktif = false;
            durum.Sozlesmeler.Add(new SozlesmeKaydi
            {
                SozlesmeKimligi = $"sozlesme-{Guid.NewGuid():N}",
                TeklifKimligi = teklif.TeklifKimligi,
                Baslik = teklif.Baslik,
                Kategori = teklif.Kategori,
                BaslangicTicki = _tick,
                BitisTicki = _tick + teklif.SureTick,
                TickOdemesi = teklif.TickOdemesi,
                IhlalCezasi = teklif.IhlalCezasi,
                AsgariKalite = teklif.AsgariKalite,
                AsgariPerformans = teklif.AsgariPerformans,
                AsgariGuvenlik = teklif.AsgariGuvenlik,
                GerekliKapasite = teklif.GerekliKapasite
            });
            IslemEkle(durum, "sozlesme", $"{teklif.Baslik} kabul edildi.", 0);
            return IslemSonucu.Basari("Sözleşme kabul edildi.");
        });

    public async Task TickCalistirAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            _tick = tickNumarasi;
            _veri.SonIslenenTick = tickNumarasi;
            KayitlariTamamla();
            YatirimlariUygula();
            OlayOlustur();
            TeklifOlustur();

            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
            {
                SirketIsletimDurumu durum = Durum(sirket.SirketKimligi);
                GiderleriIsle(sirket, durum);
                KredileriIsle(sirket, durum);
                UrunleriIsle(sirket, durum);
                SozlesmeleriIsle(sirket, durum);
                Degerle(sirket, durum);
            }

            LisanslariIsle();
            await TumunuKaydetAsync(cancellationToken);
        }
        finally
        {
            _kilit.Release();
        }
    }

    private async Task<IslemSonucu> DegistirAsync(
        string sirketKimligi,
        CancellationToken cancellationToken,
        Func<SirketKaydi, SirketIsletimDurumu, IslemSonucu> islem)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            IslemSonucu sonuc = islem(SirketZorunlu(sirketKimligi), Durum(sirketKimligi));
            if (sonuc.Basarili) await TumunuKaydetAsync(cancellationToken);
            return sonuc;
        }
        finally
        {
            _kilit.Release();
        }
    }

    private void KayitlariTamamla()
    {
        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
        {
            _ = Durum(sirket.SirketKimligi);

            if (_veri.Hesaplar.Any(h => string.Equals(
                    h.SirketKimligi,
                    sirket.SirketKimligi,
                    StringComparison.OrdinalIgnoreCase)))
                continue;

            (string kullanici, string parola) = VarsayilanGiris(sirket);
            string tuz = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            _veri.Hesaplar.Add(new SirketHesabi
            {
                SirketKimligi = sirket.SirketKimligi,
                KullaniciAdi = kullanici,
                ParolaTuzu = tuz,
                ParolaOzeti = Ozetle(tuz, parola),
                ParolaDegistirilmeli = true
            });

            KonsolKayitcisi.Uyari(
                $"Yönetim hesabı | {sirket.SirketAdi} | Kullanıcı: {kullanici} | Geçici parola: {parola}");
        }
    }

    private void YatirimlariUygula()
    {
        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
        {
            SirketIsletimDurumu durum = Durum(sirket.SirketKimligi);
            int bonus = KapasiteBonusu(durum);

            foreach (SunulanHizmet hizmet in sirket.Hizmetler)
            {
                string anahtar = Anahtar(hizmet);
                if (!durum.HizmetBazKapasiteleri.ContainsKey(anahtar))
                    durum.HizmetBazKapasiteleri[anahtar] = Math.Max(1, hizmet.AzamiEszamanliIs);

                hizmet.AzamiEszamanliIs =
                    Math.Max(1, durum.HizmetBazKapasiteleri[anahtar] + bonus);

                if (durum.HizmetFiyatEzmeDegerleri.TryGetValue(anahtar, out decimal fiyat))
                    hizmet.BirimFiyat = fiyat;
            }
        }
    }

    private void GiderleriIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        decimal tutar =
            Seviye(durum, "cpu") * 25m +
            Seviye(durum, "ram") * 18m +
            Seviye(durum, "ag") * 20m +
            Seviye(durum, "depolama") * 12m +
            Seviye(durum, "guvenlik") * 30m +
            Seviye(durum, "yedek") * 55m +
            Seviye(durum, "destek") * 35m +
            Seviye(durum, "pazarlama") * 45m +
            Seviye(durum, "satis") * 45m +
            Seviye(durum, "arge") * 60m +
            durum.Urunler.Count(u => u.Aktif) * 20m;

        double olay = AktifOlaylar().Select(o => o.GiderCarpani).DefaultIfEmpty(1).Aggregate(1d, (a, b) => a * b);
        Ode(sirket, durum, decimal.Round(tutar * (decimal)Math.Clamp(olay, 0.5, 3), 2));
    }

    private void KredileriIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        foreach (KrediKaydi kredi in durum.Krediler.Where(k => k.Aktif && _tick >= k.SonrakiOdemeTicki))
        {
            decimal odeme = Math.Min(kredi.TaksitTutari, kredi.KalanBorc);
            if (sirket.Kasa >= odeme)
            {
                sirket.Kasa -= odeme;
                kredi.KalanBorc = Math.Max(0, kredi.KalanBorc - odeme);
                kredi.KalanTaksit = Math.Max(0, kredi.KalanTaksit - 1);
                kredi.SonrakiOdemeTicki = _tick + kredi.OdemeAraligiTick;
                durum.ToplamFinansmanGideri += odeme;
                durum.KrediNotu = Math.Clamp(durum.KrediNotu + 2, 300, 900);
                if (kredi.KalanBorc <= 0 || kredi.KalanTaksit <= 0) kredi.Aktif = false;
            }
            else
            {
                kredi.GecikmeSayisi++;
                kredi.KalanBorc = decimal.Round(kredi.KalanBorc * (1 + kredi.TickFaizOrani * 2), 2);
                kredi.SonrakiOdemeTicki = _tick + kredi.OdemeAraligiTick;
                durum.TemerrutSayisi++;
                durum.KrediNotu = Math.Clamp(durum.KrediNotu - 35, 300, 900);
                sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - 1.25);
            }
        }
    }

    private void UrunleriIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        int toplam = 0;

        foreach (UrunKaydi urun in durum.Urunler.Where(u => u.Aktif))
        {
            bool arkaUc = string.IsNullOrWhiteSpace(urun.ArkaUcHizmetKimligi) ||
                sirket.Hizmetler.Any(h =>
                    h.Aktif &&
                    string.Equals(h.HizmetKimligi, urun.ArkaUcHizmetKimligi, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(h.HizmetSurumu, urun.ArkaUcHizmetSurumu, StringComparison.OrdinalIgnoreCase));

            double talep = TabanTalep(urun.Kategori) *
                TalepCarpani(urun.Kategori) *
                (1 + Seviye(durum, "pazarlama") * 0.12) *
                Math.Clamp((sirket.KodKalitesiPuani + sirket.PerformansPuani + sirket.GuvenlikPuani + urun.UrunKalitesi) / 300d, 0.2, 1.5) *
                FiyatCarpani(urun) *
                (sirket.BagliMi && arkaUc ? 1 : Seviye(durum, "yedek") > 0 ? 0.55 : 0.15) *
                (0.75 + _rastgele.NextDouble() * 0.55);

            int yeni = Math.Max(0, (int)Math.Floor(talep));
            int alinan = Math.Min(yeni, Math.Max(0, urun.KullaniciKapasitesi - urun.AktifKullaniciSayisi));
            urun.AktifKullaniciSayisi += alinan;
            urun.ToplamEdinilenKullanici += alinan;

            double kayipOrani = 0.006 + Math.Max(0, 65 - urun.UrunMemnuniyeti) / 1_500d;
            kayipOrani *= Math.Max(0.35, 1 - Seviye(durum, "destek") * 0.055);
            if (!sirket.BagliMi || !arkaUc) kayipOrani += 0.08;
            if (urun.AktifKullaniciSayisi >= urun.KullaniciKapasitesi) kayipOrani += 0.04;

            int kayip = Math.Min(
                urun.AktifKullaniciSayisi,
                (int)Math.Ceiling(urun.AktifKullaniciSayisi * Math.Clamp(kayipOrani, 0, 0.5)));
            urun.AktifKullaniciSayisi -= kayip;
            urun.ToplamKaybedilenKullanici += kayip;

            decimal gelir = Gelir(urun, alinan);
            decimal gider = decimal.Round(urun.AktifKullaniciSayisi * KullaniciGideri(urun.Kategori), 2);

            sirket.Kasa += gelir;
            sirket.ToplamGelir += gelir;
            urun.ToplamGelir += gelir;
            durum.ToplamUrunGeliri += gelir;
            if (urun.FiyatlandirmaModeli is "abonelik" or "freemium")
                durum.ToplamAbonelikGeliri += gelir;

            Ode(sirket, durum, gider);
            urun.ToplamGider += gider;

            double gozlem = Math.Clamp(
                sirket.KodKalitesiPuani * 0.35 +
                sirket.PerformansPuani * 0.30 +
                sirket.GuvenlikPuani * 0.20 +
                (arkaUc ? 15 : 0),
                0,
                100);
            urun.UrunKalitesi += (gozlem - urun.UrunKalitesi) * 0.03;
            urun.UrunMemnuniyeti += (urun.UrunKalitesi - urun.UrunMemnuniyeti) *
                (urun.UrunKalitesi < urun.UrunMemnuniyeti ? 0.08 : 0.02);

            toplam += urun.AktifKullaniciSayisi;
        }

        durum.ToplamAboneSayisi = toplam;
    }

    private void SozlesmeleriIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        foreach (SozlesmeKaydi sozlesme in durum.Sozlesmeler.Where(s => s.Aktif))
        {
            if (_tick > sozlesme.BitisTicki)
            {
                sozlesme.Aktif = false;
                continue;
            }

            bool bagli = sirket.BagliMi ||
                (Seviye(durum, "yedek") > 0 &&
                 _rastgele.NextDouble() < Math.Min(0.85, Seviye(durum, "yedek") * 0.17));
            bool tamam = bagli &&
                sirket.KodKalitesiPuani >= sozlesme.AsgariKalite &&
                sirket.PerformansPuani >= sozlesme.AsgariPerformans &&
                sirket.GuvenlikPuani >= sozlesme.AsgariGuvenlik &&
                EtkinKapasite(sirket, durum) >= sozlesme.GerekliKapasite;

            if (tamam)
            {
                sirket.Kasa += sozlesme.TickOdemesi;
                sirket.ToplamGelir += sozlesme.TickOdemesi;
                sozlesme.BasariliTickSayisi++;
            }
            else
            {
                decimal ceza = Math.Min(sirket.Kasa, sozlesme.IhlalCezasi);
                sirket.Kasa -= ceza;
                durum.ToplamIsletmeGideri += ceza;
                sozlesme.IhlalSayisi++;
                sirket.ItibarPuani = Math.Max(0, sirket.ItibarPuani - 0.35);
                sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - 0.55);
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

            foreach (string kimlik in protokol.BenimseyenSirketler)
            {
                SirketKaydi? kullanan = SirketBul(kimlik);
                if (kullanan is null) continue;
                SirketIsletimDurumu durum = Durum(kimlik);

                if (kullanan.Kasa >= protokol.TickLisansBedeli)
                {
                    kullanan.Kasa -= protokol.TickLisansBedeli;
                    durum.ToplamIsletmeGideri += protokol.TickLisansBedeli;
                    sahip.Kasa += protokol.TickLisansBedeli;
                    sahip.ToplamGelir += protokol.TickLisansBedeli;
                    protokol.ToplamLisansGeliri += protokol.TickLisansBedeli;
                }
                else
                {
                    durum.OdenemeyenGider += protokol.TickLisansBedeli;
                }
            }
        }
    }

    private void TeklifOlustur()
    {
        _veri.SozlesmeTeklifleri.RemoveAll(t =>
            !string.IsNullOrWhiteSpace(t.KabulEdenSirketKimligi) ||
            t.SonKabulTicki < _tick - 20);

        if (_tick % 12 != 0 ||
            _veri.SozlesmeTeklifleri.Count(t => t.Aktif && t.SonKabulTicki >= _tick) >= 6)
            return;

        string[] kategoriler =
        [
            "kamu-verisi", "kurumsal-eposta", "sosyal-platform",
            "bulut-altyapisi", "analitik", "guvenlik"
        ];
        string kategori = kategoriler[_rastgele.Next(kategoriler.Length)];
        int zorluk = _rastgele.Next(1, 6);

        _veri.SozlesmeTeklifleri.Add(new SozlesmeTeklifi
        {
            TeklifKimligi = $"teklif-{Guid.NewGuid():N}",
            Baslik = $"{kategori.Replace('-', ' ')} sözleşmesi",
            Kategori = kategori,
            SureTick = 20 + zorluk * 8,
            TickOdemesi = 250 + zorluk * 225,
            IhlalCezasi = 500 + zorluk * 450,
            AsgariKalite = 45 + zorluk * 5,
            AsgariPerformans = 42 + zorluk * 5,
            AsgariGuvenlik = 40 + zorluk * 7,
            GerekliKapasite = 1 + zorluk,
            SonKabulTicki = _tick + 15
        });
    }

    private void OlayOlustur()
    {
        _veri.PiyasaOlaylari.RemoveAll(o => o.BitisTicki < _tick - 60);
        if (_tick % 30 != 0) return;

        (string Baslik, string Aciklama, string Kategori, double Talep, double Gider)[] olaylar =
        [
            ("Dijital dönüşüm dalgası", "Yazılım talebi yükseldi.", "tum", 1.35, 1.05),
            ("Siber güvenlik paniği", "Güvenlik ürünleri öne çıktı.", "guvenlik", 1.85, 1.10),
            ("Bulut maliyeti artışı", "Depolama giderleri yükseldi.", "bulut-depolama", 1.10, 1.35),
            ("Sosyal medya patlaması", "Sosyal ürünlere kullanıcı akışı başladı.", "sosyal-medya", 1.90, 1.15),
            ("Ekonomik durgunluk", "Yeni müşteri talebi zayıfladı.", "tum", 0.70, 1.05),
            ("E-posta geçiş sezonu", "İşletmeler yeni sağlayıcı arıyor.", "eposta", 1.75, 1.00)
        ];
        var olay = olaylar[_rastgele.Next(olaylar.Length)];

        _veri.PiyasaOlaylari.Add(new PiyasaOlayi
        {
            OlayKimligi = $"olay-{Guid.NewGuid():N}",
            Baslik = olay.Baslik,
            Aciklama = olay.Aciklama,
            EtkilenenKategori = olay.Kategori,
            TalepCarpani = olay.Talep,
            GiderCarpani = olay.Gider,
            BaslangicTicki = _tick,
            BitisTicki = _tick + 20
        });

        KonsolKayitcisi.Uyari($"PİYASA OLAYI | {olay.Baslik} | {olay.Aciklama}");
    }

    private IEnumerable<PiyasaOlayi> AktifOlaylar() =>
        _veri.PiyasaOlaylari.Where(o => o.BaslangicTicki <= _tick && o.BitisTicki >= _tick);

    private double TalepCarpani(string kategori) =>
        AktifOlaylar()
            .Where(o =>
                string.Equals(o.EtkilenenKategori, "tum", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(o.EtkilenenKategori, kategori, StringComparison.OrdinalIgnoreCase))
            .Select(o => o.TalepCarpani)
            .DefaultIfEmpty(1)
            .Aggregate(1d, (a, b) => a * b);

    private decimal Gelir(UrunKaydi urun, int yeni) =>
        decimal.Round(Math.Max(0, urun.FiyatlandirmaModeli switch
        {
            "abonelik" => urun.AktifKullaniciSayisi * urun.AbonelikUcreti / 30m,
            "kullanim" => urun.AktifKullaniciSayisi * urun.KullanimBasinaUcret *
                         (decimal)(0.75 + _rastgele.NextDouble() * 2.5),
            "freemium" => urun.AktifKullaniciSayisi * urun.AbonelikUcreti / 90m +
                          urun.AktifKullaniciSayisi * urun.KullanimBasinaUcret * 0.25m,
            "lisans" => yeni * urun.AbonelikUcreti,
            _ => 0
        }), 2);

    private static int TabanTalep(string kategori) => kategori switch
    {
        "sosyal-medya" => 11, "mesajlasma" => 9, "eposta" => 7,
        "bulut-depolama" => 6, "e-ticaret" => 6, "api" => 5,
        "analitik" => 5, "guvenlik" => 4, _ => 3
    };

    private static decimal KullaniciGideri(string kategori) => kategori switch
    {
        "bulut-depolama" => 0.18m, "sosyal-medya" => 0.12m,
        "mesajlasma" => 0.09m, "eposta" => 0.07m, "guvenlik" => 0.10m,
        _ => 0.06m
    };

    private static double FiyatCarpani(UrunKaydi urun)
    {
        decimal referans = urun.Kategori switch
        {
            "sosyal-medya" => 25, "mesajlasma" => 20, "eposta" => 35,
            "bulut-depolama" => 60, "guvenlik" => 90, "analitik" => 75,
            "api" => 50, _ => 45
        };
        decimal fiyat = urun.FiyatlandirmaModeli == "kullanim"
            ? Math.Max(0.01m, urun.KullanimBasinaUcret * 100)
            : Math.Max(1, urun.AbonelikUcreti);
        return Math.Clamp((double)(referans / fiyat), 0.25, 2.25);
    }

    private static void Ode(
        SirketKaydi sirket,
        SirketIsletimDurumu durum,
        decimal tutar)
    {
        if (tutar <= 0) return;
        decimal odenen = Math.Min(sirket.Kasa, tutar);
        sirket.Kasa -= odenen;
        durum.ToplamIsletmeGideri += odenen;
        durum.OdenemeyenGider += tutar - odenen;
        if (tutar > odenen)
        {
            durum.KrediNotu = Math.Clamp(durum.KrediNotu - 4, 300, 900);
            sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - 0.08);
        }
    }

    private static void Degerle(
        SirketKaydi sirket,
        SirketIsletimDurumu durum)
    {
        decimal borc = durum.Krediler.Where(k => k.Aktif).Sum(k => k.KalanBorc);
        decimal duzenli = durum.Urunler.Where(u => u.Aktif)
            .Sum(u => u.AktifKullaniciSayisi * u.AbonelikUcreti / 30m);
        decimal puan = (decimal)(
            sirket.KodKalitesiPuani + sirket.PerformansPuani +
            sirket.GuvenlikPuani + sirket.ItibarPuani +
            durum.EkosistemPuani) * 125m;

        durum.SirketDegeri = decimal.Round(Math.Max(0,
            sirket.Kasa + duzenli * 40 +
            durum.ToplamAboneSayisi * 15m +
            durum.Urunler.Count(u => u.Aktif) * 2_500m +
            durum.Sozlesmeler.Count(s => s.Aktif) * 5_000m +
            puan - borc - durum.OdenemeyenGider), 2);
        durum.TahminiHisseFiyati = decimal.Round(durum.SirketDegeri / 10_000m, 4);
    }

    private static int KapasiteBonusu(SirketIsletimDurumu durum) =>
        Seviye(durum, "cpu") / 2 +
        Seviye(durum, "ram") / 3 +
        Seviye(durum, "ag") / 3 +
        Seviye(durum, "yedek") / 2;

    private static int EtkinKapasite(SirketKaydi sirket, SirketIsletimDurumu durum) =>
        (sirket.Hizmetler.Count == 0 ? 0 : sirket.Hizmetler.Max(h => h.AzamiEszamanliIs)) +
        KapasiteBonusu(durum);

    private static int Seviye(SirketIsletimDurumu durum, string tur) =>
        durum.YatirimSeviyeleri.TryGetValue(tur, out int seviye) ? Math.Max(0, seviye) : 0;

    private static decimal Maliyet(YatirimPaketi paket, int seviye) =>
        decimal.Round(paket.TabanMaliyet * (seviye + 1) * (seviye + 1), 2);

    private static string Anahtar(SunulanHizmet hizmet) =>
        $"{hizmet.HizmetKimligi.Trim()}@{hizmet.HizmetSurumu.Trim()}";

    private SirketIsletimDurumu Durum(string sirketKimligi)
    {
        SirketIsletimDurumu? durum = _veri.Sirketler.FirstOrDefault(
            s => string.Equals(s.SirketKimligi, sirketKimligi, StringComparison.OrdinalIgnoreCase));
        if (durum is not null) return durum;
        durum = new SirketIsletimDurumu { SirketKimligi = sirketKimligi };
        _veri.Sirketler.Add(durum);
        return durum;
    }

    private SirketKaydi SirketZorunlu(string sirketKimligi) =>
        SirketBul(sirketKimligi) ??
        throw new InvalidOperationException("Şirket bulunamadı.");

    private SirketKaydi? SirketBul(string sirketKimligi) =>
        _sirketler.SirketKayitlari.FirstOrDefault(
            s => string.Equals(s.SirketKimligi, sirketKimligi, StringComparison.OrdinalIgnoreCase));

    private void IslemEkle(
        SirketIsletimDurumu durum,
        string tur,
        string aciklama,
        decimal tutar)
    {
        durum.SonIslemler.Add(new IsletimIslemKaydi
        {
            IslemKimligi = $"islem-{Guid.NewGuid():N}",
            TickNumarasi = _tick,
            IslemTuru = tur,
            Aciklama = aciklama,
            Tutar = tutar
        });

        if (durum.SonIslemler.Count > 200)
            durum.SonIslemler = durum.SonIslemler.TakeLast(200).ToList();
    }

    private async Task TumunuKaydetAsync(CancellationToken cancellationToken)
    {
        await KaydetKilitsizAsync(cancellationToken);
        await _sirketler.BilancolariKaydetAsync(cancellationToken);
    }

    private async Task KaydetKilitsizAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        _veri.GuncellenmeZamani = DateTimeOffset.UtcNow;
        string gecici = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(
            gecici,
            JsonSerializer.Serialize(_veri, JsonAyarlari),
            new UTF8Encoding(false),
            cancellationToken);
        File.Move(gecici, _dosyaYolu, overwrite: true);
    }

    private static (string Kullanici, string Parola) VarsayilanGiris(SirketKaydi sirket)
    {
        string kimlik = sirket.SirketKimligi.ToLowerInvariant();
        if (kimlik.Contains("tunix")) return ("tunix", "tunix123");
        if (kimlik.Contains("mudaf")) return ("mudaf", "mudaf123");
        if (kimlik.Contains("ugax")) return ("ugax", "ugax123");

        string kullanici = new(
            sirket.SirketAdi.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(kullanici)) kullanici = "sirket";
        return (kullanici, $"{kullanici}123");
    }

    private static string Ozetle(string tuz, string parola) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{tuz}:{parola}")));

    private static bool SabitZamanliEsit(string sol, string sag)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(sol),
                Convert.FromHexString(sag));
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try
        {
            if (_baslatildi)
            {
                await _kilit.WaitAsync();
                try { await TumunuKaydetAsync(CancellationToken.None); }
                finally { _kilit.Release(); }
            }
        }
        catch (Exception exception)
        {
            KonsolKayitcisi.Hata(
                $"İşletim verileri kaydedilemedi: {exception.Message}");
        }
        finally
        {
            _disposed = true;
            _kilit.Dispose();
        }
    }
}
