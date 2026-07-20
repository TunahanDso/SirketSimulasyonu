using System.Reflection;
using System.Text;
using System.Text.Json;
using SirketMotoru.Hizmetler;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

/// <summary>
/// V9'un tek ekonomi otoritesidir. Eski işletim yöneticisi yalnız kalıcı CRUD
/// deposu olarak kullanılır; gider, ürün geliri, kredi, kapasite ve pazar
/// hesapları yalnız bu sınıfta yapılır.
/// </summary>
public sealed class V9EkonomiYoneticisi : IAsyncDisposable
{
    private sealed record TickBaslangici(
        decimal Kasa,
        decimal ToplamGelir,
        decimal ToplamCeza,
        decimal ToplamIade,
        decimal ToplamUrunGeliri,
        decimal ToplamAbonelikGeliri,
        decimal ToplamIsletmeGideri,
        decimal ToplamFinansmanGideri);

    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, (string Ad, decimal Taban, int Kapasite)> Yatirimlar =
        new Dictionary<string, (string, decimal, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["cpu"] = ("İşlemci Kümesi", 4_000m, 420),
            ["ram"] = ("Bellek Havuzu", 3_500m, 360),
            ["ag"] = ("Ağ Omurgası", 4_000m, 390),
            ["depolama"] = ("Depolama Kümesi", 3_000m, 300),
            ["guvenlik"] = ("Güvenlik Operasyonu", 5_000m, 160),
            ["yedek"] = ("Yedek Sunucu", 6_500m, 260),
            ["destek"] = ("Müşteri Destek Ekibi", 4_000m, 100),
            ["pazarlama"] = ("Pazarlama Departmanı", 5_000m, 0),
            ["satis"] = ("Kurumsal Satış", 5_500m, 0),
            ["arge"] = ("Ar-Ge Laboratuvarı", 6_000m, 120)
        };

    private readonly SirketYoneticisi _sirketler;
    private readonly MusteriYoneticisi _musteriler;
    private readonly HizmetKatalogu _katalog;
    private readonly KodTabanliSirketIsletimYoneticisi _isletim;
    private readonly object _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _kilitAlani;
    private readonly MethodInfo _kaydetMetodu;
    private readonly string _dosyaYolu;
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private readonly Random _rastgele = new(20260724);
    private readonly Dictionary<string, TickBaslangici> _baslangiclar = new(StringComparer.OrdinalIgnoreCase);
    private V9PazarDosyasi _pazar = new();
    private long _tick;
    private bool _baslatildi;

