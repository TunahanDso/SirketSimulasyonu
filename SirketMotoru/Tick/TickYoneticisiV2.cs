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
    private readonly IsYoneticisi _isYoneticisi;
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
        PazarFiyatYoneticisi pazarFiyatYoneticisi)
    {
        ArgumentNullException.ThrowIfNull(ayarlar);
        ArgumentNullException.ThrowIfNull(sirketYoneticisi);
        ArgumentNullException.ThrowIfNull(musteriYoneticisi);
        ArgumentNullException.ThrowIfNull(musteriIsletimSistemiYoneticisi);
        ArgumentNullException.ThrowIfNull(isletimYoneticisi);
        ArgumentNullException.ThrowIfNull(ekosistemYoneticisi);
        ArgumentNullException.ThrowIfNull(pazarFiyatYoneticisi);
        if (ayarlar.TickSuresiSaniye <= 0) throw new ArgumentOutOfRangeException(nameof(ayarlar));
        _ayarlar = ayarlar;
        _sirketYoneticisi = sirketYoneticisi;
        _musteriYoneticisi = musteriYoneticisi;
        _musteriIsletimSistemiYoneticisi = musteriIsletimSistemiYoneticisi;
        _isletimYoneticisi = isletimYoneticisi;
        _ekosistemYoneticisi = ekosistemYoneticisi;
        _pazarFiyatYoneticisi = pazarFiyatYoneticisi;
        _isYoneticisi = new IsYoneticisi(sirketYoneticisi, musteriYoneticisi, new SonucDogrulayicisi());
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        if (_calisiyor) throw new InvalidOperationException("Tick sistemi zaten çalışıyor.");
        _calisiyor = true;
        KonsolKayitcisi.Bilgi($"Tick sistemi başlatıldı. Tick süresi: {_ayarlar.TickSuresiSaniye} saniye.");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                long tick = Interlocked.Increment(ref _tickNumarasi);
                Stopwatch kronometre = Stopwatch.StartNew();
                KonsolKayitcisi.Tick(tick);
                try { await TickCalistirAsync(tick, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception e)
                {
                    KonsolKayitcisi.Hata($"Tick {tick} çalıştırılırken hata oluştu: {e.Message}");
                    KonsolKayitcisi.Hata(e.ToString());
                }
                finally { kronometre.Stop(); }
                KonsolKayitcisi.Bilgi($"Tick {tick} tamamlandı. İşlem süresi: {kronometre.Elapsed.TotalMilliseconds:N2} ms.");
                TimeSpan bekleme = TimeSpan.FromSeconds(_ayarlar.TickSuresiSaniye) - kronometre.Elapsed;
                if (bekleme <= TimeSpan.Zero)
                {
                    KonsolKayitcisi.Uyari($"Tick {tick} hedef süreden uzun sürdü; sonraki tick beklemeden başlayacak.");
                    continue;
                }
                try { await Task.Delay(bekleme, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            }
        }
        finally
        {
            _calisiyor = false;
            await SonKayitlariYapAsync();
            KonsolKayitcisi.Bilgi("Tick sistemi durduruldu.");
        }
    }

    private async Task TickCalistirAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _musteriYoneticisi.TickBasindaMusterileriGuncelle(tickNumarasi);
        await _sirketYoneticisi.TickCalistirAsync(tickNumarasi, cancellationToken);

        await _ekosistemYoneticisi.PazariHazirlaAsync(tickNumarasi, cancellationToken);
        _musteriIsletimSistemiYoneticisi.TickCalistir(tickNumarasi);

        // Temel ürün ekonomisi çalışır; hemen ardından kategori fiyat pazarı aşırı fiyatlı
        // edinimleri iptal eder, mevcut kullanıcı kaçışını ve rakibe göçü uygular.
        await _isletimYoneticisi.TickCalistirAsync(tickNumarasi, cancellationToken);
        await _pazarFiyatYoneticisi.TickCalistirAsync(tickNumarasi, cancellationToken);

        IReadOnlyList<HizmetTalebi> talepler = _musteriYoneticisi.TickTalepleriniOlustur(tickNumarasi);
        CanliPanoDurumDeposu.TalepleriGuncelle(tickNumarasi, talepler);
        TalepleriRaporla(tickNumarasi, talepler);

        IsIslemeOzeti islemeOzeti = await _isYoneticisi.TalepleriIsleAsync(tickNumarasi, talepler, cancellationToken);
        CanliPanoDurumDeposu.IslemeOzetiniGuncelle(islemeOzeti);

        await _sirketYoneticisi.BilancolariKaydetVeYayinlaAsync(tickNumarasi, cancellationToken);
        await _musteriYoneticisi.GerekirseKaydetAsync(tickNumarasi, cancellationToken);
        TickOzetiniYaz(tickNumarasi, talepler);
    }

    private void TalepleriRaporla(long tickNumarasi, IReadOnlyList<HizmetTalebi> talepler)
    {
        if (talepler.Count == 0) { KonsolKayitcisi.Bilgi($"Tick {tickNumarasi} | Yeni müşteri talebi oluşmadı."); return; }
        KonsolKayitcisi.Bilgi($"Tick {tickNumarasi} | Toplam {talepler.Count} müşteri talebi oluşturuldu.");
        foreach (IGrouping<string, HizmetTalebi> grup in talepler.GroupBy(t => $"{t.HizmetKimligi}@{t.HizmetSurumu}", StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).ThenBy(g => g.Key))
            KonsolKayitcisi.Bilgi($"Talep grubu: {grup.Key} | Talep: {grup.Count()} | Bütçe: {grup.Sum(t => t.AzamiButce):N2}");
    }

    private void TickOzetiniYaz(long tickNumarasi, IReadOnlyList<HizmetTalebi> talepler)
    {
        IReadOnlyList<IsletimSistemiPazarKaydi> sistemler = IsletimSistemiPazarDeposu.Getir();
        PazarFiyatDosyasi fiyatPazari = PazarFiyatDeposu.Getir();
        int osKullanan = sistemler.Sum(x => x.AktifMusteriSayisi);
        int osBekleyen = Math.Max(0, _musteriYoneticisi.AktifMusteriSayisi - osKullanan);
        int fiyatKaybi = fiyatPazari.Urunler.Sum(x => x.BuTickFiyatKaybi + x.BuTickEngellenenYeniKullanici);
        int rakibeGoc = fiyatPazari.Urunler.Sum(x => x.BuTickRakiptenGelenKullanici);
        KonsolKayitcisi.Bilgi(
            $"Tick {tickNumarasi} özeti | Aktif müşteri: {_musteriYoneticisi.AktifMusteriSayisi} | " +
            $"OS kullanan: {osKullanan} | OS bekleyen: {osBekleyen} | Aktif OS: {sistemler.Count} | " +
            $"Fiyat nedeniyle reddedilen/kaçan: {fiyatKaybi} | Rakibe göç: {rakibeGoc} | " +
            $"Yeni talep: {talepler.Count} | Talep bütçesi: {talepler.Sum(t => t.AzamiButce):N2} | " +
            $"Müşteri bakiyesi: {_musteriYoneticisi.ToplamMusteriBakiyesi:N2} | " +
            $"Şirket kasaları: {_sirketYoneticisi.SirketKayitlari.Sum(s => s.Kasa):N2}");
    }

    private async Task SonKayitlariYapAsync()
    {
        try
        {
            await _musteriYoneticisi.KaydetAsync(CancellationToken.None);
            await _sirketYoneticisi.BilancolariKaydetAsync(CancellationToken.None);
        }
        catch (Exception e) { KonsolKayitcisi.Hata($"Son kayıtlar yazılamadı: {e.Message}"); }
    }
}
