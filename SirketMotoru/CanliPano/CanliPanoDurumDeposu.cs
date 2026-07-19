using SirketMotoru.Isler;

namespace SirketMotoru.CanliPano;

public static class CanliPanoDurumDeposu
{
    private static readonly object Kilit = new();

    private static PanoPiyasaDurumu _durum =
        new();

    public static void TalepleriGuncelle(
        long tickNumarasi,
        IReadOnlyList<HizmetTalebi> talepler)
    {
        ArgumentNullException.ThrowIfNull(
            talepler);

        List<PanoTalepGrubu> gruplar =
            talepler
                .GroupBy(
                    talep => new
                    {
                        talep.HizmetKimligi,
                        talep.HizmetSurumu
                    })
                .Select(
                    grup =>
                        new PanoTalepGrubu
                        {
                            HizmetKimligi =
                                grup.Key.HizmetKimligi,

                            HizmetSurumu =
                                grup.Key.HizmetSurumu,

                            TalepSayisi =
                                grup.Count(),

                            ToplamAzamiButce =
                                grup.Sum(
                                    talep =>
                                        talep.AzamiButce),

                            OrtalamaAzamiButce =
                                grup.Any()
                                    ? grup.Average(
                                        talep =>
                                            talep.AzamiButce)
                                    : 0,

                            EnYuksekAzamiButce =
                                grup.Any()
                                    ? grup.Max(
                                        talep =>
                                            talep.AzamiButce)
                                    : 0
                        })
                .OrderByDescending(
                    grup => grup.TalepSayisi)
                .ThenBy(
                    grup => grup.HizmetKimligi,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        lock (Kilit)
        {
            _durum =
                new PanoPiyasaDurumu
                {
                    TickNumarasi =
                        tickNumarasi,

                    GuncellenmeZamani =
                        DateTimeOffset.UtcNow,

                    ToplamTalepSayisi =
                        talepler.Count,

                    ToplamTalepButcesi =
                        talepler.Sum(
                            talep => talep.AzamiButce),

                    TalepGruplari =
                        gruplar,

                    IslemeOzeti =
                        _durum.IslemeOzeti?.TickNumarasi ==
                        tickNumarasi
                            ? _durum.IslemeOzeti
                            : null
                };
        }
    }

    public static void IslemeOzetiniGuncelle(
        IsIslemeOzeti ozet)
    {
        ArgumentNullException.ThrowIfNull(
            ozet);

        PanoIslemeOzeti panoOzeti =
            new()
            {
                TickNumarasi =
                    ozet.TickNumarasi,

                ToplamTalepSayisi =
                    ozet.ToplamTalepSayisi,

                BasariliIsSayisi =
                    ozet.BasariliIsSayisi,

                BasarisizIsSayisi =
                    ozet.BasarisizIsSayisi,

                ZamanAsimiSayisi =
                    ozet.ZamanAsimiSayisi,

                SirketBulunamayanIsSayisi =
                    ozet.SirketBulunamayanIsSayisi,

                ButceYetersizIsSayisi =
                    ozet.ButceYetersizIsSayisi,

                ToplamCiro =
                    ozet.ToplamCiro,

                ToplamIslemSuresiMs =
                    ozet.ToplamIslemSuresiMs,

                BasariOrani =
                    ozet.BasariOrani
            };

        lock (Kilit)
        {
            _durum =
                new PanoPiyasaDurumu
                {
                    TickNumarasi =
                        Math.Max(
                            _durum.TickNumarasi,
                            ozet.TickNumarasi),

                    GuncellenmeZamani =
                        DateTimeOffset.UtcNow,

                    ToplamTalepSayisi =
                        _durum.ToplamTalepSayisi,

                    ToplamTalepButcesi =
                        _durum.ToplamTalepButcesi,

                    TalepGruplari =
                        _durum.TalepGruplari
                            .Select(Kopyala)
                            .ToList(),

                    IslemeOzeti =
                        panoOzeti
                };
        }
    }

    public static PanoPiyasaDurumu Getir()
    {
        lock (Kilit)
        {
            return new PanoPiyasaDurumu
            {
                TickNumarasi =
                    _durum.TickNumarasi,

                GuncellenmeZamani =
                    _durum.GuncellenmeZamani,

                ToplamTalepSayisi =
                    _durum.ToplamTalepSayisi,

                ToplamTalepButcesi =
                    _durum.ToplamTalepButcesi,

                TalepGruplari =
                    _durum.TalepGruplari
                        .Select(Kopyala)
                        .ToList(),

                IslemeOzeti =
                    _durum.IslemeOzeti is null
                        ? null
                        : Kopyala(
                            _durum.IslemeOzeti)
            };
        }
    }

    private static PanoTalepGrubu Kopyala(
        PanoTalepGrubu kaynak)
    {
        return new PanoTalepGrubu
        {
            HizmetKimligi =
                kaynak.HizmetKimligi,

            HizmetSurumu =
                kaynak.HizmetSurumu,

            TalepSayisi =
                kaynak.TalepSayisi,

            ToplamAzamiButce =
                kaynak.ToplamAzamiButce,

            OrtalamaAzamiButce =
                kaynak.OrtalamaAzamiButce,

            EnYuksekAzamiButce =
                kaynak.EnYuksekAzamiButce
        };
    }

    private static PanoIslemeOzeti Kopyala(
        PanoIslemeOzeti kaynak)
    {
        return new PanoIslemeOzeti
        {
            TickNumarasi =
                kaynak.TickNumarasi,

            ToplamTalepSayisi =
                kaynak.ToplamTalepSayisi,

            BasariliIsSayisi =
                kaynak.BasariliIsSayisi,

            BasarisizIsSayisi =
                kaynak.BasarisizIsSayisi,

            ZamanAsimiSayisi =
                kaynak.ZamanAsimiSayisi,

            SirketBulunamayanIsSayisi =
                kaynak.SirketBulunamayanIsSayisi,

            ButceYetersizIsSayisi =
                kaynak.ButceYetersizIsSayisi,

            ToplamCiro =
                kaynak.ToplamCiro,

            ToplamIslemSuresiMs =
                kaynak.ToplamIslemSuresiMs,

            BasariOrani =
                kaynak.BasariOrani
        };
    }
}

public sealed class PanoPiyasaDurumu
{
    public long TickNumarasi { get; init; }

    public DateTimeOffset GuncellenmeZamani { get; init; }

    public int ToplamTalepSayisi { get; init; }

    public decimal ToplamTalepButcesi { get; init; }

    public List<PanoTalepGrubu> TalepGruplari { get; init; } = [];

    public PanoIslemeOzeti? IslemeOzeti { get; init; }
}

public sealed class PanoTalepGrubu
{
    public string HizmetKimligi { get; init; } = string.Empty;

    public string HizmetSurumu { get; init; } = string.Empty;

    public int TalepSayisi { get; init; }

    public decimal ToplamAzamiButce { get; init; }

    public decimal OrtalamaAzamiButce { get; init; }

    public decimal EnYuksekAzamiButce { get; init; }
}

public sealed class PanoIslemeOzeti
{
    public long TickNumarasi { get; init; }

    public int ToplamTalepSayisi { get; init; }

    public int BasariliIsSayisi { get; init; }

    public int BasarisizIsSayisi { get; init; }

    public int ZamanAsimiSayisi { get; init; }

    public int SirketBulunamayanIsSayisi { get; init; }

    public int ButceYetersizIsSayisi { get; init; }

    public decimal ToplamCiro { get; init; }

    public double ToplamIslemSuresiMs { get; init; }

    public double BasariOrani { get; init; }
}
