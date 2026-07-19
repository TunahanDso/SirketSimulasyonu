using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class EkosistemDosyasi
{
    public int Surum { get; set; } = 2;
    public DateTimeOffset GuncellenmeZamani { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, SirketEkosistemAyarlari> Sirketler { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SirketEkosistemAyarlari
{
    public Dictionary<string, UrunDagitimAyari> Urunler { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> OtomatikDuraklatilanHizmetler { get; set; } = [];
    public long HizmetDuraksatmaBitisTicki { get; set; }
    public int ArdisikAsiriYukTicki { get; set; }
}

public sealed class UrunDagitimAyari
{
    public string UrunKimligi { get; set; } = string.Empty;
    public string UygulamaKimligi { get; set; } = string.Empty;
    public string UrunTuru { get; set; } = "uygulama";
    public string IsletimSistemiKimligi { get; set; } = string.Empty;
    public string BaglantiProtokoluKimligi { get; set; } = string.Empty;
    public bool KullaniciTarafindanYapilandirildi { get; set; }
    public bool ElleAktifOlmasiIsteniyor { get; set; }
    public bool Uyumlu { get; set; }
    public string UyumDurumu { get; set; } = "Yapılandırılmadı.";
    public int AsiriYukTickSayisi { get; set; }
    public long OtomatikDuraksatmaBitisTicki { get; set; }
    public string SonDuraksatmaNedeni { get; set; } = string.Empty;
    public double SonYukOrani { get; set; }
}

public sealed class UrunDagitimGuncelleIstegi
{
    public string UrunKimligi { get; set; } = string.Empty;
    public string IsletimSistemiKimligi { get; set; } = string.Empty;
    public string BaglantiProtokoluKimligi { get; set; } = string.Empty;
    public bool Aktif { get; set; } = true;
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
    private static Dictionary<string, int> _musteriDagilimi = new(StringComparer.OrdinalIgnoreCase);

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
                kopya.AktifMusteriSayisi = _musteriDagilimi.TryGetValue(kopya.UygulamaKimligi, out int sayi) ? sayi : 0;
                return kopya;
            }).ToList().AsReadOnly();
        }
    }

    public static void MusteriDagiliminiGuncelle(IReadOnlyDictionary<string, int> dagilim)
    {
        lock (Kilit)
        {
            _musteriDagilimi = new Dictionary<string, int>(dagilim, StringComparer.OrdinalIgnoreCase);
            foreach (IsletimSistemiPazarKaydi kayit in _kayitlar)
                kayit.AktifMusteriSayisi = _musteriDagilimi.TryGetValue(kayit.UygulamaKimligi, out int sayi) ? sayi : 0;
        }
    }

    private static IsletimSistemiPazarKaydi Kopyala(IsletimSistemiPazarKaydi x) => new()
    {
        SirketKimligi = x.SirketKimligi, SirketAdi = x.SirketAdi, UrunKimligi = x.UrunKimligi,
        UygulamaKimligi = x.UygulamaKimligi, UygulamaAdi = x.UygulamaAdi, Surum = x.Surum,
        Protokoller = x.Protokoller.ToList().AsReadOnly(), KullaniciKapasitesi = x.KullaniciKapasitesi,
        AktifMusteriSayisi = x.AktifMusteriSayisi, Fiyat = x.Fiyat, Kalite = x.Kalite,
        Performans = x.Performans, Guvenlik = x.Guvenlik, OperasyonRiski = x.OperasyonRiski, Aktif = x.Aktif
    };
}

public sealed class MusteriIsletimSistemiYoneticisi
{
    private readonly MusteriYoneticisi _musteriler;
    private readonly Random _rastgele = new(20260720);

    public MusteriIsletimSistemiYoneticisi(MusteriYoneticisi musteriler) =>
        _musteriler = musteriler ?? throw new ArgumentNullException(nameof(musteriler));

