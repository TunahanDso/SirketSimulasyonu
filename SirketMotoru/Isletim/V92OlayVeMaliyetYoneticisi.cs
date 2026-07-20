using System.Text;
using System.Text.Json;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class V92OlayDosyasi
{
    public int Surum { get; set; } = 93;
    public long SonTick { get; set; }
    public List<V9HaberKaydi> Haberler { get; set; } = [];
}

/// <summary>
/// V9.2 ekonomi denge paketi. Gelir büyüdükçe kademeli operasyon maliyeti
/// üretir; fakat kasadan fazla kesinti veya otomatik borç oluşturmaz.
/// Piyasa olayları daha sık, çeşitli ve şirket ölçeğine göre sınırlıdır.
/// </summary>
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
        bool Olumlu,
        string Onem = "normal");

    private static readonly IReadOnlyList<OlaySablonu> Olaylar =
    [
        new("Viral kullanıcı dalgası", "Bir ürün sosyal ağlarda gündem oldu ve yeni kullanıcı akışı başladı.", "pazar", "🚀", 220m, 1_100m, 0.04, 0.02, 0, 0, true),
        new("Kurumsal müşteri anlaşması", "Şirket önemli bir kurumsal müşteriyle dönemsel anlaşma imzaladı.", "anlasma", "🤝", 350m, 1_500m, 0.05, 0.04, 0, 0, true),
        new("Basın övgüsü", "Teknoloji basını şirketin ürün kalitesini öne çıkardı.", "itibar", "🏆", 80m, 450m, 0.12, 0.03, 0, 0, true),
        new("Verimli veri merkezi pazarlığı", "Enerji ve barındırma anlaşması beklenenden iyi sonuçlandı.", "altyapi", "⚡", 180m, 850m, 0.02, 0, 0.02, 0, true),
        new("Topluluk katkısı", "Açık kaynak topluluğu kritik bir hata düzeltmesine katkı sundu.", "teknoloji", "🧩", 80m, 500m, 0.03, 0.02, 0.05, 0.02, true),
        new("Başarılı güvenlik denetimi", "Bağımsız güvenlik denetimi olumlu tamamlandı.", "guvenlik", "🛡️", 100m, 600m, 0.04, 0.04, 0, 0.12, true),
        new("Destek ekibi takdir topladı", "Müşteri destek ekibi hızlı geri dönüşleriyle memnuniyet yarattı.", "musteri", "💬", 80m, 420m, 0.03, 0.05, 0, 0, true),
        new("Pazarlama kampanyası tuttu", "Kampanya dönüşüm oranı beklentilerin üzerine çıktı.", "pazarlama", "📣", 250m, 1_100m, 0.04, 0, 0, 0, true),
        new("Stratejik entegrasyon", "Başka bir ekosistemle yapılan entegrasyon ürün erişimini artırdı.", "ekosistem", "🔗", 220m, 950m, 0.03, 0.04, 0.02, 0, true),
        new("Tedarikçi indirimi", "Altyapı tedarikçisi dönemsel indirim uyguladı.", "maliyet", "💸", 120m, 650m, 0, 0, 0, 0, true),
        new("Ürün incelemesi yayımlandı", "Popüler bir içerik üreticisi ürünü ayrıntılı biçimde inceledi.", "medya", "🎥", 140m, 800m, 0.05, 0, 0, 0, true),
        new("Hız rekoru", "Şirket sunucusu performans testinde dikkat çekici sonuç aldı.", "performans", "⚙️", 100m, 550m, 0.03, 0.03, 0.10, 0, true),
        new("Üniversite iş birliği", "Bir araştırma ekibiyle ortak geliştirme protokolü imzalandı.", "arge", "🎓", 100m, 700m, 0.04, 0.02, 0.04, 0, true),
        new("Ödül gecesi", "Şirket yenilikçilik kategorisinde ödüle layık görüldü.", "itibar", "✨", 400m, 1_600m, 0.12, 0.04, 0, 0, true, "manset"),
        new("Büyük satın alma teklifi", "Piyasada şirket hakkında dikkat çekici bir satın alma söylentisi yayıldı.", "borsa", "💎", 1_000m, 4_500m, 0.10, 0.05, 0, 0, true, "kritik"),
        new("Dev kamu ihalesi", "Şirket yüksek görünürlüğe sahip büyük bir kamu ihalesi kazandı.", "anlasma", "🏛️", 2_000m, 6_000m, 0.12, 0.08, 0, 0, true, "kritik"),
        new("Beklenmeyen sunucu faturası", "Enerji ve ağ kullanımındaki artış ek maliyet doğurdu.", "maliyet", "🧾", -850m, -180m, -0.01, -0.01, 0, 0, false),
        new("Kısa süreli hizmet kesintisi", "Bir servis kısa süreliğine ulaşılamadı; operasyon ekibi müdahale etti.", "kesinti", "🚨", -1_100m, -250m, -0.04, -0.06, -0.04, 0, false),
        new("Müşteri şikâyet dalgası", "Bazı müşteriler destek süresinden memnun kalmadı.", "musteri", "📉", -750m, -160m, -0.07, -0.04, 0, 0, false),
        new("Lisans yenileme gideri", "Kritik geliştirme araçlarının lisansları yenilendi.", "maliyet", "🧰", -900m, -220m, 0, 0, 0.01, 0, false),
        new("Personel kaybı", "Ekipten bir uzman ayrıldı; teslimat planı yeniden düzenlendi.", "personel", "🧳", -1_100m, -260m, -0.03, -0.03, -0.05, 0, false),
        new("Hatalı sürüm geri çekildi", "Yeni sürümdeki hata nedeniyle hızlı bir geri alma operasyonu yapıldı.", "urun", "↩️", -1_400m, -320m, -0.04, -0.05, -0.05, 0, false),
        new("Küçük veri sızıntısı şüphesi", "Sınırlı bir güvenlik olayı araştırılıyor; zarar kontrol altında.", "guvenlik", "⚠️", -1_500m, -350m, -0.05, -0.06, 0, -0.08, false),
        new("Bulut bölgesi arızası", "Bölgesel altyapı sorunu geçici kapasite baskısı yarattı.", "altyapi", "☁️", -1_350m, -300m, -0.03, -0.04, -0.04, 0, false),
        new("Rakip fiyat baskısı", "Rakiplerin agresif fiyatlaması kısa süreli gelir baskısı oluşturdu.", "rekabet", "📊", -900m, -180m, -0.02, 0, 0, 0, false),
        new("Yasal danışmanlık gideri", "Yeni düzenlemelere uyum için danışmanlık hizmeti alındı.", "hukuk", "⚖️", -1_000m, -250m, 0, 0.02, 0, 0, false),
        new("Ödeme sağlayıcısı kesintisi", "Ödeme sağlayıcısındaki sorun tahsilat operasyonuna ek maliyet çıkardı.", "finans", "💳", -1_050m, -240m, -0.02, -0.02, 0, 0, false),
        new("Acil kapasite kiralaması", "Beklenmeyen trafik nedeniyle kısa süreli ek altyapı kiralandı.", "altyapi", "🖥️", -1_250m, -300m, 0, 0, -0.01, 0, false),
        new("Yanlış reklam kampanyası", "Hedefleme hatası kampanya bütçesinin bir kısmını boşa harcadı.", "pazarlama", "🎯", -950m, -220m, -0.03, 0, 0, 0, false),
        new("Tedarik zinciri gecikmesi", "Donanım tedarikindeki gecikme operasyon maliyetini artırdı.", "tedarik", "📦", -1_200m, -280m, -0.02, -0.02, -0.02, 0, false),
        new("Nadir veri merkezi kazası", "Fiziksel altyapıda ciddi fakat sınırlı bir kaza yaşandı.", "felaket", "🔥", -7_000m, -2_500m, -0.10, -0.10, -0.08, -0.04, false, "kritik"),
        new("Büyük sözleşme iptali", "Önemli bir kurumsal müşteri sözleşmesini erken sonlandırdı.", "anlasma", "📄", -5_500m, -1_800m, -0.10, -0.08, 0, 0, false, "kritik")
    ];

    private readonly SirketYoneticisi _sirketler;
    private readonly string _dosyaYolu;
    private readonly Random _rastgele = new(20260729);
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
            _veri.Surum = 93;
            _veri.Haberler ??= [];
            KonsolKayitcisi.Basari(
                "V9.2 ekonomi denge paketi hazır | Kademeli ciro maliyeti ve yoğun piyasa olayları aktif.");
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
                DengeliMaliyetiUygula(sirket, pazar, tick);

            int olaySayisi = 1;
            if (_rastgele.NextDouble() < 0.68) olaySayisi++;
            if (_rastgele.NextDouble() < 0.28) olaySayisi++;
            if (_rastgele.NextDouble() < 0.08) olaySayisi++;
            for (int i = 0; i < olaySayisi; i++) OlayUret(tick, pazar);

            _veri.SonTick = tick;
            _veri.Haberler = _veri.Haberler
                .OrderByDescending(x => x.TickNumarasi)
                .ThenByDescending(x => x.Zaman)
                .Take(700)
                .ToList();
            pazar.Haberler = pazar.Haberler
                .Concat(_veri.Haberler)
                .GroupBy(x => x.HaberKimligi, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .OrderByDescending(x => x.TickNumarasi)
                .ThenByDescending(x => x.Zaman)
                .Take(700)
                .ToList();
            V9PazarDeposu.Guncelle(pazar);
            await KaydetAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    private static void DengeliMaliyetiUygula(SirketKaydi sirket, V9PazarDosyasi pazar, long tick)
    {
        V9SirketPazarOzeti? ozet = pazar.SirketOzetleri.FirstOrDefault(x =>
            x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase));
        if (ozet is null) return;
        V9SirketKapasiteDurumu? kapasite = pazar.SirketKapasiteleri.TryGetValue(
            sirket.SirketKimligi,
            out V9SirketKapasiteDurumu? k) ? k : null;

        decimal ciro = Math.Max(0,
            ozet.HizmetGeliri +
            ozet.UygulamaGeliri +
            ozet.ProtokolGeliri);
        int aktifHizmet = sirket.Hizmetler.Count(x => x.Aktif);
        int toplamKapasite = Math.Max(0, kapasite?.ToplamFizikselKapasite ?? 0);
        int ayrilmis = Math.Max(0, kapasite?.AyrilmisKapasite ?? 0);
        int kullanilan = Math.Max(0, kapasite?.KullanilanKapasite ?? 0);
        int kullanici = Math.Max(0, ozet.AktifUygulamaKullanicisi + ozet.IsletimSistemiKullanicisi);

        decimal sabit =
            55m +
            aktifHizmet * 1.50m +
            ayrilmis * 0.004m +
            kullanilan * 0.030m +
            kullanici * 0.0020m +
            Math.Max(0, toplamKapasite - 2_500) * 0.003m;

        decimal degisken = ciro * 0.34m;
        if (ciro > 2_000m) degisken += (ciro - 2_000m) * 0.05m;
        if (ciro > 10_000m) degisken += (ciro - 10_000m) * 0.07m;
        if (ciro > 50_000m) degisken += (ciro - 50_000m) * 0.06m;

        double ortalamaSkor =
            (sirket.KodKalitesiPuani +
             sirket.PerformansPuani +
             sirket.GuvenlikPuani +
             sirket.GuvenilirlikPuani) / 4d;
        decimal verimCarpani = ortalamaSkor switch
        {
            < 45 => 1.28m,
            < 60 => 1.18m,
            < 75 => 1.08m,
            > 92 => 0.95m,
            > 85 => 0.98m,
            _ => 1m
        };

        double doluluk = toplamKapasite <= 0 ? 0 : kullanilan / (double)toplamKapasite;
        decimal kapasiteBaskisi = doluluk <= 0.80
            ? 0
            : ciro * (decimal)Math.Clamp((doluluk - 0.80) * 0.35, 0, 0.12) + sabit * 0.20m;

        decimal hesaplanan = decimal.Round((sabit + degisken + kapasiteBaskisi) * verimCarpani, 2);
        decimal guvenliTavan = decimal.Round(sabit + ciro * 0.72m, 2);
        decimal gider = Math.Clamp(hesaplanan, Math.Max(35m, sabit), Math.Max(sabit, guvenliTavan));
        decimal odenen = Math.Min(Math.Max(0, sirket.Kasa), gider);
        sirket.Kasa -= odenen;
        ozet.IsletmeGideri += odenen;
        ozet.NetKazanc -= odenen;

        if (odenen > 1_500m || ciro > 5_000m)
        {
            pazar.Haberler.Add(new V9HaberKaydi
            {
                HaberKimligi = $"denge-maliyet-{tick}-{sirket.SirketKimligi}",
                TickNumarasi = tick,
                Baslik = $"🏢 {sirket.SirketAdi}: ölçeklenen operasyon bütçesi",
                Aciklama =
                    $"Tick cirosu {ciro:N2} TL; altyapı, kullanıcı, kapasite ve operasyon gideri {odenen:N2} TL olarak gerçekleşti.",
                Tur = "maliyet",
                Onem = odenen > 10_000m ? "manset" : "normal",
                SirketKimligi = sirket.SirketKimligi,
                SirketAdi = sirket.SirketAdi,
                FinansalEtki = -odenen,
                Rozet = "OPERASYON RAPORU",
                Ikon = "🏢",
                Zaman = DateTimeOffset.Now
            });
        }
    }

    private void OlayUret(long tick, V9PazarDosyasi pazar)
    {
        List<SirketKaydi> adaylar = _sirketler.SirketKayitlari.Where(x => x.BagliMi).ToList();
        if (adaylar.Count == 0) return;
        SirketKaydi sirket = adaylar[_rastgele.Next(adaylar.Count)];
        V9SirketPazarOzeti? ozet = pazar.SirketOzetleri.FirstOrDefault(x =>
            x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase));
        decimal ciro = Math.Max(0,
            (ozet?.HizmetGeliri ?? 0) +
            (ozet?.UygulamaGeliri ?? 0) +
            (ozet?.ProtokolGeliri ?? 0));

        bool kritik = _rastgele.NextDouble() < 0.045;
        bool olumlu = _rastgele.NextDouble() < 0.48;
        List<OlaySablonu> havuz = Olaylar.Where(x =>
            x.Olumlu == olumlu &&
            (kritik ? x.Onem == "kritik" : x.Onem != "kritik")).ToList();
        if (havuz.Count == 0) havuz = Olaylar.Where(x => x.Olumlu == olumlu).ToList();
        OlaySablonu olay = havuz[_rastgele.Next(havuz.Count)];

        decimal temelEtki = RastgeleTutar(olay.AsgariEtki, olay.AzamiEtki);
        decimal olcekCarpani = Math.Clamp(
            0.85m + (decimal)Math.Sqrt((double)(ciro / 2_000m)) * 0.22m,
            0.85m,
            kritik ? 3.50m : 2.60m);
        decimal istenen = decimal.Round(temelEtki * olcekCarpani, 2);

        decimal uygulanan;
        if (istenen >= 0)
        {
            decimal tavan = kritik
                ? Math.Max(2_500m, ciro * 0.22m + 4_000m)
                : Math.Max(700m, ciro * 0.10m + 1_800m);
            uygulanan = Math.Min(istenen, tavan);
        }
        else
        {
            decimal nakitTavan = Math.Max(0, sirket.Kasa) * (kritik ? 0.14m : 0.075m);
            decimal faaliyetTavani = kritik
                ? Math.Max(3_000m, ciro * 0.28m + 3_000m)
                : Math.Max(900m, ciro * 0.14m + 1_200m);
            uygulanan = -Math.Min(Math.Abs(istenen), Math.Min(nakitTavan, faaliyetTavani));
        }

        sirket.Kasa += uygulanan;
        if (uygulanan > 0) sirket.ToplamGelir += uygulanan;
        else if (olay.Tur == "guvenlik") sirket.ToplamGuvenlikKaybi += Math.Abs(uygulanan);

        sirket.ItibarPuani = Sinirla(sirket.ItibarPuani + olay.Itibar);
        sirket.GuvenilirlikPuani = Sinirla(sirket.GuvenilirlikPuani + olay.Guvenilirlik);
        sirket.PerformansPuani = Sinirla(sirket.PerformansPuani + olay.Performans);
        sirket.GuvenlikPuani = Sinirla(sirket.GuvenlikPuani + olay.Guvenlik);

        if (ozet is not null)
        {
            if (uygulanan >= 0) ozet.OlayGeliri += uygulanan;
            else ozet.OlayGideri += Math.Abs(uygulanan);
            ozet.NetKazanc += uygulanan;
        }

        V9HaberKaydi haber = new()
        {
            HaberKimligi = $"v93-olay-{Guid.NewGuid():N}",
            TickNumarasi = tick,
            Baslik = $"{olay.Ikon} {sirket.SirketAdi}: {olay.Baslik}",
            Aciklama = olay.Aciklama +
                (uygulanan == 0 ? "" : $" Finansal etki: {uygulanan:+#,##0.00;-#,##0.00} TL."),
            Tur = olay.Tur,
            Onem = olay.Onem,
            SirketKimligi = sirket.SirketKimligi,
            SirketAdi = sirket.SirketAdi,
            FinansalEtki = uygulanan,
            Rozet = olay.Onem == "kritik"
                ? "FLAŞ GELİŞME"
                : olay.Onem == "manset" ? "MANŞET" : olumlu ? "İYİ HABER" : "PİYASA UYARISI",
            Ikon = olay.Ikon,
            Zaman = DateTimeOffset.Now
        };
        _veri.Haberler.Add(haber);
        KonsolKayitcisi.Bilgi(
            $"HABER | {haber.Baslik} | {haber.FinansalEtki:+#,##0.00;-#,##0.00;0.00} TL");
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
