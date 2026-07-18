namespace SirketMotoru.Protokol;

public sealed class SunulanHizmet
{
    public string HizmetKimligi { get; set; } = string.Empty;

    public string HizmetSurumu { get; set; } = string.Empty;

    public decimal BirimFiyat { get; set; }

    public int AzamiEszamanliIs { get; set; }

    public bool Aktif { get; set; } = true;
}