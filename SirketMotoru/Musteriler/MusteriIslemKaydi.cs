namespace SirketMotoru.Musteriler;

public sealed class MusteriIslemKaydi
{
    public string IsKimligi { get; set; } = string.Empty;

    public long TickNumarasi { get; set; }

    public string HizmetKimligi { get; set; } = string.Empty;

    public string HizmetSurumu { get; set; } = string.Empty;

    public string SirketKimligi { get; set; } = string.Empty;

    public decimal OdenenTutar { get; set; }

    public bool Basarili { get; set; }

    public double TamamlanmaSuresiMs { get; set; }

    public DateTimeOffset OlusturulmaZamani { get; set; }

    public string SonucAciklamasi { get; set; } = string.Empty;
}