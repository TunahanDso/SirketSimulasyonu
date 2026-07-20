using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SirketMotoru.Hizmetler;
using SirketMotoru.Kayit;
using SirketMotoru.Protokol;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class KodTabanliSirketIsletimYoneticisi : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SirketIsletimYoneticisi _temel;
    private readonly SirketYoneticisi _sirketler;
    private readonly string _dosyaYolu;
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private KodTabanliYayinDosyasi _yayinlar = new();
    private bool _baslatildi;
    private bool _disposed;

    public KodTabanliSirketIsletimYoneticisi(SirketYoneticisi sirketler, string motorVerileriKlasoru)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _temel = new SirketIsletimYoneticisi(sirketler, motorVerileriKlasoru);
        _dosyaYolu = Path.Combine(Path.GetFullPath(motorVerileriKlasoru), "kod-tabanli-yayinlar.json");
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        await _temel.BaslatAsync(cancellationToken);
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            if (_baslatildi) return;
            if (File.Exists(_dosyaYolu))
            {
                string json = await File.ReadAllTextAsync(_dosyaYolu, cancellationToken);
                _yayinlar = JsonSerializer.Deserialize<KodTabanliYayinDosyasi>(json, JsonAyarlari) ?? new();
            }
            _yayinlar.Surum = Math.Max(2, _yayinlar.Surum);
            _yayinlar.Sirketler ??= new(StringComparer.OrdinalIgnoreCase);
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari) _ = Ayarlar(sirket.SirketKimligi);
            await KaydetKilitsizAsync(cancellationToken);
            _baslatildi = true;
            KonsolKayitcisi.Basari("Serbest kod tabanlı ürün ekosistemi hazır | Uygulama, platform ve işletim sistemi manifestten gelebilir.");
        }
        finally { _kilit.Release(); }
    }

    public Task<IslemSonucu> GirisDogrulaAsync(GirisIstegi istek, CancellationToken ct) => _temel.GirisDogrulaAsync(istek, ct);
    public Task<IslemSonucu> ParolaDegistirAsync(string sirket, ParolaDegistirIstegi istek, CancellationToken ct) => _temel.ParolaDegistirAsync(sirket, istek, ct);
    public Task<IslemSonucu> YatirimSatinAlAsync(string sirket, YatirimIstegi istek, CancellationToken ct) => _temel.YatirimSatinAlAsync(sirket, istek, ct);
    public Task<IslemSonucu> KrediCekAsync(string sirket, KrediIstegi istek, CancellationToken ct) => _temel.KrediCekAsync(sirket, istek, ct);
    public Task<IslemSonucu> HizmetFiyatiGuncelleAsync(string sirket, FiyatGuncelleIstegi istek, CancellationToken ct) => _temel.HizmetFiyatiGuncelleAsync(sirket, istek, ct);
    public Task<IslemSonucu> HizmetYayinDurumuGuncelleAsync(string sirket, HizmetYayinDurumuIstegi istek, CancellationToken ct) => _temel.HizmetYayinDurumuGuncelleAsync(sirket, istek, ct);
    public Task<IslemSonucu> HizmetKapasitesiArtirAsync(string sirket, HizmetKapasiteIstegi istek, CancellationToken ct) => _temel.HizmetKapasitesiArtirAsync(sirket, istek, ct);
    public Task<IslemSonucu> SozlesmeKabulEtAsync(string sirket, SozlesmeKabulIstegi istek, CancellationToken ct) => _temel.SozlesmeKabulEtAsync(sirket, istek, ct);

    public async Task<IslemSonucu> UygulamaYayinlaAsync(string sirketKimligi, UygulamaYayinlaIstegi istek, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            if (!sirket.BagliMi) return IslemSonucu.Hata("Ürün piyasaya açılırken şirket sunucusu bağlı olmalıdır.");
            SunulanUygulama? uygulama = SunucuYayinManifestDeposu.Getir(sirketKimligi).Uygulamalar.FirstOrDefault(u =>
                string.Equals(u.UygulamaKimligi, istek.UygulamaKimligi?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (uygulama is null) return IslemSonucu.Hata("Bu ürün şirket sunucusu tarafından koddan ilan edilmiyor.");

            UygulamaDogrulamaSonucu dogrulama = UygulamayiDogrula(sirket, uygulama);
            if (!dogrulama.Gecerli) return IslemSonucu.Hata("Teknik doğrulama başarısız: " + string.Join(" | ", dogrulama.Hatalar));
            SirketYayinAyarlari ayarlar = Ayarlar(sirketKimligi);
            if (ayarlar.UygulamaUrunEslemeleri.ContainsKey(uygulama.UygulamaKimligi)) return IslemSonucu.Hata("Bu kodlanmış ürün zaten piyasada.");
            UygulamaOzelligi? ana = AnaOzellik(uygulama);
            if (ana is null) return IslemSonucu.Hata("Ürünün çalıştırılabilir zorunlu hizmeti yok.");

            List<string> bagimliliklar = uygulama.Bagimliliklar.Select(b =>
                $"{b.SirketKimligi}/{b.UygulamaKimligi}@{b.AsgariSurum}|{b.BagimlilikTuru}|{(b.Zorunlu ? "zorunlu" : "opsiyonel")}").ToList();

            IslemSonucu sonuc = await _temel.UrunOlusturAsync(sirketKimligi, new UrunOlusturIstegi
            {
                UrunAdi = uygulama.UygulamaAdi,
                UygulamaKimligi = uygulama.UygulamaKimligi,
                Kategori = uygulama.Kategori,
                UrunTuru = uygulama.UrunTuru,
                FiyatlandirmaModeli = istek.FiyatlandirmaModeli,
                AbonelikUcreti = istek.AbonelikUcreti,
                KullanimBasinaUcret = istek.KullanimBasinaUcret,
                ArkaUcHizmetKimligi = ana.HizmetKimligi,
                ArkaUcHizmetSurumu = ana.HizmetSurumu,
                ProtokolKimligi = uygulama.DesteklenenProtokoller.FirstOrDefault() ?? string.Empty,
                DesteklenenPlatformlar = uygulama.DesteklenenPlatformlar,
                GerekliPlatformlar = uygulama.GerekliPlatformlar,
                Bagimliliklar = bagimliliklar
            }, cancellationToken);
            if (!sonuc.Basarili) return sonuc;
            if (sonuc.Veri is not UrunKaydi urun) return IslemSonucu.Hata("Ürün oluştu fakat kimliği alınamadı.");
            ayarlar.UygulamaUrunEslemeleri[uygulama.UygulamaKimligi] = urun.UrunKimligi;
            await KaydetKilitsizAsync(cancellationToken);
            return IslemSonucu.Basari($"{uygulama.UrunTuru} türündeki kodlanmış ürün piyasaya açıldı.", new { uygulama.UygulamaKimligi, urun.UrunKimligi, dogrulama.Durum });
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> UygulamaGuncelleAsync(string sirketKimligi, UrunGuncelleIstegi istek, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            if (!Ayarlar(sirketKimligi).UygulamaUrunEslemeleri.Values.Contains(istek.UrunKimligi, StringComparer.OrdinalIgnoreCase))
                return IslemSonucu.Hata("Bu ürün koddan ilan edilen bir manifestle eşleşmiyor.");
            return await _temel.UrunGuncelleAsync(sirketKimligi, istek, cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> UygulamaKapasitesiArtirAsync(string sirketKimligi, UrunKapasiteIstegi istek, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            if (!Ayarlar(sirketKimligi).UygulamaUrunEslemeleri.Values.Contains(istek.UrunKimligi, StringComparer.OrdinalIgnoreCase))
                return IslemSonucu.Hata("Kapasite yalnız koddan ilan edilen ürünlere alınabilir.");
            return await _temel.UrunKapasitesiArtirAsync(sirketKimligi, istek, cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> ProtokolYayinlaAsync(string sirketKimligi, ProtokolYayinlaIstegi istek, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            if (!sirket.BagliMi) return IslemSonucu.Hata("Protokol yayınında sunucu bağlı olmalıdır.");
            SunulanOzelProtokol? protokol = SunucuYayinManifestDeposu.Getir(sirketKimligi).OzelProtokoller.FirstOrDefault(p =>
                string.Equals(p.ProtokolKimligi, istek.ProtokolKimligi?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Surum, istek.Surum?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (protokol is null) return IslemSonucu.Hata("Protokol sunucudan kodla ilan edilmiyor.");
            List<string> eksik = protokol.Yetkinlikler.Where(y => !HizmetVarMi(sirket, y.HizmetKimligi, y.HizmetSurumu)).Select(y => y.YetkinlikKimligi).ToList();
            if (eksik.Count > 0) return IslemSonucu.Hata("Protokol yetkinlik hizmetleri eksik: " + string.Join(", ", eksik));
            string anahtar = $"{protokol.ProtokolKimligi}@{protokol.Surum}";
            if (Ayarlar(sirketKimligi).ProtokolPiyasaEslemeleri.ContainsKey(anahtar)) return IslemSonucu.Hata("Protokol zaten piyasada.");
            IslemSonucu sonuc = await _temel.ProtokolOlusturAsync(sirketKimligi, new ProtokolOlusturIstegi
            {
                ProtokolAdi = protokol.ProtokolAdi, Surum = protokol.Surum, Aciklama = protokol.Aciklama,
                LisansModeli = istek.LisansModeli, BenimsemeBedeli = istek.BenimsemeBedeli, TickLisansBedeli = istek.TickLisansBedeli
            }, cancellationToken);
            if (!sonuc.Basarili) return sonuc;
            if (sonuc.Veri is not OzelProtokolKaydi piyasa) return IslemSonucu.Hata("Piyasa protokol kimliği alınamadı.");
            Ayarlar(sirketKimligi).ProtokolPiyasaEslemeleri[anahtar] = piyasa.ProtokolKimligi;
            await KaydetKilitsizAsync(cancellationToken);
            return IslemSonucu.Basari("Kodlanmış protokol piyasaya açıldı.", piyasa);
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> ProtokolBenimseAsync(string sirketKimligi, ProtokolBenimseIstegi istek, CancellationToken cancellationToken) =>
        await _temel.ProtokolBenimseAsync(sirketKimligi, istek, cancellationToken);

    public async Task<string> PanelJsonuOlusturAsync(string sirketKimligi, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            string temelJson = await _temel.PanelJsonuOlusturAsync(sirketKimligi, cancellationToken);
            JsonObject kok = JsonNode.Parse(temelJson) as JsonObject ?? new();
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            SunucuYayinManifesti manifest = SunucuYayinManifestDeposu.Getir(sirketKimligi);
            SirketYayinAyarlari ayarlar = Ayarlar(sirketKimligi);
            JsonObject isletim = kok["isletim"] as JsonObject ?? new();
            JsonObject hizmetAyarlari = isletim["hizmetAyarlari"] as JsonObject ?? new();

            List<object> uygulamalar = manifest.Uygulamalar.Select(u =>
            {
                UygulamaDogrulamaSonucu d = UygulamayiDogrula(sirket, u);
                ayarlar.UygulamaUrunEslemeleri.TryGetValue(u.UygulamaKimligi, out string? urunKimligi);
                return (object)new { uygulama = u, dogrulama = d, urunKimligi = urunKimligi ?? string.Empty, piyasada = !string.IsNullOrWhiteSpace(urunKimligi), sunucuBagli = sirket.BagliMi };
            }).ToList();

            List<object> protokoller = manifest.OzelProtokoller.Select(p =>
            {
                string anahtar = $"{p.ProtokolKimligi}@{p.Surum}";
                ayarlar.ProtokolPiyasaEslemeleri.TryGetValue(anahtar, out string? piyasa);
                List<string> eksik = p.Yetkinlikler.Where(y => !HizmetVarMi(sirket, y.HizmetKimligi, y.HizmetSurumu)).Select(y => y.YetkinlikKimligi).ToList();
                return (object)new { protokol = p, gecerli = eksik.Count == 0, eksikYetkinlikler = eksik, piyasaProtokolKimligi = piyasa ?? string.Empty, piyasada = !string.IsNullOrWhiteSpace(piyasa) };
            }).ToList();

            List<object> hizmetler = sirket.Hizmetler.OrderBy(h => h.HizmetKimligi, StringComparer.OrdinalIgnoreCase).Select(h =>
            {
                string anahtar = $"{h.HizmetKimligi}@{h.HizmetSurumu}";
                JsonObject a = hizmetAyarlari[anahtar] as JsonObject ?? new();
                int baz = Int(a["sunucudanGelenIlkKapasite"], h.AzamiEszamanliIs);
                int satin = Int(a["satinAlinanKapasite"], 0);
                int etkin = h.AzamiEszamanliIs;
                int yatirim = Math.Max(0, etkin - baz - satin);
                return (object)new
                {
                    h.HizmetKimligi, h.HizmetSurumu, h.BirimFiyat, h.AzamiEszamanliIs, h.Aktif,
                    bazKapasite = baz, satinAlinanKapasite = satin, yatirimKapasiteBonusu = yatirim,
                    etkinKapasite = etkin,
                    sunucuDefaultFiyat = Decimal(a["sunucudanGelenIlkFiyat"], h.BirimFiyat),
                    fiyatYonetildi = Bool(a["fiyatYonetildi"]), aktiflikYonetildi = Bool(a["aktiflikYonetildi"]),
                    uygulamalardaKullaniliyor = manifest.Uygulamalar.Any(u => u.Ozellikler.Any(o =>
                        string.Equals(o.HizmetKimligi, h.HizmetKimligi, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(o.HizmetSurumu, h.HizmetSurumu, StringComparison.OrdinalIgnoreCase)))
                };
            }).ToList();

            JsonArray urunler = isletim["urunler"] as JsonArray ?? [];
            int toplamIslem = hizmetler.Sum(x => Int(JsonSerializer.SerializeToNode(x)?["etkinKapasite"], 0));
            int toplamUygulamaKapasitesi = urunler.OfType<JsonObject>().Sum(u => Int(u["kullaniciKapasitesi"], 0));
            int toplamAktifKullanici = urunler.OfType<JsonObject>().Sum(u => Int(u["aktifKullaniciSayisi"], 0));
            int toplamSatinAlinanUygulama = urunler.OfType<JsonObject>().Sum(u => Int(u["satinAlinanKullaniciKapasitesi"], 0));
            int toplamAltyapiBonusu = urunler.OfType<JsonObject>().Sum(u => Int(u["altyapiKapasiteBonusu"], 0));

            kok["kapasiteOzeti"] = JsonSerializer.SerializeToNode(new
            {
                toplamEszamanliIslemKapasitesi = sirket.Hizmetler.Where(h => h.Aktif).Sum(h => h.AzamiEszamanliIs),
                toplamHizmetKapasitesi = toplamIslem,
                aktifHizmetSayisi = sirket.Hizmetler.Count(h => h.Aktif),
                toplamUygulamaKullaniciKapasitesi = toplamUygulamaKapasitesi,
                toplamAktifKullanici,
                toplamSatinAlinanUygulamaKapasitesi = toplamSatinAlinanUygulama,
                toplamAltyapiKapasiteBonusu = toplamAltyapiBonusu,
                uygulamaDolulukOrani = toplamUygulamaKapasitesi <= 0 ? 0 : toplamAktifKullanici * 100d / toplamUygulamaKapasitesi
            }, JsonAyarlari);

            kok["kodTabanliYayinlar"] = JsonSerializer.SerializeToNode(new
            {
                manifestGuncellenmeZamani = manifest.GuncellenmeZamani,
                uygulamalar, protokoller, hizmetler,
                serbestUrunTurleri = new[] { "uygulama", "isletim-sistemi", "platform", "altyapi", "veritabani", "oyun", "gelistirici-araci", "yapay-zeka", "gömülü-sistem", "diger" },
                kural = "Sunucu kodu ve varsayılanları ilan eder; motor ilk kez kaydeder. 8090 fiyat, aktiflik ve kapasitede kalıcı otoritedir."
            }, JsonAyarlari);
            return kok.ToJsonString(JsonAyarlari);
        }
        finally { _kilit.Release(); }
    }

    public async Task TickCalistirAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            await GecersizUrunleriDurdurAsync(cancellationToken);
            await KaydetKilitsizAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
        await _temel.TickCalistirAsync(tickNumarasi, cancellationToken);
    }

    private async Task GecersizUrunleriDurdurAsync(CancellationToken cancellationToken)
    {
        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
        {
            SirketYayinAyarlari ayarlar = Ayarlar(sirket.SirketKimligi);
            if (ayarlar.UygulamaUrunEslemeleri.Count == 0) continue;
            JsonObject panel = JsonNode.Parse(await _temel.PanelJsonuOlusturAsync(sirket.SirketKimligi, cancellationToken)) as JsonObject ?? new();
            JsonArray urunler = panel["isletim"]?["urunler"] as JsonArray ?? [];
            SunucuYayinManifesti manifest = SunucuYayinManifestDeposu.Getir(sirket.SirketKimligi);
            foreach ((string uygulamaKimligi, string urunKimligi) in ayarlar.UygulamaUrunEslemeleri.ToList())
            {
                JsonObject? urun = urunler.OfType<JsonObject>().FirstOrDefault(u => string.Equals(u["urunKimligi"]?.GetValue<string>(), urunKimligi, StringComparison.OrdinalIgnoreCase));
                if (urun is null) { ayarlar.UygulamaUrunEslemeleri.Remove(uygulamaKimligi); continue; }
                SunulanUygulama? uygulama = manifest.Uygulamalar.FirstOrDefault(u => string.Equals(u.UygulamaKimligi, uygulamaKimligi, StringComparison.OrdinalIgnoreCase));
                bool calisiyor = sirket.BagliMi && uygulama is not null && UygulamayiDogrula(sirket, uygulama).Gecerli &&
                                 uygulama.Ozellikler.Where(o => o.Zorunlu).All(o => HizmetAktifMi(sirket, o.HizmetKimligi, o.HizmetSurumu));
                bool aktif = urun["aktif"]?.GetValue<bool>() ?? false;
                if (calisiyor || !aktif) continue;
                await _temel.UrunGuncelleAsync(sirket.SirketKimligi, new UrunGuncelleIstegi
                {
                    UrunKimligi = urunKimligi,
                    AbonelikUcreti = urun["abonelikUcreti"]?.GetValue<decimal>() ?? 0,
                    KullanimBasinaUcret = urun["kullanimBasinaUcret"]?.GetValue<decimal>() ?? 0,
                    Aktif = false
                }, cancellationToken);
                KonsolKayitcisi.Uyari($"Ürün teknik manifest/bağımlılık koşulunu kaybetti ve durduruldu | {sirket.SirketAdi} | {uygulamaKimligi}");
            }
        }
    }

    private UygulamaDogrulamaSonucu UygulamayiDogrula(SirketKaydi sirket, SunulanUygulama uygulama)
    {
        List<string> hatalar = [];
        List<string> uyarilar = [];
        List<UygulamaOzelligi> zorunluOzellikler = uygulama.Ozellikler.Where(o => o.Zorunlu).ToList();
        int asgariOzellik = uygulama.UrunTuru switch { "isletim-sistemi" => 3, "platform" => 2, "altyapi" => 2, _ => 1 };
        if (zorunluOzellikler.Count < asgariOzellik) hatalar.Add($"{uygulama.UrunTuru} türü en az {asgariOzellik} zorunlu çalıştırılabilir özellik ilan etmelidir.");
        foreach (UygulamaOzelligi ozellik in uygulama.Ozellikler)
            if (!HizmetVarMi(sirket, ozellik.HizmetKimligi, ozellik.HizmetSurumu))
                hatalar.Add($"{ozellik.OzellikKimligi} hizmeti ilan edilmiyor: {ozellik.HizmetKimligi}@{ozellik.HizmetSurumu}");

        foreach (UygulamaBagimliligi b in uygulama.Bagimliliklar)
        {
            SunulanUygulama? hedef = SunucuYayinManifestDeposu.Getir(b.SirketKimligi).Uygulamalar.FirstOrDefault(u =>
                string.Equals(u.UygulamaKimligi, b.UygulamaKimligi, StringComparison.OrdinalIgnoreCase));
            bool uygun = hedef is not null && SurumYeterliMi(hedef.Surum, b.AsgariSurum);
            if (!uygun && b.Zorunlu) hatalar.Add($"Zorunlu {b.BagimlilikTuru} bağlantısı yok: {b.SirketKimligi}/{b.UygulamaKimligi} >= {b.AsgariSurum}");
            else if (!uygun) uyarilar.Add($"Opsiyonel bağlantı kullanılamıyor: {b.SirketKimligi}/{b.UygulamaKimligi}");
        }

        foreach (string platform in uygulama.GerekliPlatformlar)
        {
            bool var = SunucuYayinManifestDeposu.TumunuGetir().SelectMany(m => m.Uygulamalar).Any(u =>
                string.Equals(u.UygulamaKimligi, platform, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.UygulamaAdi, platform, StringComparison.OrdinalIgnoreCase) ||
                u.DesteklenenPlatformlar.Contains(platform, StringComparer.OrdinalIgnoreCase));
            if (!var) hatalar.Add($"Gerekli platform/işletim sistemi manifesti yok: {platform}");
        }

        if (uygulama.DesteklenenPlatformlar.Count == 0 && uygulama.UrunTuru == "uygulama")
            uyarilar.Add("Uygulama platform bağımsız ilan edildi; belirli işletim sistemi stratejisi yok.");
        return new UygulamaDogrulamaSonucu
        {
            Gecerli = hatalar.Count == 0,
            Durum = hatalar.Count == 0 ? "Kod, özellik ve bağımlılık manifesti çalışabilir." : "Teknik manifest eksik.",
            Hatalar = hatalar.AsReadOnly(), Uyarilar = uyarilar.AsReadOnly(),
            ZorunluOzellikler = zorunluOzellikler.Select(o => o.OzellikKimligi).ToList(),
            OpsiyonelOzellikler = uygulama.Ozellikler.Where(o => !o.Zorunlu).Select(o => o.OzellikKimligi).ToList()
        };
    }

    private static UygulamaOzelligi? AnaOzellik(SunulanUygulama u) => u.Ozellikler.FirstOrDefault(o => o.Zorunlu) ?? u.Ozellikler.FirstOrDefault();
    private SirketYayinAyarlari Ayarlar(string kimlik) { if (_yayinlar.Sirketler.TryGetValue(kimlik, out SirketYayinAyarlari? a)) return a; a = new(); _yayinlar.Sirketler[kimlik] = a; return a; }
    private SirketKaydi SirketZorunlu(string kimlik) => _sirketler.SirketKayitlari.FirstOrDefault(s => string.Equals(s.SirketKimligi, kimlik, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("Şirket bulunamadı.");
    private static bool HizmetVarMi(SirketKaydi s, string kimlik, string surum) => s.Hizmetler.Any(h => string.Equals(h.HizmetKimligi, kimlik, StringComparison.OrdinalIgnoreCase) && string.Equals(h.HizmetSurumu, surum, StringComparison.OrdinalIgnoreCase));
    private static bool HizmetAktifMi(SirketKaydi s, string kimlik, string surum) => s.Hizmetler.Any(h => h.Aktif && string.Equals(h.HizmetKimligi, kimlik, StringComparison.OrdinalIgnoreCase) && string.Equals(h.HizmetSurumu, surum, StringComparison.OrdinalIgnoreCase));
    private static bool SurumYeterliMi(string mevcut, string asgari) => Version.TryParse(mevcut, out Version? m) && Version.TryParse(asgari, out Version? a) ? m >= a : string.Compare(mevcut, asgari, StringComparison.OrdinalIgnoreCase) >= 0;
    private static int Int(JsonNode? n, int d = 0) { try { return n?.GetValue<int>() ?? d; } catch { return d; } }
    private static decimal Decimal(JsonNode? n, decimal d = 0) { try { return n?.GetValue<decimal>() ?? d; } catch { return d; } }
    private static bool Bool(JsonNode? n) { try { return n?.GetValue<bool>() ?? false; } catch { return false; } }

    private async Task KaydetKilitsizAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        _yayinlar.GuncellenmeZamani = DateTimeOffset.UtcNow;
        string gecici = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(gecici, JsonSerializer.Serialize(_yayinlar, JsonAyarlari), new UTF8Encoding(false), cancellationToken);
        File.Move(gecici, _dosyaYolu, true);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try { if (_baslatildi) { await _kilit.WaitAsync(); try { await KaydetKilitsizAsync(CancellationToken.None); } finally { _kilit.Release(); } } }
        finally { _disposed = true; _kilit.Dispose(); await _temel.DisposeAsync(); }
    }
}
