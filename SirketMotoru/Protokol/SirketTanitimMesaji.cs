using SirketMotoru.Hizmetler;

namespace SirketMotoru.Protokol;

public sealed class SirketTanitimMesaji
{
    public string MesajTuru { get; init; } =
        MesajTurleri.SirketTanitim;

    public string MesajKimligi { get; init; } =
        string.Empty;

    public string ProtokolSurumu { get; init; } =
        string.Empty;

    public string SirketKimligi { get; init; } =
        string.Empty;

    public string SirketAdi { get; init; } =
        string.Empty;

    public string SunucuSurumu { get; init; } =
        string.Empty;

    public List<SunulanHizmet> Hizmetler { get; init; } =
        [];
}