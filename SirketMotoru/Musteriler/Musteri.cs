namespace SirketMotoru.Musteriler;

public sealed class Musteri
{
    public string MusteriKimligi { get; set; } = string.Empty;
    public string MusteriAdi { get; set; } = string.Empty;
    public MusteriTuru MusteriTuru { get; set; }
    public decimal Bakiye { get; set; }
    public decimal TickBasinaHarcamaButcesi { get; set; }
    public double TalepOlusturmaOlasiligi { get; set; }
    public MusteriTercihleri Tercihler { get; set; } = new();
    public string? TercihEdilenSirketKimligi { get; set; }

    public string IsletimSistemiKimligi { get; set; } = string.Empty;
    public string IsletimSistemiSurumu { get; set; } = string.Empty;
    public string IsletimSistemiSirketKimligi { get; set; } = string.Empty;
    public double IsletimSistemiMemnuniyeti { get; set; } = 50;
    public int IsletimSistemiDegisimSayisi { get; set; }
    public long SonIsletimSistemiDegisimTicki { get; set; }

    // V6 müşteri CV'si: işletim sistemi, kullanılan uygulamalar, sadakat ve dijital davranış.
    public string MeslekProfili { get; set; } = string.Empty;
    public string GelirSegmenti { get; set; } = "orta";
    public List<string> KullandigiUygulamalar { get; set; } = [];
    public Dictionary<string, int> UygulamaKullanimSayilari { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> UygulamaMemnuniyetleri { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public int ToplamUygulamaDegisimSayisi { get; set; }
    public long SonUygulamaDegisimTicki { get; set; }

    public Dictionary<string, int> HizmetKullanimSayilari { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<MusteriIslemKaydi> IslemGecmisi { get; set; } = [];
    public decimal ToplamHarcama { get; set; }
    public int BasariliIsSayisi { get; set; }
    public int BasarisizIsSayisi { get; set; }
    public bool Aktif { get; set; } = true;
    public DateTimeOffset OlusturulmaZamani { get; set; }
    public DateTimeOffset? SonIslemZamani { get; set; }

    public bool OdemeYapabilirMi(decimal tutar) => Aktif && tutar >= 0 && Bakiye >= tutar;

    public void OdemeYap(decimal tutar)
    {
        if (tutar < 0) throw new ArgumentOutOfRangeException(nameof(tutar), "Ödeme tutarı negatif olamaz.");
        if (Bakiye < tutar) throw new InvalidOperationException($"{MusteriAdi} müşterisinin bakiyesi yetersiz.");
        Bakiye -= tutar;
        ToplamHarcama += tutar;
    }

    public void IslemKaydet(MusteriIslemKaydi islem)
    {
        ArgumentNullException.ThrowIfNull(islem);
        IslemGecmisi.Add(islem);
        SonIslemZamani = islem.OlusturulmaZamani;
        if (islem.Basarili) BasariliIsSayisi++; else BasarisizIsSayisi++;
        if (!string.IsNullOrWhiteSpace(islem.HizmetKimligi))
        {
            HizmetKullanimSayilari.TryGetValue(islem.HizmetKimligi, out int mevcut);
            HizmetKullanimSayilari[islem.HizmetKimligi] = mevcut + 1;
        }
        const int azamiMusteriGecmisi = 500;
        if (IslemGecmisi.Count > azamiMusteriGecmisi)
            IslemGecmisi.RemoveRange(0, IslemGecmisi.Count - azamiMusteriGecmisi);
    }
}
