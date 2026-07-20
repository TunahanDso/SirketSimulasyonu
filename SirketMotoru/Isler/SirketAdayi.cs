using SirketMotoru.Ag;
using SirketMotoru.Protokol;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isler;

public sealed class SirketAdayi
{
    public required SirketBaglantisi Baglanti { get; init; }

    public required SirketKaydi Sirket { get; init; }

    public required SunulanHizmet Hizmet { get; init; }

    public double FiyatPuani { get; init; }

    public double ItibarPuani { get; init; }

    public double GuvenilirlikPuani { get; init; }

    public double KodKalitesiPuani { get; init; }

    public double PerformansPuani { get; init; }

    public double GuvenlikPuani { get; init; }

    public double HizPuani { get; init; }

    public double KapasitePuani { get; init; }

    public double SadakatPuani { get; init; }

    public double ToplamPuan { get; init; }
}
