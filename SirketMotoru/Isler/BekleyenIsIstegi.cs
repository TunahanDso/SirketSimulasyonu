using SirketMotoru.Protokol.Mesajlar;

namespace SirketMotoru.Isler;

public sealed class BekleyenIsIstegi
{
    public required IsIstegiMesaji Istek { get; init; }

    public required string SirketKimligi { get; init; }

    public required TaskCompletionSource<IsSonucuMesaji>
        SonucBekleyicisi { get; init; }

    public DateTimeOffset GonderilmeZamani { get; init; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset ZamanAsimiZamani =>
        GonderilmeZamani.AddMilliseconds(
            Istek.ZamanAsimiMs);

    public bool ZamanAsiminaUgradiMi =>
        DateTimeOffset.UtcNow >=
        ZamanAsimiZamani;
}