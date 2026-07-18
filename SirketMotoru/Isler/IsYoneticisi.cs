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

    private readonly ConcurrentDictionary<string, byte>
        _islenenIsler =
            new(StringComparer.OrdinalIgnoreCase);

    private readonly Random _rastgele =
        new();

    private readonly object _rastgeleKilidi =
        new();

    public IsYoneticisi(
        SirketYoneticisi sirketYoneticisi,
        MusteriYoneticisi musteriYoneticisi,
        SonucDogrulayicisi sonucDogrulayicisi)
    {
        ArgumentNullException.ThrowIfNull(
            sirketYoneticisi);

        ArgumentNullException.ThrowIfNull(
            musteriYoneticisi);

        ArgumentNullException.ThrowIfNull(
            sonucDogrulayicisi);

        _sirketYoneticisi =
            sirketYoneticisi;

        _musteriYoneticisi =
            musteriYoneticisi;

        _sonucDogrulayicisi =
            sonucDogrulayicisi;
    }

    public async Task<IsIslemeOzeti>
        TalepleriIsleAsync(
            long tickNumarasi,
            IReadOnlyList<HizmetTalebi> talepler,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            talepler);

        IsIslemeOzeti ozet =
            new()
            {
                TickNumarasi =
                    tickNumarasi,

                ToplamTalepSayisi =
                    talepler.Count
            };

        if (talepler.Count == 0)
        {
            return ozet;
        }

        /*
         * Aynı şirkete eş zamanlı çok sayıda iş yollamamak ve
         * minimal sürümde akışı kolay takip etmek için işler
         * şimdilik sırayla işleniyor.
         *
         * İleride kontrollü paralel işleme geçirilebilir.
         */
        foreach (HizmetTalebi talep in talepler)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            IsAtamaSonucu sonuc =
                await TalebiIsleAsync(
                    tickNumarasi,
                    talep,
                    cancellationToken);

            OzetGuncelle(
                ozet,
                sonuc);
        }

        TickOzetiniYaz(
            ozet);

        return ozet;
    }

    public async Task<IsAtamaSonucu>
        TalebiIsleAsync(
            long tickNumarasi,
            HizmetTalebi talep,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            talep);

        cancellationToken
            .ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(
                talep.IsKimligi))
        {
            return IsAtamaSonucu.BasarisizSonuc(
                string.Empty,
                null,
                "Talebin iş kimliği bulunmuyor.");
        }

        if (!_islenenIsler.TryAdd(
                talep.IsKimligi,
                0))
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
                $"İş: {talep.IsKimligi} | " +
                $"Hata: {exception.Message}");

            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                talep.SecilenSirketKimligi,
                exception.Message);
        }
        finally
        {
            /*
             * İş kimliği bu Motor çalışması boyunca yeniden
             * işlenmesin diye sözlükten silinmiyor.
             *
             * Böylece aynı talep yanlışlıkla iki tick içinde
             * tekrar ödeme oluşturamaz.
             */
        }
    }

    private async Task<IsAtamaSonucu>
        TalebiIsleIcAsync(
            long tickNumarasi,
            HizmetTalebi talep,
            CancellationToken cancellationToken)
    {
        MusteriKaydi? musteri =
            _musteriYoneticisi.MusteriyiBul(
                talep.MusteriKimligi);

        if (musteri is null)
        {
            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                null,
                $"Müşteri bulunamadı: " +
                $"{talep.MusteriKimligi}");
        }

        if (!musteri.Aktif)
        {
            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                null,
                "Müşteri hesabı aktif değil.");
        }

        decimal kullanilabilirButce =
            Math.Min(
                talep.AzamiButce,
                musteri.Bakiye);

        if (kullanilabilirButce <= 0)
        {
            _musteriYoneticisi.TalepBasarisizKaydet(
                musteri,
                talep,
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
            _musteriYoneticisi.TalepBasarisizKaydet(
                musteri,
                talep,
                "Uygun şirket bulunamadı.");

            KonsolKayitcisi.Uyari(
                $"İşe uygun şirket bulunamadı | " +
                $"İş: {talep.IsKimligi} | " +
                $"Hizmet: {talep.HizmetKimligi}@" +
                $"{talep.HizmetSurumu} | " +
                $"Bütçe: {kullanilabilirButce:N2}");

            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                null,
                "Hizmeti sunabilen uygun şirket bulunamadı.");
        }

        SirketAdayi secilenAday =
            AgirlikliSirketSec(
                adaylar);

        SirketKaydi sirket =
            secilenAday.Sirket;

        SunulanHizmet hizmet =
            secilenAday.Hizmet;

        SirketBaglantisi baglanti =
            secilenAday.Baglanti;

        decimal islemTutari =
            hizmet.BirimFiyat;

        talep.SecilenSirketKimligi =
            sirket.SirketKimligi;

        talep.TeklifEdilenTutar =
            islemTutari;

        if (!_musteriYoneticisi
            .OdemeIcinBakiyeAyir(
                musteri,
                talep,
                islemTutari))
        {
            _musteriYoneticisi.TalepBasarisizKaydet(
                musteri,
                talep,
                "Ödeme için bakiye ayrılamadı.");

            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                sirket.SirketKimligi,
                "Müşteri bakiyesi ödeme için ayrılamadı.");
        }

        sirket.IsBaslat(
            hizmet.HizmetKimligi,
            hizmet.HizmetSurumu,
            islemTutari);

        IsIstegiMesaji isIstegi =
            new()
            {
                MesajTuru =
                    MesajTurleri.IsIstegi,

                IstekKimligi =
                    $"istek-{Guid.NewGuid():N}",

                IsKimligi =
                    talep.IsKimligi,

                TickNumarasi =
                    tickNumarasi,

                MusteriKimligi =
                    talep.MusteriKimligi,

                HizmetKimligi =
                    talep.HizmetKimligi,

                HizmetSurumu =
                    talep.HizmetSurumu,

                TeklifEdilenTutar =
                    islemTutari,

                ZamanAsimiMs =
                    ZamanAsiminiHesapla(
                        talep),

                IstekVerisiJson =
                    talep.IstekVerisiJson,

                OlusturulmaZamani =
                    DateTimeOffset.UtcNow
            };

        try
        {
            IsSonucuMesaji sirketSonucu =
                await baglanti
                    .IsIstegiGonderVeSonucuBekleAsync(
                        isIstegi,
                        cancellationToken);

            SonucDogrulamaSonucu dogrulama =
                _sonucDogrulayicisi.Dogrula(
                    talep,
                    sirketSonucu);

            if (!dogrulama.Gecerli)
            {
                sirket.BasarisizIsKaydet(
                    sirketSonucu.IslemSuresiMs);

                sirket.CezaUygula(
                    IslemCezasiniHesapla(
                        islemTutari));

                _musteriYoneticisi
                    .AyrilanBakiyeyiIadeEt(
                        musteri,
                        talep,
                        islemTutari);

                _musteriYoneticisi
                    .TalepBasarisizKaydet(
                        musteri,
                        talep,
                        dogrulama.Aciklama);

                KonsolKayitcisi.Uyari(
                    $"Şirket sonucu doğrulanamadı | " +
                    $"Şirket: {sirket.SirketAdi} | " +
                    $"İş: {talep.IsKimligi} | " +
                    $"Sebep: {dogrulama.Aciklama}");

                return IsAtamaSonucu.BasarisizSonuc(
                    talep.IsKimligi,
                    sirket.SirketKimligi,
                    dogrulama.Aciklama,
                    sirketSonucu.IslemSuresiMs);
            }

            /*
             * Sonuç doğrulandığı için ayrılan bakiye kesin
             * harcamaya dönüştürülür ve şirket parasını alır.
             */
            _musteriYoneticisi.OdemeyiTamamla(
                musteri,
                talep,
                sirket.SirketKimligi,
                islemTutari,
                sirketSonucu.SonucVerisiJson);

            sirket.BasariliIsKaydet(
                islemTutari,
                sirketSonucu.IslemSuresiMs);

            _musteriYoneticisi.SadakatiGuncelle(
                musteri,
                sirket.SirketKimligi,
                basarili:
                    true);

            KonsolKayitcisi.Basari(
                $"İş tamamlandı | " +
                $"İş: {talep.IsKimligi} | " +
                $"Müşteri: {musteri.MusteriAdi} | " +
                $"Şirket: {sirket.SirketAdi} | " +
                $"Hizmet: {talep.HizmetKimligi} | " +
                $"Ödeme: {islemTutari:N2} | " +
                $"Süre: " +
                $"{sirketSonucu.IslemSuresiMs:N2} ms");

            return IsAtamaSonucu.BasariliSonuc(
                talep.IsKimligi,
                sirket.SirketKimligi,
                islemTutari,
                sirketSonucu.IslemSuresiMs,
                sirketSonucu.SonucVerisiJson);
        }
        catch (TimeoutException exception)
        {
            sirket.ZamanAsimiKaydet();

            _musteriYoneticisi
                .AyrilanBakiyeyiIadeEt(
                    musteri,
                    talep,
                    islemTutari);

            _musteriYoneticisi
                .TalepBasarisizKaydet(
                    musteri,
                    talep,
                    "Şirket zaman aşımına uğradı.");

            _musteriYoneticisi.SadakatiGuncelle(
                musteri,
                sirket.SirketKimligi,
                basarili:
                    false);

            KonsolKayitcisi.Uyari(
                $"İş zaman aşımı | " +
                $"Şirket: {sirket.SirketAdi} | " +
                $"İş: {talep.IsKimligi} | " +
                $"{exception.Message}");

            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                sirket.SirketKimligi,
                $"Zaman aşımı: {exception.Message}");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            sirket.IsIptalEt();

            _musteriYoneticisi
                .AyrilanBakiyeyiIadeEt(
                    musteri,
                    talep,
                    islemTutari);

            throw;
        }
        catch (Exception exception)
        {
            sirket.BasarisizIsKaydet(
                0);

            _musteriYoneticisi
                .AyrilanBakiyeyiIadeEt(
                    musteri,
                    talep,
                    islemTutari);

            _musteriYoneticisi
                .TalepBasarisizKaydet(
                    musteri,
                    talep,
                    exception.Message);

            _musteriYoneticisi.SadakatiGuncelle(
                musteri,
                sirket.SirketKimligi,
                basarili:
                    false);

            KonsolKayitcisi.Uyari(
                $"İş başarısız | " +
                $"Şirket: {sirket.SirketAdi} | " +
                $"İş: {talep.IsKimligi} | " +
                $"Hata: {exception.Message}");

            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                sirket.SirketKimligi,
                exception.Message);
        }
    }

    private IReadOnlyList<SirketAdayi>
        SirketAdaylariniOlustur(
            MusteriKaydi musteri,
            HizmetTalebi talep,
            decimal kullanilabilirButce)
    {
        List<(SirketBaglantisi Baglanti,
              SirketKaydi Sirket,
              SunulanHizmet Hizmet)> uygunlar =
            [];

        foreach (SirketBaglantisi baglanti in
                 _sirketYoneticisi
                     .BaglantilariGetir())
        {
            SirketKaydi sirket =
                baglanti.Kayit;

            if (!baglanti.Bagli ||
                !sirket.YeniIsAlabilirMi)
            {
                continue;
            }

            SunulanHizmet? hizmet =
                sirket.HizmetiBul(
                    talep.HizmetKimligi,
                    talep.HizmetSurumu);

            if (hizmet is null ||
                !hizmet.Aktif)
            {
                continue;
            }

            if (hizmet.BirimFiyat <= 0 ||
                hizmet.BirimFiyat >
                kullanilabilirButce)
            {
                continue;
            }

            if (!sirket.HizmetIcinKapasiteVarMi(
                    talep.HizmetKimligi,
                    talep.HizmetSurumu))
            {
                continue;
            }

            uygunlar.Add(
                (
                    baglanti,
                    sirket,
                    hizmet
                ));
        }

        if (uygunlar.Count == 0)
        {
            return [];
        }

        decimal enUcuzFiyat =
            uygunlar.Min(
                aday => aday.Hizmet.BirimFiyat);

        List<SirketAdayi> puanliAdaylar =
            [];

        foreach ((SirketBaglantisi baglanti,
                  SirketKaydi sirket,
                  SunulanHizmet hizmet)
                 in uygunlar)
        {
            double fiyatPuani =
                FiyatPuaniHesapla(
                    enUcuzFiyat,
                    hizmet.BirimFiyat);

            double itibarPuani =
                Sinirla(
                    sirket.ItibarPuani,
                    0,
                    100);

            double guvenilirlikPuani =
                Sinirla(
                    sirket.GuvenilirlikPuani,
                    0,
                    100);

            double hizPuani =
                Sinirla(
                    sirket.HizPuaniHesapla(),
                    0,
                    100);

            double kapasitePuani =
                Sinirla(
                    sirket.KapasitePuaniHesapla(),
                    0,
                    100);

            double sadakatPuani =
                Sinirla(
                    _musteriYoneticisi
                        .SirketSadakatPuaniGetir(
                            musteri,
                            sirket.SirketKimligi),
                    0,
                    100);

            /*
             * Fiyat tek başına oyunu yönetmesin.
             *
             * Ucuz ama güvensiz bir şirket iş alabilir;
             * fakat sürekli kazanamaz.
             */
            double toplamPuan =
                fiyatPuani * 0.25 +
                itibarPuani * 0.20 +
                guvenilirlikPuani * 0.25 +
                hizPuani * 0.15 +
                kapasitePuani * 0.10 +
                sadakatPuani * 0.05;

            puanliAdaylar.Add(
                new SirketAdayi
                {
                    Baglanti =
                        baglanti,

                    Sirket =
                        sirket,

                    Hizmet =
                        hizmet,

                    FiyatPuani =
                        fiyatPuani,

                    ItibarPuani =
                        itibarPuani,

                    GuvenilirlikPuani =
                        guvenilirlikPuani,

                    HizPuani =
                        hizPuani,

                    KapasitePuani =
                        kapasitePuani,

                    SadakatPuani =
                        sadakatPuani,

                    ToplamPuan =
                        toplamPuan
                });
        }

        return puanliAdaylar
            .OrderByDescending(
                aday => aday.ToplamPuan)
            .ThenBy(
                aday => aday.Hizmet.BirimFiyat)
            .Take(
                EnFazlaAdaySayisi)
            .ToList();
    }

    private SirketAdayi AgirlikliSirketSec(
        IReadOnlyList<SirketAdayi> adaylar)
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

        double toplamAgirlik =
            adaylar.Sum(
                aday =>
                    Math.Max(
                        1,
                        aday.ToplamPuan));

        double secimDegeri;

        lock (_rastgeleKilidi)
        {
            secimDegeri =
                _rastgele.NextDouble() *
                toplamAgirlik;
        }

        double birikenAgirlik = 0;

        foreach (SirketAdayi aday in
                 adaylar)
        {
            birikenAgirlik +=
                Math.Max(
                    1,
                    aday.ToplamPuan);

            if (secimDegeri <=
                birikenAgirlik)
            {
                return aday;
            }
        }

        return adaylar[^1];
    }

    private static double FiyatPuaniHesapla(
        decimal enUcuzFiyat,
        decimal sirketFiyati)
    {
        if (enUcuzFiyat <= 0 ||
            sirketFiyati <= 0)
        {
            return 0;
        }

        double oran =
            (double)(
                enUcuzFiyat /
                sirketFiyati);

        return Sinirla(
            oran * 100,
            0,
            100);
    }

    private static int ZamanAsiminiHesapla(
        HizmetTalebi talep)
    {
        if (talep.ZamanAsimiMs > 0)
        {
            return Math.Clamp(
                talep.ZamanAsimiMs,
                500,
                30_000);
        }

        return VarsayilanZamanAsimiMs;
    }

    private static decimal IslemCezasiniHesapla(
        decimal islemTutari)
    {
        return decimal.Round(
            Math.Max(
                1,
                islemTutari * 0.10m),
            2);
    }

    private static double Sinirla(
        double deger,
        double altSinir,
        double ustSinir)
    {
        if (double.IsNaN(deger) ||
            double.IsInfinity(deger))
        {
            return altSinir;
        }

        return Math.Clamp(
            deger,
            altSinir,
            ustSinir);
    }

    private static void OzetGuncelle(
        IsIslemeOzeti ozet,
        IsAtamaSonucu sonuc)
    {
        if (sonuc.Basarili)
        {
            ozet.BasariliIsSayisi++;

            ozet.ToplamCiro +=
                sonuc.IslemTutari;

            ozet.ToplamIslemSuresiMs +=
                Math.Max(
                    0,
                    sonuc.IslemSuresiMs);

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

    private static void TickOzetiniYaz(
        IsIslemeOzeti ozet)
    {
        KonsolKayitcisi.Bilgi(
            $"İş tick özeti | " +
            $"Tick: {ozet.TickNumarasi} | " +
            $"Talep: {ozet.ToplamTalepSayisi} | " +
            $"Başarılı: {ozet.BasariliIsSayisi} | " +
            $"Başarısız: {ozet.BasarisizIsSayisi} | " +
            $"Zaman aşımı: {ozet.ZamanAsimiSayisi} | " +
            $"Şirket yok: " +
            $"{ozet.SirketBulunamayanIsSayisi} | " +
            $"Ciro: {ozet.ToplamCiro:N2} | " +
            $"Başarı oranı: {ozet.BasariOrani:N2}%");
    }
}