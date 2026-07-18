using System.Text.Json;
using SirketMotoru.Ayarlar;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;
using SirketMotoru.Tick;

Console.OutputEncoding = System.Text.Encoding.UTF8;

KonsolKayitcisi.Bilgi(
    "Üç Kardeş Yazılım Şirketi Simülasyonu başlatılıyor.");

CancellationTokenSource iptalKaynagi = new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;

    KonsolKayitcisi.Uyari(
        "Motor kapatma sinyali aldı.");

    iptalKaynagi.Cancel();
};

try
{
    string ayarDosyasiYolu =
        AyarDosyasiniBul();

    KonsolKayitcisi.Bilgi(
        $"Ayar dosyası bulundu: {ayarDosyasiYolu}");

    string ayarJson =
        await File.ReadAllTextAsync(
            ayarDosyasiYolu,
            iptalKaynagi.Token);

    JsonSerializerOptions jsonAyarlari = new()
    {
        PropertyNameCaseInsensitive = true
    };

    MotorAyarlari? motorAyarlari =
        JsonSerializer.Deserialize<MotorAyarlari>(
            ayarJson,
            jsonAyarlari);

    if (motorAyarlari is null)
    {
        throw new InvalidOperationException(
            "Motor ayarları okunamadı.");
    }

    AyarlariDogrula(motorAyarlari);

    KonsolKayitcisi.Basari(
        $"Motor ayarları yüklendi. " +
        $"Şirket sayısı: {motorAyarlari.Sirketler.Count}");

    await using SirketYoneticisi sirketYoneticisi =
        new(motorAyarlari);

    await sirketYoneticisi.IlkBaglantilariKurAsync(
        iptalKaynagi.Token);

    TickYoneticisi tickYoneticisi =
        new(
            motorAyarlari,
            sirketYoneticisi);

    await tickYoneticisi.BaslatAsync(
        iptalKaynagi.Token);
}
catch (OperationCanceledException)
{
    KonsolKayitcisi.Bilgi(
        "Motor kullanıcı tarafından durduruldu.");
}
catch (Exception exception)
{
    KonsolKayitcisi.Hata(
        $"Motor çalıştırılamadı: {exception.Message}");

    KonsolKayitcisi.Hata(
        exception.ToString());

    Environment.ExitCode = 1;
}
finally
{
    iptalKaynagi.Dispose();

    KonsolKayitcisi.Bilgi(
        "Motor kapatıldı.");
}

static string AyarDosyasiniBul()
{
    string[] olasiYollar =
    [
        Path.Combine(
            Directory.GetCurrentDirectory(),
            "MotorVerileri",
            "motor-ayarlari.json"),

        Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "MotorVerileri",
            "motor-ayarlari.json"),

        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "MotorVerileri",
            "motor-ayarlari.json")
    ];

    foreach (string yol in olasiYollar)
    {
        string tamYol = Path.GetFullPath(yol);

        if (File.Exists(tamYol))
        {
            return tamYol;
        }
    }

    throw new FileNotFoundException(
        "MotorVerileri/motor-ayarlari.json dosyası bulunamadı.");
}

static void AyarlariDogrula(
    MotorAyarlari ayarlar)
{
    if (string.IsNullOrWhiteSpace(ayarlar.MotorKimligi))
    {
        throw new InvalidOperationException(
            "Motor kimliği boş olamaz.");
    }

    if (ayarlar.TickSuresiSaniye < 1)
    {
        throw new InvalidOperationException(
            "Tick süresi en az 1 saniye olmalıdır.");
    }

    if (ayarlar.Sirketler.Count == 0)
    {
        throw new InvalidOperationException(
            "Motor ayarlarında en az bir şirket bulunmalıdır.");
    }

    HashSet<string> kimlikler =
        new(StringComparer.OrdinalIgnoreCase);

    HashSet<int> portlar = [];

    foreach (SirketBaglantiAyari sirket in ayarlar.Sirketler)
    {
        if (string.IsNullOrWhiteSpace(sirket.SirketKimligi))
        {
            throw new InvalidOperationException(
                "Şirket kimliği boş olamaz.");
        }

        if (!kimlikler.Add(sirket.SirketKimligi))
        {
            throw new InvalidOperationException(
                $"Tekrarlanan şirket kimliği: {sirket.SirketKimligi}");
        }

        if (sirket.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                $"{sirket.SirketAdi} için port geçersiz: {sirket.Port}");
        }

        if (!portlar.Add(sirket.Port))
        {
            throw new InvalidOperationException(
                $"Aynı port birden fazla şirkette kullanılıyor: {sirket.Port}");
        }
    }
}