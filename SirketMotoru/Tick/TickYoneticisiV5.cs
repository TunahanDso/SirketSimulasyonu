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
    private readonly SirketYoneticisi _sirketler;
    private readonly MusteriYoneticisi _musteriler;
    private readonly KodTabanliSirketIsletimYoneticisi _isletim;
    private readonly EkosistemYoneticisi _ekosistem;
    private readonly V9EkonomiYoneticisi _v9;
    private readonly IsYoneticisi _isler;
    private readonly ProtokolKimlikUzlastiricisi _protokoller;
    private readonly V9IsletimKoordinatoru _isletimKoordinatoru;
    private readonly V92OlayVeMaliyetYoneticisi _olaylar;
    private readonly V93KapasiteDengeleyicisi _kapasiteDengeleyicisi;
    private readonly V93YayinMaliyetiYoneticisi _yayinMaliyetleri;
    private long _tick;
    private bool _calisiyor;

    public long TickNumarasi => Interlocked.Read(ref _tick);
    public bool Calisiyor => _calisiyor;

    public TickYoneticisi(
        MotorAyarlari ayarlar,
        SirketYoneticisi sirketler,
        MusteriYoneticisi musteriler,
        KodTabanliSirketIsletimYoneticisi isletim,
        EkosistemYoneticisi ekosistem,
        V9EkonomiYoneticisi v9,
        string motorVerileriKlasoru)
    {
        _ayarlar = ayarlar ?? throw new ArgumentNullException(nameof(ayarlar));
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _musteriler = musteriler ?? throw new ArgumentNullException(nameof(musteriler));
        _isletim = isletim ?? throw new ArgumentNullException(nameof(isletim));
        _ekosistem = ekosistem ?? throw new ArgumentNullException(nameof(ekosistem));
        _v9 = v9 ?? throw new ArgumentNullException(nameof(v9));
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);
        _isler = new IsYoneticisi(sirketler, musteriler, new SonucDogrulayicisi());
        _protokoller = new ProtokolKimlikUzlastiricisi(isletim, ekosistem, sirketler);
        _isletimKoordinatoru = new V9IsletimKoordinatoru(sirketler, isletim);
        _olaylar = new V92OlayVeMaliyetYoneticisi(sirketler, motorVerileriKlasoru);
        _kapasiteDengeleyicisi = new V93KapasiteDengeleyicisi(isletim, v9);
        _yayinMaliyetleri = new V93YayinMaliyetiYoneticisi(sirketler, isletim, motorVerileriKlasoru);
        _tick = Math.Max(0, TickSaatDeposu.SonTamamlananTick);
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        if (_calisiyor) throw new InvalidOperationException("Tick sistemi zaten çalışıyor.");
        _calisiyor = true;
        await _olaylar.BaslatAsync(cancellationToken);
        await _yayinMaliyetleri.BaslatAsync(cancellationToken);
        KonsolKayitcisi.Basari(
            $"V9.3 tick sistemi başladı | Son tick: {TickNumarasi} | Hedef aralık: {_ayarlar.TickSuresiSaniye} sn | " +
            "Gerçek fiziksel kapasite, düşük hizmet geliri, pahalı lansman ve siber tehdit haritası aktif.");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                long tick = checked(TickNumarasi + 1);
                Stopwatch sure = Stopwatch.StartNew();
                KonsolKayitcisi.Tick(tick);
                try
                {
                    await TickCalistirAsync(tick, cancellationToken);
                    await TickSaatDeposu.TamamlandiAsync(tick, cancellationToken);
                    Interlocked.Exchange(ref _tick, tick);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception hata)
                {
                    KonsolKayitcisi.Hata($"V9.3 tick {tick} tamamlanamadı; saat ilerletilmedi: {hata.Message}");
                    KonsolKayitcisi.Hata(hata.ToString());
                    throw;
                }
                finally { sure.Stop(); }

                KonsolKayitcisi.Bilgi($"V9.3 tick {tick} tamamlandı | Süre: {sure.Elapsed.TotalMilliseconds:N0} ms");
                TimeSpan bekleme = TimeSpan.FromSeconds(_ayarlar.TickSuresiSaniye) - sure.Elapsed;
                if (bekleme > TimeSpan.Zero)
                {
                    try { await Task.Delay(bekleme, cancellationToken); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                }
                else
                {
                    KonsolKayitcisi.Uyari(
                        $"Tick {tick} hedef aralıktan uzun sürdü. 20.000 kişilik talep korunuyor; arz yalnız gerçek kapasite kadar karşılanır.");
                }
            }
        }
        finally
        {
            _calisiyor = false;
            await SonKayitAsync();
            KonsolKayitcisi.Bilgi("V9.3 tick sistemi durdu.");
        }
    }

    private async Task TickCalistirAsync(long tick, CancellationToken cancellationToken)
    {
        await _sirketler.TickCalistirAsync(tick, cancellationToken);
        await _protokoller.UygulaAsync(cancellationToken);
        await MotorHizmetFiyatlari.KaliciEzmeKayitlariniTemizleVeUygulaAsync(
            _isletim,
            _sirketler.SirketKayitlari,
            cancellationToken);

        await _v9.TickOncesiAsync(tick, cancellationToken);
        await _kapasiteDengeleyicisi.UrunKapasiteleriniFizikselYapAsync(cancellationToken);
        V93HizmetKapasiteDeposu.TickBaslat(tick);

        await _isletimKoordinatoru.TickCalistirAsync(tick, cancellationToken);

        // Yeni ürün/protokol, lansman sermayesi ödenmeden pazara ve kullanıcı
        // dağıtımına giremez. Yetersiz sermayeli kayıt burada pasifleştirilir.
        await _yayinMaliyetleri.UygulaAsync(tick, cancellationToken);
        await _ekosistem.PazariHazirlaAsync(tick, cancellationToken);
        await _v9.UrunPazariniVeEkonomiyiIsleAsync(tick, cancellationToken);
        await _kapasiteDengeleyicisi.RaporuGuncelleAsync(cancellationToken);

        _musteriler.TickBasindaMusterileriGuncelle(tick);
        IReadOnlyList<HizmetTalebi> talepler = _musteriler.TickTalepleriniOlustur(tick);
        CanliPanoDurumDeposu.TalepleriGuncelle(tick, talepler);
        IsIslemeOzeti isOzeti = await _isler.TalepleriIsleAsync(tick, talepler, cancellationToken);
        CanliPanoDurumDeposu.IslemeOzetiniGuncelle(isOzeti);

        await _v9.TickSonuAsync(tick, cancellationToken);
        await _kapasiteDengeleyicisi.RaporuGuncelleAsync(cancellationToken);
        await V93SiberOlayDeposu.PazaraYayinlaAsync(_v9, tick, cancellationToken);
        V92OlayOzetSifirlayici.Sifirla();
        await _olaylar.TickSonuAsync(tick, cancellationToken);
        await _sirketler.BilancolariKaydetVeYayinlaAsync(tick, cancellationToken);
        await _musteriler.GerekirseKaydetAsync(tick, cancellationToken);

        V9PazarDosyasi pazar = V9PazarDeposu.Getir();
        KonsolKayitcisi.Bilgi(
            $"V9.3 piyasa özeti | Hizmet talebi {pazar.HizmetTalepleri.Values.Sum(x => x.BuTickTalep):N0} | " +
            $"Hizmet karşılanan {pazar.HizmetTalepleri.Values.Sum(x => x.KarsilananTalep):N0} | " +
            $"OS {pazar.IsletimSistemiTalebi.KarsilananTalep:N0}/{pazar.IsletimSistemiTalebi.BuTickTalep:N0} | " +
            $"Fiziksel kullanım {pazar.SirketKapasiteleri.Values.Sum(x => x.KullanilanKapasite):N0}/" +
            $"{pazar.SirketKapasiteleri.Values.Sum(x => x.ToplamFizikselKapasite):N0} | " +
            $"Şirket neti {pazar.SirketOzetleri.Sum(x => x.NetKazanc):N2} TL | Haber {pazar.Haberler.Count:N0}");
    }

    private async Task SonKayitAsync()
    {
        try
        {
            await _musteriler.KaydetAsync(CancellationToken.None);
            await _sirketler.BilancolariKaydetAsync(CancellationToken.None);
        }
        catch (Exception hata)
        {
            KonsolKayitcisi.Hata($"V9.3 kapanış kayıtları yazılamadı: {hata.Message}");
        }
    }
}
