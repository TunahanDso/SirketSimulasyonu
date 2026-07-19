using System.Reflection;
using System.Text.Json;
using SirketMotoru.Isletim;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Tick;

/// <summary>
/// Sunucusu kapalı olan şirketin hizmet ve uygulama tanımlarına dokunmadan
/// bilanço, puan, kredi, yatırım ve ürün piyasa durumunu tick boyunca dondurur.
/// Global tick ve bağlı şirketler normal biçimde çalışmaya devam eder.
/// </summary>
public sealed class BaglantiBazliSirketDonmaYoneticisi
{
    private sealed record SirketAnlik(
        decimal Kasa,
        decimal ToplamGelir,
        decimal ToplamIade,
        decimal ToplamCeza,
        decimal BekleyenOdeme,
        decimal ToplamGuvenlikKaybi,
        double ItibarPuani,
        double GuvenilirlikPuani,
        double KodKalitesiPuani,
        double PerformansPuani,
        double GuvenlikPuani,
        double MusteriMemnuniyeti,
        int AktifIsSayisi,
        int TamamlananIsSayisi,
        int BasarisizIsSayisi,
        int ZamanAsimiSayisi,
        int IptalSayisi,
        int ReddedilenSayisi,
        int EngellenenSaldiri,
        int BasariliSaldiri,
        long ToplamIslemSuresi,
        DateTimeOffset? SonBasariliIs,
        DateTimeOffset? SonBasarisizIs);

    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly SirketYoneticisi _sirketler;
    private readonly object _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _kilitAlani;
    private readonly MethodInfo _kaydetMetodu;
    private readonly Dictionary<string, SirketAnlik> _sirketAnliklari = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SirketIsletimDurumu> _isletimAnliklari = new(StringComparer.OrdinalIgnoreCase);

    public BaglantiBazliSirketDonmaYoneticisi(
        SirketYoneticisi sirketler,
        KodTabanliSirketIsletimYoneticisi isletim)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        ArgumentNullException.ThrowIfNull(isletim);

        FieldInfo temelAlani = typeof(KodTabanliSirketIsletimYoneticisi)
            .GetField("_temel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Bağlantı bazlı donma köprüsü kurulamadı.");
        _temel = temelAlani.GetValue(isletim)
            ?? throw new InvalidOperationException("Temel işletim yöneticisi boş.");
        Type tur = _temel.GetType();
        _veriAlani = tur.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("İşletim veri alanı bulunamadı.");
        _kilitAlani = tur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("İşletim kilidi bulunamadı.");
        _kaydetMetodu = tur.GetMethod("TumunuKaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("İşletim kayıt metodu bulunamadı.");
    }

    public async Task TickOncesiAsync(CancellationToken cancellationToken)
    {
        _sirketAnliklari.Clear();
        _isletimAnliklari.Clear();

        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari.Where(x => !x.BagliMi))
            _sirketAnliklari[sirket.SirketKimligi] = Al(sirket);

        if (_sirketAnliklari.Count == 0) return;

        SemaphoreSlim kilit = (SemaphoreSlim)(_kilitAlani.GetValue(_temel)
            ?? throw new InvalidOperationException("İşletim kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try
        {
            SirketIsletimDosyasi veri = (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel)
                ?? throw new InvalidOperationException("İşletim verisi boş."));
            foreach (SirketIsletimDurumu durum in veri.Sirketler
                         .Where(x => _sirketAnliklari.ContainsKey(x.SirketKimligi)))
                _isletimAnliklari[durum.SirketKimligi] = Kopyala(durum);
        }
        finally { kilit.Release(); }
    }

