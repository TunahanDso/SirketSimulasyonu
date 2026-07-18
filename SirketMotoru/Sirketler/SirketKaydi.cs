using SirketMotoru.Protokol;

namespace SirketMotoru.Sirketler;

public sealed class SirketKaydi
{
    public string SirketKimligi { get; set; } = string.Empty;

    public string SirketAdi { get; set; } = string.Empty;

    public string SunucuSurumu { get; set; } = string.Empty;

    public List<SunulanHizmet> Hizmetler { get; set; } = [];

    public SirketDurumu Durum { get; set; } =
        SirketDurumu.BagliDegil;

    public double SonGecikmeMs { get; set; }

    public DateTimeOffset? SonCevapZamani { get; set; }

    public DateTimeOffset? SonBaglantiZamani { get; set; }

    public DateTimeOffset? SonBasariliIsZamani { get; set; }

    public DateTimeOffset? SonBasarisizIsZamani { get; set; }

    public int BasariliKontrolSayisi { get; set; }

    public int BasarisizKontrolSayisi { get; set; }

    public int AktifBaglantiSayisi { get; set; }

    public int KuyrukUzunlugu { get; set; }

    public decimal Kasa { get; set; }

    public decimal ToplamGelir { get; set; }

    public decimal ToplamIade { get; set; }

    public decimal ToplamCeza { get; set; }

    public decimal BekleyenOdeme { get; set; }

    public double ItibarPuani { get; set; } = 50.0;

    public double GuvenilirlikPuani { get; set; } = 50.0;

    public double OrtalamaMusteriMemnuniyeti { get; set; } = 50.0;

    public int AktifIsSayisi { get; set; }

    public int TamamlananIsSayisi { get; set; }

    public int BasarisizIsSayisi { get; set; }

    public int ZamanAsiminaUgrayanIsSayisi { get; set; }

    public int IptalEdilenIsSayisi { get; set; }

    public int ReddedilenIsSayisi { get; set; }

    public long ToplamIslemSuresiMs { get; set; }

    public int ToplamIsSayisi =>
        TamamlananIsSayisi +
        BasarisizIsSayisi +
        ZamanAsiminaUgrayanIsSayisi +
        IptalEdilenIsSayisi;

    public decimal NetGelir =>
        ToplamGelir -
        ToplamIade -
        ToplamCeza;

    public decimal OrtalamaIsTutari =>
        TamamlananIsSayisi == 0
            ? 0
            : ToplamGelir / TamamlananIsSayisi;

    public double OrtalamaIslemSuresiMs =>
        TamamlananIsSayisi == 0
            ? 0
            : (double)ToplamIslemSuresiMs /
              TamamlananIsSayisi;

    public double IsBasariOrani
    {
        get
        {
            int tamamlanmisIsSayisi =
                TamamlananIsSayisi +
                BasarisizIsSayisi +
                ZamanAsiminaUgrayanIsSayisi;

            if (tamamlanmisIsSayisi == 0)
            {
                return 0;
            }

            return
                (double)TamamlananIsSayisi /
                tamamlanmisIsSayisi *
                100.0;
        }
    }

    public bool BagliMi =>
        Durum is
            SirketDurumu.Bagli or
            SirketDurumu.Calisiyor;

    public bool YeniIsAlabilirMi =>
        BagliMi &&
        Hizmetler.Any(
            hizmet =>
                hizmet.Aktif &&
                AktifIsSayisi <
                hizmet.AzamiEszamanliIs);

    public bool HizmetSunuyorMu(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        if (string.IsNullOrWhiteSpace(
                hizmetKimligi) ||
            string.IsNullOrWhiteSpace(
                hizmetSurumu))
        {
            return false;
        }

        return Hizmetler.Any(
            hizmet =>
                hizmet.Aktif &&
                string.Equals(
                    hizmet.HizmetKimligi,
                    hizmetKimligi,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    hizmet.HizmetSurumu,
                    hizmetSurumu,
                    StringComparison.OrdinalIgnoreCase));
    }

