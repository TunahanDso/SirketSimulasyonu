using System.Text.Json;
using SirketMotoru.Ayarlar;
using SirketMotoru.CanliPano;
using SirketMotoru.Hizmetler;
using SirketMotoru.Isletim;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Sirketler;
using SirketMotoru.Tick;

Console.OutputEncoding = System.Text.Encoding.UTF8;
KonsolKayitcisi.Bilgi("Üç Kardeş Yazılım Şirketi Simülasyonu başlatılıyor.");
using CancellationTokenSource iptalKaynagi = new();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    KonsolKayitcisi.Uyari("Motor kapatma sinyali aldı.");
    iptalKaynagi.Cancel();
};

try
{
    JsonSerializerOptions jsonAyarlari = new() { PropertyNameCaseInsensitive = true };
    string ayarDosyasiYolu = VeriDosyasiniBul("motor-ayarlari.json");
    string motorVerileriKlasoru = Path.GetDirectoryName(ayarDosyasiYolu)
        ?? throw new InvalidOperationException("MotorVerileri klasörü belirlenemedi.");
    KonsolKayitcisi.Bilgi($"Motor veri klasörü: {motorVerileriKlasoru}");

    MotorAyarlari motorAyarlari = await JsonDosyasiniOkuAsync<MotorAyarlari>(ayarDosyasiYolu, jsonAyarlari, iptalKaynagi.Token);
    MotorAyarlariDogrulayicisi.Dogrula(motorAyarlari);
    KonsolKayitcisi.Basari($"Motor ayarları yüklendi. Şirket sayısı: {motorAyarlari.Sirketler.Count}");

    string katalogDosyasiYolu = Path.Combine(motorVerileriKlasoru, "hizmet-katalogu.json");
    if (!File.Exists(katalogDosyasiYolu)) katalogDosyasiYolu = VeriDosyasiniBul("hizmet-katalogu.json");
    HizmetKatalogAyarlari tohumKatalog = await JsonDosyasiniOkuAsync<HizmetKatalogAyarlari>(katalogDosyasiYolu, jsonAyarlari, iptalKaynagi.Token);
    HizmetKatalogAyarlari katalogAyarlari = StandartKatalogV6.Genislet(tohumKatalog);
    HizmetKatalogu hizmetKatalogu = new(katalogAyarlari);
    KonsolKayitcisi.Basari($"V6 hizmet kataloğu yüklendi | Sürüm: {hizmetKatalogu.KatalogSurumu} | Aktif: {hizmetKatalogu.Hizmetler.Count(h => h.Aktif)} | Uygulama kategorisi: {StandartKatalogV6.UygulamaKategorileri.Count}");

    string musteriDosyasiYolu = Path.Combine(motorVerileriKlasoru, "musteriler.json");
    MusteriVeritabani musteriVeritabani = new(musteriDosyasiYolu);
    await musteriVeritabani.YukleVeyaOlusturAsync(20_000, iptalKaynagi.Token);
    await using MusteriYoneticisi musteriYoneticisi = new(musteriVeritabani, hizmetKatalogu);
    await musteriYoneticisi.BaslatAsync(iptalKaynagi.Token);
    MusteriIsletimSistemiYoneticisi musteriIsletimSistemiYoneticisi = new(musteriYoneticisi);
    KonsolKayitcisi.Basari($"Müşteri sistemi hazır | Toplam: {musteriYoneticisi.Musteriler.Count} | Aktif: {musteriYoneticisi.AktifMusteriSayisi}");

    await using SirketYoneticisi sirketYoneticisi = new(motorAyarlari, hizmetKatalogu);
    await BaslangicMigrasyonlari.YonetimHesaplariniHazirlaAsync(motorVerileriKlasoru, iptalKaynagi.Token);

    await using KodTabanliSirketIsletimYoneticisi isletimYoneticisi = new(sirketYoneticisi, motorVerileriKlasoru);
    await isletimYoneticisi.BaslatAsync(iptalKaynagi.Token);
    await using EkosistemYoneticisi ekosistemYoneticisi = new(sirketYoneticisi, isletimYoneticisi, motorVerileriKlasoru);
    await ekosistemYoneticisi.BaslatAsync(iptalKaynagi.Token);

    PazarFiyatOzTesti.Dogrula();
    await using PazarFiyatYoneticisi pazarFiyatYoneticisi = new(isletimYoneticisi, sirketYoneticisi, motorVerileriKlasoru);
    await pazarFiyatYoneticisi.BaslatAsync(iptalKaynagi.Token);
    PazarGelirDuzeltmeYoneticisi pazarGelirDuzeltmeYoneticisi = new(isletimYoneticisi, sirketYoneticisi);

    await using EkonomiV6Yoneticisi ekonomiV6Yoneticisi = new(
        sirketYoneticisi,
        musteriYoneticisi,
        isletimYoneticisi,
        motorVerileriKlasoru);
    await ekonomiV6Yoneticisi.BaslatAsync(iptalKaynagi.Token);

    await BaslangicMigrasyonlari.TekSeferlikFinansmanDestekleriniUygulaAsync(sirketYoneticisi, motorVerileriKlasoru, iptalKaynagi.Token);

    TickYoneticisi tickYoneticisi = new(
        motorAyarlari,
        sirketYoneticisi,
        musteriYoneticisi,
        musteriIsletimSistemiYoneticisi,
        isletimYoneticisi,
        ekosistemYoneticisi,
        pazarFiyatYoneticisi,
        pazarGelirDuzeltmeYoneticisi,
        ekonomiV6Yoneticisi);

    await using YazilimBorsasiSunucusu yazilimBorsasiSunucusu = new(
        motorAyarlari,
        sirketYoneticisi,
        musteriYoneticisi,
        hizmetKatalogu,
        isletimYoneticisi,
        () => tickYoneticisi.TickNumarasi);

    await using SirketYonetimSunucusu sirketYonetimSunucusu = new(
        motorAyarlari,
        isletimYoneticisi,
        ekosistemYoneticisi);

    await yazilimBorsasiSunucusu.BaslatAsync(iptalKaynagi.Token);
    await sirketYonetimSunucusu.BaslatAsync(iptalKaynagi.Token);
    await sirketYoneticisi.IlkBaglantilariKurAsync(iptalKaynagi.Token);
    await tickYoneticisi.BaslatAsync(iptalKaynagi.Token);
}
catch (OperationCanceledException)
{
    KonsolKayitcisi.Bilgi("Motor kullanıcı tarafından durduruldu.");
}
catch (FileNotFoundException exception)
{
    KonsolKayitcisi.Hata($"Gerekli motor dosyası bulunamadı: {exception.Message}");
    KonsolKayitcisi.Hata(exception.ToString());
    Environment.ExitCode = 1;
}
catch (JsonException exception)
{
    KonsolKayitcisi.Hata($"JSON dosyası okunamadı: {exception.Message}");
    KonsolKayitcisi.Hata(exception.ToString());
    Environment.ExitCode = 1;
}
catch (InvalidOperationException exception)
{
    KonsolKayitcisi.Hata($"Motor yapılandırması veya durumu geçersiz: {exception.Message}");
    KonsolKayitcisi.Hata(exception.ToString());
    Environment.ExitCode = 1;
}
catch (Exception exception)
{
    KonsolKayitcisi.Hata($"Motor çalıştırılamadı: {exception.Message}");
    KonsolKayitcisi.Hata(exception.ToString());
    Environment.ExitCode = 1;
}
finally
{
    KonsolKayitcisi.Bilgi("Motor kapatıldı.");
}

