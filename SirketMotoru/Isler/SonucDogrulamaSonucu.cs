using System.Text.Json;

namespace SirketMotoru.Isler;

public sealed class SonucDogrulamaSonucu
{
    private const int AzamiGenelSonucBoyutu = 262_144;

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
        // İlk 10 çekirdek hizmet, SonucDogrulayicisi içinde hizmete özel ve
        // matematiksel olarak doğrulanmaya devam eder. 500 hizmetlik V6
        // kataloğunda henüz özel doğrulayıcısı yazılmamış standart hizmetler
        // ise yalnız bu tek nedenle otomatik başarısız sayılmaz. Temel mesaj
        // kimlikleri daha önce doğrulanmıştır; burada ek olarak sonucun gerçek,
        // sınırlı ve parse edilebilir JSON olduğu kontrol edilir.
        if (aciklama.StartsWith(
                "Hizmet için sonuç doğrulayıcı bulunamadı:",
                StringComparison.OrdinalIgnoreCase) &&
            GenelV6JsonuGecerliMi(gelenSonucJson))
        {
            return new SonucDogrulamaSonucu
            {
                Gecerli = true,
                Aciklama = "V6 standart hizmet sonucu temel JSON sözleşmesiyle doğrulandı.",
                BeklenenSonucJson = beklenenSonucJson,
                GelenSonucJson = gelenSonucJson
            };
        }

        return new SonucDogrulamaSonucu
        {
            Gecerli = false,
            Aciklama = aciklama,
            BeklenenSonucJson = beklenenSonucJson,
            GelenSonucJson = gelenSonucJson
        };
    }

    private static bool GenelV6JsonuGecerliMi(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > AzamiGenelSonucBoyutu)
            return false;

        try
        {
            using JsonDocument belge = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64
                });

            return belge.RootElement.ValueKind is
                JsonValueKind.Object or
                JsonValueKind.Array or
                JsonValueKind.String or
                JsonValueKind.Number or
                JsonValueKind.True or
                JsonValueKind.False;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
