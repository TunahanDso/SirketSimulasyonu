namespace SirketMotoru.Protokol;

public sealed class SaglikSonucuMesaji : TemelMesaj
{
    public string IstekKimligi { get; set; } = string.Empty;

    public string Durum { get; set; } = string.Empty;

    public int AktifBaglanti { get; set; }

    public int KuyrukUzunlugu { get; set; }
}