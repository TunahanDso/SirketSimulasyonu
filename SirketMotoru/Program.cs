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
KonsolKayitcisi.Bilgi(
    "Üç Kardeş Yazılım Şirketi Simülasyonu başlatılıyor.");

using CancellationTokenSource iptalKaynagi = new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    KonsolKayitcisi.Uyari("Motor kapatma sinyali aldı.");
    iptalKaynagi.Cancel();
};

try
{
    JsonSerializerOptions jsonAyarlari = new()
    {
        PropertyNameCaseInsensitive = true
    };

    string ayarDosyasiYolu = VeriDosyasiniBul("motor-ayarlari.json");
    string motorVerileriKlasoru =
        Path.GetDirectoryName(ayarDosyasiYolu)
        ?? throw new InvalidOperationException(
            "MotorVerileri klasörü belirlenemedi.");

    KonsolKayitcisi.Bilgi(
        $"Motor veri klasörü: {motorVerileriKlasoru}");
    KonsolKayitcisi.Bilgi(
        $"Motor ayar dosyası bulundu: {ayarDosyasiYolu}");

    MotorAyarlari motorAyarlari =
        await JsonDosyasiniOkuAsync<MotorAyarlari>(
            ayarDosyasiYolu,
            jsonAyarlari,
            iptalKaynagi.Token);

    MotorAyarlariDogrulayicisi.Dogrula(motorAyarlari);
    KonsolKayitcisi.Basari(
        $"Motor ayarları yüklendi. Şirket sayısı: " +
        $"{motorAyarlari.Sirketler.Count}");

    string katalogDosyasiYolu = Path.Combine(
        motorVerileriKlasoru,
        "hizmet-katalogu.json");

    if (!File.Exists(katalogDosyasiYolu))
    {
        katalogDosyasiYolu = VeriDosyasiniBul("hizmet-katalogu.json");
    }

    HizmetKatalogAyarlari katalogAyarlari =
        await JsonDosyasiniOkuAsync<HizmetKatalogAyarlari>(
            katalogDosyasiYolu,
            jsonAyarlari,
            iptalKaynagi.Token);
    HizmetKatalogu hizmetKatalogu = new(katalogAyarlari);
    int aktifHizmetSayisi = hizmetKatalogu.Hizmetler.Count(
        hizmet => hizmet.Aktif);

    KonsolKayitcisi.Basari(
        $"Hizmet kataloğu yüklendi. Katalog sürümü: " +
        $"{hizmetKatalogu.KatalogSurumu} | " +
        $"Aktif hizmet sayısı: {aktifHizmetSayisi}");

    foreach (HizmetTanimi hizmet in
             hizmetKatalogu.Hizmetler
                 .Where(hizmet => hizmet.Aktif)
                 .OrderBy(
                     hizmet => hizmet.HizmetKimligi,
                     StringComparer.OrdinalIgnoreCase))
    {
        KonsolKayitcisi.Bilgi(
            $"Hizmet: {hizmet.HizmetKimligi}@{hizmet.HizmetSurumu}");
    }

    string musteriDosyasiYolu = Path.Combine(
        motorVerileriKlasoru,
        "musteriler.json");

    MusteriVeritabani musteriVeritabani = new(musteriDosyasiYolu);
    await using MusteriYoneticisi musteriYoneticisi =
        new(musteriVeritabani, hizmetKatalogu);
    await musteriYoneticisi.BaslatAsync(iptalKaynagi.Token);

    KonsolKayitcisi.Basari(
        $"Müşteri sistemi hazır. Toplam müşteri: " +
        $"{musteriYoneticisi.Musteriler.Count} | " +
        $"Aktif müşteri: {musteriYoneticisi.AktifMusteriSayisi} | " +
        $"Toplam müşteri bakiyesi: " +
        $"{musteriYoneticisi.ToplamMusteriBakiyesi:N2}");

    await using SirketYoneticisi sirketYoneticisi =
        new(motorAyarlari, hizmetKatalogu);

    await using KodTabanliSirketIsletimYoneticisi isletimYoneticisi =
        new(sirketYoneticisi, motorVerileriKlasoru);
    await isletimYoneticisi.BaslatAsync(iptalKaynagi.Token);

    TickYoneticisi tickYoneticisi = new(
        motorAyarlari,
        sirketYoneticisi,
        musteriYoneticisi,
        isletimYoneticisi);

    await using YazilimBorsasiSunucusu yazilimBorsasiSunucusu = new(
        motorAyarlari,
        sirketYoneticisi,
        musteriYoneticisi,
        hizmetKatalogu,
        isletimYoneticisi,
        () => tickYoneticisi.TickNumarasi);

    await using SirketYonetimSunucusu sirketYonetimSunucusu = new(
        motorAyarlari,
        isletimYoneticisi);

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
    KonsolKayitcisi.Hata(
        $"Gerekli motor dosyası bulunamadı: {exception.Message}");
    KonsolKayitcisi.Hata(exception.ToString());
    Environment.ExitCode = 1;
}
catch (JsonException exception)
{
    KonsolKayitcisi.Hata(
        $"JSON dosyası okunamadı: {exception.Message}");
    KonsolKayitcisi.Hata(exception.ToString());
    Environment.ExitCode = 1;
}
catch (InvalidOperationException exception)
{
    KonsolKayitcisi.Hata(
        $"Motor yapılandırması veya durumu geçersiz: " +
        exception.Message);
    KonsolKayitcisi.Hata(exception.ToString());
    Environment.ExitCode = 1;
}
catch (Exception exception)
{
    KonsolKayitcisi.Hata(
        $"Motor çalıştırılamadı: {exception.Message}");
    KonsolKayitcisi.Hata(exception.ToString());
    Environment.ExitCode = 1;
}
finally
{
    KonsolKayitcisi.Bilgi("Motor kapatıldı.");
}

