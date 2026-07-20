using System.Collections.Concurrent;
using SirketMotoru.Ag;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Protokol;
using SirketMotoru.Protokol.Mesajlar;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isler;

public sealed class IsYoneticisi
{
    private const int EnFazlaAdaySayisi = 3;
    private const int VarsayilanZamanAsimiMs = 5_000;

    private readonly SirketYoneticisi _sirketYoneticisi;
    private readonly MusteriYoneticisi _musteriYoneticisi;
    private readonly SonucDogrulayicisi _sonucDogrulayicisi;
    private readonly ConcurrentDictionary<string, byte> _islenenIsler =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _rastgele = new();
    private readonly object _rastgeleKilidi = new();

    public IsYoneticisi(
        SirketYoneticisi sirketYoneticisi,
        MusteriYoneticisi musteriYoneticisi,
        SonucDogrulayicisi sonucDogrulayicisi)
    {
        ArgumentNullException.ThrowIfNull(sirketYoneticisi);
        ArgumentNullException.ThrowIfNull(musteriYoneticisi);
        ArgumentNullException.ThrowIfNull(sonucDogrulayicisi);
        _sirketYoneticisi = sirketYoneticisi;
        _musteriYoneticisi = musteriYoneticisi;
        _sonucDogrulayicisi = sonucDogrulayicisi;
    }

    public async Task<IsIslemeOzeti> TalepleriIsleAsync(
        long tickNumarasi,
        IReadOnlyList<HizmetTalebi> talepler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(talepler);
        IsIslemeOzeti ozet =
            new()
            {
                TickNumarasi = tickNumarasi,
                ToplamTalepSayisi = talepler.Count,
                KotuNiyetliIsSayisi =
                    talepler.Count(talep => talep.KotuNiyetli)
            };

        foreach (HizmetTalebi talep in talepler)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsAtamaSonucu sonuc =
                await TalebiIsleAsync(
                    tickNumarasi,
                    talep,
                    cancellationToken);
            OzetGuncelle(ozet, sonuc);
        }

