namespace SirketMotoru.Ayarlar;

public sealed class MotorAyarlari
{
    public string MotorKimligi { get; set; } = "ana-motor";

    public string ProtokolSurumu { get; set; } = "0.1";

    public int TickSuresiSaniye { get; set; } = 10;

    public int BaglantiZamanAsimiMs { get; set; } = 3000;

    public int MesajZamanAsimiMs { get; set; } = 3000;

    public int AzamiMesajBoyutuByte { get; set; } = 65_536;

    public bool CanliPanoAktif { get; set; } = true;

    public int CanliPanoPortu { get; set; } = 8080;

    public int CanliPanoYenilemeMs { get; set; } = 1_000;

    public bool SirketYonetimAktif { get; set; } = true;

    public int SirketYonetimPortu { get; set; } = 8_090;

    public int SirketYonetimOturumDakika { get; set; } = 180;

    public List<SirketBaglantiAyari> Sirketler { get; set; } = [];
}
