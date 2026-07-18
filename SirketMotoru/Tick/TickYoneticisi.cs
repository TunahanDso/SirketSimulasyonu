using SirketMotoru.Ayarlar;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Tick;

public sealed class TickYoneticisi
{
    private readonly MotorAyarlari _ayarlar;
    private readonly SirketYoneticisi _sirketYoneticisi;

    private long _tickNumarasi;

    public TickYoneticisi(
        MotorAyarlari ayarlar,
        SirketYoneticisi sirketYoneticisi)
    {
        _ayarlar = ayarlar;
        _sirketYoneticisi = sirketYoneticisi;
    }

    public async Task BaslatAsync(
        CancellationToken cancellationToken)
    {
        KonsolKayitcisi.Bilgi(
            $"Tick sistemi başlatıldı. " +
            $"Tick süresi: {_ayarlar.TickSuresiSaniye} saniye.");

        while (!cancellationToken.IsCancellationRequested)
        {
            _tickNumarasi++;

            KonsolKayitcisi.Tick(_tickNumarasi);

            await _sirketYoneticisi.TickCalistirAsync(
                _tickNumarasi,
                cancellationToken);

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        _ayarlar.TickSuresiSaniye),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        KonsolKayitcisi.Bilgi(
            "Tick sistemi durduruldu.");
    }
}