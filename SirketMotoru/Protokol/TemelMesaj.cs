namespace SirketMotoru.Protokol;

public abstract class TemelMesaj
{
    public string MesajTuru { get; set; } = string.Empty;

    public string MesajKimligi { get; set; } = string.Empty;

    public string ProtokolSurumu { get; set; } = "0.1";
}