    public void TickCalistir(long tickNumarasi)
    {
        IReadOnlyList<IsletimSistemiPazarKaydi> sistemler = IsletimSistemiPazarDeposu.Getir()
            .Where(x => x.Aktif && x.KullaniciKapasitesi > 0 && x.Protokoller.Count > 0).ToList();
        Dictionary<string, int> dagilim = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IsletimSistemiPazarKaydi> indeks = sistemler.ToDictionary(x => x.UygulamaKimligi, StringComparer.OrdinalIgnoreCase);

        foreach (Musteri musteri in _musteriler.Musteriler.Where(x => x.Aktif))
        {
            IsletimSistemiPazarKaydi? mevcut = null;
            if (!string.IsNullOrWhiteSpace(musteri.IsletimSistemiKimligi))
                indeks.TryGetValue(musteri.IsletimSistemiKimligi, out mevcut);

            bool mevcutGecerli = mevcut is not null;
            if (mevcut is not null)
            {
                dagilim.TryGetValue(mevcut.UygulamaKimligi, out int mevcutSayi);
                if (mevcutSayi >= mevcut.KullaniciKapasitesi) mevcutGecerli = false;
                else
                {
                    double hedef = Math.Clamp((mevcut.Kalite + mevcut.Performans + mevcut.Guvenlik) / 3d - mevcut.OperasyonRiski * 0.22, 0, 100);
                    musteri.IsletimSistemiMemnuniyeti += (hedef - musteri.IsletimSistemiMemnuniyeti) * 0.035;
                    double degisim = 0.0015 + Math.Max(0, 58 - musteri.IsletimSistemiMemnuniyeti) / 2_500d + mevcut.OperasyonRiski / 18_000d;
                    if (tickNumarasi - musteri.SonIsletimSistemiDegisimTicki > 5 && _rastgele.NextDouble() < degisim)
                        mevcutGecerli = false;
                }
            }

            IsletimSistemiPazarKaydi? secilen = mevcutGecerli ? mevcut : SistemSec(sistemler, dagilim, musteri);
            if (secilen is null)
            {
                musteri.IsletimSistemiKimligi = "isletim-sistemi-bekleniyor";
                musteri.IsletimSistemiSurumu = string.Empty;
                musteri.IsletimSistemiSirketKimligi = string.Empty;
                continue;
            }

            if (!string.Equals(musteri.IsletimSistemiKimligi, secilen.UygulamaKimligi, StringComparison.OrdinalIgnoreCase))
            {
                musteri.IsletimSistemiDegisimSayisi++;
                musteri.SonIsletimSistemiDegisimTicki = tickNumarasi;
                musteri.IsletimSistemiMemnuniyeti = 52;
            }
            musteri.IsletimSistemiKimligi = secilen.UygulamaKimligi;
            musteri.IsletimSistemiSurumu = secilen.Surum;
            musteri.IsletimSistemiSirketKimligi = secilen.SirketKimligi;
            dagilim.TryGetValue(secilen.UygulamaKimligi, out int sayi);
            dagilim[secilen.UygulamaKimligi] = sayi + 1;
        }
        IsletimSistemiPazarDeposu.MusteriDagiliminiGuncelle(dagilim);
    }

