using SirketMotoru.Ag;
using SirketMotoru.Ayarlar;
using SirketMotoru.Hizmetler;
using SirketMotoru.Kayit;

namespace SirketMotoru.Sirketler;

public sealed class SirketYoneticisi : IAsyncDisposable
{
    private readonly List<SirketBaglantisi> _baglantilar;

    private readonly HizmetKatalogu _hizmetKatalogu;

    private readonly SirketBilancoVeritabani
        _bilancoVeritabani;

    public IReadOnlyCollection<SirketKaydi> SirketKayitlari =>
        _baglantilar
            .Select(baglanti => baglanti.Kayit)
            .ToList()
            .AsReadOnly();

    public SirketYoneticisi(
        MotorAyarlari motorAyarlari,
        HizmetKatalogu hizmetKatalogu)
    {
        ArgumentNullException.ThrowIfNull(
            motorAyarlari);

        ArgumentNullException.ThrowIfNull(
            hizmetKatalogu);

        _hizmetKatalogu =
            hizmetKatalogu;

        _baglantilar =
            motorAyarlari.Sirketler
                .Select(
                    sirketAyari =>
                        new SirketBaglantisi(
                            motorAyarlari,
                            sirketAyari))
                .ToList();

        _bilancoVeritabani =
            new SirketBilancoVeritabani();

        IReadOnlyDictionary<string, SirketBilancoKaydi>
            kaliciBilancolar =
                _bilancoVeritabani.Yukle();

        int yuklenenBilancoSayisi =
            0;

        foreach (SirketBaglantisi baglanti in
                 _baglantilar)
        {
            if (!kaliciBilancolar.TryGetValue(
                    baglanti.Kayit.SirketKimligi,
                    out SirketBilancoKaydi? bilanco))
            {
                continue;
            }

            bilanco.Uygula(
                baglanti.Kayit);

            yuklenenBilancoSayisi++;
        }

        KonsolKayitcisi.Bilgi(
            $"Şirket bilanço veritabanı: " +
            $"{_bilancoVeritabani.DosyaYolu} | " +
            $"Yüklenen kayıt: {yuklenenBilancoSayisi}");
    }

    public IReadOnlyList<SirketBaglantisi>
        BaglantilariGetir()
    {
        return _baglantilar
            .ToList()
            .AsReadOnly();
    }

    public async Task IlkBaglantilariKurAsync(
        CancellationToken cancellationToken)
    {
        KonsolKayitcisi.Bilgi(
            $"{_baglantilar.Count} şirkete " +
            "bağlantı kurulacak.");

        foreach (SirketBaglantisi baglanti in
                 _baglantilar)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            bool baglandi =
                await baglanti.BaglanVeKaydetAsync(
                    cancellationToken);

            if (baglandi)
            {
                await FinansDurumunuGuvenliGonderAsync(
                    baglanti,
                    tickNumarasi:
                        0,
                    cancellationToken);
            }
        }

        await BilancolariKaydetAsync(
            cancellationToken);

        SirketDurumOzetiniYazdir();
    }

    public async Task TickCalistirAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        foreach (SirketBaglantisi baglanti in
                 _baglantilar)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (!baglanti.Bagli)
            {
                KonsolKayitcisi.Bilgi(
                    $"{baglanti.Kayit.SirketAdi} için " +
                    "yeniden bağlantı deneniyor.");

                bool baglandi =
                    await baglanti.BaglanVeKaydetAsync(
                        cancellationToken);

                if (!baglandi)
                {
                    continue;
                }
            }

            bool saglikli =
                await baglanti.SaglikKontrolEtAsync(
                    tickNumarasi,
                    cancellationToken);

            if (saglikli)
            {
                await FinansDurumunuGuvenliGonderAsync(
                    baglanti,
                    tickNumarasi,
                    cancellationToken);
            }
        }

        /*
         * Bu kayıt önceki tickte tamamlanan işlerin finansal
         * etkilerini kalıcı hâle getirir. Tick sonunda da
         * TickYoneticisi tarafından tekrar çağrılır.
         */
        await BilancolariKaydetAsync(
            cancellationToken);

        if (tickNumarasi % 5 == 0)
        {
            SirketDurumOzetiniYazdir();
        }
    }

    public async Task BilancolariKaydetVeYayinlaAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        await BilancolariKaydetAsync(
            cancellationToken);

        foreach (SirketBaglantisi baglanti in
                 _baglantilar.Where(
                     baglanti => baglanti.Bagli))
        {
            await FinansDurumunuGuvenliGonderAsync(
                baglanti,
                tickNumarasi,
                cancellationToken);
        }
    }

    public async Task BilancolariKaydetAsync(
        CancellationToken cancellationToken)
    {
        await _bilancoVeritabani.KaydetAsync(
            SirketKayitlari,
            cancellationToken);
    }

    public IReadOnlyList<SirketKaydi>
        HizmetSunabilenSirketleriBul(
            string hizmetKimligi,
            string hizmetSurumu)
    {
        if (!_hizmetKatalogu.HizmetVarMi(
                hizmetKimligi,
                hizmetSurumu))
        {
            return [];
        }

        return _baglantilar
            .Select(baglanti => baglanti.Kayit)
            .Where(
                kayit =>
                    kayit.Durum is
                        SirketDurumu.Bagli or
                        SirketDurumu.Calisiyor)
            .Where(
                kayit =>
                    kayit.HizmetSunuyorMu(
                        hizmetKimligi,
                        hizmetSurumu))
            .OrderBy(
                kayit => kayit.SonGecikmeMs)
            .ThenBy(
                kayit => kayit.KuyrukUzunlugu)
            .ToList()
            .AsReadOnly();
    }

    public SirketKaydi? EnUygunSirketiBul(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        return HizmetSunabilenSirketleriBul(
                hizmetKimligi,
                hizmetSurumu)
            .FirstOrDefault();
    }

    private async Task FinansDurumunuGuvenliGonderAsync(
        SirketBaglantisi baglanti,
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        try
        {
            await baglanti.FinansDurumuGonderAsync(
                tickNumarasi,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            KonsolKayitcisi.Uyari(
                $"Finans durumu gönderilemedi | " +
                $"Şirket: {baglanti.Kayit.SirketAdi} | " +
                $"Hata: {exception.Message}");
        }
    }

    private void SirketDurumOzetiniYazdir()
    {
        int bagliSirketSayisi =
            _baglantilar.Count(
                baglanti =>
                    baglanti.Kayit.Durum is
                        SirketDurumu.Bagli or
                        SirketDurumu.Calisiyor);

        int toplamHizmetBildirimi =
            _baglantilar.Sum(
                baglanti =>
                    baglanti.Kayit.Hizmetler.Count);

        decimal toplamSirketKasasi =
            _baglantilar.Sum(
                baglanti =>
                    baglanti.Kayit.Kasa);

        KonsolKayitcisi.Bilgi(
            $"Şirket özeti | " +
            $"Bağlı: {bagliSirketSayisi}/" +
            $"{_baglantilar.Count} | " +
            $"Geçerli hizmet bildirimi: " +
            $"{toplamHizmetBildirimi} | " +
            $"Toplam şirket kasası: " +
            $"{toplamSirketKasasi:N2}");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await BilancolariKaydetAsync(
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            KonsolKayitcisi.Hata(
                $"Motor kapanırken şirket bilançoları " +
                $"kaydedilemedi: {exception.Message}");
        }

        foreach (SirketBaglantisi baglanti in
                 _baglantilar)
        {
            await baglanti.DisposeAsync();
        }
    }
}
