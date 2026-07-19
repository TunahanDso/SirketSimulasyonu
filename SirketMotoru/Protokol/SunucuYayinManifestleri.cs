using System.Collections.Concurrent;

namespace SirketMotoru.Protokol;

public sealed class SunulanUygulama
{
    public string UygulamaKimligi { get; init; } = string.Empty;
    public string UygulamaAdi { get; init; } = string.Empty;
    public string Surum { get; init; } = "1.0";
    public string Kategori { get; init; } = "diger";
    public string UrunTuru { get; init; } = "uygulama";
    public string DagitimModeli { get; init; } = "sunucu";
    public string Aciklama { get; init; } = string.Empty;
    public List<UygulamaOzelligi> Ozellikler { get; init; } = [];
    public List<UygulamaBagimliligi> Bagimliliklar { get; init; } = [];
    public List<string> DesteklenenProtokoller { get; init; } = [];
    public List<string> DesteklenenPlatformlar { get; init; } = [];
    public List<string> GerekliPlatformlar { get; init; } = [];
    public List<string> Mimariler { get; init; } = [];
    public List<string> Etiketler { get; init; } = [];
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
    public string BagimlilikTuru { get; init; } = "uygulama";
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
    private static readonly ConcurrentDictionary<string, SunucuYayinManifesti> Manifestler =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Guncelle(string sirketKimligi, IEnumerable<SunulanUygulama>? uygulamalar, IEnumerable<SunulanOzelProtokol>? protokoller)
    {
        string kimlik = TemizMetin(sirketKimligi, 100);
        if (string.IsNullOrWhiteSpace(kimlik)) return;
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
        if (!string.IsNullOrWhiteSpace(sirketKimligi) && Manifestler.TryGetValue(sirketKimligi, out SunucuYayinManifesti? manifest)) return manifest;
        return new SunucuYayinManifesti { SirketKimligi = sirketKimligi?.Trim() ?? string.Empty };
    }

    public static IReadOnlyCollection<SunucuYayinManifesti> TumunuGetir() => Manifestler.Values.ToList().AsReadOnly();

