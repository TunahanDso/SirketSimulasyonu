namespace SirketMotoru.Isletim;

public sealed class KodTabanliYayinDosyasi
{
    public int Surum { get; set; } = 1;
    public DateTimeOffset GuncellenmeZamani { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, SirketYayinAyarlari> Sirketler { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SirketYayinAyarlari
{
    public Dictionary<string, bool> HizmetAktiflikEzmeDegerleri { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> UygulamaUrunEslemeleri { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ProtokolPiyasaEslemeleri { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HizmetYayinDurumuIstegi
{
    public string HizmetKimligi { get; set; } = string.Empty;
    public string HizmetSurumu { get; set; } = "1.0";
    public bool Aktif { get; set; }
}

public sealed class UygulamaYayinlaIstegi
{
    public string UygulamaKimligi { get; set; } = string.Empty;
    public string FiyatlandirmaModeli { get; set; } = "abonelik";
    public decimal AbonelikUcreti { get; set; }
    public decimal KullanimBasinaUcret { get; set; }

    // V8.2: ürün tek düğmeyle oluşturulur ve aynı işlemde dağıtımı bağlanır.
    public string IsletimSistemiKimligi { get; set; } = string.Empty;
    public string BaglantiProtokoluKimligi { get; set; } = string.Empty;
    public bool AktifOlmasiIsteniyor { get; set; } = true;
}

public sealed class ProtokolYayinlaIstegi
{
    public string ProtokolKimligi { get; set; } = string.Empty;
    public string Surum { get; set; } = "1.0";
    public string LisansModeli { get; set; } = "acik";
    public decimal BenimsemeBedeli { get; set; }
    public decimal TickLisansBedeli { get; set; }
}

public sealed class UygulamaDogrulamaSonucu
{
    public bool Gecerli { get; init; }
    public string Durum { get; init; } = string.Empty;
    public IReadOnlyList<string> Hatalar { get; init; } = [];
    public IReadOnlyList<string> Uyarilar { get; init; } = [];
    public IReadOnlyList<string> ZorunluOzellikler { get; init; } = [];
    public IReadOnlyList<string> OpsiyonelOzellikler { get; init; } = [];
}

public sealed class UygulamaStandardi
{
    public string Kategori { get; init; } = string.Empty;
    public string Ad { get; init; } = string.Empty;
    public IReadOnlyList<string> ZorunluOzellikler { get; init; } = [];
    public IReadOnlyList<string> OpsiyonelOzellikler { get; init; } = [];
}
