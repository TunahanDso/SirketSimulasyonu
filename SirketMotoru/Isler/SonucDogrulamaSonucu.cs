namespace SirketMotoru.Isler;

public sealed class SonucDogrulamaSonucu
{
    public bool Gecerli { get; init; }

    public string Aciklama { get; init; } =
        string.Empty;

    public string? BeklenenSonucJson { get; init; }

    public string? GelenSonucJson { get; init; }

    public static SonucDogrulamaSonucu Basarili(
        string aciklama,
        string? beklenenSonucJson = null,
        string? gelenSonucJson = null)
    {
        return new SonucDogrulamaSonucu
        {
            Gecerli = true,
            Aciklama = aciklama,
            BeklenenSonucJson = beklenenSonucJson,
            GelenSonucJson = gelenSonucJson
        };
    }

    public static SonucDogrulamaSonucu Basarisiz(
        string aciklama,
        string? beklenenSonucJson = null,
        string? gelenSonucJson = null)
    {
        return new SonucDogrulamaSonucu
        {
            Gecerli = false,
            Aciklama = aciklama,
            BeklenenSonucJson = beklenenSonucJson,
            GelenSonucJson = gelenSonucJson
        };
    }
}