    private static IReadOnlyList<SunulanUygulama> TemizUygulamalar(IEnumerable<SunulanUygulama>? uygulamalar)
    {
        Dictionary<string, SunulanUygulama> sonuc = new(StringComparer.OrdinalIgnoreCase);
        foreach (SunulanUygulama uygulama in (uygulamalar ?? []).Take(100))
        {
            string kimlik = TemizMetin(uygulama.UygulamaKimligi, 120);
            string ad = TemizMetin(uygulama.UygulamaAdi, 100);
            string surum = TemizMetin(uygulama.Surum, 30);
            if (string.IsNullOrWhiteSpace(kimlik) || string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(surum)) continue;

            List<UygulamaOzelligi> ozellikler = (uygulama.Ozellikler ?? [])
                .Take(200)
                .Select(o => new UygulamaOzelligi
                {
                    OzellikKimligi = TemizMetin(o.OzellikKimligi, 120),
                    HizmetKimligi = TemizMetin(o.HizmetKimligi, 160),
                    HizmetSurumu = TemizMetin(o.HizmetSurumu, 30),
                    Aciklama = TemizMetin(o.Aciklama, 400),
                    Zorunlu = o.Zorunlu
                })
                .Where(o => !string.IsNullOrWhiteSpace(o.OzellikKimligi) && !string.IsNullOrWhiteSpace(o.HizmetKimligi) && !string.IsNullOrWhiteSpace(o.HizmetSurumu))
                .GroupBy(o => o.OzellikKimligi, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last()).ToList();

            List<UygulamaBagimliligi> bagimliliklar = (uygulama.Bagimliliklar ?? [])
                .Take(100)
                .Select(b => new UygulamaBagimliligi
                {
                    SirketKimligi = TemizMetin(b.SirketKimligi, 100),
                    UygulamaKimligi = TemizMetin(b.UygulamaKimligi, 120),
                    AsgariSurum = TemizMetin(b.AsgariSurum, 30),
                    ProtokolKimligi = TemizMetin(b.ProtokolKimligi, 120),
                    BagimlilikTuru = Normal(b.BagimlilikTuru, "uygulama", 40),
                    Zorunlu = b.Zorunlu
                })
                .Where(b => !string.IsNullOrWhiteSpace(b.SirketKimligi) && !string.IsNullOrWhiteSpace(b.UygulamaKimligi))
                .ToList();

            sonuc[kimlik] = new SunulanUygulama
            {
                UygulamaKimligi = kimlik,
                UygulamaAdi = ad,
                Surum = surum,
                Kategori = Normal(uygulama.Kategori, "diger", 60),
                UrunTuru = Normal(uygulama.UrunTuru, "uygulama", 60),
                DagitimModeli = Normal(uygulama.DagitimModeli, "sunucu", 60),
                Aciklama = TemizMetin(uygulama.Aciklama, 1_000),
                Ozellikler = ozellikler,
                Bagimliliklar = bagimliliklar,
                DesteklenenProtokoller = TemizListe(uygulama.DesteklenenProtokoller, 100, 120),
                DesteklenenPlatformlar = TemizListe(uygulama.DesteklenenPlatformlar, 100, 120),
                GerekliPlatformlar = TemizListe(uygulama.GerekliPlatformlar, 100, 120),
                Mimariler = TemizListe(uygulama.Mimariler, 50, 80),
                Etiketler = TemizListe(uygulama.Etiketler, 50, 80)
            };
        }
        return sonuc.Values.OrderBy(u => u.UygulamaKimligi, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
    }

    private static IReadOnlyList<SunulanOzelProtokol> TemizProtokoller(IEnumerable<SunulanOzelProtokol>? protokoller)
    {
        Dictionary<string, SunulanOzelProtokol> sonuc = new(StringComparer.OrdinalIgnoreCase);
        foreach (SunulanOzelProtokol protokol in (protokoller ?? []).Take(100))
        {
            string kimlik = TemizMetin(protokol.ProtokolKimligi, 120);
            string ad = TemizMetin(protokol.ProtokolAdi, 100);
            string surum = TemizMetin(protokol.Surum, 30);
            if (string.IsNullOrWhiteSpace(kimlik) || string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(surum)) continue;
            sonuc[$"{kimlik}@{surum}"] = new SunulanOzelProtokol
            {
                ProtokolKimligi = kimlik,
                ProtokolAdi = ad,
                Surum = surum,
                Aciklama = TemizMetin(protokol.Aciklama, 1_000),
                SemaKimligi = TemizMetin(protokol.SemaKimligi, 120),
                SemaOzeti = TemizMetin(protokol.SemaOzeti, 256),
                Yetkinlikler = (protokol.Yetkinlikler ?? []).Take(200).Select(y => new ProtokolYetkinligi
                {
                    YetkinlikKimligi = TemizMetin(y.YetkinlikKimligi, 120),
                    HizmetKimligi = TemizMetin(y.HizmetKimligi, 160),
                    HizmetSurumu = TemizMetin(y.HizmetSurumu, 30),
                    Aciklama = TemizMetin(y.Aciklama, 400)
                }).Where(y => !string.IsNullOrWhiteSpace(y.YetkinlikKimligi) && !string.IsNullOrWhiteSpace(y.HizmetKimligi)).ToList(),
                UyumluProtokoller = TemizListe(protokol.UyumluProtokoller, 100, 120)
            };
        }
        return sonuc.Values.OrderBy(p => p.ProtokolKimligi, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Surum, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
    }

    private static List<string> TemizListe(IEnumerable<string>? liste, int azamiAdet, int azamiUzunluk) =>
        (liste ?? []).Take(azamiAdet).Select(x => TemizMetin(x, azamiUzunluk)).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static string Normal(string? metin, string varsayilan, int azamiUzunluk)
    {
        string sonuc = TemizMetin(metin, azamiUzunluk).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(sonuc) ? varsayilan : sonuc;
    }

    private static string TemizMetin(string? metin, int azamiUzunluk)
    {
        string sonuc = metin?.Trim() ?? string.Empty;
        return sonuc.Length <= azamiUzunluk ? sonuc : sonuc[..azamiUzunluk];
    }
}
