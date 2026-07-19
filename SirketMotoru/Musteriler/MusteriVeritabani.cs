using System.Text;
using System.Text.Json;
using SirketMotoru.Kayit;

namespace SirketMotoru.Musteriler;

public sealed class MusteriVeritabani
{
    private static readonly JsonSerializerOptions JsonAyarlari =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

    private readonly string _dosyaYolu;
    private readonly List<Musteri> _musteriler = [];

    public IReadOnlyList<Musteri> Musteriler => _musteriler;

    public MusteriVeritabani(string dosyaYolu)
    {
        if (string.IsNullOrWhiteSpace(dosyaYolu))
        {
            throw new ArgumentException(
                "Müşteri dosya yolu boş olamaz.",
                nameof(dosyaYolu));
        }

        _dosyaYolu = Path.GetFullPath(dosyaYolu);
    }

    public async Task YukleVeyaOlusturAsync(
        int musteriSayisi,
        CancellationToken cancellationToken)
    {
        if (musteriSayisi < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(musteriSayisi));
        }

        if (File.Exists(_dosyaYolu))
        {
            await YukleAsync(cancellationToken);

            if (_musteriler.Count < musteriSayisi)
            {
                int eskiMusteriSayisi = _musteriler.Count;
                HashSet<string> mevcutKimlikler =
                    _musteriler
                        .Select(musteri => musteri.MusteriKimligi)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (Musteri yeniMusteri in
                         MusterileriOlustur(musteriSayisi))
                {
                    if (mevcutKimlikler.Add(
                            yeniMusteri.MusteriKimligi))
                    {
                        _musteriler.Add(yeniMusteri);
                    }
                }

                await KaydetAsync(cancellationToken);

                KonsolKayitcisi.Basari(
                    $"Müşteri veritabanı genişletildi | " +
                    $"Eski: {eskiMusteriSayisi} | " +
                    $"Yeni: {_musteriler.Count}");
            }
            else
            {
                KonsolKayitcisi.Basari(
                    $"{_musteriler.Count} statik müşteri yüklendi.");
            }

            return;
        }

        _musteriler.AddRange(
            MusterileriOlustur(musteriSayisi));
        await KaydetAsync(cancellationToken);