    private IsletimSistemiPazarKaydi? SistemSec(IReadOnlyList<IsletimSistemiPazarKaydi> sistemler, IReadOnlyDictionary<string, int> dagilim, Musteri musteri)
    {
        List<(IsletimSistemiPazarKaydi Sistem, double Agirlik)> adaylar = [];
        foreach (IsletimSistemiPazarKaydi sistem in sistemler)
        {
            dagilim.TryGetValue(sistem.UygulamaKimligi, out int kullanim);
            if (kullanim >= sistem.KullaniciKapasitesi) continue;
            double bosluk = Math.Clamp(1 - (double)kullanim / sistem.KullaniciKapasitesi, 0.05, 1);
            double teknik = (sistem.Kalite * 0.32 + sistem.Performans * 0.28 + sistem.Guvenlik * 0.30) / 100d;
            double fiyat = 1d / (1d + (double)Math.Max(0, sistem.Fiyat) / Math.Max(10d, (double)musteri.TickBasinaHarcamaButcesi));
            double risk = Math.Clamp(1 - sistem.OperasyonRiski / 115d, 0.08, 1);
            adaylar.Add((sistem, Math.Max(0.001, teknik * fiyat * risk * Math.Sqrt(bosluk) * (0.85 + _rastgele.NextDouble() * 0.3))));
        }
        if (adaylar.Count == 0) return null;
        double secim = _rastgele.NextDouble() * adaylar.Sum(x => x.Agirlik);
        foreach (var aday in adaylar) { secim -= aday.Agirlik; if (secim <= 0) return aday.Sistem; }
        return adaylar[^1].Sistem;
    }
}

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
    private readonly Random _rastgele = new(20260721);
    private EkosistemDosyasi _veri = new();
    private long _tick;
    private bool _baslatildi;

    public EkosistemYoneticisi(SirketYoneticisi sirketler, KodTabanliSirketIsletimYoneticisi isletim, string motorVerileriKlasoru)
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
            _veri.Surum = 2;
            _veri.Sirketler ??= new(StringComparer.OrdinalIgnoreCase);
            foreach (SirketEkosistemAyarlari ayar in _veri.Sirketler.Values)
            {
                ayar.Urunler ??= new(StringComparer.OrdinalIgnoreCase);
                ayar.OtomatikDuraklatilanHizmetler ??= [];
            }
            await KaydetKilitsizAsync(cancellationToken);
            _baslatildi = true;
            KonsolKayitcisi.Basari("İşletim sistemi, protokol ve aşırı yük ekosistemi hazır.");
        }
        finally { _kilit.Release(); }
    }

    public async Task PazariHazirlaAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            _tick = tickNumarasi;
            Dictionary<string, JsonObject> paneller = new(StringComparer.OrdinalIgnoreCase);
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
                paneller[sirket.SirketKimligi] = await PanelOkuAsync(sirket.SirketKimligi, cancellationToken);

            HashSet<string> aktifProtokoller = AktifProtokolleriBul(paneller);
            List<IsletimSistemiPazarKaydi> sistemler = [];

            // İlk geçiş: işletim sistemi ürünleri doğrulanır ve pazar kurulur.
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
            {
                JsonObject panel = paneller[sirket.SirketKimligi];
                SirketEkosistemAyarlari sirketAyari = Ayarlar(sirket.SirketKimligi);
                Dictionary<string, JsonObject> uygulamalar = UygulamaIndeksi(panel);
                foreach (JsonObject urun in Urunler(panel).Where(x => Normal(Str(x["urunTuru"]), "uygulama") == "isletim-sistemi"))
                {
                    string urunKimligi = Str(urun["urunKimligi"]), uygulamaKimligi = Str(urun["uygulamaKimligi"]);
                    if (string.IsNullOrWhiteSpace(urunKimligi)) continue;
                    uygulamalar.TryGetValue(uygulamaKimligi, out JsonObject? uygulama);
                    UrunDagitimAyari ayar = UrunAyari(sirketAyari, urunKimligi, uygulamaKimligi, "isletim-sistemi");
                    VarsayilanProtokoluUygula(ayar, uygulama, Bool(urun["aktif"]));
                    (ayar.Uyumlu, ayar.UyumDurumu) = IsletimSistemiDogrula(ayar, uygulama, aktifProtokoller);
                    await AktiflikUygulaAsync(sirket.SirketKimligi, urun, ayar, cancellationToken);
                    if (ayar.Uyumlu && ayar.ElleAktifOlmasiIsteniyor && ayar.OtomatikDuraksatmaBitisTicki < _tick)
                    {
                        sistemler.Add(new IsletimSistemiPazarKaydi
                        {
                            SirketKimligi = sirket.SirketKimligi, SirketAdi = sirket.SirketAdi,
                            UrunKimligi = urunKimligi, UygulamaKimligi = uygulamaKimligi,
                            UygulamaAdi = Str(urun["urunAdi"]), Surum = Str(uygulama?["surum"], "1.0"),
                            Protokoller = ProtokolListesi(uygulama, ayar), KullaniciKapasitesi = Int(urun["kullaniciKapasitesi"]),
                            Fiyat = Dec(urun["abonelikUcreti"]), Kalite = Num(urun["urunKalitesi"]),
                            Performans = sirket.PerformansPuani, Guvenlik = sirket.GuvenlikPuani,
                            OperasyonRiski = Num(panel["isletim"]?["operasyonRiski"]), Aktif = true
                        });
                    }
                }
            }
            IsletimSistemiPazarDeposu.Guncelle(sistemler);

            // İkinci geçiş: bütün diğer uygulamalar seçilen OS ve ortak protokole göre doğrulanır.
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
            {
                JsonObject panel = paneller[sirket.SirketKimligi];
                SirketEkosistemAyarlari sirketAyari = Ayarlar(sirket.SirketKimligi);
                Dictionary<string, JsonObject> uygulamalar = UygulamaIndeksi(panel);
                foreach (JsonObject urun in Urunler(panel).Where(x => Normal(Str(x["urunTuru"]), "uygulama") != "isletim-sistemi"))
                {
                    string urunKimligi = Str(urun["urunKimligi"]), uygulamaKimligi = Str(urun["uygulamaKimligi"]), tur = Normal(Str(urun["urunTuru"]), "uygulama");
                    if (string.IsNullOrWhiteSpace(urunKimligi)) continue;
                    uygulamalar.TryGetValue(uygulamaKimligi, out JsonObject? uygulama);
                    UrunDagitimAyari ayar = UrunAyari(sirketAyari, urunKimligi, uygulamaKimligi, tur);
                    VarsayilanProtokoluUygula(ayar, uygulama, Bool(urun["aktif"]));
                    (ayar.Uyumlu, ayar.UyumDurumu) = UygulamaDogrula(ayar, uygulama, aktifProtokoller);
                    await AktiflikUygulaAsync(sirket.SirketKimligi, urun, ayar, cancellationToken);
                }
                AltyapiSinirlari limit = LimitHesapla(sirket, panel);
                await AsiriYukuIsleAsync(sirket, panel, sirketAyari, limit, cancellationToken);
            }
            await KaydetKilitsizAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> DagitimGuncelleAsync(string sirketKimligi, UrunDagitimGuncelleIstegi istek, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            JsonObject panel = await PanelOkuAsync(sirketKimligi, cancellationToken);
            JsonObject? urun = Urunler(panel).FirstOrDefault(x => string.Equals(Str(x["urunKimligi"]), istek.UrunKimligi, StringComparison.OrdinalIgnoreCase));
            if (urun is null) return IslemSonucu.Hata("Ürün bulunamadı.");
            string tur = Normal(Str(urun["urunTuru"]), "uygulama"), uygulamaKimligi = Str(urun["uygulamaKimligi"]);
            UygulamaIndeksi(panel).TryGetValue(uygulamaKimligi, out JsonObject? uygulama);
            UrunDagitimAyari ayar = UrunAyari(Ayarlar(sirketKimligi), istek.UrunKimligi, uygulamaKimligi, tur);
            ayar.IsletimSistemiKimligi = istek.IsletimSistemiKimligi?.Trim() ?? string.Empty;
            ayar.BaglantiProtokoluKimligi = istek.BaglantiProtokoluKimligi?.Trim() ?? string.Empty;
            ayar.KullaniciTarafindanYapilandirildi = true;
            ayar.ElleAktifOlmasiIsteniyor = istek.Aktif;
            HashSet<string> aktifProtokoller = AktifProtokolleriBul(new Dictionary<string, JsonObject> { [sirketKimligi] = panel });
            // Global protokoller diğer şirketlerden gelebilir.
            foreach (SirketKaydi diger in _sirketler.SirketKayitlari.Where(x => !string.Equals(x.SirketKimligi, sirketKimligi, StringComparison.OrdinalIgnoreCase)))
            {
                JsonObject digerPanel = await PanelOkuAsync(diger.SirketKimligi, cancellationToken);
                foreach (string protokol in AktifProtokolleriBul(new Dictionary<string, JsonObject> { [diger.SirketKimligi] = digerPanel })) aktifProtokoller.Add(protokol);
            }
            (ayar.Uyumlu, ayar.UyumDurumu) = tur == "isletim-sistemi"
                ? IsletimSistemiDogrula(ayar, uygulama, aktifProtokoller)
                : UygulamaDogrula(ayar, uygulama, aktifProtokoller);
            await AktiflikUygulaAsync(sirketKimligi, urun, ayar, cancellationToken);
            await KaydetKilitsizAsync(cancellationToken);
            return IslemSonucu.Basari(ayar.Uyumlu
                ? (istek.Aktif ? "Dağıtım uyumlu; ürün yayına alındı." : "Dağıtım kaydedildi; ürün pasif bırakıldı.")
                : "Dağıtım kaydedildi fakat ürün uyumsuz olduğu için pasif: " + ayar.UyumDurumu, ayar);
        }
        finally { _kilit.Release(); }
    }

    public async Task<string> PanelJsonunuZenginlestirAsync(string sirketKimligi, string temelJson, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            JsonObject panel = JsonNode.Parse(temelJson) as JsonObject ?? new();
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            SirketEkosistemAyarlari ayarlar = Ayarlar(sirketKimligi);
            List<object> dagitim = Urunler(panel).Select(u =>
            {
                string kimlik = Str(u["urunKimligi"]);
                return (object)new { urunKimligi = kimlik, ayar = UrunAyari(ayarlar, kimlik, Str(u["uygulamaKimligi"]), Normal(Str(u["urunTuru"]), "uygulama")) };
            }).ToList();
            IReadOnlyList<IsletimSistemiPazarKaydi> sistemler = IsletimSistemiPazarDeposu.Getir();
            panel["ekosistem"] = JsonSerializer.SerializeToNode(new
            {
                altyapiSinirlari = LimitHesapla(sirket, panel),
                dagitimAyarlari = dagitim,
                isletimSistemleri = sistemler,
                musteriIsletimSistemiDagilimi = sistemler.Select(x => new { x.UygulamaKimligi, x.UygulamaAdi, x.SirketAdi, x.AktifMusteriSayisi, x.KullaniciKapasitesi }),
                zorunluKural = "Her müşteri bir işletim sistemi kullanır. Her uygulama seçili işletim sistemi ve ortak aktif protokol olmadan pasif kalır."
            }, JsonAyarlari);
            return panel.ToJsonString(JsonAyarlari);
        }
        finally { _kilit.Release(); }
    }

    private static HashSet<string> AktifProtokolleriBul(IReadOnlyDictionary<string, JsonObject> paneller)
    {
        HashSet<string> sonuc = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonObject panel in paneller.Values)
        {
            foreach (JsonObject kayit in (panel["kodTabanliYayinlar"]?["protokoller"] as JsonArray ?? []).OfType<JsonObject>().Where(x => Bool(x["piyasada"])))
            {
                string teknik = Str(kayit["protokol"]?["protokolKimligi"]), piyasa = Str(kayit["piyasaProtokolKimligi"]);
                if (!string.IsNullOrWhiteSpace(teknik)) sonuc.Add(teknik);
                if (!string.IsNullOrWhiteSpace(piyasa)) sonuc.Add(piyasa);
            }
        }
        return sonuc;
    }

    private static (bool, string) IsletimSistemiDogrula(UrunDagitimAyari ayar, JsonObject? uygulama, IReadOnlySet<string> aktifProtokoller)
    {
        if (string.IsNullOrWhiteSpace(ayar.BaglantiProtokoluKimligi)) return (false, "Aktif bağlantı protokolü seçilmedi.");
        if (!aktifProtokoller.Contains(ayar.BaglantiProtokoluKimligi)) return (false, "Seçilen protokol piyasada aktif değil.");
        IReadOnlyList<string> destek = ProtokolListesi(uygulama, ayar);
        if (destek.Count > 0 && !destek.Contains(ayar.BaglantiProtokoluKimligi, StringComparer.OrdinalIgnoreCase)) return (false, "İşletim sistemi manifesti seçilen protokolü desteklemiyor.");
        return (true, "İşletim sistemi aktif protokol üzerinden müşteri kabul edebilir.");
    }

    private static (bool, string) UygulamaDogrula(UrunDagitimAyari ayar, JsonObject? uygulama, IReadOnlySet<string> aktifProtokoller)
    {
        if (string.IsNullOrWhiteSpace(ayar.IsletimSistemiKimligi)) return (false, "İşletim sistemi seçilmedi.");
        if (string.IsNullOrWhiteSpace(ayar.BaglantiProtokoluKimligi)) return (false, "Bağlantı protokolü seçilmedi.");
        if (!aktifProtokoller.Contains(ayar.BaglantiProtokoluKimligi)) return (false, "Seçilen protokol piyasada aktif değil.");
        IsletimSistemiPazarKaydi? sistem = IsletimSistemiPazarDeposu.Getir().FirstOrDefault(x => string.Equals(x.UygulamaKimligi, ayar.IsletimSistemiKimligi, StringComparison.OrdinalIgnoreCase));
        if (sistem is null || !sistem.Aktif) return (false, "Seçilen işletim sistemi aktif piyasada değil.");
        if (!sistem.Protokoller.Contains(ayar.BaglantiProtokoluKimligi, StringComparer.OrdinalIgnoreCase)) return (false, "İşletim sistemi seçilen protokolü desteklemiyor.");
        IReadOnlyList<string> destek = ProtokolListesi(uygulama, ayar);
        if (destek.Count > 0 && !destek.Contains(ayar.BaglantiProtokoluKimligi, StringComparer.OrdinalIgnoreCase)) return (false, "Uygulama manifesti seçilen protokolü desteklemiyor.");
        return (true, "İşletim sistemi ve protokol bağlantısı geçerli.");
    }

    private static void VarsayilanProtokoluUygula(UrunDagitimAyari ayar, JsonObject? uygulama, bool mevcutAktiflik)
    {
        if (ayar.KullaniciTarafindanYapilandirildi) return;
        ayar.BaglantiProtokoluKimligi = (uygulama?["desteklenenProtokoller"] as JsonArray)?.Select(x => Str(x)).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
        ayar.ElleAktifOlmasiIsteniyor = mevcutAktiflik;
    }

    private async Task AktiflikUygulaAsync(string sirketKimligi, JsonObject urun, UrunDagitimAyari ayar, CancellationToken cancellationToken)
    {
        bool hedef = ayar.Uyumlu && ayar.ElleAktifOlmasiIsteniyor && ayar.OtomatikDuraksatmaBitisTicki < _tick;
        if (Bool(urun["aktif"]) != hedef) await UrunAktifliginiDegistirAsync(sirketKimligi, urun, hedef, cancellationToken);
    }

    private async Task AsiriYukuIsleAsync(SirketKaydi sirket, JsonObject panel, SirketEkosistemAyarlari ayarlar, AltyapiSinirlari limit, CancellationToken cancellationToken)
    {
        if (ayarlar.HizmetDuraksatmaBitisTicki > 0 && ayarlar.HizmetDuraksatmaBitisTicki < _tick && ayarlar.OtomatikDuraklatilanHizmetler.Count > 0)
        {
            foreach (string anahtar in ayarlar.OtomatikDuraklatilanHizmetler.ToList())
            {
                int at = anahtar.LastIndexOf('@'); if (at <= 0) continue;
                await _isletim.HizmetYayinDurumuGuncelleAsync(sirket.SirketKimligi, new HizmetYayinDurumuIstegi { HizmetKimligi = anahtar[..at], HizmetSurumu = anahtar[(at + 1)..], Aktif = true }, cancellationToken);
            }
            ayarlar.OtomatikDuraklatilanHizmetler.Clear();
            ayarlar.HizmetDuraksatmaBitisTicki = 0;
        }

        if (limit.BirlesikYukOrani <= 1)
        {
            ayarlar.ArdisikAsiriYukTicki = Math.Max(0, ayarlar.ArdisikAsiriYukTicki - 1);
            return;
        }

        ayarlar.ArdisikAsiriYukTicki++;
        double siddet = Math.Clamp(limit.BirlesikYukOrani - 1, 0, 2.5);
        sirket.PerformansPuani = Math.Max(0, sirket.PerformansPuani - 0.35 - siddet * 0.9);
        sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - 0.18 - siddet * 0.55);
        sirket.ItibarPuani = Math.Max(0, sirket.ItibarPuani - siddet * 0.25);
        sirket.KuyrukUzunlugu += Math.Max(1, (int)Math.Round((limit.MevcutAktifIs + limit.MevcutAktifKullanici / 100d) * siddet));
        decimal zarar = decimal.Round((2_500m + (decimal)(siddet * 12_000) + limit.MevcutAktifKullanici * 0.7m) * (decimal)(0.7 + _rastgele.NextDouble() * 0.6), 2);
        decimal odenen = Math.Min(Math.Max(0, sirket.Kasa), zarar);
        sirket.Kasa -= odenen;
        sirket.ToplamCeza += zarar - odenen;

        foreach (JsonObject urun in Urunler(panel))
        {
            string kimlik = Str(urun["urunKimligi"]);
            UrunDagitimAyari ayar = UrunAyari(ayarlar, kimlik, Str(urun["uygulamaKimligi"]), Normal(Str(urun["urunTuru"]), "uygulama"));
            double kapasite = Math.Max(1, Int(urun["kullaniciKapasitesi"]));
            double urunYuku = Int(urun["aktifKullaniciSayisi"]) / kapasite + siddet * (0.15 + _rastgele.NextDouble() * 0.25);
            ayar.SonYukOrani = urunYuku;
            ayar.AsiriYukTickSayisi = urunYuku > 1 ? ayar.AsiriYukTickSayisi + 1 : Math.Max(0, ayar.AsiriYukTickSayisi - 1);
            if (ayar.AsiriYukTickSayisi >= 2 && Bool(urun["aktif"]))
            {
                int sure = _rastgele.Next(1, 4) + (urunYuku > 1.5 ? 2 : 0);
                ayar.OtomatikDuraksatmaBitisTicki = _tick + sure;
                ayar.SonDuraksatmaNedeni = $"Aşırı yük %{urunYuku * 100:N0}; otomatik {sure} tick duraksatma.";
                await UrunAktifliginiDegistirAsync(sirket.SirketKimligi, urun, false, cancellationToken);
            }
        }

        if (ayarlar.ArdisikAsiriYukTicki >= 3 && ayarlar.OtomatikDuraklatilanHizmetler.Count == 0)
        {
            foreach (JsonObject h in (panel["kodTabanliYayinlar"]?["hizmetler"] as JsonArray ?? []).OfType<JsonObject>().Where(x => Bool(x["aktif"])))
            {
                string kimlik = Str(h["hizmetKimligi"]), surum = Str(h["hizmetSurumu"]);
                if (string.IsNullOrWhiteSpace(kimlik)) continue;
                await _isletim.HizmetYayinDurumuGuncelleAsync(sirket.SirketKimligi, new HizmetYayinDurumuIstegi { HizmetKimligi = kimlik, HizmetSurumu = surum, Aktif = false }, cancellationToken);
                ayarlar.OtomatikDuraklatilanHizmetler.Add($"{kimlik}@{surum}");
            }
            ayarlar.HizmetDuraksatmaBitisTicki = _tick + _rastgele.Next(1, 4);
            KonsolKayitcisi.Uyari($"AŞIRI YÜK KORUMASI | {sirket.SirketAdi} hizmetleri geçici duraksatıldı | Yük ×{limit.BirlesikYukOrani:N2}");
        }
    }

    private AltyapiSinirlari LimitHesapla(SirketKaydi sirket, JsonObject panel)
    {
        JsonObject yatirim = panel["isletim"]?["yatirimSeviyeleri"] as JsonObject ?? new();
        int cpu = Int(yatirim["cpu"]), ram = Int(yatirim["ram"]), ag = Int(yatirim["ag"]), depolama = Int(yatirim["depolama"]), yedek = Int(yatirim["yedek"]), destek = Int(yatirim["destek"]);
        JsonArray hizmetler = panel["kodTabanliYayinlar"]?["hizmetler"] as JsonArray ?? [];
        List<JsonObject> urunler = Urunler(panel).ToList();
        int toplamHizmet = hizmetler.OfType<JsonObject>().Where(x => Bool(x["aktif"])).Sum(x => Int(x["etkinKapasite"], Int(x["azamiEszamanliIs"])));
        int toplamUrun = urunler.Where(x => Bool(x["aktif"])).Sum(x => Int(x["kullaniciKapasitesi"]));
        int aktifKullanici = urunler.Where(x => Bool(x["aktif"])).Sum(x => Int(x["aktifKullaniciSayisi"]));
        double teknik = (sirket.KodKalitesiPuani * 0.28 + sirket.PerformansPuani * 0.32 + sirket.GuvenlikPuani * 0.20 + sirket.GuvenilirlikPuani * 0.20) / 100d;
        double islemKatsayi = Math.Clamp(0.48 + teknik * 0.32 + cpu * 0.018 + ag * 0.014 + yedek * 0.012, 0.5, 0.98);
        double kullaniciKatsayi = Math.Clamp(0.50 + teknik * 0.22 + ram * 0.017 + depolama * 0.014 + ag * 0.012 + destek * 0.008, 0.5, 0.98);
        int guvenliIslem = Math.Max(1, (int)Math.Floor(toplamHizmet * islemKatsayi));
        int guvenliKullanici = Math.Max(1, (int)Math.Floor(toplamUrun * kullaniciKatsayi));
        double islemYuku = Math.Max((double)sirket.AktifIsSayisi / guvenliIslem, (double)(sirket.AktifIsSayisi + sirket.KuyrukUzunlugu * 0.35) / guvenliIslem);
        double kullaniciYuku = (double)aktifKullanici / guvenliKullanici;
        double gecikme = Math.Clamp(sirket.SonGecikmeMs / 1_500d, 0, 3);
        double birlesik = Math.Max(islemYuku, kullaniciYuku) + gecikme * 0.18 + 0.04 + _rastgele.NextDouble() * 0.12;
        return new AltyapiSinirlari
        {
            ToplamHizmetKapasitesi = toplamHizmet, GuvenliEszamanliIslemSiniri = guvenliIslem,
            MevcutAktifIs = sirket.AktifIsSayisi, KuyrukUzunlugu = sirket.KuyrukUzunlugu,
            ToplamUygulamaKapasitesi = toplamUrun, GuvenliAktifKullaniciSiniri = guvenliKullanici,
            MevcutAktifKullanici = aktifKullanici, IslemYukOrani = islemYuku,
            KullaniciYukOrani = kullaniciYuku, GecikmeBaskisi = gecikme, BirlesikYukOrani = birlesik,
            Durum = birlesik switch { < 0.75 => "rahat", < 0.95 => "yüksek", < 1.2 => "aşırı-yük", _ => "kritik" }
        };
    }

    private async Task UrunAktifliginiDegistirAsync(string sirketKimligi, JsonObject urun, bool aktif, CancellationToken cancellationToken)
    {
        await _isletim.UygulamaGuncelleAsync(sirketKimligi, new UrunGuncelleIstegi
        {
            UrunKimligi = Str(urun["urunKimligi"]), AbonelikUcreti = Dec(urun["abonelikUcreti"]),
            KullanimBasinaUcret = Dec(urun["kullanimBasinaUcret"]), Aktif = aktif
        }, cancellationToken);
    }

    private async Task<JsonObject> PanelOkuAsync(string sirketKimligi, CancellationToken cancellationToken) =>
        JsonNode.Parse(await _isletim.PanelJsonuOlusturAsync(sirketKimligi, cancellationToken)) as JsonObject ?? new();

    private static Dictionary<string, JsonObject> UygulamaIndeksi(JsonObject panel) =>
        (panel["kodTabanliYayinlar"]?["uygulamalar"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(x => x["uygulama"] as JsonObject).Where(x => x is not null && !string.IsNullOrWhiteSpace(Str(x["uygulamaKimligi"])))
            .ToDictionary(x => Str(x!["uygulamaKimligi"]), x => x!, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<JsonObject> Urunler(JsonObject panel) =>
        (panel["isletim"]?["urunler"] as JsonArray ?? []).OfType<JsonObject>();

    private SirketEkosistemAyarlari Ayarlar(string sirketKimligi)
    {
        if (_veri.Sirketler.TryGetValue(sirketKimligi, out SirketEkosistemAyarlari? ayar)) return ayar;
        ayar = new SirketEkosistemAyarlari(); _veri.Sirketler[sirketKimligi] = ayar; return ayar;
    }

    private static UrunDagitimAyari UrunAyari(SirketEkosistemAyarlari sirket, string urunKimligi, string uygulamaKimligi, string tur)
    {
        if (sirket.Urunler.TryGetValue(urunKimligi, out UrunDagitimAyari? ayar)) return ayar;
        ayar = new UrunDagitimAyari { UrunKimligi = urunKimligi, UygulamaKimligi = uygulamaKimligi, UrunTuru = tur };
        sirket.Urunler[urunKimligi] = ayar; return ayar;
    }

    private static IReadOnlyList<string> ProtokolListesi(JsonObject? uygulama, UrunDagitimAyari ayar)
    {
        List<string> liste = (uygulama?["desteklenenProtokoller"] as JsonArray)?.Select(x => Str(x)).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        if (!string.IsNullOrWhiteSpace(ayar.BaglantiProtokoluKimligi) && !liste.Contains(ayar.BaglantiProtokoluKimligi, StringComparer.OrdinalIgnoreCase)) liste.Add(ayar.BaglantiProtokoluKimligi);
        return liste.AsReadOnly();
    }

    private SirketKaydi SirketZorunlu(string kimlik) => _sirketler.SirketKayitlari.FirstOrDefault(x => string.Equals(x.SirketKimligi, kimlik, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("Şirket bulunamadı.");
    private static string Str(JsonNode? n, string d = "") { try { return n?.GetValue<string>() ?? d; } catch { return d; } }
    private static int Int(JsonNode? n, int d = 0) { try { return n?.GetValue<int>() ?? d; } catch { return d; } }
    private static decimal Dec(JsonNode? n, decimal d = 0) { try { return n?.GetValue<decimal>() ?? d; } catch { return d; } }
    private static double Num(JsonNode? n, double d = 0) { try { return n?.GetValue<double>() ?? d; } catch { return d; } }
    private static bool Bool(JsonNode? n) { try { return n?.GetValue<bool>() ?? false; } catch { return false; } }
    private static string Normal(string? x, string d) => string.IsNullOrWhiteSpace(x) ? d : x.Trim().ToLowerInvariant();

    private async Task KaydetKilitsizAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        _veri.GuncellenmeZamani = DateTimeOffset.UtcNow;
        string tmp = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(_veri, JsonAyarlari), new UTF8Encoding(false), cancellationToken);
        File.Move(tmp, _dosyaYolu, true);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_baslatildi) return;
        await _kilit.WaitAsync();
        try { await KaydetKilitsizAsync(CancellationToken.None); }
        finally { _kilit.Release(); _kilit.Dispose(); }
    }
}
