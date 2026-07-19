using System.Text;
using System.Text.Json;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

/// <summary>
/// Temiz sezonun ilk açılışında bütün şirketleri eşit başlangıç çizgisine getirir.
/// Yalnız bir kez çalışır; sonraki açılışlarda gerçek oyun ilerlemesine dokunmaz.
/// </summary>
public static class V81AdilBaslangicDengeleyicisi
{
    private const decimal BaslangicKasasi = 50_000m;
    private const double BaslangicPuani = 50d;

    public static async Task UygulaAsync(
        SirketYoneticisi sirketYoneticisi,
        string motorVerileriKlasoru,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sirketYoneticisi);
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);

        string dosyaYolu = Path.Combine(
            Path.GetFullPath(motorVerileriKlasoru),
            "v8.1-adil-baslangic-v1.json");
        if (File.Exists(dosyaYolu))
        {
            KonsolKayitcisi.Bilgi("V8.1 adil başlangıç daha önce uygulanmış; şirket ilerlemesi korunuyor.");
            return;
        }

        foreach (SirketKaydi sirket in sirketYoneticisi.SirketKayitlari)
        {
            sirket.Kasa = BaslangicKasasi;
            sirket.ItibarPuani = BaslangicPuani;
            sirket.GuvenilirlikPuani = BaslangicPuani;
            sirket.KodKalitesiPuani = BaslangicPuani;
            sirket.PerformansPuani = BaslangicPuani;
            sirket.GuvenlikPuani = BaslangicPuani;
            sirket.OrtalamaMusteriMemnuniyeti = BaslangicPuani;
        }

        await sirketYoneticisi.BilancolariKaydetAsync(cancellationToken);

        string json = JsonSerializer.Serialize(
            new
            {
                surum = "8.1",
                uygulamaZamani = DateTimeOffset.Now,
                sirketSayisi = sirketYoneticisi.SirketKayitlari.Count,
                baslangicKasasi = BaslangicKasasi,
                baslangicPuani = BaslangicPuani
            },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

        string gecici = dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(
            gecici,
            json,
            new UTF8Encoding(false),
            cancellationToken);
        File.Move(gecici, dosyaYolu, overwrite: true);

        KonsolKayitcisi.Basari(
            $"V8.1 adil başlangıç uygulandı | Her şirket: {BaslangicKasasi:N2} TL | Taban puan: {BaslangicPuani:N0}");
    }
}
