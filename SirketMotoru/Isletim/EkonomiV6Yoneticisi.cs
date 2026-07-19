using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SirketMotoru.Hizmetler;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Protokol;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class V6HaberKaydi
{
    public string HaberKimligi { get; set; } = string.Empty;
    public long TickNumarasi { get; set; }
    public string Baslik { get; set; } = string.Empty;
    public string Ozet { get; set; } = string.Empty;
    public string Tur { get; set; } = "piyasa";
    public string Onem { get; set; } = "normal";
    public string SirketKimligi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public decimal FinansalEtki { get; set; }
    public DateTimeOffset Zaman { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class V6TickFinansKaydi
{
    public long TickNumarasi { get; set; }
    public decimal TickNetGeliri { get; set; }
    public decimal Kasa { get; set; }
    public decimal SirketDegeri { get; set; }
    public int AktifKullanici { get; set; }
    public int GercekKapasite { get; set; }
    public int KullanilanKapasite { get; set; }
}

public sealed class V6YatirimSureci
{
    public string YatirimTuru { get; set; } = string.Empty;
    public int HedefSeviye { get; set; }
    public long BaslangicTicki { get; set; }
    public long BitisTicki { get; set; }
    public string Durum { get; set; } = "bekliyor";
    public string GerekliHizmet { get; set; } = string.Empty;
}

public sealed class V6SirketDurumu
{
    public string SirketKimligi { get; set; } = string.Empty;
    public Dictionary<string, int> TamamlananYatirimSeviyeleri { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<V6YatirimSureci> YatirimSurecleri { get; set; } = [];
    public Dictionary<string, int> IstenenHizmetTahsisleri { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> IstenenUygulamaTahsisleri { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public decimal OncekiNetGelir { get; set; }
    public int GercekKapasiteBirimi { get; set; }
    public int TahsisEdilenKapasiteBirimi { get; set; }
    public int KullanilanKapasiteBirimi { get; set; }
    public double KapasiteDolulukOrani { get; set; }
    public List<V6TickFinansKaydi> TickGecmisi { get; set; } = [];
}

public sealed class V6UygulamaPazarKaydi
{
    public string SirketKimligi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public string UygulamaKimligi { get; set; } = string.Empty;
    public string UrunKimligi { get; set; } = string.Empty;
    public string UygulamaAdi { get; set; } = string.Empty;
    public string Kategori { get; set; } = string.Empty;
    public string UrunTuru { get; set; } = "uygulama";
    public bool Aktif { get; set; }
    public int AktifKullanici { get; set; }
    public int TahsisKapasitesi { get; set; }
    public decimal Fiyat { get; set; }
    public double HizmetKalitePuani { get; set; }
    public bool KategoriGecerli { get; set; }
    public IReadOnlyList<string> EksikHizmetler { get; set; } = [];
    public IReadOnlyList<string> DesteklenenPlatformlar { get; set; } = [];
}

public sealed class V6KategoriPazarPayi
{
    public string Kategori { get; set; } = string.Empty;
    public int ToplamKullanici { get; set; }
    public List<V6KategoriUygulamaPayi> Uygulamalar { get; set; } = [];
}

public sealed class V6KategoriUygulamaPayi
{
    public string UygulamaKimligi { get; set; } = string.Empty;
    public string UygulamaAdi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public int Kullanici { get; set; }
    public double PayYuzdesi { get; set; }
}

public sealed class V6MusteriCvOzeti
{
    public string MusteriKimligi { get; set; } = string.Empty;
    public string MusteriAdi { get; set; } = string.Empty;
    public string MusteriTuru { get; set; } = string.Empty;
    public string MeslekProfili { get; set; } = string.Empty;
    public string GelirSegmenti { get; set; } = string.Empty;
    public string IsletimSistemiKimligi { get; set; } = string.Empty;
    public IReadOnlyList<string> Uygulamalar { get; set; } = [];
    public decimal ToplamHarcama { get; set; }
    public int BasariliIs { get; set; }
    public int BasarisizIs { get; set; }
}

public sealed class V6PanoDurumu
{
    public long SonTick { get; set; }
    public DateTimeOffset GuncellenmeZamani { get; set; } = DateTimeOffset.UtcNow;
    public List<V6SirketDurumu> Sirketler { get; set; } = [];
    public List<V6UygulamaPazarKaydi> Uygulamalar { get; set; } = [];
    public List<V6KategoriPazarPayi> KategoriPazarPaylari { get; set; } = [];
    public List<V6HaberKaydi> Haberler { get; set; } = [];
    public List<V6MusteriCvOzeti> MusteriCvOrnekleri { get; set; } = [];
}

public static class V6PanoDeposu
{
    private static readonly object Kilit = new();
    private static V6PanoDurumu _durum = new();

    public static void Guncelle(V6PanoDurumu durum)
    {
        lock (Kilit) _durum = Kopyala(durum);
    }

    public static V6PanoDurumu Getir()
    {
        lock (Kilit) return Kopyala(_durum);
    }

    private static V6PanoDurumu Kopyala(V6PanoDurumu kaynak)
    {
        string json = JsonSerializer.Serialize(kaynak);
        return JsonSerializer.Deserialize<V6PanoDurumu>(json) ?? new();
    }
}

public sealed class EkonomiV6Yoneticisi : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, string> YatirimOnKosullari =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cpu"] = "isletim.kaynak-ata",
            ["ram"] = "isletim.surec-listele",
            ["ag"] = "isletim.ag-yapilandir",
            ["depolama"] = "dosya.yukle",
            ["guvenlik"] = "guvenlik.saldiri-tespit",
            ["yedek"] = "veritabani.yedek-al",
            ["destek"] = "bildirim.gonder",
            ["pazarlama"] = "analitik.kullanici-yolu",
            ["satis"] = "ticaret.siparis-olustur",
            ["arge"] = "gelistirme.test-calistir"
        };

    private readonly SirketYoneticisi _sirketler;
    private readonly MusteriYoneticisi _musteriler;
    private readonly KodTabanliSirketIsletimYoneticisi _isletim;
    private readonly string _dosyaYolu;
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private readonly Random _rastgele = new(20260723);
    private readonly object _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _temelKilitAlani;
    private readonly MethodInfo _kaydetMetodu;
    private V6PanoDurumu _durum = new();
    private bool _baslatildi;

    public EkonomiV6Yoneticisi(
        SirketYoneticisi sirketler,
        MusteriYoneticisi musteriler,
        KodTabanliSirketIsletimYoneticisi isletim,
        string motorVerileriKlasoru)
    {
        _sirketler = sirketler;
        _musteriler = musteriler;
        _isletim = isletim;
        _dosyaYolu = Path.Combine(Path.GetFullPath(motorVerileriKlasoru), "ekonomi-v6.json");

        FieldInfo temelAlani = typeof(KodTabanliSirketIsletimYoneticisi).GetField("_temel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V6 işletim köprüsü kurulamadı.");
        _temel = temelAlani.GetValue(_isletim) ?? throw new InvalidOperationException("V6 temel işletim yöneticisi boş.");
        Type tur = _temel.GetType();
        _veriAlani = tur.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V6 işletim verisi alanı bulunamadı.");
        _temelKilitAlani = tur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V6 işletim kilidi bulunamadı.");
        _kaydetMetodu = tur.GetMethod("TumunuKaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V6 işletim kayıt metodu bulunamadı.");
        YatirimSeviyesiSiniriniKaldir();
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
                _durum = JsonSerializer.Deserialize<V6PanoDurumu>(json, JsonAyarlari) ?? new();
            }
            _durum.Sirketler ??= [];
            _durum.Haberler ??= [];
            foreach (SirketKaydi s in _sirketler.SirketKayitlari) _ = SirketDurumu(s.SirketKimligi);
            MusteriCvleriniTamamla();
            V6PanoDeposu.Guncelle(_durum);
            await KaydetAsync(cancellationToken);
            _baslatildi = true;
            KonsolKayitcisi.Basari("V6 ekonomi hazır | Tek kapasite havuzu, 200 kategori, gecikmeli sınırsız yatırım ve haber bülteni aktif.");
        }
        finally { _kilit.Release(); }
    }

    public async Task TickOncesiAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            _durum.SonTick = tickNumarasi;
            List<V6UygulamaPazarKaydi> uygulamaPazari = [];
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
            {
                JsonObject panel = PanelOku(sirket.SirketKimligi, cancellationToken);
                V6SirketDurumu v6 = SirketDurumu(sirket.SirketKimligi);
                YatirimSurecleriniIsle(sirket, panel, v6, tickNumarasi);
                await UygulamalariDogrulaAsync(sirket, panel, v6, uygulamaPazari, tickNumarasi, cancellationToken);
                await KapasiteyiUygulaAsync(sirket, panel, v6, uygulamaPazari, tickNumarasi, cancellationToken);
            }
            _durum.Uygulamalar = uygulamaPazari;
            _durum.KategoriPazarPaylari = PazarPaylariniHesapla(uygulamaPazari);
            _durum.GuncellenmeZamani = DateTimeOffset.UtcNow;
            V6PanoDeposu.Guncelle(_durum);
        }
        finally { _kilit.Release(); }
    }

    public async Task TickSonuAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            MusteriUygulamalariniGuncelle(tickNumarasi);
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
            {
                V6SirketDurumu v6 = SirketDurumu(sirket.SirketKimligi);
                decimal tickGeliri = sirket.NetGelir - v6.OncekiNetGelir;
                v6.OncekiNetGelir = sirket.NetGelir;
                int aktif = _durum.Uygulamalar.Where(x => x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase) && x.Aktif).Sum(x => x.AktifKullanici);
                decimal deger = 0;
                try
                {
                    JsonObject panel = PanelOku(sirket.SirketKimligi, cancellationToken);
                    deger = Dec(panel["isletim"]?["sirketDegeri"]);
                }
                catch { }
                v6.TickGecmisi.Add(new V6TickFinansKaydi
                {
                    TickNumarasi = tickNumarasi,
                    TickNetGeliri = tickGeliri,
                    Kasa = sirket.Kasa,
                    SirketDegeri = deger,
                    AktifKullanici = aktif,
                    GercekKapasite = v6.GercekKapasiteBirimi,
                    KullanilanKapasite = v6.KullanilanKapasiteBirimi
                });
                if (v6.TickGecmisi.Count > 180) v6.TickGecmisi.RemoveRange(0, v6.TickGecmisi.Count - 180);
                if (Math.Abs(tickGeliri) >= 10_000)
                    HaberEkle(tickNumarasi, sirket, tickGeliri >= 0 ? "Gelir sıçraması" : "Finansal daralma",
                        $"{sirket.SirketAdi} bir önceki tickte net {tickGeliri:N2} TL sonuç yazdı.", "finans", tickGeliri >= 0 ? "iyi" : "kritik", tickGeliri);
            }
            _durum.MusteriCvOrnekleri = _musteriler.Musteriler.Take(250).Select(CvOzeti).ToList();
            _durum.GuncellenmeZamani = DateTimeOffset.UtcNow;
            V6PanoDeposu.Guncelle(_durum);
            await KaydetAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    private void YatirimSurecleriniIsle(SirketKaydi sirket, JsonObject panel, V6SirketDurumu v6, long tick)
    {
        JsonObject seviyeler = panel["isletim"]?["yatirimSeviyeleri"] as JsonObject ?? new();
        HashSet<string> aktifHizmetler = sirket.Hizmetler.Where(x => x.Aktif).Select(x => x.HizmetKimligi).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach ((string tur, string gerekli) in YatirimOnKosullari)
        {
            int satinAlinan = Int(seviyeler[tur]);
            v6.TamamlananYatirimSeviyeleri.TryGetValue(tur, out int tamamlanan);
            V6YatirimSureci? surec = v6.YatirimSurecleri.FirstOrDefault(x => x.YatirimTuru.Equals(tur, StringComparison.OrdinalIgnoreCase) && x.Durum == "insaat");
            if (surec is not null && tick >= surec.BitisTicki)
            {
                v6.TamamlananYatirimSeviyeleri[tur] = Math.Max(tamamlanan, surec.HedefSeviye);
                surec.Durum = "tamamlandi";
                HaberEkle(tick, sirket, "Altyapı yatırımı tamamlandı", $"{tur} yatırımı seviye {surec.HedefSeviye} olarak devreye alındı.", "yatirim", "iyi", 0);
                tamamlanan = surec.HedefSeviye;
                surec = null;
            }
            if (satinAlinan <= tamamlanan || surec is not null) continue;
            if (!aktifHizmetler.Contains(gerekli))
            {
                if (!v6.YatirimSurecleri.Any(x => x.YatirimTuru == tur && x.HedefSeviye == tamamlanan + 1 && x.Durum == "bloke"))
                {
                    v6.YatirimSurecleri.Add(new V6YatirimSureci { YatirimTuru = tur, HedefSeviye = tamamlanan + 1, BaslangicTicki = tick, BitisTicki = 0, Durum = "bloke", GerekliHizmet = gerekli });
                    HaberEkle(tick, sirket, "Yatırım teknik ön koşula takıldı", $"{tur} seviye {tamamlanan + 1}, {gerekli} hizmeti olmadığı için gerçek kapasiteye eklenmedi.", "yatirim", "uyari", 0);
                }
                continue;
            }
            v6.YatirimSurecleri.RemoveAll(x => x.YatirimTuru == tur && x.Durum == "bloke");
            int hedef = tamamlanan + 1;
            long sure = 2 + (long)Math.Ceiling(Math.Pow(hedef, 1.18) / 2.0);
            v6.YatirimSurecleri.Add(new V6YatirimSureci { YatirimTuru = tur, HedefSeviye = hedef, BaslangicTicki = tick, BitisTicki = tick + sure, Durum = "insaat", GerekliHizmet = gerekli });
            HaberEkle(tick, sirket, "Yeni yatırım inşaatı başladı", $"{tur} seviye {hedef}, {sure} tick sonra kapasiteye katılacak.", "yatirim", "normal", 0);
        }
        if (v6.YatirimSurecleri.Count > 300) v6.YatirimSurecleri = v6.YatirimSurecleri.TakeLast(300).ToList();
    }

    private async Task UygulamalariDogrulaAsync(
        SirketKaydi sirket,
        JsonObject panel,
        V6SirketDurumu v6,
        List<V6UygulamaPazarKaydi> pazar,
        long tick,
        CancellationToken cancellationToken)
    {
        JsonArray urunler = panel["isletim"]?["urunler"] as JsonArray ?? [];
        Dictionary<string, JsonObject> urunIndeksi = urunler.OfType<JsonObject>()
            .Where(x => !string.IsNullOrWhiteSpace(Str(x["uygulamaKimligi"])))
            .GroupBy(x => Str(x["uygulamaKimligi"]), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
        foreach (SunulanUygulama uygulama in SunucuYayinManifestDeposu.Getir(sirket.SirketKimligi).Uygulamalar)
        {
            UygulamaKategoriDogrulamaSonucu d = StandartKatalogV6.UygulamayiDogrula(uygulama, sirket);
            urunIndeksi.TryGetValue(uygulama.UygulamaKimligi, out JsonObject? urun);
            string urunKimligi = Str(urun?["urunKimligi"]);
            int istenen = Math.Max(0, Int(urun?["kullaniciKapasitesi"]));
            if (!string.IsNullOrWhiteSpace(urunKimligi))
            {
                v6.IstenenUygulamaTahsisleri.TryGetValue(urunKimligi, out int eski);
                v6.IstenenUygulamaTahsisleri[urunKimligi] = Math.Max(eski, istenen);
            }
            bool aktif = Bool(urun?["aktif"]);
            if (!d.Gecerli && aktif && urun is not null)
            {
                await _isletim.UygulamaGuncelleAsync(sirket.SirketKimligi, new UrunGuncelleIstegi
                {
                    UrunKimligi = urunKimligi,
                    AbonelikUcreti = Dec(urun["abonelikUcreti"]),
                    KullanimBasinaUcret = Dec(urun["kullanimBasinaUcret"]),
                    Aktif = false
                }, cancellationToken);
                aktif = false;
                HaberEkle(tick, sirket, "Uygulama standart dışı kaldı", $"{uygulama.UygulamaAdi}, eksik zorunlu hizmetler nedeniyle pasifleştirildi: {string.Join(", ", d.EksikZorunluHizmetler)}", "uygulama", "kritik", 0);
            }
            pazar.Add(new V6UygulamaPazarKaydi
            {
                SirketKimligi = sirket.SirketKimligi,
                SirketAdi = sirket.SirketAdi,
                UygulamaKimligi = uygulama.UygulamaKimligi,
                UrunKimligi = urunKimligi,
                UygulamaAdi = uygulama.UygulamaAdi,
                Kategori = uygulama.Kategori,
                UrunTuru = uygulama.UrunTuru,
                Aktif = aktif && d.Gecerli,
                AktifKullanici = Int(urun?["aktifKullaniciSayisi"]),
                TahsisKapasitesi = istenen,
                Fiyat = Dec(urun?["abonelikUcreti"]),
                HizmetKalitePuani = d.HizmetKalitePuani,
                KategoriGecerli = d.Gecerli,
                EksikHizmetler = d.EksikZorunluHizmetler,
                DesteklenenPlatformlar = uygulama.DesteklenenPlatformlar
            });
        }
    }

    private async Task KapasiteyiUygulaAsync(
        SirketKaydi sirket,
        JsonObject panel,
        V6SirketDurumu v6,
        List<V6UygulamaPazarKaydi> uygulamalar,
        long tick,
        CancellationToken cancellationToken)
    {
        JsonArray hizmetler = panel["kodTabanliYayinlar"]?["hizmetler"] as JsonArray ?? [];
        int bazToplam = 0;
        foreach (JsonObject h in hizmetler.OfType<JsonObject>())
        {
            string anahtar = $"{Str(h["hizmetKimligi"])}@{Str(h["hizmetSurumu"])}";
            int baz = Math.Max(1, Int(h["bazKapasite"]));
            int istenen = Math.Max(baz, Int(h["etkinKapasite"]));
            bazToplam += baz;
            v6.IstenenHizmetTahsisleri.TryGetValue(anahtar, out int eski);
            v6.IstenenHizmetTahsisleri[anahtar] = Math.Max(eski, istenen);
        }
        int Seviye(string tur) => v6.TamamlananYatirimSeviyeleri.TryGetValue(tur, out int x) ? x : 0;
        double YatirimEtki(string tur, double katsayi) => Math.Pow(Seviye(tur), 1.12) * katsayi;
        int havuz = Math.Max(120, (int)Math.Round(
            100 + bazToplam * 7.5 +
            YatirimEtki("cpu", 125) + YatirimEtki("ram", 95) +
            YatirimEtki("ag", 82) + YatirimEtki("depolama", 58) +
            YatirimEtki("yedek", 70) + YatirimEtki("guvenlik", 35)));

        int hizmetTalebi = v6.IstenenHizmetTahsisleri.Values.Sum(x => x * 6);
        int uygulamaTalebi = 0;
        foreach (V6UygulamaPazarKaydi u in uygulamalar.Where(x => x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase)))
        {
            if (!StandartKatalogV6.KategoriBul(u.Kategori, out UygulamaKategoriTanimi? kategori) || kategori is null) continue;
            int istenen = v6.IstenenUygulamaTahsisleri.TryGetValue(u.UrunKimligi, out int x) ? x : u.TahsisKapasitesi;
            uygulamaTalebi += (int)Math.Ceiling(kategori.TabanKapasiteTuketimi + istenen * kategori.KullaniciBasinaKapasiteTuketimi);
        }
        int tahsisTalebi = hizmetTalebi + uygulamaTalebi;
        double tahsisCarpani = tahsisTalebi <= 0 ? 1 : Math.Min(1, (double)havuz / tahsisTalebi);

        foreach (SunulanHizmet h in sirket.Hizmetler)
        {
            string anahtar = $"{h.HizmetKimligi}@{h.HizmetSurumu}";
            if (!v6.IstenenHizmetTahsisleri.TryGetValue(anahtar, out int istenen)) continue;
            h.AzamiEszamanliIs = Math.Max(1, (int)Math.Floor(istenen * tahsisCarpani));
        }

        int aktifKullanici = uygulamalar.Where(x => x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase) && x.Aktif).Sum(x => x.AktifKullanici);
        int kullanim = sirket.AktifIsSayisi * 8 + sirket.KuyrukUzunlugu * 4 + (int)Math.Ceiling(aktifKullanici * 0.006);
        v6.GercekKapasiteBirimi = havuz;
        v6.TahsisEdilenKapasiteBirimi = Math.Min(havuz, tahsisTalebi);
        v6.KullanilanKapasiteBirimi = kullanim;
        v6.KapasiteDolulukOrani = havuz <= 0 ? 0 : (double)kullanim / havuz;

        if (kullanim > havuz)
        {
            double asim = (double)kullanim / havuz;
            sirket.PerformansPuani = Math.Max(0, sirket.PerformansPuani - Math.Min(4.5, (asim - 1) * 2.2));
            sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - Math.Min(3.5, (asim - 1) * 1.6));
            HaberEkle(tick, sirket, "Gerçek kapasite aşıldı", $"Kullanım {kullanim}, fiziksel havuz {havuz}. Hizmetler ve uygulamalar duraksama riski altında.", "kapasite", asim > 1.5 ? "kritik" : "uyari", 0);
            foreach (V6UygulamaPazarKaydi u in uygulamalar.Where(x => x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase) && x.Aktif && x.AktifKullanici > Math.Max(10, x.TahsisKapasitesi * tahsisCarpani)))
            {
                await _isletim.UygulamaGuncelleAsync(sirket.SirketKimligi, new UrunGuncelleIstegi
                {
                    UrunKimligi = u.UrunKimligi,
                    AbonelikUcreti = u.Fiyat,
                    KullanimBasinaUcret = 0,
                    Aktif = false
                }, cancellationToken);
                u.Aktif = false;
            }
        }
        await TemelVeriyiKaydetAsync(cancellationToken);
    }

    private void MusteriUygulamalariniGuncelle(long tick)
    {
        List<V6UygulamaPazarKaydi> aktifler = _durum.Uygulamalar.Where(x => x.Aktif && x.KategoriGecerli).ToList();
        Dictionary<string, V6UygulamaPazarKaydi> indeks = aktifler.ToDictionary(x => x.UygulamaKimligi, StringComparer.OrdinalIgnoreCase);
        foreach (Musteri m in _musteriler.Musteriler.Where(x => x.Aktif))
        {
            CvVarsayilanlariniUygula(m);
            m.KullandigiUygulamalar.RemoveAll(id => !indeks.TryGetValue(id, out V6UygulamaPazarKaydi? u) || !PlatformUyumlu(m, u));
            int hedef = m.MusteriTuru.ToString().Contains("Kurumsal", StringComparison.OrdinalIgnoreCase) ? 8 : 5;
            if (tick % 3 == 0 && m.KullandigiUygulamalar.Count < hedef)
            {
                List<V6UygulamaPazarKaydi> adaylar = aktifler.Where(x => PlatformUyumlu(m, x) && !m.KullandigiUygulamalar.Contains(x.UygulamaKimligi, StringComparer.OrdinalIgnoreCase)).ToList();
                while (adaylar.Count > 0 && m.KullandigiUygulamalar.Count < hedef)
                {
                    double toplam = adaylar.Sum(x => Math.Max(0.01, (x.AktifKullanici + 10d) * (0.4 + x.HizmetKalitePuani / 100d) / (1 + (double)x.Fiyat / Math.Max(10, (double)m.TickBasinaHarcamaButcesi))));
                    double secim = _rastgele.NextDouble() * toplam;
                    int idx = adaylar.Count - 1;
                    for (int i = 0; i < adaylar.Count; i++)
                    {
                        secim -= Math.Max(0.01, (adaylar[i].AktifKullanici + 10d) * (0.4 + adaylar[i].HizmetKalitePuani / 100d) / (1 + (double)adaylar[i].Fiyat / Math.Max(10, (double)m.TickBasinaHarcamaButcesi)));
                        if (secim <= 0) { idx = i; break; }
                    }
                    V6UygulamaPazarKaydi secilen = adaylar[idx];
                    adaylar.RemoveAt(idx);
                    m.KullandigiUygulamalar.Add(secilen.UygulamaKimligi);
                    m.UygulamaMemnuniyetleri[secilen.UygulamaKimligi] = 50;
                    m.ToplamUygulamaDegisimSayisi++;
                    m.SonUygulamaDegisimTicki = tick;
                }
            }
            if (m.KullandigiUygulamalar.Count > 0)
            {
                string kullanilan = m.KullandigiUygulamalar[_rastgele.Next(m.KullandigiUygulamalar.Count)];
                m.UygulamaKullanimSayilari.TryGetValue(kullanilan, out int sayi);
                m.UygulamaKullanimSayilari[kullanilan] = sayi + 1;
            }
        }
    }

    private static bool PlatformUyumlu(Musteri m, V6UygulamaPazarKaydi u) =>
        u.UrunTuru == "isletim-sistemi" ||
        u.DesteklenenPlatformlar.Count == 0 ||
        u.DesteklenenPlatformlar.Contains("tum", StringComparer.OrdinalIgnoreCase) ||
        u.DesteklenenPlatformlar.Contains(m.IsletimSistemiKimligi, StringComparer.OrdinalIgnoreCase);

    private void MusteriCvleriniTamamla()
    {
        foreach (Musteri m in _musteriler.Musteriler) CvVarsayilanlariniUygula(m);
    }

    private static void CvVarsayilanlariniUygula(Musteri m)
    {
        m.KullandigiUygulamalar ??= [];
        m.UygulamaKullanimSayilari ??= new(StringComparer.OrdinalIgnoreCase);
        m.UygulamaMemnuniyetleri ??= new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(m.MeslekProfili))
        {
            string tur = m.MusteriTuru.ToString().ToLowerInvariant();
            m.MeslekProfili = tur.Contains("kamu") ? "Kamu çalışanı" : tur.Contains("kurumsal") ? "Kurumsal yönetici" : tur.Contains("islet") ? "İşletme sahibi" : "Bireysel kullanıcı";
        }
        if (string.IsNullOrWhiteSpace(m.GelirSegmenti))
            m.GelirSegmenti = m.TickBasinaHarcamaButcesi switch { < 150 => "düşük", < 750 => "orta", < 3_000 => "üst", _ => "kurumsal" };
    }

    private static V6MusteriCvOzeti CvOzeti(Musteri m) => new()
    {
        MusteriKimligi = m.MusteriKimligi,
        MusteriAdi = m.MusteriAdi,
        MusteriTuru = m.MusteriTuru.ToString(),
        MeslekProfili = m.MeslekProfili,
        GelirSegmenti = m.GelirSegmenti,
        IsletimSistemiKimligi = m.IsletimSistemiKimligi,
        Uygulamalar = m.KullandigiUygulamalar.ToList().AsReadOnly(),
        ToplamHarcama = m.ToplamHarcama,
        BasariliIs = m.BasariliIsSayisi,
        BasarisizIs = m.BasarisizIsSayisi
    };

    private static List<V6KategoriPazarPayi> PazarPaylariniHesapla(IEnumerable<V6UygulamaPazarKaydi> uygulamalar) =>
        uygulamalar.Where(x => x.Aktif).GroupBy(x => x.Kategori, StringComparer.OrdinalIgnoreCase).Select(g =>
        {
            int toplam = g.Sum(x => x.AktifKullanici);
            return new V6KategoriPazarPayi
            {
                Kategori = g.Key,
                ToplamKullanici = toplam,
                Uygulamalar = g.OrderByDescending(x => x.AktifKullanici).Select(x => new V6KategoriUygulamaPayi
                {
                    UygulamaKimligi = x.UygulamaKimligi,
                    UygulamaAdi = x.UygulamaAdi,
                    SirketAdi = x.SirketAdi,
                    Kullanici = x.AktifKullanici,
                    PayYuzdesi = toplam <= 0 ? 0 : x.AktifKullanici * 100d / toplam
                }).ToList()
            };
        }).OrderByDescending(x => x.ToplamKullanici).ToList();

    private void HaberEkle(long tick, SirketKaydi sirket, string baslik, string ozet, string tur, string onem, decimal finansalEtki)
    {
        string anahtar = $"{tick}|{sirket.SirketKimligi}|{baslik}|{ozet}";
        if (_durum.Haberler.Any(x => x.HaberKimligi == anahtar)) return;
        _durum.Haberler.Add(new V6HaberKaydi
        {
            HaberKimligi = anahtar,
            TickNumarasi = tick,
            Baslik = baslik,
            Ozet = ozet,
            Tur = tur,
            Onem = onem,
            SirketKimligi = sirket.SirketKimligi,
            SirketAdi = sirket.SirketAdi,
            FinansalEtki = finansalEtki
        });
        if (_durum.Haberler.Count > 250) _durum.Haberler.RemoveRange(0, _durum.Haberler.Count - 250);
    }

    private V6SirketDurumu SirketDurumu(string kimlik)
    {
        V6SirketDurumu? d = _durum.Sirketler.FirstOrDefault(x => x.SirketKimligi.Equals(kimlik, StringComparison.OrdinalIgnoreCase));
        if (d is not null) return d;
        d = new V6SirketDurumu { SirketKimligi = kimlik };
        _durum.Sirketler.Add(d);
        return d;
    }

    private JsonObject PanelOku(string sirketKimligi, CancellationToken cancellationToken) =>
        JsonNode.Parse(_isletim.PanelJsonuOlusturAsync(sirketKimligi, cancellationToken).GetAwaiter().GetResult()) as JsonObject ?? new();

    private void YatirimSeviyesiSiniriniKaldir()
    {
        FieldInfo? alan = _temel.GetType().GetField("Yatirimlar", BindingFlags.Static | BindingFlags.NonPublic);
        if (alan?.GetValue(null) is IEnumerable<YatirimPaketi> paketler)
            foreach (YatirimPaketi p in paketler) p.AzamiSeviye = int.MaxValue;
    }

    private async Task TemelVeriyiKaydetAsync(CancellationToken cancellationToken)
    {
        object? sonuc = _kaydetMetodu.Invoke(_temel, [cancellationToken]);
        if (sonuc is Task task) await task;
    }

    private async Task KaydetAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        string tmp = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(_durum, JsonAyarlari), new UTF8Encoding(false), cancellationToken);
        File.Move(tmp, _dosyaYolu, true);
    }

    private static string Str(JsonNode? n) { try { return n?.GetValue<string>() ?? string.Empty; } catch { return string.Empty; } }
    private static int Int(JsonNode? n) { try { return n?.GetValue<int>() ?? 0; } catch { return 0; } }
    private static decimal Dec(JsonNode? n) { try { return n?.GetValue<decimal>() ?? 0; } catch { return 0; } }
    private static bool Bool(JsonNode? n) { try { return n?.GetValue<bool>() ?? false; } catch { return false; } }

    public async ValueTask DisposeAsync()
    {
        if (!_baslatildi) return;
        await _kilit.WaitAsync();
        try { await KaydetAsync(CancellationToken.None); }
        finally { _kilit.Release(); _kilit.Dispose(); }
    }
}
