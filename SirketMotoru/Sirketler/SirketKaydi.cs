namespace SirketMotoru.Sirketler;

public sealed class SirketKaydi
{
    public string SirketKimligi { get; set; } = string.Empty;

    public string SirketAdi { get; set; } = string.Empty;

    public string SunucuSurumu { get; set; } = string.Empty;

    public SirketDurumu Durum { get; set; } = SirketDurumu.BagliDegil;

    public double SonGecikmeMs { get; set; }

    public DateTimeOffset? SonCevapZamani { get; set; }

    public int BasariliKontrolSayisi { get; set; }

    public int BasarisizKontrolSayisi { get; set; }

    public int AktifBaglantiSayisi { get; set; }

    public int KuyrukUzunlugu { get; set; }
}