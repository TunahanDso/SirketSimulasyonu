using SirketMotoru.Ag;
using SirketMotoru.Ayarlar;
using SirketMotoru.Kayit;

namespace SirketMotoru.Sirketler;

public sealed class SirketYoneticisi : IAsyncDisposable
{
    private readonly List<SirketBaglantisi> _baglantilar;

    public SirketYoneticisi(MotorAyarlari motorAyarlari)
    {
        _baglantilar = motorAyarlari.Sirketler
            .Select(sirketAyari =>
                new SirketBaglantisi(
                    motorAyarlari,
                    sirketAyari))
            .ToList();
    }

    public async Task IlkBaglantilariKurAsync(
        CancellationToken cancellationToken)
    {
        KonsolKayitcisi.Bilgi(
            $"{_baglantilar.Count} şirkete bağlantı kurulacak.");

        foreach (SirketBaglantisi baglanti in _baglantilar)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await baglanti.BaglanVeKaydetAsync(
                cancellationToken);
        }
    }

    public async Task TickCalistirAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        foreach (SirketBaglantisi baglanti in _baglantilar)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!baglanti.Bagli)
            {
                KonsolKayitcisi.Bilgi(
                    $"{baglanti.Kayit.SirketAdi} için yeniden bağlantı deneniyor.");

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
    }

    public async ValueTask DisposeAsync()
    {
        foreach (SirketBaglantisi baglanti in _baglantilar)
        {
            await baglanti.DisposeAsync();
        }
    }
}