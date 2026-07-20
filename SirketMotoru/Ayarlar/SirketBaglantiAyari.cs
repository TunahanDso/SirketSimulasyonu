namespace SirketMotoru.Ayarlar;

public sealed class SirketBaglantiAyari
{
    public string SirketKimligi { get; set; } = string.Empty;

    public string SirketAdi { get; set; } = string.Empty;

    public string Adres { get; set; } = "127.0.0.1";

    public int Port { get; set; }

    // Bu değerler yalnız şirketin henüz kalıcı bilanço kaydı yoksa uygulanır.
    // Böylece mevcut şirketlerin geçmişi korunurken oyuna sonradan eklenen
    // şirketler kontrollü bir başlangıç profiliyle açılabilir.
    public decimal BaslangicKasasi { get; set; }

    public double? BaslangicItibarPuani { get; set; }

    public double? BaslangicGuvenilirlikPuani { get; set; }

    public double? BaslangicKodKalitesiPuani { get; set; }

    public double? BaslangicPerformansPuani { get; set; }

    public double? BaslangicGuvenlikPuani { get; set; }

    public double? BaslangicMusteriMemnuniyeti { get; set; }
}
