using System.Collections.Concurrent;

namespace SirketMotoru.Isletim;

public sealed class V9HizmetGerceklesme
{
    public string HizmetAnahtari { get; set; } = string.Empty;
    public string SirketKimligi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public int Basarili { get; set; }
    public int Basarisiz { get; set; }
    public decimal ToplamOdeme { get; set; }
    public int Toplam => Basarili + Basarisiz;
}

public static class V9HizmetSonucDeposu
{
    private static readonly object TickKilidi = new();
    private static readonly ConcurrentDictionary<string, V9HizmetGerceklesme> Kayitlar =
        new(StringComparer.OrdinalIgnoreCase);
    private static long _tick;

    public static void TickBaslat(long tick)
    {
        lock (TickKilidi)
        {
            if (_tick == tick) return;
            _tick = tick;
            Kayitlar.Clear();
        }
    }

    public static void Kaydet(
        string hizmetAnahtari,
        string sirketKimligi,
        string sirketAdi,
        bool basarili,
        decimal odeme)
    {
        string anahtar = $"{hizmetAnahtari}|{sirketKimligi}";
        V9HizmetGerceklesme kayit = Kayitlar.GetOrAdd(anahtar, _ => new V9HizmetGerceklesme
        {
            HizmetAnahtari = hizmetAnahtari,
            SirketKimligi = sirketKimligi,
            SirketAdi = sirketAdi
        });
        lock (kayit)
        {
            if (basarili) kayit.Basarili++;
            else kayit.Basarisiz++;
            kayit.ToplamOdeme += Math.Max(0, odeme);
        }
    }

    public static IReadOnlyList<V9HizmetGerceklesme> Getir() =>
        Kayitlar.Values.Select(x => new V9HizmetGerceklesme
        {
            HizmetAnahtari = x.HizmetAnahtari,
            SirketKimligi = x.SirketKimligi,
            SirketAdi = x.SirketAdi,
            Basarili = x.Basarili,
            Basarisiz = x.Basarisiz,
            ToplamOdeme = x.ToplamOdeme
        }).ToList().AsReadOnly();
}
