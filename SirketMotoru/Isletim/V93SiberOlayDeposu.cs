using System.Reflection;

namespace SirketMotoru.Isletim;

public sealed class V93SiberOlayKaydi
{
    public string OlayKimligi { get; set; } = string.Empty;
    public long TickNumarasi { get; set; }
    public string SirketKimligi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public string SaldiriTuru { get; set; } = string.Empty;
    public string KaynakUlke { get; set; } = string.Empty;
    public string KaynakBolge { get; set; } = string.Empty;
    public double KaynakEnlem { get; set; }
    public double KaynakBoylam { get; set; }
    public double HedefEnlem { get; set; }
    public double HedefBoylam { get; set; }
    public bool Engellendi { get; set; }
    public int ZorlukSeviyesi { get; set; }
    public decimal FinansalEtki { get; set; }
    public DateTimeOffset Zaman { get; set; } = DateTimeOffset.UtcNow;
}

public static class V93SiberOlayDeposu
{
    private static readonly object Kilit = new();
    private static readonly List<V93SiberOlayKaydi> Olaylar = [];
    private static readonly (string Ulke, string Bolge, double Enlem, double Boylam)[] Kaynaklar =
    [
        ("ABD", "Kuzey Amerika", 38.9, -77.0),
        ("Brezilya", "Güney Amerika", -15.8, -47.9),
        ("Almanya", "Avrupa", 52.5, 13.4),
        ("Hollanda", "Avrupa", 52.4, 4.9),
        ("Romanya", "Avrupa", 44.4, 26.1),
        ("Rusya", "Avrasya", 55.8, 37.6),
        ("Hindistan", "Güney Asya", 28.6, 77.2),
        ("Çin", "Doğu Asya", 39.9, 116.4),
        ("Japonya", "Doğu Asya", 35.7, 139.7),
        ("Singapur", "Güneydoğu Asya", 1.35, 103.8),
        ("Avustralya", "Okyanusya", -33.9, 151.2),
        ("Güney Afrika", "Afrika", -26.2, 28.0),
        ("Nijerya", "Afrika", 9.1, 7.5),
        ("Türkiye", "Yakın Bölge", 39.9, 32.9)
    ];

    public static void Kaydet(
        long tick,
        string sirketKimligi,
        string sirketAdi,
        string hizmetKimligi,
        int zorluk,
        bool engellendi,
        decimal finansalEtki)
    {
        int karma = HashCode.Combine(tick, sirketKimligi, hizmetKimligi, zorluk, engellendi) & int.MaxValue;
        var kaynak = Kaynaklar[karma % Kaynaklar.Length];
        string tur = SaldiriTuru(hizmetKimligi, zorluk, karma);
        (double hedefEnlem, double hedefBoylam) = Hedef(sirketKimligi);

        lock (Kilit)
        {
            Olaylar.Add(new V93SiberOlayKaydi
            {
                OlayKimligi = $"siber-{Guid.NewGuid():N}",
                TickNumarasi = tick,
                SirketKimligi = sirketKimligi,
                SirketAdi = sirketAdi,
                SaldiriTuru = tur,
                KaynakUlke = kaynak.Ulke,
                KaynakBolge = kaynak.Bolge,
                KaynakEnlem = kaynak.Enlem,
                KaynakBoylam = kaynak.Boylam,
                HedefEnlem = hedefEnlem,
                HedefBoylam = hedefBoylam,
                Engellendi = engellendi,
                ZorlukSeviyesi = Math.Max(1, zorluk),
                FinansalEtki = finansalEtki,
                Zaman = DateTimeOffset.Now
            });
            if (Olaylar.Count > 600) Olaylar.RemoveRange(0, Olaylar.Count - 600);
        }
    }

    public static IReadOnlyList<V93SiberOlayKaydi> Getir(int azami = 300)
    {
        lock (Kilit)
            return Olaylar.OrderByDescending(x => x.TickNumarasi).ThenByDescending(x => x.Zaman)
                .Take(Math.Clamp(azami, 1, 600)).Select(Kopyala).ToList().AsReadOnly();
    }

