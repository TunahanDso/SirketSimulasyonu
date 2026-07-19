using System.Diagnostics;
using SirketMotoru.Ayarlar;
using SirketMotoru.CanliPano;
using SirketMotoru.Isler;
using SirketMotoru.Isletim;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Tick;

public sealed class TickYoneticisi
{
    private readonly MotorAyarlari _ayarlar;
    private readonly SirketYoneticisi _sirketYoneticisi;
    private readonly MusteriYoneticisi _musteriYoneticisi;
    private readonly SirketIsletimYoneticisi _sirketIsletimYoneticisi;
    private readonly IsYoneticisi _isYoneticisi;
    private long _tickNumarasi;
    private bool _calisiyor;

    public long TickNumarasi => Interlocked.Read(ref _tickNumarasi);
    public bool Calisiyor => _calisiyor;

    public TickYoneticisi(
        MotorAyarlari ayarlar,
        SirketYoneticisi sirketYoneticisi,
        MusteriYoneticisi musteriYoneticisi,
        SirketIsletimYoneticisi sirketIsletimYoneticisi)
    {
        ArgumentNullException.ThrowIfNull(ayarlar);
        ArgumentNullException.ThrowIfNull(sirketYoneticisi);
        ArgumentNullException.ThrowIfNull(musteriYoneticisi);
        ArgumentNullException.ThrowIfNull(sirketIsletimYoneticisi);

        if (ayarlar.TickSuresiSaniye <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ayarlar),
                "Tick süresi sıfırdan büyük olmalıdır.");
        }

        _ayarlar = ayarlar;
        _sirketYoneticisi = sirketYoneticisi;
        _musteriYoneticisi = musteriYoneticisi;
        _sirketIsletimYoneticisi = sirketIsletimYoneticisi;
        _isYoneticisi = new IsYoneticisi(
            sirketYoneticisi,
            musteriYoneticisi,
            new SonucDogrulayicisi());
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        if (_calisiyor)
        {
            throw new InvalidOperationException(
                "Tick sistemi zaten çalışıyor.");
        }

        _calisiyor = true;
        KonsolKayitcisi.Bilgi(
            $"Tick sistemi başlatıldı. Tick süresi: " +
            $"{_ayarlar.TickSuresiSaniye} saniye.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                long tickNumarasi =
                    Interlocked.Increment(ref _tickNumarasi);
                Stopwatch tickKronometresi = Stopwatch.StartNew();
                KonsolKayitcisi.Tick(tickNumarasi);

                try
                {
                    await TickCalistirAsync(
                        tickNumarasi,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    KonsolKayitcisi.Hata(
                        $"Tick {tickNumarasi} çalıştırılırken hata oluştu: " +
                        exception.Message);
                    KonsolKayitcisi.Hata(exception.ToString());
                }
                finally
                {
                    tickKronometresi.Stop();
                }

                KonsolKayitcisi.Bilgi(
                    $"Tick {tickNumarasi} tamamlandı. İşlem süresi: " +
                    $"{tickKronometresi.Elapsed.TotalMilliseconds:N2} ms.");

                TimeSpan beklemeSuresi =
                    TickBeklemeSuresiniHesapla(tickKronometresi.Elapsed);

                if (beklemeSuresi <= TimeSpan.Zero)
                {
                    KonsolKayitcisi.Uyari(
                        $"Tick {tickNumarasi}, belirlenen tick süresinden " +
                        "uzun sürdü. Sonraki tick beklemeden başlatılacak.");
                    continue;
                }

                try
                {
                    await Task.Delay(beklemeSuresi, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            _calisiyor = false;
            await SonKayitlariYapAsync();
            KonsolKayitcisi.Bilgi("Tick sistemi durduruldu.");
        }
    }

    private async Task TickCalistirAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Müşteri gelirleri ve dönemsel durumları güncellenir.
        _musteriYoneticisi.TickBasindaMusterileriGuncelle(tickNumarasi);

        // 2. Şirket bağlantıları ve sağlık durumları güncellenir.
        await _sirketYoneticisi.TickCalistirAsync(
            tickNumarasi,
            cancellationToken);

        // 3. Altyapı gideri, kredi, ürün, abonelik, protokol,
        //    sözleşme, piyasa olayı ve şirket değerlemesi işlenir.
        await _sirketIsletimYoneticisi.TickCalistirAsync(
            tickNumarasi,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // 4. Müşteri hizmet talepleri oluşturulur.
        IReadOnlyList<HizmetTalebi> talepler =
            _musteriYoneticisi.TickTalepleriniOlustur(tickNumarasi);

        CanliPanoDurumDeposu.TalepleriGuncelle(
            tickNumarasi,
            talepler);
        TalepleriRaporla(tickNumarasi, talepler);

        // 5. Talepler şirketlere atanır, sonuçlar doğrulanır.
        IsIslemeOzeti islemeOzeti =
            await _isYoneticisi.TalepleriIsleAsync(
                tickNumarasi,
                talepler,
                cancellationToken);

        CanliPanoDurumDeposu.IslemeOzetiniGuncelle(islemeOzeti);

        // 6. Güncel finans durumları kalıcı yazılır ve yayınlanır.
        await _sirketYoneticisi.BilancolariKaydetVeYayinlaAsync(
            tickNumarasi,
            cancellationToken);

        // 7. Müşteri kayıtları periyodik kaydedilir.
        await _musteriYoneticisi.GerekirseKaydetAsync(
            tickNumarasi,
            cancellationToken);

        TickOzetiniYaz(tickNumarasi, talepler);
    }

    private void TalepleriRaporla(
        long tickNumarasi,
        IReadOnlyList<HizmetTalebi> talepler)
    {
        ArgumentNullException.ThrowIfNull(talepler);

        if (talepler.Count == 0)
        {
            KonsolKayitcisi.Bilgi(
                $"Tick {tickNumarasi} | Yeni müşteri talebi oluşmadı.");
            return;
        }

        IEnumerable<IGrouping<string, HizmetTalebi>> hizmetGruplari =
            talepler
                .GroupBy(
                    talep =>
                        $"{talep.HizmetKimligi}@{talep.HizmetSurumu}",
                    StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(grup => grup.Count())
                .ThenBy(
                    grup => grup.Key,
                    StringComparer.OrdinalIgnoreCase);

        KonsolKayitcisi.Bilgi(
            $"Tick {tickNumarasi} | Toplam {talepler.Count} " +
            "müşteri talebi oluşturuldu.");

        foreach (IGrouping<string, HizmetTalebi> grup in hizmetGruplari)
        {
            decimal toplamAzamiButce =
                grup.Sum(talep => talep.AzamiButce);

            KonsolKayitcisi.Bilgi(
                $"Talep grubu: {grup.Key} | Talep sayısı: " +
                $"{grup.Count()} | Toplam azami bütçe: " +
                $"{toplamAzamiButce:N2}");
        }
    }

    private void TickOzetiniYaz(
        long tickNumarasi,
        IReadOnlyList<HizmetTalebi> talepler)
    {
        int aktifMusteriSayisi = _musteriYoneticisi.AktifMusteriSayisi;
        decimal toplamMusteriBakiyesi =
            _musteriYoneticisi.ToplamMusteriBakiyesi;
        decimal toplamMusteriHarcamasi =
            _musteriYoneticisi.ToplamMusteriHarcamasi;
        decimal toplamTalepButcesi =
            talepler.Sum(talep => talep.AzamiButce);
        decimal toplamSirketKasasi =
            _sirketYoneticisi.SirketKayitlari.Sum(sirket => sirket.Kasa);

        KonsolKayitcisi.Bilgi(
            $"Tick {tickNumarasi} özeti | Aktif müşteri: " +
            $"{aktifMusteriSayisi} | Yeni talep: {talepler.Count} | " +
            $"Talep bütçesi: {toplamTalepButcesi:N2} | " +
            $"Müşteri bakiyesi: {toplamMusteriBakiyesi:N2} | " +
            $"Toplam harcama: {toplamMusteriHarcamasi:N2} | " +
            $"Şirket kasaları: {toplamSirketKasasi:N2}");
    }

    private TimeSpan TickBeklemeSuresiniHesapla(
        TimeSpan tickIslemSuresi)
    {
        TimeSpan hedefTickSuresi =
            TimeSpan.FromSeconds(_ayarlar.TickSuresiSaniye);
        return hedefTickSuresi - tickIslemSuresi;
    }

    private async Task SonKayitlariYapAsync()
    {
        try
        {
            await _musteriYoneticisi.KaydetAsync(CancellationToken.None);
            await _sirketYoneticisi.BilancolariKaydetAsync(
                CancellationToken.None);

            KonsolKayitcisi.Basari(
                "Motor kapanmadan önce müşteri ve şirket verileri " +
                "son kez kaydedildi.");
        }
        catch (Exception exception)
        {
            KonsolKayitcisi.Hata(
                $"Motor kapanırken kalıcı veriler kaydedilemedi: " +
                exception.Message);
            KonsolKayitcisi.Hata(exception.ToString());
        }
    }
}
