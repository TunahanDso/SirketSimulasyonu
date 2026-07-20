using System.Reflection;
using System.Text;
using System.Text.Json;
using SirketMotoru.Hizmetler;
using SirketMotoru.Musteriler;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

/// <summary>
/// V9.1 tek ekonomi otoritesi. Çevrimdışı şirketleri dondurur; ürün geliri,
/// gider, kredi, kapasite ve pazar talebini tek defterde hesaplar.
/// </summary>
public sealed class V9EkonomiYoneticisi : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, (string Ad, decimal Taban, int Kapasite)> Yatirimlar =
        new Dictionary<string, (string, decimal, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["cpu"] = ("İşlemci Kümesi", 2_500m, 500),
            ["ram"] = ("Bellek Havuzu", 2_200m, 420),
            ["ag"] = ("Ağ Omurgası", 2_500m, 460),
            ["depolama"] = ("Depolama Kümesi", 2_000m, 340),
            ["guvenlik"] = ("Güvenlik Operasyonu", 3_000m, 180),
            ["yedek"] = ("Yedek Sunucu", 3_500m, 300),
            ["destek"] = ("Müşteri Destek Ekibi", 2_400m, 120),
            ["pazarlama"] = ("Pazarlama Departmanı", 3_000m, 0),
            ["satis"] = ("Kurumsal Satış", 3_200m, 0),
            ["arge"] = ("Ar-Ge Laboratuvarı", 3_500m, 180)
        };

    private readonly SirketYoneticisi _sirketler;
    private readonly MusteriYoneticisi _musteriler;
    private readonly HizmetKatalogu _katalog;
    private readonly KodTabanliSirketIsletimYoneticisi _isletim;
    private readonly object _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _temelKilitAlani;
    private readonly MethodInfo _temelKaydetMetodu;
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private readonly Random _rastgele = new(20260726);
    private readonly string _dosyaYolu;
    private readonly Dictionary<string, decimal> _tickBaslangicKasasi = new(StringComparer.OrdinalIgnoreCase);
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
            ?? throw new InvalidOperationException("V9.1 işletim köprüsü kurulamadı.");
        _temel = temelAlani.GetValue(isletim)
            ?? throw new InvalidOperationException("V9.1 temel işletim yöneticisi boş.");
        Type tur = _temel.GetType();
        _veriAlani = tur.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.1 işletim veri alanı bulunamadı.");
        _temelKilitAlani = tur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.1 işletim kilidi bulunamadı.");
        _temelKaydetMetodu = tur.GetMethod("TumunuKaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.1 işletim kayıt metodu bulunamadı.");
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
                    EskiKapasiteleriTemizle(durum);
                    FiyatlariSinirla(durum);
                    V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirket.SirketKimligi);
                    kapasite.ToplamFizikselKapasite = ToplamFizikselKapasite(durum);
                    TahsisleriUygula(sirket, durum, kapasite);
                }
                await TemelKaydetAsync(cancellationToken);
            }, cancellationToken);
            TumKategoriKayitlariniHazirla();
            V9PazarDeposu.Guncelle(_pazar);
            await KaydetAsync(cancellationToken);
            _baslatildi = true;
            KonsolKayitcisi.Basari(
                "V9.1 ekonomi hazır | Çevrimdışı şirket donar, borç büyümez, 200 kategori ve tek fiziksel kapasite havuzu aktiftir.");
        }
        finally { _kilit.Release(); }
    }

    public async Task TickOncesiAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        Dogrula();
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            _tick = tickNumarasi;
            _pazar.SonTick = tickNumarasi;
            _tickBaslangicKasasi.Clear();
            V9HizmetSonucDeposu.TickBaslat(tickNumarasi);
            HizmetTalebiniGuncelle(tickNumarasi);
            TumKategoriKayitlariniHazirla();

            await TemelKilitAsync(async veri =>
            {
                foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
                {
                    SirketIsletimDurumu durum = Durum(veri, sirket.SirketKimligi);
                    V9SirketPazarOzeti ozet = SirketOzeti(sirket);
                    OzetSifirla(ozet);
                    _tickBaslangicKasasi[sirket.SirketKimligi] = sirket.Kasa;
                    EskiKapasiteleriTemizle(durum);
                    FiyatlariSinirla(durum);
                    V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirket.SirketKimligi);
                    kapasite.ToplamFizikselKapasite = ToplamFizikselKapasite(durum);
                    TahsisleriUygula(sirket, durum, kapasite);
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

    public async Task UrunPazariniVeEkonomiyiIsleAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        Dogrula();
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            _tick = tickNumarasi;
            await TemelKilitAsync(async veri =>
            {
                Dictionary<string, int> oncekiKullanicilar = veri.Sirketler
                    .SelectMany(x => x.Urunler)
                    .ToDictionary(x => x.UrunKimligi, x => Math.Max(0, x.AktifKullaniciSayisi), StringComparer.OrdinalIgnoreCase);

                List<(SirketKaydi Sirket, SirketIsletimDurumu Durum, UrunKaydi Urun)> pazarUrunleri =
                    PazarUrunleri(veri);
                CevrimdisiUrunleriBosalt(veri);
                IsletimSistemleriniDagit(pazarUrunleri, tickNumarasi);
                UygulamalariDagit(pazarUrunleri, tickNumarasi);
                UygulamaTalebiniGuncelle(pazarUrunleri, tickNumarasi);

                foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
                {
                    SirketIsletimDurumu durum = Durum(veri, sirket.SirketKimligi);
                    V9SirketPazarOzeti ozet = SirketOzeti(sirket);
                    V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirket.SirketKimligi);
                    KapasiteKullaniminiGuncelle(sirket, durum, kapasite);
                    ozet.ToplamKapasite = kapasite.ToplamFizikselKapasite;
                    ozet.KullanilanKapasite = kapasite.KullanilanKapasite;

                    if (!sirket.BagliMi)
                    {
                        ozet.AktifUygulamaKullanicisi = 0;
                        ozet.IsletimSistemiKullanicisi = 0;
                        ozet.AktifBorc = durum.Krediler.Where(x => x.Aktif).Sum(x => x.KalanBorc);
                        continue;
                    }

                    decimal uygulamaGeliri = UrunGelirleriniIsle(sirket, durum, oncekiKullanicilar, out decimal abonelikGeliri);
                    (decimal protokolGeliri, decimal protokolGideri) = ProtokolLisanslariniIsle(veri, sirket);
                    decimal isletmeGideri = IsletmeGideriniIsle(sirket, durum) + protokolGideri;
                    decimal finansmanGideri = KredileriIsle(sirket, durum);
                    Degerle(sirket, durum);

                    ozet.UygulamaGeliri = uygulamaGeliri;
                    ozet.AbonelikGeliri = abonelikGeliri;
                    ozet.ProtokolGeliri = protokolGeliri;
                    ozet.IsletmeGideri = isletmeGideri;
                    ozet.FinansmanGideri = finansmanGideri;
                    ozet.AktifUygulamaKullanicisi = durum.Urunler
                        .Where(x => x.Aktif && x.UrunTuru != "isletim-sistemi")
                        .Sum(x => x.AktifKullaniciSayisi);
                    ozet.IsletimSistemiKullanicisi = durum.Urunler
                        .Where(x => x.Aktif && x.UrunTuru == "isletim-sistemi")
                        .Sum(x => x.AktifKullaniciSayisi);
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
        Dogrula();
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            HizmetArziniGuncelle();
            IReadOnlyList<V9HizmetGerceklesme> sonuclar = V9HizmetSonucDeposu.Getir();
            await TemelKilitAsync(async veri =>
            {
                foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
                {
                    SirketIsletimDurumu durum = Durum(veri, sirket.SirketKimligi);
                    V9SirketPazarOzeti ozet = SirketOzeti(sirket);
                    ozet.HizmetGeliri = sonuclar
                        .Where(x => x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase))
                        .Sum(x => x.ToplamOdeme);
                    decimal baslangic = _tickBaslangicKasasi.TryGetValue(sirket.SirketKimligi, out decimal kasa)
                        ? kasa
                        : sirket.Kasa;
                    ozet.NetKazanc = decimal.Round(sirket.Kasa - baslangic, 2);
                    Degerle(sirket, durum);
                    HaberUret(sirket, ozet, tickNumarasi);
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
        Dogrula();
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
                HashSet<string> gecerli = GecerliTahsisAnahtarlari(sirket, durum);
                Dictionary<string, int> temiz = istek.Tahsisler
                    .Where(x => gecerli.Contains(x.Key) && x.Value >= 0)
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
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
                    $"Kapasite tahsisi kaydedildi. Ayrılan {kapasite.AyrilmisKapasite:N0}; boş {kapasite.BosKapasite:N0}.",
                    Kopyala(kapasite));
            }, cancellationToken);
            if (sonuc.Basarili) { V9PazarDeposu.Guncelle(_pazar); await KaydetAsync(cancellationToken); }
            return sonuc;
        }
        finally { _kilit.Release(); }
    }

    public async Task<IslemSonucu> YatirimSatinAlAsync(
        string sirketKimligi,
        YatirimIstegi istek,
        CancellationToken cancellationToken)
    {
        Dogrula();
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            IslemSonucu sonuc = IslemSonucu.Hata("Şirket bulunamadı.");
            await TemelKilitAsync(async veri =>
            {
                SirketKaydi? sirket = SirketBul(sirketKimligi);
                if (sirket is null) return;
                string tur = istek.YatirimTuru?.Trim() ?? string.Empty;
                if (!Yatirimlar.TryGetValue(tur, out var paket))
                {
                    sonuc = IslemSonucu.Hata("Bilinmeyen yatırım türü.");
                    return;
                }
                SirketIsletimDurumu durum = Durum(veri, sirketKimligi);
                int seviye = Seviye(durum, tur);
                decimal maliyet = YatirimMaliyeti(tur, seviye);
                if (sirket.Kasa < maliyet)
                {
                    sonuc = IslemSonucu.Hata($"Yetersiz kasa. Gerekli {maliyet:N2} TL.");
                    return;
                }
                sirket.Kasa -= maliyet;
                durum.ToplamYatirimHarcamasi += maliyet;
                durum.YatirimSeviyeleri[tur] = seviye + 1;
                durum.TeknikBorc = Math.Max(0, durum.TeknikBorc - (tur == "arge" ? 1.2 : 0.15));
                V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirketKimligi);
                kapasite.ToplamFizikselKapasite = ToplamFizikselKapasite(durum);
                TahsisleriUygula(sirket, durum, kapasite);
                IslemEkle(durum, "v9-yatirim", $"{paket.Ad} seviye {seviye + 1} oldu.", -maliyet);
                await TemelKaydetAsync(cancellationToken);
                sonuc = IslemSonucu.Basari(
                    $"{paket.Ad} yükseltildi. Fiziksel kapasite {kapasite.ToplamFizikselKapasite:N0}.",
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
        Dogrula();
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
                decimal limit = Math.Clamp(60_000m + Math.Max(0, sirket.Kasa) * 0.60m + Math.Max(0, durum.SirketDegeri) * 0.10m, 60_000m, 400_000m);
                if (mevcut + tutar > limit)
                {
                    sonuc = IslemSonucu.Hata($"Kredi limiti {limit:N2} TL; kullanılabilir {Math.Max(0, limit - mevcut):N2} TL.");
                    return;
                }
                const int taksit = 20;
                const decimal toplamFaiz = 0.08m;
                decimal toplam = decimal.Round(tutar * (1 + toplamFaiz), 2);
                durum.Krediler.Add(new KrediKaydi
                {
                    KrediKimligi = $"v9-kredi-{Guid.NewGuid():N}",
                    KrediTuru = string.IsNullOrWhiteSpace(istek.KrediTuru) ? "isletme" : istek.KrediTuru.Trim(),
                    AnaPara = tutar,
                    KalanBorc = toplam,
                    TickFaizOrani = toplamFaiz / taksit,
                    TaksitTutari = decimal.Round(toplam / taksit, 2),
                    KalanTaksit = taksit,
                    SonrakiOdemeTicki = _tick + 5,
                    OdemeAraligiTick = 5
                });
                sirket.Kasa += tutar;
                IslemEkle(durum, "v9-kredi", $"{tutar:N2} TL sabit toplam faizli kredi kullanıldı.", tutar);
                await TemelKaydetAsync(cancellationToken);
                sonuc = IslemSonucu.Basari("Kredi kasaya aktarıldı. Gecikmede borç büyümez; otomatik kredi yoktur.");
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
        Dogrula();
        await _kilit.WaitAsync(cancellationToken);
        try
        {
            IslemSonucu sonuc = IslemSonucu.Hata("Ürün bulunamadı.");
            await TemelKilitAsync(async veri =>
            {
                SirketIsletimDurumu durum = Durum(veri, sirketKimligi);
                UrunKaydi? urun = durum.Urunler.FirstOrDefault(x => x.UrunKimligi.Equals(istek.UrunKimligi, StringComparison.OrdinalIgnoreCase));
                if (urun is null) return;
                decimal istenen = urun.FiyatlandirmaModeli == "kullanim" ? istek.KullanimBasinaUcret : istek.AbonelikUcreti;
                decimal fiyat = V9FiyatPolitikasi.Sinirla(urun.UrunTuru, urun.Kategori, istenen);
                if (urun.FiyatlandirmaModeli == "kullanim")
                {
                    urun.KullanimBasinaUcret = fiyat;
                    urun.AbonelikUcreti = 0;
                }
                else
                {
                    urun.AbonelikUcreti = fiyat;
                    urun.KullanimBasinaUcret = urun.FiyatlandirmaModeli == "freemium" ? Math.Min(fiyat / 20m, 10m) : 0;
                }
                urun.Aktif = istek.Aktif;
                IslemEkle(durum, "v9-urun-fiyat", $"{urun.UrunAdi} fiyatı {fiyat:N2} TL oldu.", 0);
                await TemelKaydetAsync(cancellationToken);
                sonuc = IslemSonucu.Basari("Ürün fiyatı kategori sınırında kaydedildi.", new { fiyat, aralik = V9FiyatPolitikasi.Aralik(urun.UrunTuru, urun.Kategori) });
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
            IslemSonucu sonuc = IslemSonucu.Hata("Ürün bulunamadı.");
            await TemelKilitAsync(async veri =>
            {
                SirketKaydi? sirket = SirketBul(sirketKimligi);
                if (sirket is null) return;
                SirketIsletimDurumu durum = Durum(veri, sirketKimligi);
                UrunKaydi? urun = durum.Urunler.FirstOrDefault(x => x.UrunKimligi.Equals(urunKimligi, StringComparison.OrdinalIgnoreCase));
                if (urun is null) return;
                decimal hedef = urun.UrunTuru switch
                {
                    "isletim-sistemi" => 3_000m,
                    "platform" => 2_500m,
                    "altyapi" => 2_000m,
                    _ => 1_000m
                };
                decimal eskiKesinti = Math.Max(0, yayinOncesiKasa - sirket.Kasa);
                decimal fark = eskiKesinti - hedef;
                if (fark > 0) sirket.Kasa += fark;
                else if (fark < 0 && sirket.Kasa >= -fark) sirket.Kasa -= -fark;
                durum.ToplamYatirimHarcamasi = Math.Max(0, durum.ToplamYatirimHarcamasi + hedef - eskiKesinti);
                FiyatlariSinirla(durum);
                IslemEkle(durum, "v9-yayin", $"{urun.UrunAdi} yayın bedeli {hedef:N2} TL olarak dengelendi.", -hedef);
                await TemelKaydetAsync(cancellationToken);
                sonuc = IslemSonucu.Basari($"Yayın maliyeti {hedef:N2} TL olarak uygulandı.");
            }, cancellationToken);
            return sonuc;
        }
        finally { _kilit.Release(); }
    }

    public IReadOnlyList<object> YatirimMagazasi(string sirketKimligi)
    {
        SirketIsletimDurumu durum = Durum(Veri(), sirketKimligi);
        return Yatirimlar.Select(x =>
        {
            int seviye = Seviye(durum, x.Key);
            return (object)new
            {
                yatirimTuru = x.Key,
                ad = x.Value.Ad,
                aciklama = x.Value.Kapasite > 0
                    ? $"Fiziksel kapasiteye seviye başına yaklaşık {x.Value.Kapasite:N0} birim ekler."
                    : "Pazar ve organizasyon gücünü geliştirir.",
                seviye,
                sonrakiMaliyet = YatirimMaliyeti(x.Key, seviye),
                kapasiteKatkisi = x.Value.Kapasite
            };
        }).ToList().AsReadOnly();
    }

    public IReadOnlyList<object> KrediPaketleri() =>
    [
        new
        {
            krediTuru = "isletme",
            ad = "Sabit Faizli İşletme Kredisi",
            asgariTutar = 5_000m,
            azamiTutar = 250_000m,
            tickFaizOrani = 0.004m,
            taksitSayisi = 20,
            odemeAraligiTick = 5
        }
    ];

    public V9SirketKapasiteDurumu KapasiteOzeti(string sirketKimligi) => Kopyala(KapasiteDurumu(sirketKimligi));

    public object FiyatAraligi(UrunKaydi urun)
    {
        V9FiyatAraligi a = V9FiyatPolitikasi.Aralik(urun.UrunTuru, urun.Kategori);
        return new { a.Min, a.Max, a.Adim, a.Onerilen };
    }

    private void HizmetTalebiniGuncelle(long tick)
    {
        List<HizmetTanimi> hizmetler = _katalog.Hizmetler.Where(x => x.Aktif).ToList();
        if (hizmetler.Count == 0) return;
        int hedef = Math.Max(10_000, (int)Math.Round(_musteriler.AktifMusteriSayisi * 0.58));
        Dictionary<string, double> agirlik = new(StringComparer.OrdinalIgnoreCase);
        foreach (HizmetTanimi h in hizmetler)
        {
            string anahtar = $"{h.HizmetKimligi}@{h.HizmetSurumu}";
            V9TalepKaydi kayit = TalepKaydi(_pazar.HizmetTalepleri, anahtar, h.HizmetKimligi, "hizmet");
            TrendiGuncelle(kayit, tick, 0.012);
            agirlik[anahtar] = (0.7 + KaliciKarma(anahtar) % 160 / 100d) * kayit.TrendCarpani;
        }
        double toplamAgirlik = Math.Max(0.01, agirlik.Values.Sum());
        int dagitilan = 0;
        foreach (HizmetTanimi h in hizmetler)
        {
            string anahtar = $"{h.HizmetKimligi}@{h.HizmetSurumu}";
            V9TalepKaydi k = _pazar.HizmetTalepleri[anahtar];
            int ham = (int)Math.Round(hedef * agirlik[anahtar] / toplamAgirlik);
            int yeni = k.BuTickTalep <= 0 ? ham : (int)Math.Round(k.BuTickTalep * 0.78 + ham * 0.22);
            k.OncekiTalep = k.BuTickTalep;
            k.BuTickTalep = Math.Max(0, yeni);
            k.DegisimYuzdesi = k.OncekiTalep <= 0 ? 0 : (k.BuTickTalep - k.OncekiTalep) * 100d / k.OncekiTalep;
            dagitilan += k.BuTickTalep;
        }
        if (dagitilan < hedef)
        {
            string ilk = $"{hizmetler[0].HizmetKimligi}@{hizmetler[0].HizmetSurumu}";
            _pazar.HizmetTalepleri[ilk].BuTickTalep += hedef - dagitilan;
        }
    }

    private void TumKategoriKayitlariniHazirla()
    {
        foreach (var kategori in StandartKatalogV6.UygulamaKategorileri)
            _ = TalepKaydi(_pazar.UygulamaTalepleri, kategori.KategoriKimligi, kategori.KategoriAdi, "uygulama-kategorisi");
    }

    private List<(SirketKaydi Sirket, SirketIsletimDurumu Durum, UrunKaydi Urun)> PazarUrunleri(SirketIsletimDosyasi veri) =>
        _sirketler.SirketKayitlari.Where(x => x.BagliMi).SelectMany(sirket =>
        {
            SirketIsletimDurumu durum = Durum(veri, sirket.SirketKimligi);
            return durum.Urunler.Where(x => x.Aktif && x.KullaniciKapasitesi > 0 && !string.IsNullOrWhiteSpace(x.UygulamaKimligi))
                .Select(urun => (sirket, durum, urun));
        }).ToList();

    private void CevrimdisiUrunleriBosalt(SirketIsletimDosyasi veri)
    {
        HashSet<string> bagli = _sirketler.SirketKayitlari.Where(x => x.BagliMi).Select(x => x.SirketKimligi)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (SirketIsletimDurumu durum in veri.Sirketler.Where(x => !bagli.Contains(x.SirketKimligi)))
        {
            foreach (UrunKaydi urun in durum.Urunler)
            {
                int onceki = urun.AktifKullaniciSayisi;
                urun.AktifKullaniciSayisi = 0;
                urun.ToplamKaybedilenKullanici += onceki;
            }
            durum.ToplamAboneSayisi = 0;
        }
    }

    private void IsletimSistemleriniDagit(
        IReadOnlyList<(SirketKaydi Sirket, SirketIsletimDurumu Durum, UrunKaydi Urun)> urunler,
        long tick)
    {
        List<(SirketKaydi Sirket, UrunKaydi Urun)> sistemler = urunler
            .Where(x => x.Urun.UrunTuru == "isletim-sistemi")
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
                (tick - musteri.SonIsletimSistemiDegisimTicki < 10 || _rastgele.NextDouble() > 0.02))
                secim = mevcut;
            else
                secim = UrunSec(sistemler, sayac, musteri);

            if (secim is null)
            {
                musteri.IsletimSistemiKimligi = "isletim-sistemi-bekleniyor";
                musteri.IsletimSistemiSurumu = string.Empty;
                musteri.IsletimSistemiSirketKimligi = string.Empty;
                continue;
            }
            var s = secim.Value;
            if (!musteri.IsletimSistemiKimligi.Equals(s.Urun.UygulamaKimligi, StringComparison.OrdinalIgnoreCase))
            {
                musteri.IsletimSistemiDegisimSayisi++;
                musteri.SonIsletimSistemiDegisimTicki = tick;
                musteri.IsletimSistemiMemnuniyeti = 55;
            }
            musteri.IsletimSistemiKimligi = s.Urun.UygulamaKimligi;
            musteri.IsletimSistemiSurumu = "1.0";
            musteri.IsletimSistemiSirketKimligi = s.Sirket.SirketKimligi;
            sayac[s.Urun.UygulamaKimligi]++;
        }
        foreach ((SirketKaydi _, UrunKaydi urun) in sistemler) urun.AktifKullaniciSayisi = sayac[urun.UygulamaKimligi];
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
        GecmiseEkle(talep, tick);
    }

    private void UygulamalariDagit(
        IReadOnlyList<(SirketKaydi Sirket, SirketIsletimDurumu Durum, UrunKaydi Urun)> urunler,
        long tick)
    {
        List<(SirketKaydi Sirket, UrunKaydi Urun)> uygulamalar = urunler
            .Where(x => x.Urun.UrunTuru != "isletim-sistemi")
            .Select(x => (x.Sirket, x.Urun)).ToList();
        Dictionary<string, int> sayac = uygulamalar.ToDictionary(x => x.Urun.UygulamaKimligi, _ => 0, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<(SirketKaydi Sirket, UrunKaydi Urun)>> kategoriler = uygulamalar
            .GroupBy(x => x.Urun.Kategori, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        HashSet<string> aktifKimlikler = uygulamalar.Select(x => x.Urun.UygulamaKimligi)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (Musteri musteri in _musteriler.Musteriler.Where(x => x.Aktif))
        {
            musteri.KullandigiUygulamalar ??= [];
            musteri.KullandigiUygulamalar.RemoveAll(x => !aktifKimlikler.Contains(x));
            foreach (List<(SirketKaydi Sirket, UrunKaydi Urun)> adaylar in kategoriler.Values)
            {
                (SirketKaydi Sirket, UrunKaydi Urun)? mevcut = adaylar
                    .Where(x => musteri.KullandigiUygulamalar.Contains(x.Urun.UygulamaKimligi, StringComparer.OrdinalIgnoreCase))
                    .Select(x => ((SirketKaydi Sirket, UrunKaydi Urun)?)x).FirstOrDefault();
                bool degistir = mevcut is null ||
                    sayac[mevcut.Value.Urun.UygulamaKimligi] >= mevcut.Value.Urun.KullaniciKapasitesi ||
                    (tick - musteri.SonUygulamaDegisimTicki >= 8 && _rastgele.NextDouble() < 0.015);
                (SirketKaydi Sirket, UrunKaydi Urun)? secim = degistir ? UrunSec(adaylar, sayac, musteri) : mevcut;
                if (secim is null) continue;
                var s = secim.Value;
                musteri.KullandigiUygulamalar.RemoveAll(x => adaylar.Any(a => a.Urun.UygulamaKimligi.Equals(x, StringComparison.OrdinalIgnoreCase)));
                musteri.KullandigiUygulamalar.Add(s.Urun.UygulamaKimligi);
                sayac[s.Urun.UygulamaKimligi]++;
                musteri.UygulamaKullanimSayilari.TryGetValue(s.Urun.UygulamaKimligi, out int kullanim);
                musteri.UygulamaKullanimSayilari[s.Urun.UygulamaKimligi] = kullanim + 1;
                if (degistir)
                {
                    musteri.ToplamUygulamaDegisimSayisi++;
                    musteri.SonUygulamaDegisimTicki = tick;
                    musteri.UygulamaMemnuniyetleri[s.Urun.UygulamaKimligi] = 55;
                }
            }
        }
        foreach ((SirketKaydi _, UrunKaydi urun) in uygulamalar) urun.AktifKullaniciSayisi = sayac[urun.UygulamaKimligi];
    }

    private void UygulamaTalebiniGuncelle(
        IReadOnlyList<(SirketKaydi Sirket, SirketIsletimDurumu Durum, UrunKaydi Urun)> urunler,
        long tick)
    {
        Dictionary<string, List<(SirketKaydi Sirket, UrunKaydi Urun)>> arz = urunler
            .Where(x => x.Urun.UrunTuru != "isletim-sistemi")
            .GroupBy(x => x.Urun.Kategori, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Select(y => (y.Sirket, y.Urun)).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var kategori in StandartKatalogV6.UygulamaKategorileri)
        {
            V9TalepKaydi kayit = TalepKaydi(_pazar.UygulamaTalepleri, kategori.KategoriKimligi, kategori.KategoriAdi, "uygulama-kategorisi");
            TrendiGuncelle(kayit, tick, 0.018);
            int ham = (int)Math.Round(_musteriler.AktifMusteriSayisi * kayit.TrendCarpani);
            int yeni = kayit.BuTickTalep <= 0 ? ham : (int)Math.Round(kayit.BuTickTalep * 0.82 + ham * 0.18);
            kayit.OncekiTalep = kayit.BuTickTalep;
            kayit.BuTickTalep = Math.Max(0, yeni);
            List<(SirketKaydi Sirket, UrunKaydi Urun)> adaylar = arz.TryGetValue(kategori.KategoriKimligi, out var liste) ? liste : [];
            kayit.ArzKapasitesi = adaylar.Sum(x => x.Urun.KullaniciKapasitesi);
            kayit.KarsilananTalep = adaylar.Sum(x => x.Urun.AktifKullaniciSayisi);
            kayit.DegisimYuzdesi = kayit.OncekiTalep <= 0 ? 0 : (kayit.BuTickTalep - kayit.OncekiTalep) * 100d / kayit.OncekiTalep;
            kayit.ArzDagilimi = adaylar.Select(x => new V9ArzPayi
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
            GecmiseEkle(kayit, tick);
        }
    }

    private decimal UrunGelirleriniIsle(
        SirketKaydi sirket,
        SirketIsletimDurumu durum,
        IReadOnlyDictionary<string, int> oncekiKullanicilar,
        out decimal abonelikGeliri)
    {
        decimal toplam = 0;
        abonelikGeliri = 0;
        foreach (UrunKaydi urun in durum.Urunler.Where(x => x.Aktif))
        {
            int onceki = oncekiKullanicilar.TryGetValue(urun.UrunKimligi, out int x) ? x : 0;
            int yeni = Math.Max(0, urun.AktifKullaniciSayisi - onceki);
            int kayip = Math.Max(0, onceki - urun.AktifKullaniciSayisi);
            decimal gelir = TickUrunGeliri(urun, yeni);
            sirket.Kasa += gelir;
            sirket.ToplamGelir += gelir;
            urun.ToplamGelir += gelir;
            durum.ToplamUrunGeliri += gelir;
            urun.ToplamEdinilenKullanici += yeni;
            urun.ToplamKaybedilenKullanici += kayip;
            toplam += gelir;
            if (urun.FiyatlandirmaModeli is "abonelik" or "freemium") abonelikGeliri += gelir;
        }
        durum.ToplamAbonelikGeliri += abonelikGeliri;
        durum.ToplamAboneSayisi = durum.Urunler.Where(x => x.Aktif).Sum(x => x.AktifKullaniciSayisi);
        return decimal.Round(toplam, 2);
    }

    private (decimal Gelir, decimal Gider) ProtokolLisanslariniIsle(SirketIsletimDosyasi veri, SirketKaydi sirket)
    {
        decimal gelir = 0;
        decimal gider = 0;
        foreach (OzelProtokolKaydi protokol in veri.Protokoller.Where(x => x.Aktif))
        {
            decimal bedel = Math.Clamp(protokol.TickLisansBedeli, 0, 100m);
            if (bedel <= 0) continue;
            if (protokol.SahipSirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase))
            {
                foreach (string kullananKimligi in protokol.BenimseyenSirketler.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    SirketKaydi? kullanan = SirketBul(kullananKimligi);
                    if (kullanan is null || !kullanan.BagliMi || kullanan.Kasa <= 0) continue;
                    decimal odeme = Math.Min(kullanan.Kasa, bedel);
                    kullanan.Kasa -= odeme;
                    Durum(veri, kullananKimligi).ToplamIsletmeGideri += odeme;
                    sirket.Kasa += odeme;
                    sirket.ToplamGelir += odeme;
                    protokol.ToplamLisansGeliri += odeme;
                    gelir += odeme;
                }
            }
            else if (protokol.BenimseyenSirketler.Contains(sirket.SirketKimligi, StringComparer.OrdinalIgnoreCase))
                gider += Math.Min(sirket.Kasa, bedel);
        }
        return (decimal.Round(gelir, 2), decimal.Round(gider, 2));
    }

    private decimal IsletmeGideriniIsle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirket.SirketKimligi);
        int aktifHizmet = sirket.Hizmetler.Count(x => x.Aktif);
        int aktifUrun = durum.Urunler.Count(x => x.Aktif);
        int kullanici = durum.Urunler.Where(x => x.Aktif).Sum(x => x.AktifKullaniciSayisi);
        decimal gider =
            20m +
            aktifHizmet * 0.50m +
            aktifUrun * 6m +
            kapasite.AyrilmisKapasite * 0.0015m +
            kapasite.KullanilanKapasite * 0.008m +
            kullanici * 0.0008m +
            durum.YatirimSeviyeleri.Sum(x => YatirimBakimi(x.Key, x.Value));
        gider = decimal.Round(Math.Clamp(gider, 15m, 10_000m), 2);
        decimal odenen = Math.Min(Math.Max(0, sirket.Kasa), gider);
        sirket.Kasa -= odenen;
        durum.ToplamIsletmeGideri += odenen;
        durum.OdenemeyenGider = decimal.Round(Math.Clamp(durum.OdenemeyenGider * 0.25m + (gider - odenen), 0, 20_000m), 2);
        if (gider > odenen)
        {
            sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - 0.01);
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
                durum.KrediNotu = Math.Max(300, durum.KrediNotu - 2);
                sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - 0.02);
            }
            kredi.SonrakiOdemeTicki = _tick + Math.Max(2, kredi.OdemeAraligiTick);
            if (kredi.KalanBorc <= 0 || kredi.KalanTaksit <= 0) kredi.Aktif = false;
        }
        return decimal.Round(toplam, 2);
    }

    private void HizmetArziniGuncelle()
    {
        IReadOnlyList<V9HizmetGerceklesme> sonuclar = V9HizmetSonucDeposu.Getir();
        foreach ((string anahtar, V9TalepKaydi talep) in _pazar.HizmetTalepleri)
        {
            string[] p = anahtar.Split('@', 2);
            string kimlik = p[0];
            string surum = p.Length > 1 ? p[1] : "1.0";
            List<(SirketKaydi Sirket, int Kapasite)> saglayicilar = _sirketler.SirketKayitlari
                .Where(x => x.BagliMi)
                .Select(s => (Sirket: s, Hizmet: s.Hizmetler.FirstOrDefault(h => h.Aktif && h.HizmetKimligi.Equals(kimlik, StringComparison.OrdinalIgnoreCase) && h.HizmetSurumu.Equals(surum, StringComparison.OrdinalIgnoreCase))))
                .Where(x => x.Hizmet is not null && x.Hizmet.AzamiEszamanliIs > 0)
                .Select(x => (x.Sirket, x.Hizmet!.AzamiEszamanliIs * 20))
                .ToList();
            talep.ArzKapasitesi = saglayicilar.Sum(x => x.Kapasite);
            List<V9HizmetGerceklesme> hizmetSonuclari = sonuclar.Where(x => x.HizmetAnahtari.Equals(anahtar, StringComparison.OrdinalIgnoreCase)).ToList();
            int ornek = hizmetSonuclari.Sum(x => x.Toplam);
            double oran = ornek <= 0 ? 0 : hizmetSonuclari.Sum(x => x.Basarili + x.Basarisiz * 0.5) / ornek;
            talep.KarsilananTalep = (int)Math.Round(Math.Min(talep.BuTickTalep, talep.ArzKapasitesi) * Math.Clamp(oran, 0, 1));
            double payTabani = Math.Max(0.0001, hizmetSonuclari.Sum(x => x.Basarili + x.Basarisiz * 0.5));
            talep.ArzDagilimi = saglayicilar.Select(x =>
            {
                V9HizmetGerceklesme? sonuc = hizmetSonuclari.FirstOrDefault(y => y.SirketKimligi.Equals(x.Sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase));
                double puan = (sonuc?.Basarili ?? 0) + (sonuc?.Basarisiz ?? 0) * 0.5;
                return new V9ArzPayi
                {
                    SirketKimligi = x.Sirket.SirketKimligi,
                    SirketAdi = x.Sirket.SirketAdi,
                    UrunVeyaHizmetKimligi = anahtar,
                    Ad = kimlik,
                    Kapasite = x.Kapasite,
                    Karsilanan = (int)Math.Round(talep.KarsilananTalep * puan / payTabani),
                    PazarPayi = puan * 100d / payTabani,
                    Fiyat = MotorHizmetFiyatlari.Fiyat(kimlik, surum)
                };
            }).OrderByDescending(x => x.Karsilanan).ToList();
            GecmiseEkle(talep, _tick);
        }
    }

    private void TahsisleriUygula(SirketKaydi sirket, SirketIsletimDurumu durum, V9SirketKapasiteDurumu kapasite)
    {
        HashSet<string> gecerli = GecerliTahsisAnahtarlari(sirket, durum);
        kapasite.Tahsisler = kapasite.Tahsisler.Where(x => gecerli.Contains(x.Key) && x.Value >= 0)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        if (!kapasite.KullaniciElleAyarladi || kapasite.Tahsisler.Count == 0)
            kapasite.Tahsisler = OtomatikTahsis(sirket, durum, kapasite.ToplamFizikselKapasite);
        Olcekle(kapasite.Tahsisler, kapasite.ToplamFizikselKapasite);

        foreach (SunulanHizmet hizmet in sirket.Hizmetler)
        {
            string anahtar = $"hizmet:{hizmet.HizmetKimligi}@{hizmet.HizmetSurumu}";
            int tahsis = kapasite.Tahsisler.TryGetValue(anahtar, out int x) ? x : 0;
            hizmet.AzamiEszamanliIs = hizmet.Aktif && tahsis >= 10 ? tahsis / 10 : 0;
        }
        foreach (UrunKaydi urun in durum.Urunler)
        {
            string anahtar = $"urun:{urun.UrunKimligi}";
            int tahsis = kapasite.Tahsisler.TryGetValue(anahtar, out int x) ? x : 0;
            urun.TabanKullaniciKapasitesi = 0;
            urun.SatinAlinanKullaniciKapasitesi = 0;
            urun.AltyapiKapasiteBonusu = 0;
            urun.KullaniciKapasitesi = urun.Aktif ? tahsis * 40 : 0;
            if (urun.AktifKullaniciSayisi > urun.KullaniciKapasitesi) urun.AktifKullaniciSayisi = urun.KullaniciKapasitesi;
        }
        kapasite.AyrilmisKapasite = kapasite.Tahsisler.Values.Sum();
        KapasiteKullaniminiGuncelle(sirket, durum, kapasite);
    }

    private static HashSet<string> GecerliTahsisAnahtarlari(SirketKaydi sirket, SirketIsletimDurumu durum) =>
        sirket.Hizmetler.Where(x => x.Aktif).Select(x => $"hizmet:{x.HizmetKimligi}@{x.HizmetSurumu}")
            .Concat(durum.Urunler.Where(x => x.Aktif).Select(x => $"urun:{x.UrunKimligi}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, int> OtomatikTahsis(SirketKaydi sirket, SirketIsletimDurumu durum, int toplam)
    {
        List<string> hizmetler = sirket.Hizmetler.Where(x => x.Aktif).Select(x => $"hizmet:{x.HizmetKimligi}@{x.HizmetSurumu}").ToList();
        List<UrunKaydi> urunler = durum.Urunler.Where(x => x.Aktif).ToList();
        Dictionary<string, int> sonuc = new(StringComparer.OrdinalIgnoreCase);
        if (hizmetler.Count + urunler.Count == 0) return sonuc;
        int hizmetHavuzu = urunler.Count == 0 ? toplam : (int)Math.Round(toplam * 0.35);
        int urunHavuzu = toplam - hizmetHavuzu;
        if (hizmetler.Count > 0)
        {
            int pay = hizmetHavuzu / hizmetler.Count;
            foreach (string h in hizmetler) sonuc[h] = Math.Max(0, pay);
        }
        if (urunler.Count > 0)
        {
            int agirlikToplam = urunler.Sum(x => x.UrunTuru == "isletim-sistemi" ? 4 : 2);
            foreach (UrunKaydi u in urunler)
            {
                int a = u.UrunTuru == "isletim-sistemi" ? 4 : 2;
                sonuc[$"urun:{u.UrunKimligi}"] = Math.Max(0, urunHavuzu * a / Math.Max(1, agirlikToplam));
            }
        }
        Olcekle(sonuc, toplam);
        return sonuc;
    }

    private static void Olcekle(Dictionary<string, int> tahsisler, int tavan)
    {
        int toplam = tahsisler.Values.Sum();
        if (toplam <= tavan || toplam <= 0) return;
        double oran = tavan / (double)toplam;
        foreach (string anahtar in tahsisler.Keys.ToList()) tahsisler[anahtar] = Math.Max(0, (int)Math.Floor(tahsisler[anahtar] * oran));
    }

    private static void KapasiteKullaniminiGuncelle(SirketKaydi sirket, SirketIsletimDurumu durum, V9SirketKapasiteDurumu kapasite)
    {
        kapasite.KullanilanKapasite = sirket.BagliMi
            ? sirket.AktifIsSayisi * 10 + durum.Urunler.Where(x => x.Aktif).Sum(x => (int)Math.Ceiling(x.AktifKullaniciSayisi / 40d))
            : 0;
    }

    private (SirketKaydi Sirket, UrunKaydi Urun)? UrunSec(
        IReadOnlyList<(SirketKaydi Sirket, UrunKaydi Urun)> adaylar,
        IReadOnlyDictionary<string, int> sayac,
        Musteri musteri)
    {
        List<((SirketKaydi Sirket, UrunKaydi Urun) Aday, double Agirlik)> uygun = [];
        foreach (var aday in adaylar)
        {
            sayac.TryGetValue(aday.Urun.UygulamaKimligi, out int kullanim);
            if (kullanim >= aday.Urun.KullaniciKapasitesi) continue;
            double bosluk = Math.Clamp(1 - kullanim / (double)Math.Max(1, aday.Urun.KullaniciKapasitesi), 0.03, 1);
            double kalite = Math.Clamp((aday.Sirket.KodKalitesiPuani + aday.Sirket.PerformansPuani + aday.Sirket.GuvenlikPuani + aday.Urun.UrunKalitesi) / 400d, 0.05, 1);
            double fiyat = 1d / (1 + (double)UrunFiyati(aday.Urun) / Math.Max(10d, (double)musteri.TickBasinaHarcamaButcesi));
            uygun.Add((aday, Math.Max(0.001, kalite * fiyat * Math.Sqrt(bosluk))));
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

    private int ToplamFizikselKapasite(SirketIsletimDurumu durum)
    {
        int toplam = 2_500;
        foreach ((string tur, int seviye) in durum.YatirimSeviyeleri)
            if (Yatirimlar.TryGetValue(tur, out var paket) && paket.Kapasite > 0)
                toplam += (int)Math.Round(paket.Kapasite * Math.Pow(Math.Max(0, seviye), 1.06));
        return Math.Clamp(toplam, 500, 500_000);
    }

    private static decimal TickUrunGeliri(UrunKaydi urun, int yeni) => decimal.Round(Math.Max(0, urun.FiyatlandirmaModeli switch
    {
        "abonelik" => urun.AktifKullaniciSayisi * UrunFiyati(urun) / 30m,
        "freemium" => urun.AktifKullaniciSayisi * UrunFiyati(urun) * 0.20m / 30m + urun.AktifKullaniciSayisi * urun.KullanimBasinaUcret * 0.05m,
        "kullanim" => urun.AktifKullaniciSayisi * UrunFiyati(urun) * 0.12m,
        "lisans" or "tek-seferlik" => yeni * UrunFiyati(urun),
        _ => 0
    }), 2);

    private static decimal UrunFiyati(UrunKaydi urun) => urun.FiyatlandirmaModeli == "kullanim" ? urun.KullanimBasinaUcret : urun.AbonelikUcreti;

    private static decimal YatirimMaliyeti(string tur, int seviye) =>
        Yatirimlar.TryGetValue(tur, out var paket)
            ? decimal.Round(paket.Taban * (decimal)Math.Pow(1.32, Math.Max(0, seviye)), 2)
            : decimal.MaxValue;

    private static decimal YatirimBakimi(string tur, int seviye) => seviye <= 0 ? 0 : tur switch
    {
        "cpu" => seviye * 12m,
        "ram" => seviye * 10m,
        "ag" => seviye * 11m,
        "depolama" => seviye * 8m,
        "guvenlik" => seviye * 14m,
        "yedek" => seviye * 16m,
        "destek" => seviye * 10m,
        "pazarlama" => seviye * 12m,
        "satis" => seviye * 13m,
        "arge" => seviye * 15m,
        _ => 0
    };

    private static int Seviye(SirketIsletimDurumu durum, string tur) => durum.YatirimSeviyeleri.TryGetValue(tur, out int s) ? Math.Max(0, s) : 0;

    private static void EskiKapasiteleriTemizle(SirketIsletimDurumu durum)
    {
        foreach (HizmetKaliciAyari ayar in durum.HizmetAyarlari.Values) ayar.SatinAlinanKapasite = 0;
        foreach (UrunKaydi urun in durum.Urunler)
        {
            urun.SatinAlinanKullaniciKapasitesi = 0;
            urun.AltyapiKapasiteBonusu = 0;
        }
    }

    private static void FiyatlariSinirla(SirketIsletimDurumu durum)
    {
        foreach (UrunKaydi urun in durum.Urunler)
        {
            decimal mevcut = UrunFiyati(urun);
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

    private void TrendiGuncelle(V9TalepKaydi kayit, long tick, double olasilik)
    {
        if (kayit.TrendBitisTicki > 0 && tick > kayit.TrendBitisTicki)
        {
            kayit.TrendBitisTicki = 0;
            kayit.AktifTrend = "normal";
        }
        if (kayit.TrendBitisTicki == 0 && _rastgele.NextDouble() < olasilik)
        {
            bool yukari = _rastgele.NextDouble() < 0.62;
            kayit.TrendCarpani = yukari ? 1.15 + _rastgele.NextDouble() * 0.45 : 0.72 + _rastgele.NextDouble() * 0.18;
            kayit.AktifTrend = yukari ? "yukselen" : "dusen";
            kayit.TrendBitisTicki = tick + _rastgele.Next(6, 16);
        }
        else if (kayit.TrendBitisTicki == 0)
        {
            kayit.TrendCarpani += (1 - kayit.TrendCarpani) * 0.10 + (_rastgele.NextDouble() - 0.5) * 0.018;
            kayit.TrendCarpani = Math.Clamp(kayit.TrendCarpani, 0.85, 1.15);
        }
    }

    private static void GecmiseEkle(V9TalepKaydi kayit, long tick)
    {
        if (kayit.Gecmis.LastOrDefault()?.TickNumarasi == tick) kayit.Gecmis.RemoveAt(kayit.Gecmis.Count - 1);
        kayit.Gecmis.Add(new V9TalepNoktasi { TickNumarasi = tick, Talep = kayit.BuTickTalep, Karsilanan = kayit.KarsilananTalep, Arz = kayit.ArzKapasitesi, TrendCarpani = kayit.TrendCarpani });
        if (kayit.Gecmis.Count > 120) kayit.Gecmis.RemoveRange(0, kayit.Gecmis.Count - 120);
    }

    private static uint KaliciKarma(string metin)
    {
        uint karma = 2166136261;
        foreach (char c in metin) { karma ^= c; karma *= 16777619; }
        return karma;
    }

    private V9TalepKaydi TalepKaydi(Dictionary<string, V9TalepKaydi> kaynak, string anahtar, string ad, string tur)
    {
        if (kaynak.TryGetValue(anahtar, out V9TalepKaydi? kayit)) return kayit;
        kayit = new V9TalepKaydi { Anahtar = anahtar, Ad = ad, Tur = tur, TrendCarpani = 1 };
        kaynak[anahtar] = kayit;
        return kayit;
    }

    private V9SirketKapasiteDurumu KapasiteDurumu(string sirketKimligi)
    {
        if (_pazar.SirketKapasiteleri.TryGetValue(sirketKimligi, out V9SirketKapasiteDurumu? k)) return k;
        k = new V9SirketKapasiteDurumu { SirketKimligi = sirketKimligi };
        _pazar.SirketKapasiteleri[sirketKimligi] = k;
        return k;
    }

    private V9SirketPazarOzeti SirketOzeti(SirketKaydi sirket)
    {
        V9SirketPazarOzeti? o = _pazar.SirketOzetleri.FirstOrDefault(x => x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase));
        if (o is not null) return o;
        o = new V9SirketPazarOzeti { SirketKimligi = sirket.SirketKimligi, SirketAdi = sirket.SirketAdi };
        _pazar.SirketOzetleri.Add(o);
        return o;
    }

    private static void OzetSifirla(V9SirketPazarOzeti o)
    {
        o.HizmetGeliri = 0;
        o.UygulamaGeliri = 0;
        o.AbonelikGeliri = 0;
        o.ProtokolGeliri = 0;
        o.IsletmeGideri = 0;
        o.FinansmanGideri = 0;
        o.NetKazanc = 0;
        o.AktifUygulamaKullanicisi = 0;
        o.IsletimSistemiKullanicisi = 0;
    }

    private void HaberUret(SirketKaydi sirket, V9SirketPazarOzeti ozet, long tick)
    {
        if (!sirket.BagliMi) return;
        if (Math.Abs(ozet.NetKazanc) < 5_000 && tick % 10 != 0) return;
        bool olumlu = ozet.NetKazanc >= 0;
        _pazar.Haberler.Add(new V9HaberKaydi
        {
            HaberKimligi = $"v9-haber-{Guid.NewGuid():N}",
            TickNumarasi = tick,
            Baslik = olumlu ? $"{sirket.SirketAdi} büyüme açıkladı" : $"{sirket.SirketAdi} maliyet baskısı yaşıyor",
            Aciklama = $"Son tick net sonucu {ozet.NetKazanc:N2} TL; uygulama geliri {ozet.UygulamaGeliri:N2} TL, hizmet geliri {ozet.HizmetGeliri:N2} TL.",
            Tur = olumlu ? "finans-olumlu" : "finans-olumsuz",
            Onem = Math.Abs(ozet.NetKazanc) >= 20_000 ? "kritik" : "normal",
            SirketKimligi = sirket.SirketKimligi,
            SirketAdi = sirket.SirketAdi,
            FinansalEtki = ozet.NetKazanc
        });
        if (_pazar.Haberler.Count > 250) _pazar.Haberler = _pazar.Haberler.TakeLast(250).ToList();
    }

    private void Degerle(SirketKaydi sirket, SirketIsletimDurumu durum)
    {
        decimal borc = durum.Krediler.Where(x => x.Aktif).Sum(x => x.KalanBorc);
        V9SirketKapasiteDurumu kapasite = KapasiteDurumu(sirket.SirketKimligi);
        decimal duzenli = durum.Urunler.Where(x => x.Aktif).Sum(x => TickUrunGeliri(x, 0));
        decimal kalite = (decimal)(sirket.KodKalitesiPuani + sirket.PerformansPuani + sirket.GuvenlikPuani + sirket.ItibarPuani + sirket.GuvenilirlikPuani + sirket.OrtalamaMusteriMemnuniyeti) * 150m;
        decimal deger = Math.Max(0, sirket.Kasa) * 0.35m + duzenli * 24m + durum.ToplamAboneSayisi * 30m + kapasite.ToplamFizikselKapasite * 4m + kalite + durum.YatirimSeviyeleri.Values.Sum() * 2_500m - borc - durum.OdenemeyenGider * 1.2m - (decimal)(durum.OperasyonRiski + durum.TeknikBorc) * 100m;
        durum.SirketDegeri = decimal.Round(Math.Max(0, deger), 2);
        durum.TahminiHisseFiyati = decimal.Round(durum.SirketDegeri / 10_000m, 4);
    }

    private void NormalizeEt()
    {
        _pazar.Surum = 91;
        _pazar.HizmetTalepleri ??= new(StringComparer.OrdinalIgnoreCase);
        _pazar.UygulamaTalepleri ??= new(StringComparer.OrdinalIgnoreCase);
        _pazar.SirketKapasiteleri ??= new(StringComparer.OrdinalIgnoreCase);
        _pazar.SirketOzetleri ??= [];
        _pazar.Haberler ??= [];
        _pazar.IsletimSistemiTalebi ??= new V9TalepKaydi { Anahtar = "isletim-sistemi", Ad = "İşletim sistemi", Tur = "isletim-sistemi", TrendCarpani = 1 };
        foreach (V9TalepKaydi k in _pazar.HizmetTalepleri.Values.Concat(_pazar.UygulamaTalepleri.Values).Append(_pazar.IsletimSistemiTalebi))
        {
            k.Gecmis ??= [];
            k.ArzDagilimi ??= [];
            if (k.TrendCarpani <= 0) k.TrendCarpani = 1;
        }
        foreach (V9SirketKapasiteDurumu k in _pazar.SirketKapasiteleri.Values) k.Tahsisler ??= new(StringComparer.OrdinalIgnoreCase);
    }

    private SirketIsletimDosyasi Veri() => (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel) ?? new SirketIsletimDosyasi());

    private static SirketIsletimDurumu Durum(SirketIsletimDosyasi veri, string sirketKimligi)
    {
        SirketIsletimDurumu? d = veri.Sirketler.FirstOrDefault(x => x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
        if (d is not null) return d;
        d = new SirketIsletimDurumu { SirketKimligi = sirketKimligi };
        veri.Sirketler.Add(d);
        return d;
    }

    private SirketKaydi? SirketBul(string sirketKimligi) => _sirketler.SirketKayitlari.FirstOrDefault(x => x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));

    private async Task TemelKilitAsync(Func<SirketIsletimDosyasi, Task> islem, CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = (SemaphoreSlim)(_temelKilitAlani.GetValue(_temel) ?? throw new InvalidOperationException("V9.1 işletim kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try { await islem(Veri()); }
        finally { kilit.Release(); }
    }

    private async Task TemelKaydetAsync(CancellationToken cancellationToken)
    {
        object? sonuc = _temelKaydetMetodu.Invoke(_temel, [cancellationToken]);
        if (sonuc is Task gorev) await gorev;
    }

    private static V9SirketKapasiteDurumu Kopyala(V9SirketKapasiteDurumu k) => new()
    {
        SirketKimligi = k.SirketKimligi,
        ToplamFizikselKapasite = k.ToplamFizikselKapasite,
        AyrilmisKapasite = k.AyrilmisKapasite,
        KullanilanKapasite = k.KullanilanKapasite,
        KullaniciElleAyarladi = k.KullaniciElleAyarladi,
        Tahsisler = new Dictionary<string, int>(k.Tahsisler, StringComparer.OrdinalIgnoreCase)
    };

    private static void IslemEkle(SirketIsletimDurumu durum, string tur, string aciklama, decimal tutar)
    {
        durum.SonIslemler.Add(new IsletimIslemKaydi { IslemKimligi = $"v9-islem-{Guid.NewGuid():N}", TickNumarasi = V9PazarDeposu.Getir().SonTick, IslemTuru = tur, Aciklama = aciklama, Tutar = tutar });
        if (durum.SonIslemler.Count > 200) durum.SonIslemler = durum.SonIslemler.TakeLast(200).ToList();
    }

    private async Task KaydetAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        _pazar.GuncellenmeZamani = DateTimeOffset.UtcNow;
        string tmp = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(_pazar, JsonAyarlari), new UTF8Encoding(false), cancellationToken);
        File.Move(tmp, _dosyaYolu, overwrite: true);
    }

    private void Dogrula()
    {
        if (!_baslatildi) throw new InvalidOperationException("V9.1 ekonomi yöneticisi henüz başlatılmadı.");
    }

    public async ValueTask DisposeAsync()
    {
        if (!_baslatildi) { _kilit.Dispose(); return; }
        await _kilit.WaitAsync();
        try { await KaydetAsync(CancellationToken.None); }
        finally { _kilit.Release(); _kilit.Dispose(); }
    }
}
