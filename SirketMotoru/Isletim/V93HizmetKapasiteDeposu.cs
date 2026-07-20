using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class V93HizmetKapasiteAnligi
{
    public string SirketKimligi { get; set; } = string.Empty;
    public long TickNumarasi { get; set; }
    public int AyrilanHizmetHavuzu { get; set; }
    public int KullanilanHizmetKapasitesi { get; set; }
    public int ReddedilenIsSayisi { get; set; }
    public Dictionary<string, int> HizmetKullanimlari { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int KalanHizmetKapasitesi => Math.Max(0, AyrilanHizmetHavuzu - KullanilanHizmetKapasitesi);
    public double DolulukOrani => AyrilanHizmetHavuzu <= 0
        ? 0
        : Math.Clamp(KullanilanHizmetKapasitesi * 100d / AyrilanHizmetHavuzu, 0, 300);
}

/// <summary>
/// Hizmet işleri seri tamamlansa bile tick boyunca tüketilen fiziksel kapasiteyi
/// kaybetmeden tutar. Kapasite iş tamamlanınca geri verilmez; sonraki tickte sıfırlanır.
/// </summary>
public static class V93HizmetKapasiteDeposu
{
    private static readonly object Kilit = new();
    private static long _tick;
    private static readonly Dictionary<string, V93HizmetKapasiteAnligi> Sirketler =
        new(StringComparer.OrdinalIgnoreCase);

    public static void TickBaslat(long tickNumarasi, IEnumerable<SirketKaydi> sirketKayitlari)
    {
        ArgumentNullException.ThrowIfNull(sirketKayitlari);
        lock (Kilit)
        {
            _tick = tickNumarasi;
            Sirketler.Clear();
            V9PazarDosyasi pazar = V9PazarDeposu.Getir();
            Dictionary<string, SirketKaydi> kayitlar = sirketKayitlari
                .ToDictionary(x => x.SirketKimligi, StringComparer.OrdinalIgnoreCase);

            foreach ((string sirketKimligi, V9SirketKapasiteDurumu kapasite) in pazar.SirketKapasiteleri)
            {
                int havuz = Math.Max(0, kapasite.HizmetHavuzu);
                Sirketler[sirketKimligi] = new V93HizmetKapasiteAnligi
                {
                    SirketKimligi = sirketKimligi,
                    TickNumarasi = tickNumarasi,
                    AyrilanHizmetHavuzu = havuz
                };

                // SirketKaydi.IsBaslat içindeki eski hizmet-başına eşzamanlı kontrol,
                // V9.3 ortak havuzundan ayrı ikinci bir kapasite otoritesi olmasın.
                if (!kayitlar.TryGetValue(sirketKimligi, out SirketKaydi? sirket)) continue;
                foreach (var hizmet in sirket.Hizmetler)
                    hizmet.AzamiEszamanliIs = hizmet.Aktif && havuz > 0 ? havuz : 0;
            }
        }
    }

    public static int IsMaliyeti(string? hizmetKimligi, int zorlukSeviyesi, bool siberSaldiri)
    {
        string aile = (hizmetKimligi ?? string.Empty).Split('.', 2)[0].Trim().ToLowerInvariant();
        int agirlik = aile switch
        {
            "yapay-zeka" => 4,
            "isletim" or "veritabani" or "medya" or "analitik" => 3,
            "guvenlik" or "odeme" or "dosya" or "arama" => 2,
            _ => 1
        };
        int maliyet = 1 + Math.Max(0, zorlukSeviyesi - 1) / 2 + agirlik - 1;
        if (siberSaldiri) maliyet += 2;
        return Math.Clamp(maliyet, 1, 12);
    }

    public static bool SigabilirMi(
        string sirketKimligi,
        string hizmetKimligi,
        int zorlukSeviyesi,
        bool siberSaldiri)
    {
        lock (Kilit)
        {
            V93HizmetKapasiteAnligi anlik = AnlikKilitsiz(sirketKimligi);
            int maliyet = IsMaliyeti(hizmetKimligi, zorlukSeviyesi, siberSaldiri);
            return anlik.KullanilanHizmetKapasitesi + maliyet <= anlik.AyrilanHizmetHavuzu;
        }
    }

    public static bool RezerveEt(
        string sirketKimligi,
        string hizmetKimligi,
        int zorlukSeviyesi,
        bool siberSaldiri,
        out int maliyet)
    {
        lock (Kilit)
        {
            V93HizmetKapasiteAnligi anlik = AnlikKilitsiz(sirketKimligi);
            maliyet = IsMaliyeti(hizmetKimligi, zorlukSeviyesi, siberSaldiri);
            if (anlik.KullanilanHizmetKapasitesi + maliyet > anlik.AyrilanHizmetHavuzu)
            {
                anlik.ReddedilenIsSayisi++;
                return false;
            }

            anlik.KullanilanHizmetKapasitesi += maliyet;
            anlik.HizmetKullanimlari.TryGetValue(hizmetKimligi, out int mevcut);
            anlik.HizmetKullanimlari[hizmetKimligi] = mevcut + maliyet;
            return true;
        }
    }

    public static V93HizmetKapasiteAnligi Getir(string sirketKimligi)
    {
        lock (Kilit) return Kopyala(AnlikKilitsiz(sirketKimligi));
    }

    public static IReadOnlyList<V93HizmetKapasiteAnligi> TumunuGetir()
    {
        lock (Kilit) return Sirketler.Values.Select(Kopyala).ToList().AsReadOnly();
    }

    private static V93HizmetKapasiteAnligi AnlikKilitsiz(string sirketKimligi)
    {
        if (Sirketler.TryGetValue(sirketKimligi, out V93HizmetKapasiteAnligi? anlik)) return anlik;
        anlik = new V93HizmetKapasiteAnligi
        {
            SirketKimligi = sirketKimligi,
            TickNumarasi = _tick,
            AyrilanHizmetHavuzu = 0
        };
        Sirketler[sirketKimligi] = anlik;
        return anlik;
    }

    private static V93HizmetKapasiteAnligi Kopyala(V93HizmetKapasiteAnligi x) => new()
    {
        SirketKimligi = x.SirketKimligi,
        TickNumarasi = x.TickNumarasi,
        AyrilanHizmetHavuzu = x.AyrilanHizmetHavuzu,
        KullanilanHizmetKapasitesi = x.KullanilanHizmetKapasitesi,
        ReddedilenIsSayisi = x.ReddedilenIsSayisi,
        HizmetKullanimlari = new Dictionary<string, int>(x.HizmetKullanimlari, StringComparer.OrdinalIgnoreCase)
    };
}
