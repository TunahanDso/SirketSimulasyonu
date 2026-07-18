namespace SirketMotoru.Hizmetler;

public sealed class HizmetTanimi
{
    public string HizmetKimligi { get; set; } = string.Empty;

    public string HizmetSurumu { get; set; } = string.Empty;

    public string Aciklama { get; set; } = string.Empty;

    public int ZamanAsimiMs { get; set; }

    public int AzamiIstekBoyutuByte { get; set; }

    public bool Aktif { get; set; } = true;
}