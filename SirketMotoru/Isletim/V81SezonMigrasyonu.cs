using System.Text;
using System.Text.Json;
using SirketMotoru.Ayarlar;
using SirketMotoru.Isler;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

/// <summary>
/// Deneysel V5-V8 ekonomi geçmişini temiz bir sezona taşır.
/// Hizmet ve uygulama tanımları korunur; şirketlerin bilanço, yatırım,
/// kredi, ceza, kapasite satın alımı ve tarihsel piyasa sayaçları sıfırlanır.
/// Yalnız bir kez çalışır ve işlem öncesinde bütün dosyaları arşivler.
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
        "musteriler.json",
        "sirket-bilancolari.json",
        "sirket-isletim.json",
        "kod-tabanli-yayinlar.json",
        "ekosistem.json",
        "ekonomi-v6.json",
        "finans-v7.json",
        "pazar-fiyat.json",
        "ceza-v8.json",
        "tick-saat.json"
    ];

    public static async Task UygulaAsync(
        MotorAyarlari motorAyarlari,
        SirketYoneticisi sirketYoneticisi,
        MusteriVeritabani musteriVeritabani,
        string motorVerileriKlasoru,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(motorAyarlari);
        ArgumentNullException.ThrowIfNull(sirketYoneticisi);
        ArgumentNullException.ThrowIfNull(musteriVeritabani);
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);

        string kok = Path.GetFullPath(motorVerileriKlasoru);
        string isaret = Path.Combine(kok, "v8.1-tam-sirket-reset-v2.json");
        if (File.Exists(isaret))
        {
            KonsolKayitcisi.Bilgi("V8.1 tam şirket reseti daha önce uygulanmış; tekrar çalıştırılmadı.");
            return;
        }

        string zamanDamgasi = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        string arsivKlasoru = Path.Combine(kok, "Arsiv", $"Reset-oncesi-{zamanDamgasi}");
        Directory.CreateDirectory(arsivKlasoru);

        foreach (string dosyaAdi in ArsivlenecekDosyalar)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string kaynak = Path.Combine(kok, dosyaAdi);
            if (File.Exists(kaynak))
                File.Copy(kaynak, Path.Combine(arsivKlasoru, dosyaAdi), overwrite: true);
        }

        Dictionary<string, SirketBaglantiAyari> ayarlar = motorAyarlari.Sirketler
            .ToDictionary(x => x.SirketKimligi, StringComparer.OrdinalIgnoreCase);

        foreach (SirketKaydi sirket in sirketYoneticisi.SirketKayitlari)
        {
            ayarlar.TryGetValue(sirket.SirketKimligi, out SirketBaglantiAyari? ayar);
            SirketiBaslangicaDondur(sirket, ayar);
        }
        await sirketYoneticisi.BilancolariKaydetAsync(cancellationToken);

        await IsletimKaydiniSifirlaAsync(
            Path.Combine(kok, "sirket-isletim.json"),
            cancellationToken);
        MusteriPazarGecmisiniSifirla(musteriVeritabani.Musteriler);
        await musteriVeritabani.KaydetAsync(cancellationToken);

        await AtomikNesneYazAsync(Path.Combine(kok, "finans-v7.json"), new FinansV7Dosyasi(), cancellationToken);
        await AtomikNesneYazAsync(Path.Combine(kok, "ceza-v8.json"), new CezaV8Dosyasi(), cancellationToken);
        GuvenliSil(Path.Combine(kok, "pazar-fiyat.json"));
        GuvenliSil(Path.Combine(kok, "ekonomi-v6.json"));
        GuvenliSil(Path.Combine(kok, "ekosistem.json"));
        GuvenliSil(Path.Combine(kok, "tick-saat.json"));

        var kayit = new
        {
            surum = "8.1-reset-v2",
            uygulamaZamani = DateTimeOffset.Now,
            arsivKlasoru,
            korunanlar = new[]
            {
                "şirket sunucularındaki hizmet kodları ve manifestleri",
                "hizmetlerin yönetilen fiyat ve aktiflik ayarları",
                "uygulama kimlikleri, adları, kategorileri, fiyatları ve platform/protokol bağlantıları",
                "özel protokol tanımları ve uygulama eşlemeleri",
                "8090 hesapları ve değiştirilmiş parolalar",
                "20.000 müşteri kimliği, profili ve mevcut bakiyesi"
            },
            sifirlananlar = new[]
            {
                "şirket kasaları başlangıç kasasına döndü",
                "toplam gelir, gider, ceza, iş ve saldırı geçmişi",
                "yatırım seviyeleri ve devam eden yatırım süreçleri",
                "satın alınmış hizmet ve uygulama kapasitesi",
                "bütün krediler, temerrütler, SLA sözleşmeleri ve olaylar",
                "uygulama kullanıcıları ve tarihsel ürün gelir/gider sayaçları",
                "müşteri işletim sistemi ve uygulama tercih geçmişi",
                "finans, fiyat, ceza, haber, grafik ve tick geçmişi"
            }
        };
        await AtomikYazAsync(isaret, JsonSerializer.Serialize(kayit, JsonAyarlari), cancellationToken);

        KonsolKayitcisi.Basari(
            $"V8.1 TAM ŞİRKET RESETİ tamamlandı | Hizmetler ve uygulama tanımları korundu | Arşiv: {arsivKlasoru}");
    }

    private static void SirketiBaslangicaDondur(SirketKaydi sirket, SirketBaglantiAyari? ayar)
    {
        sirket.Kasa = Math.Max(0, ayar?.BaslangicKasasi ?? 0);
        sirket.ToplamGelir = 0;
        sirket.ToplamIade = 0;
        sirket.ToplamCeza = 0;
        sirket.BekleyenOdeme = 0;
        sirket.ToplamGuvenlikKaybi = 0;
        sirket.ItibarPuani = Puan(ayar?.BaslangicItibarPuani);
        sirket.GuvenilirlikPuani = Puan(ayar?.BaslangicGuvenilirlikPuani);
        sirket.KodKalitesiPuani = Puan(ayar?.BaslangicKodKalitesiPuani);
        sirket.PerformansPuani = Puan(ayar?.BaslangicPerformansPuani);
        sirket.GuvenlikPuani = Puan(ayar?.BaslangicGuvenlikPuani);
        sirket.OrtalamaMusteriMemnuniyeti = Puan(ayar?.BaslangicMusteriMemnuniyeti);
        sirket.AktifIsSayisi = 0;
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
    }

    private static double Puan(double? deger) =>
        !deger.HasValue || double.IsNaN(deger.Value) || double.IsInfinity(deger.Value)
            ? 50
            : Math.Clamp(deger.Value, 0, 100);

    private static async Task IsletimKaydiniSifirlaAsync(
        string dosyaYolu,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(dosyaYolu)) return;

        string json = await File.ReadAllTextAsync(dosyaYolu, cancellationToken);
        SirketIsletimDosyasi veri = JsonSerializer.Deserialize<SirketIsletimDosyasi>(json, JsonAyarlari)
            ?? throw new InvalidOperationException("V8.1 reseti işletim kaydını okuyamadı.");

        veri.SonIslenenTick = 0;
        veri.PiyasaOlaylari ??= [];
        veri.SozlesmeTeklifleri ??= [];
        veri.Protokoller ??= [];
        veri.Sirketler ??= [];
        veri.PiyasaOlaylari.Clear();
        veri.SozlesmeTeklifleri.Clear();

        foreach (OzelProtokolKaydi protokol in veri.Protokoller)
        {
            protokol.ToplamLisansGeliri = 0;
            protokol.YayinTicki = 0;
        }

        foreach (SirketIsletimDurumu durum in veri.Sirketler)
        {
            durum.YatirimSeviyeleri ??= new(StringComparer.OrdinalIgnoreCase);
            durum.HizmetAyarlari ??= new(StringComparer.OrdinalIgnoreCase);
            durum.HizmetFiyatEzmeDegerleri ??= new(StringComparer.OrdinalIgnoreCase);
            durum.HizmetBazKapasiteleri ??= new(StringComparer.OrdinalIgnoreCase);
            durum.Krediler ??= [];
            durum.Urunler ??= [];
            durum.BenimsenenProtokoller ??= [];
            durum.Sozlesmeler ??= [];
            durum.SonIslemler ??= [];
            durum.SonOlaylar ??= [];

            durum.YatirimSeviyeleri.Clear();
            durum.Krediler.Clear();
            durum.Sozlesmeler.Clear();
            durum.SonIslemler.Clear();
            durum.SonOlaylar.Clear();

            foreach (HizmetKaliciAyari hizmet in durum.HizmetAyarlari.Values)
            {
                // Hizmet ve yönetilen ticari ayar korunur; satın alınmış kapasite sıfırlanır.
                hizmet.SatinAlinanKapasite = 0;
                hizmet.IlkGorulmeTicki = 0;
                hizmet.SonGorulmeTicki = 0;
            }

            durum.KrediNotu = 650;
            durum.EkosistemPuani = 50;
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
            durum.ToplamAboneSayisi = 0;
            durum.TemerrutSayisi = 0;
            durum.SonYonetimIslemiTicki = 0;

            foreach (UrunKaydi urun in durum.Urunler)
            {
                // Uygulama tanımı ve ticari ayarı korunur; eski pazar geçmişi temizlenir.
                urun.SatinAlinanKullaniciKapasitesi = 0;
                urun.AltyapiKapasiteBonusu = 0;
                urun.KullaniciKapasitesi = Math.Max(100, urun.TabanKullaniciKapasitesi);
                urun.AktifKullaniciSayisi = 0;
                urun.ToplamEdinilenKullanici = 0;
                urun.ToplamKaybedilenKullanici = 0;
                urun.UrunMemnuniyeti = 50;
                urun.UrunKalitesi = 50;
                urun.ToplamGelir = 0;
                urun.ToplamGider = 0;
                urun.YayinTicki = 0;
                urun.KesintiTicki = 0;
                urun.SonTalepCarpani = 1;
            }
        }

        veri.GuncellenmeZamani = DateTimeOffset.UtcNow;
        await AtomikYazAsync(dosyaYolu, JsonSerializer.Serialize(veri, JsonAyarlari), cancellationToken);
    }

    private static void MusteriPazarGecmisiniSifirla(IEnumerable<Musteri> musteriler)
    {
        foreach (Musteri musteri in musteriler)
        {
            musteri.TercihEdilenSirketKimligi = null;
            musteri.IsletimSistemiKimligi = string.Empty;
            musteri.IsletimSistemiSurumu = string.Empty;
            musteri.IsletimSistemiSirketKimligi = string.Empty;
            musteri.IsletimSistemiMemnuniyeti = 50;
            musteri.IsletimSistemiDegisimSayisi = 0;
            musteri.SonIsletimSistemiDegisimTicki = 0;
            musteri.KullandigiUygulamalar ??= [];
            musteri.UygulamaKullanimSayilari ??= new(StringComparer.OrdinalIgnoreCase);
            musteri.UygulamaMemnuniyetleri ??= new(StringComparer.OrdinalIgnoreCase);
            musteri.HizmetKullanimSayilari ??= new(StringComparer.OrdinalIgnoreCase);
            musteri.IslemGecmisi ??= [];
            musteri.KullandigiUygulamalar.Clear();
            musteri.UygulamaKullanimSayilari.Clear();
            musteri.UygulamaMemnuniyetleri.Clear();
            musteri.HizmetKullanimSayilari.Clear();
            musteri.IslemGecmisi.Clear();
            musteri.ToplamUygulamaDegisimSayisi = 0;
            musteri.SonUygulamaDegisimTicki = 0;
            musteri.ToplamHarcama = 0;
            musteri.BasariliIsSayisi = 0;
            musteri.BasarisizIsSayisi = 0;
            musteri.SonIslemZamani = null;
        }
    }

    private static async Task AtomikNesneYazAsync<T>(
        string dosyaYolu,
        T veri,
        CancellationToken cancellationToken) =>
        await AtomikYazAsync(dosyaYolu, JsonSerializer.Serialize(veri, JsonAyarlari), cancellationToken);

    private static async Task AtomikYazAsync(
        string dosyaYolu,
        string icerik,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dosyaYolu)!);
        string gecici = dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(gecici, icerik, new UTF8Encoding(false), cancellationToken);
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
                $"V8.1 çalışma dosyası temizlenemedi: {Path.GetFileName(dosyaYolu)} | {hata.Message}");
        }
    }
}
