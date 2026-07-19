using SirketMotoru.Protokol;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

/// <summary>
/// Standart hizmet fiyatlarının tek otoritesidir. Şirket manifesti veya 8090
/// hizmet fiyatını değiştiremez. Aynı hizmet bütün şirketlerde aynı fiyattadır.
/// </summary>
public static class MotorHizmetFiyatlari
{
    private static readonly IReadOnlyDictionary<string, decimal> AileTabanFiyatlari =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["matematik"] = 12m,
            ["metin"] = 10m,
            ["dizi"] = 11m,
            ["veri"] = 16m,
            ["arama"] = 20m,
            ["bildirim"] = 14m,
            ["eposta"] = 22m,
            ["sosyal"] = 24m,
            ["mesajlasma"] = 23m,
            ["profil"] = 18m,
            ["kimlik"] = 30m,
            ["guvenlik"] = 48m,
            ["isletim"] = 60m,
            ["dosya"] = 20m,
            ["veritabani"] = 42m,
            ["odeme"] = 50m,
            ["ticaret"] = 38m,
            ["medya"] = 36m,
            ["harita"] = 30m,
            ["takvim"] = 22m,
            ["analitik"] = 46m,
            ["gelistirme"] = 44m,
            ["yapay-zeka"] = 85m,
            ["cihaz"] = 40m,
            ["sektor"] = 52m
        };

    public static decimal Fiyat(string? hizmetKimligi, string? hizmetSurumu = "1.0")
    {
        string kimlik = hizmetKimligi?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(kimlik)) return 10m;

        string aile = kimlik.Split('.', 2)[0];
        decimal taban = AileTabanFiyatlari.TryGetValue(aile, out decimal bulunan)
            ? bulunan
            : 25m;

        uint karma = 2166136261;
        foreach (char karakter in $"{kimlik}@{hizmetSurumu?.Trim() ?? "1.0"}")
        {
            karma ^= karakter;
            karma *= 16777619;
        }

        decimal carpan = 0.85m + karma % 41 / 100m;
        if (kimlik.Contains("video", StringComparison.OrdinalIgnoreCase) ||
            kimlik.Contains("saldiri", StringComparison.OrdinalIgnoreCase) ||
            kimlik.Contains("sanal-makine", StringComparison.OrdinalIgnoreCase) ||
            kimlik.Contains("model", StringComparison.OrdinalIgnoreCase))
        {
            carpan += 0.25m;
        }

        return decimal.Round(Math.Max(5m, taban * carpan), 2);
    }

    public static void Uygula(IEnumerable<SirketKaydi> sirketler)
    {
        ArgumentNullException.ThrowIfNull(sirketler);
        foreach (SirketKaydi sirket in sirketler)
        {
            foreach (SunulanHizmet hizmet in sirket.Hizmetler)
            {
                hizmet.BirimFiyat = Fiyat(hizmet.HizmetKimligi, hizmet.HizmetSurumu);
            }
        }
    }
}
