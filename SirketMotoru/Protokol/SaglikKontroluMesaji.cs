namespace SirketMotoru.Protokol;

public sealed class SaglikKontroluMesaji : TemelMesaj
{
    public string IstekKimligi { get; set; } = string.Empty;

    public long TickNumarasi { get; set; }
}