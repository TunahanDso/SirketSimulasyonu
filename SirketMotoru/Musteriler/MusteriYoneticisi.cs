using SirketMotoru.Hizmetler;
using SirketMotoru.Isler;
using SirketMotoru.Kayit;

namespace SirketMotoru.Musteriler;

public sealed class MusteriYoneticisi : IAsyncDisposable
{
    private const int VarsayilanMusteriSayisi = 2_000;

    private readonly MusteriVeritabani _musteriVeritabani;

    private readonly HizmetKatalogu _hizmetKatalogu;

    private readonly Random _rastgele;

    private readonly SemaphoreSlim _kayitKilidi =
        new(1, 1);

    private readonly Dictionary<string, Musteri> _musteriIndeksi =
        new(StringComparer.OrdinalIgnoreCase);

    private long _sonKayitTicki;

    private bool _baslatildi;

    public IReadOnlyList<Musteri> Musteriler =>
        _musteriVeritabani.Musteriler;

    public int AktifMusteriSayisi =>
        Musteriler.Count(
            musteri => musteri.Aktif);

    public decimal ToplamMusteriBakiyesi =>
        Musteriler.Sum(
            musteri => musteri.Bakiye);

    public decimal ToplamMusteriHarcamasi =>
        Musteriler.Sum(
            musteri => musteri.ToplamHarcama);

    public MusteriYoneticisi(
        MusteriVeritabani musteriVeritabani,
        HizmetKatalogu hizmetKatalogu,
        int rastgeleTohum = 1881)
    {
        ArgumentNullException.ThrowIfNull(
            musteriVeritabani);

        ArgumentNullException.ThrowIfNull(
            hizmetKatalogu);

        _musteriVeritabani =
            musteriVeritabani;

        _hizmetKatalogu =
            hizmetKatalogu;

        _rastgele =
            new Random(rastgeleTohum);
    }

