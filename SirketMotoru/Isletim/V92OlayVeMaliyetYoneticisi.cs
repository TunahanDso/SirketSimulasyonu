using System.Text;
using System.Text.Json;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class V92OlayDosyasi
{
    public int Surum { get; set; } = 92;
    public long SonTick { get; set; }
    public List<V9HaberKaydi> Haberler { get; set; } = [];
}

public sealed class V92OlayVeMaliyetYoneticisi
{
    private sealed record OlaySablonu(
        string Baslik,
        string Aciklama,
        string Tur,
        string Ikon,
        decimal AsgariEtki,
        decimal AzamiEtki,
        double Itibar,
        double Guvenilirlik,
        double Performans,
        double Guvenlik,
        string Onem = "normal");

    private static readonly IReadOnlyList<OlaySablonu> Olaylar =
    [
        new("Viral kullanıcı dalgası", "Bir ürün sosyal ağlarda gündem oldu; yeni kullanıcı akışı kasaya yansıdı.", "pazar", "🚀", 180m, 900m, 0.04, 0.02, 0, 0),
        new("Kurumsal müşteri anlaşması", "Şirket önemli bir kurumsal müşteriyle kısa dönemli anlaşma imzaladı.", "anlasma", "🤝", 250m, 1_200m, 0.05, 0.04, 0, 0),
        new("Basın övgüsü", "Teknoloji basını şirketin ürün kalitesini öne çıkardı.", "itibar", "🏆", 0m, 180m, 0.12, 0.03, 0, 0),
        new("Verimli veri merkezi pazarlığı", "Enerji ve barındırma anlaşması beklenenden iyi sonuçlandı.", "altyapi", "⚡", 100m, 500m, 0.02, 0, 0.02, 0),
        new("Topluluk katkısı", "Açık kaynak topluluğu kritik bir hata düzeltmesine katkı sundu.", "teknoloji", "🧩", 0m, 250m, 0.03, 0.02, 0.05, 0.02),
        new("Başarılı güvenlik denetimi", "Bağımsız güvenlik denetimi olumlu tamamlandı.", "guvenlik", "🛡️", 0m, 220m, 0.04, 0.04, 0, 0.12),
        new("Destek ekibi takdir topladı", "Müşteri destek ekibi hızlı geri dönüşleriyle memnuniyet yarattı.", "musteri", "💬", 0m, 180m, 0.03, 0.05, 0, 0),
        new("Yeni yetenek transferi", "Deneyimli bir geliştirici ekibe katıldı; geliştirme disiplini güçlendi.", "personel", "🧠", -180m, -60m, 0.02, 0.02, 0.06, 0),
        new("Pazarlama kampanyası tuttu", "Kampanya dönüşüm oranı beklentilerin üzerine çıktı.", "pazarlama", "📣", 120m, 700m, 0.04, 0, 0, 0),
        new("Stratejik entegrasyon", "Başka bir ekosistemle yapılan entegrasyon ürün erişimini artırdı.", "ekosistem", "🔗", 120m, 650m, 0.03, 0.04, 0.02, 0),
        new("Beklenmeyen sunucu faturası", "Enerji ve ağ kullanımındaki artış ek maliyet doğurdu.", "maliyet", "🧾", -520m, -90m, -0.01, -0.01, 0, 0),
        new("Kısa süreli hizmet kesintisi", "Bir servis kısa süreliğine ulaşılamadı; operasyon ekibi müdahale etti.", "kesinti", "🚨", -700m, -160m, -0.04, -0.06, -0.04, 0),
        new("Müşteri şikâyet dalgası", "Bazı müşteriler destek süresinden memnun kalmadı.", "musteri", "📉", -400m, -80m, -0.07, -0.04, 0, 0),
        new("Lisans yenileme gideri", "Kritik geliştirme araçlarının lisansları yenilendi.", "maliyet", "🧰", -480m, -120m, 0, 0, 0.01, 0),
        new("Personel kaybı", "Ekipten bir uzman ayrıldı; teslimat planı yeniden düzenlendi.", "personel", "🧳", -650m, -140m, -0.03, -0.03, -0.05, 0),
        new("Hatalı sürüm geri çekildi", "Yeni sürümdeki hata nedeniyle hızlı bir geri alma operasyonu yapıldı.", "urun", "↩️", -800m, -180m, -0.04, -0.05, -0.05, 0),
        new("Dolandırıcılık girişimi engellendi", "Şüpheli ödeme trafiği başarıyla durduruldu.", "guvenlik", "🔒", 0m, 250m, 0.02, 0.03, 0, 0.08),
        new("Küçük veri sızıntısı şüphesi", "Sınırlı bir güvenlik olayı araştırılıyor; zarar kontrol altında.", "guvenlik", "⚠️", -900m, -220m, -0.05, -0.06, 0, -0.08),
        new("Bulut bölgesi arızası", "Bölgesel altyapı sorunu geçici kapasite baskısı yarattı.", "altyapi", "☁️", -850m, -180m, -0.03, -0.04, -0.04, 0),
        new("Tedarikçi indirimi", "Altyapı tedarikçisi dönemsel indirim uyguladı.", "maliyet", "💸", 80m, 420m, 0, 0, 0, 0),
        new("Ürün incelemesi yayımlandı", "Popüler bir içerik üreticisi ürünü ayrıntılı biçimde inceledi.", "medya", "🎥", 50m, 550m, 0.05, 0, 0, 0),
        new("Hız rekoru", "Şirket sunucusu performans testinde dikkat çekici sonuç aldı.", "performans", "⚙️", 0m, 240m, 0.03, 0.03, 0.10, 0),
        new("Rakip fiyat baskısı", "Rakiplerin agresif fiyatlaması kısa süreli gelir baskısı oluşturdu.", "rekabet", "📊", -500m, -100m, -0.02, 0, 0, 0),
        new("Yasal danışmanlık gideri", "Yeni düzenlemelere uyum için danışmanlık hizmeti alındı.", "hukuk", "⚖️", -600m, -130m, 0, 0.02, 0, 0),
        new("Üniversite iş birliği", "Bir araştırma ekibiyle ortak geliştirme protokolü imzalandı.", "arge", "🎓", -120m, 350m, 0.04, 0.02, 0.04, 0),
        new("Ödül gecesi", "Şirket yenilikçilik kategorisinde ödüle layık görüldü.", "itibar", "✨", 150m, 800m, 0.12, 0.04, 0, 0, "manset"),
        new("Büyük satın alma teklifi", "Piyasada şirket hakkında dikkat çekici bir satın alma söylentisi yayıldı.", "borsa", "💎", 500m, 2_500m, 0.10, 0.05, 0, 0, "kritik"),
        new("Nadir veri merkezi kazası", "Fiziksel altyapıda ciddi fakat sınırlı bir kaza yaşandı.", "felaket", "🔥", -4_000m, -1_500m, -0.10, -0.10, -0.08, -0.04, "kritik"),
        new("Dev kamu ihalesi", "Şirket yüksek görünürlüğe sahip büyük bir kamu ihalesi kazandı.", "anlasma", "🏛️", 1_500m, 4_000m, 0.12, 0.08, 0, 0, "kritik")
    ];

