using System.Text.Json.Nodes;

namespace SirketMotoru.Isletim;

/// <summary>
/// Opsiyonel ikinci parametreli JSON metin okuyucularının LINQ method-group
/// çözümlemesinde indeksli Select overload'uyla çakışmasını önler.
/// </summary>
internal static class JsonArraySecimUyumlulugu
{
    public static IEnumerable<string> Select(
        this JsonArray kaynak,
        Func<JsonNode?, string, string> secici)
    {
        ArgumentNullException.ThrowIfNull(kaynak);
        ArgumentNullException.ThrowIfNull(secici);

        return ((IEnumerable<JsonNode?>)kaynak)
            .Select(oge => secici(oge, string.Empty));
    }
}
