using System.Reflection;
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

    public static async Task KaliciEzmeKayitlariniTemizleVeUygulaAsync(
        KodTabanliSirketIsletimYoneticisi isletim,
        IEnumerable<SirketKaydi> sirketler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(isletim);
        ArgumentNullException.ThrowIfNull(sirketler);

        FieldInfo temelAlani = typeof(KodTabanliSirketIsletimYoneticisi)
            .GetField("_temel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Sabit fiyat temel yönetici alanı bulunamadı.");
        object temel = temelAlani.GetValue(isletim)
            ?? throw new InvalidOperationException("Sabit fiyat temel yöneticisi boş.");
        Type tur = temel.GetType();
        FieldInfo veriAlani = tur.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Sabit fiyat işletim verisi bulunamadı.");
        FieldInfo kilitAlani = tur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Sabit fiyat işletim kilidi bulunamadı.");
        MethodInfo kaydetMetodu = tur.GetMethod("TumunuKaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Sabit fiyat kayıt metodu bulunamadı.");

        SemaphoreSlim kilit = (SemaphoreSlim)(kilitAlani.GetValue(temel)
            ?? throw new InvalidOperationException("Sabit fiyat kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try
        {
            SirketIsletimDosyasi veri = (SirketIsletimDosyasi)(veriAlani.GetValue(temel)
                ?? throw new InvalidOperationException("Sabit fiyat verisi boş."));
            Dictionary<string, SirketKaydi> sirketIndeksi = sirketler
                .ToDictionary(x => x.SirketKimligi, StringComparer.OrdinalIgnoreCase);

            foreach (SirketIsletimDurumu durum in veri.Sirketler)
            {
                durum.HizmetAyarlari ??= new(StringComparer.OrdinalIgnoreCase);
                durum.HizmetFiyatEzmeDegerleri ??= new(StringComparer.OrdinalIgnoreCase);
                if (!sirketIndeksi.TryGetValue(durum.SirketKimligi, out SirketKaydi? sirket))
                    continue;

                foreach (SunulanHizmet hizmet in sirket.Hizmetler)
                {
                    string anahtar = $"{hizmet.HizmetKimligi.Trim()}@{hizmet.HizmetSurumu.Trim()}";
                    decimal fiyat = Fiyat(hizmet.HizmetKimligi, hizmet.HizmetSurumu);
                    hizmet.BirimFiyat = fiyat;
                    durum.HizmetFiyatEzmeDegerleri[anahtar] = fiyat;
                    if (!durum.HizmetAyarlari.TryGetValue(anahtar, out HizmetKaliciAyari? ayar))
                        continue;
                    ayar.SunucudanGelenIlkFiyat = fiyat;
                    ayar.YonetilenFiyat = fiyat;
                    ayar.FiyatYonetildi = false;
                }
            }

            object? sonuc = kaydetMetodu.Invoke(temel, [cancellationToken]);
            if (sonuc is Task gorev) await gorev;
        }
        finally
        {
            kilit.Release();
        }
    }
}
