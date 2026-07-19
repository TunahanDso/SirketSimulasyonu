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
    private readonly MusteriIsletimSistemiYoneticisi _musteriIsletimSistemiYoneticisi;
    private readonly KodTabanliSirketIsletimYoneticisi _isletimYoneticisi;
    private readonly EkosistemYoneticisi _ekosistemYoneticisi;
    private readonly PazarFiyatYoneticisi _pazarFiyatYoneticisi;
    private readonly PazarGelirDuzeltmeYoneticisi _pazarGelirDuzeltmeYoneticisi;
    private readonly EkonomiV6Yoneticisi _ekonomiV6Yoneticisi;
    private readonly EkonomiDengeV7Yoneticisi _ekonomiDengeV7Yoneticisi;
    private readonly FinansV7Yoneticisi _finansV7Yoneticisi;
    private readonly IsYoneticisi _isYoneticisi;
    private readonly ProtokolKimlikUzlastiricisi _protokolUzlastiricisi;
    private long _tickNumarasi;
    private bool _calisiyor;

    public long TickNumarasi => Interlocked.Read(ref _tickNumarasi);
    public bool Calisiyor => _calisiyor;

    public TickYoneticisi(
        MotorAyarlari ayarlar,
        SirketYoneticisi sirketYoneticisi,
        MusteriYoneticisi musteriYoneticisi,
        MusteriIsletimSistemiYoneticisi musteriIsletimSistemiYoneticisi,
        KodTabanliSirketIsletimYoneticisi isletimYoneticisi,
        EkosistemYoneticisi ekosistemYoneticisi,
        PazarFiyatYoneticisi pazarFiyatYoneticisi,
        PazarGelirDuzeltmeYoneticisi pazarGelirDuzeltmeYoneticisi,
        EkonomiV6Yoneticisi ekonomiV6Yoneticisi,
        EkonomiDengeV7Yoneticisi ekonomiDengeV7Yoneticisi,
        FinansV7Yoneticisi finansV7Yoneticisi)
    {
        _ayarlar = ayarlar ?? throw new ArgumentNullException(nameof(ayarlar));
        _sirketYoneticisi = sirketYoneticisi ?? throw new ArgumentNullException(nameof(sirketYoneticisi));
        _musteriYoneticisi = musteriYoneticisi ?? throw new ArgumentNullException(nameof(musteriYoneticisi));
        _musteriIsletimSistemiYoneticisi = musteriIsletimSistemiYoneticisi ?? throw new ArgumentNullException(nameof(musteriIsletimSistemiYoneticisi));
        _isletimYoneticisi = isletimYoneticisi ?? throw new ArgumentNullException(nameof(isletimYoneticisi));
        _ekosistemYoneticisi = ekosistemYoneticisi ?? throw new ArgumentNullException(nameof(ekosistemYoneticisi));
        _pazarFiyatYoneticisi = pazarFiyatYoneticisi ?? throw new ArgumentNullException(nameof(pazarFiyatYoneticisi));
        _pazarGelirDuzeltmeYoneticisi = pazarGelirDuzeltmeYoneticisi ?? throw new ArgumentNullException(nameof(pazarGelirDuzeltmeYoneticisi));
        _ekonomiV6Yoneticisi = ekonomiV6Yoneticisi ?? throw new ArgumentNullException(nameof(ekonomiV6Yoneticisi));
        _ekonomiDengeV7Yoneticisi = ekonomiDengeV7Yoneticisi ?? throw new ArgumentNullException(nameof(ekonomiDengeV7Yoneticisi));
        _finansV7Yoneticisi = finansV7Yoneticisi ?? throw new ArgumentNullException(nameof(finansV7Yoneticisi));
        if (ayarlar.TickSuresiSaniye <= 0) throw new ArgumentOutOfRangeException(nameof(ayarlar));

        _isYoneticisi = new IsYoneticisi(
            sirketYoneticisi,
            musteriYoneticisi,
            new SonucDogrulayicisi());
        _protokolUzlastiricisi = new ProtokolKimlikUzlastiricisi(
            isletimYoneticisi,
            ekosistemYoneticisi,
            sirketYoneticisi);
        _tickNumarasi = Math.Max(0, TickSaatDeposu.SonTamamlananTick);
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        if (_calisiyor) throw new InvalidOperationException("Tick sistemi zaten çalışıyor.");
        _calisiyor = true;
        KonsolKayitcisi.Bilgi(
            $"Tick sistemi V8.2 başlatıldı | Son tamamlanan tick: {TickNumarasi} | " +
            $"Tick süresi: {_ayarlar.TickSuresiSaniye} saniye | " +
            "Bağlı şirketler oynar, kapalı şirketler donar; hizmet fiyatları motor tarafından sabittir.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                long tick = checked(TickNumarasi + 1);
                Stopwatch kronometre = Stopwatch.StartNew();
                KonsolKayitcisi.Tick(tick);
                try
                {
                    await TickCalistirAsync(tick, cancellationToken);
                    await TickSaatDeposu.TamamlandiAsync(tick, cancellationToken);
                    Interlocked.Exchange(ref _tickNumarasi, tick);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception hata)
                {
                    KonsolKayitcisi.Hata(
                        $"Tick {tick} tamamlanamadı; tick saati ilerletilmedi: {hata.Message}");
                    KonsolKayitcisi.Hata(hata.ToString());
                    throw new InvalidOperationException(
                        $"Tick {tick} atomik olarak tamamlanamadığı için motor güvenli biçimde durduruldu.",
                        hata);
                }
                finally
                {
                    kronometre.Stop();
                }

                KonsolKayitcisi.Bilgi(
                    $"Tick {tick} başarıyla tamamlandı ve kalıcı saate işlendi. " +
                    $"İşlem süresi: {kronometre.Elapsed.TotalMilliseconds:N2} ms.");
                TimeSpan bekleme =
                    TimeSpan.FromSeconds(_ayarlar.TickSuresiSaniye) - kronometre.Elapsed;
                if (bekleme <= TimeSpan.Zero)
                {
                    KonsolKayitcisi.Uyari(
                        $"Tick {tick} hedef süreden uzun sürdü; sonraki tick beklemeden başlayacak.");
                    continue;
                }
                try
                {
                    await Task.Delay(bekleme, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
        CezaV8Deposu.TickBaslat(tickNumarasi);

        await _sirketYoneticisi.TickCalistirAsync(
            tickNumarasi,
            cancellationToken);
        await _protokolUzlastiricisi.UygulaAsync(cancellationToken);

        BaglantiBazliSirketDonmaYoneticisi donma = new(
            _sirketYoneticisi,
            _isletimYoneticisi,
            _finansV7Yoneticisi,
            _ekonomiV6Yoneticisi);
        await donma.TickOncesiAsync(cancellationToken);

        try
        {
            _musteriYoneticisi.TickBasindaMusterileriGuncelle(tickNumarasi);
            await _ekosistemYoneticisi.PazariHazirlaAsync(
                tickNumarasi,
                cancellationToken);
            _musteriIsletimSistemiYoneticisi.TickCalistir(tickNumarasi);

            await _finansV7Yoneticisi.TickOncesiAsync(
                tickNumarasi,
                cancellationToken);
            await _ekonomiDengeV7Yoneticisi.TickOncesiAsync(cancellationToken);
            await _ekonomiV6Yoneticisi.TickOncesiAsync(
                tickNumarasi,
                cancellationToken);
            await _pazarGelirDuzeltmeYoneticisi.TickOncesiHazirlaAsync(
                tickNumarasi,
                cancellationToken);
            await _isletimYoneticisi.TickCalistirAsync(
                tickNumarasi,
                cancellationToken);

            // Kalıcı eski fiyat ezmeleri bu noktadan sonra iş seçimini etkileyemez.
            MotorHizmetFiyatlari.Uygula(_sirketYoneticisi.SirketKayitlari);

            await _pazarFiyatYoneticisi.TickCalistirAsync(
                tickNumarasi,
                cancellationToken);
            await _pazarGelirDuzeltmeYoneticisi.TickSonrasiDuzeltAsync(
                tickNumarasi,
                cancellationToken);

            IReadOnlyList<HizmetTalebi> talepler =
                _musteriYoneticisi.TickTalepleriniOlustur(tickNumarasi);
            CanliPanoDurumDeposu.TalepleriGuncelle(tickNumarasi, talepler);
            TalepleriRaporla(tickNumarasi, talepler);

            IsIslemeOzeti islemeOzeti = await _isYoneticisi.TalepleriIsleAsync(
                tickNumarasi,
                talepler,
                cancellationToken);
            CanliPanoDurumDeposu.IslemeOzetiniGuncelle(islemeOzeti);

            await _ekonomiV6Yoneticisi.TickSonuAsync(
                tickNumarasi,
                cancellationToken);
            await _ekonomiDengeV7Yoneticisi.TickSonuAsync(
                tickNumarasi,
                cancellationToken);

            await donma.GeriYukleAsync(tickNumarasi, cancellationToken);
            await _finansV7Yoneticisi.TickSonuAsync(
                tickNumarasi,
                cancellationToken);
            await donma.GeriYukleAsync(tickNumarasi, cancellationToken);

            CezaV8Deposu.TickBitir(tickNumarasi);
            await _sirketYoneticisi.BilancolariKaydetVeYayinlaAsync(
                tickNumarasi,
                cancellationToken);
            await _musteriYoneticisi.GerekirseKaydetAsync(
                tickNumarasi,
                cancellationToken);
            TickOzetiniYaz(tickNumarasi, talepler);
        }
        finally
        {
            try
            {
                await donma.GeriYukleAsync(
                    tickNumarasi,
                    CancellationToken.None);
            }
            catch (Exception hata)
            {
                KonsolKayitcisi.Hata(
                    $"Kapalı şirket anlıkları geri yüklenemedi: {hata.Message}");
                throw;
            }
        }
    }

    private static void TalepleriRaporla(
        long tickNumarasi,
        IReadOnlyList<HizmetTalebi> talepler)
    {
        if (talepler.Count == 0)
        {
            KonsolKayitcisi.Bilgi(
                $"Tick {tickNumarasi} | Yeni müşteri talebi oluşmadı.");
            return;
        }

        KonsolKayitcisi.Bilgi(
            $"Tick {tickNumarasi} | Toplam {talepler.Count} müşteri talebi oluşturuldu.");
        foreach (IGrouping<string, HizmetTalebi> grup in talepler
                     .GroupBy(
                         x => $"{x.HizmetKimligi}@{x.HizmetSurumu}",
                         StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(x => x.Count())
                     .ThenBy(x => x.Key))
        {
            KonsolKayitcisi.Bilgi(
                $"Talep grubu: {grup.Key} | Talep: {grup.Count()} | " +
                $"Bütçe: {grup.Sum(x => x.AzamiButce):N2}");
        }
    }

    private void TickOzetiniYaz(
        long tickNumarasi,
        IReadOnlyList<HizmetTalebi> talepler)
    {
        IReadOnlyList<IsletimSistemiPazarKaydi> sistemler =
            IsletimSistemiPazarDeposu.Getir();
        PazarFiyatDosyasi fiyatPazari = PazarFiyatDeposu.Getir();
        FinansV7Dosyasi finans = FinansV7Deposu.Getir();
        V6PanoDurumu v6 = V6PanoDeposu.Getir();
        int osKullanan = sistemler.Sum(x => x.AktifMusteriSayisi);
        int fiyatKaybi = fiyatPazari.Urunler.Sum(
            x => x.BuTickFiyatKaybi + x.BuTickEngellenenYeniKullanici);
        decimal tickGelir = finans.Sirketler.Sum(
            x => x.Tickler.LastOrDefault()?.ToplamGelir ?? 0);
        decimal tickGider = finans.Sirketler.Sum(
            x => x.Tickler.LastOrDefault()?.ToplamGider ?? 0);
        int bagli = _sirketYoneticisi.SirketKayitlari.Count(x => x.BagliMi);
        int toplam = _sirketYoneticisi.SirketKayitlari.Count;

        KonsolKayitcisi.Bilgi(
            $"Tick {tickNumarasi} özeti | Bağlı şirket: {bagli}/{toplam} | " +
            $"Aktif müşteri: {_musteriYoneticisi.AktifMusteriSayisi} | " +
            $"OS kullanan: {osKullanan} | " +
            $"OS bekleyen: {Math.Max(0, _musteriYoneticisi.AktifMusteriSayisi - osKullanan)} | " +
            $"Fiyat nedeniyle reddedilen/kaçan: {fiyatKaybi} | " +
            $"V6 gerçek kapasite: {v6.Sirketler.Sum(x => x.GercekKapasiteBirimi)} | " +
            $"V6 kullanılan kapasite: {v6.Sirketler.Sum(x => x.KullanilanKapasiteBirimi)} | " +
            $"Tick gelir: {tickGelir:N2} | Tick gider: {tickGider:N2} | " +
            $"Tick net: {tickGelir - tickGider:N2} | " +
            $"Yeni talep: {talepler.Count} | " +
            $"Talep bütçesi: {talepler.Sum(x => x.AzamiButce):N2} | " +
            $"Şirket kasaları: {_sirketYoneticisi.SirketKayitlari.Sum(x => x.Kasa):N2} | " +
            "V8.2 sabit hizmet fiyatı aktif");
    }

    private async Task SonKayitlariYapAsync()
    {
        try
        {
            CezaV8Deposu.TickBitir(TickNumarasi);
            await _musteriYoneticisi.KaydetAsync(CancellationToken.None);
            await _sirketYoneticisi.BilancolariKaydetAsync(CancellationToken.None);
        }
        catch (Exception hata)
        {
            KonsolKayitcisi.Hata($"Son kayıtlar yazılamadı: {hata.Message}");
        }
    }
}
