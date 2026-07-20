using System.Text.Json.Serialization;
using SirketMotoru.Hizmetler;
using SirketMotoru.Kayit;

namespace SirketMotoru.Protokol;

public sealed class SirketTanitimMesaji : IJsonOnDeserialized
{
    public string MesajTuru { get; init; } = MesajTurleri.SirketTanitim;
    public string MesajKimligi { get; init; } = string.Empty;
    public string ProtokolSurumu { get; init; } = string.Empty;
    public string SirketKimligi { get; init; } = string.Empty;
    public string SirketAdi { get; init; } = string.Empty;
    public string SunucuSurumu { get; init; } = string.Empty;
    public List<SunulanHizmet> Hizmetler { get; init; } = [];

    // Standart V9 alanları.
    public List<SunulanUygulama> Uygulamalar { get; init; } = [];
    public List<SunulanOzelProtokol> OzelProtokoller { get; init; } = [];

    // Go/Node/Python sunucularının eski manifest adları için geriye dönük uyumluluk.
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

    public void OnDeserialized()
    {
        List<SunulanUygulama> uygulamalar = Uygulamalar
            .Concat(Urunler)
            .Concat(UygulamaManifestleri)
            .Concat(Products)
            .Concat(Apps)
            .Where(x => x is not null)
            .GroupBy(x => x.UygulamaKimligi ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => x.Last())
            .ToList();

        List<SunulanOzelProtokol> protokoller = OzelProtokoller
            .Concat(Protokoller)
            .Concat(Protocols)
            .Where(x => x is not null)
            .GroupBy(x => $"{x.ProtokolKimligi}@{x.Surum}", StringComparer.OrdinalIgnoreCase)
            .Where(x => !x.Key.StartsWith("@", StringComparison.Ordinal))
            .Select(x => x.Last())
            .ToList();

        SunucuYayinManifestDeposu.Guncelle(SirketKimligi, uygulamalar, protokoller);
        KonsolKayitcisi.Bilgi(
            $"Yayın manifesti alındı | Şirket: {SirketAdi} | Uygulama/OS: {uygulamalar.Count} | Protokol: {protokoller.Count}");
    }
}