static async Task<T> JsonDosyasiniOkuAsync<T>(string dosyaYolu, JsonSerializerOptions jsonAyarlari, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(dosyaYolu)) throw new ArgumentException("JSON dosya yolu boş olamaz.", nameof(dosyaYolu));
    if (!File.Exists(dosyaYolu)) throw new FileNotFoundException($"JSON dosyası bulunamadı: {dosyaYolu}", dosyaYolu);
    string json = await File.ReadAllTextAsync(dosyaYolu, cancellationToken);
    if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException($"{Path.GetFileName(dosyaYolu)} dosyası boş.");
    T? veri = JsonSerializer.Deserialize<T>(json, jsonAyarlari);
    return veri ?? throw new InvalidOperationException($"{Path.GetFileName(dosyaYolu)} dosyası okunamadı.");
}

static string VeriDosyasiniBul(string dosyaAdi)
{
    if (string.IsNullOrWhiteSpace(dosyaAdi)) throw new ArgumentException("Dosya adı boş olamaz.", nameof(dosyaAdi));
    string[] olasiYollar =
    [
        Path.Combine(Directory.GetCurrentDirectory(), "MotorVerileri", dosyaAdi),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "MotorVerileri", dosyaAdi),
        Path.Combine(AppContext.BaseDirectory, "MotorVerileri", dosyaAdi),
        Path.Combine(AppContext.BaseDirectory, "..", "MotorVerileri", dosyaAdi),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "MotorVerileri", dosyaAdi),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MotorVerileri", dosyaAdi)
    ];
    foreach (string yol in olasiYollar.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        if (File.Exists(yol)) return yol;
    throw new FileNotFoundException($"{dosyaAdi} bulunamadı. Denenen yollar: {string.Join(" | ", olasiYollar.Select(Path.GetFullPath))}");
}