        TickOzetiniYaz(ozet);
        return ozet;
    }

    public async Task<IsAtamaSonucu> TalebiIsleAsync(
        long tickNumarasi,
        HizmetTalebi talep,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(talep);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(talep.IsKimligi))
        {
            return IsAtamaSonucu.BasarisizSonuc(
                string.Empty,
                null,
                "Talebin iş kimliği bulunmuyor.");
        }

        if (!_islenenIsler.TryAdd(talep.IsKimligi, 0))
        {
            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                null,
                "Aynı iş ikinci kez işlenmeye çalışıldı.");
        }

        try
        {
            return await TalebiIsleIcAsync(
                tickNumarasi,
                talep,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            KonsolKayitcisi.Hata(
                $"İş işlenirken beklenmeyen hata oluştu | " +
                $"İş: {talep.IsKimligi} | Hata: {exception.Message}");
            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                talep.SecilenSirketKimligi,
                exception.Message);
        }
    }

    private async Task<IsAtamaSonucu> TalebiIsleIcAsync(
        long tickNumarasi,
        HizmetTalebi talep,
        CancellationToken cancellationToken)
    {
        Musteri? musteri =
            _musteriYoneticisi.MusteriyiBul(talep.MusteriKimligi);

        if (musteri is null)
        {
            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                null,
                $"Müşteri bulunamadı: {talep.MusteriKimligi}");
        }

        if (!musteri.Aktif)
        {
            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                null,
                "Müşteri hesabı aktif değil.");
        }

        decimal kullanilabilirButce =
            Math.Min(talep.AzamiButce, musteri.Bakiye);

        if (kullanilabilirButce <= 0)
        {
            _musteriYoneticisi.IslemSonucunuKaydet(
                talep,
                false,
                0,
                0,
                "Müşterinin kullanılabilir bakiyesi yok.");
            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                null,
                "Müşteri bakiyesi yetersiz.");
        }

        IReadOnlyList<SirketAdayi> adaylar =
            SirketAdaylariniOlustur(
                musteri,
                talep,
                kullanilabilirButce);

        if (adaylar.Count == 0)
        {
            _musteriYoneticisi.IslemSonucunuKaydet(
                talep,
                false,
                0,
                0,
                "Uygun şirket bulunamadı.");
            KonsolKayitcisi.Uyari(
                $"İşe uygun şirket bulunamadı | İş: {talep.IsKimligi} | " +
                $"Hizmet: {talep.HizmetKimligi}@{talep.HizmetSurumu} | " +
                $"Bütçe: {kullanilabilirButce:N2}");
            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                null,
                "Hizmeti sunabilen uygun şirket bulunamadı.");
        }

        SirketAdayi secilenAday =
            AgirlikliSirketSec(adaylar, talep);
        SirketKaydi sirket = secilenAday.Sirket;
        SunulanHizmet hizmet = secilenAday.Hizmet;
        SirketBaglantisi baglanti = secilenAday.Baglanti;
        decimal islemTutari = hizmet.BirimFiyat;
        int zamanAsimiMs = ZamanAsiminiHesapla(talep);

        talep.SecilenSirketKimligi = sirket.SirketKimligi;
        talep.TeklifEdilenTutar = islemTutari;
        sirket.IsBaslat(hizmet.HizmetKimligi, hizmet.HizmetSurumu);

        IsIstegiMesaji isIstegi =
            new()
            {
                MesajTuru = MesajTurleri.IsIstegi,
                IstekKimligi = $"istek-{Guid.NewGuid():N}",
                IsKimligi = talep.IsKimligi,
                TickNumarasi = tickNumarasi,
                MusteriKimligi = talep.MusteriKimligi,
                HizmetKimligi = talep.HizmetKimligi,
                HizmetSurumu = talep.HizmetSurumu,
                TeklifEdilenTutar = islemTutari,
                ZamanAsimiMs = zamanAsimiMs,
                IstekVerisiJson = talep.IstekVerisiJson,
                OlusturulmaZamani = DateTimeOffset.UtcNow
            };

        try
        {
            IsSonucuMesaji sirketSonucu =
                await baglanti.IsIstegiGonderVeSonucuBekleAsync(
                    isIstegi,
                    cancellationToken);

            if (talep.KotuNiyetli)
            {
                return GuvenlikSinamasiniSonuclandir(
                    talep,
                    sirket,
                    sirketSonucu,
                    zamanAsimiMs);
            }

            SonucDogrulamaSonucu dogrulama =
                _sonucDogrulayicisi.Dogrula(talep, sirketSonucu);

            if (!dogrulama.Gecerli)
            {
                sirket.BasarisizIsKaydet(
                    "gecersiz-sonuc",
                    talep.ZorlukSeviyesi);
                sirket.CezaUygula(
                    IslemCezasiniHesapla(islemTutari));
                _musteriYoneticisi.IslemSonucunuKaydet(
                    talep,
                    false,
                    0,
                    sirketSonucu.IslemSuresiMs,
                    dogrulama.Aciklama);
                KonsolKayitcisi.Uyari(
                    $"Şirket sonucu doğrulanamadı | Şirket: {sirket.SirketAdi} | " +
                    $"İş: {talep.IsKimligi} | Zorluk: {talep.ZorlukSeviyesi} | " +
                    $"Sebep: {dogrulama.Aciklama}");
                return IsAtamaSonucu.BasarisizSonuc(
                    talep.IsKimligi,
                    sirket.SirketKimligi,
                    dogrulama.Aciklama,
                    sirketSonucu.IslemSuresiMs);
            }

            if (!_musteriYoneticisi.MusteridenOdemeAl(
                    musteri.MusteriKimligi,
                    islemTutari))
            {
                sirket.IsIptalEt();
                _musteriYoneticisi.IslemSonucunuKaydet(
                    talep,
                    false,
                    0,
                    sirketSonucu.IslemSuresiMs,
                    "Sonuç doğrulandı ancak müşteri ödemesi alınamadı.");
                return IsAtamaSonucu.BasarisizSonuc(
                    talep.IsKimligi,
                    sirket.SirketKimligi,
                    "Müşteri ödemesi alınamadı.",
                    sirketSonucu.IslemSuresiMs);
            }

            double memnuniyetPuani =
                MemnuniyetPuaniHesapla(
                    sirketSonucu.IslemSuresiMs,
                    zamanAsimiMs,
                    talep.ZorlukSeviyesi);

            sirket.BasariliIsKaydet(
                islemTutari,
                sirketSonucu.IslemSuresiMs,
                memnuniyetPuani,
                talep.ZorlukSeviyesi,
                zamanAsimiMs);
            _musteriYoneticisi.IslemSonucunuKaydet(
                talep,
                true,
                islemTutari,
                sirketSonucu.IslemSuresiMs,
                "İş motor tarafından doğrulandı ve ödeme tamamlandı.");

            KonsolKayitcisi.Basari(
                $"İş tamamlandı | İş: {talep.IsKimligi} | " +
                $"Şirket: {sirket.SirketAdi} | Hizmet: {talep.HizmetKimligi} | " +
                $"Zorluk: {talep.ZorlukSeviyesi} | Ödeme: {islemTutari:N2} | " +
                $"Süre: {sirketSonucu.IslemSuresiMs:N2} ms | " +
                $"Kalite: {sirket.KodKalitesiPuani:N1} | " +
                $"Performans: {sirket.PerformansPuani:N1}");

            return IsAtamaSonucu.BasariliSonuc(
                talep.IsKimligi,
                sirket.SirketKimligi,
                islemTutari,
                sirketSonucu.IslemSuresiMs,
                sirketSonucu.SonucVerisiJson);
        }
        catch (TimeoutException exception)
        {
            if (talep.KotuNiyetli)
            {
                decimal kayip =
                    decimal.Round(
                        talep.OlasiGuvenlikKaybi * 1.25m,
                        2);
                sirket.SaldiriBasariliKaydet(
                    kayip,
                    talep.ZorlukSeviyesi);
                _musteriYoneticisi.IslemSonucunuKaydet(
                    talep,
                    false,
                    0,
                    zamanAsimiMs,
                    "Kötü niyetli istek sunucuyu zaman aşımına uğrattı.");
                KonsolKayitcisi.Hata(
                    $"SALDIRI SUNUCUYU YORDU | Şirket: {sirket.SirketAdi} | " +
                    $"Tür: {talep.KotuNiyetTuru} | Kayıp: {kayip:N2}");
                return IsAtamaSonucu.GuvenlikSonucu(
                    talep.IsKimligi,
                    sirket.SirketKimligi,
                    false,
                    kayip,
                    zamanAsimiMs,
                    "Kötü niyetli istek zaman aşımı ve finansal kayıp oluşturdu.");
            }

            sirket.ZamanAsimiKaydet(talep.ZorlukSeviyesi);
            _musteriYoneticisi.IslemSonucunuKaydet(
                talep,
                false,
                0,
                0,
                "Şirket zaman aşımına uğradı.");
            KonsolKayitcisi.Uyari(
                $"İş zaman aşımı | Şirket: {sirket.SirketAdi} | " +
                $"İş: {talep.IsKimligi} | {exception.Message}");
            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                sirket.SirketKimligi,
                $"Zaman aşımı: {exception.Message}");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            sirket.IsIptalEt();
            throw;
        }
        catch (Exception exception)
        {
            if (talep.KotuNiyetli)
            {
                decimal kayip = talep.OlasiGuvenlikKaybi;
                sirket.SaldiriBasariliKaydet(
                    kayip,
                    talep.ZorlukSeviyesi);
                _musteriYoneticisi.IslemSonucunuKaydet(
                    talep,
                    false,
                    0,
                    0,
                    "Kötü niyetli istek sunucu hatasına yol açtı.");
                return IsAtamaSonucu.GuvenlikSonucu(
                    talep.IsKimligi,
                    sirket.SirketKimligi,
                    false,
                    kayip,
                    0,
                    "Saldırı sunucu hatası ve finansal kayıp oluşturdu.");
            }

            sirket.BasarisizIsKaydet(
                "sunucu-hatasi",
                talep.ZorlukSeviyesi);
            _musteriYoneticisi.IslemSonucunuKaydet(
                talep,
                false,
                0,
                0,
                exception.Message);
            KonsolKayitcisi.Uyari(
                $"İş başarısız | Şirket: {sirket.SirketAdi} | " +
                $"İş: {talep.IsKimligi} | Hata: {exception.Message}");
            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                sirket.SirketKimligi,
                exception.Message);
        }
    }

    private IsAtamaSonucu GuvenlikSinamasiniSonuclandir(
        HizmetTalebi talep,
        SirketKaydi sirket,
        IsSonucuMesaji sirketSonucu,
        int zamanAsimiMs)
    {
        if (GuvenlikReddiMi(sirketSonucu))
        {
            sirket.SaldiriEngellendiKaydet(
                talep.ZorlukSeviyesi,
                sirketSonucu.IslemSuresiMs,
                zamanAsimiMs);
            _musteriYoneticisi.IslemSonucunuKaydet(
                talep,
                false,
                0,
                sirketSonucu.IslemSuresiMs,
                "Şirket kötü niyetli isteği güvenli biçimde reddetti.");
            KonsolKayitcisi.Basari(
                $"SALDIRI ENGELLENDİ | Şirket: {sirket.SirketAdi} | " +
                $"Tür: {talep.KotuNiyetTuru} | Zorluk: {talep.ZorlukSeviyesi} | " +
                $"Güvenlik: {sirket.GuvenlikPuani:N1}");
            return IsAtamaSonucu.GuvenlikSonucu(
                talep.IsKimligi,
                sirket.SirketKimligi,
                true,
                0,
                sirketSonucu.IslemSuresiMs,
                "Kötü niyetli iş güvenli biçimde engellendi.");
        }

        decimal kayip = talep.OlasiGuvenlikKaybi;
        sirket.SaldiriBasariliKaydet(
            kayip,
            talep.ZorlukSeviyesi);
        _musteriYoneticisi.IslemSonucunuKaydet(
            talep,
            false,
            0,
            sirketSonucu.IslemSuresiMs,
            "Şirket kötü niyetli isteği fark edemedi.");
        KonsolKayitcisi.Hata(
            $"SALDIRI BAŞARILI | Şirket: {sirket.SirketAdi} | " +
            $"Tür: {talep.KotuNiyetTuru} | Kayıp: {kayip:N2} | " +
            $"Güvenlik: {sirket.GuvenlikPuani:N1}");
        return IsAtamaSonucu.GuvenlikSonucu(
            talep.IsKimligi,
            sirket.SirketKimligi,
            false,
            kayip,
            sirketSonucu.IslemSuresiMs,
            "Şirket saldırı isteğini normal iş gibi çalıştırdı.");
    }

    private IReadOnlyList<SirketAdayi> SirketAdaylariniOlustur(
        Musteri musteri,
        HizmetTalebi talep,
        decimal kullanilabilirButce)
    {
        List<(SirketBaglantisi Baglanti, SirketKaydi Sirket, SunulanHizmet Hizmet)>
            uygunlar = [];

        foreach (SirketBaglantisi baglanti in
                 _sirketYoneticisi.BaglantilariGetir())
        {
            SirketKaydi sirket = baglanti.Kayit;

            if (!baglanti.Bagli || !sirket.YeniIsAlabilirMi)
            {
                continue;
            }

            SunulanHizmet? hizmet =
                sirket.HizmetiBul(
                    talep.HizmetKimligi,
                    talep.HizmetSurumu);

            if (hizmet is null ||
                !hizmet.Aktif ||
                hizmet.BirimFiyat <= 0 ||
                hizmet.BirimFiyat > kullanilabilirButce ||
                !sirket.HizmetIcinKapasiteVarMi(
                    talep.HizmetKimligi,
                    talep.HizmetSurumu))
            {
                continue;
            }

            uygunlar.Add((baglanti, sirket, hizmet));
        }

        if (uygunlar.Count == 0)
        {
            return [];
        }

        decimal enUcuzFiyat =
            uygunlar.Min(aday => aday.Hizmet.BirimFiyat);
        List<SirketAdayi> puanliAdaylar = [];

        foreach ((SirketBaglantisi baglanti,
                  SirketKaydi sirket,
                  SunulanHizmet hizmet) in uygunlar)
        {
            double fiyatPuani =
                FiyatPuaniHesapla(enUcuzFiyat, hizmet.BirimFiyat);
            double itibarPuani = Sinirla(sirket.ItibarPuani, 0, 100);
            double guvenilirlikPuani =
                Sinirla(sirket.GuvenilirlikPuani, 0, 100);
            double kodKalitesiPuani =
                Sinirla(sirket.KodKalitesiPuani, 0, 100);
            double performansPuani =
                Sinirla(sirket.PerformansPuani, 0, 100);
            double guvenlikPuani =
                Sinirla(sirket.GuvenlikPuani, 0, 100);
            double hizPuani = Sinirla(sirket.HizPuaniHesapla(), 0, 100);
            double kapasitePuani =
                Sinirla(sirket.KapasitePuaniHesapla(hizmet), 0, 100);
            double sadakatPuani =
                string.Equals(
                    musteri.TercihEdilenSirketKimligi,
                    sirket.SirketKimligi,
                    StringComparison.OrdinalIgnoreCase)
                    ? 100
                    : 50;

            double toplamPuan =
                fiyatPuani * 0.10 +
                itibarPuani * 0.10 +
                guvenilirlikPuani * 0.13 +
                kodKalitesiPuani * 0.25 +
                performansPuani * 0.15 +
                hizPuani * 0.05 +
                guvenlikPuani * 0.12 +
                kapasitePuani * 0.05 +
                sadakatPuani * 0.05;

            puanliAdaylar.Add(
                new SirketAdayi
                {
                    Baglanti = baglanti,
                    Sirket = sirket,
                    Hizmet = hizmet,
                    FiyatPuani = fiyatPuani,
                    ItibarPuani = itibarPuani,
                    GuvenilirlikPuani = guvenilirlikPuani,
                    KodKalitesiPuani = kodKalitesiPuani,
                    PerformansPuani = performansPuani,
                    GuvenlikPuani = guvenlikPuani,
                    HizPuani = hizPuani,
                    KapasitePuani = kapasitePuani,
                    SadakatPuani = sadakatPuani,
                    ToplamPuan = toplamPuan
                });
        }

        return puanliAdaylar
            .OrderByDescending(aday => aday.ToplamPuan)
            .ThenBy(aday => aday.Hizmet.BirimFiyat)
            .Take(EnFazlaAdaySayisi)
            .ToList();
    }

    private SirketAdayi AgirlikliSirketSec(
        IReadOnlyList<SirketAdayi> adaylar,
        HizmetTalebi talep)
    {
        if (adaylar.Count == 0)
        {
            throw new InvalidOperationException(
                "Şirket seçimi için aday bulunmuyor.");
        }

        if (adaylar.Count == 1)
        {
            return adaylar[0];
        }

        double Agirlik(SirketAdayi aday)
        {
            if (!talep.KotuNiyetli)
            {
                return Math.Max(1, aday.ToplamPuan);
            }

            double acikPuani = 101 - aday.GuvenlikPuani;
            return Math.Max(
                1,
                acikPuani * 0.75 +
                (101 - aday.KodKalitesiPuani) * 0.20 +
                aday.KapasitePuani * 0.05);
        }

        double toplamAgirlik = adaylar.Sum(Agirlik);
        double secimDegeri;

        lock (_rastgeleKilidi)
        {
            secimDegeri = _rastgele.NextDouble() * toplamAgirlik;
        }

        double birikenAgirlik = 0;

        foreach (SirketAdayi aday in adaylar)
        {
            birikenAgirlik += Agirlik(aday);

            if (secimDegeri <= birikenAgirlik)
            {
                return aday;
            }
        }

        return adaylar[^1];
    }

    private static bool GuvenlikReddiMi(IsSonucuMesaji sonuc)
    {
        if (sonuc.Basarili)
        {
            return false;
        }

        string kod = sonuc.HataKodu?.Trim() ?? string.Empty;
        return kod.Equals(
                   "GUVENLIK_REDDI",
                   StringComparison.OrdinalIgnoreCase) ||
               kod.Equals(
                   "ISTEK_GUVENLI_DEGIL",
                   StringComparison.OrdinalIgnoreCase) ||
               kod.Equals(
                   "KOTU_NIYETLI_ISTEK",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static double FiyatPuaniHesapla(
        decimal enUcuzFiyat,
        decimal sirketFiyati)
    {
        if (enUcuzFiyat <= 0 || sirketFiyati <= 0)
        {
            return 0;
        }

        return Sinirla(
            (double)(enUcuzFiyat / sirketFiyati) * 100,
            0,
            100);
    }

    private static int ZamanAsiminiHesapla(HizmetTalebi talep)
    {
        return talep.ZamanAsimiMs > 0
            ? Math.Clamp(talep.ZamanAsimiMs, 500, 30_000)
            : VarsayilanZamanAsimiMs;
    }

    private static decimal IslemCezasiniHesapla(decimal islemTutari)
    {
        return decimal.Round(
            Math.Max(5, islemTutari * 0.20m),
            2);
    }

    private static double MemnuniyetPuaniHesapla(
        double islemSuresiMs,
        int zamanAsimiMs,
        int zorlukSeviyesi)
    {
        if (zamanAsimiMs <= 0)
        {
            return 40;
        }

        double oran =
            Math.Clamp(islemSuresiMs / zamanAsimiMs, 0, 1);
        double puan =
            100 - Math.Pow(oran, 0.60) * 100;
        puan -= Math.Max(0, zorlukSeviyesi - 3) * 1.5;
        return Math.Clamp(puan, 0, 100);
    }

    private static double Sinirla(
        double deger,
        double altSinir,
        double ustSinir)
    {
        if (double.IsNaN(deger) || double.IsInfinity(deger))
        {
            return altSinir;
        }

        return Math.Clamp(deger, altSinir, ustSinir);
    }

    private static void OzetGuncelle(
        IsIslemeOzeti ozet,
        IsAtamaSonucu sonuc)
    {
        if (sonuc.GuvenlikOlayi)
        {
            if (sonuc.SaldiriEngellendi)
            {
                ozet.EngellenenSaldiriSayisi++;
            }
            else
            {
                ozet.BasariliSaldiriSayisi++;
                ozet.ToplamGuvenlikKaybi += sonuc.GuvenlikKaybi;
            }

            ozet.BasarisizIsSayisi++;
            ozet.ToplamIslemSuresiMs +=
                Math.Max(0, sonuc.IslemSuresiMs);
            return;
        }

        if (sonuc.Basarili)
        {
            ozet.BasariliIsSayisi++;
            ozet.ToplamCiro += sonuc.IslemTutari;
            ozet.ToplamIslemSuresiMs +=
                Math.Max(0, sonuc.IslemSuresiMs);
            return;
        }

        ozet.BasarisizIsSayisi++;

        if (sonuc.SonucAciklamasi.Contains(
                "zaman aşımı",
                StringComparison.OrdinalIgnoreCase))
        {
            ozet.ZamanAsimiSayisi++;
        }

        if (sonuc.SonucAciklamasi.Contains(
                "uygun şirket",
                StringComparison.OrdinalIgnoreCase))
        {
            ozet.SirketBulunamayanIsSayisi++;
        }

        if (sonuc.SonucAciklamasi.Contains(
                "bakiye",
                StringComparison.OrdinalIgnoreCase) ||
            sonuc.SonucAciklamasi.Contains(
                "bütçe",
                StringComparison.OrdinalIgnoreCase))
        {
            ozet.ButceYetersizIsSayisi++;
        }
    }

    private static void TickOzetiniYaz(IsIslemeOzeti ozet)
    {
        KonsolKayitcisi.Bilgi(
            $"İş tick özeti | Tick: {ozet.TickNumarasi} | " +
            $"Talep: {ozet.ToplamTalepSayisi} | " +
            $"Başarılı: {ozet.BasariliIsSayisi} | " +
            $"Başarısız: {ozet.BasarisizIsSayisi} | " +
            $"Şüpheli: {ozet.KotuNiyetliIsSayisi} | " +
            $"Engellenen saldırı: {ozet.EngellenenSaldiriSayisi} | " +
            $"Başarılı saldırı: {ozet.BasariliSaldiriSayisi} | " +
            $"Güvenlik kaybı: {ozet.ToplamGuvenlikKaybi:N2} | " +
            $"Zaman aşımı: {ozet.ZamanAsimiSayisi} | " +
            $"Şirket yok: {ozet.SirketBulunamayanIsSayisi} | " +
            $"Ciro: {ozet.ToplamCiro:N2} | " +
            $"Başarı oranı: {ozet.BasariOrani:N2}%");
    }
}
