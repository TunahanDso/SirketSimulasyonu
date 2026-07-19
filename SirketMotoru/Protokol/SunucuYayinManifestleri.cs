using System.Collections.Concurrent;

namespace SirketMotoru.Protokol;

public sealed class SunulanUygulama
{
    public string UygulamaKimligi { get; init; } = string.Empty;
    public string UygulamaAdi { get; init; } = string.Empty;
    public string Surum { get; init; } = "1.0";
    public string Kategori { get; init; } = "diger";
    public string Aciklama { get; init; } = string.Empty;
    public List<UygulamaOzelligi> Ozellikler { get; init; } = [];
    public List<UygulamaBagimliligi> Bagimliliklar { get; init; } = [];
    public List<string> DesteklenenProtokoller { get; init; } = [];
}

public sealed class UygulamaOzelligi
{
    public string OzellikKimligi { get; init; } = string.Empty;
    public string HizmetKimligi { get; init; } = string.Empty;
    public string HizmetSurumu { get; init; } = "1.0";
    public string Aciklama { get; init; } = string.Empty;
    public bool Zorunlu { get; init; } = true;
}

public sealed class UygulamaBagimliligi
{
    public string SirketKimligi { get; init; } = string.Empty;
    public string UygulamaKimligi { get; init; } = string.Empty;
    public string AsgariSurum { get; init; } = "1.0";
    public string ProtokolKimligi { get; init; } = string.Empty;
    public bool Zorunlu { get; init; } = true;
}

public sealed class SunulanOzelProtokol
{
    public string ProtokolKimligi { get; init; } = string.Empty;
    public string ProtokolAdi { get; init; } = string.Empty;
    public string Surum { get; init; } = "1.0";
    public string Aciklama { get; init; } = string.Empty;
    public string SemaKimligi { get; init; } = string.Empty;
    public string SemaOzeti { get; init; } = string.Empty;
    public List<ProtokolYetkinligi> Yetkinlikler { get; init; } = [];
    public List<string> UyumluProtokoller { get; init; } = [];
}

public sealed class ProtokolYetkinligi
{
    public string YetkinlikKimligi { get; init; } = string.Empty;
    public string HizmetKimligi { get; init; } = string.Empty;
    public string HizmetSurumu { get; init; } = "1.0";
    public string Aciklama { get; init; } = string.Empty;
}

public sealed class SunucuYayinManifesti
{
    public string SirketKimligi { get; init; } = string.Empty;
    public DateTimeOffset GuncellenmeZamani { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<SunulanUygulama> Uygulamalar { get; init; } = [];
    public IReadOnlyList<SunulanOzelProtokol> OzelProtokoller { get; init; } = [];
}

public static class SunucuYayinManifestDeposu
{
    private static readonly ConcurrentDictionary<string, SunucuYayinManifesti>
        Manifestler = new(StringComparer.OrdinalIgnoreCase);

    public static void Guncelle(
        string sirketKimligi,
        IEnumerable<SunulanUygulama>? uygulamalar,
        IEnumerable<SunulanOzelProtokol>? protokoller)
    {
        string kimlik = TemizMetin(sirketKimligi, 100);
        if (string.IsNullOrWhiteSpace(kimlik))
        {
            return;
        }

        Manifestler[kimlik] = new SunucuYayinManifesti
        {
            SirketKimligi = kimlik,
            GuncellenmeZamani = DateTimeOffset.UtcNow,
            Uygulamalar = TemizUygulamalar(uygulamalar),
            OzelProtokoller = TemizProtokoller(protokoller)
        };
    }

    public static SunucuYayinManifesti Getir(string sirketKimligi)
    {
        if (!string.IsNullOrWhiteSpace(sirketKimligi) &&
            Manifestler.TryGetValue(sirketKimligi, out SunucuYayinManifesti? manifest))
        {
            return manifest;
        }

        return new SunucuYayinManifesti
        {
            SirketKimligi = sirketKimligi?.Trim() ?? string.Empty
        };
    }

    public static IReadOnlyCollection<SunucuYayinManifesti> TumunuGetir() =>
        Manifestler.Values.ToList().AsReadOnly();

