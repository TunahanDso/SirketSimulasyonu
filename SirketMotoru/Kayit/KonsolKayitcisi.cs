namespace SirketMotoru.Kayit;

public static class KonsolKayitcisi
{
    private const int AzamiKayitSayisi =
        500;

    private static readonly object Kilit =
        new();

    private static readonly Queue<KonsolKaydi>
        SonKayitlar =
            new();

    public static void Bilgi(string mesaj)
    {
        Yaz("BILGI", mesaj, ConsoleColor.Cyan);
    }

    public static void Basari(string mesaj)
    {
        Yaz("BASARILI", mesaj, ConsoleColor.Green);
    }

    public static void Uyari(string mesaj)
    {
        Yaz("UYARI", mesaj, ConsoleColor.Yellow);
    }

    public static void Hata(string mesaj)
    {
        Yaz("HATA", mesaj, ConsoleColor.Red);
    }

    public static void Tick(long tickNumarasi)
    {
        lock (Kilit)
        {
            KayitEkle(
                new KonsolKaydi
                {
                    Zaman =
                        DateTimeOffset.Now,

                    Seviye =
                        "TICK",

                    Mesaj =
                        $"Tick {tickNumarasi} başladı.",

                    TickNumarasi =
                        tickNumarasi
                });

            Console.ForegroundColor =
                ConsoleColor.Magenta;

            Console.WriteLine();

            Console.WriteLine(
                $"================ TICK " +
                $"{tickNumarasi} ================");

            Console.ResetColor();
        }
    }

    public static IReadOnlyList<KonsolKaydi>
        SonKayitlariGetir(
            int azamiKayitSayisi = 250)
    {
        int sinir =
            Math.Clamp(
                azamiKayitSayisi,
                1,
                AzamiKayitSayisi);

        lock (Kilit)
        {
            return SonKayitlar
                .TakeLast(
                    sinir)
                .ToList()
                .AsReadOnly();
        }
    }

    private static void Yaz(
        string seviye,
        string mesaj,
        ConsoleColor renk)
    {
        ArgumentNullException.ThrowIfNull(
            mesaj);

        lock (Kilit)
        {
            DateTimeOffset zaman =
                DateTimeOffset.Now;

            KayitEkle(
                new KonsolKaydi
                {
                    Zaman =
                        zaman,

                    Seviye =
                        seviye,

                    Mesaj =
                        mesaj
                });

            Console.ForegroundColor =
                ConsoleColor.DarkGray;

            Console.Write(
                $"[{zaman:HH:mm:ss}] ");

            Console.ForegroundColor =
                renk;

            Console.Write(
                $"[{seviye}] ");

            Console.ResetColor();

            Console.WriteLine(
                mesaj);
        }
    }

    private static void KayitEkle(
        KonsolKaydi kayit)
    {
        SonKayitlar.Enqueue(
            kayit);

        while (SonKayitlar.Count >
               AzamiKayitSayisi)
        {
            SonKayitlar.Dequeue();
        }
    }
}

public sealed class KonsolKaydi
{
    public DateTimeOffset Zaman { get; init; }

    public string Seviye { get; init; } = string.Empty;

    public string Mesaj { get; init; } = string.Empty;

    public long? TickNumarasi { get; init; }
}
