using System.Text;
using System.Text.Json;
using SirketMotoru.Isler;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

/// <summary>
/// V8 öncesindeki deneysel ekonomi sayaçlarını kalıcı oyun varlıklarından ayırır.
/// Yalnız bir kez çalışır; mevcut dosyaları arşivler, kasa/yatırım/ürün/hizmet
/// ayarlarını korur ve yeni sezonun kümülatif sayaçlarını temizler.
/// </summary>
public static class V81SezonMigrasyonu
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly string[] ArsivlenecekDosyalar =
    [
        "sirket-bilancolari.json",
        "sirket-isletim.json",
        "finans-v7.json",
        "ceza-v8.json",
        "pazar-fiyat.json",
        "ekonomi-v6.json"
    ];

    public static async Task UygulaAsync(
        SirketYoneticisi sirketYoneticisi,
        string motorVerileriKlasoru,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sirketYoneticisi);
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);

        string kok = Path.GetFullPath(motorVerileriKlasoru);
        string isaret = Path.Combine(kok, "v8.1-sezon-migrasyonu.json");
        if (File.Exists(isaret))
        {
            KonsolKayitcisi.Bilgi("V8.1 sezon migrasyonu daha önce uygulanmış; kalıcı varlıklar korunuyor.");
            return;
        }

        string zamanDamgasi = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        string arsivKlasoru = Path.Combine(kok, "Arsiv", $"V8-oncesi-{zamanDamgasi}");
        Directory.CreateDirectory(arsivKlasoru);

        foreach (string dosyaAdi in ArsivlenecekDosyalar)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string kaynak = Path.Combine(kok, dosyaAdi);
            if (!File.Exists(kaynak)) continue;
            File.Copy(kaynak, Path.Combine(arsivKlasoru, dosyaAdi), overwrite: true);
        }

        foreach (SirketKaydi sirket in sirketYoneticisi.SirketKayitlari)
        {
            SirketOperasyonSayaclariniSifirla(sirket);
        }
        await sirketYoneticisi.BilancolariKaydetAsync(cancellationToken);

        await IsletimKaydiniYeniSezonaHazirlaAsync(
            Path.Combine(kok, "sirket-isletim.json"),
            cancellationToken);

        // Tick grafikleri, eski fiyat şokları ve ceza kayıtları yeni sezonu eğmemeli.
        await BosJsonYazAsync(
            Path.Combine(kok, "finans-v7.json"),
            new FinansV7Dosyasi(),
            cancellationToken);
        await BosJsonYazAsync(
            Path.Combine(kok, "ceza-v8.json"),
            new CezaV8Dosyasi(),
            cancellationToken);

        GuvenliSil(Path.Combine(kok, "pazar-fiyat.json"));
        GuvenliSil(Path.Combine(kok, "ekonomi-v6.json"));

        var kayit = new
        {
            surum = "8.1",
            uygulamaZamani = DateTimeOffset.Now,
            arsivKlasoru,
            korunanlar = new[]
            {
                "kasa",
                "yatırım seviyeleri",
                "hizmet fiyatı/aktifliği/kapasitesi",
                "uygulamalar ve aktif kullanıcıları",
                "işletim sistemi ve protokol dağıtımı",
                "manuel kredilerin kalan borcu"
            },
            sifirlananlar = new[]
            {
                "eski toplam gelir/iade/ceza sayaçları",
                "iş ve saldırı sayaçları",
                "eski finans grafikleri",
                "eski fiyat şoku geçmişi",
                "otomatik kurtarma kredileri",
                "eski SLA ve olay takvimleri"
            }
        };
        await AtomikYazAsync(isaret, JsonSerializer.Serialize(kayit, JsonAyarlari), cancellationToken);

        KonsolKayitcisi.Basari(
            $"V8.1 temiz sezon migrasyonu tamamlandı | Arşiv: {arsivKlasoru}");
    }

    private static void SirketOperasyonSayaclariniSifirla(SirketKaydi sirket)
    {
        // Kasa ve kalite puanları korunur. Yalnız V8 öncesi kümülatif operasyon geçmişi ayrılır.
        sirket.ToplamGelir = 0;
        sirket.ToplamIade = 0;
        sirket.ToplamCeza = 0;
        sirket.BekleyenOdeme = 0;
        sirket.ToplamGuvenlikKaybi = 0;
        sirket.TamamlananIsSayisi = 0;
        sirket.BasarisizIsSayisi = 0;
        sirket.ZamanAsiminaUgrayanIsSayisi = 0;
        sirket.IptalEdilenIsSayisi = 0;
        sirket.ReddedilenIsSayisi = 0;
        sirket.EngellenenSaldiriSayisi = 0;
        sirket.BasariliSaldiriSayisi = 0;
        sirket.ToplamIslemSuresiMs = 0;
        sirket.SonBasariliIsZamani = null;
        sirket.SonBasarisizIsZamani = null;
        sirket.AktifIsSayisi = 0;
    }

    private static async Task IsletimKaydiniYeniSezonaHazirlaAsync(
        string dosyaYolu,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(dosyaYolu)) return;

        string json = await File.ReadAllTextAsync(dosyaYolu, cancellationToken);
        SirketIsletimDosyasi veri = JsonSerializer.Deserialize<SirketIsletimDosyasi>(json, JsonAyarlari)
            ?? throw new InvalidOperationException("V8.1 migrasyonu işletim kaydını okuyamadı.");

        veri.SonIslenenTick = 0;
        veri.PiyasaOlaylari ??= [];
        veri.SozlesmeTeklifleri ??= [];
        veri.PiyasaOlaylari.Clear();
        veri.SozlesmeTeklifleri.Clear();

        foreach (OzelProtokolKaydi protokol in veri.Protokoller ?? [])
        {
            protokol.ToplamLisansGeliri = 0;
            protokol.YayinTicki = 0;
        }

        foreach (SirketIsletimDurumu durum in veri.Sirketler ?? [])
        {
            durum.Krediler ??= [];
            durum.Urunler ??= [];
            durum.Sozlesmeler ??= [];
            durum.SonIslemler ??= [];
            durum.SonOlaylar ??= [];

            // Bozuk eski ekonomi tarafından açılan otomatik krediler temizlenir;
            // oyuncunun elle aldığı kredilerin kalan borcu korunur ve takvimi yeni sezona alınır.
            durum.Krediler.RemoveAll(k =>
                string.Equals(k.KrediTuru, "otomatik-kurtarma", StringComparison.OrdinalIgnoreCase));
            foreach (KrediKaydi kredi in durum.Krediler.Where(k => k.Aktif))
            {
                kredi.SonrakiOdemeTicki = 5;
                kredi.GecikmeSayisi = 0;
            }

            durum.Sozlesmeler.Clear();
            durum.SonIslemler.Clear();
            durum.SonOlaylar.Clear();
            durum.KrediNotu = 650;
            durum.TemerrutSayisi = 0;
            durum.OperasyonRiski = 18;
            durum.TeknikBorc = 12;
            durum.BakimBaskisi = 0;
            durum.ToplamAbonelikGeliri = 0;
            durum.ToplamUrunGeliri = 0;
            durum.ToplamIsletmeGideri = 0;
            durum.ToplamFinansmanGideri = 0;
            durum.ToplamYatirimHarcamasi = 0;
            durum.OdenemeyenGider = 0;
            durum.SirketDegeri = 0;
            durum.TahminiHisseFiyati = 0;
            durum.SonYonetimIslemiTicki = 0;

            foreach (UrunKaydi urun in durum.Urunler)
            {
                urun.ToplamGelir = 0;
                urun.ToplamGider = 0;
                urun.ToplamEdinilenKullanici = 0;
                urun.ToplamKaybedilenKullanici = 0;
                urun.YayinTicki = 0;
                urun.KesintiTicki = 0;
                urun.SonTalepCarpani = 1;
            }
        }

        veri.GuncellenmeZamani = DateTimeOffset.UtcNow;
        await AtomikYazAsync(
            dosyaYolu,
            JsonSerializer.Serialize(veri, JsonAyarlari),
            cancellationToken);
    }

    private static async Task BosJsonYazAsync<T>(
        string dosyaYolu,
        T veri,
        CancellationToken cancellationToken)
    {
        await AtomikYazAsync(
            dosyaYolu,
            JsonSerializer.Serialize(veri, JsonAyarlari),
            cancellationToken);
    }

    private static async Task AtomikYazAsync(
        string dosyaYolu,
        string icerik,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dosyaYolu)!);
        string gecici = dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(
            gecici,
            icerik,
            new UTF8Encoding(false),
            cancellationToken);
        File.Move(gecici, dosyaYolu, overwrite: true);
    }

    private static void GuvenliSil(string dosyaYolu)
    {
        try
        {
            if (File.Exists(dosyaYolu)) File.Delete(dosyaYolu);
        }
        catch (IOException hata)
        {
            KonsolKayitcisi.Uyari(
                $"V8.1 eski çalışma dosyası silinemedi: {Path.GetFileName(dosyaYolu)} | {hata.Message}");
        }
    }
}