    public V9EkonomiYoneticisi(
        SirketYoneticisi sirketler,
        MusteriYoneticisi musteriler,
        HizmetKatalogu katalog,
        KodTabanliSirketIsletimYoneticisi isletim,
        string motorVerileriKlasoru)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _musteriler = musteriler ?? throw new ArgumentNullException(nameof(musteriler));
        _katalog = katalog ?? throw new ArgumentNullException(nameof(katalog));
        _isletim = isletim ?? throw new ArgumentNullException(nameof(isletim));
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);
        _dosyaYolu = Path.Combine(Path.GetFullPath(motorVerileriKlasoru), "pazar-v9.json");

        FieldInfo temelAlani = typeof(KodTabanliSirketIsletimYoneticisi)
            .GetField("_temel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9 işletim köprüsü kurulamadı.");
        _temel = temelAlani.GetValue(isletim)
            ?? throw new InvalidOperationException("V9 temel işletim yöneticisi boş.");
        Type tur = _temel.GetType();
        _veriAlani = tur.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9 işletim veri alanı bulunamadı.");
        _kilitAlani = tur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9 işletim kilidi bulunamadı.");
        _kaydetMetodu = tur.GetMethod("TumunuKaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9 işletim kayıt metodu bulunamadı.");
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
                _pazar = JsonSerializer.Deserialize<V9PazarDosyasi>(json, JsonAyarlari) ?? new();
            }
            NormalizeEt();
            await TemelKilitAsync(async veri =>
            {
                foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
                {
                    SirketIsletimDurumu durum = Durum(veri, sirket.SirketKimligi);
                    EskiKapasiteSatinalimlariniTemizle(durum);
                    FiyatlariSinirla(durum);
                    V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirket.SirketKimligi);
                    kapasite.ToplamFizikselKapasite = ToplamFizikselKapasite(durum);
                }
                await TemelKaydetAsync(cancellationToken);
            }, cancellationToken);
            V9PazarDeposu.Guncelle(_pazar);
            await KaydetAsync(cancellationToken);
            _baslatildi = true;
            KonsolKayitcisi.Basari(
                "V9 ekonomi hazır | Tek gider defteri, tek ürün geliri, fiziksel kapasite tahsisi ve trendli pazar aktif.");
        }
        finally { _kilit.Release(); }
    }

    public async Task TickOncesiAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            _tick = tickNumarasi;
            _pazar.SonTick = tickNumarasi;
            _baslangiclar.Clear();
            V9HizmetSonucDeposu.TickBaslat(tickNumarasi);

            HizmetTalebiniGuncelle(tickNumarasi);
            await TemelKilitAsync(async veri =>
            {
                foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
                {
                    SirketIsletimDurumu durum = Durum(veri, sirket.SirketKimligi);
                    EskiKapasiteSatinalimlariniTemizle(durum);
                    FiyatlariSinirla(durum);
                    V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirket.SirketKimligi);
                    kapasite.ToplamFizikselKapasite = ToplamFizikselKapasite(durum);
                    TahsisleriUygula(sirket, durum, kapasite);
                    _baslangiclar[sirket.SirketKimligi] = Anlik(sirket, durum);
                }
                await TemelKaydetAsync(cancellationToken);
            }, cancellationToken);

            V9PazarDeposu.Guncelle(_pazar);
            await KaydetAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    public async Task UrunPazariniVeEkonomiyiIsleAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            _tick = tickNumarasi;
            await TemelKilitAsync(async veri =>
            {
                Dictionary<string, (SirketKaydi Sirket, SirketIsletimDurumu Durum, UrunKaydi Urun)> urunler =
                    TumAktifUrunler(veri);
                IsletimSistemleriniDagit(urunler, tickNumarasi);
                UygulamalariDagit(urunler, tickNumarasi);
                UygulamaTalebiniGuncelle(urunler, tickNumarasi);

                foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
                {
                    SirketIsletimDurumu durum = Durum(veri, sirket.SirketKimligi);
                    decimal urunGeliri = UrunGelirleriniIsle(sirket, durum);
                    decimal protokolGeliri = ProtokolLisanslariniIsle(veri, sirket, durum);
                    decimal isletmeGideri = IsletmeGideriniIsle(sirket, durum);
                    decimal finansmanGideri = KredileriIsle(sirket, durum);
                    Degerle(sirket, durum);

                    V9SirketPazarOzeti ozet = SirketOzeti(sirket.SirketKimligi, sirket.SirketAdi);
                    ozet.UygulamaGeliri = urunGeliri;
                    ozet.AbonelikGeliri = durum.Urunler
                        .Where(x => x.Aktif && x.FiyatlandirmaModeli is "abonelik" or "freemium")
                        .Sum(x => TickUrunGeliri(x, 0));
                    ozet.ProtokolGeliri = protokolGeliri;
                    ozet.IsletmeGideri = isletmeGideri;
                    ozet.FinansmanGideri = finansmanGideri;
                    ozet.AktifUygulamaKullanicisi = durum.Urunler
                        .Where(x => x.Aktif && x.UrunTuru != "isletim-sistemi")
                        .Sum(x => x.AktifKullaniciSayisi);
                    ozet.IsletimSistemiKullanicisi = durum.Urunler
                        .Where(x => x.Aktif && x.UrunTuru == "isletim-sistemi")
                        .Sum(x => x.AktifKullaniciSayisi);
                    V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirket.SirketKimligi);
                    ozet.ToplamKapasite = kapasite.ToplamFizikselKapasite;
                    ozet.KullanilanKapasite = kapasite.KullanilanKapasite;
                    ozet.AktifBorc = durum.Krediler.Where(x => x.Aktif).Sum(x => x.KalanBorc);
                }
                await TemelKaydetAsync(cancellationToken);
            }, cancellationToken);

            V9PazarDeposu.Guncelle(_pazar);
            await KaydetAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    public async Task TickSonuAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            HizmetArziniVeKarsilanmayiGuncelle();
            await TemelKilitAsync(async veri =>
            {
                foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
                {
                    SirketIsletimDurumu durum = Durum(veri, sirket.SirketKimligi);
                    V9SirketPazarOzeti ozet = SirketOzeti(sirket.SirketKimligi, sirket.SirketAdi);
                    TickBaslangici once = _baslangiclar.TryGetValue(sirket.SirketKimligi, out TickBaslangici? b)
                        ? b
                        : Anlik(sirket, durum);
                    ozet.HizmetGeliri = Math.Max(0,
                        (sirket.ToplamGelir - once.ToplamGelir) - ozet.UygulamaGeliri - ozet.ProtokolGeliri);
                    ozet.NetKazanc = decimal.Round(sirket.Kasa - once.Kasa, 2);
                    Degerle(sirket, durum);
                }
                await TemelKaydetAsync(cancellationToken);
            }, cancellationToken);

            _pazar.GuncellenmeZamani = DateTimeOffset.UtcNow;
            V9PazarDeposu.Guncelle(_pazar);
            await KaydetAsync(cancellationToken);
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> KapasiteTahsisEtAsync(
        string sirketKimligi,
        V9KapasiteTahsisIstegi istek,
        CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();
        ArgumentNullException.ThrowIfNull(istek);
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            IslemSonucu sonuc = IslemSonucu.Hata("Şirket bulunamadı.");
            await TemelKilitAsync(async veri =>
            {
                SirketKaydi? sirket = SirketBul(sirketKimligi);
                if (sirket is null) return;
                SirketIsletimDurumu durum = Durum(veri, sirketKimligi);
                V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirketKimligi);
                kapasite.ToplamFizikselKapasite = ToplamFizikselKapasite(durum);
                Dictionary<string, int> temiz = istek.Tahsisler
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value >= 0)
                    .ToDictionary(x => x.Key.Trim(), x => x.Value, StringComparer.OrdinalIgnoreCase);
                int toplam = temiz.Values.Sum();
                if (toplam > kapasite.ToplamFizikselKapasite)
                {
                    sonuc = IslemSonucu.Hata(
                        $"Tahsis toplamı {toplam:N0}; fiziksel kapasite {kapasite.ToplamFizikselKapasite:N0}. Önce altyapıyı yükseltin.");
                    return;
                }
                kapasite.Tahsisler = temiz;
                kapasite.KullaniciElleAyarladi = true;
                TahsisleriUygula(sirket, durum, kapasite);
                await TemelKaydetAsync(cancellationToken);
                sonuc = IslemSonucu.Basari(
                    $"Kapasite tahsisi kaydedildi. Ayrılan: {kapasite.AyrilmisKapasite:N0}; boş: {kapasite.BosKapasite:N0}.",
                    kapasite);
            }, cancellationToken);
            if (sonuc.Basarili)
            {
                V9PazarDeposu.Guncelle(_pazar);
                await KaydetAsync(cancellationToken);
            }
            return sonuc;
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> YatirimSatinAlAsync(
        string sirketKimligi,
        YatirimIstegi istek,
        CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            IslemSonucu sonuc = IslemSonucu.Hata("Şirket bulunamadı.");
            await TemelKilitAsync(async veri =>
            {
                SirketKaydi? sirket = SirketBul(sirketKimligi);
                if (sirket is null) return;
                if (!Yatirimlar.TryGetValue(istek.YatirimTuru?.Trim() ?? string.Empty, out var paket))
                {
                    sonuc = IslemSonucu.Hata("Bilinmeyen yatırım türü.");
                    return;
                }
                SirketIsletimDurumu durum = Durum(veri, sirketKimligi);
                int seviye = Seviye(durum, istek.YatirimTuru);
                decimal maliyet = YatirimMaliyeti(istek.YatirimTuru, seviye);
                if (sirket.Kasa < maliyet)
                {
                    sonuc = IslemSonucu.Hata($"Yetersiz kasa. Gerekli: {maliyet:N2} TL.");
                    return;
                }
                sirket.Kasa -= maliyet;
                durum.ToplamYatirimHarcamasi += maliyet;
                durum.YatirimSeviyeleri[istek.YatirimTuru] = seviye + 1;
                durum.TeknikBorc = Math.Max(0, durum.TeknikBorc - (istek.YatirimTuru == "arge" ? 1.5 : 0.2));
                V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirketKimligi);
                kapasite.ToplamFizikselKapasite = ToplamFizikselKapasite(durum);
                TahsisleriUygula(sirket, durum, kapasite);
                IslemEkle(durum, "v9-yatirim", $"{paket.Ad} seviye {seviye + 1} oldu.", -maliyet);
                await TemelKaydetAsync(cancellationToken);
                sonuc = IslemSonucu.Basari(
                    $"{paket.Ad} yükseltildi. Toplam fiziksel kapasite {kapasite.ToplamFizikselKapasite:N0}.",
                    new { seviye = seviye + 1, maliyet, kapasite = kapasite.ToplamFizikselKapasite });
            }, cancellationToken);
            if (sonuc.Basarili) await KaydetAsync(cancellationToken);
            return sonuc;
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> KrediCekAsync(
        string sirketKimligi,
        KrediIstegi istek,
        CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            IslemSonucu sonuc = IslemSonucu.Hata("Şirket bulunamadı.");
            await TemelKilitAsync(async veri =>
            {
                SirketKaydi? sirket = SirketBul(sirketKimligi);
                if (sirket is null) return;
                SirketIsletimDurumu durum = Durum(veri, sirketKimligi);
                decimal tutar = Math.Clamp(istek.Tutar, 5_000m, 250_000m);
                decimal mevcut = durum.Krediler.Where(x => x.Aktif).Sum(x => x.KalanBorc);
                decimal limit = Math.Clamp(50_000m + Math.Max(0, sirket.Kasa) * 0.75m + Math.Max(0, durum.SirketDegeri) * 0.15m, 50_000m, 500_000m);
                if (mevcut + tutar > limit)
                {
                    sonuc = IslemSonucu.Hata($"Kredi limiti {limit:N2} TL; kullanılabilir {Math.Max(0, limit - mevcut):N2} TL.");
                    return;
                }
                const decimal faiz = 0.0025m;
                const int taksit = 20;
                decimal toplam = decimal.Round(tutar * (1 + faiz * taksit), 2);
                durum.Krediler.Add(new KrediKaydi
                {
                    KrediKimligi = $"v9-kredi-{Guid.NewGuid():N}",
                    KrediTuru = string.IsNullOrWhiteSpace(istek.KrediTuru) ? "isletme" : istek.KrediTuru.Trim(),
                    AnaPara = tutar,
                    KalanBorc = toplam,
                    TickFaizOrani = faiz,
                    TaksitTutari = decimal.Round(toplam / taksit, 2),
                    KalanTaksit = taksit,
                    SonrakiOdemeTicki = _tick + 5,
                    OdemeAraligiTick = 5
                });
                sirket.Kasa += tutar;
                IslemEkle(durum, "v9-kredi", $"{tutar:N2} TL düşük faizli kredi kullanıldı.", tutar);
                await TemelKaydetAsync(cancellationToken);
                sonuc = IslemSonucu.Basari("Kredi kasaya aktarıldı; otomatik kurtarma kredisi yoktur.");
            }, cancellationToken);
            return sonuc;
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> UrunFiyatiniGuncelleAsync(
        string sirketKimligi,
        UrunGuncelleIstegi istek,
        CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            IslemSonucu sonuc = IslemSonucu.Hata("Ürün bulunamadı.");
            await TemelKilitAsync(async veri =>
            {
                SirketIsletimDurumu durum = Durum(veri, sirketKimligi);
                UrunKaydi? urun = durum.Urunler.FirstOrDefault(x =>
                    x.UrunKimligi.Equals(istek.UrunKimligi, StringComparison.OrdinalIgnoreCase));
                if (urun is null) return;
                decimal istenen = urun.FiyatlandirmaModeli == "kullanim"
                    ? istek.KullanimBasinaUcret
                    : istek.AbonelikUcreti;
                decimal fiyat = V9FiyatPolitikasi.Sinirla(urun.UrunTuru, urun.Kategori, istenen);
                if (urun.FiyatlandirmaModeli == "kullanim")
                {
                    urun.KullanimBasinaUcret = fiyat;
                    urun.AbonelikUcreti = 0;
                }
                else
                {
                    urun.AbonelikUcreti = fiyat;
                    urun.KullanimBasinaUcret = urun.FiyatlandirmaModeli == "freemium"
                        ? Math.Min(fiyat / 20m, 10m)
                        : 0;
                }
                urun.Aktif = istek.Aktif;
                IslemEkle(durum, "v9-urun-fiyat", $"{urun.UrunAdi} fiyatı {fiyat:N2} TL olarak sınır içinde güncellendi.", 0);
                await TemelKaydetAsync(cancellationToken);
                sonuc = IslemSonucu.Basari("Ürün fiyatı kategori sınırları içinde kaydedildi.", new
                {
                    fiyat,
                    aralik = V9FiyatPolitikasi.Aralik(urun.UrunTuru, urun.Kategori)
                });
            }, cancellationToken);
            return sonuc;
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> YayinMaliyetiniDengeleAsync(
        string sirketKimligi,
        string urunKimligi,
        decimal yayinOncesiKasa,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            IslemSonucu sonuc = IslemSonucu.Basari("Yayın maliyeti dengeli.");
            await TemelKilitAsync(async veri =>
            {
                SirketKaydi? sirket = SirketBul(sirketKimligi);
                if (sirket is null) return;
                SirketIsletimDurumu durum = Durum(veri, sirketKimligi);
                UrunKaydi? urun = durum.Urunler.FirstOrDefault(x =>
                    x.UrunKimligi.Equals(urunKimligi, StringComparison.OrdinalIgnoreCase));
                if (urun is null) return;
                decimal eskiKesinti = Math.Max(0, yayinOncesiKasa - sirket.Kasa);
                decimal hedef = urun.UrunTuru switch
                {
                    "isletim-sistemi" => 8_000m,
                    "platform" => 6_000m,
                    "altyapi" => 5_000m,
                    _ => 2_500m
                };
                decimal iade = Math.Max(0, eskiKesinti - hedef);
                if (iade > 0)
                {
                    sirket.Kasa += iade;
                    durum.ToplamYatirimHarcamasi = Math.Max(0, durum.ToplamYatirimHarcamasi - iade);
                }
                IslemEkle(durum, "v9-yayin-maliyet", $"{urun.UrunAdi} V9 yayın bedeli {hedef:N2} TL olarak dengelendi.", -hedef);
                FiyatlariSinirla(durum);
                await TemelKaydetAsync(cancellationToken);
                sonuc = IslemSonucu.Basari($"Yayın maliyeti {hedef:N2} TL; eski formülden {iade:N2} TL iade edildi.");
            }, cancellationToken);
            return sonuc;
        }
        finally { _kilit.Release(); }
    }

    public IReadOnlyList<object> YatirimMagazasi(string sirketKimligi)
    {
        SirketIsletimDosyasi veri = Veri();
        SirketIsletimDurumu durum = Durum(veri, sirketKimligi);
        return Yatirimlar.Select(x =>
        {
            int seviye = Seviye(durum, x.Key);
            return (object)new
            {
                yatirimTuru = x.Key,
                ad = x.Value.Ad,
                aciklama = x.Value.Kapasite > 0
                    ? $"Toplam fiziksel kapasiteye seviye başına yaklaşık {x.Value.Kapasite:N0} birim ekler."
                    : "Pazar, kalite veya müşteri yönetimini güçlendirir.",
                seviye,
                sonrakiMaliyet = YatirimMaliyeti(x.Key, seviye),
                kapasiteKatkisi = x.Value.Kapasite
            };
        }).ToList().AsReadOnly();
    }

    public IReadOnlyList<object> KrediPaketleri() =>
    [
        new { krediTuru = "isletme", ad = "Dengeli İşletme Kredisi", asgariTutar = 5_000m, azamiTutar = 250_000m, tickFaizOrani = 0.0025m, taksitSayisi = 20, odemeAraligiTick = 5 }
    ];

    public V9SirketKapasiteDurumu KapasiteOzeti(string sirketKimligi) =>
        KopyalaKapasite(KapasiteDurumu(sirketKimligi));

    public object FiyatAraligi(UrunKaydi urun)
    {
        V9FiyatAraligi aralik = V9FiyatPolitikasi.Aralik(urun.UrunTuru, urun.Kategori);
        return new { aralik.Min, aralik.Max, aralik.Adim, aralik.Onerilen };
    }

    private void HizmetTalebiniGuncelle(long tickNumarasi)
    {
        List<HizmetTanimi> hizmetler = _katalog.Hizmetler.Where(x => x.Aktif).ToList();
        if (hizmetler.Count == 0) return;
        int toplamHedef = Math.Max(10_000, (int)Math.Round(_musteriler.AktifMusteriSayisi * 0.56));
        Dictionary<string, double> agirliklar = new(StringComparer.OrdinalIgnoreCase);
        foreach (HizmetTanimi hizmet in hizmetler)
        {
            string anahtar = $"{hizmet.HizmetKimligi}@{hizmet.HizmetSurumu}";
            V9TalepKaydi kayit = TalepKaydi(_pazar.HizmetTalepleri, anahtar, hizmet.HizmetKimligi, "hizmet");
            TrendiGuncelle(kayit, tickNumarasi, nadirlik: 0.012);
            uint karma = KaliciKarma(anahtar);
            double taban = 0.65 + karma % 170 / 100d;
            double gecmis = kayit.BuTickTalep > 0 ? Math.Sqrt(kayit.BuTickTalep + 1) : 1;
            agirliklar[anahtar] = Math.Max(0.01, taban * kayit.TrendCarpani * (0.75 + gecmis * 0.025));
        }
        double toplamAgirlik = agirliklar.Values.Sum();
        int dagitilan = 0;
        foreach (HizmetTanimi hizmet in hizmetler)
        {
            string anahtar = $"{hizmet.HizmetKimligi}@{hizmet.HizmetSurumu}";
            V9TalepKaydi kayit = _pazar.HizmetTalepleri[anahtar];
            int ham = (int)Math.Round(toplamHedef * agirliklar[anahtar] / Math.Max(0.01, toplamAgirlik));
            int yeni = kayit.BuTickTalep <= 0 ? ham : (int)Math.Round(kayit.BuTickTalep * 0.72 + ham * 0.28);
            kayit.OncekiTalep = kayit.BuTickTalep;
            kayit.BuTickTalep = Math.Max(0, yeni);
            kayit.DegisimYuzdesi = kayit.OncekiTalep <= 0 ? 0 : (kayit.BuTickTalep - kayit.OncekiTalep) * 100d / kayit.OncekiTalep;
            dagitilan += kayit.BuTickTalep;
        }
        if (dagitilan < toplamHedef && hizmetler.Count > 0)
        {
            string ilk = $"{hizmetler[0].HizmetKimligi}@{hizmetler[0].HizmetSurumu}";
            _pazar.HizmetTalepleri[ilk].BuTickTalep += toplamHedef - dagitilan;
        }
    }

    private void IsletimSistemleriniDagit(
        Dictionary<string, (SirketKaydi Sirket, SirketIsletimDurumu Durum, UrunKaydi Urun)> urunler,
        long tickNumarasi)
    {
        List<(SirketKaydi Sirket, UrunKaydi Urun)> sistemler = urunler.Values
            .Where(x => x.Urun.Aktif && x.Urun.UrunTuru == "isletim-sistemi" && x.Urun.KullaniciKapasitesi > 0)
            .Select(x => (x.Sirket, x.Urun)).ToList();
        Dictionary<string, int> sayac = sistemler.ToDictionary(x => x.Urun.UygulamaKimligi, _ => 0, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, (SirketKaydi Sirket, UrunKaydi Urun)> indeks = sistemler
            .ToDictionary(x => x.Urun.UygulamaKimligi, x => x, StringComparer.OrdinalIgnoreCase);

        foreach (Musteri musteri in _musteriler.Musteriler.Where(x => x.Aktif))
        {
            (SirketKaydi Sirket, UrunKaydi Urun)? secim = null;
            if (!string.IsNullOrWhiteSpace(musteri.IsletimSistemiKimligi) &&
                indeks.TryGetValue(musteri.IsletimSistemiKimligi, out var mevcut) &&
                sayac[mevcut.Urun.UygulamaKimligi] < mevcut.Urun.KullaniciKapasitesi &&
                (tickNumarasi - musteri.SonIsletimSistemiDegisimTicki < 8 || _rastgele.NextDouble() > 0.025))
            {
                secim = mevcut;
            }
            else
            {
                secim = AgirlikliUrunSec(sistemler, sayac, musteri);
            }

            if (secim is null)
            {
                musteri.IsletimSistemiKimligi = "isletim-sistemi-bekleniyor";
                musteri.IsletimSistemiSurumu = string.Empty;
                musteri.IsletimSistemiSirketKimligi = string.Empty;
                continue;
            }
            var secilen = secim.Value;
            if (!musteri.IsletimSistemiKimligi.Equals(secilen.Urun.UygulamaKimligi, StringComparison.OrdinalIgnoreCase))
            {
                musteri.IsletimSistemiDegisimSayisi++;
                musteri.SonIsletimSistemiDegisimTicki = tickNumarasi;
                musteri.IsletimSistemiMemnuniyeti = 55;
            }
            musteri.IsletimSistemiKimligi = secilen.Urun.UygulamaKimligi;
            musteri.IsletimSistemiSurumu = "1.0";
            musteri.IsletimSistemiSirketKimligi = secilen.Sirket.SirketKimligi;
            sayac[secilen.Urun.UygulamaKimligi]++;
        }

        foreach ((SirketKaydi Sirket, UrunKaydi Urun) sistem in sistemler)
            sistem.Urun.AktifKullaniciSayisi = sayac[sistem.Urun.UygulamaKimligi];
        IsletimSistemiPazarDeposu.MusteriDagiliminiGuncelle(sayac);

        V9TalepKaydi talep = _pazar.IsletimSistemiTalebi;
        talep.OncekiTalep = talep.BuTickTalep;
        talep.BuTickTalep = _musteriler.AktifMusteriSayisi;
        talep.ArzKapasitesi = sistemler.Sum(x => x.Urun.KullaniciKapasitesi);
        talep.KarsilananTalep = sayac.Values.Sum();
        talep.ArzDagilimi = sistemler.Select(x => new V9ArzPayi
        {
            SirketKimligi = x.Sirket.SirketKimligi,
            SirketAdi = x.Sirket.SirketAdi,
            UrunVeyaHizmetKimligi = x.Urun.UygulamaKimligi,
            Ad = x.Urun.UrunAdi,
            Kapasite = x.Urun.KullaniciKapasitesi,
            Karsilanan = sayac[x.Urun.UygulamaKimligi],
            PazarPayi = talep.KarsilananTalep <= 0 ? 0 : sayac[x.Urun.UygulamaKimligi] * 100d / talep.KarsilananTalep,
            Fiyat = UrunFiyati(x.Urun)
        }).OrderByDescending(x => x.Karsilanan).ToList();
        GecmiseEkle(talep, tickNumarasi);
    }

    private void UygulamalariDagit(
        Dictionary<string, (SirketKaydi Sirket, SirketIsletimDurumu Durum, UrunKaydi Urun)> urunler,
        long tickNumarasi)
    {
        List<(SirketKaydi Sirket, UrunKaydi Urun)> uygulamalar = urunler.Values
            .Where(x => x.Urun.Aktif && x.Urun.UrunTuru != "isletim-sistemi" && x.Urun.KullaniciKapasitesi > 0)
            .Select(x => (x.Sirket, x.Urun)).ToList();
        Dictionary<string, int> sayac = uygulamalar.ToDictionary(x => x.Urun.UygulamaKimligi, _ => 0, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<(SirketKaydi Sirket, UrunKaydi Urun)>> kategoriler = uygulamalar
            .GroupBy(x => x.Urun.Kategori, StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        HashSet<string> aktifKimlikler = uygulamalar.Select(x => x.Urun.UygulamaKimligi).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (Musteri musteri in _musteriler.Musteriler.Where(x => x.Aktif))
        {
            musteri.KullandigiUygulamalar ??= [];
            musteri.KullandigiUygulamalar.RemoveAll(x => !aktifKimlikler.Contains(x));
            foreach ((string _, List<(SirketKaydi Sirket, UrunKaydi Urun)> adaylar) in kategoriler)
            {
                (SirketKaydi Sirket, UrunKaydi Urun)? mevcut = adaylar
                    .Where(x => musteri.KullandigiUygulamalar.Contains(x.Urun.UygulamaKimligi, StringComparer.OrdinalIgnoreCase))
                    .Cast<(SirketKaydi Sirket, UrunKaydi Urun)?>().FirstOrDefault();
                bool degistir = mevcut is null ||
                    sayac[mevcut.Value.Urun.UygulamaKimligi] >= mevcut.Value.Urun.KullaniciKapasitesi ||
                    (tickNumarasi - musteri.SonUygulamaDegisimTicki >= 6 && _rastgele.NextDouble() < 0.02);
                (SirketKaydi Sirket, UrunKaydi Urun)? secim = degistir
                    ? AgirlikliUrunSec(adaylar, sayac, musteri)
                    : mevcut;
                if (secim is null) continue;
                var secilen = secim.Value;
                musteri.KullandigiUygulamalar.RemoveAll(x => adaylar.Any(a =>
                    a.Urun.UygulamaKimligi.Equals(x, StringComparison.OrdinalIgnoreCase)));
                musteri.KullandigiUygulamalar.Add(secilen.Urun.UygulamaKimligi);
                sayac[secilen.Urun.UygulamaKimligi]++;
                musteri.UygulamaKullanimSayilari.TryGetValue(secilen.Urun.UygulamaKimligi, out int kullanim);
                musteri.UygulamaKullanimSayilari[secilen.Urun.UygulamaKimligi] = kullanim + 1;
                if (degistir)
                {
                    musteri.ToplamUygulamaDegisimSayisi++;
                    musteri.SonUygulamaDegisimTicki = tickNumarasi;
                    musteri.UygulamaMemnuniyetleri[secilen.Urun.UygulamaKimligi] = 55;
                }
            }
        }

        foreach ((SirketKaydi _, UrunKaydi urun) in uygulamalar)
            urun.AktifKullaniciSayisi = sayac[urun.UygulamaKimligi];
    }

    private void UygulamaTalebiniGuncelle(
        Dictionary<string, (SirketKaydi Sirket, SirketIsletimDurumu Durum, UrunKaydi Urun)> urunler,
        long tickNumarasi)
    {
        foreach (IGrouping<string, (SirketKaydi Sirket, SirketIsletimDurumu Durum, UrunKaydi Urun)> grup in
                 urunler.Values.Where(x => x.Urun.Aktif && x.Urun.UrunTuru != "isletim-sistemi")
                     .GroupBy(x => x.Urun.Kategori, StringComparer.OrdinalIgnoreCase))
        {
            V9TalepKaydi kayit = TalepKaydi(_pazar.UygulamaTalepleri, grup.Key, grup.Key, "uygulama-kategorisi");
            TrendiGuncelle(kayit, tickNumarasi, nadirlik: 0.025);
            int ham = (int)Math.Round(_musteriler.AktifMusteriSayisi * kayit.TrendCarpani);
            int yeni = kayit.BuTickTalep <= 0 ? ham : (int)Math.Round(kayit.BuTickTalep * 0.78 + ham * 0.22);
            kayit.OncekiTalep = kayit.BuTickTalep;
            kayit.BuTickTalep = Math.Max(0, yeni);
            kayit.ArzKapasitesi = grup.Sum(x => x.Urun.KullaniciKapasitesi);
            kayit.KarsilananTalep = grup.Sum(x => x.Urun.AktifKullaniciSayisi);
            kayit.DegisimYuzdesi = kayit.OncekiTalep <= 0 ? 0 : (kayit.BuTickTalep - kayit.OncekiTalep) * 100d / kayit.OncekiTalep;
            kayit.ArzDagilimi = grup.Select(x => new V9ArzPayi
            {
                SirketKimligi = x.Sirket.SirketKimligi,
                SirketAdi = x.Sirket.SirketAdi,
                UrunVeyaHizmetKimligi = x.Urun.UygulamaKimligi,
                Ad = x.Urun.UrunAdi,
                Kapasite = x.Urun.KullaniciKapasitesi,
                Karsilanan = x.Urun.AktifKullaniciSayisi,
                PazarPayi = kayit.KarsilananTalep <= 0 ? 0 : x.Urun.AktifKullaniciSayisi * 100d / kayit.KarsilananTalep,
                Fiyat = UrunFiyati(x.Urun)
            }).OrderByDescending(x => x.Karsilanan).ToList();
            GecmiseEkle(kayit, tickNumarasi);
        }
    }

    private decimal UrunGelirleriniIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        decimal toplam = 0;
        foreach (UrunKaydi urun in durum.Urunler.Where(x => x.Aktif))
        {
            int oncekiToplam = urun.ToplamEdinilenKullanici - urun.ToplamKaybedilenKullanici;
            int yeni = Math.Max(0, urun.AktifKullaniciSayisi - Math.Max(0, oncekiToplam));
            decimal gelir = TickUrunGeliri(urun, yeni);
            if (gelir > 0)
            {
                sirket.Kasa += gelir;
                sirket.ToplamGelir += gelir;
                urun.ToplamGelir += gelir;
                durum.ToplamUrunGeliri += gelir;
                if (urun.FiyatlandirmaModeli is "abonelik" or "freemium")
                    durum.ToplamAbonelikGeliri += gelir;
            }
            urun.ToplamEdinilenKullanici += yeni;
            urun.ToplamKaybedilenKullanici = Math.Max(0, urun.ToplamEdinilenKullanici - urun.AktifKullaniciSayisi);
            toplam += gelir;
        }
        durum.ToplamAboneSayisi = durum.Urunler.Where(x => x.Aktif).Sum(x => x.AktifKullaniciSayisi);
        return decimal.Round(toplam, 2);
    }

    private decimal ProtokolLisanslariniIsle(
        SirketIsletimDosyasi veri,
        SirketKaydi sirket,
        SirketIsletimDurumu durum)
    {
        decimal gelir = 0;
        foreach (OzelProtokolKaydi protokol in veri.Protokoller.Where(x =>
                     x.Aktif && x.SahipSirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase)))
        {
            decimal tickBedeli = Math.Clamp(protokol.TickLisansBedeli, 0, 250m);
            foreach (string kullananKimligi in protokol.BenimseyenSirketler.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                SirketKaydi? kullanan = SirketBul(kullananKimligi);
                if (kullanan is null || kullanan.Kasa <= 0 || tickBedeli <= 0) continue;
                decimal odeme = Math.Min(kullanan.Kasa, tickBedeli);
                kullanan.Kasa -= odeme;
                Durum(veri, kullananKimligi).ToplamIsletmeGideri += odeme;
                sirket.Kasa += odeme;
                sirket.ToplamGelir += odeme;
                protokol.ToplamLisansGeliri += odeme;
                gelir += odeme;
            }
        }
        return decimal.Round(gelir, 2);
    }

    private decimal IsletmeGideriniIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirket.SirketKimligi);
        int aktifHizmet = sirket.Hizmetler.Count(x => x.Aktif);
        int aktifUrun = durum.Urunler.Count(x => x.Aktif);
        int aktifKullanici = durum.Urunler.Where(x => x.Aktif).Sum(x => x.AktifKullaniciSayisi);
        decimal bakim = durum.YatirimSeviyeleri.Sum(x => YatirimBakimi(x.Key, x.Value));
        decimal gider =
            80m +
            aktifHizmet * 2.5m +
            aktifUrun * 24m +
            kapasite.AyrilmisKapasite * 0.008m +
            kapasite.KullanilanKapasite * 0.035m +
            aktifKullanici * 0.006m +
            bakim +
            (decimal)Math.Max(0, durum.TeknikBorc) * 0.08m;
        if (!sirket.BagliMi) gider = Math.Max(35m, gider * 0.20m);
        gider = decimal.Round(Math.Clamp(gider, 35m, 50_000m), 2);
        decimal odenen = Math.Min(Math.Max(0, sirket.Kasa), gider);
        sirket.Kasa -= odenen;
        durum.ToplamIsletmeGideri += odenen;
        decimal acik = gider - odenen;
        durum.OdenemeyenGider = decimal.Round(Math.Clamp(durum.OdenemeyenGider * 0.35m + acik, 0, 50_000m), 2);
        if (acik > 0)
        {
            sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - 0.02);
            durum.KrediNotu = Math.Max(300, durum.KrediNotu - 1);
        }
        return gider;
    }

    private decimal KredileriIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        decimal toplam = 0;
        foreach (KrediKaydi kredi in durum.Krediler.Where(x => x.Aktif && _tick >= x.SonrakiOdemeTicki))
        {
            decimal odeme = Math.Min(kredi.TaksitTutari, kredi.KalanBorc);
            decimal odenen = Math.Min(Math.Max(0, sirket.Kasa), odeme);
            sirket.Kasa -= odenen;
            kredi.KalanBorc = Math.Max(0, kredi.KalanBorc - odenen);
            toplam += odenen;
            durum.ToplamFinansmanGideri += odenen;
            if (odenen >= odeme)
            {
                kredi.KalanTaksit = Math.Max(0, kredi.KalanTaksit - 1);
                durum.KrediNotu = Math.Min(900, durum.KrediNotu + 1);
            }
            else
            {
                kredi.GecikmeSayisi++;
                decimal tavan = kredi.AnaPara * 1.50m;
                kredi.KalanBorc = Math.Min(tavan, decimal.Round(kredi.KalanBorc * 1.0025m, 2));
                durum.KrediNotu = Math.Max(300, durum.KrediNotu - 4);
                sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - 0.04);
            }
            kredi.SonrakiOdemeTicki = _tick + Math.Max(2, kredi.OdemeAraligiTick);
            if (kredi.KalanBorc <= 0 || kredi.KalanTaksit <= 0) kredi.Aktif = false;
        }
        return decimal.Round(toplam, 2);
    }

    private void HizmetArziniVeKarsilanmayiGuncelle()
    {
        IReadOnlyList<V9HizmetGerceklesme> gerceklesmeler = V9HizmetSonucDeposu.Getir();
        foreach ((string anahtar, V9TalepKaydi talep) in _pazar.HizmetTalepleri)
        {
            string[] parca = anahtar.Split('@', 2);
            string kimlik = parca[0];
            string surum = parca.Length > 1 ? parca[1] : "1.0";
            List<(SirketKaydi Sirket, int Kapasite)> saglayicilar = _sirketler.SirketKayitlari
                .Select(s => (Sirket: s, Hizmet: s.Hizmetler.FirstOrDefault(h => h.Aktif &&
                    h.HizmetKimligi.Equals(kimlik, StringComparison.OrdinalIgnoreCase) &&
                    h.HizmetSurumu.Equals(surum, StringComparison.OrdinalIgnoreCase))))
                .Where(x => x.Hizmet is not null)
                .Select(x => (x.Sirket, Math.Max(0, x.Hizmet!.AzamiEszamanliIs * 20)))
                .ToList();
            talep.ArzKapasitesi = saglayicilar.Sum(x => x.Kapasite);
            List<V9HizmetGerceklesme> sonuclar = gerceklesmeler.Where(x =>
                x.HizmetAnahtari.Equals(anahtar, StringComparison.OrdinalIgnoreCase)).ToList();
            int ornek = sonuclar.Sum(x => x.Toplam);
            int basarili = sonuclar.Sum(x => x.Basarili);
            double basariOrani = ornek <= 0 ? 0 : basarili / (double)ornek;
            int teorik = Math.Min(talep.BuTickTalep, talep.ArzKapasitesi);
            talep.KarsilananTalep = (int)Math.Round(teorik * basariOrani);
            double payTabani = sonuclar.Sum(x => x.Basarili + x.Basarisiz * 0.5);
            talep.ArzDagilimi = saglayicilar.Select(x =>
            {
                V9HizmetGerceklesme? sonuc = sonuclar.FirstOrDefault(y =>
                    y.SirketKimligi.Equals(x.Sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase));
                double puan = (sonuc?.Basarili ?? 0) + (sonuc?.Basarisiz ?? 0) * 0.5;
                return new V9ArzPayi
                {
                    SirketKimligi = x.Sirket.SirketKimligi,
                    SirketAdi = x.Sirket.SirketAdi,
                    UrunVeyaHizmetKimligi = anahtar,
                    Ad = kimlik,
                    Kapasite = x.Kapasite,
                    Karsilanan = payTabani <= 0 ? 0 : (int)Math.Round(talep.KarsilananTalep * puan / payTabani),
                    PazarPayi = payTabani <= 0 ? 0 : puan * 100d / payTabani,
                    Fiyat = MotorHizmetFiyatlari.Fiyat(kimlik, surum)
                };
            }).OrderByDescending(x => x.Karsilanan).ToList();
            GecmiseEkle(talep, _tick);
        }
    }

    private void TahsisleriUygula(
        SirketKaydi sirket,
        SirketIsletimDurumu durum,
        V9SirketKapasiteDurumu kapasite)
    {
        List<string> hizmetAnahtarlari = sirket.Hizmetler.Where(x => x.Aktif)
            .Select(x => $"hizmet:{x.HizmetKimligi}@{x.HizmetSurumu}").ToList();
        List<string> urunAnahtarlari = durum.Urunler.Where(x => x.Aktif)
            .Select(x => $"urun:{x.UrunKimligi}").ToList();
        HashSet<string> gecerli = hizmetAnahtarlari.Concat(urunAnahtarlari)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        kapasite.Tahsisler = kapasite.Tahsisler
            .Where(x => gecerli.Contains(x.Key) && x.Value >= 0)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        if (!kapasite.KullaniciElleAyarladi || kapasite.Tahsisler.Count == 0)
            kapasite.Tahsisler = OtomatikTahsis(durum, hizmetAnahtarlari, urunAnahtarlari, kapasite.ToplamFizikselKapasite);

        int toplam = kapasite.Tahsisler.Values.Sum();
        if (toplam > kapasite.ToplamFizikselKapasite && toplam > 0)
        {
            double carpan = kapasite.ToplamFizikselKapasite / (double)toplam;
            kapasite.Tahsisler = kapasite.Tahsisler.ToDictionary(
                x => x.Key,
                x => Math.Max(0, (int)Math.Floor(x.Value * carpan)),
                StringComparer.OrdinalIgnoreCase);
        }

        foreach (SunulanHizmet hizmet in sirket.Hizmetler)
        {
            string anahtar = $"hizmet:{hizmet.HizmetKimligi}@{hizmet.HizmetSurumu}";
            int tahsis = kapasite.Tahsisler.TryGetValue(anahtar, out int x) ? x : 0;
            hizmet.AzamiEszamanliIs = hizmet.Aktif ? Math.Max(1, tahsis / 10) : 0;
        }
        foreach (UrunKaydi urun in durum.Urunler)
        {
            string anahtar = $"urun:{urun.UrunKimligi}";
            int tahsis = kapasite.Tahsisler.TryGetValue(anahtar, out int x) ? x : 0;
            urun.TabanKullaniciKapasitesi = 0;
            urun.SatinAlinanKullaniciKapasitesi = 0;
            urun.AltyapiKapasiteBonusu = 0;
            urun.KullaniciKapasitesi = urun.Aktif ? tahsis * 40 : 0;
            if (urun.AktifKullaniciSayisi > urun.KullaniciKapasitesi)
                urun.AktifKullaniciSayisi = urun.KullaniciKapasitesi;
        }
        kapasite.AyrilmisKapasite = kapasite.Tahsisler.Values.Sum();
        kapasite.KullanilanKapasite =
            sirket.AktifIsSayisi * 10 +
            durum.Urunler.Where(x => x.Aktif).Sum(x => (int)Math.Ceiling(x.AktifKullaniciSayisi / 40d));
    }

    private Dictionary<string, int> OtomatikTahsis(
        SirketIsletimDurumu durum,
        IReadOnlyList<string> hizmetler,
        IReadOnlyList<string> urunler,
        int toplam)
    {
        Dictionary<string, int> sonuc = new(StringComparer.OrdinalIgnoreCase);
        if (hizmetler.Count + urunler.Count == 0) return sonuc;
        int hizmetHavuzu = urunler.Count == 0 ? toplam : (int)Math.Round(toplam * 0.38);
        int urunHavuzu = toplam - hizmetHavuzu;
        if (hizmetler.Count > 0)
        {
            int pay = Math.Max(10, hizmetHavuzu / hizmetler.Count);
            foreach (string anahtar in hizmetler) sonuc[anahtar] = pay;
        }
        if (urunler.Count > 0)
        {
            Dictionary<string, int> agirlik = urunler.ToDictionary(
                x => x,
                x => durum.Urunler.FirstOrDefault(u => $"urun:{u.UrunKimligi}".Equals(x, StringComparison.OrdinalIgnoreCase))?.UrunTuru == "isletim-sistemi" ? 4 : 2,
                StringComparer.OrdinalIgnoreCase);
            int toplamAgirlik = agirlik.Values.Sum();
            foreach ((string anahtar, int a) in agirlik)
                sonuc[anahtar] = Math.Max(10, urunHavuzu * a / Math.Max(1, toplamAgirlik));
        }
        int kullanilan = sonuc.Values.Sum();
        if (kullanilan > toplam && kullanilan > 0)
        {
            double carpan = toplam / (double)kullanilan;
            foreach (string anahtar in sonuc.Keys.ToList())
                sonuc[anahtar] = Math.Max(0, (int)Math.Floor(sonuc[anahtar] * carpan));
        }
        return sonuc;
    }

    private (SirketKaydi Sirket, UrunKaydi Urun)? AgirlikliUrunSec(
        IReadOnlyList<(SirketKaydi Sirket, UrunKaydi Urun)> adaylar,
        IReadOnlyDictionary<string, int> sayac,
        Musteri musteri)
    {
        List<((SirketKaydi Sirket, UrunKaydi Urun) Aday, double Agirlik)> uygun = [];
        foreach (var aday in adaylar)
        {
            sayac.TryGetValue(aday.Urun.UygulamaKimligi, out int kullanim);
            if (kullanim >= aday.Urun.KullaniciKapasitesi) continue;
            double bosluk = Math.Clamp(1 - kullanim / (double)Math.Max(1, aday.Urun.KullaniciKapasitesi), 0.05, 1);
            double kalite = Math.Clamp((aday.Sirket.KodKalitesiPuani + aday.Sirket.PerformansPuani + aday.Sirket.GuvenlikPuani + aday.Urun.UrunKalitesi) / 400d, 0.08, 1);
            decimal fiyatDegeri = UrunFiyati(aday.Urun);
            double fiyat = 1d / (1 + (double)fiyatDegeri / Math.Max(10d, (double)musteri.TickBasinaHarcamaButcesi));
            uygun.Add((aday, Math.Max(0.001, kalite * fiyat * Math.Sqrt(bosluk) * (0.92 + _rastgele.NextDouble() * 0.16))));
        }
        if (uygun.Count == 0) return null;
        double secim = _rastgele.NextDouble() * uygun.Sum(x => x.Agirlik);
        foreach (var aday in uygun)
        {
            secim -= aday.Agirlik;
            if (secim <= 0) return aday.Aday;
        }
        return uygun[^1].Aday;
    }

    private Dictionary<string, (SirketKaydi Sirket, SirketIsletimDurumu Durum, UrunKaydi Urun)> TumAktifUrunler(
        SirketIsletimDosyasi veri)
    {
        Dictionary<string, (SirketKaydi, SirketIsletimDurumu, UrunKaydi)> sonuc = new(StringComparer.OrdinalIgnoreCase);
        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
        {
            SirketIsletimDurumu durum = Durum(veri, sirket.SirketKimligi);
            foreach (UrunKaydi urun in durum.Urunler.Where(x => x.Aktif && !string.IsNullOrWhiteSpace(x.UygulamaKimligi)))
                sonuc[$"{sirket.SirketKimligi}|{urun.UygulamaKimligi}"] = (sirket, durum, urun);
        }
        return sonuc;
    }

    private void FiyatlariSinirla(SirketIsletimDurumu durum)
    {
        foreach (UrunKaydi urun in durum.Urunler)
        {
            decimal mevcut = urun.FiyatlandirmaModeli == "kullanim"
                ? urun.KullanimBasinaUcret
                : urun.AbonelikUcreti;
            decimal fiyat = V9FiyatPolitikasi.Sinirla(urun.UrunTuru, urun.Kategori, mevcut);
            if (urun.FiyatlandirmaModeli == "kullanim")
            {
                urun.KullanimBasinaUcret = fiyat;
                urun.AbonelikUcreti = 0;
            }
            else
            {
                urun.AbonelikUcreti = fiyat;
                if (urun.FiyatlandirmaModeli != "freemium") urun.KullanimBasinaUcret = 0;
            }
        }
    }

    private static decimal TickUrunGeliri(UrunKaydi urun, int yeniKullanici)
    {
        decimal fiyat = UrunFiyati(urun);
        decimal gelir = urun.FiyatlandirmaModeli switch
        {
            "abonelik" => urun.AktifKullaniciSayisi * fiyat / 30m,
            "freemium" => urun.AktifKullaniciSayisi * fiyat * 0.20m / 30m + urun.AktifKullaniciSayisi * urun.KullanimBasinaUcret * 0.05m,
            "kullanim" => urun.AktifKullaniciSayisi * fiyat * 0.14m,
            "lisans" or "tek-seferlik" => yeniKullanici * fiyat,
            _ => 0
        };
        return decimal.Round(Math.Max(0, gelir), 2);
    }

    private static decimal UrunFiyati(UrunKaydi urun) =>
        urun.FiyatlandirmaModeli == "kullanim"
            ? urun.KullanimBasinaUcret
            : urun.AbonelikUcreti;

    private int ToplamFizikselKapasite(SirketIsletimDurumu durum)
    {
        int toplam = 2_500;
        foreach ((string tur, int seviye) in durum.YatirimSeviyeleri)
        {
            if (!Yatirimlar.TryGetValue(tur, out var paket) || paket.Kapasite <= 0) continue;
            toplam += (int)Math.Round(paket.Kapasite * Math.Pow(Math.Max(0, seviye), 1.08));
        }
        return Math.Clamp(toplam, 500, 500_000);
    }

    private static decimal YatirimBakimi(string tur, int seviye) =>
        seviye <= 0 ? 0 : tur switch
        {
            "cpu" => seviye * 45m,
            "ram" => seviye * 38m,
            "ag" => seviye * 42m,
            "depolama" => seviye * 30m,
            "guvenlik" => seviye * 55m,
            "yedek" => seviye * 65m,
            "destek" => seviye * 45m,
            "pazarlama" => seviye * 55m,
            "satis" => seviye * 60m,
            "arge" => seviye * 65m,
            _ => 0
        };

    private static decimal YatirimMaliyeti(string tur, int mevcutSeviye)
    {
        if (!Yatirimlar.TryGetValue(tur, out var paket)) return decimal.MaxValue;
        return decimal.Round(paket.Taban * (decimal)Math.Pow(1.38, Math.Max(0, mevcutSeviye)), 2);
    }

    private static void EskiKapasiteSatinalimlariniTemizle(SirketIsletimDurumu durum)
    {
        foreach (HizmetKaliciAyari ayar in durum.HizmetAyarlari.Values)
            ayar.SatinAlinanKapasite = 0;
        foreach (UrunKaydi urun in durum.Urunler)
        {
            urun.SatinAlinanKullaniciKapasitesi = 0;
            urun.AltyapiKapasiteBonusu = 0;
        }
    }

    private void TrendiGuncelle(V9TalepKaydi kayit, long tick, double nadirlik)
    {
        if (kayit.TrendBitisTicki > 0 && tick > kayit.TrendBitisTicki)
        {
            kayit.AktifTrend = "normal";
            kayit.TrendBitisTicki = 0;
        }
        if (kayit.TrendBitisTicki == 0 && _rastgele.NextDouble() < nadirlik)
        {
            bool yukari = _rastgele.NextDouble() < 0.62;
            kayit.TrendCarpani = yukari
                ? 1.15 + _rastgele.NextDouble() * 0.55
                : 0.68 + _rastgele.NextDouble() * 0.22;
            kayit.AktifTrend = yukari ? "yukselen" : "dusen";
            kayit.TrendBitisTicki = tick + _rastgele.Next(5, 15);
        }
        else if (kayit.TrendBitisTicki == 0)
        {
            kayit.TrendCarpani += (1 - kayit.TrendCarpani) * 0.12 + (_rastgele.NextDouble() - 0.5) * 0.025;
            kayit.TrendCarpani = Math.Clamp(kayit.TrendCarpani, 0.82, 1.18);
        }
    }

    private static void GecmiseEkle(V9TalepKaydi kayit, long tick)
    {
        if (kayit.Gecmis.LastOrDefault()?.TickNumarasi == tick) kayit.Gecmis.RemoveAt(kayit.Gecmis.Count - 1);
        kayit.Gecmis.Add(new V9TalepNoktasi
        {
            TickNumarasi = tick,
            Talep = kayit.BuTickTalep,
            Karsilanan = kayit.KarsilananTalep,
            Arz = kayit.ArzKapasitesi,
            TrendCarpani = kayit.TrendCarpani
        });
        if (kayit.Gecmis.Count > 120) kayit.Gecmis.RemoveRange(0, kayit.Gecmis.Count - 120);
    }

    private V9TalepKaydi TalepKaydi(
        Dictionary<string, V9TalepKaydi> kaynak,
        string anahtar,
        string ad,
        string tur)
    {
        if (kaynak.TryGetValue(anahtar, out V9TalepKaydi? kayit)) return kayit;
        kayit = new V9TalepKaydi { Anahtar = anahtar, Ad = ad, Tur = tur, TrendCarpani = 1 };
        kaynak[anahtar] = kayit;
        return kayit;
    }

    private V9SirketKapasiteDurumu KapasiteDurumu(string sirketKimligi)
    {
        if (_pazar.SirketKapasiteleri.TryGetValue(sirketKimligi, out V9SirketKapasiteDurumu? durum)) return durum;
        durum = new V9SirketKapasiteDurumu { SirketKimligi = sirketKimligi };
        _pazar.SirketKapasiteleri[sirketKimligi] = durum;
        return durum;
    }

    private V9SirketPazarOzeti SirketOzeti(string sirketKimligi, string sirketAdi)
    {
        V9SirketPazarOzeti? ozet = _pazar.SirketOzetleri.FirstOrDefault(x =>
            x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
        if (ozet is not null) return ozet;
        ozet = new V9SirketPazarOzeti { SirketKimligi = sirketKimligi, SirketAdi = sirketAdi };
        _pazar.SirketOzetleri.Add(ozet);
        return ozet;
    }

    private static V9SirketKapasiteDurumu KopyalaKapasite(V9SirketKapasiteDurumu kaynak) => new()
    {
        SirketKimligi = kaynak.SirketKimligi,
        ToplamFizikselKapasite = kaynak.ToplamFizikselKapasite,
        AyrilmisKapasite = kaynak.AyrilmisKapasite,
        KullanilanKapasite = kaynak.KullanilanKapasite,
        KullaniciElleAyarladi = kaynak.KullaniciElleAyarladi,
        Tahsisler = new Dictionary<string, int>(kaynak.Tahsisler, StringComparer.OrdinalIgnoreCase)
    };

    private TickBaslangici Anlik(SirketKaydi sirket, SirketIsletimDurumu durum) => new(
        sirket.Kasa,
        sirket.ToplamGelir,
        sirket.ToplamCeza,
        sirket.ToplamIade,
        durum.ToplamUrunGeliri,
        durum.ToplamAbonelikGeliri,
        durum.ToplamIsletmeGideri,
        durum.ToplamFinansmanGideri);

    private static int Seviye(SirketIsletimDurumu durum, string tur) =>
        durum.YatirimSeviyeleri.TryGetValue(tur, out int seviye) ? Math.Max(0, seviye) : 0;

    private static uint KaliciKarma(string metin)
    {
        uint karma = 2166136261;
        foreach (char c in metin) { karma ^= c; karma *= 16777619; }
        return karma;
    }

    private void Degerle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        decimal borc = durum.Krediler.Where(x => x.Aktif).Sum(x => x.KalanBorc);
        V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirket.SirketKimligi);
        decimal duzenli = durum.Urunler.Where(x => x.Aktif).Sum(x => TickUrunGeliri(x, 0));
        decimal kalite = (decimal)(sirket.KodKalitesiPuani + sirket.PerformansPuani + sirket.GuvenlikPuani +
            sirket.ItibarPuani + sirket.GuvenilirlikPuani + sirket.OrtalamaMusteriMemnuniyeti) * 160m;
        decimal deger =
            Math.Max(0, sirket.Kasa) * 0.35m +
            duzenli * 28m +
            durum.ToplamAboneSayisi * 35m +
            kapasite.ToplamFizikselKapasite * 4m +
            kalite +
            durum.YatirimSeviyeleri.Values.Sum() * 3_000m -
            borc -
            durum.OdenemeyenGider * 1.25m -
            (decimal)(durum.OperasyonRiski + durum.TeknikBorc) * 120m;
        durum.SirketDegeri = decimal.Round(Math.Max(0, deger), 2);
        durum.TahminiHisseFiyati = decimal.Round(durum.SirketDegeri / 10_000m, 4);
    }

    private void NormalizeEt()
    {
        _pazar.Surum = 9;
        _pazar.HizmetTalepleri ??= new(StringComparer.OrdinalIgnoreCase);
        _pazar.UygulamaTalepleri ??= new(StringComparer.OrdinalIgnoreCase);
        _pazar.SirketKapasiteleri ??= new(StringComparer.OrdinalIgnoreCase);
        _pazar.SirketOzetleri ??= [];
        _pazar.Haberler ??= [];
        _pazar.IsletimSistemiTalebi ??= new V9TalepKaydi { Anahtar = "isletim-sistemi", Ad = "İşletim sistemi", Tur = "isletim-sistemi" };
        foreach (V9TalepKaydi kayit in _pazar.HizmetTalepleri.Values.Concat(_pazar.UygulamaTalepleri.Values).Append(_pazar.IsletimSistemiTalebi))
        {
            kayit.Gecmis ??= [];
            kayit.ArzDagilimi ??= [];
            if (kayit.TrendCarpani <= 0) kayit.TrendCarpani = 1;
        }
        foreach (V9SirketKapasiteDurumu kapasite in _pazar.SirketKapasiteleri.Values)
            kapasite.Tahsisler ??= new(StringComparer.OrdinalIgnoreCase);
    }

    private SirketIsletimDosyasi Veri() =>
        (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel) ?? new SirketIsletimDosyasi());

    private static SirketIsletimDurumu Durum(SirketIsletimDosyasi veri, string sirketKimligi)
    {
        SirketIsletimDurumu? durum = veri.Sirketler.FirstOrDefault(x =>
            x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
        if (durum is not null) return durum;
        durum = new SirketIsletimDurumu { SirketKimligi = sirketKimligi };
        veri.Sirketler.Add(durum);
        return durum;
    }

    private SirketKaydi? SirketBul(string sirketKimligi) =>
        _sirketler.SirketKayitlari.FirstOrDefault(x =>
            x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));

    private SemaphoreSlim TemelKilit() =>
        (SemaphoreSlim)(_kilitAlani.GetValue(_temel)
            ?? throw new InvalidOperationException("V9 temel işletim kilidi boş."));

    private async Task TemelKilitAsync(
        Func<SirketIsletimDosyasi, Task> islem,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = TemelKilit();
        await kilit.WaitAsync(cancellationToken);
        try { await islem(Veri()); }
        finally { kilit.Release(); }
    }

    private async Task TemelKaydetAsync(CancellationToken cancellationToken)
    {
        object? sonuc = _kaydetMetodu.Invoke(_temel, [cancellationToken]);
        if (sonuc is Task gorev) await gorev;
    }

    private static void IslemEkle(
        SirketIsletimDurumu durum,
        string tur,
        string aciklama,
        decimal tutar)
    {
        durum.SonIslemler.Add(new IsletimIslemKaydi
        {
            IslemKimligi = $"v9-islem-{Guid.NewGuid():N}",
            TickNumarasi = V9PazarDeposu.Getir().SonTick,
            IslemTuru = tur,
            Aciklama = aciklama,
            Tutar = tutar
        });
        if (durum.SonIslemler.Count > 200)
            durum.SonIslemler = durum.SonIslemler.TakeLast(200).ToList();
    }

    private async Task KaydetAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        _pazar.GuncellenmeZamani = DateTimeOffset.UtcNow;
        string tmp = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(
            tmp,
            JsonSerializer.Serialize(_pazar, JsonAyarlari),
            new UTF8Encoding(false),
            cancellationToken);
        File.Move(tmp, _dosyaYolu, overwrite: true);
    }

    private void BaslatilmisOlmasiniDogrula()
    {
        if (!_baslatildi) throw new InvalidOperationException("V9 ekonomi yöneticisi henüz başlatılmadı.");
    }

    public async ValueTask DisposeAsync()
    {
        if (!_baslatildi) { _kilit.Dispose(); return; }
        await _kilit.WaitAsync();
        try { await KaydetAsync(CancellationToken.None); }
        finally { _kilit.Release(); _kilit.Dispose(); }
    }
}