    private readonly SirketYoneticisi _sirketler;
    private readonly string _dosyaYolu;
    private readonly Random _rastgele = new(20260727);
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private V92OlayDosyasi _veri = new();

    public V92OlayVeMaliyetYoneticisi(SirketYoneticisi sirketler, string motorVerileriKlasoru)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _dosyaYolu = Path.Combine(Path.GetFullPath(motorVerileriKlasoru), "olaylar-v92.json");
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_dosyaYolu))
            {
                string json = await File.ReadAllTextAsync(_dosyaYolu, cancellationToken);
                _veri = JsonSerializer.Deserialize<V92OlayDosyasi>(json) ?? new();
            }
            _veri.Haberler ??= [];
            KonsolKayitcisi.Basari("V9.2 olay merkezi hazır | Şaşaalı haberler ve kontrollü gerçek etkiler aktif.");
        }
        finally { _kilit.Release(); }
    }

    public async Task TickSonuAsync(long tick, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            V9PazarDosyasi pazar = V9PazarDeposu.Getir();
            foreach (SirketKaydi sirket in _sirketler.SirketKayitlari.Where(x => x.BagliMi))
                EkMaliyetiUygula(sirket, pazar);

            int olaySayisi = _rastgele.NextDouble() < 0.72 ? 1 : 0;
            if (_rastgele.NextDouble() < 0.20) olaySayisi++;
            for (int i = 0; i < olaySayisi; i++) OlayUret(tick, pazar);

            _veri.SonTick = tick;
            _veri.Haberler = _veri.Haberler
                .OrderByDescending(x => x.TickNumarasi)
                .ThenByDescending(x => x.Zaman)
                .Take(500)
                .ToList();
            pazar.Haberler = pazar.Haberler
                .Concat(_veri.Haberler)
                .GroupBy(x => x.HaberKimligi, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .OrderByDescending(x => x.TickNumarasi)
                .ThenByDescending(x => x.Zaman)
                .Take(500)
                .ToList();
            V9PazarDeposu.Guncelle(pazar);
            await KaydetAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    private static void EkMaliyetiUygula(SirketKaydi sirket, V9PazarDosyasi pazar)
    {
        V9SirketPazarOzeti? ozet = pazar.SirketOzetleri.FirstOrDefault(x =>
            x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase));
        V9SirketKapasiteDurumu? kapasite = pazar.SirketKapasiteleri.TryGetValue(sirket.SirketKimligi, out V9SirketKapasiteDurumu? k) ? k : null;
        decimal ek = decimal.Round(
            8m +
            Math.Max(0, ozet?.IsletmeGideri ?? 0) * 0.12m +
            Math.Max(0, kapasite?.KullanilanKapasite ?? 0) * 0.0012m +
            Math.Max(0, (ozet?.AktifUygulamaKullanicisi ?? 0) + (ozet?.IsletimSistemiKullanicisi ?? 0)) * 0.00015m,
            2);
        ek = Math.Clamp(ek, 8m, 650m);
        decimal odenen = Math.Min(Math.Max(0, sirket.Kasa), ek);
        sirket.Kasa -= odenen;
        if (ozet is not null)
        {
            ozet.IsletmeGideri += odenen;
            ozet.NetKazanc -= odenen;
        }
    }

    private void OlayUret(long tick, V9PazarDosyasi pazar)
    {
        List<SirketKaydi> adaylar = _sirketler.SirketKayitlari.Where(x => x.BagliMi).ToList();
        if (adaylar.Count == 0) return;
        SirketKaydi sirket = adaylar[_rastgele.Next(adaylar.Count)];

        // Kritik şablonlar nadir; normal şablonlar sık seçilir.
        IReadOnlyList<OlaySablonu> havuz = _rastgele.NextDouble() < 0.035
            ? Olaylar.Where(x => x.Onem == "kritik").ToList()
            : Olaylar.Where(x => x.Onem != "kritik").ToList();
        OlaySablonu olay = havuz[_rastgele.Next(havuz.Count)];
        decimal istenen = RastgeleTutar(olay.AsgariEtki, olay.AzamiEtki);
        decimal uygulanan = istenen >= 0
            ? istenen
            : -Math.Min(Math.Max(0, sirket.Kasa), Math.Abs(istenen));
        sirket.Kasa += uygulanan;
        if (uygulanan > 0) sirket.ToplamGelir += uygulanan;
        else sirket.ToplamGuvenlikKaybi += olay.Tur == "guvenlik" ? Math.Abs(uygulanan) : 0;

        sirket.ItibarPuani = Sinirla(sirket.ItibarPuani + olay.Itibar);
        sirket.GuvenilirlikPuani = Sinirla(sirket.GuvenilirlikPuani + olay.Guvenilirlik);
        sirket.PerformansPuani = Sinirla(sirket.PerformansPuani + olay.Performans);
        sirket.GuvenlikPuani = Sinirla(sirket.GuvenlikPuani + olay.Guvenlik);

        V9SirketPazarOzeti? ozet = pazar.SirketOzetleri.FirstOrDefault(x =>
            x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase));
        if (ozet is not null)
        {
            if (uygulanan >= 0) ozet.OlayGeliri += uygulanan;
            else ozet.OlayGideri += Math.Abs(uygulanan);
            ozet.NetKazanc += uygulanan;
        }

        V9HaberKaydi haber = new()
        {
            HaberKimligi = $"v92-olay-{Guid.NewGuid():N}",
            TickNumarasi = tick,
            Baslik = $"{olay.Ikon} {sirket.SirketAdi}: {olay.Baslik}",
            Aciklama = olay.Aciklama + (uygulanan == 0 ? "" : $" Finansal etki: {uygulanan:+#,##0.00;-#,##0.00} TL."),
            Tur = olay.Tur,
            Onem = olay.Onem,
            SirketKimligi = sirket.SirketKimligi,
            SirketAdi = sirket.SirketAdi,
            FinansalEtki = uygulanan,
            Rozet = olay.Onem == "kritik" ? "FLAŞ GELİŞME" : olay.Onem == "manset" ? "MANŞET" : "PİYASA HABERİ",
            Ikon = olay.Ikon,
            Zaman = DateTimeOffset.Now
        };
        _veri.Haberler.Add(haber);
        KonsolKayitcisi.Bilgi($"HABER | {haber.Baslik} | {haber.FinansalEtki:+#,##0.00;-#,##0.00;0.00} TL");
    }

    private decimal RastgeleTutar(decimal a, decimal b)
    {
        decimal min = Math.Min(a, b);
        decimal max = Math.Max(a, b);
        return decimal.Round(min + (decimal)_rastgele.NextDouble() * (max - min), 2);
    }

    private static double Sinirla(double deger) => Math.Clamp(deger, 0, 100);

    private async Task KaydetAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        string tmp = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(
            tmp,
            JsonSerializer.Serialize(_veri, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }),
            new UTF8Encoding(false),
            cancellationToken);
        File.Move(tmp, _dosyaYolu, overwrite: true);
    }
}
