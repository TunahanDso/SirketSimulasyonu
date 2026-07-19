using System.Text.Json;
using System.Text.Json.Nodes;
using SirketMotoru.Hizmetler;
using SirketMotoru.Isler;
using SirketMotoru.Kayit;

namespace SirketMotoru.Musteriler;

public sealed class MusteriYoneticisi : IAsyncDisposable
{
    private const int VarsayilanMusteriSayisi = 5_000;

    private readonly MusteriVeritabani _musteriVeritabani;
    private readonly HizmetKatalogu _hizmetKatalogu;
    private readonly Random _rastgele;
    private readonly SemaphoreSlim _kayitKilidi = new(1, 1);
    private readonly Dictionary<string, Musteri> _musteriIndeksi =
        new(StringComparer.OrdinalIgnoreCase);

    private long _sonKayitTicki;
    private bool _baslatildi;

    public IReadOnlyList<Musteri> Musteriler =>
        _musteriVeritabani.Musteriler;

    public int AktifMusteriSayisi =>
        Musteriler.Count(musteri => musteri.Aktif);

    public decimal ToplamMusteriBakiyesi =>
        Musteriler.Sum(musteri => musteri.Bakiye);

    public decimal ToplamMusteriHarcamasi =>
        Musteriler.Sum(musteri => musteri.ToplamHarcama);

    public MusteriYoneticisi(
        MusteriVeritabani musteriVeritabani,
        HizmetKatalogu hizmetKatalogu,
        int rastgeleTohum = 1881)
    {
        ArgumentNullException.ThrowIfNull(musteriVeritabani);
        ArgumentNullException.ThrowIfNull(hizmetKatalogu);
        _musteriVeritabani = musteriVeritabani;
        _hizmetKatalogu = hizmetKatalogu;
        _rastgele = new Random(rastgeleTohum);
    }

    public async Task BaslatAsync(
        CancellationToken cancellationToken)
    {
        if (_baslatildi)
        {
            return;
        }

        await _musteriVeritabani.YukleVeyaOlusturAsync(
            VarsayilanMusteriSayisi,
            cancellationToken);
        MusteriIndeksiniOlustur();
        MusterileriDogrula();
        _baslatildi = true;

        KonsolKayitcisi.Basari(
            $"Müşteri yöneticisi başlatıldı. " +
            $"Toplam müşteri: {Musteriler.Count} | " +
            $"Aktif müşteri: {AktifMusteriSayisi} | " +
            $"Toplam bakiye: {ToplamMusteriBakiyesi:N2}");
    }

    public Musteri? MusteriyiBul(string musteriKimligi)
    {
        if (string.IsNullOrWhiteSpace(musteriKimligi))
        {
            return null;
        }

        _musteriIndeksi.TryGetValue(
            musteriKimligi.Trim(),
            out Musteri? musteri);
        return musteri;
    }

    public IReadOnlyList<Musteri> TalepOlusturacakMusterileriSec(
        long tickNumarasi)
    {
        BaslatilmisOlmasiniDogrula();
        List<Musteri> secilenMusteriler = [];
        double tickEtkisi = TickTalepCarpaniHesapla(tickNumarasi);

        foreach (Musteri musteri in Musteriler)
        {
            if (!MusteriTalepOlusturabilirMi(musteri))
            {
                continue;
            }

            double sonOlasilik =
                Math.Clamp(
                    musteri.TalepOlusturmaOlasiligi * tickEtkisi,
                    0,
                    1);

            if (_rastgele.NextDouble() <= sonOlasilik)
            {
                secilenMusteriler.Add(musteri);
            }
        }

        return secilenMusteriler;
    }

