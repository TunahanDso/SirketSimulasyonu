using System.Text;
using System.Text.Json;
using SirketMotoru.Kayit;

namespace SirketMotoru.Isletim;

/// <summary>
/// V5-V8 döneminden kalan bilanço, borç, destek, ceza, kapasite ve pazar
/// dosyalarını bir defa arşivleyip temizler. Şirket kodları ve statik motor
/// ayarları korunur; bütün çalışma verisi yeniden üretilir.
/// </summary>
public static class V9TamReset
{
    private static readonly string[] CalismaDosyalari =
    [
        "musteriler.json",
        "sirket-bilancolari.json",
        "sirket-isletim.json",
        "kod-tabanli-yayinlar.json",
        "ekosistem.json",
        "ekonomi-v6.json",
        "finans-v7.json",
        "pazar-fiyat.json",
        "pazar-v9.json",
        "ceza-v8.json",
        "tick-saat.json",
        "tek-seferlik-destekler.json",
        "v8.1-tam-sirket-reset-v2.json",
        "v8.1-adil-baslangic-v1.json"
    ];

    public static async Task UygulaAsync(
        string motorVerileriKlasoru,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);
        string kok = Path.GetFullPath(motorVerileriKlasoru);
        Directory.CreateDirectory(kok);
        string isaret = Path.Combine(kok, "v9-temiz-sezon-1.json");
        if (File.Exists(isaret))
        {
            KonsolKayitcisi.Bilgi("V9 tam sezon reseti daha önce uygulanmış; çalışma verileri korunuyor.");
            return;
        }

        string zaman = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        string arsiv = Path.Combine(kok, "Arsiv", $"V9-reset-oncesi-{zaman}");
        Directory.CreateDirectory(arsiv);

        foreach (string ad in CalismaDosyalari)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string kaynak = Path.Combine(kok, ad);
            if (!File.Exists(kaynak)) continue;
            File.Copy(kaynak, Path.Combine(arsiv, ad), overwrite: true);
            File.Delete(kaynak);
            string tmp = kaynak + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
        }

        // Önceki sürümlerin çalışma artıkları da yeni sezona taşınmaz.
        foreach (string desen in new[] { "*.tmp", "*destek*.json", "*reset-v*.json" })
        {
            foreach (string dosya in Directory.EnumerateFiles(kok, desen, SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(dosya).Equals(Path.GetFileName(isaret), StringComparison.OrdinalIgnoreCase))
                    continue;
                File.Delete(dosya);
            }
        }

        var kayit = new
        {
            surum = "9.0-temiz-sezon-1",
            uygulamaZamani = DateTimeOffset.Now,
            arsiv,
            korunanlar = new[]
            {
                "motor-ayarlari.json",
                "hizmet-katalogu.json",
                "şirket sunucu kodları ve manifestleri"
            },
            sifirlananlar = new[]
            {
                "şirket kasaları ve bütün bilanço sayaçları",
                "krediler, temerrütler ve ödenemeyen giderler",
                "tek seferlik destekler",
                "uygulama, işletim sistemi ve protokol piyasa kayıtları",
                "satın alınmış veya eski tahsis edilmiş kapasite",
                "müşteri OS, uygulama, sadakat ve işlem geçmişi",
                "ceza, finans, pazar, haber ve tick geçmişi"
            }
        };
        string gecici = isaret + ".tmp";
        await File.WriteAllTextAsync(
            gecici,
            JsonSerializer.Serialize(kayit, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false),
            cancellationToken);
        File.Move(gecici, isaret, overwrite: true);

        KonsolKayitcisi.Basari(
            $"V9 TAM RESET tamamlandı | Eski bilanço, borç, destek ve pazar kalıntıları silindi | Arşiv: {arsiv}");
    }
}
