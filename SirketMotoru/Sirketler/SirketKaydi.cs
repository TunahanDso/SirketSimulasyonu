using SirketMotoru.Protokol;

namespace SirketMotoru.Sirketler;

public sealed class SirketKaydi
{
    public string SirketKimligi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public string SunucuSurumu { get; set; } = string.Empty;
    public List<SunulanHizmet> Hizmetler { get; set; } = [];
    public SirketDurumu Durum { get; set; } = SirketDurumu.BagliDegil;
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
    public decimal ToplamGuvenlikKaybi { get; set; }
    public double ItibarPuani { get; set; } = 50.0;
    public double GuvenilirlikPuani { get; set; } = 50.0;
    public double KodKalitesiPuani { get; set; } = 50.0;
    public double PerformansPuani { get; set; } = 50.0;
    public double GuvenlikPuani { get; set; } = 50.0;
    public double OrtalamaMusteriMemnuniyeti { get; set; } = 50.0;
    public int AktifIsSayisi { get; set; }
    public int TamamlananIsSayisi { get; set; }
    public int BasarisizIsSayisi { get; set; }
    public int ZamanAsiminaUgrayanIsSayisi { get; set; }
    public int IptalEdilenIsSayisi { get; set; }
    public int ReddedilenIsSayisi { get; set; }
    public int EngellenenSaldiriSayisi { get; set; }
    public int BasariliSaldiriSayisi { get; set; }
    public long ToplamIslemSuresiMs { get; set; }

    public int ToplamIsSayisi =>
        TamamlananIsSayisi +
        BasarisizIsSayisi +
        ZamanAsiminaUgrayanIsSayisi +
        IptalEdilenIsSayisi;

    public decimal NetGelir =>
        ToplamGelir - ToplamIade - ToplamCeza;

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

