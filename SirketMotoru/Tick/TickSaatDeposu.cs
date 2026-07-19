using System.Text;
using System.Text.Json;
using SirketMotoru.Kayit;

namespace SirketMotoru.Tick;

public sealed class TickSaatKaydi
{
    public int Surum { get; set; } = 1;
    public long SonTamamlananTick { get; set; }
    public DateTimeOffset GuncellenmeZamani { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Tick numarasının tek kalıcı otoritesidir. Finans, kredi veya panel dosyasından
/// tick tahmin edilmez. Yalnız başarıyla tamamlanan tick atomik olarak kaydedilir.
/// </summary>
public static class TickSaatDeposu
{
    private static readonly SemaphoreSlim Kilit = new(1, 1);
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static string _dosyaYolu = string.Empty;
    private static long _sonTamamlananTick;
    private static bool _baslatildi;

    public static long SonTamamlananTick => Interlocked.Read(ref _sonTamamlananTick);

    public static async Task BaslatAsync(
        string motorVerileriKlasoru,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);
        await Kilit.WaitAsync(cancellationToken);
        try
        {
            if (_baslatildi) return;
            _dosyaYolu = Path.Combine(Path.GetFullPath(motorVerileriKlasoru), "tick-saat.json");
            long tick = 0;
            if (File.Exists(_dosyaYolu))
            {
                string json = await File.ReadAllTextAsync(_dosyaYolu, cancellationToken);
                TickSaatKaydi? kayit = JsonSerializer.Deserialize<TickSaatKaydi>(json, JsonAyarlari);
                tick = Math.Max(0, kayit?.SonTamamlananTick ?? 0);
            }
            Interlocked.Exchange(ref _sonTamamlananTick, tick);
            _baslatildi = true;
            KonsolKayitcisi.Basari($"Kalıcı tick saati hazır | Son tamamlanan tick: {tick}");
        }
        finally { Kilit.Release(); }
    }

    public static async Task TamamlandiAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        if (!_baslatildi) throw new InvalidOperationException("Tick saat deposu başlatılmadı.");
        if (tickNumarasi < 0) throw new ArgumentOutOfRangeException(nameof(tickNumarasi));

        await Kilit.WaitAsync(cancellationToken);
        try
        {
            long mevcut = SonTamamlananTick;
            if (tickNumarasi <= mevcut) return;
            TickSaatKaydi kayit = new()
            {
                SonTamamlananTick = tickNumarasi,
                GuncellenmeZamani = DateTimeOffset.UtcNow
            };
            string json = JsonSerializer.Serialize(kayit, JsonAyarlari);
            Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
            string gecici = _dosyaYolu + ".tmp";
            await File.WriteAllTextAsync(
                gecici,
                json,
                new UTF8Encoding(false),
                cancellationToken);
            File.Move(gecici, _dosyaYolu, overwrite: true);
            Interlocked.Exchange(ref _sonTamamlananTick, tickNumarasi);
        }
        finally { Kilit.Release(); }
    }
}
