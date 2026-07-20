using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class EkosistemDosyasi
{
    public int Surum { get; set; } = 92;
    public DateTimeOffset GuncellenmeZamani { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, SirketEkosistemAyarlari> Sirketler { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SirketEkosistemAyarlari
{
    public Dictionary<string, UrunDagitimAyari> Urunler { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class UrunDagitimAyari
{
    public string UrunKimligi { get; set; } = string.Empty;
    public string UygulamaKimligi { get; set; } = string.Empty;
    public string UrunTuru { get; set; } = "uygulama";
    public string IsletimSistemiKimligi { get; set; } = string.Empty;

    // V9.2 ana kayıt alanı.
    public List<string> BaglantiProtokoluKimlikleri { get; set; } = [];

    // Eski ekosistem.json kayıtlarını kaybetmeden dönüştürmek için tutulur.
    public string BaglantiProtokoluKimligi { get; set; } = string.Empty;
    public bool KullaniciTarafindanYapilandirildi { get; set; }
    public bool ElleAktifOlmasiIsteniyor { get; set; }
    public bool Uyumlu { get; set; }
    public string UyumDurumu { get; set; } = "Yapılandırılmadı.";

    public IReadOnlyList<string> ProtokolleriGetir()
    {
        IEnumerable<string> kaynak = BaglantiProtokoluKimlikleri ?? [];
        if (!string.IsNullOrWhiteSpace(BaglantiProtokoluKimligi))
            kaynak = kaynak.Append(BaglantiProtokoluKimligi);
        return kaynak.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    public void ProtokolleriAyarla(IEnumerable<string>? protokoller)
    {
        BaglantiProtokoluKimlikleri = (protokoller ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        BaglantiProtokoluKimligi = BaglantiProtokoluKimlikleri.FirstOrDefault() ?? string.Empty;
    }
}

public sealed class UrunDagitimGuncelleIstegi
{
    public string UrunKimligi { get; set; } = string.Empty;
    public string IsletimSistemiKimligi { get; set; } = string.Empty;
    public string BaglantiProtokoluKimligi { get; set; } = string.Empty;
    public List<string> BaglantiProtokoluKimlikleri { get; set; } = [];
    public bool Aktif { get; set; } = true;

    public IReadOnlyList<string> ProtokolleriGetir()
    {
        IEnumerable<string> kaynak = BaglantiProtokoluKimlikleri ?? [];
        if (!string.IsNullOrWhiteSpace(BaglantiProtokoluKimligi))
            kaynak = kaynak.Append(BaglantiProtokoluKimligi);
        return kaynak.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }
}

public sealed class IsletimSistemiPazarKaydi
{
    public string SirketKimligi { get; init; } = string.Empty;
    public string SirketAdi { get; init; } = string.Empty;
    public string UrunKimligi { get; init; } = string.Empty;
    public string UygulamaKimligi { get; init; } = string.Empty;
    public string UygulamaAdi { get; init; } = string.Empty;
    public string Surum { get; init; } = "1.0";
    public IReadOnlyList<string> Protokoller { get; init; } = [];
    public int KullaniciKapasitesi { get; init; }
    public int AktifMusteriSayisi { get; set; }
    public decimal Fiyat { get; init; }
    public double Kalite { get; init; }
    public double Performans { get; init; }
    public double Guvenlik { get; init; }
    public double OperasyonRiski { get; init; }
    public bool Aktif { get; init; }
}

public sealed class AltyapiSinirlari
{
    public int ToplamHizmetKapasitesi { get; init; }
    public int GuvenliEszamanliIslemSiniri { get; init; }
    public int MevcutAktifIs { get; init; }
    public int KuyrukUzunlugu { get; init; }
    public int ToplamUygulamaKapasitesi { get; init; }
    public int GuvenliAktifKullaniciSiniri { get; init; }
    public int MevcutAktifKullanici { get; init; }
    public double IslemYukOrani { get; init; }
    public double KullaniciYukOrani { get; init; }
    public double GecikmeBaskisi { get; init; }
    public double BirlesikYukOrani { get; init; }
    public string Durum { get; init; } = "normal";
}

public static class IsletimSistemiPazarDeposu
{
    private static readonly object Kilit = new();
    private static IReadOnlyList<IsletimSistemiPazarKaydi> _kayitlar = [];
    private static Dictionary<string, int> _dagilim = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<IsletimSistemiPazarKaydi> Getir()
    {
        lock (Kilit) return _kayitlar.Select(Kopyala).ToList().AsReadOnly();
    }

    public static void Guncelle(IEnumerable<IsletimSistemiPazarKaydi> kayitlar)
    {
        lock (Kilit)
        {
            _kayitlar = kayitlar.Select(x =>
            {
                IsletimSistemiPazarKaydi kopya = Kopyala(x);
                kopya.AktifMusteriSayisi = _dagilim.TryGetValue(kopya.UygulamaKimligi, out int sayi) ? sayi : 0;
                return kopya;
            }).ToList().AsReadOnly();
        }
    }

    public static void MusteriDagiliminiGuncelle(IReadOnlyDictionary<string, int> dagilim)
    {
        lock (Kilit)
        {
            _dagilim = new Dictionary<string, int>(dagilim, StringComparer.OrdinalIgnoreCase);
            foreach (IsletimSistemiPazarKaydi kayit in _kayitlar)
                kayit.AktifMusteriSayisi = _dagilim.TryGetValue(kayit.UygulamaKimligi, out int sayi) ? sayi : 0;
        }
    }

    private static IsletimSistemiPazarKaydi Kopyala(IsletimSistemiPazarKaydi x) => new()
    {
        SirketKimligi = x.SirketKimligi,
        SirketAdi = x.SirketAdi,
        UrunKimligi = x.UrunKimligi,
        UygulamaKimligi = x.UygulamaKimligi,
        UygulamaAdi = x.UygulamaAdi,
        Surum = x.Surum,
        Protokoller = x.Protokoller.ToList().AsReadOnly(),
        KullaniciKapasitesi = x.KullaniciKapasitesi,
        AktifMusteriSayisi = x.AktifMusteriSayisi,
        Fiyat = x.Fiyat,
        Kalite = x.Kalite,
        Performans = x.Performans,
        Guvenlik = x.Guvenlik,
        OperasyonRiski = x.OperasyonRiski,
        Aktif = x.Aktif
    };
}

/// <summary>
/// V9.2 ürün dağıtım katmanı. Finans veya kapasite üretmez; yalnız aktif OS ve
/// bir ya da daha fazla kanonik protokol üzerinden dağıtım uyumluluğu kurar.
/// </summary>
public sealed class EkosistemYoneticisi : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SirketYoneticisi _sirketler;
    private readonly KodTabanliSirketIsletimYoneticisi _isletim;
    private readonly string _dosyaYolu;
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private EkosistemDosyasi _veri = new();
    private bool _baslatildi;

    public EkosistemYoneticisi(
        SirketYoneticisi sirketler,
        KodTabanliSirketIsletimYoneticisi isletim,
        string motorVerileriKlasoru)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _isletim = isletim ?? throw new ArgumentNullException(nameof(isletim));
        _dosyaYolu = Path.Combine(Path.GetFullPath(motorVerileriKlasoru), "ekosistem.json");
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
                _veri = JsonSerializer.Deserialize<EkosistemDosyasi>(json, JsonAyarlari) ?? new();
            }
            NormalizeEt();
            await KaydetKilitsizAsync(cancellationToken);
            _baslatildi = true;
            KonsolKayitcisi.Basari("V9.2 ekosistem hazır | OS ve uygulamalar çoklu protokol kabul eder; kapasite V9.2 tek havuzundadır.");
        }
        finally { _kilit.Release(); }
    }

    public async Task PazariHazirlaAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            Dictionary<string, JsonObject> paneller = new(StringComparer.OrdinalIgnoreCase);
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari.Where(x => x.BagliMi))
                paneller[sirket.SirketKimligi] = await PanelOkuAsync(sirket.SirketKimligi, cancellationToken);

            HashSet<string> aktifProtokoller = AktifProtokolleriBul(paneller);
            List<IsletimSistemiPazarKaydi> sistemler = [];

            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari.Where(x => x.BagliMi))
            {
                JsonObject panel = paneller[sirket.SirketKimligi];
                SirketEkosistemAyarlari ayarlar = SirketAyari(sirket.SirketKimligi);
                Dictionary<string, JsonObject> uygulamalar = UygulamaIndeksi(panel);
                foreach (JsonObject urun in Urunler(panel).Where(x => Normal(Str(x["urunTuru"]), "uygulama") == "isletim-sistemi"))
                {
                    string urunKimligi = Str(urun["urunKimligi"]);
                    string uygulamaKimligi = Str(urun["uygulamaKimligi"]);
                    if (string.IsNullOrWhiteSpace(urunKimligi)) continue;
                    uygulamalar.TryGetValue(uygulamaKimligi, out JsonObject? uygulama);
                    UrunDagitimAyari ayar = UrunAyari(ayarlar, urunKimligi, uygulamaKimligi, "isletim-sistemi");
                    VarsayilanlariUygula(ayar, uygulama, Bool(urun["aktif"]));
                    (ayar.Uyumlu, ayar.UyumDurumu) = IsletimSistemiDogrula(ayar, aktifProtokoller);
                    await AktiflikUygulaAsync(sirket.SirketKimligi, urun, ayar, cancellationToken);
                    if (!ayar.Uyumlu || !ayar.ElleAktifOlmasiIsteniyor) continue;
                    sistemler.Add(new IsletimSistemiPazarKaydi
                    {
                        SirketKimligi = sirket.SirketKimligi,
                        SirketAdi = sirket.SirketAdi,
                        UrunKimligi = urunKimligi,
                        UygulamaKimligi = uygulamaKimligi,
                        UygulamaAdi = Str(urun["urunAdi"]),
                        Surum = Str(uygulama?["surum"], "1.0"),
                        Protokoller = ayar.ProtokolleriGetir(),
                        KullaniciKapasitesi = Int(urun["kullaniciKapasitesi"]),
                        Fiyat = UrunFiyati(urun),
                        Kalite = Num(urun["urunKalitesi"]),
                        Performans = sirket.PerformansPuani,
                        Guvenlik = sirket.GuvenlikPuani,
                        OperasyonRiski = 0,
                        Aktif = true
                    });
                }
            }
            IsletimSistemiPazarDeposu.Guncelle(sistemler);

            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari.Where(x => x.BagliMi))
            {
                JsonObject panel = paneller[sirket.SirketKimligi];
                SirketEkosistemAyarlari ayarlar = SirketAyari(sirket.SirketKimligi);
                Dictionary<string, JsonObject> uygulamalar = UygulamaIndeksi(panel);
                foreach (JsonObject urun in Urunler(panel).Where(x => Normal(Str(x["urunTuru"]), "uygulama") != "isletim-sistemi"))
                {
                    string urunKimligi = Str(urun["urunKimligi"]);
                    string uygulamaKimligi = Str(urun["uygulamaKimligi"]);
                    if (string.IsNullOrWhiteSpace(urunKimligi)) continue;
                    uygulamalar.TryGetValue(uygulamaKimligi, out JsonObject? uygulama);
                    UrunDagitimAyari ayar = UrunAyari(ayarlar, urunKimligi, uygulamaKimligi, Normal(Str(urun["urunTuru"]), "uygulama"));
                    VarsayilanlariUygula(ayar, uygulama, Bool(urun["aktif"]));
                    (ayar.Uyumlu, ayar.UyumDurumu) = UygulamaDogrula(ayar, aktifProtokoller);
                    await AktiflikUygulaAsync(sirket.SirketKimligi, urun, ayar, cancellationToken);
                }
            }
            await KaydetKilitsizAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> DagitimGuncelleAsync(
        string sirketKimligi,
        UrunDagitimGuncelleIstegi istek,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            JsonObject panel = await PanelOkuAsync(sirketKimligi, cancellationToken);
            JsonObject? urun = Urunler(panel).FirstOrDefault(x =>
                Str(x["urunKimligi"]).Equals(istek.UrunKimligi, StringComparison.OrdinalIgnoreCase));
            if (urun is null) return IslemSonucu.Hata("Ürün bulunamadı.");

            string tur = Normal(Str(urun["urunTuru"]), "uygulama");
            string uygulamaKimligi = Str(urun["uygulamaKimligi"]);
            UygulamaIndeksi(panel).TryGetValue(uygulamaKimligi, out JsonObject? uygulama);
            UrunDagitimAyari ayar = UrunAyari(SirketAyari(sirketKimligi), istek.UrunKimligi, uygulamaKimligi, tur);
            ayar.IsletimSistemiKimligi = istek.IsletimSistemiKimligi?.Trim() ?? string.Empty;
            ayar.ProtokolleriAyarla(istek.ProtokolleriGetir());
            ayar.KullaniciTarafindanYapilandirildi = true;
            ayar.ElleAktifOlmasiIsteniyor = istek.Aktif;

            Dictionary<string, JsonObject> paneller = new(StringComparer.OrdinalIgnoreCase);
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari.Where(x => x.BagliMi))
                paneller[sirket.SirketKimligi] = sirket.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase)
                    ? panel
                    : await PanelOkuAsync(sirket.SirketKimligi, cancellationToken);
            HashSet<string> aktifProtokoller = AktifProtokolleriBul(paneller);

            (ayar.Uyumlu, ayar.UyumDurumu) = tur == "isletim-sistemi"
                ? IsletimSistemiDogrula(ayar, aktifProtokoller)
                : UygulamaDogrula(ayar, aktifProtokoller);
            await AktiflikUygulaAsync(sirketKimligi, urun, ayar, cancellationToken);
            await KaydetKilitsizAsync(cancellationToken);
            return IslemSonucu.Basari(
                ayar.Uyumlu
                    ? (istek.Aktif ? $"Dağıtım uyumlu; ürün {ayar.ProtokolleriGetir().Count} protokolle yayına alındı." : "Dağıtım kaydedildi; ürün pasif.")
                    : "Dağıtım kaydedildi fakat ürün pasif: " + ayar.UyumDurumu,
                ayar);
        }
        finally { _kilit.Release(); }
    }

    public async Task<string> PanelJsonunuZenginlestirAsync(
        string sirketKimligi,
        string temelJson,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            JsonObject panel = JsonNode.Parse(temelJson) as JsonObject ?? new();
            SirketEkosistemAyarlari ayarlar = SirketAyari(sirketKimligi);
            List<object> dagitim = Urunler(panel).Select(u =>
            {
                string kimlik = Str(u["urunKimligi"]);
                UrunDagitimAyari ayar = UrunAyari(ayarlar, kimlik, Str(u["uygulamaKimligi"]), Normal(Str(u["urunTuru"]), "uygulama"));
                return (object)new
                {
                    urunKimligi = kimlik,
                    ayar,
                    baglantiProtokoluKimlikleri = ayar.ProtokolleriGetir()
                };
            }).ToList();
            V9SirketKapasiteDurumu kapasite = V9PazarDeposu.Getir().SirketKapasiteleri.TryGetValue(
                sirketKimligi,
                out V9SirketKapasiteDurumu? k) ? k : new V9SirketKapasiteDurumu { SirketKimligi = sirketKimligi };
            panel["ekosistem"] = JsonSerializer.SerializeToNode(new
            {
                altyapiSinirlari = new AltyapiSinirlari
                {
                    ToplamHizmetKapasitesi = kapasite.ToplamFizikselKapasite,
                    GuvenliEszamanliIslemSiniri = kapasite.ToplamFizikselKapasite,
                    MevcutAktifIs = kapasite.KullanilanKapasite,
                    ToplamUygulamaKapasitesi = kapasite.ToplamFizikselKapasite,
                    GuvenliAktifKullaniciSiniri = kapasite.ToplamFizikselKapasite,
                    MevcutAktifKullanici = kapasite.KullanilanKapasite,
                    IslemYukOrani = kapasite.ToplamFizikselKapasite <= 0 ? 0 : kapasite.KullanilanKapasite / (double)kapasite.ToplamFizikselKapasite,
                    KullaniciYukOrani = kapasite.ToplamFizikselKapasite <= 0 ? 0 : kapasite.KullanilanKapasite / (double)kapasite.ToplamFizikselKapasite,
                    BirlesikYukOrani = kapasite.DolulukOrani / 100d,
                    Durum = kapasite.DolulukOrani switch { < 70 => "rahat", < 90 => "yüksek", < 110 => "dolu", _ => "aşırı" }
                },
                dagitimAyarlari = dagitim,
                isletimSistemleri = IsletimSistemiPazarDeposu.Getir(),
                zorunluKural = "Ürün toplam fiziksel kapasiteden pay alır; OS birden fazla aktif kanonik protokol kabul edebilir."
            }, JsonAyarlari);
            return panel.ToJsonString(JsonAyarlari);
        }
        finally { _kilit.Release(); }
    }

    private void NormalizeEt()
    {
        _veri.Surum = 92;
        _veri.Sirketler ??= new(StringComparer.OrdinalIgnoreCase);
        foreach (SirketEkosistemAyarlari ayar in _veri.Sirketler.Values)
        {
            ayar.Urunler ??= new(StringComparer.OrdinalIgnoreCase);
            foreach (UrunDagitimAyari urun in ayar.Urunler.Values)
                urun.ProtokolleriAyarla(urun.ProtokolleriGetir());
        }
    }

    private static HashSet<string> AktifProtokolleriBul(IReadOnlyDictionary<string, JsonObject> paneller)
    {
        HashSet<string> sonuc = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonObject panel in paneller.Values)
        {
            foreach (JsonObject kayit in (panel["kodTabanliYayinlar"]?["protokoller"] as JsonArray ?? [])
                         .OfType<JsonObject>().Where(x => Bool(x["piyasada"])))
            {
                string teknik = Str(kayit["protokol"]?["protokolKimligi"]);
                string piyasa = Str(kayit["piyasaProtokolKimligi"]);
                if (!string.IsNullOrWhiteSpace(teknik)) sonuc.Add(teknik);
                if (!string.IsNullOrWhiteSpace(piyasa)) sonuc.Add(piyasa);
            }
        }
        return sonuc;
    }

    private static (bool, string) IsletimSistemiDogrula(
        UrunDagitimAyari ayar,
        IReadOnlySet<string> aktifProtokoller)
    {
        IReadOnlyList<string> protokoller = ayar.ProtokolleriGetir();
        if (protokoller.Count == 0) return (false, "En az bir aktif bağlantı protokolü seçilmelidir.");
        List<string> pasif = protokoller.Where(x => !aktifProtokoller.Contains(x)).ToList();
        if (pasif.Count > 0) return (false, "Piyasada aktif olmayan protokoller: " + string.Join(", ", pasif));
        return (true, $"İşletim sistemi {protokoller.Count} aktif protokol üzerinden müşteri kabul edebilir.");
    }

    private static (bool, string) UygulamaDogrula(
        UrunDagitimAyari ayar,
        IReadOnlySet<string> aktifProtokoller)
    {
        if (string.IsNullOrWhiteSpace(ayar.IsletimSistemiKimligi)) return (false, "İşletim sistemi seçilmedi.");
        IReadOnlyList<string> protokoller = ayar.ProtokolleriGetir();
        if (protokoller.Count == 0) return (false, "En az bir bağlantı protokolü seçilmedi.");
        List<string> pasif = protokoller.Where(x => !aktifProtokoller.Contains(x)).ToList();
        if (pasif.Count > 0) return (false, "Piyasada aktif olmayan protokoller: " + string.Join(", ", pasif));
        IsletimSistemiPazarKaydi? sistem = IsletimSistemiPazarDeposu.Getir().FirstOrDefault(x =>
            x.UygulamaKimligi.Equals(ayar.IsletimSistemiKimligi, StringComparison.OrdinalIgnoreCase) && x.Aktif);
        if (sistem is null) return (false, "Seçilen işletim sistemi aktif piyasada değil.");
        List<string> desteklenmeyen = protokoller.Where(x => !sistem.Protokoller.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();
        if (desteklenmeyen.Count > 0)
            return (false, "İşletim sistemi şu protokolleri desteklemiyor: " + string.Join(", ", desteklenmeyen));
        return (true, $"İşletim sistemi ve {protokoller.Count} protokol bağlantısı geçerli.");
    }

    private static void VarsayilanlariUygula(UrunDagitimAyari ayar, JsonObject? uygulama, bool mevcutAktiflik)
    {
        if (ayar.KullaniciTarafindanYapilandirildi) return;
        List<string> protokoller = (uygulama?["desteklenenProtokoller"] as JsonArray)?
            .Select(x => Str(x)).Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        ayar.ProtokolleriAyarla(protokoller);
        ayar.ElleAktifOlmasiIsteniyor = mevcutAktiflik;
    }

    private async Task AktiflikUygulaAsync(
        string sirketKimligi,
        JsonObject urun,
        UrunDagitimAyari ayar,
        CancellationToken cancellationToken)
    {
        bool hedef = ayar.Uyumlu && ayar.ElleAktifOlmasiIsteniyor;
        if (Bool(urun["aktif"]) == hedef) return;
        await _isletim.UygulamaGuncelleAsync(sirketKimligi, new UrunGuncelleIstegi
        {
            UrunKimligi = Str(urun["urunKimligi"]),
            AbonelikUcreti = Dec(urun["abonelikUcreti"]),
            KullanimBasinaUcret = Dec(urun["kullanimBasinaUcret"]),
            Aktif = hedef
        }, cancellationToken);
    }

    private async Task<JsonObject> PanelOkuAsync(string sirketKimligi, CancellationToken cancellationToken) =>
        JsonNode.Parse(await _isletim.PanelJsonuOlusturAsync(sirketKimligi, cancellationToken)) as JsonObject ?? new();

    private static Dictionary<string, JsonObject> UygulamaIndeksi(JsonObject panel) =>
        (panel["kodTabanliYayinlar"]?["uygulamalar"] as JsonArray ?? [])
        .OfType<JsonObject>().Select(x => x["uygulama"] as JsonObject)
        .Where(x => x is not null && !string.IsNullOrWhiteSpace(Str(x["uygulamaKimligi"])))
        .ToDictionary(x => Str(x!["uygulamaKimligi"]), x => x!, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<JsonObject> Urunler(JsonObject panel) =>
        (panel["isletim"]?["urunler"] as JsonArray ?? []).OfType<JsonObject>();

    private SirketEkosistemAyarlari SirketAyari(string sirketKimligi)
    {
        if (_veri.Sirketler.TryGetValue(sirketKimligi, out SirketEkosistemAyarlari? ayar)) return ayar;
        ayar = new SirketEkosistemAyarlari();
        _veri.Sirketler[sirketKimligi] = ayar;
        return ayar;
    }

    private static UrunDagitimAyari UrunAyari(
        SirketEkosistemAyarlari sirket,
        string urunKimligi,
        string uygulamaKimligi,
        string tur)
    {
        if (sirket.Urunler.TryGetValue(urunKimligi, out UrunDagitimAyari? ayar)) return ayar;
        ayar = new UrunDagitimAyari
        {
            UrunKimligi = urunKimligi,
            UygulamaKimligi = uygulamaKimligi,
            UrunTuru = tur
        };
        sirket.Urunler[urunKimligi] = ayar;
        return ayar;
    }

    private async Task KaydetKilitsizAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        _veri.GuncellenmeZamani = DateTimeOffset.UtcNow;
        string tmp = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(_veri, JsonAyarlari), new UTF8Encoding(false), cancellationToken);
        File.Move(tmp, _dosyaYolu, overwrite: true);
    }

    private static string Str(JsonNode? n, string varsayilan = "") { try { return n?.GetValue<string>() ?? varsayilan; } catch { return varsayilan; } }
    private static int Int(JsonNode? n) { try { return n?.GetValue<int>() ?? 0; } catch { return 0; } }
    private static decimal Dec(JsonNode? n) { try { return n?.GetValue<decimal>() ?? 0; } catch { return 0; } }
    private static double Num(JsonNode? n) { try { return n?.GetValue<double>() ?? 0; } catch { return 0; } }
    private static bool Bool(JsonNode? n) { try { return n?.GetValue<bool>() ?? false; } catch { return false; } }
    private static string Normal(string? s, string varsayilan) => string.IsNullOrWhiteSpace(s) ? varsayilan : s.Trim().ToLowerInvariant();
    private static decimal UrunFiyati(JsonObject urun) =>
        Str(urun["fiyatlandirmaModeli"]).Equals("kullanim", StringComparison.OrdinalIgnoreCase)
            ? Dec(urun["kullanimBasinaUcret"])
            : Dec(urun["abonelikUcreti"]);

    public async ValueTask DisposeAsync()
    {
        if (!_baslatildi) { _kilit.Dispose(); return; }
        await _kilit.WaitAsync();
        try { await KaydetKilitsizAsync(CancellationToken.None); }
        finally { _kilit.Release(); _kilit.Dispose(); }
    }
}
