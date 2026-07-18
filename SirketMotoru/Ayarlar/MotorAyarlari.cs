namespace SirketMotoru.Ayarlar;

public sealed class MotorAyarlari
{
    public string MotorKimligi { get; set; } = "ana-motor";

    public string ProtokolSurumu { get; set; } = "0.1";

    public int TickSuresiSaniye { get; set; } = 10;

    public int BaglantiZamanAsimiMs { get; set; } = 3000;

    public int MesajZamanAsimiMs { get; set; } = 3000;

    public int AzamiMesajBoyutuByte { get; set; } = 65_536;

    public List<SirketBaglantiAyari> Sirketler { get; set; } = [];
}