    public static async Task PazaraYayinlaAsync(
        V9EkonomiYoneticisi v9,
        long tick,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(v9);
        FieldInfo pazarAlani = v9.GetType().GetField("_pazar", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Siber olay pazar alanı bulunamadı.");
        MethodInfo kaydet = v9.GetType().GetMethod("KaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Siber olay pazar kayıt metodu bulunamadı.");
        V9PazarDosyasi pazar = (V9PazarDosyasi)(pazarAlani.GetValue(v9)
            ?? throw new InvalidOperationException("Siber olay pazar verisi boş."));

        List<V93SiberOlayKaydi> yeni;
        lock (Kilit) yeni = Olaylar.Where(x => x.TickNumarasi == tick).Select(Kopyala).ToList();
        foreach (V93SiberOlayKaydi olay in yeni)
        {
            pazar.Haberler.RemoveAll(x => x.HaberKimligi.Equals(olay.OlayKimligi, StringComparison.OrdinalIgnoreCase));
            pazar.Haberler.Add(new V9HaberKaydi
            {
                HaberKimligi = olay.OlayKimligi,
                TickNumarasi = olay.TickNumarasi,
                Baslik = olay.Engellendi
                    ? $"🛡️ {olay.SirketAdi} saldırıyı püskürttü"
                    : $"🚨 {olay.SirketAdi} siber ihlal yaşadı",
                Aciklama = $"{olay.KaynakUlke} kaynaklı {olay.SaldiriTuru} girişimi " +
                    (olay.Engellendi
                        ? $"başarıyla engellendi. Zorluk: {olay.ZorlukSeviyesi}."
                        : $"savunmayı aştı. Finansal kayıp: {Math.Abs(olay.FinansalEtki):N2} TL."),
                Tur = "siber",
                Onem = olay.Engellendi ? "normal" : olay.FinansalEtki <= -5_000m ? "kritik" : "manset",
                SirketKimligi = olay.SirketKimligi,
                SirketAdi = olay.SirketAdi,
                FinansalEtki = olay.FinansalEtki,
                Rozet = olay.Engellendi ? "SİBER SAVUNMA" : "SİBER SALDIRI",
                Ikon = olay.Engellendi ? "🛡️" : "🚨",
                Zaman = olay.Zaman,
                KaynakUlke = olay.KaynakUlke,
                KaynakBolge = olay.KaynakBolge,
                KaynakEnlem = olay.KaynakEnlem,
                KaynakBoylam = olay.KaynakBoylam,
                HedefEnlem = olay.HedefEnlem,
                HedefBoylam = olay.HedefBoylam,
                SaldiriTuru = olay.SaldiriTuru,
                Engellendi = olay.Engellendi
            });
        }
        pazar.Haberler = pazar.Haberler.OrderByDescending(x => x.TickNumarasi).ThenByDescending(x => x.Zaman).Take(600).ToList();
        V9PazarDeposu.Guncelle(pazar);
        object? sonuc = kaydet.Invoke(v9, [cancellationToken]);
        if (sonuc is Task gorev) await gorev;
    }

    private static string SaldiriTuru(string hizmet, int zorluk, int karma)
    {
        string h = hizmet.ToLowerInvariant();
        if (h.Contains("kimlik") || h.Contains("oturum")) return "Kimlik bilgisi ele geçirme";
        if (h.Contains("odeme") || h.Contains("ticaret")) return "Ödeme sahteciliği";
        if (h.Contains("dosya") || h.Contains("veri")) return "Veri sızdırma";
        if (h.Contains("isletim") || zorluk >= 8) return "Uzaktan kod çalıştırma";
        string[] turler = ["DDoS", "API istismarı", "Yetki yükseltme", "Bot trafiği", "Enjeksiyon", "Tedarik zinciri saldırısı"];
        return turler[karma % turler.Length];
    }

    private static (double Enlem, double Boylam) Hedef(string sirketKimligi)
    {
        int karma = sirketKimligi.GetHashCode(StringComparison.OrdinalIgnoreCase) & int.MaxValue;
        return (39.0 + karma % 60 / 100d, 28.0 + karma % 160 / 100d);
    }

    private static V93SiberOlayKaydi Kopyala(V93SiberOlayKaydi x) => new()
    {
        OlayKimligi = x.OlayKimligi,
        TickNumarasi = x.TickNumarasi,
        SirketKimligi = x.SirketKimligi,
        SirketAdi = x.SirketAdi,
        SaldiriTuru = x.SaldiriTuru,
        KaynakUlke = x.KaynakUlke,
        KaynakBolge = x.KaynakBolge,
        KaynakEnlem = x.KaynakEnlem,
        KaynakBoylam = x.KaynakBoylam,
        HedefEnlem = x.HedefEnlem,
        HedefBoylam = x.HedefBoylam,
        Engellendi = x.Engellendi,
        ZorlukSeviyesi = x.ZorlukSeviyesi,
        FinansalEtki = x.FinansalEtki,
        Zaman = x.Zaman
    };
}
