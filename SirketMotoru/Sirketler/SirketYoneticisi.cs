using SirketMotoru.Ag;
using SirketMotoru.Ayarlar;
using SirketMotoru.Hizmetler;
using SirketMotoru.Kayit;

namespace SirketMotoru.Sirketler;

public sealed class SirketYoneticisi : IAsyncDisposable
{
    private readonly List<SirketBaglantisi> _baglantilar;

    private readonly HizmetKatalogu _hizmetKatalogu;

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
                            sirketAyari,
                            hizmetKatalogu))
                .ToList();
    }

    public async Task IlkBaglantilariKurAsync(
        CancellationToken cancellationToken)
    {
        KonsolKayitcisi.Bilgi(
            $"{_baglantilar.Count} şirkete " +
            $"bağlantı kurulacak.");

        foreach (SirketBaglantisi baglanti in
                 _baglantilar)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            await baglanti.BaglanVeKaydetAsync(
                cancellationToken);
        }

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
                    $"yeniden bağlantı deneniyor.");

                bool baglandi =
                    await baglanti.BaglanVeKaydetAsync(
                        cancellationToken);

                if (!baglandi)
                {
                    continue;
                }
            }

            await baglanti.SaglikKontrolEtAsync(
                tickNumarasi,
                cancellationToken);
        }

        if (tickNumarasi % 5 == 0)
        {
            SirketDurumOzetiniYazdir();
        }
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

        KonsolKayitcisi.Bilgi(
            $"Şirket özeti | " +
            $"Bağlı: {bagliSirketSayisi}/" +
            $"{_baglantilar.Count} | " +
            $"Geçerli hizmet bildirimi: " +
            $"{toplamHizmetBildirimi}");
    }

    public async ValueTask DisposeAsync()
    {
        foreach (SirketBaglantisi baglanti in
                 _baglantilar)
        {
            await baglanti.DisposeAsync();
        }
    }
}