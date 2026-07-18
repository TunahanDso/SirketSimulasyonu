namespace SirketMotoru.Kayit;

public static class KonsolKayitcisi
{
    private static readonly object Kilit = new();

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
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine();
            Console.WriteLine($"================ TICK {tickNumarasi} ================");
            Console.ResetColor();
        }
    }

    private static void Yaz(
        string seviye,
        string mesaj,
        ConsoleColor renk)
    {
        lock (Kilit)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{DateTime.Now:HH:mm:ss}] ");

            Console.ForegroundColor = renk;
            Console.Write($"[{seviye}] ");

            Console.ResetColor();
            Console.WriteLine(mesaj);
        }
    }
}