    public SunulanHizmet? HizmetiBul(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        if (string.IsNullOrWhiteSpace(
                hizmetKimligi) ||
            string.IsNullOrWhiteSpace(
                hizmetSurumu))
        {
            return null;
        }

        return Hizmetler.FirstOrDefault(
            hizmet =>
                hizmet.Aktif &&
                string.Equals(
                    hizmet.HizmetKimligi,
                    hizmetKimligi,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    hizmet.HizmetSurumu,
                    hizmetSurumu,
                    StringComparison.OrdinalIgnoreCase));
    }

    public bool HizmetIcinKapasiteVarMi(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        SunulanHizmet? hizmet =
            HizmetiBul(
                hizmetKimligi,
                hizmetSurumu);

        if (hizmet is null)
        {
            return false;
        }

        return
            BagliMi &&
            AktifIsSayisi <
            hizmet.AzamiEszamanliIs;
    }

    public decimal? HizmetFiyatiniGetir(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        SunulanHizmet? hizmet =
            HizmetiBul(
                hizmetKimligi,
                hizmetSurumu);

        return hizmet?.BirimFiyat;
    }

    public void IsBaslat(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        if (!HizmetSunuyorMu(
                hizmetKimligi,
                hizmetSurumu))
        {
            throw new InvalidOperationException(
                $"{SirketAdi} şirketi " +
                $"{hizmetKimligi}@" +
                $"{hizmetSurumu} hizmetini sunmuyor.");
        }

        if (!HizmetIcinKapasiteVarMi(
                hizmetKimligi,
                hizmetSurumu))
        {
            throw new InvalidOperationException(
                $"{SirketAdi} şirketinin " +
                $"{hizmetKimligi}@" +
                $"{hizmetSurumu} hizmeti için " +
                $"boş kapasitesi bulunmuyor.");
        }

        AktifIsSayisi++;
    }