    public async Task BaslatAsync(
        CancellationToken cancellationToken)
    {
        if (_baslatildi)
        {
            return;
        }

        await _musteriVeritabani
            .YukleVeyaOlusturAsync(
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

    public Musteri? MusteriyiBul(
        string musteriKimligi)
    {
        if (string.IsNullOrWhiteSpace(
                musteriKimligi))
        {
            return null;
        }

        _musteriIndeksi.TryGetValue(
            musteriKimligi.Trim(),
            out Musteri? musteri);

        return musteri;
    }

    public IReadOnlyList<Musteri>
        TalepOlusturacakMusterileriSec(
            long tickNumarasi)
    {
        BaslatilmisOlmasiniDogrula();

        List<Musteri> secilenMusteriler = [];

        foreach (Musteri musteri in Musteriler)
        {
            if (!MusteriTalepOlusturabilirMi(
                    musteri))
            {
                continue;
            }

            double tickEtkisi =
                TickTalepCarpaniHesapla(
                    tickNumarasi);

            double sonOlasilik =
                Math.Clamp(
                    musteri.TalepOlusturmaOlasiligi *
                    tickEtkisi,
                    0,
                    1);

            if (_rastgele.NextDouble() <=
                sonOlasilik)
            {
                secilenMusteriler.Add(
                    musteri);
            }
        }

        return secilenMusteriler;
    }

    public IReadOnlyList<HizmetTalebi>
        TickTalepleriniOlustur(
            long tickNumarasi)
    {
        BaslatilmisOlmasiniDogrula();

        IReadOnlyList<Musteri> talepSahipleri =
            TalepOlusturacakMusterileriSec(
                tickNumarasi);

        List<HizmetTalebi> talepler =
            new(talepSahipleri.Count);

        foreach (Musteri musteri in
                 talepSahipleri)
        {
            HizmetTalebi? talep =
                MusteriIcinTalepOlustur(
                    musteri,
                    tickNumarasi);

            if (talep is not null)
            {
                talepler.Add(
                    talep);
            }
        }

        KonsolKayitcisi.Bilgi(
            $"Tick {tickNumarasi} | " +
            $"Talep oluşturan müşteri: " +
            $"{talepSahipleri.Count} | " +
            $"Geçerli talep: {talepler.Count}");

        return talepler;
    }

    public HizmetTalebi? MusteriIcinTalepOlustur(
        Musteri musteri,
        long tickNumarasi)
    {
        ArgumentNullException.ThrowIfNull(
            musteri);

        BaslatilmisOlmasiniDogrula();

        if (!MusteriTalepOlusturabilirMi(
                musteri))
        {
            return null;
        }

        HizmetTanimi? hizmet =
            HizmetSec(
                musteri);

        if (hizmet is null)
        {
            return null;
        }

        decimal azamiButce =
            AzamiButceHesapla(
                musteri);

        if (azamiButce <= 0)
        {
            return null;
        }

        return new HizmetTalebi
        {
            IsKimligi =
                YeniIsKimligi(
                    tickNumarasi),

            MusteriKimligi =
                musteri.MusteriKimligi,

            HizmetKimligi =
                hizmet.HizmetKimligi,

            HizmetSurumu =
                hizmet.HizmetSurumu,

            OlusturulmaTicki =
                tickNumarasi,

            AzamiButce =
                azamiButce,

            IstekVerisiJson =
                IstekVerisiOlustur(
                    hizmet),

            Durum =
                IsDurumu.Olusturuldu,

            OlusturulmaZamani =
                DateTimeOffset.UtcNow
        };
    }

    public bool MusteridenOdemeAl(
        string musteriKimligi,
        decimal tutar)
    {
        Musteri? musteri =
            MusteriyiBul(
                musteriKimligi);

        if (musteri is null)
        {
            return false;
        }

        if (!musteri.OdemeYapabilirMi(
                tutar))
        {
            return false;
        }

        musteri.OdemeYap(
            tutar);

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

        Musteri? musteri =
            MusteriyiBul(
                musteriKimligi);

        if (musteri is null)
        {
            throw new InvalidOperationException(
                $"Müşteri bulunamadı: " +
                $"{musteriKimligi}");
        }

        musteri.Bakiye += tutar;

        musteri.ToplamHarcama =
            Math.Max(
                0,
                musteri.ToplamHarcama - tutar);
    }

    public void IslemSonucunuKaydet(
        HizmetTalebi talep,
        bool basarili,
        decimal odenenTutar,
        double tamamlanmaSuresiMs,
        string sonucAciklamasi)
    {
        ArgumentNullException.ThrowIfNull(
            talep);

        Musteri? musteri =
            MusteriyiBul(
                talep.MusteriKimligi);

        if (musteri is null)
        {
            throw new InvalidOperationException(
                $"İşlem müşterisi bulunamadı: " +
                $"{talep.MusteriKimligi}");
        }

        MusteriIslemKaydi islemKaydi =
            new()
            {
                IsKimligi =
                    talep.IsKimligi,

                TickNumarasi =
                    talep.OlusturulmaTicki,

                HizmetKimligi =
                    talep.HizmetKimligi,

                HizmetSurumu =
                    talep.HizmetSurumu,

                SirketKimligi =
                    talep.SecilenSirketKimligi ??
                    string.Empty,

                OdenenTutar =
                    basarili
                        ? odenenTutar
                        : 0,

                Basarili =
                    basarili,

                TamamlanmaSuresiMs =
                    Math.Max(
                        0,
                        tamamlanmaSuresiMs),

                OlusturulmaZamani =
                    DateTimeOffset.UtcNow,

                SonucAciklamasi =
                    sonucAciklamasi?.Trim() ??
                    string.Empty
            };

        musteri.IslemKaydet(
            islemKaydi);

        MusteriSadakatiniGuncelle(
            musteri,
            talep,
            basarili);
    }

    public async Task GerekirseKaydetAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();

        const int kayitAraligiTick = 10;

        if (tickNumarasi - _sonKayitTicki <
            kayitAraligiTick)
        {
            return;
        }

        await KaydetAsync(
            cancellationToken);

        _sonKayitTicki =
            tickNumarasi;
    }

    public async Task KaydetAsync(
        CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();

        await _kayitKilidi.WaitAsync(
            cancellationToken);

        try
        {
            await _musteriVeritabani
                .KaydetAsync(
                    cancellationToken);

            KonsolKayitcisi.Bilgi(
                $"{Musteriler.Count} müşteri kaydı " +
                $"disk üzerine yazıldı.");
        }
        finally
        {
            _kayitKilidi.Release();
        }
    }

    public void TickBasindaMusterileriGuncelle(
        long tickNumarasi)
    {
        BaslatilmisOlmasiniDogrula();

        foreach (Musteri musteri in Musteriler)
        {
            if (!musteri.Aktif)
            {
                continue;
            }

            MusteriGeliriEkle(
                musteri,
                tickNumarasi);
        }
    }

    private HizmetTanimi? HizmetSec(
        Musteri musteri)
    {
        List<HizmetTanimi> aktifHizmetler =
            _hizmetKatalogu.Hizmetler
                .Where(
                    hizmet => hizmet.Aktif)
                .ToList();

        if (aktifHizmetler.Count == 0)
        {
            return null;
        }

        List<(HizmetTanimi Hizmet, double Agirlik)>
            agirlikliHizmetler = [];

        foreach (HizmetTanimi hizmet in
                 aktifHizmetler)
        {
            musteri.HizmetKullanimSayilari
                .TryGetValue(
                    hizmet.HizmetKimligi,
                    out int kullanimSayisi);

            double tekrarKullanimBonusu =
                Math.Min(
                    3.0,
                    1.0 +
                    kullanimSayisi * 0.10);

            double hizmetAgirligi =
                MusteriTuruneGoreHizmetAgirligi(
                    musteri.MusteriTuru,
                    hizmet.HizmetKimligi);

            double sonAgirlik =
                Math.Max(
                    0.01,
                    hizmetAgirligi *
                    tekrarKullanimBonusu);

            agirlikliHizmetler.Add(
                (hizmet, sonAgirlik));
        }

        return AgirlikliSecim(
            agirlikliHizmetler);
    }

    private HizmetTanimi? AgirlikliSecim(
        IReadOnlyList<(
            HizmetTanimi Hizmet,
            double Agirlik)> hizmetler)
    {
        if (hizmetler.Count == 0)
        {
            return null;
        }

        double toplamAgirlik =
            hizmetler.Sum(
                oge => oge.Agirlik);

        if (toplamAgirlik <= 0)
        {
            return hizmetler[
                _rastgele.Next(
                    hizmetler.Count)].Hizmet;
        }

        double secim =
            _rastgele.NextDouble() *
            toplamAgirlik;

        double birikenAgirlik = 0;

        foreach (var oge in hizmetler)
        {
            birikenAgirlik +=
                oge.Agirlik;

            if (secim <= birikenAgirlik)
            {
                return oge.Hizmet;
            }
        }

        return hizmetler[^1].Hizmet;
    }

    private decimal AzamiButceHesapla(
        Musteri musteri)
    {
        decimal tickButcesi =
            Math.Min(
                musteri.TickBasinaHarcamaButcesi,
                musteri.Bakiye);

        if (tickButcesi <= 0)
        {
            return 0;
        }

        decimal butceCarpani =
            (decimal)(
                0.50 +
                _rastgele.NextDouble() *
                0.50);

        decimal azamiButce =
            tickButcesi *
            butceCarpani;

        return decimal.Round(
            Math.Max(
                0.01m,
                azamiButce),
            2);
    }

    private string IstekVerisiOlustur(
        HizmetTanimi hizmet)
    {
        return hizmet.HizmetKimligi
            .ToLowerInvariant() switch
        {
            "matematik.topla" =>
                MatematikToplaIstegiOlustur(),

            "matematik.carp" =>
                MatematikCarpIstegiOlustur(),

            "veri.ortalama-hesapla" =>
                OrtalamaIstegiOlustur(),

            "metin.kelime-say" =>
                KelimeSayIstegiOlustur(),

            "metin.karakter-say" =>
                KarakterSayIstegiOlustur(),

            _ => "{}"
        };
    }

    private string MatematikToplaIstegiOlustur()
    {
        int sayiAdedi =
            _rastgele.Next(
                2,
                11);

        int[] sayilar =
            Enumerable.Range(
                    0,
                    sayiAdedi)
                .Select(
                    _ => _rastgele.Next(
                        -1_000,
                        1_001))
                .ToArray();

        return System.Text.Json.JsonSerializer.Serialize(
            new
            {
                sayilar
            });
    }

    private string MatematikCarpIstegiOlustur()
    {
        int sayiAdedi =
            _rastgele.Next(
                2,
                6);

        int[] sayilar =
            Enumerable.Range(
                    0,
                    sayiAdedi)
                .Select(
                    _ => _rastgele.Next(
                        1,
                        11))
                .ToArray();

        return System.Text.Json.JsonSerializer.Serialize(
            new
            {
                sayilar
            });
    }

    private string OrtalamaIstegiOlustur()
    {
        int sayiAdedi =
            _rastgele.Next(
                3,
                16);

        double[] sayilar =
            Enumerable.Range(
                    0,
                    sayiAdedi)
                .Select(
                    _ =>
                        Math.Round(
                            _rastgele.NextDouble() *
                            1_000,
                            2))
                .ToArray();

        return System.Text.Json.JsonSerializer.Serialize(
            new
            {
                sayilar
            });
    }

    private string KelimeSayIstegiOlustur()
    {
        string[] kelimeler =
        [
            "Tunix",
            "motor",
            "müşteri",
            "şirket",
            "hizmet",
            "yazılım",
            "sunucu",
            "ekonomi",
            "protokol",
            "güvenlik",
            "performans",
            "kalite"
        ];

        int kelimeSayisi =
            _rastgele.Next(
                3,
                20);

        string metin =
            string.Join(
                " ",
                Enumerable.Range(
                        0,
                        kelimeSayisi)
                    .Select(
                        _ => kelimeler[
                            _rastgele.Next(
                                kelimeler.Length)]));

        return System.Text.Json.JsonSerializer.Serialize(
            new
            {
                metin
            });
    }

    private string KarakterSayIstegiOlustur()
    {
        string[] cumleler =
        [
            "İnsan için teknoloji.",
            "Şirket motoru hizmetleri değerlendiriyor.",
            "Müşteriler hızlı ve güvenilir hizmet istiyor.",
            "Yazılım şirketleri piyasada rekabet ediyor.",
            "Motor bütün ekonomik sonuçları takip ediyor."
        ];

        string metin =
            cumleler[
                _rastgele.Next(
                    cumleler.Length)];

        return System.Text.Json.JsonSerializer.Serialize(
            new
            {
                metin
            });
    }

    private void MusteriGeliriEkle(
        Musteri musteri,
        long tickNumarasi)
    {
        int gelirAraligi =
            musteri.MusteriTuru switch
            {
                MusteriTuru.Bireysel => 50,
                MusteriTuru.KucukIsletme => 250,
                MusteriTuru.OrtaOlcekliIsletme => 1_000,
                MusteriTuru.Kurumsal => 5_000,
                MusteriTuru.KamuKurumu => 10_000,
                _ => 50
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

        if (tickNumarasi <= 0 ||
            tickNumarasi % gelirPeriyodu != 0)
        {
            return;
        }

        decimal gelir =
            _rastgele.Next(
                Math.Max(
                    1,
                    gelirAraligi / 2),
                gelirAraligi + 1);

        musteri.Bakiye += gelir;
    }

    private static bool MusteriTalepOlusturabilirMi(
        Musteri musteri)
    {
        return
            musteri.Aktif &&
            musteri.Bakiye > 0 &&
            musteri.TickBasinaHarcamaButcesi > 0 &&
            musteri.TalepOlusturmaOlasiligi > 0;
    }

    private static double TickTalepCarpaniHesapla(
        long tickNumarasi)
    {
        if (tickNumarasi <= 0)
        {
            return 1;
        }

        int pazarDongusu =
            (int)(tickNumarasi % 100);

        return pazarDongusu switch
        {
            < 20 => 0.80,
            < 40 => 1.00,
            < 60 => 1.20,
            < 80 => 1.05,
            _ => 0.90
        };
    }

    private static double
        MusteriTuruneGoreHizmetAgirligi(
            MusteriTuru musteriTuru,
            string hizmetKimligi)
    {
        bool matematikHizmeti =
            hizmetKimligi.StartsWith(
                "matematik.",
                StringComparison.OrdinalIgnoreCase);

        bool metinHizmeti =
            hizmetKimligi.StartsWith(
                "metin.",
                StringComparison.OrdinalIgnoreCase);

        bool veriHizmeti =
            hizmetKimligi.StartsWith(
                "veri.",
                StringComparison.OrdinalIgnoreCase);

        return musteriTuru switch
        {
            MusteriTuru.Bireysel
                when metinHizmeti =>
                1.50,

            MusteriTuru.Bireysel
                when matematikHizmeti =>
                1.10,

            MusteriTuru.KucukIsletme
                when matematikHizmeti =>
                1.40,

            MusteriTuru.KucukIsletme
                when metinHizmeti =>
                1.25,

            MusteriTuru.OrtaOlcekliIsletme
                when veriHizmeti =>
                1.60,

            MusteriTuru.OrtaOlcekliIsletme
                when matematikHizmeti =>
                1.30,

            MusteriTuru.Kurumsal
                when veriHizmeti =>
                2.00,

            MusteriTuru.Kurumsal
                when metinHizmeti =>
                1.20,

            MusteriTuru.KamuKurumu
                when veriHizmeti =>
                1.80,

            MusteriTuru.KamuKurumu
                when matematikHizmeti =>
                1.30,

            _ => 1.00
        };
    }

    private static void MusteriSadakatiniGuncelle(
        Musteri musteri,
        HizmetTalebi talep,
        bool basarili)
    {
        if (string.IsNullOrWhiteSpace(
                talep.SecilenSirketKimligi))
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
            musteri.TercihEdilenSirketKimligi =
                null;
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
                    $"Tekrarlanan müşteri kimliği: " +
                    $"{musteri.MusteriKimligi}");
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
            if (string.IsNullOrWhiteSpace(
                    musteri.MusteriKimligi))
            {
                throw new InvalidOperationException(
                    "Müşteri kimliği boş olamaz.");
            }

            musteri.Bakiye =
                Math.Max(
                    0,
                    musteri.Bakiye);

            musteri.TickBasinaHarcamaButcesi =
                Math.Max(
                    0,
                    musteri.TickBasinaHarcamaButcesi);

            musteri.TalepOlusturmaOlasiligi =
                Math.Clamp(
                    musteri.TalepOlusturmaOlasiligi,
                    0,
                    1);

            musteri.Tercihler ??=
                new MusteriTercihleri();

            musteri.Tercihler.NormalizeEt();

            musteri.HizmetKullanimSayilari ??=
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            musteri.IslemGecmisi ??= [];
        }
    }

    private static string YeniIsKimligi(
        long tickNumarasi)
    {
        return
            $"is-{tickNumarasi:D8}-" +
            $"{Guid.NewGuid():N}";
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
                await KaydetAsync(
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                KonsolKayitcisi.Uyari(
                    $"Müşteri verileri kapatılırken " +
                    $"kaydedilemedi: " +
                    $"{exception.Message}");
            }
        }

        _kayitKilidi.Dispose();
    }
}