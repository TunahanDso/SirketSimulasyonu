namespace SirketMotoru.Isler;

public sealed class IsIslemeOzeti
{
    public long TickNumarasi { get; init; }

    public int ToplamTalepSayisi { get; set; }

    public int BasariliIsSayisi { get; set; }

    public int BasarisizIsSayisi { get; set; }

    public int ZamanAsimiSayisi { get; set; }

    public int SirketBulunamayanIsSayisi { get; set; }

    public int ButceYetersizIsSayisi { get; set; }

    public int KotuNiyetliIsSayisi { get; set; }

    public int EngellenenSaldiriSayisi { get; set; }

    public int BasariliSaldiriSayisi { get; set; }

    public decimal ToplamGuvenlikKaybi { get; set; }

    public decimal ToplamCiro { get; set; }

    public double ToplamIslemSuresiMs { get; set; }

    public double BasariOrani =>
        ToplamTalepSayisi == 0
            ? 0
            : BasariliIsSayisi * 100.0 /
              ToplamTalepSayisi;
}
