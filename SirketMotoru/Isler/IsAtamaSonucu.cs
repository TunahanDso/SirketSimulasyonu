namespace SirketMotoru.Isler;

public sealed class IsAtamaSonucu
{
    public bool Basarili { get; init; }

    public string IsKimligi { get; init; } =
        string.Empty;

    public string? SirketKimligi { get; init; }

    public decimal IslemTutari { get; init; }

    public double IslemSuresiMs { get; init; }

    public string SonucAciklamasi { get; init; } =
        string.Empty;

    public string SonucVerisiJson { get; init; } =
        "{}";

    public bool GuvenlikOlayi { get; init; }

    public bool SaldiriEngellendi { get; init; }

    public decimal GuvenlikKaybi { get; init; }

    public static IsAtamaSonucu BasariliSonuc(
        string isKimligi,
        string sirketKimligi,
        decimal islemTutari,
        double islemSuresiMs,
        string sonucVerisiJson)
    {
        return new IsAtamaSonucu
        {
            Basarili = true,
            IsKimligi = isKimligi,
            SirketKimligi = sirketKimligi,
            IslemTutari = islemTutari,
            IslemSuresiMs = islemSuresiMs,
            SonucAciklamasi =
                "İş başarıyla tamamlandı.",
            SonucVerisiJson =
                sonucVerisiJson
        };
    }

    public static IsAtamaSonucu BasarisizSonuc(
        string isKimligi,
        string? sirketKimligi,
        string sonucAciklamasi,
        double islemSuresiMs = 0)
    {
        return new IsAtamaSonucu
        {
            Basarili = false,
            IsKimligi = isKimligi,
            SirketKimligi = sirketKimligi,
            IslemTutari = 0,
            IslemSuresiMs =
                Math.Max(
                    0,
                    islemSuresiMs),
            SonucAciklamasi =
                sonucAciklamasi,
            SonucVerisiJson =
                "{}"
        };
    }

    public static IsAtamaSonucu GuvenlikSonucu(
        string isKimligi,
        string sirketKimligi,
        bool saldiriEngellendi,
        decimal guvenlikKaybi,
        double islemSuresiMs,
        string aciklama)
    {
        return new IsAtamaSonucu
        {
            Basarili = false,
            IsKimligi = isKimligi,
            SirketKimligi = sirketKimligi,
            IslemTutari = 0,
            IslemSuresiMs = Math.Max(0, islemSuresiMs),
            SonucAciklamasi = aciklama,
            SonucVerisiJson = "{}",
            GuvenlikOlayi = true,
            SaldiriEngellendi = saldiriEngellendi,
            GuvenlikKaybi = Math.Max(0, guvenlikKaybi)
        };
    }
}
