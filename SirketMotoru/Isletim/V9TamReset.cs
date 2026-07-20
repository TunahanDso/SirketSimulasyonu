using System.Text;
using System.Text.Json;

namespace SirketMotoru.Isletim;

/// <summary>
/// V5-V9 döneminden kalan bütün çalışma verisini bir defa arşivleyip temizler.
/// Şirket kaynak kodları, manifestleri ve statik motor ayarları korunur.
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
        "v8.1-adil-baslangic-v1.json",
        "v9-temiz-sezon-1.json"
    ];

    public static async Task UygulaAsync(
        string motorVerileriKlasoru,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);
        string kok = Path.GetFullPath(motorVerileriKlasoru);
        Directory.CreateDirectory(kok);
        string isaret = Path.Combine(kok, "v9.1-temiz-sezon-2.json");
        if (File.Exists(isaret))
        {
            KonsolKayitcisi.Bilgi("V9.1 tam reset daha önce uygulanmış; yeni sezon ilerlemesi korunuyor.");
            return;
        }

        string zaman = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        string arsiv = Path.Combine(kok, "Arsiv", $"V9.1-reset-oncesi-{zaman}");
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

        foreach (string desen in new[] { "*.tmp", "*destek*.json", "*reset-v*.json", "v9-*.json" })
        {
            foreach (string dosya in Directory.EnumerateFiles(kok, desen, SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFullPath(dosya).Equals(Path.GetFullPath(isaret), StringComparison.OrdinalIgnoreCase)) continue;
                string hedef = Path.Combine(arsiv, Path.GetFileName(dosya));
                if (!File.Exists(hedef)) File.Copy(dosya, hedef);
                File.Delete(dosya);
            }
        }

        var kayit = new
        {
            surum = "9.1-temiz-sezon-2",
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
                "şirket kasaları, gelir-gider ve bütün bilanço sayaçları",
                "yatırım seviyeleri, krediler, temerrütler ve ödenemeyen giderler",
                "tek seferlik ve geçici destekler",
                "uygulama, işletim sistemi ve protokol piyasa kayıtları",
                "satın alınmış ve eski tahsis edilmiş bütün kapasite",
                "müşteri işletim sistemi, uygulama, sadakat ve işlem geçmişi",
                "ceza, finans, haber, arz-talep ve tick geçmişi"
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
            $"V9.1 TAM RESET tamamlandı | Bilanço, yatırım, borç, destek ve pazar kalıntıları silindi | Arşiv: {arsiv}");
    }
}