    public IReadOnlyList<HizmetTalebi> TickTalepleriniOlustur(
        long tickNumarasi)
    {
        BaslatilmisOlmasiniDogrula();
        IReadOnlyList<Musteri> talepSahipleri =
            TalepOlusturacakMusterileriSec(tickNumarasi);
        List<HizmetTalebi> talepler =
            new(talepSahipleri.Count * 2);

        foreach (Musteri musteri in talepSahipleri)
        {
            int talepAdedi =
                _rastgele.NextDouble() < EkTalepOlasiligi(musteri.MusteriTuru)
                    ? 2
                    : 1;

            for (int sira = 0; sira < talepAdedi; sira++)
            {
                HizmetTalebi? talep =
                    MusteriIcinTalepOlustur(
                        musteri,
                        tickNumarasi);

                if (talep is not null)
                {
                    talepler.Add(talep);
                }
            }
        }

        int kotuNiyetliSayisi =
            talepler.Count(talep => talep.KotuNiyetli);

        KonsolKayitcisi.Bilgi(
            $"Tick {tickNumarasi} | " +
            $"Talep oluşturan müşteri: {talepSahipleri.Count} | " +
            $"Geçerli talep: {talepler.Count} | " +
            $"Şüpheli iş: {kotuNiyetliSayisi}");

        return talepler;
    }

    public HizmetTalebi? MusteriIcinTalepOlustur(
        Musteri musteri,
        long tickNumarasi)
    {
        ArgumentNullException.ThrowIfNull(musteri);
        BaslatilmisOlmasiniDogrula();

        if (!MusteriTalepOlusturabilirMi(musteri))
        {
            return null;
        }

        HizmetTanimi? hizmet = HizmetSec(musteri);

        if (hizmet is null)
        {
            return null;
        }

        decimal azamiButce = AzamiButceHesapla(musteri);

        if (azamiButce <= 0)
        {
            return null;
        }

        int zorlukSeviyesi = ZorlukSeviyesiSec(hizmet.HizmetKimligi);
        IstekSenaryosu senaryo =
            IstekVerisiOlustur(hizmet, zorlukSeviyesi);
        bool kotuNiyetli =
            KotuNiyetliIsMi(tickNumarasi, zorlukSeviyesi);
        string kotuNiyetTuru =
            kotuNiyetli
                ? KotuNiyetTuruSec(tickNumarasi)
                : string.Empty;
        string istekJson =
            kotuNiyetli
                ? GuvenlikSinamasiEkle(
                    senaryo.Json,
                    kotuNiyetTuru,
                    zorlukSeviyesi)
                : senaryo.Json;

        decimal olasiGuvenlikKaybi =
            kotuNiyetli
                ? decimal.Round(
                    Math.Clamp(
                        azamiButce * 0.35m +
                        zorlukSeviyesi * 25m,
                        40m,
                        2_500m),
                    2)
                : 0;

        return new HizmetTalebi
        {
            IsKimligi = YeniIsKimligi(tickNumarasi),
            MusteriKimligi = musteri.MusteriKimligi,
            HizmetKimligi = hizmet.HizmetKimligi,
            HizmetSurumu = hizmet.HizmetSurumu,
            OlusturulmaTicki = tickNumarasi,
            AzamiButce = azamiButce,
            IstekVerisiJson = istekJson,
            ZamanAsimiMs = hizmet.ZamanAsimiMs,
            ZorlukSeviyesi = zorlukSeviyesi,
            KotuNiyetli = kotuNiyetli,
            KotuNiyetTuru = kotuNiyetTuru,
            OlasiGuvenlikKaybi = olasiGuvenlikKaybi,
            Durum = IsDurumu.Olusturuldu,
            OlusturulmaZamani = DateTimeOffset.UtcNow
        };
    }

    public bool MusteridenOdemeAl(
        string musteriKimligi,
        decimal tutar)
    {
        Musteri? musteri = MusteriyiBul(musteriKimligi);

        if (musteri is null || !musteri.OdemeYapabilirMi(tutar))
        {
            return false;
        }

        musteri.OdemeYap(tutar);
        return true;
    }