    public async Task GeriYukleAsync(CancellationToken cancellationToken)
    {
        if (_sirketAnliklari.Count == 0) return;

        SemaphoreSlim kilit = (SemaphoreSlim)(_kilitAlani.GetValue(_temel)
            ?? throw new InvalidOperationException("İşletim kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try
        {
            SirketIsletimDosyasi veri = (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel)
                ?? throw new InvalidOperationException("İşletim verisi boş."));

            foreach ((string kimlik, SirketIsletimDurumu anlik) in _isletimAnliklari)
            {
                int sira = veri.Sirketler.FindIndex(x =>
                    x.SirketKimligi.Equals(kimlik, StringComparison.OrdinalIgnoreCase));
                if (sira >= 0) veri.Sirketler[sira] = Kopyala(anlik);
                else veri.Sirketler.Add(Kopyala(anlik));
            }

            Task kayit = (Task)(_kaydetMetodu.Invoke(_temel, [cancellationToken])
                ?? throw new InvalidOperationException("İşletim kayıt görevi oluşturulamadı."));
            await kayit;
        }
        finally { kilit.Release(); }

        foreach ((string kimlik, SirketAnlik anlik) in _sirketAnliklari)
        {
            SirketKaydi? sirket = _sirketler.SirketKayitlari.FirstOrDefault(x =>
                x.SirketKimligi.Equals(kimlik, StringComparison.OrdinalIgnoreCase));
            if (sirket is not null) Uygula(sirket, anlik);
        }
    }

    private static SirketAnlik Al(SirketKaydi s) => new(
        s.Kasa, s.ToplamGelir, s.ToplamIade, s.ToplamCeza, s.BekleyenOdeme,
        s.ToplamGuvenlikKaybi, s.ItibarPuani, s.GuvenilirlikPuani,
        s.KodKalitesiPuani, s.PerformansPuani, s.GuvenlikPuani,
        s.OrtalamaMusteriMemnuniyeti, s.AktifIsSayisi, s.TamamlananIsSayisi,
        s.BasarisizIsSayisi, s.ZamanAsiminaUgrayanIsSayisi, s.IptalEdilenIsSayisi,
        s.ReddedilenIsSayisi, s.EngellenenSaldiriSayisi, s.BasariliSaldiriSayisi,
        s.ToplamIslemSuresiMs, s.SonBasariliIsZamani, s.SonBasarisizIsZamani);

    private static void Uygula(SirketKaydi s, SirketAnlik a)
    {
        s.Kasa = a.Kasa;
        s.ToplamGelir = a.ToplamGelir;
        s.ToplamIade = a.ToplamIade;
        s.ToplamCeza = a.ToplamCeza;
        s.BekleyenOdeme = a.BekleyenOdeme;
        s.ToplamGuvenlikKaybi = a.ToplamGuvenlikKaybi;
        s.ItibarPuani = a.ItibarPuani;
        s.GuvenilirlikPuani = a.GuvenilirlikPuani;
        s.KodKalitesiPuani = a.KodKalitesiPuani;
        s.PerformansPuani = a.PerformansPuani;
        s.GuvenlikPuani = a.GuvenlikPuani;
        s.OrtalamaMusteriMemnuniyeti = a.MusteriMemnuniyeti;
        s.AktifIsSayisi = a.AktifIsSayisi;
        s.TamamlananIsSayisi = a.TamamlananIsSayisi;
        s.BasarisizIsSayisi = a.BasarisizIsSayisi;
        s.ZamanAsiminaUgrayanIsSayisi = a.ZamanAsimiSayisi;
        s.IptalEdilenIsSayisi = a.IptalSayisi;
        s.ReddedilenIsSayisi = a.ReddedilenSayisi;
        s.EngellenenSaldiriSayisi = a.EngellenenSaldiri;
        s.BasariliSaldiriSayisi = a.BasariliSaldiri;
        s.ToplamIslemSuresiMs = a.ToplamIslemSuresi;
        s.SonBasariliIsZamani = a.SonBasariliIs;
        s.SonBasarisizIsZamani = a.SonBasarisizIs;
    }

    private static SirketIsletimDurumu Kopyala(SirketIsletimDurumu kaynak)
    {
        string json = JsonSerializer.Serialize(kaynak, JsonAyarlari);
        return JsonSerializer.Deserialize<SirketIsletimDurumu>(json, JsonAyarlari)
            ?? throw new InvalidOperationException("Şirket işletim anlığı kopyalanamadı.");
    }
}
