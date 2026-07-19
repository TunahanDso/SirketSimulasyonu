namespace SirketMotoru.Isletim;

public sealed class SirketIsletimDosyasi
{
    public int Surum { get; set; } = 5;
    public DateTimeOffset GuncellenmeZamani { get; set; } = DateTimeOffset.UtcNow;
    public long SonIslenenTick { get; set; }
    public List<SirketHesabi> Hesaplar { get; set; } = [];
    public List<SirketIsletimDurumu> Sirketler { get; set; } = [];
    public List<OzelProtokolKaydi> Protokoller { get; set; } = [];
    public List<SozlesmeTeklifi> SozlesmeTeklifleri { get; set; } = [];
    public List<PiyasaOlayi> PiyasaOlaylari { get; set; } = [];
}

public sealed class SirketHesabi
{
    public string SirketKimligi { get; set; } = string.Empty;
    public string KullaniciAdi { get; set; } = string.Empty;
    public string ParolaTuzu { get; set; } = string.Empty;
    public string ParolaOzeti { get; set; } = string.Empty;
    public bool ParolaDegistirilmeli { get; set; } = true;
    public DateTimeOffset OlusturulmaZamani { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SonGirisZamani { get; set; }
}

public sealed class SirketIsletimDurumu
{
    public string SirketKimligi { get; set; } = string.Empty;
    public Dictionary<string, int> YatirimSeviyeleri { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Eski kayıtlarla uyumluluk için tutulur. Yeni sürümde HizmetAyarlari otoritedir.
    public Dictionary<string, decimal> HizmetFiyatEzmeDegerleri { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> HizmetBazKapasiteleri { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, HizmetKaliciAyari> HizmetAyarlari { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<KrediKaydi> Krediler { get; set; } = [];
    public List<UrunKaydi> Urunler { get; set; } = [];
    public List<string> BenimsenenProtokoller { get; set; } = [];
    public List<SozlesmeKaydi> Sozlesmeler { get; set; } = [];
    public List<IsletimIslemKaydi> SonIslemler { get; set; } = [];
    public List<SirketOlayKaydi> SonOlaylar { get; set; } = [];
    public int KrediNotu { get; set; } = 650;
    public double EkosistemPuani { get; set; } = 50;
    public double OperasyonRiski { get; set; } = 18;
    public double TeknikBorc { get; set; } = 12;
    public double BakimBaskisi { get; set; }
    public decimal ToplamAbonelikGeliri { get; set; }
    public decimal ToplamUrunGeliri { get; set; }
    public decimal ToplamIsletmeGideri { get; set; }
    public decimal ToplamFinansmanGideri { get; set; }
    public decimal ToplamYatirimHarcamasi { get; set; }
    public decimal OdenemeyenGider { get; set; }
    public decimal SirketDegeri { get; set; }
    public decimal TahminiHisseFiyati { get; set; }
    public int ToplamAboneSayisi { get; set; }
    public int TemerrutSayisi { get; set; }
    public long SonYonetimIslemiTicki { get; set; }
}

public sealed class HizmetKaliciAyari
{
    public string HizmetKimligi { get; set; } = string.Empty;
    public string HizmetSurumu { get; set; } = "1.0";
    public decimal SunucudanGelenIlkFiyat { get; set; }
    public int SunucudanGelenIlkKapasite { get; set; } = 1;
    public bool SunucudanGelenIlkAktiflik { get; set; } = true;
    public decimal YonetilenFiyat { get; set; }
    public int SatinAlinanKapasite { get; set; }
    public bool YonetilenAktiflik { get; set; } = true;
    public bool FiyatYonetildi { get; set; }
    public bool AktiflikYonetildi { get; set; }
    public long IlkGorulmeTicki { get; set; }
    public long SonGorulmeTicki { get; set; }
}

public sealed class KrediKaydi
{
    public string KrediKimligi { get; set; } = string.Empty;
    public string KrediTuru { get; set; } = string.Empty;
    public decimal AnaPara { get; set; }
    public decimal KalanBorc { get; set; }
    public decimal TickFaizOrani { get; set; }
    public decimal TaksitTutari { get; set; }
    public int KalanTaksit { get; set; }
    public long SonrakiOdemeTicki { get; set; }
    public int OdemeAraligiTick { get; set; } = 5;
    public bool Aktif { get; set; } = true;
    public int GecikmeSayisi { get; set; }
    public DateTimeOffset OlusturulmaZamani { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UrunKaydi
{
    public string UrunKimligi { get; set; } = string.Empty;
    public string UygulamaKimligi { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public string Kategori { get; set; } = "diger";
    public string UrunTuru { get; set; } = "uygulama";
    public string FiyatlandirmaModeli { get; set; } = "abonelik";
    public decimal AbonelikUcreti { get; set; }
    public decimal KullanimBasinaUcret { get; set; }
    public int TabanKullaniciKapasitesi { get; set; } = 100;
    public int SatinAlinanKullaniciKapasitesi { get; set; }
    public int AltyapiKapasiteBonusu { get; set; }
    public int KullaniciKapasitesi { get; set; } = 100;
    public int AktifKullaniciSayisi { get; set; }
    public int ToplamEdinilenKullanici { get; set; }
    public int ToplamKaybedilenKullanici { get; set; }
    public double UrunMemnuniyeti { get; set; } = 50;
    public double UrunKalitesi { get; set; } = 50;
    public string ArkaUcHizmetKimligi { get; set; } = string.Empty;
    public string ArkaUcHizmetSurumu { get; set; } = "1.0";
    public string ProtokolKimligi { get; set; } = string.Empty;
    public List<string> DesteklenenPlatformlar { get; set; } = [];
    public List<string> GerekliPlatformlar { get; set; } = [];
    public List<string> Bagimliliklar { get; set; } = [];
    public bool Aktif { get; set; } = true;
    public long YayinTicki { get; set; }
    public decimal ToplamGelir { get; set; }
    public decimal ToplamGider { get; set; }
    public int KesintiTicki { get; set; }
    public double SonTalepCarpani { get; set; } = 1;
}

public sealed class OzelProtokolKaydi
{
    public string ProtokolKimligi { get; set; } = string.Empty;
    public string SahipSirketKimligi { get; set; } = string.Empty;
    public string ProtokolAdi { get; set; } = string.Empty;
    public string Surum { get; set; } = "1.0";
    public string Aciklama { get; set; } = string.Empty;
    public string LisansModeli { get; set; } = "acik";
    public decimal BenimsemeBedeli { get; set; }
    public decimal TickLisansBedeli { get; set; }
    public List<string> BenimseyenSirketler { get; set; } = [];
    public int BagliUrunSayisi { get; set; }
    public decimal ToplamLisansGeliri { get; set; }
    public long YayinTicki { get; set; }
    public bool Aktif { get; set; } = true;
}

public sealed class SozlesmeTeklifi
{
    public string TeklifKimligi { get; set; } = string.Empty;
    public string Baslik { get; set; } = string.Empty;
    public string Kategori { get; set; } = string.Empty;
    public int SureTick { get; set; }
    public decimal TickOdemesi { get; set; }
    public decimal IhlalCezasi { get; set; }
    public double AsgariKalite { get; set; }
    public double AsgariPerformans { get; set; }
    public double AsgariGuvenlik { get; set; }
    public int GerekliKapasite { get; set; }
    public long SonKabulTicki { get; set; }
    public string KabulEdenSirketKimligi { get; set; } = string.Empty;
    public bool Aktif { get; set; } = true;
}

public sealed class SozlesmeKaydi
{
    public string SozlesmeKimligi { get; set; } = string.Empty;
    public string TeklifKimligi { get; set; } = string.Empty;
    public string Baslik { get; set; } = string.Empty;
    public string Kategori { get; set; } = string.Empty;
    public long BaslangicTicki { get; set; }
    public long BitisTicki { get; set; }
    public decimal TickOdemesi { get; set; }
    public decimal IhlalCezasi { get; set; }
    public double AsgariKalite { get; set; }
    public double AsgariPerformans { get; set; }
    public double AsgariGuvenlik { get; set; }
    public int GerekliKapasite { get; set; }
    public int BasariliTickSayisi { get; set; }
    public int IhlalSayisi { get; set; }
    public bool Aktif { get; set; } = true;
}

public sealed class PiyasaOlayi
{
    public string OlayKimligi { get; set; } = string.Empty;
    public string Baslik { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public string EtkilenenKategori { get; set; } = "tum";
    public double TalepCarpani { get; set; } = 1;
    public double GiderCarpani { get; set; } = 1;
    public double GelirCarpani { get; set; } = 1;
    public double KullaniciKaybiCarpani { get; set; } = 1;
    public double ArizaRiskiCarpani { get; set; } = 1;
    public long BaslangicTicki { get; set; }
    public long BitisTicki { get; set; }
}

public sealed class SirketOlayKaydi
{
    public string OlayKimligi { get; set; } = string.Empty;
    public long TickNumarasi { get; set; }
    public string Tur { get; set; } = string.Empty;
    public string Baslik { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public string EtkilenenVarlik { get; set; } = string.Empty;
    public decimal FinansalEtki { get; set; }
    public double ItibarEtkisi { get; set; }
    public double GuvenlikEtkisi { get; set; }
    public double PerformansEtkisi { get; set; }
    public bool Olumlu { get; set; }
    public DateTimeOffset Zaman { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class IsletimIslemKaydi
{
    public string IslemKimligi { get; set; } = string.Empty;
    public long TickNumarasi { get; set; }
    public string IslemTuru { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public DateTimeOffset Zaman { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class YatirimPaketi
{
    public string YatirimTuru { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public int AzamiSeviye { get; set; }
    public decimal TabanMaliyet { get; set; }
}

public sealed class KrediPaketi
{
    public string KrediTuru { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public decimal AsgariTutar { get; set; }
    public decimal AzamiTutar { get; set; }
    public decimal TickFaizOrani { get; set; }
    public int TaksitSayisi { get; set; }
    public int OdemeAraligiTick { get; set; }
    public int AsgariKrediNotu { get; set; }
}

public sealed class GirisIstegi { public string KullaniciAdi { get; set; } = string.Empty; public string Parola { get; set; } = string.Empty; }
public sealed class ParolaDegistirIstegi { public string EskiParola { get; set; } = string.Empty; public string YeniParola { get; set; } = string.Empty; }
public sealed class YatirimIstegi { public string YatirimTuru { get; set; } = string.Empty; }
public sealed class KrediIstegi { public string KrediTuru { get; set; } = string.Empty; public decimal Tutar { get; set; } }
public sealed class FiyatGuncelleIstegi { public string HizmetKimligi { get; set; } = string.Empty; public string HizmetSurumu { get; set; } = "1.0"; public decimal YeniFiyat { get; set; } }
public sealed class HizmetKapasiteIstegi { public string HizmetKimligi { get; set; } = string.Empty; public string HizmetSurumu { get; set; } = "1.0"; public int EklenecekKapasite { get; set; } = 1; }

public sealed class UrunOlusturIstegi
{
    public string UrunAdi { get; set; } = string.Empty;
    public string UygulamaKimligi { get; set; } = string.Empty;
    public string Kategori { get; set; } = string.Empty;
    public string UrunTuru { get; set; } = "uygulama";
    public string FiyatlandirmaModeli { get; set; } = "abonelik";
    public decimal AbonelikUcreti { get; set; }
    public decimal KullanimBasinaUcret { get; set; }
    public string ArkaUcHizmetKimligi { get; set; } = string.Empty;
    public string ArkaUcHizmetSurumu { get; set; } = "1.0";
    public string ProtokolKimligi { get; set; } = string.Empty;
    public List<string> DesteklenenPlatformlar { get; set; } = [];
    public List<string> GerekliPlatformlar { get; set; } = [];
    public List<string> Bagimliliklar { get; set; } = [];
}

public sealed class UrunGuncelleIstegi { public string UrunKimligi { get; set; } = string.Empty; public decimal AbonelikUcreti { get; set; } public decimal KullanimBasinaUcret { get; set; } public bool Aktif { get; set; } }
public sealed class UrunKapasiteIstegi { public string UrunKimligi { get; set; } = string.Empty; public int EklenecekKapasite { get; set; } = 100; }
public sealed class ProtokolOlusturIstegi { public string ProtokolAdi { get; set; } = string.Empty; public string Surum { get; set; } = "1.0"; public string Aciklama { get; set; } = string.Empty; public string LisansModeli { get; set; } = "acik"; public decimal BenimsemeBedeli { get; set; } public decimal TickLisansBedeli { get; set; } }
public sealed class ProtokolBenimseIstegi { public string ProtokolKimligi { get; set; } = string.Empty; }
public sealed class SozlesmeKabulIstegi { public string TeklifKimligi { get; set; } = string.Empty; }

public sealed class IslemSonucu
{
    public bool Basarili { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public object? Veri { get; set; }
    public static IslemSonucu Basari(string aciklama, object? veri = null) => new() { Basarili = true, Aciklama = aciklama, Veri = veri };
    public static IslemSonucu Hata(string aciklama) => new() { Basarili = false, Aciklama = aciklama };
}