    public void MusteriyeIadeYap(
        string musteriKimligi,
        decimal tutar)
    {
        if (tutar < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tutar),
                "İade tutarı negatif olamaz.");
        }

        Musteri? musteri = MusteriyiBul(musteriKimligi)
            ?? throw new InvalidOperationException(
                $"Müşteri bulunamadı: {musteriKimligi}");
        musteri.Bakiye += tutar;
        musteri.ToplamHarcama =
            Math.Max(0, musteri.ToplamHarcama - tutar);
    }

    public void IslemSonucunuKaydet(
        HizmetTalebi talep,
        bool basarili,
        decimal odenenTutar,
        double tamamlanmaSuresiMs,
        string sonucAciklamasi)
    {
        ArgumentNullException.ThrowIfNull(talep);
        Musteri? musteri = MusteriyiBul(talep.MusteriKimligi)
            ?? throw new InvalidOperationException(
                $"İşlem müşterisi bulunamadı: {talep.MusteriKimligi}");

        MusteriIslemKaydi islemKaydi =
            new()
            {
                IsKimligi = talep.IsKimligi,
                TickNumarasi = talep.OlusturulmaTicki,
                HizmetKimligi = talep.HizmetKimligi,
                HizmetSurumu = talep.HizmetSurumu,
                SirketKimligi = talep.SecilenSirketKimligi ?? string.Empty,
                OdenenTutar = basarili ? odenenTutar : 0,
                Basarili = basarili,
                TamamlanmaSuresiMs = Math.Max(0, tamamlanmaSuresiMs),
                OlusturulmaZamani = DateTimeOffset.UtcNow,
                SonucAciklamasi = sonucAciklamasi?.Trim() ?? string.Empty
            };

        musteri.IslemKaydet(islemKaydi);

        if (!talep.KotuNiyetli)
        {
            MusteriSadakatiniGuncelle(musteri, talep, basarili);
        }
    }

    public async Task GerekirseKaydetAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();
        const int kayitAraligiTick = 10;

        if (tickNumarasi - _sonKayitTicki < kayitAraligiTick)
        {
            return;
        }

        await KaydetAsync(cancellationToken);
        _sonKayitTicki = tickNumarasi;
    }

    public async Task KaydetAsync(
        CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();
        await _kayitKilidi.WaitAsync(cancellationToken);

        try
        {
            await _musteriVeritabani.KaydetAsync(cancellationToken);
            KonsolKayitcisi.Bilgi(
                $"{Musteriler.Count} müşteri kaydı disk üzerine yazıldı.");
        }
        finally
        {
            _kayitKilidi.Release();
        }
    }

    public void TickBasindaMusterileriGuncelle(long tickNumarasi)
    {
        BaslatilmisOlmasiniDogrula();

        foreach (Musteri musteri in Musteriler)
        {
            if (musteri.Aktif)
            {
                MusteriGeliriEkle(musteri, tickNumarasi);
            }
        }
    }

    private HizmetTanimi? HizmetSec(Musteri musteri)
    {
        List<(HizmetTanimi Hizmet, double Agirlik)> agirlikliHizmetler = [];

        foreach (HizmetTanimi hizmet in
                 _hizmetKatalogu.Hizmetler.Where(hizmet => hizmet.Aktif))
        {
            musteri.HizmetKullanimSayilari.TryGetValue(
                hizmet.HizmetKimligi,
                out int kullanimSayisi);
            double tekrarKullanimBonusu =
                Math.Min(2.25, 1.0 + kullanimSayisi * 0.04);
            double hizmetAgirligi =
                MusteriTuruneGoreHizmetAgirligi(
                    musteri.MusteriTuru,
                    hizmet.HizmetKimligi);
            agirlikliHizmetler.Add(
                (hizmet, Math.Max(0.01, hizmetAgirligi * tekrarKullanimBonusu)));
        }

        return AgirlikliSecim(agirlikliHizmetler);
    }

    private HizmetTanimi? AgirlikliSecim(
        IReadOnlyList<(HizmetTanimi Hizmet, double Agirlik)> hizmetler)
    {
        if (hizmetler.Count == 0)
        {
            return null;
        }

        double toplamAgirlik = hizmetler.Sum(oge => oge.Agirlik);

        if (toplamAgirlik <= 0)
        {
            return hizmetler[_rastgele.Next(hizmetler.Count)].Hizmet;
        }

        double secim = _rastgele.NextDouble() * toplamAgirlik;
        double birikenAgirlik = 0;

        foreach (var oge in hizmetler)
        {
            birikenAgirlik += oge.Agirlik;

            if (secim <= birikenAgirlik)
            {
                return oge.Hizmet;
            }
        }

        return hizmetler[^1].Hizmet;
    }

    private decimal AzamiButceHesapla(Musteri musteri)
    {
        decimal tickButcesi =
            Math.Min(musteri.TickBasinaHarcamaButcesi, musteri.Bakiye);

        if (tickButcesi <= 0)
        {
            return 0;
        }

        decimal butceCarpani =
            (decimal)(0.55 + _rastgele.NextDouble() * 0.45);
        return decimal.Round(
            Math.Max(0.01m, tickButcesi * butceCarpani),
            2);
    }

    private IstekSenaryosu IstekVerisiOlustur(
        HizmetTanimi hizmet,
        int zorluk)
    {
        string json =
            hizmet.HizmetKimligi.ToLowerInvariant() switch
            {
                "matematik.topla" => MatematikToplaIstegiOlustur(zorluk),
                "matematik.carp" => MatematikCarpIstegiOlustur(zorluk),
                "veri.ortalama-hesapla" => OrtalamaIstegiOlustur(zorluk),
                "metin.kelime-say" => KelimeSayIstegiOlustur(zorluk),
                "metin.karakter-say" => KarakterSayIstegiOlustur(zorluk),
                "veri.medyan-hesapla" => MedyanIstegiOlustur(zorluk),
                "veri.standart-sapma" => StandartSapmaIstegiOlustur(zorluk),
                "dizi.sirala" => SiralamaIstegiOlustur(zorluk),
                "matematik.asal-carpanlar" => AsalCarpanIstegiOlustur(zorluk),
                "metin.frekans-analizi" => FrekansAnaliziIstegiOlustur(zorluk),
                _ => "{}"
            };

        return new IstekSenaryosu(json);
    }

    private string MatematikToplaIstegiOlustur(int zorluk)
    {
        int[] sayilar = RastgeleTamSayiDizisi(4 + zorluk * 8, -10_000, 10_001);
        return JsonSerializer.Serialize(new { sayilar });
    }

    private string MatematikCarpIstegiOlustur(int zorluk)
    {
        int[] sayilar = RastgeleTamSayiDizisi(2 + zorluk, -9, 10);
        return JsonSerializer.Serialize(new { sayilar });
    }

    private string OrtalamaIstegiOlustur(int zorluk)
    {
        double[] sayilar = RastgeleOndalikDizisi(8 + zorluk * 20);
        return JsonSerializer.Serialize(new { sayilar });
    }

    private string KelimeSayIstegiOlustur(int zorluk)
    {
        return JsonSerializer.Serialize(
            new { metin = RastgeleMetin(10 + zorluk * 35) });
    }

    private string KarakterSayIstegiOlustur(int zorluk)
    {
        string metin =
            RastgeleMetin(8 + zorluk * 25) +
            (zorluk >= 4 ? " teknoloji 🚀 güvenlik 🔐" : string.Empty);
        return JsonSerializer.Serialize(new { metin });
    }

    private string MedyanIstegiOlustur(int zorluk)
    {
        int adet = 15 + zorluk * 45;
        double[] sayilar = RastgeleOndalikDizisi(adet);
        return JsonSerializer.Serialize(new { sayilar });
    }

    private string StandartSapmaIstegiOlustur(int zorluk)
    {
        int adet = 25 + zorluk * 70;
        double[] sayilar = RastgeleOndalikDizisi(adet);
        return JsonSerializer.Serialize(new { sayilar });
    }

    private string SiralamaIstegiOlustur(int zorluk)
    {
        int adet = 40 + zorluk * 160;
        int[] sayilar = RastgeleTamSayiDizisi(adet, -1_000_000, 1_000_001);
        string yon = _rastgele.Next(2) == 0 ? "artan" : "azalan";
        return JsonSerializer.Serialize(new { sayilar, yon });
    }

    private string AsalCarpanIstegiOlustur(int zorluk)
    {
        int[] asalHavuzu =
        [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 97, 193, 997, 5003, 10007];
        long sayi = 1;
        int carpanAdedi = 2 + zorluk;

        for (int i = 0; i < carpanAdedi; i++)
        {
            int ustSinir = Math.Min(
                asalHavuzu.Length,
                6 + zorluk * 2);
            int carpan = asalHavuzu[_rastgele.Next(ustSinir)];

            if (sayi > 2_000_000_000L / carpan)
            {
                break;
            }

            sayi *= carpan;
        }

        sayi = Math.Max(2, sayi);
        return JsonSerializer.Serialize(new { sayi });
    }

    private string FrekansAnaliziIstegiOlustur(int zorluk)
    {
        return JsonSerializer.Serialize(
            new { metin = RastgeleMetin(40 + zorluk * 180) });
    }

    private int[] RastgeleTamSayiDizisi(
        int adet,
        int alt,
        int ust)
    {
        return Enumerable.Range(0, adet)
            .Select(_ => _rastgele.Next(alt, ust))
            .ToArray();
    }

    private double[] RastgeleOndalikDizisi(int adet)
    {
        return Enumerable.Range(0, adet)
            .Select(
                _ => Math.Round(
                    _rastgele.NextDouble() * 20_000 - 10_000,
                    3))
            .ToArray();
    }

    private string RastgeleMetin(int kelimeSayisi)
    {
        string[] kelimeler =
        [
            "tunix", "motor", "musteri", "sirket", "hizmet",
            "yazilim", "sunucu", "ekonomi", "protokol", "guvenlik",
            "performans", "kalite", "veri", "sistem", "ag",
            "rekabet", "kapasite", "islem", "analiz", "teknoloji"
        ];

        return string.Join(
            " ",
            Enumerable.Range(0, kelimeSayisi)
                .Select(_ => kelimeler[_rastgele.Next(kelimeler.Length)]));
    }

    private string GuvenlikSinamasiEkle(
        string json,
        string saldiriTuru,
        int zorluk)
    {
        JsonObject kok =
            JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        kok["_guvenlikSinamasi"] =
            new JsonObject
            {
                ["etiket"] = "motor-saldiri-v1",
                ["tur"] = saldiriTuru,
                ["yogunluk"] = zorluk,
                ["sahteYetki"] = "yonetici",
                ["komut"] = "kaynaklari-tuket"
            };
        kok["_saldiriDolgusu"] =
            new string('X', 500 + zorluk * 1_500);
        return kok.ToJsonString();
    }

    private int ZorlukSeviyesiSec(string hizmetKimligi)
    {
        bool ileriHizmet =
            hizmetKimligi is
                "veri.medyan-hesapla" or
                "veri.standart-sapma" or
                "dizi.sirala" or
                "matematik.asal-carpanlar" or
                "metin.frekans-analizi";

        int taban = ileriHizmet ? 2 : 1;
        int ust = ileriHizmet ? 6 : 5;
        int zorluk = _rastgele.Next(taban, ust);

        if (_rastgele.NextDouble() < 0.08)
        {
            zorluk = 5;
        }

        return Math.Clamp(zorluk, 1, 5);
    }

    private bool KotuNiyetliIsMi(
        long tickNumarasi,
        int zorluk)
    {
        int dongu = (int)(Math.Abs(tickNumarasi) % 120);
        bool saldiriDalgasi =
            dongu is >= 58 and < 74 or >= 104 and < 113;
        double olasilik =
            saldiriDalgasi
                ? 0.10 + zorluk * 0.015
                : 0.008 + zorluk * 0.002;
        return _rastgele.NextDouble() < olasilik;
    }

    private string KotuNiyetTuruSec(long tickNumarasi)
    {
        string[] turler =
        [
            "kaynak-tuketimi",
            "buyuk-payload",
            "yetki-yukseltme-denemesi",
            "komut-enjeksiyonu",
            "tekrar-saldirisi"
        ];
        return turler[(int)(Math.Abs(tickNumarasi) % turler.Length)];
    }

    private void MusteriGeliriEkle(
        Musteri musteri,
        long tickNumarasi)
    {
        int gelirAraligi =
            musteri.MusteriTuru switch
            {
                MusteriTuru.Bireysel => 75,
                MusteriTuru.KucukIsletme => 400,
                MusteriTuru.OrtaOlcekliIsletme => 1_500,
                MusteriTuru.Kurumsal => 7_500,
                MusteriTuru.KamuKurumu => 15_000,
                _ => 75
            };
        int gelirPeriyodu =
            musteri.MusteriTuru switch
            {
                MusteriTuru.Bireysel => 20,
                MusteriTuru.KucukIsletme => 10,
                MusteriTuru.OrtaOlcekliIsletme => 8,
                MusteriTuru.Kurumsal => 5,
                MusteriTuru.KamuKurumu => 10,
                _ => 20
            };

        if (tickNumarasi <= 0 || tickNumarasi % gelirPeriyodu != 0)
        {
            return;
        }

        musteri.Bakiye +=
            _rastgele.Next(
                Math.Max(1, gelirAraligi / 2),
                gelirAraligi + 1);
    }

    private static bool MusteriTalepOlusturabilirMi(Musteri musteri)
    {
        return musteri.Aktif &&
               musteri.Bakiye > 0 &&
               musteri.TickBasinaHarcamaButcesi > 0 &&
               musteri.TalepOlusturmaOlasiligi > 0;
    }

    private static double TickTalepCarpaniHesapla(long tickNumarasi)
    {
        if (tickNumarasi <= 0)
        {
            return 1;
        }

        int pazarDongusu = (int)(tickNumarasi % 120);

        return pazarDongusu switch
        {
            < 20 => 0.90,
            < 40 => 1.20,
            < 58 => 1.50,
            < 74 => 1.80,
            < 94 => 1.25,
            < 104 => 1.05,
            < 113 => 1.55,
            _ => 0.85
        };
    }

    private static double EkTalepOlasiligi(MusteriTuru tur)
    {
        return tur switch
        {
            MusteriTuru.Bireysel => 0.03,
            MusteriTuru.KucukIsletme => 0.10,
            MusteriTuru.OrtaOlcekliIsletme => 0.18,
            MusteriTuru.Kurumsal => 0.30,
            MusteriTuru.KamuKurumu => 0.22,
            _ => 0.05
        };
    }

    private static double MusteriTuruneGoreHizmetAgirligi(
        MusteriTuru musteriTuru,
        string hizmetKimligi)
    {
        bool matematik = hizmetKimligi.StartsWith(
            "matematik.",
            StringComparison.OrdinalIgnoreCase);
        bool metin = hizmetKimligi.StartsWith(
            "metin.",
            StringComparison.OrdinalIgnoreCase);
        bool veri = hizmetKimligi.StartsWith(
            "veri.",
            StringComparison.OrdinalIgnoreCase);
        bool dizi = hizmetKimligi.StartsWith(
            "dizi.",
            StringComparison.OrdinalIgnoreCase);

        return musteriTuru switch
        {
            MusteriTuru.Bireysel when metin => 1.45,
            MusteriTuru.Bireysel when matematik => 1.05,
            MusteriTuru.KucukIsletme when matematik => 1.30,
            MusteriTuru.KucukIsletme when metin => 1.25,
            MusteriTuru.KucukIsletme when dizi => 1.15,
            MusteriTuru.OrtaOlcekliIsletme when veri => 1.75,
            MusteriTuru.OrtaOlcekliIsletme when dizi => 1.55,
            MusteriTuru.Kurumsal when veri => 2.20,
            MusteriTuru.Kurumsal when dizi => 1.90,
            MusteriTuru.Kurumsal when metin => 1.25,
            MusteriTuru.KamuKurumu when veri => 2.00,
            MusteriTuru.KamuKurumu when matematik => 1.35,
            MusteriTuru.KamuKurumu when dizi => 1.60,
            _ => 1.00
        };
    }

    private static void MusteriSadakatiniGuncelle(
        Musteri musteri,
        HizmetTalebi talep,
        bool basarili)
    {
        if (string.IsNullOrWhiteSpace(talep.SecilenSirketKimligi))
        {
            return;
        }

        if (basarili)
        {
            musteri.TercihEdilenSirketKimligi =
                talep.SecilenSirketKimligi;
        }
        else if (string.Equals(
                     musteri.TercihEdilenSirketKimligi,
                     talep.SecilenSirketKimligi,
                     StringComparison.OrdinalIgnoreCase))
        {
            musteri.TercihEdilenSirketKimligi = null;
        }
    }

    private void MusteriIndeksiniOlustur()
    {
        _musteriIndeksi.Clear();

        foreach (Musteri musteri in Musteriler)
        {
            if (!_musteriIndeksi.TryAdd(
                    musteri.MusteriKimligi,
                    musteri))
            {
                throw new InvalidOperationException(
                    $"Tekrarlanan müşteri kimliği: {musteri.MusteriKimligi}");
            }
        }
    }

    private void MusterileriDogrula()
    {
        if (Musteriler.Count == 0)
        {
            throw new InvalidOperationException(
                "Motorun müşteri listesi boş.");
        }

        foreach (Musteri musteri in Musteriler)
        {
            if (string.IsNullOrWhiteSpace(musteri.MusteriKimligi))
            {
                throw new InvalidOperationException(
                    "Müşteri kimliği boş olamaz.");
            }

            musteri.Bakiye = Math.Max(0, musteri.Bakiye);
            musteri.TickBasinaHarcamaButcesi =
                Math.Max(0, musteri.TickBasinaHarcamaButcesi);
            musteri.TalepOlusturmaOlasiligi =
                Math.Clamp(musteri.TalepOlusturmaOlasiligi, 0, 1);
            musteri.Tercihler ??= new MusteriTercihleri();
            musteri.Tercihler.NormalizeEt();
            musteri.HizmetKullanimSayilari ??=
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
            musteri.IslemGecmisi ??= [];
        }
    }

    private static string YeniIsKimligi(long tickNumarasi)
    {
        return $"is-{tickNumarasi:D8}-{Guid.NewGuid():N}";
    }

    private void BaslatilmisOlmasiniDogrula()
    {
        if (!_baslatildi)
        {
            throw new InvalidOperationException(
                "Müşteri yöneticisi henüz başlatılmadı.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_baslatildi)
        {
            try
            {
                await KaydetAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                KonsolKayitcisi.Uyari(
                    $"Müşteri verileri kapatılırken kaydedilemedi: " +
                    exception.Message);
            }
        }

        _kayitKilidi.Dispose();
    }

    private sealed record IstekSenaryosu(string Json);
}
