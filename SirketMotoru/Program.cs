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
KonsolKayitcisi.Bilgi("Üç Kardeş Yazılım Şirketi Simülasyonu V9 başlatılıyor.");
using CancellationTokenSource iptalKaynagi = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    KonsolKayitcisi.Uyari("Motor kapatma sinyali aldı.");
    iptalKaynagi.Cancel();
};

try
{
    JsonSerializerOptions jsonAyarlari = new() { PropertyNameCaseInsensitive = true };
    string ayarYolu = VeriDosyasiniBul("motor-ayarlari.json");
    string motorVerileri = Path.GetDirectoryName(ayarYolu)
        ?? throw new InvalidOperationException("MotorVerileri klasörü belirlenemedi.");
    KonsolKayitcisi.Bilgi($"Motor veri klasörü: {motorVerileri}");

    MotorAyarlari ayarlar = await JsonDosyasiniOkuAsync<MotorAyarlari>(
        ayarYolu,
        jsonAyarlari,
        iptalKaynagi.Token);
    MotorAyarlariDogrulayicisi.Dogrula(ayarlar);

    // Bütün eski bilanço, borç, destek, müşteri tercihi ve pazar dosyaları
    // yöneticiler kurulmadan önce bir defa arşivlenip temizlenir.
    await V9TamReset.UygulaAsync(motorVerileri, iptalKaynagi.Token);

    string katalogYolu = Path.Combine(motorVerileri, "hizmet-katalogu.json");
    if (!File.Exists(katalogYolu)) katalogYolu = VeriDosyasiniBul("hizmet-katalogu.json");
    HizmetKatalogAyarlari tohum = await JsonDosyasiniOkuAsync<HizmetKatalogAyarlari>(
        katalogYolu,
        jsonAyarlari,
        iptalKaynagi.Token);
    HizmetKatalogu katalog = new(StandartKatalogV6.Genislet(tohum));
    KonsolKayitcisi.Basari(
        $"V9 hizmet kataloğu hazır | Aktif hizmet: {katalog.Hizmetler.Count(x => x.Aktif):N0} | " +
        $"Kategori: {StandartKatalogV6.UygulamaKategorileri.Count:N0}");

    MusteriVeritabani musteriDb = new(Path.Combine(motorVerileri, "musteriler.json"));
    await using MusteriYoneticisi musteriler = new(musteriDb, katalog);
    await musteriler.BaslatAsync(iptalKaynagi.Token);

    await using SirketYoneticisi sirketler = new(ayarlar, katalog);
    await TickSaatDeposu.BaslatAsync(motorVerileri, iptalKaynagi.Token);

    await using KodTabanliSirketIsletimYoneticisi isletim = new(sirketler, motorVerileri);
    await isletim.BaslatAsync(iptalKaynagi.Token);

    await using EkosistemYoneticisi ekosistem = new(sirketler, isletim, motorVerileri);
    await ekosistem.BaslatAsync(iptalKaynagi.Token);

    await using V9EkonomiYoneticisi v9 = new(sirketler, musteriler, katalog, isletim, motorVerileri);
    await v9.BaslatAsync(iptalKaynagi.Token);

    TickYoneticisi tick = new(ayarlar, sirketler, musteriler, isletim, ekosistem, v9);

    await using YazilimBorsasiSunucusu borsa = new(
        ayarlar,
        sirketler,
        musteriler,
        katalog,
        isletim,
        v9,
        () => tick.TickNumarasi);
    await using SirketYonetimSunucusu yonetim = new(
        ayarlar,
        sirketler,
        katalog,
        isletim,
        ekosistem,
        v9);

    await borsa.BaslatAsync(iptalKaynagi.Token);
    await yonetim.BaslatAsync(iptalKaynagi.Token);
    await sirketler.IlkBaglantilariKurAsync(iptalKaynagi.Token);
    await MotorHizmetFiyatlari.KaliciEzmeKayitlariniTemizleVeUygulaAsync(
        isletim,
        sirketler.SirketKayitlari,
        iptalKaynagi.Token);

    KonsolKayitcisi.Basari(
        "V9 TEMİZ MOTOR HAZIR | Eşit başlangıç, sabit hizmet fiyatı, tek kapasite havuzu, " +
        "trendli 20.000 müşteri pazarı, hafif gider ve otomatik kredisi olmayan ekonomi aktif.");
    await tick.BaslatAsync(iptalKaynagi.Token);
}
catch (OperationCanceledException)
{
    KonsolKayitcisi.Bilgi("Motor kullanıcı tarafından durduruldu.");
}
catch (Exception hata)
{
    KonsolKayitcisi.Hata($"Motor çalıştırılamadı: {hata.Message}");
    KonsolKayitcisi.Hata(hata.ToString());
    Environment.ExitCode = 1;
}
finally
{
    KonsolKayitcisi.Bilgi("Motor kapatıldı.");
}

static async Task<T> JsonDosyasiniOkuAsync<T>(
    string yol,
    JsonSerializerOptions ayarlar,
    CancellationToken ct)
{
    if (!File.Exists(yol)) throw new FileNotFoundException($"JSON dosyası bulunamadı: {yol}", yol);
    string json = await File.ReadAllTextAsync(yol, ct);
    return JsonSerializer.Deserialize<T>(json, ayarlar)
        ?? throw new InvalidOperationException($"{Path.GetFileName(yol)} okunamadı.");
}

static string VeriDosyasiniBul(string ad)
{
    string[] yollar =
    [
        Path.Combine(Directory.GetCurrentDirectory(), "MotorVerileri", ad),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "MotorVerileri", ad),
        Path.Combine(AppContext.BaseDirectory, "MotorVerileri", ad),
        Path.Combine(AppContext.BaseDirectory, "..", "MotorVerileri", ad),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "MotorVerileri", ad),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MotorVerileri", ad)
    ];
    foreach (string yol in yollar.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        if (File.Exists(yol)) return yol;
    throw new FileNotFoundException($"{ad} bulunamadı. Denenen yollar: {string.Join(" | ", yollar.Select(Path.GetFullPath))}");
}