    private static IReadOnlyList<SunulanUygulama> TemizUygulamalar(
        IEnumerable<SunulanUygulama>? uygulamalar)
    {
        Dictionary<string, SunulanUygulama> sonuc =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (SunulanUygulama uygulama in (uygulamalar ?? []).Take(50))
        {
            string kimlik = TemizMetin(uygulama.UygulamaKimligi, 120);
            string ad = TemizMetin(uygulama.UygulamaAdi, 100);
            string surum = TemizMetin(uygulama.Surum, 30);
            string kategori = TemizMetin(uygulama.Kategori, 50).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(kimlik) ||
                string.IsNullOrWhiteSpace(ad) ||
                string.IsNullOrWhiteSpace(surum))
            {
                continue;
            }

            List<UygulamaOzelligi> ozellikler = uygulama.Ozellikler
                .Take(100)
                .Select(ozellik => new UygulamaOzelligi
                {
                    OzellikKimligi = TemizMetin(ozellik.OzellikKimligi, 120),
                    HizmetKimligi = TemizMetin(ozellik.HizmetKimligi, 160),
                    HizmetSurumu = TemizMetin(ozellik.HizmetSurumu, 30),
                    Aciklama = TemizMetin(ozellik.Aciklama, 400),
                    Zorunlu = ozellik.Zorunlu
                })
                .Where(ozellik =>
                    !string.IsNullOrWhiteSpace(ozellik.OzellikKimligi) &&
                    !string.IsNullOrWhiteSpace(ozellik.HizmetKimligi) &&
                    !string.IsNullOrWhiteSpace(ozellik.HizmetSurumu))
                .GroupBy(ozellik => ozellik.OzellikKimligi, StringComparer.OrdinalIgnoreCase)
                .Select(grup => grup.Last())
                .ToList();

            List<UygulamaBagimliligi> bagimliliklar = uygulama.Bagimliliklar
                .Take(50)
                .Select(bagimlilik => new UygulamaBagimliligi
                {
                    SirketKimligi = TemizMetin(bagimlilik.SirketKimligi, 100),
                    UygulamaKimligi = TemizMetin(bagimlilik.UygulamaKimligi, 120),
                    AsgariSurum = TemizMetin(bagimlilik.AsgariSurum, 30),
                    ProtokolKimligi = TemizMetin(bagimlilik.ProtokolKimligi, 120),
                    Zorunlu = bagimlilik.Zorunlu
                })
                .Where(bagimlilik =>
                    !string.IsNullOrWhiteSpace(bagimlilik.SirketKimligi) &&
                    !string.IsNullOrWhiteSpace(bagimlilik.UygulamaKimligi))
                .ToList();

            sonuc[kimlik] = new SunulanUygulama
            {
                UygulamaKimligi = kimlik,
                UygulamaAdi = ad,
                Surum = surum,
                Kategori = string.IsNullOrWhiteSpace(kategori) ? "diger" : kategori,
                Aciklama = TemizMetin(uygulama.Aciklama, 1_000),
                Ozellikler = ozellikler,
                Bagimliliklar = bagimliliklar,
                DesteklenenProtokoller = uygulama.DesteklenenProtokoller
                    .Take(50)
                    .Select(protokol => TemizMetin(protokol, 120))
                    .Where(protokol => !string.IsNullOrWhiteSpace(protokol))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        return sonuc.Values
            .OrderBy(uygulama => uygulama.UygulamaKimligi, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyList<SunulanOzelProtokol> TemizProtokoller(
        IEnumerable<SunulanOzelProtokol>? protokoller)
    {
        Dictionary<string, SunulanOzelProtokol> sonuc =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (SunulanOzelProtokol protokol in (protokoller ?? []).Take(50))
        {
            string kimlik = TemizMetin(protokol.ProtokolKimligi, 120);
            string ad = TemizMetin(protokol.ProtokolAdi, 100);
            string surum = TemizMetin(protokol.Surum, 30);

            if (string.IsNullOrWhiteSpace(kimlik) ||
                string.IsNullOrWhiteSpace(ad) ||
                string.IsNullOrWhiteSpace(surum))
            {
                continue;
            }

            sonuc[$"{kimlik}@{surum}"] = new SunulanOzelProtokol
            {
                ProtokolKimligi = kimlik,
                ProtokolAdi = ad,
                Surum = surum,
                Aciklama = TemizMetin(protokol.Aciklama, 1_000),
                SemaKimligi = TemizMetin(protokol.SemaKimligi, 120),
                SemaOzeti = TemizMetin(protokol.SemaOzeti, 256),
                Yetkinlikler = protokol.Yetkinlikler
                    .Take(100)
                    .Select(yetkinlik => new ProtokolYetkinligi
                    {
                        YetkinlikKimligi = TemizMetin(yetkinlik.YetkinlikKimligi, 120),
                        HizmetKimligi = TemizMetin(yetkinlik.HizmetKimligi, 160),
                        HizmetSurumu = TemizMetin(yetkinlik.HizmetSurumu, 30),
                        Aciklama = TemizMetin(yetkinlik.Aciklama, 400)
                    })
                    .Where(yetkinlik =>
                        !string.IsNullOrWhiteSpace(yetkinlik.YetkinlikKimligi) &&
                        !string.IsNullOrWhiteSpace(yetkinlik.HizmetKimligi))
                    .ToList(),
                UyumluProtokoller = protokol.UyumluProtokoller
                    .Take(50)
                    .Select(uyumlu => TemizMetin(uyumlu, 120))
                    .Where(uyumlu => !string.IsNullOrWhiteSpace(uyumlu))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        return sonuc.Values
            .OrderBy(protokol => protokol.ProtokolKimligi, StringComparer.OrdinalIgnoreCase)
            .ThenBy(protokol => protokol.Surum, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private static string TemizMetin(string? metin, int azamiUzunluk)
    {
        string sonuc = metin?.Trim() ?? string.Empty;
        return sonuc.Length <= azamiUzunluk
            ? sonuc
            : sonuc[..azamiUzunluk];
    }
}