static async Task<T> JsonDosyasiniOkuAsync<T>(
    string dosyaYolu,
    JsonSerializerOptions jsonAyarlari,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(dosyaYolu))
    {
        throw new ArgumentException(
            "JSON dosya yolu boş olamaz.",
            nameof(dosyaYolu));
    }

    if (!File.Exists(dosyaYolu))
    {
        throw new FileNotFoundException(
            $"JSON dosyası bulunamadı: {dosyaYolu}",
            dosyaYolu);
    }

    string json = await File.ReadAllTextAsync(
        dosyaYolu,
        cancellationToken);

    if (string.IsNullOrWhiteSpace(json))
    {
        throw new InvalidOperationException(
            $"{Path.GetFileName(dosyaYolu)} dosyası boş.");
    }

    T? veri = JsonSerializer.Deserialize<T>(json, jsonAyarlari);
    return veri ?? throw new InvalidOperationException(
        $"{Path.GetFileName(dosyaYolu)} dosyası okunamadı.");
}

static string VeriDosyasiniBul(string dosyaAdi)
{
    if (string.IsNullOrWhiteSpace(dosyaAdi))
    {
        throw new ArgumentException(
            "Dosya adı boş olamaz.",
            nameof(dosyaAdi));
    }

    string[] olasiYollar =
    [
        Path.Combine(
            Directory.GetCurrentDirectory(),
            "MotorVerileri",
            dosyaAdi),
        Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "MotorVerileri",
            dosyaAdi),
        Path.Combine(
            AppContext.BaseDirectory,
            "MotorVerileri",
            dosyaAdi),
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "MotorVerileri",
            dosyaAdi),
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "MotorVerileri",
            dosyaAdi),
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "MotorVerileri",
            dosyaAdi)
    ];

    foreach (string yol in olasiYollar)
    {
        string tamYol = Path.GetFullPath(yol);
        if (File.Exists(tamYol))
        {
            return tamYol;
        }
    }

    string arananYollar = string.Join(
        Environment.NewLine,
        olasiYollar.Select(
            yol => $" - {Path.GetFullPath(yol)}"));

    throw new FileNotFoundException(
        $"MotorVerileri/{dosyaAdi} dosyası bulunamadı." +
        Environment.NewLine +
        "Aranan yollar:" +
        Environment.NewLine +
        arananYollar);
}
