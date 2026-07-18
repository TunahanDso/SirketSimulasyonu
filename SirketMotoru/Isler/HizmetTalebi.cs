namespace SirketMotoru.Isler;

public sealed class HizmetTalebi
{
    public string IsKimligi { get; init; } =
        $"is-{Guid.NewGuid():N}";

    public string MusteriKimligi { get; init; } =
        string.Empty;

    public string HizmetKimligi { get; init; } =
        string.Empty;

    public string HizmetSurumu { get; init; } =
        "1.0";

    public long OlusturulmaTicki { get; init; }

    public decimal AzamiButce { get; init; }

    public string IstekVerisiJson { get; init; } =
        "{}";

    public int ZamanAsimiMs { get; init; } =
        5_000;

    public IsDurumu Durum { get; set; } =
        IsDurumu.Olusturuldu;

    public string? SecilenSirketKimligi { get; set; }

    public decimal TeklifEdilenTutar { get; set; }

    public DateTimeOffset OlusturulmaZamani { get; init; } =
        DateTimeOffset.UtcNow;
}