        KonsolKayitcisi.Basari(
            $"{_musteriler.Count} statik müşteri " +
            "oluşturuldu ve kaydedildi.");
    }

    public async Task KaydetAsync(
        CancellationToken cancellationToken)
    {
        string? klasor = Path.GetDirectoryName(_dosyaYolu);

        if (!string.IsNullOrWhiteSpace(klasor))
        {
            Directory.CreateDirectory(klasor);
        }

        string geciciDosyaYolu = $"{_dosyaYolu}.tmp";
        string json = JsonSerializer.Serialize(_musteriler, JsonAyarlari);

        await File.WriteAllTextAsync(
            geciciDosyaYolu,
            json,
            new UTF8Encoding(false),
            cancellationToken);

        File.Move(
            geciciDosyaYolu,
            _dosyaYolu,
            overwrite: true);
    }

    public Musteri? MusteriyiBul(string musteriKimligi)
    {
        if (string.IsNullOrWhiteSpace(musteriKimligi))
        {
            return null;
        }

        return _musteriler.FirstOrDefault(
            musteri =>
                string.Equals(
                    musteri.MusteriKimligi,
                    musteriKimligi,
                    StringComparison.OrdinalIgnoreCase));
    }

    private async Task YukleAsync(
        CancellationToken cancellationToken)
    {
        string json =
            await File.ReadAllTextAsync(
                _dosyaYolu,
                cancellationToken);

        List<Musteri>? musteriler =
            JsonSerializer.Deserialize<List<Musteri>>(
                json,
                JsonAyarlari);

        if (musteriler is null)
        {
            throw new InvalidOperationException(
                "Müşteri veritabanı okunamadı.");
        }

        _musteriler.Clear();

        foreach (Musteri musteri in musteriler)
        {
            MusteriyiDogrula(musteri);
            _musteriler.Add(musteri);
        }
    }

    private static List<Musteri> MusterileriOlustur(
        int musteriSayisi)
    {
        Random rastgele = new(1881);
        List<Musteri> musteriler = new(musteriSayisi);

        for (int sira = 1; sira <= musteriSayisi; sira++)
        {
            MusteriTuru musteriTuru = MusteriTuruSec(rastgele);

            Musteri musteri =
                new()
                {
                    MusteriKimligi = $"musteri-{sira:D5}",
                    MusteriAdi = $"Müşteri {sira:D5}",
                    MusteriTuru = musteriTuru,
                    Bakiye = BaslangicBakiyesiOlustur(
                        musteriTuru,
                        rastgele),
                    TickBasinaHarcamaButcesi = TickButcesiOlustur(
                        musteriTuru,
                        rastgele),
                    TalepOlusturmaOlasiligi = TalepOlasiligiOlustur(
                        musteriTuru,
                        rastgele),
                    Tercihler = TercihOlustur(
                        musteriTuru,
                        rastgele),
                    OlusturulmaZamani = DateTimeOffset.UtcNow
                };

            musteriler.Add(musteri);
        }

        return musteriler;
    }

    private static MusteriTuru MusteriTuruSec(Random rastgele)
    {
        int deger = rastgele.Next(100);

        return deger switch
        {
            < 65 => MusteriTuru.Bireysel,
            < 85 => MusteriTuru.KucukIsletme,
            < 95 => MusteriTuru.OrtaOlcekliIsletme,
            < 99 => MusteriTuru.Kurumsal,
            _ => MusteriTuru.KamuKurumu
        };
    }

    private static decimal BaslangicBakiyesiOlustur(
        MusteriTuru musteriTuru,
        Random rastgele)
    {
        return musteriTuru switch
        {
            MusteriTuru.Bireysel => rastgele.Next(500, 5_001),
            MusteriTuru.KucukIsletme => rastgele.Next(5_000, 30_001),
            MusteriTuru.OrtaOlcekliIsletme => rastgele.Next(25_000, 150_001),
            MusteriTuru.Kurumsal => rastgele.Next(150_000, 1_000_001),
            MusteriTuru.KamuKurumu => rastgele.Next(500_000, 2_000_001),
            _ => 1_000
        };
    }

    private static decimal TickButcesiOlustur(
        MusteriTuru musteriTuru,
        Random rastgele)
    {
        return musteriTuru switch
        {
            MusteriTuru.Bireysel => rastgele.Next(15, 151),
            MusteriTuru.KucukIsletme => rastgele.Next(75, 751),
            MusteriTuru.OrtaOlcekliIsletme => rastgele.Next(400, 3_001),
            MusteriTuru.Kurumsal => rastgele.Next(1_500, 15_001),
            MusteriTuru.KamuKurumu => rastgele.Next(4_000, 35_001),
            _ => 75
        };
    }

    private static double TalepOlasiligiOlustur(
        MusteriTuru musteriTuru,
        Random rastgele)
    {
        double temelOlasilik =
            musteriTuru switch
            {
                MusteriTuru.Bireysel => 0.012,
                MusteriTuru.KucukIsletme => 0.030,
                MusteriTuru.OrtaOlcekliIsletme => 0.060,
                MusteriTuru.Kurumsal => 0.120,
                MusteriTuru.KamuKurumu => 0.080,
                _ => 0.020
            };

        return Math.Clamp(
            temelOlasilik *
            (0.80 + rastgele.NextDouble() * 0.40),
            0,
            1);
    }

    private static MusteriTercihleri TercihOlustur(
        MusteriTuru musteriTuru,
        Random rastgele)
    {
        MusteriTercihleri tercihler =
            musteriTuru switch
            {
                MusteriTuru.Bireysel =>
                    new MusteriTercihleri
                    {
                        FiyatAgirligi = 0.45,
                        ItibarAgirligi = 0.15,
                        HizAgirligi = 0.20,
                        GuvenilirlikAgirligi = 0.20
                    },
                MusteriTuru.KucukIsletme =>
                    new MusteriTercihleri
                    {
                        FiyatAgirligi = 0.30,
                        ItibarAgirligi = 0.20,
                        HizAgirligi = 0.20,
                        GuvenilirlikAgirligi = 0.30
                    },
                MusteriTuru.OrtaOlcekliIsletme =>
                    new MusteriTercihleri
                    {
                        FiyatAgirligi = 0.20,
                        ItibarAgirligi = 0.25,
                        HizAgirligi = 0.25,
                        GuvenilirlikAgirligi = 0.30
                    },
                MusteriTuru.Kurumsal =>
                    new MusteriTercihleri
                    {
                        FiyatAgirligi = 0.08,
                        ItibarAgirligi = 0.27,
                        HizAgirligi = 0.25,
                        GuvenilirlikAgirligi = 0.40
                    },
                MusteriTuru.KamuKurumu =>
                    new MusteriTercihleri
                    {
                        FiyatAgirligi = 0.15,
                        ItibarAgirligi = 0.25,
                        HizAgirligi = 0.20,
                        GuvenilirlikAgirligi = 0.40
                    },
                _ =>
                    new MusteriTercihleri
                    {
                        FiyatAgirligi = 0.25,
                        ItibarAgirligi = 0.25,
                        HizAgirligi = 0.25,
                        GuvenilirlikAgirligi = 0.25
                    }
            };

        tercihler.FiyatAgirligi *=
            0.90 + rastgele.NextDouble() * 0.20;
        tercihler.ItibarAgirligi *=
            0.90 + rastgele.NextDouble() * 0.20;
        tercihler.HizAgirligi *=
            0.90 + rastgele.NextDouble() * 0.20;
        tercihler.GuvenilirlikAgirligi *=
            0.90 + rastgele.NextDouble() * 0.20;
        tercihler.NormalizeEt();
        return tercihler;
    }

    private static void MusteriyiDogrula(Musteri musteri)
    {
        if (string.IsNullOrWhiteSpace(musteri.MusteriKimligi))
        {
            throw new InvalidOperationException(
                "Müşteri kimliği boş olamaz.");
        }

        musteri.MusteriAdi =
            string.IsNullOrWhiteSpace(musteri.MusteriAdi)
                ? musteri.MusteriKimligi
                : musteri.MusteriAdi.Trim();
        musteri.Tercihler ??= new MusteriTercihleri();
        musteri.Tercihler.NormalizeEt();
        musteri.HizmetKullanimSayilari ??=
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
        musteri.IslemGecmisi ??= [];
        musteri.Bakiye = Math.Max(0, musteri.Bakiye);
        musteri.TalepOlusturmaOlasiligi =
            Math.Clamp(musteri.TalepOlusturmaOlasiligi, 0, 1);
    }
}
