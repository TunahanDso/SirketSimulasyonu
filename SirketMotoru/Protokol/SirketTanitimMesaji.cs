using System.Text.Json;
using System.Text.Json.Serialization;
using SirketMotoru.Hizmetler;
using SirketMotoru.Kayit;

namespace SirketMotoru.Protokol;

public sealed class SirketTanitimMesaji : IJsonOnDeserialized
{
    private static readonly JsonSerializerOptions UyumlulukJsonu = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string MesajTuru { get; init; } = MesajTurleri.SirketTanitim;
    public string MesajKimligi { get; init; } = string.Empty;
    public string ProtokolSurumu { get; init; } = string.Empty;
    public string SirketKimligi { get; init; } = string.Empty;
    public string SirketAdi { get; init; } = string.Empty;
    public string SunucuSurumu { get; init; } = string.Empty;
    public List<SunulanHizmet> Hizmetler { get; init; } = [];

    public List<SunulanUygulama> Uygulamalar { get; init; } = [];
    public List<SunulanOzelProtokol> OzelProtokoller { get; init; } = [];

    [JsonPropertyName("urunler")]
    public List<SunulanUygulama> Urunler { get; init; } = [];

    [JsonPropertyName("uygulamaManifestleri")]
    public List<SunulanUygulama> UygulamaManifestleri { get; init; } = [];

    [JsonPropertyName("products")]
    public List<SunulanUygulama> Products { get; init; } = [];

    [JsonPropertyName("apps")]
    public List<SunulanUygulama> Apps { get; init; } = [];

    [JsonPropertyName("protokoller")]
    public List<SunulanOzelProtokol> Protokoller { get; init; } = [];

    [JsonPropertyName("protocols")]
    public List<SunulanOzelProtokol> Protocols { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement> EkAlanlar { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public void OnDeserialized()
    {
        List<SunulanUygulama> uygulamalar = Uygulamalar
            .Concat(Urunler)
            .Concat(UygulamaManifestleri)
            .Concat(Products)
            .Concat(Apps)
            .ToList();
        List<SunulanOzelProtokol> protokoller = OzelProtokoller
            .Concat(Protokoller)
            .Concat(Protocols)
            .ToList();

        foreach ((string anahtar, JsonElement deger) in EkAlanlar)
        {
            if (deger.ValueKind != JsonValueKind.Object ||
                !new[] { "manifest", "yayinManifesti", "uygulamaManifesti", "applicationManifest" }
                    .Contains(anahtar, StringComparer.OrdinalIgnoreCase))
                continue;

            UygulamalariOku(deger, uygulamalar, "uygulamalar", "urunler", "products", "apps", "uygulamaManifestleri");
            ProtokolleriOku(deger, protokoller, "ozelProtokoller", "protokoller", "protocols");
        }

        uygulamalar = uygulamalar
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.UygulamaKimligi))
            .GroupBy(x => x.UygulamaKimligi, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .ToList();
        protokoller = protokoller
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.ProtokolKimligi))
            .GroupBy(x => $"{x.ProtokolKimligi}@{x.Surum}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .ToList();

        SunucuYayinManifestDeposu.Guncelle(SirketKimligi, uygulamalar, protokoller);
        KonsolKayitcisi.Bilgi(
            $"Yayın manifesti alındı | Şirket: {SirketAdi} | Uygulama/OS: {uygulamalar.Count} | Protokol: {protokoller.Count}");
    }

    private static void UygulamalariOku(
        JsonElement nesne,
        List<SunulanUygulama> hedef,
        params string[] alanlar)
    {
        foreach (string alan in alanlar)
        {
            if (!OzellikBul(nesne, alan, out JsonElement liste) || liste.ValueKind != JsonValueKind.Array) continue;
            try
            {
                List<SunulanUygulama>? bulunan = listaDeserialize<SunulanUygulama>(liste);
                if (bulunan is not null) hedef.AddRange(bulunan);
            }
            catch { }
        }
    }

    private static void ProtokolleriOku(
        JsonElement nesne,
        List<SunulanOzelProtokol> hedef,
        params string[] alanlar)
    {
        foreach (string alan in alanlar)
        {
            if (!OzellikBul(nesne, alan, out JsonElement liste) || liste.ValueKind != JsonValueKind.Array) continue;
            try
            {
                List<SunulanOzelProtokol>? bulunan = listaDeserialize<SunulanOzelProtokol>(liste);
                if (bulunan is not null) hedef.AddRange(bulunan);
            }
            catch { }
        }
    }

    private static List<T>? listaDeserialize<T>(JsonElement element) =>
        JsonSerializer.Deserialize<List<T>>(element.GetRawText(), UyumlulukJsonu);

    private static bool OzellikBul(JsonElement nesne, string aranan, out JsonElement deger)
    {
        foreach (JsonProperty ozellik in nesne.EnumerateObject())
        {
            if (ozellik.Name.Equals(aranan, StringComparison.OrdinalIgnoreCase))
            {
                deger = ozellik.Value;
                return true;
            }
        }
        deger = default;
        return false;
    }
}
