using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class EkosistemDosyasi
{
    public int Surum { get; set; } = 1;
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
        lock (Kilit)
        {
            return _kayitlar.Select(Kopyala).ToList().AsReadOnly();
        }
    }

    public static void Guncelle(IEnumerable<IsletimSistemiPazarKaydi> kayitlar)
    {
        lock (Kilit)
        {
            _kayitlar = kayitlar.Select(k =>
            {
                IsletimSistemiPazarKaydi c = Kopyala(k);
                c.AktifMusteriSayisi = _musteriDagilimi.TryGetValue(c.UygulamaKimligi, out int sayi) ? sayi : 0;
                return c;
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

    private static IsletimSistemiPazarKaydi Kopyala(IsletimSistemiPazarKaydi k) => new()
    {
        SirketKimligi = k.SirketKimligi, SirketAdi = k.SirketAdi, UrunKimligi = k.UrunKimligi,
        UygulamaKimligi = k.UygulamaKimligi, UygulamaAdi = k.UygulamaAdi, Surum = k.Surum,
        Protokoller = k.Protokoller.ToList().AsReadOnly(), KullaniciKapasitesi = k.KullaniciKapasitesi,
        AktifMusteriSayisi = k.AktifMusteriSayisi, Fiyat = k.Fiyat, Kalite = k.Kalite,
        Performans = k.Performans, Guvenlik = k.Guvenlik, OperasyonRiski = k.OperasyonRiski, Aktif = k.Aktif
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
            .Where(x => x.Aktif && x.KullaniciKapasitesi > 0 && x.Protokoller.Count > 0)
            .ToList();
        Dictionary<string, int> dagilim = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IsletimSistemiPazarKaydi> indeks = sistemler.ToDictionary(x => x.UygulamaKimligi, StringComparer.OrdinalIgnoreCase);

        foreach (Musteri musteri in _musteriler.Musteriler.Where(m => m.Aktif))
        {
            bool mevcutGecerli = !string.IsNullOrWhiteSpace(musteri.IsletimSistemiKimligi) &&
                                indeks.TryGetValue(musteri.IsletimSistemiKimligi, out IsletimSistemiPazarKaydi? mevcut);
            if (mevcutGecerli && mevcut is not null)
            {
                dagilim.TryGetValue(mevcut.UygulamaKimligi, out int mevcutSayi);
                if (mevcutSayi >= mevcut.KullaniciKapasitesi) mevcutGecerli = false;
                else
                {
                    double memHedef = Math.Clamp((mevcut.Kalite + mevcut.Performans + mevcut.Guvenlik) / 3d - mevcut.OperasyonRiski * 0.22, 0, 100);
                    musteri.IsletimSistemiMemnuniyeti += (memHedef - musteri.IsletimSistemiMemnuniyeti) * 0.035;
                    double degisimOlasiligi = 0.0015 + Math.Max(0, 58 - musteri.IsletimSistemiMemnuniyeti) / 2_500d + mevcut.OperasyonRiski / 18_000d;
                    if (tickNumarasi - musteri.SonIsletimSistemiDegisimTicki > 5 && _rastgele.NextDouble() < degisimOlasiligi)
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

    private IsletimSistemiPazarKaydi? SistemSec(
        IReadOnlyList<IsletimSistemiPazarKaydi> sistemler,
        IReadOnlyDictionary<string, int> dagilim,
        Musteri musteri)
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
            double agirlik = Math.Max(0.001, teknik * fiyat * risk * Math.Sqrt(bosluk) * (0.85 + _rastgele.NextDouble() * 0.3));
            adaylar.Add((sistem, agirlik));
        }
        if (adaylar.Count == 0) return null;
        double toplam = adaylar.Sum(x => x.Agirlik);
        double secim = _rastgele.NextDouble() * toplam;
        foreach (var aday in adaylar)
        {
            secim -= aday.Agirlik;
            if (secim <= 0) return aday.Sistem;
        }
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
        _sirketler = sirketler;
        _isletim = isletim;
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
            _veri.Sirketler ??= new(StringComparer.OrdinalIgnoreCase);
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
            List<IsletimSistemiPazarKaydi> sistemler = [];
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
            {
                JsonObject panel = await PanelOkuAsync(sirket.SirketKimligi, cancellationToken);
                SirketEkosistemAyarlari sirketAyari = Ayarlar(sirket.SirketKimligi);
                JsonArray urunler = panel["isletim"]?["urunler"] as JsonArray ?? [];
                JsonArray teknikUygulamalar = panel["kodTabanliYayinlar"]?["uygulamalar"] as JsonArray ?? [];
                JsonArray teknikProtokoller = panel["kodTabanliYayinlar"]?["protokoller"] as JsonArray ?? [];
                Dictionary<string, JsonObject> uygulamalar = teknikUygulamalar.OfType<JsonObject>()
                    .Select(x => x["uygulama"] as JsonObject)
                    .Where(x => x is not null && !string.IsNullOrWhiteSpace(Str(x["uygulamaKimligi"])))
                    .ToDictionary(x => Str(x!["uygulamaKimligi"]), x => x!, StringComparer.OrdinalIgnoreCase);

                foreach (JsonObject urun in urunler.OfType<JsonObject>())
                {
                    string urunKimligi = Str(urun["urunKimligi"]);
                    if (string.IsNullOrWhiteSpace(urunKimligi)) continue;
                    string uygulamaKimligi = Str(urun["uygulamaKimligi"]);
                    string tur = Normal(Str(urun["urunTuru"]), "uygulama");
                    UrunDagitimAyari ayar = UrunAyari(sirketAyari, urunKimligi, uygulamaKimligi, tur);
                    uygulamalar.TryGetValue(uygulamaKimligi, out JsonObject? uygulama);
                    if (!ayar.KullaniciTarafindanYapilandirildi)
                    {
                        string manifestProtokolu = (uygulama?["desteklenenProtokoller"] as JsonArray)?.Select(Str).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
                        ayar.BaglantiProtokoluKimligi = manifestProtokolu;
                        ayar.ElleAktifOlmasiIsteniyor = Bool(urun["aktif"]);
                    }
                    (ayar.Uyumlu, ayar.UyumDurumu) = UyumDogrula(sirket, ayar, tur, uygulama, teknikProtokoller);
                    bool aktif = Bool(urun["aktif"]);
                    if ((!ayar.Uyumlu || ayar.OtomatikDuraksatmaBitisTicki >= _tick) && aktif)
                        await UrunAktifliginiDegistirAsync(sirket.SirketKimligi, urun, false, cancellationToken);
                    else if (ayar.Uyumlu && ayar.ElleAktifOlmasiIsteniyor && ayar.OtomatikDuraksatmaBitisTicki < _tick && !aktif)
                        await UrunAktifliginiDegistirAsync(sirket.SirketKimligi, urun, true, cancellationToken);

                    if (tur == "isletim-sistemi" && ayar.Uyumlu && ayar.ElleAktifOlmasiIsteniyor && ayar.OtomatikDuraksatmaBitisTicki < _tick)
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

                AltyapiSinirlari limit = LimitHesapla(sirket, panel);
                await AsiriYukuIsleAsync(sirket, panel, sirketAyari, limit, cancellationToken);
            }
            IsletimSistemiPazarDeposu.Guncelle(sistemler);
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
            JsonObject? urun = (panel["isletim"]?["urunler"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(x => string.Equals(Str(x["urunKimligi"]), istek.UrunKimligi, StringComparison.OrdinalIgnoreCase));
            if (urun is null) return IslemSonucu.Hata("Ürün bulunamadı.");
            string tur = Normal(Str(urun["urunTuru"]), "uygulama");
            string uygulamaKimligi = Str(urun["uygulamaKimligi"]);
            SirketEkosistemAyarlari sirketAyari = Ayarlar(sirketKimligi);
            UrunDagitimAyari ayar = UrunAyari(sirketAyari, istek.UrunKimligi, uygulamaKimligi, tur);
            ayar.IsletimSistemiKimligi = istek.IsletimSistemiKimligi?.Trim() ?? string.Empty;
            ayar.BaglantiProtokoluKimligi = istek.BaglantiProtokoluKimligi?.Trim() ?? string.Empty;
            ayar.KullaniciTarafindanYapilandirildi = true;
            ayar.ElleAktifOlmasiIsteniyor = istek.Aktif;
            JsonObject? teknik = (panel["kodTabanliYayinlar"]?["uygulamalar"] as JsonArray)?.OfType<JsonObject>()
                .Select(x => x["uygulama"] as JsonObject)
                .FirstOrDefault(x => x is not null && string.Equals(Str(x["uygulamaKimligi"]), uygulamaKimligi, StringComparison.OrdinalIgnoreCase));
            JsonArray protokoller = panel["kodTabanliYayinlar"]?["protokoller"] as JsonArray ?? [];
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            (ayar.Uyumlu, ayar.UyumDurumu) = UyumDogrula(sirket, ayar, tur, teknik, protokoller);
            bool hedefAktif = istek.Aktif && ayar.Uyumlu && ayar.OtomatikDuraksatmaBitisTicki < _tick;
            await UrunAktifliginiDegistirAsync(sirketKimligi, urun, hedefAktif, cancellationToken);
            await KaydetKilitsizAsync(cancellationToken);
            return ayar.Uyumlu
                ? IslemSonucu.Basari(hedefAktif ? "İşletim sistemi/protokol ayarı kaydedildi ve ürün yayına alındı." : "Dağıtım ayarı kaydedildi; ürün pasif bırakıldı.", ayar)
                : IslemSonucu.Hata("Dağıtım ayarı kaydedildi fakat ürün aktif edilemedi: " + ayar.UyumDurumu);
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
            AltyapiSinirlari limit = LimitHesapla(sirket, panel);
            IReadOnlyList<IsletimSistemiPazarKaydi> sistemler = IsletimSistemiPazarDeposu.Getir();
            JsonArray urunler = panel["isletim"]?["urunler"] as JsonArray ?? [];
            List<object> dagitim = urunler.OfType<JsonObject>().Select(u =>
            {
                string kimlik = Str(u["urunKimligi"]);
                UrunDagitimAyari ayar = UrunAyari(ayarlar, kimlik, Str(u["uygulamaKimligi"]), Normal(Str(u["urunTuru"]), "uygulama"));
                return (object)new { urunKimligi = kimlik, ayar };
            }).ToList();
            panel["ekosistem"] = JsonSerializer.SerializeToNode(new
            {
                altyapiSinirlari = limit,
                dagitimAyarlari = dagitim,
                isletimSistemleri = sistemler,
                musteriIsletimSistemiDagilimi = sistemler.Select(x => new { x.UygulamaKimligi, x.UygulamaAdi, x.SirketAdi, x.AktifMusteriSayisi, x.KullaniciKapasitesi }),
                zorunluKural = "Her müşteri bir işletim sistemi kullanır. Her uygulama seçili işletim sistemi ve uyumlu aktif protokol olmadan pasif kalır."
            }, JsonAyarlari);
            return panel.ToJsonString(JsonAyarlari);
        }
        finally { _kilit.Release(); }
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

        JsonArray urunler = panel["isletim"]?["urunler"] as JsonArray ?? [];
        foreach (JsonObject urun in urunler.OfType<JsonObject>())
        {
            string kimlik = Str(urun["urunKimligi"]);
            UrunDagitimAyari ayar = UrunAyari(ayarlar, kimlik, Str(urun["uygulamaKimligi"]), Normal(Str(urun["urunTuru"]), "uygulama"));
            double kapasite = Math.Max(1, Int(urun["kullaniciKapasitesi"]));
            double urunYuku = Int(urun["aktifKullaniciSayisi"]) / kapasite + siddet * (0.15 + _rastgele.NextDouble() * 0.25);
            ayar.SonYukOrani = urunYuku;
            if (urunYuku > 1) ayar.AsiriYukTickSayisi++; else ayar.AsiriYukTickSayisi = Math.Max(0, ayar.AsiriYukTickSayisi - 1);
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
            foreach (JsonObject h in (panel["kodTabanliYayinlar"]?["hizmetler"] as JsonArray ?? []).OfType<JsonObject>().Where(h => Bool(h["aktif"])))
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

    private (bool Gecerli, string Aciklama) UyumDogrula(SirketKaydi sirket, UrunDagitimAyari ayar, string tur, JsonObject? uygulama, JsonArray teknikProtokoller)
    {
        if (string.IsNullOrWhiteSpace(ayar.BaglantiProtokoluKimligi)) return (false, "Aktif bağlantı protokolü seçilmedi.");
        JsonObject? teknikProtokol = teknikProtokoller.OfType<JsonObject>().FirstOrDefault(p =>
            string.Equals(Str(p["protokol"]?["protokolKimligi"]), ayar.BaglantiProtokoluKimligi, StringComparison.OrdinalIgnoreCase));
        bool kendiProtokolu = teknikProtokol is not null && Bool(teknikProtokol["piyasada"]);
        bool piyasaProtokolu = false;
        foreach (SirketKaydi diger in _sirketler.SirketKayitlari)
        {
            JsonObject? panel = null;
            try { panel = JsonNode.Parse(_isletim.PanelJsonuOlusturAsync(diger.SirketKimligi, CancellationToken.None).GetAwaiter().GetResult()) as JsonObject; } catch { }
            if ((panel?["kodTabanliYayinlar"]?["protokoller"] as JsonArray)?.OfType<JsonObject>().Any(p =>
                string.Equals(Str(p["protokol"]?["protokolKimligi"]), ayar.BaglantiProtokoluKimligi, StringComparison.OrdinalIgnoreCase) && Bool(p["piyasada"])) == true)
            {
                piyasaProtokolu = true;
                break;
            }
        }
        if (!kendiProtokolu && !piyasaProtokolu) return (false, "Seçilen protokol piyasada aktif değil.");

        List<string> desteklenen = ProtokolListesi(uygulama, ayar).ToList();
        if (desteklenen.Count > 0 && !desteklenen.Contains(ayar.BaglantiProtokoluKimligi, StringComparer.OrdinalIgnoreCase))
            return (false, "Ürün manifesti seçilen protokolü desteklemiyor.");

        if (tur == "isletim-sistemi") return (true, "İşletim sistemi aktif protokol üzerinden müşteri kabul edebilir.");
        if (string.IsNullOrWhiteSpace(ayar.IsletimSistemiKimligi)) return (false, "İşletim sistemi seçilmedi.");
        IsletimSistemiPazarKaydi? sistem = IsletimSistemiPazarDeposu.Getir().FirstOrDefault(x =>
            string.Equals(x.UygulamaKimligi, ayar.IsletimSistemiKimligi, StringComparison.OrdinalIgnoreCase));
        if (sistem is null || !sistem.Aktif) return (false, "Seçilen işletim sistemi aktif piyasada değil.");
        if (!sistem.Protokoller.Contains(ayar.BaglantiProtokoluKimligi, StringComparer.OrdinalIgnoreCase))
            return (false, "İşletim sistemi seçilen bağlantı protokolünü desteklemiyor.");
        return (true, "İşletim sistemi ve protokol bağlantısı geçerli.");
    }

    private AltyapiSinirlari LimitHesapla(SirketKaydi sirket, JsonObject panel)
    {
        JsonObject yatirim = panel["isletim"]?["yatirimSeviyeleri"] as JsonObject ?? new();
        int cpu = Int(yatirim["cpu"]), ram = Int(yatirim["ram"]), ag = Int(yatirim["ag"]), depolama = Int(yatirim["depolama"]), yedek = Int(yatirim["yedek"]), destek = Int(yatirim["destek"]);
        JsonArray hizmetler = panel["kodTabanliYayinlar"]?["hizmetler"] as JsonArray ?? [];
        JsonArray urunler = panel["isletim"]?["urunler"] as JsonArray ?? [];
        int toplamHizmet = hizmetler.OfType<JsonObject>().Where(h => Bool(h["aktif"])).Sum(h => Int(h["etkinKapasite"], Int(h["azamiEszamanliIs"])));
        int toplamUrun = urunler.OfType<JsonObject>().Where(u => Bool(u["aktif"])).Sum(u => Int(u["kullaniciKapasitesi"]));
        int aktifKullanici = urunler.OfType<JsonObject>().Where(u => Bool(u["aktif"])).Sum(u => Int(u["aktifKullaniciSayisi"]));
        double teknik = (sirket.KodKalitesiPuani * 0.28 + sirket.PerformansPuani * 0.32 + sirket.GuvenlikPuani * 0.20 + sirket.GuvenilirlikPuani * 0.20) / 100d;
        double islemKatsayi = Math.Clamp(0.48 + teknik * 0.32 + cpu * 0.018 + ag * 0.014 + yedek * 0.012, 0.5, 0.98);
        double kullaniciKatsayi = Math.Clamp(0.50 + teknik * 0.22 + ram * 0.017 + depolama * 0.014 + ag * 0.012 + destek * 0.008, 0.5, 0.98);
        int guvenliIslem = Math.Max(1, (int)Math.Floor(toplamHizmet * islemKatsayi));
        int guvenliKullanici = Math.Max(1, (int)Math.Floor(toplamUrun * kullaniciKatsayi));
        double islemYuku = Math.Max((double)sirket.AktifIsSayisi / guvenliIslem, (double)(sirket.AktifIsSayisi + sirket.KuyrukUzunlugu * 0.35) / guvenliIslem);
        double kullaniciYuku = (double)aktifKullanici / guvenliKullanici;
        double gecikme = Math.Clamp(sirket.SonGecikmeMs / 1_500d, 0, 3);
        double burst = 0.04 + _rastgele.NextDouble() * 0.12;
        double birlesik = Math.Max(islemYuku, kullaniciYuku) + gecikme * 0.18 + burst;
        string durum = birlesik switch { < 0.75 => "rahat", < 0.95 => "yüksek", < 1.2 => "aşırı-yük", _ => "kritik" };
        return new AltyapiSinirlari
        {
            ToplamHizmetKapasitesi = toplamHizmet, GuvenliEszamanliIslemSiniri = guvenliIslem,
            MevcutAktifIs = sirket.AktifIsSayisi, KuyrukUzunlugu = sirket.KuyrukUzunlugu,
            ToplamUygulamaKapasitesi = toplamUrun, GuvenliAktifKullaniciSiniri = guvenliKullanici,
            MevcutAktifKullanici = aktifKullanici, IslemYukOrani = islemYuku,
            KullaniciYukOrani = kullaniciYuku, GecikmeBaskisi = gecikme, BirlesikYukOrani = birlesik, Durum = durum
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
        List<string> liste = (uygulama?["desteklenenProtokoller"] as JsonArray)?.Select(Str).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        if (!string.IsNullOrWhiteSpace(ayar.BaglantiProtokoluKimligi) && !liste.Contains(ayar.BaglantiProtokoluKimligi, StringComparer.OrdinalIgnoreCase)) liste.Add(ayar.BaglantiProtokoluKimligi);
        return liste.AsReadOnly();
    }

    private SirketKaydi SirketZorunlu(string kimlik) => _sirketler.SirketKayitlari.FirstOrDefault(s => string.Equals(s.SirketKimligi, kimlik, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("Şirket bulunamadı.");
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
