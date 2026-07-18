namespace SirketMotoru.Protokol;

public sealed class SirketTanitimMesaji : TemelMesaj
{
    public string SirketKimligi { get; set; } = string.Empty;

    public string SirketAdi { get; set; } = string.Empty;

    public string SunucuSurumu { get; set; } = string.Empty;
}