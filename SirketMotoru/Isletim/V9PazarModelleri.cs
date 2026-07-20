using System.Text.Json;

namespace SirketMotoru.Isletim;

public sealed class V9PazarDosyasi
{
    public int Surum { get; set; } = 9;
    public long SonTick { get; set; }
    public DateTimeOffset GuncellenmeZamani { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, V9TalepKaydi> HizmetTalepleri { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, V9TalepKaydi> UygulamaTalepleri { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public V9TalepKaydi IsletimSistemiTalebi { get; set; } = new()
    {
        Anahtar = "isletim-sistemi",
        Ad = "İşletim sistemi",
        Tur = "isletim-sistemi",
        TrendCarpani = 1
    };
    public Dictionary<string, V9SirketKapasiteDurumu> SirketKapasiteleri { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<V9SirketPazarOzeti> SirketOzetleri { get; set; } = [];
    public List<V9HaberKaydi> Haberler { get; set; } = [];
}

public sealed class V9TalepKaydi
{
    public string Anahtar { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Tur { get; set; } = string.Empty;
    public int OncekiTalep { get; set; }
    public int BuTickTalep { get; set; }
    public int KarsilananTalep { get; set; }
    public int ArzKapasitesi { get; set; }
    public double TrendCarpani { get; set; } = 1;
    public double DegisimYuzdesi { get; set; }
    public string AktifTrend { get; set; } = "normal";
    public long TrendBitisTicki { get; set; }
    public List<V9TalepNoktasi> Gecmis { get; set; } = [];
    public List<V9ArzPayi> ArzDagilimi { get; set; } = [];
    public int KarsilanamayanTalep => Math.Max(0, BuTickTalep - KarsilananTalep);
    public double KarsilanmaOrani => BuTickTalep <= 0 ? 0 : Math.Clamp(KarsilananTalep * 100d / BuTickTalep, 0, 100);
}

public sealed class V9TalepNoktasi
{
    public long TickNumarasi { get; set; }
    public int Talep { get; set; }
    public int Karsilanan { get; set; }
    public int Arz { get; set; }
    public double TrendCarpani { get; set; }
}

public sealed class V9ArzPayi
{
    public string SirketKimligi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public string UrunVeyaHizmetKimligi { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public int Kapasite { get; set; }
    public int Karsilanan { get; set; }
    public double PazarPayi { get; set; }
    public decimal Fiyat { get; set; }
}

public sealed class V9SirketKapasiteDurumu
{
    public string SirketKimligi { get; set; } = string.Empty;
    public int ToplamFizikselKapasite { get; set; }
    public int AyrilmisKapasite { get; set; }
    public int KullanilanKapasite { get; set; }
    public Dictionary<string, int> Tahsisler { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool KullaniciElleAyarladi { get; set; }
    public int BosKapasite => Math.Max(0, ToplamFizikselKapasite - AyrilmisKapasite);
    public double DolulukOrani => ToplamFizikselKapasite <= 0 ? 0 : Math.Clamp(KullanilanKapasite * 100d / ToplamFizikselKapasite, 0, 300);
}

public sealed class V9SirketPazarOzeti
{
    public string SirketKimligi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public decimal HizmetGeliri { get; set; }
    public decimal UygulamaGeliri { get; set; }
    public decimal AbonelikGeliri { get; set; }
    public decimal ProtokolGeliri { get; set; }
    public decimal IsletmeGideri { get; set; }
    public decimal FinansmanGideri { get; set; }
    public decimal NetKazanc { get; set; }
    public int AktifUygulamaKullanicisi { get; set; }
    public int IsletimSistemiKullanicisi { get; set; }
    public int ToplamKapasite { get; set; }
    public int KullanilanKapasite { get; set; }
    public decimal AktifBorc { get; set; }
}

public sealed class V9HaberKaydi
{
    public string HaberKimligi { get; set; } = string.Empty;
    public long TickNumarasi { get; set; }
    public string Baslik { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public string Tur { get; set; } = "piyasa";
    public string Onem { get; set; } = "normal";
    public string SirketKimligi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public decimal FinansalEtki { get; set; }
    public DateTimeOffset Zaman { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class V9KapasiteTahsisIstegi
{
    public Dictionary<string, int> Tahsisler { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public readonly record struct V9FiyatAraligi(decimal Min, decimal Max, decimal Adim, decimal Onerilen);

public static class V9FiyatPolitikasi
{
    public static V9FiyatAraligi Aralik(string? urunTuru, string? kategori)
    {
        string tur = Normal(urunTuru);
        string kat = Normal(kategori);
        if (tur == "isletim-sistemi") return new(15m, 120m, 1m, 45m);
        if (tur == "platform") return new(8m, 160m, 1m, 40m);
        if (tur == "altyapi" || kat.Contains("altyapi")) return new(10m, 220m, 2m, 60m);
        if (tur == "yapay-zeka" || kat.Contains("yapay") || kat.Contains("ai")) return new(15m, 250m, 2m, 70m);
        if (tur == "oyun" || kat.Contains("oyun")) return new(2m, 80m, 1m, 20m);
        if (kat.Contains("finans") || kat.Contains("odeme") || kat.Contains("ticaret")) return new(5m, 140m, 1m, 35m);
        if (kat.Contains("gelistir") || kat.Contains("analitik") || kat.Contains("veri")) return new(6m, 180m, 1m, 45m);
        if (kat.Contains("mesaj") || kat.Contains("eposta") || kat.Contains("sosyal")) return new(1m, 60m, 0.5m, 12m);
        if (kat.Contains("guvenlik")) return new(8m, 180m, 1m, 50m);
        if (kat.Contains("medya") || kat.Contains("video") || kat.Contains("muzik")) return new(3m, 100m, 1m, 25m);
        return new(2m, 120m, 1m, 25m);
    }

    public static decimal Sinirla(string? urunTuru, string? kategori, decimal fiyat)
    {
        V9FiyatAraligi aralik = Aralik(urunTuru, kategori);
        decimal sonuc = Math.Clamp(fiyat <= 0 ? aralik.Onerilen : fiyat, aralik.Min, aralik.Max);
        return decimal.Round(sonuc / aralik.Adim, 0, MidpointRounding.AwayFromZero) * aralik.Adim;
    }

    private static string Normal(string? deger) =>
        (deger ?? string.Empty).Trim().ToLowerInvariant()
            .Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g')
            .Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c');
}

public static class V9PazarDeposu
{
    private static readonly object Kilit = new();
    private static V9PazarDosyasi _veri = new();

    public static void Guncelle(V9PazarDosyasi veri)
    {
        ArgumentNullException.ThrowIfNull(veri);
        lock (Kilit) _veri = Kopyala(veri);
    }

    public static V9PazarDosyasi Getir()
    {
        lock (Kilit) return Kopyala(_veri);
    }

    public static IReadOnlyDictionary<string, int> HizmetTalepDagilimi()
    {
        lock (Kilit)
            return _veri.HizmetTalepleri.ToDictionary(x => x.Key, x => x.Value.BuTickTalep, StringComparer.OrdinalIgnoreCase);
    }

    private static V9PazarDosyasi Kopyala(V9PazarDosyasi kaynak)
    {
        string json = JsonSerializer.Serialize(kaynak);
        return JsonSerializer.Deserialize<V9PazarDosyasi>(json) ?? new();
    }
}
