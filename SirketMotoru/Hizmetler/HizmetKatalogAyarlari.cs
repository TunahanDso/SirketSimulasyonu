namespace SirketMotoru.Hizmetler;

public sealed class HizmetKatalogAyarlari
{
    public string KatalogSurumu { get; set; } = "1.0";

    public List<HizmetTanimi> Hizmetler { get; set; } = [];
}