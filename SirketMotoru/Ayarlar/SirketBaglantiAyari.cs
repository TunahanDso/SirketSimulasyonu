namespace SirketMotoru.Ayarlar;

public sealed class SirketBaglantiAyari
{
    public string SirketKimligi { get; set; } = string.Empty;

    public string SirketAdi { get; set; } = string.Empty;

    public string Adres { get; set; } = "127.0.0.1";

    public int Port { get; set; }
}