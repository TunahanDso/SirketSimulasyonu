using System.Reflection;
using System.Text;
using System.Text.Json;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class FinansV7TickKaydi
{
    public long TickNumarasi { get; set; }
    public decimal HizmetSlaVeOlayGeliri { get; set; }
    public decimal UrunGeliri { get; set; }
    public decimal AbonelikGeliriBilgi { get; set; }
    public decimal ProtokolGeliri { get; set; }
    public decimal ToplamGelir { get; set; }
    public decimal OperasyonGideri { get; set; }
    public decimal FinansmanGideri { get; set; }
    public decimal YatirimGideri { get; set; }
    public decimal IadeGideri { get; set; }
    public decimal CezaGideri { get; set; }
    public decimal OdenemeyenGiderArtisi { get; set; }
    public decimal ToplamGider { get; set; }
    public decimal TickNetKazanc { get; set; }
    public decimal KurtarmaKredisiGirisi { get; set; }
    public decimal NakitDegisimi { get; set; }
    public decimal KapanisKasasi { get; set; }
    public decimal ToplamBorc { get; set; }
    public int KrediNotu { get; set; }
    public DateTimeOffset Zaman { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FinansV7SirketKaydi
{
    public string SirketKimligi { get; set; } = string.Empty;
    public int KurtarmaKredisiSayisi { get; set; }
    public long SonKurtarmaKredisiTicki { get; set; } = -1000;
    public List<FinansV7TickKaydi> Tickler { get; set; } = [];
}

public sealed class FinansV7HaberKaydi
{
    public string HaberKimligi { get; set; } = string.Empty;
    public long TickNumarasi { get; set; }
    public string SirketKimligi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public string Baslik { get; set; } = string.Empty;
    public string Ozet { get; set; } = string.Empty;
    public string Seviye { get; set; } = "normal";
    public decimal FinansalEtki { get; set; }
    public DateTimeOffset Zaman { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FinansV7Dosyasi
{
    public int Surum { get; set; } = 7;
    public long SonTick { get; set; }
    public DateTimeOffset GuncellenmeZamani { get; set; } = DateTimeOffset.UtcNow;
    public List<FinansV7SirketKaydi> Sirketler { get; set; } = [];
    public List<FinansV7HaberKaydi> Haberler { get; set; } = [];
}

public sealed class FinansV7BankaOzeti
{
    public int KrediNotu { get; set; }
    public string KrediNotuSinifi { get; set; } = string.Empty;
    public decimal AktifBorc { get; set; }
    public decimal TahminiKrediLimiti { get; set; }
    public decimal KullanilabilirLimit { get; set; }
    public decimal SonrakiTaksit { get; set; }
    public long SonrakiOdemeTicki { get; set; }
    public int AktifKrediSayisi { get; set; }
    public int TemerrutSayisi { get; set; }
    public int KurtarmaKredisiSayisi { get; set; }
    public decimal PiyasaFaizCarpani { get; set; }
    public string RiskDurumu { get; set; } = string.Empty;
}

public sealed class SlaUygunlukKaydi
{
    public string TeklifKimligi { get; set; } = string.Empty;
    public string Baslik { get; set; } = string.Empty;
    public string Kategori { get; set; } = string.Empty;
    public List<string> GerekliHizmetler { get; set; } = [];
    public List<string> EksikHizmetler { get; set; } = [];
    public List<string> YetersizKosullar { get; set; } = [];
    public bool Uygun { get; set; }
}

public static class FinansV7Deposu
{
    private static readonly object Kilit = new();
    private static FinansV7Dosyasi _veri = new();

    public static void Guncelle(FinansV7Dosyasi veri)
    {
        lock (Kilit) _veri = Kopyala(veri);
    }

    public static FinansV7Dosyasi Getir()
    {
        lock (Kilit) return Kopyala(_veri);
    }

    public static FinansV7SirketKaydi SirketGetir(string sirketKimligi) =>
        Getir().Sirketler.FirstOrDefault(x => x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase))
        ?? new FinansV7SirketKaydi { SirketKimligi = sirketKimligi };

    private static FinansV7Dosyasi Kopyala(FinansV7Dosyasi veri)
    {
        string json = JsonSerializer.Serialize(veri);
        return JsonSerializer.Deserialize<FinansV7Dosyasi>(json) ?? new();
    }
}

public sealed class FinansV7Yoneticisi : IAsyncDisposable
{
    private sealed record Anlik(
        decimal Kasa,
        decimal ToplamGelir,
        decimal ToplamIade,
        decimal ToplamCeza,
        decimal UrunGeliri,
        decimal AbonelikGeliri,
        decimal IsletmeGideri,
        decimal FinansmanGideri,
        decimal YatirimGideri,
        decimal OdenemeyenGider,
        decimal ProtokolGeliri);

    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, string[]> SlaHizmetleri =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["api"] = ["kimlik.oturum-dogrula", "gelistirme.api-dokumani"],
            ["analitik"] = ["veri.ortalama-hesapla", "analitik.rapor-olustur"],
            ["eposta"] = ["eposta.gonder", "eposta.gelen-kutusu"],
            ["sosyal-medya"] = ["sosyal.gonderi-olustur", "sosyal.akisi-getir"],
            ["guvenlik"] = ["guvenlik.istek-dogrula", "guvenlik.saldiri-tespit"],
            ["platform"] = ["kimlik.oturum-dogrula", "dosya.yukle"],
            ["isletim-sistemi"] = ["isletim.surec-baslat", "isletim.kaynak-ata", "isletim.ag-yapilandir"],
            ["altyapi"] = ["veritabani.baglanti-havuzu", "bildirim.hata-raporu"]
        };

    private readonly SirketYoneticisi _sirketler;
    private readonly SirketIsletimYoneticisi _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _temelKilitAlani;
    private readonly MethodInfo _kaydetMetodu;
    private readonly string _dosyaYolu;
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private readonly Dictionary<string, Anlik> _baslangic = new(StringComparer.OrdinalIgnoreCase);
    private FinansV7Dosyasi _dosya = new();
    private bool _baslatildi;

    public FinansV7Yoneticisi(
        SirketYoneticisi sirketler,
        KodTabanliSirketIsletimYoneticisi isletim,
        string motorVerileriKlasoru)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        ArgumentNullException.ThrowIfNull(isletim);
        _dosyaYolu = Path.Combine(Path.GetFullPath(motorVerileriKlasoru), "finans-v7.json");

        FieldInfo temelAlani = typeof(KodTabanliSirketIsletimYoneticisi)
            .GetField("_temel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Finans V7 işletim köprüsü kurulamadı.");
        _temel = (SirketIsletimYoneticisi)(temelAlani.GetValue(isletim)
            ?? throw new InvalidOperationException("Finans V7 temel işletim yöneticisi boş."));
        Type tur = _temel.GetType();
        _veriAlani = tur.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Finans V7 işletim veri alanı bulunamadı.");
        _temelKilitAlani = tur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Finans V7 işletim kilidi bulunamadı.");
        _kaydetMetodu = tur.GetMethod("TumunuKaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Finans V7 kayıt metodu bulunamadı.");
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            if (_baslatildi) return;
            if (File.Exists(_dosyaYolu))
            {
                string json = await File.ReadAllTextAsync(_dosyaYolu, cancellationToken);
                _dosya = JsonSerializer.Deserialize<FinansV7Dosyasi>(json, JsonAyarlari) ?? new();
            }
            _dosya.Sirketler ??= [];
            _dosya.Haberler ??= [];
            foreach (SirketKaydi s in _sirketler.SirketKayitlari) _ = SirketDurumu(s.SirketKimligi);
            FinansV7Deposu.Guncelle(_dosya);
            await KaydetAsync(cancellationToken);
            _baslatildi = true;
            KonsolKayitcisi.Basari("Finans V7 hazır | Tick bazlı gelir-gider defteri, banka notu ve otomatik kurtarma kredisi aktif.");
        }
        finally { _kilit.Release(); }
    }

    public async Task TickOncesiAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            _baslangic.Clear();
            await TemelKilitAsync(async veri =>
            {
                foreach (SirketKaydi s in _sirketler.SirketKayitlari)
                    _baslangic[s.SirketKimligi] = AnlikGoruntu(s, Durum(veri, s.SirketKimligi), veri);
                await Task.CompletedTask;
            }, cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    public async Task TickSonuAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            await TemelKilitAsync(async veri =>
            {
                foreach (SirketKaydi s in _sirketler.SirketKayitlari)
                {
                    SirketIsletimDurumu d = Durum(veri, s.SirketKimligi);
                    Anlik once = _baslangic.TryGetValue(s.SirketKimligi, out Anlik? x)
                        ? x
                        : AnlikGoruntu(s, d, veri);
                    Anlik sonra = AnlikGoruntu(s, d, veri);

                    decimal toplamGelir = Fark(sonra.ToplamGelir, once.ToplamGelir);
                    decimal urunGeliri = Fark(sonra.UrunGeliri, once.UrunGeliri);
                    decimal protokolGeliri = Fark(sonra.ProtokolGeliri, once.ProtokolGeliri);
                    decimal operasyon = Fark(sonra.IsletmeGideri, once.IsletmeGideri);
                    decimal finansman = Fark(sonra.FinansmanGideri, once.FinansmanGideri);
                    decimal yatirim = Fark(sonra.YatirimGideri, once.YatirimGideri);
                    decimal iade = Fark(sonra.ToplamIade, once.ToplamIade);
                    decimal ceza = Fark(sonra.ToplamCeza, once.ToplamCeza);
                    decimal odenemeyen = Fark(sonra.OdenemeyenGider, once.OdenemeyenGider);
                    decimal toplamGider = operasyon + finansman + yatirim + iade + ceza;
                    decimal tickNet = toplamGelir - toplamGider;

                    FinansV7SirketKaydi finans = SirketDurumu(s.SirketKimligi);
                    decimal kurtarma = 0;
                    if ((s.Kasa <= 0 || odenemeyen > 0) && tickNumarasi - finans.SonKurtarmaKredisiTicki >= 3)
                    {
                        kurtarma = KurtarmaKredisiUygula(s, d, finans, tickNumarasi, Math.Max(odenemeyen, toplamGider - toplamGelir));
                    }

                    FinansV7TickKaydi kayit = new()
                    {
                        TickNumarasi = tickNumarasi,
                        HizmetSlaVeOlayGeliri = toplamGelir - urunGeliri - protokolGeliri,
                        UrunGeliri = urunGeliri,
                        AbonelikGeliriBilgi = Fark(sonra.AbonelikGeliri, once.AbonelikGeliri),
                        ProtokolGeliri = protokolGeliri,
                        ToplamGelir = toplamGelir,
                        OperasyonGideri = operasyon,
                        FinansmanGideri = finansman,
                        YatirimGideri = yatirim,
                        IadeGideri = iade,
                        CezaGideri = ceza,
                        OdenemeyenGiderArtisi = odenemeyen,
                        ToplamGider = toplamGider,
                        TickNetKazanc = tickNet,
                        KurtarmaKredisiGirisi = kurtarma,
                        NakitDegisimi = s.Kasa - once.Kasa,
                        KapanisKasasi = s.Kasa,
                        ToplamBorc = d.Krediler.Where(k => k.Aktif).Sum(k => k.KalanBorc),
                        KrediNotu = d.KrediNotu
                    };
                    finans.Tickler.Add(kayit);
                    if (finans.Tickler.Count > 360) finans.Tickler.RemoveRange(0, finans.Tickler.Count - 360);

                    if (Math.Abs(tickNet) >= 5_000)
                        HaberEkle(tickNumarasi, s, tickNet >= 0 ? "Güçlü tick kârı" : "Tick zararı alarmı",
                            $"{s.SirketAdi}, bu tickte {toplamGelir:N2} TL gelir ve {toplamGider:N2} TL gider yazarak net {tickNet:N2} TL sonuç açıkladı.",
                            tickNet >= 0 ? "iyi" : "kritik", tickNet);
                }
                await TemelKaydetAsync(cancellationToken);
            }, cancellationToken);

            _dosya.SonTick = tickNumarasi;
            _dosya.GuncellenmeZamani = DateTimeOffset.UtcNow;
            FinansV7Deposu.Guncelle(_dosya);
            await KaydetAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    public FinansV7BankaOzeti BankaOzetiGetir(string sirketKimligi)
    {
        SirketIsletimDosyasi veri = Veri();
        SirketIsletimDurumu d = Durum(veri, sirketKimligi);
        SirketKaydi s = _sirketler.SirketKayitlari.First(x => x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
        decimal borc = d.Krediler.Where(k => k.Aktif).Sum(k => k.KalanBorc);
        decimal limit = Math.Max(20_000, d.SirketDegeri * 0.55m + Math.Max(0, s.Kasa) * 0.5m);
        KrediKaydi? sonraki = d.Krediler.Where(k => k.Aktif).OrderBy(k => k.SonrakiOdemeTicki).FirstOrDefault();
        int not = d.KrediNotu;
        FinansV7SirketKaydi f = SirketDurumu(sirketKimligi);
        return new FinansV7BankaOzeti
        {
            KrediNotu = not,
            KrediNotuSinifi = not switch { >= 820 => "A+", >= 760 => "A", >= 690 => "B+", >= 620 => "B", >= 540 => "C", >= 450 => "D", _ => "E" },
            AktifBorc = borc,
            TahminiKrediLimiti = decimal.Round(limit, 2),
            KullanilabilirLimit = decimal.Round(Math.Max(0, limit - borc), 2),
            SonrakiTaksit = sonraki?.TaksitTutari ?? 0,
            SonrakiOdemeTicki = sonraki?.SonrakiOdemeTicki ?? 0,
            AktifKrediSayisi = d.Krediler.Count(k => k.Aktif),
            TemerrutSayisi = d.TemerrutSayisi,
            KurtarmaKredisiSayisi = f.KurtarmaKredisiSayisi,
            PiyasaFaizCarpani = decimal.Round(not switch { >= 800 => 0.78m, >= 700 => 0.95m, >= 600 => 1.15m, >= 500 => 1.42m, _ => 1.85m }, 2),
            RiskDurumu = not switch { >= 760 => "çok düşük", >= 650 => "düşük", >= 550 => "orta", >= 450 => "yüksek", _ => "kritik" }
        };
    }

    public IReadOnlyList<SlaUygunlukKaydi> SlaUygunluklariniGetir(string sirketKimligi)
    {
        SirketIsletimDosyasi veri = Veri();
        SirketKaydi s = _sirketler.SirketKayitlari.First(x => x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
        HashSet<string> aktif = s.Hizmetler.Where(h => h.Aktif).Select(h => h.HizmetKimligi).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int kapasite = s.Hizmetler.Where(h => h.Aktif).Sum(h => h.AzamiEszamanliIs);
        return veri.SozlesmeTeklifleri
            .Where(t => t.Aktif && string.IsNullOrWhiteSpace(t.KabulEdenSirketKimligi))
            .Select(t =>
            {
                List<string> gerekli = SlaHizmetleri.TryGetValue(t.Kategori, out string[]? h) ? h.ToList() : [];
                List<string> eksik = gerekli.Where(x => !aktif.Contains(x)).ToList();
                List<string> kosul = [];
                if (s.KodKalitesiPuani < t.AsgariKalite) kosul.Add($"Kalite {s.KodKalitesiPuani:N1}/{t.AsgariKalite:N1}");
                if (s.PerformansPuani < t.AsgariPerformans) kosul.Add($"Performans {s.PerformansPuani:N1}/{t.AsgariPerformans:N1}");
                if (s.GuvenlikPuani < t.AsgariGuvenlik) kosul.Add($"Güvenlik {s.GuvenlikPuani:N1}/{t.AsgariGuvenlik:N1}");
                if (kapasite < t.GerekliKapasite) kosul.Add($"Kapasite {kapasite}/{t.GerekliKapasite}");
                return new SlaUygunlukKaydi
                {
                    TeklifKimligi = t.TeklifKimligi,
                    Baslik = t.Baslik,
                    Kategori = t.Kategori,
                    GerekliHizmetler = gerekli,
                    EksikHizmetler = eksik,
                    YetersizKosullar = kosul,
                    Uygun = eksik.Count == 0 && kosul.Count == 0
                };
            }).ToList();
    }

    public bool SlaKabulEdilebilirMi(string sirketKimligi, string teklifKimligi, out string aciklama)
    {
        SlaUygunlukKaydi? kayit = SlaUygunluklariniGetir(sirketKimligi)
            .FirstOrDefault(x => x.TeklifKimligi.Equals(teklifKimligi, StringComparison.OrdinalIgnoreCase));
        if (kayit is null) { aciklama = "SLA teklifi bulunamadı."; return false; }
        if (kayit.Uygun) { aciklama = "Şirket SLA şartlarına uygun."; return true; }
        aciklama = $"SLA uygun değil. Eksik hizmetler: {string.Join(", ", kayit.EksikHizmetler.DefaultIfEmpty("yok"))}. Yetersiz koşullar: {string.Join(", ", kayit.YetersizKosullar.DefaultIfEmpty("yok"))}.";
        return false;
    }

    private decimal KurtarmaKredisiUygula(SirketKaydi s, SirketIsletimDurumu d, FinansV7SirketKaydi f, long tick, decimal acik)
    {
        decimal anaPara = Math.Clamp(decimal.Ceiling(Math.Max(25_000m, acik * 1.6m + 15_000m) / 1_000m) * 1_000m, 25_000m, 500_000m);
        decimal faiz = Math.Min(0.075m, 0.035m + f.KurtarmaKredisiSayisi * 0.006m);
        const int taksit = 12;
        decimal toplam = decimal.Round(anaPara * (1 + faiz * taksit), 2);
        d.Krediler.Add(new KrediKaydi
        {
            KrediKimligi = $"kurtarma-{Guid.NewGuid():N}",
            KrediTuru = "otomatik-kurtarma",
            AnaPara = anaPara,
            KalanBorc = toplam,
            TickFaizOrani = faiz,
            TaksitTutari = decimal.Round(toplam / taksit, 2),
            KalanTaksit = taksit,
            SonrakiOdemeTicki = tick + 2,
            OdemeAraligiTick = 2
        });
        s.Kasa += anaPara;
        d.KrediNotu = Math.Clamp(d.KrediNotu - 90, 300, 900);
        d.TemerrutSayisi++;
        d.SonIslemler.Add(new IsletimIslemKaydi
        {
            IslemKimligi = $"kurtarma-{Guid.NewGuid():N}",
            TickNumarasi = tick,
            IslemTuru = "otomatik-kurtarma-kredisi",
            Aciklama = $"Likidite tükendiği için %{faiz * 100:N2} tick faizli zorunlu kurtarma kredisi verildi.",
            Tutar = anaPara
        });
        f.KurtarmaKredisiSayisi++;
        f.SonKurtarmaKredisiTicki = tick;
        HaberEkle(tick, s, "Merkez Bankası kurtarma hattı devrede",
            $"{s.SirketAdi} kasası tükendi. Motor {anaPara:N2} TL zorunlu kurtarma kredisi açtı; kredi notu düştü ve yüksek faizli geri ödeme başladı.",
            "kritik", anaPara);
        KonsolKayitcisi.Uyari($"OTOMATİK KURTARMA KREDİSİ | {s.SirketAdi} | {anaPara:N2} TL | Faiz %{faiz * 100:N2}");
        return anaPara;
    }

    private void HaberEkle(long tick, SirketKaydi s, string baslik, string ozet, string seviye, decimal etki)
    {
        string kimlik = $"{tick}|{s.SirketKimligi}|{baslik}";
        if (_dosya.Haberler.Any(x => x.HaberKimligi == kimlik)) return;
        _dosya.Haberler.Add(new FinansV7HaberKaydi
        {
            HaberKimligi = kimlik,
            TickNumarasi = tick,
            SirketKimligi = s.SirketKimligi,
            SirketAdi = s.SirketAdi,
            Baslik = baslik,
            Ozet = ozet,
            Seviye = seviye,
            FinansalEtki = etki
        });
        if (_dosya.Haberler.Count > 300) _dosya.Haberler.RemoveRange(0, _dosya.Haberler.Count - 300);
    }

    private Anlik AnlikGoruntu(SirketKaydi s, SirketIsletimDurumu d, SirketIsletimDosyasi veri) => new(
        s.Kasa,
        s.ToplamGelir,
        s.ToplamIade,
        s.ToplamCeza,
        d.ToplamUrunGeliri,
        d.ToplamAbonelikGeliri,
        d.ToplamIsletmeGideri,
        d.ToplamFinansmanGideri,
        d.ToplamYatirimHarcamasi,
        d.OdenemeyenGider,
        veri.Protokoller.Where(p => p.SahipSirketKimligi.Equals(s.SirketKimligi, StringComparison.OrdinalIgnoreCase)).Sum(p => p.ToplamLisansGeliri));

    private static decimal Fark(decimal son, decimal ilk) => decimal.Round(son - ilk, 2);

    private FinansV7SirketKaydi SirketDurumu(string kimlik)
    {
        FinansV7SirketKaydi? d = _dosya.Sirketler.FirstOrDefault(x => x.SirketKimligi.Equals(kimlik, StringComparison.OrdinalIgnoreCase));
        if (d is not null) return d;
        d = new FinansV7SirketKaydi { SirketKimligi = kimlik };
        _dosya.Sirketler.Add(d);
        return d;
    }

    private SirketIsletimDosyasi Veri() => (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel) ?? new SirketIsletimDosyasi());

    private static SirketIsletimDurumu Durum(SirketIsletimDosyasi veri, string kimlik)
    {
        SirketIsletimDurumu? d = veri.Sirketler.FirstOrDefault(x => x.SirketKimligi.Equals(kimlik, StringComparison.OrdinalIgnoreCase));
        if (d is not null) return d;
        d = new SirketIsletimDurumu { SirketKimligi = kimlik };
        veri.Sirketler.Add(d);
        return d;
    }

    private async Task TemelKilitAsync(Func<SirketIsletimDosyasi, Task> islem, CancellationToken cancellationToken)
    {
        SemaphoreSlim temelKilit = (SemaphoreSlim)(_temelKilitAlani.GetValue(_temel)
            ?? throw new InvalidOperationException("Temel işletim kilidi boş."));
        await temelKilit.WaitAsync(cancellationToken);
        try { await islem(Veri()); }
        finally { temelKilit.Release(); }
    }

    private async Task TemelKaydetAsync(CancellationToken cancellationToken)
    {
        object? sonuc = _kaydetMetodu.Invoke(_temel, [cancellationToken]);
        if (sonuc is Task task) await task;
    }

    private async Task KaydetAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        string tmp = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(_dosya, JsonAyarlari), new UTF8Encoding(false), cancellationToken);
        File.Move(tmp, _dosyaYolu, true);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_baslatildi) return;
        await _kilit.WaitAsync();
        try { await KaydetAsync(CancellationToken.None); }
        finally { _kilit.Release(); _kilit.Dispose(); }
    }
}