    public void BasariliIsKaydet(
        decimal gelir,
        double islemSuresiMs,
        double memnuniyetPuani)
    {
        if (gelir < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gelir),
                "Gelir negatif olamaz.");
        }

        if (islemSuresiMs < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(islemSuresiMs),
                "İşlem süresi negatif olamaz.");
        }

        AktifIsiAzalt();

        TamamlananIsSayisi++;

        ToplamGelir += gelir;

        Kasa += gelir;

        ToplamIslemSuresiMs +=
            (long)Math.Round(
                islemSuresiMs);

        SonBasariliIsZamani =
            DateTimeOffset.UtcNow;

        double hizBonusu =
            islemSuresiMs switch
            {
                <= 100 => 0.30,
                <= 500 => 0.20,
                <= 1_000 => 0.10,
                <= 2_000 => 0.05,
                _ => 0
            };

        double gelirBonusu =
            Math.Min(
                0.25,
                (double)(gelir / 10_000m));

        ItibarPuani =
            SinirlaPuani(
                ItibarPuani +
                0.10 +
                hizBonusu +
                gelirBonusu);

        GuvenilirlikPuani =
            SinirlaPuani(
                GuvenilirlikPuani +
                0.15);

        MusteriMemnuniyetiniGuncelle(
            memnuniyetPuani);
    }

    public void BasarisizIsKaydet(
        string? hataKodu = null)
    {
        AktifIsiAzalt();

        BasarisizIsSayisi++;

        SonBasarisizIsZamani =
            DateTimeOffset.UtcNow;

        double itibarCezasi =
            hataKodu?.ToLowerInvariant() switch
            {
                "yanlis-sonuc" => 1.25,
                "sunucu-hatasi" => 1.50,
                "gecersiz-sonuc" => 1.75,
                "baglanti-koptu" => 2.50,
                "sahte-sonuc" => 10.00,
                _ => 1.00
            };

        double guvenilirlikCezasi =
            hataKodu?.ToLowerInvariant() switch
            {
                "yanlis-sonuc" => 1.50,
                "sunucu-hatasi" => 2.00,
                "gecersiz-sonuc" => 2.25,
                "baglanti-koptu" => 3.00,
                "sahte-sonuc" => 15.00,
                _ => 1.25
            };

        ItibarPuani =
            SinirlaPuani(
                ItibarPuani -
                itibarCezasi);

        GuvenilirlikPuani =
            SinirlaPuani(
                GuvenilirlikPuani -
                guvenilirlikCezasi);

        MusteriMemnuniyetiniGuncelle(
            0);
    }

    public void ZamanAsimiKaydet()
    {
        AktifIsiAzalt();

        ZamanAsiminaUgrayanIsSayisi++;

        SonBasarisizIsZamani =
            DateTimeOffset.UtcNow;

        ItibarPuani =
            SinirlaPuani(
                ItibarPuani - 2.00);

        GuvenilirlikPuani =
            SinirlaPuani(
                GuvenilirlikPuani - 3.00);

        MusteriMemnuniyetiniGuncelle(
            0);
    }

    public void IsIptalEt()
    {
        AktifIsiAzalt();

        IptalEdilenIsSayisi++;
    }

    public void IsReddet()
    {
        ReddedilenIsSayisi++;
    }

    public void IadeYap(
        decimal tutar)
    {
        if (tutar < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tutar),
                "İade tutarı negatif olamaz.");
        }

        decimal gercekIade =
            Math.Min(
                tutar,
                Kasa);

        Kasa -= gercekIade;

        ToplamIade += gercekIade;
    }

    public void CezaUygula(
        decimal tutar,
        double itibarCezasi = 0,
        double guvenilirlikCezasi = 0)
    {
        if (tutar < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tutar),
                "Ceza tutarı negatif olamaz.");
        }

        decimal kasadanKesilen =
            Math.Min(
                Kasa,
                tutar);

        Kasa -= kasadanKesilen;

        ToplamCeza += tutar;

        ItibarPuani =
            SinirlaPuani(
                ItibarPuani -
                Math.Max(
                    0,
                    itibarCezasi));

        GuvenilirlikPuani =
            SinirlaPuani(
                GuvenilirlikPuani -
                Math.Max(
                    0,
                    guvenilirlikCezasi));
    }

    public double HizPuaniHesapla()
    {
        if (SonGecikmeMs <= 0)
        {
            return 50;
        }

        double puan =
            100.0 -
            Math.Log10(
                Math.Max(
                    1,
                    SonGecikmeMs)) *
            20.0;

        return SinirlaPuani(
            puan);
    }

    public double KapasitePuaniHesapla(
        SunulanHizmet hizmet)
    {
        ArgumentNullException.ThrowIfNull(
            hizmet);

        if (hizmet.AzamiEszamanliIs <= 0)
        {
            return 0;
        }

        double dolulukOrani =
            Math.Clamp(
                (double)AktifIsSayisi /
                hizmet.AzamiEszamanliIs,
                0,
                1);

        return
            (1.0 - dolulukOrani) *
            100.0;
    }

    private void AktifIsiAzalt()
    {
        if (AktifIsSayisi > 0)
        {
            AktifIsSayisi--;
        }
    }

    private void MusteriMemnuniyetiniGuncelle(
        double yeniPuan)
    {
        yeniPuan =
            SinirlaPuani(
                yeniPuan);

        int tamamlananDegerlendirmeSayisi =
            TamamlananIsSayisi +
            BasarisizIsSayisi +
            ZamanAsiminaUgrayanIsSayisi;

        if (tamamlananDegerlendirmeSayisi <= 1)
        {
            OrtalamaMusteriMemnuniyeti =
                yeniPuan;

            return;
        }

        double oncekiToplam =
            OrtalamaMusteriMemnuniyeti *
            (tamamlananDegerlendirmeSayisi - 1);

        OrtalamaMusteriMemnuniyeti =
            SinirlaPuani(
                (oncekiToplam + yeniPuan) /
                tamamlananDegerlendirmeSayisi);
    }

    private static double SinirlaPuani(
        double puan)
    {
        return Math.Clamp(
            puan,
            0,
            100);
    }
}