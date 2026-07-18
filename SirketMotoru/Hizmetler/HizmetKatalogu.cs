using SirketMotoru.Protokol;

namespace SirketMotoru.Hizmetler;

public sealed class HizmetKatalogu
{
    private readonly Dictionary<string, HizmetTanimi> _hizmetler;

    public string KatalogSurumu { get; }

    public IReadOnlyCollection<HizmetTanimi> Hizmetler =>
        _hizmetler.Values;

    public HizmetKatalogu(
        HizmetKatalogAyarlari ayarlar)
    {
        ArgumentNullException.ThrowIfNull(ayarlar);

        KatalogSurumu =
            string.IsNullOrWhiteSpace(ayarlar.KatalogSurumu)
                ? "1.0"
                : ayarlar.KatalogSurumu.Trim();

        _hizmetler =
            new Dictionary<string, HizmetTanimi>(
                StringComparer.OrdinalIgnoreCase);

        foreach (HizmetTanimi hizmet in ayarlar.Hizmetler)
        {
            HizmetTaniminiEkle(hizmet);
        }

        if (_hizmetler.Count == 0)
        {
            throw new InvalidOperationException(
                "Hizmet kataloğunda en az bir hizmet bulunmalıdır.");
        }
    }

    public bool HizmetVarMi(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        if (string.IsNullOrWhiteSpace(hizmetKimligi) ||
            string.IsNullOrWhiteSpace(hizmetSurumu))
        {
            return false;
        }

        string anahtar =
            AnahtarOlustur(
                hizmetKimligi,
                hizmetSurumu);

        return _hizmetler.TryGetValue(
                   anahtar,
                   out HizmetTanimi? hizmet) &&
               hizmet.Aktif;
    }

    public bool HizmetiBul(
        string hizmetKimligi,
        string hizmetSurumu,
        out HizmetTanimi? hizmet)
    {
        hizmet = null;

        if (string.IsNullOrWhiteSpace(hizmetKimligi) ||
            string.IsNullOrWhiteSpace(hizmetSurumu))
        {
            return false;
        }

        string anahtar =
            AnahtarOlustur(
                hizmetKimligi,
                hizmetSurumu);

        if (!_hizmetler.TryGetValue(
                anahtar,
                out HizmetTanimi? bulunanHizmet))
        {
            return false;
        }

        if (!bulunanHizmet.Aktif)
        {
            return false;
        }

        hizmet = bulunanHizmet;

        return true;
    }

    public List<SunulanHizmet> HizmetleriDogrula(
        IEnumerable<SunulanHizmet>? bildirilenHizmetler,
        out List<string> reddedilenHizmetler)
    {
        reddedilenHizmetler = [];

        if (bildirilenHizmetler is null)
        {
            return [];
        }

        List<SunulanHizmet> kabulEdilenHizmetler = [];

        HashSet<string> gorulenHizmetler =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (SunulanHizmet? hizmet in bildirilenHizmetler)
        {
            if (hizmet is null)
            {
                reddedilenHizmetler.Add(
                    "Boş hizmet bildirimi");

                continue;
            }

            string hizmetKimligi =
                hizmet.HizmetKimligi?.Trim() ??
                string.Empty;

            string hizmetSurumu =
                hizmet.HizmetSurumu?.Trim() ??
                string.Empty;

            if (string.IsNullOrWhiteSpace(hizmetKimligi))
            {
                reddedilenHizmetler.Add(
                    "Hizmet kimliği boş");

                continue;
            }

            if (string.IsNullOrWhiteSpace(hizmetSurumu))
            {
                reddedilenHizmetler.Add(
                    $"{hizmetKimligi}: hizmet sürümü boş");

                continue;
            }

            string anahtar =
                AnahtarOlustur(
                    hizmetKimligi,
                    hizmetSurumu);

            if (!gorulenHizmetler.Add(anahtar))
            {
                continue;
            }

            if (!HizmetVarMi(
                    hizmetKimligi,
                    hizmetSurumu))
            {
                reddedilenHizmetler.Add(
                    $"{hizmetKimligi}@{hizmetSurumu}");

                continue;
            }

            kabulEdilenHizmetler.Add(
                new SunulanHizmet
                {
                    HizmetKimligi = hizmetKimligi,
                    HizmetSurumu = hizmetSurumu
                });
        }

        return kabulEdilenHizmetler
            .OrderBy(
                hizmet => hizmet.HizmetKimligi,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                hizmet => hizmet.HizmetSurumu,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void HizmetTaniminiEkle(
        HizmetTanimi hizmet)
    {
        ArgumentNullException.ThrowIfNull(hizmet);

        hizmet.HizmetKimligi =
            hizmet.HizmetKimligi?.Trim() ??
            string.Empty;

        hizmet.HizmetSurumu =
            hizmet.HizmetSurumu?.Trim() ??
            string.Empty;

        hizmet.Aciklama =
            hizmet.Aciklama?.Trim() ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                hizmet.HizmetKimligi))
        {
            throw new InvalidOperationException(
                "Katalogdaki hizmet kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(
                hizmet.HizmetSurumu))
        {
            throw new InvalidOperationException(
                $"{hizmet.HizmetKimligi} hizmetinin " +
                $"sürümü boş olamaz.");
        }

        if (hizmet.ZamanAsimiMs < 100)
        {
            throw new InvalidOperationException(
                $"{hizmet.HizmetKimligi} hizmetinin " +
                $"zaman aşımı en az 100 ms olmalıdır.");
        }

        if (hizmet.AzamiIstekBoyutuByte < 1)
        {
            throw new InvalidOperationException(
                $"{hizmet.HizmetKimligi} hizmetinin " +
                $"azami istek boyutu en az 1 byte olmalıdır.");
        }

        string anahtar =
            AnahtarOlustur(
                hizmet.HizmetKimligi,
                hizmet.HizmetSurumu);

        if (!_hizmetler.TryAdd(
                anahtar,
                hizmet))
        {
            throw new InvalidOperationException(
                $"Katalogda aynı hizmet iki kez tanımlanmış: " +
                $"{hizmet.HizmetKimligi}@" +
                $"{hizmet.HizmetSurumu}");
        }
    }

    private static string AnahtarOlustur(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        return
            $"{hizmetKimligi.Trim()}@" +
            $"{hizmetSurumu.Trim()}";
    }
}