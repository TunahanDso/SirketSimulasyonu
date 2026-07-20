using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

/// <summary>
/// V9.1 kalıcı işletim deposu. Panel okumak veya kayıt almak ekonomi, kapasite,
/// kredi faizi, kullanıcı ya da puan üretmez. Bütün tick ekonomisi yalnız
/// V9EkonomiYoneticisi ve V9IsletimKoordinatoru tarafından yürütülür.
/// </summary>
public sealed class SirketIsletimYoneticisi : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SirketYoneticisi _sirketler;
    private readonly string _dosyaYolu;
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private SirketIsletimDosyasi _veri = new();
    private long _tick;
    private bool _baslatildi;
    private bool _disposed;

    public SirketIsletimYoneticisi(
        SirketYoneticisi sirketler,
        string motorVerileriKlasoru)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);
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
            NormalizeEt();
            _tick = Math.Max(0, _veri.SonIslenenTick);
            await KaydetKilitsizAsync(cancellationToken);
            _baslatildi = true;
            KonsolKayitcisi.Basari(
                "V9.1 yan etkisiz işletim deposu hazır | Panel okuma ekonomi veya kapasiteyi değiştirmez.");
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> GirisDogrulaAsync(
        GirisIstegi istek,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            string kullanici = istek.KullaniciAdi?.Trim() ?? string.Empty;
            SirketHesabi? hesap = _veri.Hesaplar.FirstOrDefault(x =>
                x.KullaniciAdi.Equals(kullanici, StringComparison.OrdinalIgnoreCase));
            if (hesap is null || !SabitZamanliEsit(hesap.ParolaOzeti, Ozetle(hesap.ParolaTuzu, istek.Parola ?? string.Empty)))
                return IslemSonucu.Hata("Kullanıcı adı veya parola yanlış.");
            hesap.SonGirisZamani = DateTimeOffset.UtcNow;
            await KaydetKilitsizAsync(cancellationToken);
            return IslemSonucu.Basari("Giriş başarılı.", new
            {
                hesap.SirketKimligi,
                hesap.KullaniciAdi,
                hesap.ParolaDegistirilmeli
            });
        }
        finally { _kilit.Release(); }
    }

    public Task<IslemSonucu> ParolaDegistirAsync(
        string sirketKimligi,
        ParolaDegistirIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (_, _) =>
        {
            SirketHesabi? hesap = _veri.Hesaplar.FirstOrDefault(x =>
                x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
            if (hesap is null) return IslemSonucu.Hata("Yönetim hesabı bulunamadı.");
            if (!SabitZamanliEsit(hesap.ParolaOzeti, Ozetle(hesap.ParolaTuzu, istek.EskiParola ?? string.Empty)))
                return IslemSonucu.Hata("Mevcut parola yanlış.");
            string yeni = istek.YeniParola ?? string.Empty;
            if (yeni.Length < 8 || yeni.Length > 128)
                return IslemSonucu.Hata("Yeni parola 8-128 karakter arasında olmalıdır.");
            string tuz = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            hesap.ParolaTuzu = tuz;
            hesap.ParolaOzeti = Ozetle(tuz, yeni);
            hesap.ParolaDegistirilmeli = false;
            return IslemSonucu.Basari("Parola kalıcı olarak değiştirildi.");
        });

    public Task<IslemSonucu> YatirimSatinAlAsync(string _, YatirimIstegi __, CancellationToken ___) =>
        Task.FromResult(IslemSonucu.Hata("Yatırımlar V9.1 fiziksel kapasite yöneticisinden yapılır."));

    public Task<IslemSonucu> KrediCekAsync(string _, KrediIstegi __, CancellationToken ___) =>
        Task.FromResult(IslemSonucu.Hata("Krediler V9.1 sabit faizli banka yöneticisinden kullanılır."));

    public Task<IslemSonucu> HizmetFiyatiGuncelleAsync(string _, FiyatGuncelleIstegi __, CancellationToken ___) =>
        Task.FromResult(IslemSonucu.Hata("Hizmet fiyatları motor tarafından sabittir."));

    public Task<IslemSonucu> HizmetKapasitesiArtirAsync(string _, HizmetKapasiteIstegi __, CancellationToken ___) =>
        Task.FromResult(IslemSonucu.Hata("Hizmet kapasitesi satın alınamaz; fiziksel havuzdan tahsis edilir."));

    public Task<IslemSonucu> UrunKapasitesiArtirAsync(string _, UrunKapasiteIstegi __, CancellationToken ___) =>
        Task.FromResult(IslemSonucu.Hata("Ürün kapasitesi satın alınamaz; fiziksel havuzdan tahsis edilir."));

    public Task<IslemSonucu> HizmetYayinDurumuGuncelleAsync(
        string sirketKimligi,
        HizmetYayinDurumuIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            SunulanHizmet? hizmet = sirket.Hizmetler.FirstOrDefault(x =>
                x.HizmetKimligi.Equals(istek.HizmetKimligi, StringComparison.OrdinalIgnoreCase) &&
                x.HizmetSurumu.Equals(istek.HizmetSurumu, StringComparison.OrdinalIgnoreCase));
            if (hizmet is null) return IslemSonucu.Hata("Hizmet şirket manifestinde bulunamadı.");
            string anahtar = Anahtar(hizmet.HizmetKimligi, hizmet.HizmetSurumu);
            HizmetKaliciAyari ayar = HizmetAyari(durum, hizmet);
            ayar.YonetilenAktiflik = istek.Aktif;
            ayar.AktiflikYonetildi = true;
            hizmet.Aktif = istek.Aktif;
            durum.HizmetBazKapasiteleri[anahtar] = Math.Max(0, hizmet.AzamiEszamanliIs);
            IslemEkle(durum, "hizmet-durum", $"{hizmet.HizmetKimligi} {(istek.Aktif ? "aktif" : "pasif")} oldu.", 0);
            return IslemSonucu.Basari("Hizmet yayın durumu kaydedildi.");
        });

    public Task<IslemSonucu> UrunOlusturAsync(
        string sirketKimligi,
        UrunOlusturIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (_, durum) =>
        {
            string uygulama = istek.UygulamaKimligi?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(uygulama)) return IslemSonucu.Hata("Uygulama kimliği boş olamaz.");
            if (durum.Urunler.Any(x => x.UygulamaKimligi.Equals(uygulama, StringComparison.OrdinalIgnoreCase)))
                return IslemSonucu.Hata("Bu uygulama zaten piyasada.");
            string tur = Normal(istek.UrunTuru, "uygulama");
            string kategori = Normal(istek.Kategori, "diger");
            string model = Normal(istek.FiyatlandirmaModeli, "abonelik");
            decimal gelenFiyat = model == "kullanim" ? istek.KullanimBasinaUcret : istek.AbonelikUcreti;
            decimal fiyat = V9FiyatPolitikasi.Sinirla(tur, kategori, gelenFiyat);
            UrunKaydi urun = new()
            {
                UrunKimligi = $"urun-{Guid.NewGuid():N}",
                UygulamaKimligi = uygulama,
                UrunAdi = string.IsNullOrWhiteSpace(istek.UrunAdi) ? uygulama : istek.UrunAdi.Trim(),
                Kategori = kategori,
                UrunTuru = tur,
                FiyatlandirmaModeli = model,
                AbonelikUcreti = model == "kullanim" ? 0 : fiyat,
                KullanimBasinaUcret = model == "kullanim" ? fiyat : model == "freemium" ? Math.Min(fiyat / 20m, 10m) : 0,
                TabanKullaniciKapasitesi = 0,
                SatinAlinanKullaniciKapasitesi = 0,
                AltyapiKapasiteBonusu = 0,
                KullaniciKapasitesi = 0,
                AktifKullaniciSayisi = 0,
                UrunMemnuniyeti = 52,
                UrunKalitesi = 50,
                ArkaUcHizmetKimligi = istek.ArkaUcHizmetKimligi?.Trim() ?? string.Empty,
                ArkaUcHizmetSurumu = string.IsNullOrWhiteSpace(istek.ArkaUcHizmetSurumu) ? "1.0" : istek.ArkaUcHizmetSurumu.Trim(),
                ProtokolKimligi = istek.ProtokolKimligi?.Trim() ?? string.Empty,
                DesteklenenPlatformlar = (istek.DesteklenenPlatformlar ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList(),
                GerekliPlatformlar = (istek.GerekliPlatformlar ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList(),
                Bagimliliklar = (istek.Bagimliliklar ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToList(),
                Aktif = true,
                YayinTicki = _tick
            };
            durum.Urunler.Add(urun);
            IslemEkle(durum, "urun-yayin", $"{urun.UrunAdi} kalıcı ürün kaydına eklendi.", 0);
            return IslemSonucu.Basari("Ürün kalıcı kayda eklendi.", urun);
        });

    public Task<IslemSonucu> UrunGuncelleAsync(
        string sirketKimligi,
        UrunGuncelleIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (_, durum) =>
        {
            UrunKaydi? urun = durum.Urunler.FirstOrDefault(x =>
                x.UrunKimligi.Equals(istek.UrunKimligi, StringComparison.OrdinalIgnoreCase));
            if (urun is null) return IslemSonucu.Hata("Ürün bulunamadı.");
            decimal gelen = urun.FiyatlandirmaModeli == "kullanim" ? istek.KullanimBasinaUcret : istek.AbonelikUcreti;
            decimal fiyat = V9FiyatPolitikasi.Sinirla(urun.UrunTuru, urun.Kategori, gelen);
            if (urun.FiyatlandirmaModeli == "kullanim")
            {
                urun.AbonelikUcreti = 0;
                urun.KullanimBasinaUcret = fiyat;
            }
            else
            {
                urun.AbonelikUcreti = fiyat;
                if (urun.FiyatlandirmaModeli != "freemium") urun.KullanimBasinaUcret = 0;
            }
            urun.Aktif = istek.Aktif;
            IslemEkle(durum, "urun-guncelle", $"{urun.UrunAdi} fiyat ve aktiflik ayarı kaydedildi.", 0);
            return IslemSonucu.Basari("Ürün ayarları kaydedildi.", urun);
        });

    public Task<IslemSonucu> ProtokolOlusturAsync(
        string sirketKimligi,
        ProtokolOlusturIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            string ad = istek.ProtokolAdi?.Trim() ?? string.Empty;
            string surum = string.IsNullOrWhiteSpace(istek.Surum) ? "1.0" : istek.Surum.Trim();
            if (ad.Length is < 2 or > 100) return IslemSonucu.Hata("Protokol adı geçersiz.");
            if (_veri.Protokoller.Any(x =>
                x.SahipSirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase) &&
                x.ProtokolAdi.Equals(ad, StringComparison.OrdinalIgnoreCase) &&
                x.Surum.Equals(surum, StringComparison.OrdinalIgnoreCase)))
                return IslemSonucu.Hata("Bu protokol sürümü zaten piyasada.");
            decimal maliyet = 500m;
            if (sirket.Kasa < maliyet) return IslemSonucu.Hata($"Protokol yayın maliyeti {maliyet:N2} TL.");
            sirket.Kasa -= maliyet;
            durum.ToplamYatirimHarcamasi += maliyet;
            OzelProtokolKaydi protokol = new()
            {
                ProtokolKimligi = $"protokol-{Guid.NewGuid():N}",
                SahipSirketKimligi = sirketKimligi,
                ProtokolAdi = ad,
                Surum = surum,
                Aciklama = istek.Aciklama?.Trim() ?? string.Empty,
                LisansModeli = Normal(istek.LisansModeli, "acik"),
                BenimsemeBedeli = Math.Clamp(istek.BenimsemeBedeli, 0, 25_000m),
                TickLisansBedeli = Math.Clamp(istek.TickLisansBedeli, 0, 100m),
                YayinTicki = _tick,
                Aktif = true
            };
            _veri.Protokoller.Add(protokol);
            IslemEkle(durum, "protokol-yayin", $"{ad} v{surum} piyasaya açıldı.", -maliyet);
            return IslemSonucu.Basari("Protokol piyasaya açıldı.", protokol);
        });

    public Task<IslemSonucu> ProtokolBenimseAsync(
        string sirketKimligi,
        ProtokolBenimseIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            OzelProtokolKaydi? protokol = _veri.Protokoller.FirstOrDefault(x =>
                x.Aktif && x.ProtokolKimligi.Equals(istek.ProtokolKimligi, StringComparison.OrdinalIgnoreCase));
            if (protokol is null) return IslemSonucu.Hata("Protokol bulunamadı.");
            if (protokol.SahipSirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase))
                return IslemSonucu.Basari("Kendi protokolünüz zaten kullanılabilir.");
            if (protokol.BenimseyenSirketler.Contains(sirketKimligi, StringComparer.OrdinalIgnoreCase))
                return IslemSonucu.Basari("Protokol zaten benimsenmiş.");
            decimal bedel = Math.Clamp(protokol.BenimsemeBedeli, 0, 25_000m);
            if (sirket.Kasa < bedel) return IslemSonucu.Hata("Benimseme bedeli için kasa yetersiz.");
            sirket.Kasa -= bedel;
            durum.ToplamIsletmeGideri += bedel;
            protokol.BenimseyenSirketler.Add(sirketKimligi);
            durum.BenimsenenProtokoller.Add(protokol.ProtokolKimligi);
            SirketKaydi? sahip = SirketBul(protokol.SahipSirketKimligi);
            if (sahip is not null)
            {
                sahip.Kasa += bedel;
                sahip.ToplamGelir += bedel;
            }
            IslemEkle(durum, "protokol-benimse", $"{protokol.ProtokolAdi} benimsendi.", -bedel);
            return IslemSonucu.Basari("Protokol benimsendi.");
        });

    public Task<IslemSonucu> SozlesmeKabulEtAsync(
        string sirketKimligi,
        SozlesmeKabulIstegi istek,
        CancellationToken cancellationToken) =>
        DegistirAsync(sirketKimligi, cancellationToken, (sirket, durum) =>
        {
            SozlesmeTeklifi? teklif = _veri.SozlesmeTeklifleri.FirstOrDefault(x =>
                x.Aktif && string.IsNullOrWhiteSpace(x.KabulEdenSirketKimligi) &&
                x.TeklifKimligi.Equals(istek.TeklifKimligi, StringComparison.OrdinalIgnoreCase) &&
                x.SonKabulTicki >= _tick);
            if (teklif is null) return IslemSonucu.Hata("Teklif artık açık değil.");
            if (!V9IsletimKoordinatoru.SozlesmeyeUygunMu(sirket, teklif, out List<string> eksikler))
                return IslemSonucu.Hata("SLA şartları karşılanmıyor: " + string.Join(", ", eksikler));
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
                GerekliKapasite = teklif.GerekliKapasite,
                Aktif = true
            });
            IslemEkle(durum, "sla-kabul", $"{teklif.Baslik} kabul edildi.", 0);
            return IslemSonucu.Basari("SLA sözleşmesi kabul edildi.");
        });

    public async Task<string> PanelJsonuOlusturAsync(
        string sirketKimligi,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            SirketIsletimDurumu durum = Durum(sirketKimligi);
            HizmetKayitlariniTamamla(sirket, durum);
            SirketHesabi? hesap = _veri.Hesaplar.FirstOrDefault(x =>
                x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
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
                    durum.OperasyonRiski,
                    durum.TeknikBorc,
                    durum.BakimBaskisi,
                    durum.ToplamAboneSayisi,
                    durum.ToplamAbonelikGeliri,
                    durum.ToplamUrunGeliri,
                    durum.ToplamIsletmeGideri,
                    durum.ToplamFinansmanGideri,
                    durum.ToplamYatirimHarcamasi,
                    durum.OdenemeyenGider,
                    durum.SirketDegeri,
                    durum.TahminiHisseFiyati,
                    toplamBorc = durum.Krediler.Where(x => x.Aktif).Sum(x => x.KalanBorc),
                    durum.TemerrutSayisi,
                    durum.YatirimSeviyeleri,
                    durum.HizmetAyarlari,
                    durum.Krediler,
                    durum.Urunler,
                    durum.Sozlesmeler,
                    sonOlaylar = durum.SonOlaylar.OrderByDescending(x => x.TickNumarasi).Take(30),
                    sonIslemler = durum.SonIslemler.OrderByDescending(x => x.Zaman).Take(60)
                },
                protokoller = _veri.Protokoller.Where(x => x.Aktif),
                sozlesmeTeklifleri = _veri.SozlesmeTeklifleri.Where(x =>
                    x.Aktif && string.IsNullOrWhiteSpace(x.KabulEdenSirketKimligi) && x.SonKabulTicki >= _tick),
                parolaDegistirilmeli = hesap?.ParolaDegistirilmeli ?? false,
                kaliciOtoriteKurali = "Panel salt okunurdur; ekonomi ve kapasite yalnız V9.1 yöneticilerinde değişir."
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
            _tick = Math.Max(_tick, tickNumarasi);
            _veri.SonIslenenTick = _tick;
            await TumunuKaydetAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    private async Task<IslemSonucu> DegistirAsync(
        string sirketKimligi,
        CancellationToken cancellationToken,
        Func<SirketKaydi, SirketIsletimDurumu, IslemSonucu> islem)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            SirketIsletimDurumu durum = Durum(sirketKimligi);
            HizmetKayitlariniTamamla(sirket, durum);
            IslemSonucu sonuc = islem(sirket, durum);
            if (sonuc.Basarili) await TumunuKaydetAsync(cancellationToken);
            return sonuc;
        }
        finally { _kilit.Release(); }
    }

    private void NormalizeEt()
    {
        _veri.Surum = Math.Max(10, _veri.Surum);
        _veri.Hesaplar ??= [];
        _veri.Sirketler ??= [];
        _veri.Protokoller ??= [];
        _veri.SozlesmeTeklifleri ??= [];
        _veri.PiyasaOlaylari ??= [];
        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
        {
            SirketIsletimDurumu durum = Durum(sirket.SirketKimligi);
            NormalizeDurum(durum);
            HizmetKayitlariniTamamla(sirket, durum);
            if (_veri.Hesaplar.Any(x => x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase)))
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

    private static void NormalizeDurum(SirketIsletimDurumu durum)
    {
        durum.YatirimSeviyeleri ??= new(StringComparer.OrdinalIgnoreCase);
        durum.HizmetFiyatEzmeDegerleri ??= new(StringComparer.OrdinalIgnoreCase);
        durum.HizmetBazKapasiteleri ??= new(StringComparer.OrdinalIgnoreCase);
        durum.HizmetAyarlari ??= new(StringComparer.OrdinalIgnoreCase);
        durum.Krediler ??= [];
        durum.Urunler ??= [];
        durum.BenimsenenProtokoller ??= [];
        durum.Sozlesmeler ??= [];
        durum.SonIslemler ??= [];
        durum.SonOlaylar ??= [];
        foreach (UrunKaydi urun in durum.Urunler)
        {
            urun.DesteklenenPlatformlar ??= [];
            urun.GerekliPlatformlar ??= [];
            urun.Bagimliliklar ??= [];
        }
    }

    private void HizmetKayitlariniTamamla(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        NormalizeDurum(durum);
        foreach (SunulanHizmet hizmet in sirket.Hizmetler)
        {
            string anahtar = Anahtar(hizmet.HizmetKimligi, hizmet.HizmetSurumu);
            HizmetKaliciAyari ayar = HizmetAyari(durum, hizmet);
            hizmet.BirimFiyat = MotorHizmetFiyatlari.Fiyat(hizmet.HizmetKimligi, hizmet.HizmetSurumu);
            ayar.SunucudanGelenIlkFiyat = hizmet.BirimFiyat;
            ayar.YonetilenFiyat = hizmet.BirimFiyat;
            ayar.FiyatYonetildi = false;
            ayar.SonGorulmeTicki = _tick;
            if (ayar.AktiflikYonetildi) hizmet.Aktif = ayar.YonetilenAktiflik;
            durum.HizmetFiyatEzmeDegerleri[anahtar] = hizmet.BirimFiyat;
            durum.HizmetBazKapasiteleri[anahtar] = Math.Max(0, hizmet.AzamiEszamanliIs);
        }
    }

    private static HizmetKaliciAyari HizmetAyari(SirketIsletimDurumu durum, SunulanHizmet hizmet)
    {
        string anahtar = Anahtar(hizmet.HizmetKimligi, hizmet.HizmetSurumu);
        if (durum.HizmetAyarlari.TryGetValue(anahtar, out HizmetKaliciAyari? ayar)) return ayar;
        ayar = new HizmetKaliciAyari
        {
            HizmetKimligi = hizmet.HizmetKimligi,
            HizmetSurumu = hizmet.HizmetSurumu,
            SunucudanGelenIlkFiyat = MotorHizmetFiyatlari.Fiyat(hizmet.HizmetKimligi, hizmet.HizmetSurumu),
            YonetilenFiyat = MotorHizmetFiyatlari.Fiyat(hizmet.HizmetKimligi, hizmet.HizmetSurumu),
            SunucudanGelenIlkKapasite = Math.Max(0, hizmet.AzamiEszamanliIs),
            SunucudanGelenIlkAktiflik = hizmet.Aktif,
            YonetilenAktiflik = hizmet.Aktif,
            IlkGorulmeTicki = 0,
            SonGorulmeTicki = 0
        };
        durum.HizmetAyarlari[anahtar] = ayar;
        return ayar;
    }

    private SirketIsletimDurumu Durum(string sirketKimligi)
    {
        SirketIsletimDurumu? durum = _veri.Sirketler.FirstOrDefault(x =>
            x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
        if (durum is not null) return durum;
        durum = new SirketIsletimDurumu { SirketKimligi = sirketKimligi };
        _veri.Sirketler.Add(durum);
        return durum;
    }

    private SirketKaydi SirketZorunlu(string sirketKimligi) =>
        SirketBul(sirketKimligi) ?? throw new InvalidOperationException("Şirket bulunamadı.");

    private SirketKaydi? SirketBul(string sirketKimligi) =>
        _sirketler.SirketKayitlari.FirstOrDefault(x =>
            x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));

    private static string Anahtar(string kimlik, string surum) =>
        $"{kimlik.Trim()}@{surum.Trim()}";

    private void IslemEkle(SirketIsletimDurumu durum, string tur, string aciklama, decimal tutar)
    {
        durum.SonIslemler.Add(new IsletimIslemKaydi
        {
            IslemKimligi = $"v9-islem-{Guid.NewGuid():N}",
            TickNumarasi = _tick,
            IslemTuru = tur,
            Aciklama = aciklama,
            Tutar = tutar
        });
        if (durum.SonIslemler.Count > 200) durum.SonIslemler = durum.SonIslemler.TakeLast(200).ToList();
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
        string tmp = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(
            tmp,
            JsonSerializer.Serialize(_veri, JsonAyarlari),
            new UTF8Encoding(false),
            cancellationToken);
        File.Move(tmp, _dosyaYolu, overwrite: true);
    }

    private static (string Kullanici, string Parola) VarsayilanGiris(SirketKaydi sirket)
    {
        string kimlik = sirket.SirketKimligi.ToLowerInvariant();
        if (kimlik.Contains("tunix")) return ("tunix", "tunix123");
        if (kimlik.Contains("mudaf")) return ("mudaf", "mudaf123");
        if (kimlik.Contains("ugax")) return ("ugax", "ugax123");
        if (kimlik.Contains("ilos")) return ("ilos", "ilos123");
        string ad = new(sirket.SirketAdi.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(ad)) ad = "sirket";
        return (ad, $"{ad}123");
    }

    private static string Normal(string? deger, string varsayilan) =>
        string.IsNullOrWhiteSpace(deger) ? varsayilan : deger.Trim().ToLowerInvariant();

    private static string Ozetle(string tuz, string parola) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{tuz}:{parola}")));

    private static bool SabitZamanliEsit(string a, string b)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(a), Convert.FromHexString(b)); }
        catch { return false; }
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
        catch (Exception hata)
        {
            KonsolKayitcisi.Hata($"V9.1 işletim verileri kaydedilemedi: {hata.Message}");
        }
        finally
        {
            _disposed = true;
            _kilit.Dispose();
        }
    }
}