            return tamamlanmisIsSayisi == 0
                ? 0
                : (double)TamamlananIsSayisi /
                  tamamlanmisIsSayisi * 100.0;
        }
    }

    public bool BagliMi =>
        Durum is SirketDurumu.Bagli or SirketDurumu.Calisiyor;

    public bool YeniIsAlabilirMi =>
        BagliMi &&
        Hizmetler.Any(
            hizmet =>
                hizmet.Aktif &&
                AktifIsSayisi < hizmet.AzamiEszamanliIs);

    public bool HizmetSunuyorMu(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        if (string.IsNullOrWhiteSpace(hizmetKimligi) ||
            string.IsNullOrWhiteSpace(hizmetSurumu))
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
        if (string.IsNullOrWhiteSpace(hizmetKimligi) ||
            string.IsNullOrWhiteSpace(hizmetSurumu))
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
            HizmetiBul(hizmetKimligi, hizmetSurumu);

        return hizmet is not null &&
               BagliMi &&
               AktifIsSayisi < hizmet.AzamiEszamanliIs;
    }

    public decimal? HizmetFiyatiniGetir(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        return HizmetiBul(
            hizmetKimligi,
            hizmetSurumu)?.BirimFiyat;
    }

    public void IsBaslat(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        if (!HizmetSunuyorMu(hizmetKimligi, hizmetSurumu))
        {
            throw new InvalidOperationException(
                $"{SirketAdi} şirketi " +
                $"{hizmetKimligi}@{hizmetSurumu} hizmetini sunmuyor.");
        }

        if (!HizmetIcinKapasiteVarMi(hizmetKimligi, hizmetSurumu))
        {
            throw new InvalidOperationException(
                $"{SirketAdi} şirketinin " +
                $"{hizmetKimligi}@{hizmetSurumu} hizmeti için " +
                "boş kapasitesi bulunmuyor.");
        }

        AktifIsSayisi++;
    }

    public void BasariliIsKaydet(
        decimal gelir,
        double islemSuresiMs,
        double memnuniyetPuani,
        int zorlukSeviyesi = 1,
        int zamanAsimiMs = 5_000)
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

        zorlukSeviyesi = Math.Clamp(zorlukSeviyesi, 1, 5);
        AktifIsiAzalt();
        TamamlananIsSayisi++;
        ToplamGelir += gelir;
        Kasa += gelir;
        ToplamIslemSuresiMs +=
            (long)Math.Round(islemSuresiMs);
        SonBasariliIsZamani = DateTimeOffset.UtcNow;

        double performansGozlemi =
            PerformansGozlemiHesapla(
                islemSuresiMs,
                zamanAsimiMs);
        double performansAlfasi =
            0.025 + zorlukSeviyesi * 0.005;
        PerformansPuani =
            HareketliPuan(
                PerformansPuani,
                performansGozlemi,
                performansAlfasi);

        double kaliteGozlemi =
            Math.Clamp(72 + zorlukSeviyesi * 5.6, 0, 100);
        double kaliteAlfasi =
            0.008 + zorlukSeviyesi * 0.004;
        KodKalitesiPuani =
            HareketliPuan(
                KodKalitesiPuani,
                kaliteGozlemi,
                kaliteAlfasi);

        double hizBonusu =
            performansGozlemi / 100.0 * 0.002;
        double zorlukBonusu =
            zorlukSeviyesi * 0.0007;
        double gelirBonusu =
            Math.Min(0.001, (double)(gelir / 100_000m));

        ItibarPuani =
            SinirlaPuani(
                ItibarPuani +
                0.0008 +
                hizBonusu +
                zorlukBonusu +
                gelirBonusu);

        GuvenilirlikPuani =
            SinirlaPuani(
                GuvenilirlikPuani +
                0.001 +
                zorlukSeviyesi * 0.0004);

        MusteriMemnuniyetiniGuncelle(
            memnuniyetPuani);
    }

    public void BasarisizIsKaydet(
        string? hataKodu = null,
        int zorlukSeviyesi = 1)
    {
        zorlukSeviyesi = Math.Clamp(zorlukSeviyesi, 1, 5);
        AktifIsiAzalt();
        BasarisizIsSayisi++;
        SonBasarisizIsZamani = DateTimeOffset.UtcNow;

        double itibarCezasi =
            hataKodu?.ToLowerInvariant() switch
            {
                "yanlis-sonuc" => 1.75,
                "sunucu-hatasi" => 2.00,
                "gecersiz-sonuc" => 2.50,
                "baglanti-koptu" => 3.50,
                "sahte-sonuc" => 12.00,
                _ => 1.50
            };

        double guvenilirlikCezasi =
            hataKodu?.ToLowerInvariant() switch
            {
                "yanlis-sonuc" => 2.25,
                "sunucu-hatasi" => 2.75,
                "gecersiz-sonuc" => 3.00,
                "baglanti-koptu" => 4.00,
                "sahte-sonuc" => 18.00,
                _ => 1.75
            };

        ItibarPuani =
            SinirlaPuani(
                ItibarPuani -
                itibarCezasi * (0.75 + zorlukSeviyesi * 0.15));
        GuvenilirlikPuani =
            SinirlaPuani(
                GuvenilirlikPuani -
                guvenilirlikCezasi * (0.75 + zorlukSeviyesi * 0.15));
        KodKalitesiPuani =
            SinirlaPuani(
                KodKalitesiPuani -
                (0.75 + zorlukSeviyesi * 0.65));

        MusteriMemnuniyetiniGuncelle(0);
    }

    public void ZamanAsimiKaydet(
        int zorlukSeviyesi = 1)
    {
        zorlukSeviyesi = Math.Clamp(zorlukSeviyesi, 1, 5);
        AktifIsiAzalt();
        ZamanAsiminaUgrayanIsSayisi++;
        SonBasarisizIsZamani = DateTimeOffset.UtcNow;
        ItibarPuani =
            SinirlaPuani(ItibarPuani - 2.5 - zorlukSeviyesi * 0.25);
        GuvenilirlikPuani =
            SinirlaPuani(GuvenilirlikPuani - 3.5 - zorlukSeviyesi * 0.35);
        PerformansPuani =
            SinirlaPuani(PerformansPuani - 3.0 - zorlukSeviyesi * 0.8);
        MusteriMemnuniyetiniGuncelle(0);
    }

    public void SaldiriEngellendiKaydet(
        int zorlukSeviyesi,
        double islemSuresiMs,
        int zamanAsimiMs)
    {
        zorlukSeviyesi = Math.Clamp(zorlukSeviyesi, 1, 5);
        AktifIsiAzalt();
        ReddedilenIsSayisi++;
        EngellenenSaldiriSayisi++;

        GuvenlikPuani =
            HareketliPuan(
                GuvenlikPuani,
                100,
                0.025 + zorlukSeviyesi * 0.008);
        PerformansPuani =
            HareketliPuan(
                PerformansPuani,
                PerformansGozlemiHesapla(
                    islemSuresiMs,
                    zamanAsimiMs),
                0.015);
        ItibarPuani =
            SinirlaPuani(
                ItibarPuani + 0.003 + zorlukSeviyesi * 0.001);
        GuvenilirlikPuani =
            SinirlaPuani(
                GuvenilirlikPuani + 0.004);
    }

    public void SaldiriBasariliKaydet(
        decimal kayip,
        int zorlukSeviyesi)
    {
        zorlukSeviyesi = Math.Clamp(zorlukSeviyesi, 1, 5);
        kayip = Math.Max(0, kayip);
        AktifIsiAzalt();
        BasarisizIsSayisi++;
        BasariliSaldiriSayisi++;
        ToplamGuvenlikKaybi += kayip;
        SonBasarisizIsZamani = DateTimeOffset.UtcNow;

        CezaUygula(
            kayip,
            itibarCezasi: 2.0 + zorlukSeviyesi * 0.75,
            guvenilirlikCezasi: 2.5 + zorlukSeviyesi * 0.9);
        GuvenlikPuani =
            SinirlaPuani(
                GuvenlikPuani - 5.0 - zorlukSeviyesi * 2.0);
        KodKalitesiPuani =
            SinirlaPuani(
                KodKalitesiPuani - 0.5 - zorlukSeviyesi * 0.4);
        MusteriMemnuniyetiniGuncelle(0);
    }

    public void PiyasaYipranmasiUygula()
    {
        ItibarPuani = MerkezeYaklastir(ItibarPuani, 50, 0.0015);
        GuvenilirlikPuani = MerkezeYaklastir(GuvenilirlikPuani, 50, 0.0010);
        KodKalitesiPuani = MerkezeYaklastir(KodKalitesiPuani, 50, 0.0008);
        PerformansPuani = MerkezeYaklastir(PerformansPuani, 50, 0.0008);
        GuvenlikPuani = MerkezeYaklastir(GuvenlikPuani, 50, 0.0008);
        OrtalamaMusteriMemnuniyeti =
            MerkezeYaklastir(
                OrtalamaMusteriMemnuniyeti,
                50,
                0.0005);
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

    public void IadeYap(decimal tutar)
    {
        if (tutar < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tutar),
                "İade tutarı negatif olamaz.");
        }

        decimal gercekIade = Math.Min(tutar, Kasa);
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

        decimal kasadanKesilen = Math.Min(Kasa, tutar);
        Kasa -= kasadanKesilen;
        ToplamCeza += tutar;
        ItibarPuani =
            SinirlaPuani(
                ItibarPuani - Math.Max(0, itibarCezasi));
        GuvenilirlikPuani =
            SinirlaPuani(
                GuvenilirlikPuani - Math.Max(0, guvenilirlikCezasi));
    }

    public double HizPuaniHesapla()
    {
        double agGecikmePuani;

        if (SonGecikmeMs <= 0)
        {
            agGecikmePuani = 50;
        }
        else
        {
            agGecikmePuani =
                SinirlaPuani(
                    100.0 -
                    Math.Log10(
                        Math.Max(1, SonGecikmeMs)) * 20.0);
        }

        return SinirlaPuani(
            PerformansPuani * 0.85 +
            agGecikmePuani * 0.15);
    }

    public double KapasitePuaniHesapla(
        SunulanHizmet hizmet)
    {
        ArgumentNullException.ThrowIfNull(hizmet);

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

        return (1.0 - dolulukOrani) * 100.0;
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
        yeniPuan = SinirlaPuani(yeniPuan);
        double alfa =
            yeniPuan < OrtalamaMusteriMemnuniyeti
                ? 0.040
                : 0.006;

        OrtalamaMusteriMemnuniyeti =
            HareketliPuan(
                OrtalamaMusteriMemnuniyeti,
                yeniPuan,
                alfa);
    }

    private static double PerformansGozlemiHesapla(
        double islemSuresiMs,
        int zamanAsimiMs)
    {
        if (zamanAsimiMs <= 0)
        {
            return 50;
        }

        double oran =
            Math.Clamp(
                islemSuresiMs / zamanAsimiMs,
                0,
                1);

        return Math.Clamp(
            100 - Math.Pow(oran, 0.55) * 100,
            0,
            100);
    }

    private static double HareketliPuan(
        double mevcut,
        double gozlem,
        double alfa)
    {
        alfa = Math.Clamp(alfa, 0, 1);
        return SinirlaPuani(
            mevcut + (gozlem - mevcut) * alfa);
    }

    private static double MerkezeYaklastir(
        double mevcut,
        double merkez,
        double oran)
    {
        return SinirlaPuani(
            mevcut + (merkez - mevcut) * oran);
    }

    private static double SinirlaPuani(double puan)
    {
        if (double.IsNaN(puan) ||
            double.IsInfinity(puan))
        {
            return 50;
        }

        return Math.Clamp(puan, 0, 100);
    }
}
