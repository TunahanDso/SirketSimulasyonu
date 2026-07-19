using System.Reflection;
using System.Text.Json;
using SirketMotoru.Isletim;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Tick;

/// <summary>
/// Sunucusu kapalı olan şirketin hizmet ve uygulama tanımlarına dokunmadan
/// bilanço, puan, kredi, yatırım, ürün ve finans defterini tick boyunca dondurur.
/// Kapalı şirketin uygulamaları o tickte piyasaya katılmaz; global tick ve bağlı
/// şirketler normal biçimde çalışmaya devam eder.
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

    private readonly FinansV7Yoneticisi _finans;
    private readonly FieldInfo _finansDosyaAlani;
    private readonly FieldInfo _finansKilitAlani;
    private readonly MethodInfo _finansKaydetMetodu;

    private readonly Dictionary<string, SirketAnlik> _sirketAnliklari = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SirketIsletimDurumu> _isletimAnliklari = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FinansV7SirketKaydi?> _finansAnliklari = new(StringComparer.OrdinalIgnoreCase);

    public BaglantiBazliSirketDonmaYoneticisi(
        SirketYoneticisi sirketler,
        KodTabanliSirketIsletimYoneticisi isletim,
        FinansV7Yoneticisi finans)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        ArgumentNullException.ThrowIfNull(isletim);
        _finans = finans ?? throw new ArgumentNullException(nameof(finans));

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

        Type finansTur = typeof(FinansV7Yoneticisi);
        _finansDosyaAlani = finansTur.GetField("_dosya", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Finans dosya alanı bulunamadı.");
        _finansKilitAlani = finansTur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Finans kilidi bulunamadı.");
        _finansKaydetMetodu = finansTur.GetMethod("KaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Finans kayıt metodu bulunamadı.");
    }

    public async Task TickOncesiAsync(CancellationToken cancellationToken)
    {
        _sirketAnliklari.Clear();
        _isletimAnliklari.Clear();
        _finansAnliklari.Clear();

        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari.Where(x => !x.BagliMi))
            _sirketAnliklari[sirket.SirketKimligi] = Al(sirket);

        if (_sirketAnliklari.Count == 0) return;
        await IsletimAnliklariniAlVePazardanCikarAsync(cancellationToken);
        await FinansAnliklariniAlAsync(cancellationToken);
    }

    public async Task GeriYukleAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        if (_sirketAnliklari.Count == 0) return;

        // Bilanço kaydı yazılmadan önce şirketin kamuya açık finans alanları geri konur.
        foreach ((string kimlik, SirketAnlik anlik) in _sirketAnliklari)
        {
            SirketKaydi? sirket = _sirketler.SirketKayitlari.FirstOrDefault(x =>
                x.SirketKimligi.Equals(kimlik, StringComparison.OrdinalIgnoreCase));
            if (sirket is not null) Uygula(sirket, anlik);
        }

        await IsletimiGeriYukleAsync(cancellationToken);
        await FinansiGeriYukleAsync(tickNumarasi, cancellationToken);
    }

    private async Task IsletimAnliklariniAlVePazardanCikarAsync(CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = (SemaphoreSlim)(_kilitAlani.GetValue(_temel)
            ?? throw new InvalidOperationException("İşletim kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try
        {
            SirketIsletimDosyasi veri = (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel)
                ?? throw new InvalidOperationException("İşletim verisi boş."));
            foreach (SirketIsletimDurumu durum in veri.Sirketler
                         .Where(x => _sirketAnliklari.ContainsKey(x.SirketKimligi)))
            {
                _isletimAnliklari[durum.SirketKimligi] = Kopyala(durum);
                foreach (UrunKaydi urun in durum.Urunler)
                    urun.Aktif = false;
                durum.ToplamAboneSayisi = 0;
            }
        }
        finally { kilit.Release(); }
    }

    private async Task FinansAnliklariniAlAsync(CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = (SemaphoreSlim)(_finansKilitAlani.GetValue(_finans)
            ?? throw new InvalidOperationException("Finans kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try
        {
            FinansV7Dosyasi dosya = (FinansV7Dosyasi)(_finansDosyaAlani.GetValue(_finans)
                ?? throw new InvalidOperationException("Finans dosyası boş."));
            foreach (string kimlik in _sirketAnliklari.Keys)
            {
                FinansV7SirketKaydi? kayit = dosya.Sirketler.FirstOrDefault(x =>
                    x.SirketKimligi.Equals(kimlik, StringComparison.OrdinalIgnoreCase));
                _finansAnliklari[kimlik] = kayit is null ? null : Kopyala(kayit);
            }
        }
        finally { kilit.Release(); }
    }

    private async Task IsletimiGeriYukleAsync(CancellationToken cancellationToken)
    {
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
    }

    private async Task FinansiGeriYukleAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = (SemaphoreSlim)(_finansKilitAlani.GetValue(_finans)
            ?? throw new InvalidOperationException("Finans kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try
        {
            FinansV7Dosyasi dosya = (FinansV7Dosyasi)(_finansDosyaAlani.GetValue(_finans)
                ?? throw new InvalidOperationException("Finans dosyası boş."));

            foreach ((string kimlik, FinansV7SirketKaydi? anlik) in _finansAnliklari)
            {
                dosya.Sirketler.RemoveAll(x =>
                    x.SirketKimligi.Equals(kimlik, StringComparison.OrdinalIgnoreCase));
                if (anlik is not null) dosya.Sirketler.Add(Kopyala(anlik));
            }
            dosya.Haberler.RemoveAll(x =>
                x.TickNumarasi == tickNumarasi && _sirketAnliklari.ContainsKey(x.SirketKimligi));
            FinansV7Deposu.Guncelle(dosya);

            Task kayit = (Task)(_finansKaydetMetodu.Invoke(_finans, [cancellationToken])
                ?? throw new InvalidOperationException("Finans kayıt görevi oluşturulamadı."));
            await kayit;
        }
        finally { kilit.Release(); }
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

    private static T Kopyala<T>(T kaynak)
    {
        string json = JsonSerializer.Serialize(kaynak, JsonAyarlari);
        return JsonSerializer.Deserialize<T>(json, JsonAyarlari)
            ?? throw new InvalidOperationException($"{typeof(T).Name} anlığı kopyalanamadı.");
    }
}
