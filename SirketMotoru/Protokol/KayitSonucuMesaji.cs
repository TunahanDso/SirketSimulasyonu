namespace SirketMotoru.Protokol;

public sealed class KayitSonucuMesaji : TemelMesaj
{
    public bool Basarili { get; set; }

    public string SirketKimligi { get; set; } = string.Empty;

    public string Aciklama { get; set; } = string.Empty;
}