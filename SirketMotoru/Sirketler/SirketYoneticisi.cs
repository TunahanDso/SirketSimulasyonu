using SirketMotoru.Ag;
using SirketMotoru.Ayarlar;
using SirketMotoru.Hizmetler;
using SirketMotoru.Kayit;

namespace SirketMotoru.Sirketler;

public sealed class SirketYoneticisi : IAsyncDisposable
{
    private readonly List<SirketBaglantisi> _baglantilar;
    private readonly HizmetKatalogu _hizmetKatalogu;
    private readonly SirketBilancoVeritabani _bilancoVeritabani;

    public IReadOnlyCollection<SirketKaydi> SirketKayitlari =>
        _baglantilar
            .Select(baglanti => baglanti.Kayit)
            .ToList()
            .AsReadOnly();

    public SirketYoneticisi(
        MotorAyarlari motorAyarlari,
        HizmetKatalogu hizmetKatalogu)
    {
        ArgumentNullException.ThrowIfNull(motorAyarlari);
        ArgumentNullException.ThrowIfNull(hizmetKatalogu);
        _hizmetKatalogu = hizmetKatalogu;
        _baglantilar = motorAyarlari.Sirketler
            .Select(ayar => new SirketBaglantisi(motorAyarlari, ayar))
            .ToList();
        _bilancoVeritabani = new SirketBilancoVeritabani();

        IReadOnlyDictionary<string, SirketBilancoKaydi> kaliciBilancolar =
            _bilancoVeritabani.Yukle();
        Dictionary<string, SirketBaglantiAyari> ayarIndeksi =
            motorAyarlari.Sirketler.ToDictionary(
                ayar => ayar.SirketKimligi,
                StringComparer.OrdinalIgnoreCase);
        int yuklenenBilancoSayisi = 0;
        int baslangicProfiliSayisi = 0;

        foreach (SirketBaglantisi baglanti in _baglantilar)
        {
            if (kaliciBilancolar.TryGetValue(
                    baglanti.Kayit.SirketKimligi,
                    out SirketBilancoKaydi? bilanco))
            {
                bilanco.Uygula(baglanti.Kayit);
                yuklenenBilancoSayisi++;
                continue;
            }

            if (ayarIndeksi.TryGetValue(
                    baglanti.Kayit.SirketKimligi,
                    out SirketBaglantiAyari? ayar))
            {
                BaslangicProfiliniUygula(baglanti.Kayit, ayar);
                baslangicProfiliSayisi++;
            }
        }

        KonsolKayitcisi.Bilgi(
            $"Şirket bilanço veritabanı: {_bilancoVeritabani.DosyaYolu} | " +
            $"Yüklenen kayıt: {yuklenenBilancoSayisi} | " +
            $"Yeni başlangıç profili: {baslangicProfiliSayisi}");
    }

    public IReadOnlyList<SirketBaglantisi> BaglantilariGetir() =>
        _baglantilar.ToList().AsReadOnly();

    public async Task IlkBaglantilariKurAsync(CancellationToken cancellationToken)
    {
        KonsolKayitcisi.Bilgi($"{_baglantilar.Count} şirkete bağlantı kurulacak.");

        foreach (SirketBaglantisi baglanti in _baglantilar)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool baglandi = await baglanti.BaglanVeKaydetAsync(cancellationToken);
            if (baglandi)
            {
                await FinansDurumunuGuvenliGonderAsync(
                    baglanti,
                    0,
                    cancellationToken);
            }
        }

        await BilancolariKaydetAsync(cancellationToken);
        SirketDurumOzetiniYazdir();
    }

    public async Task TickCalistirAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        foreach (SirketBaglantisi baglanti in _baglantilar)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!baglanti.Bagli)
            {
                KonsolKayitcisi.Bilgi(
                    $"{baglanti.Kayit.SirketAdi} için yeniden bağlantı deneniyor.");
                bool baglandi = await baglanti.BaglanVeKaydetAsync(cancellationToken);
                if (!baglandi) continue;
            }

            bool saglikli = await baglanti.SaglikKontrolEtAsync(
                tickNumarasi,
                cancellationToken);
            if (!saglikli) continue;

            // Yıpranma yalnız gerçekten bağlı ve sağlık kontrolünü geçen şirkete uygulanır.
            baglanti.Kayit.PiyasaYipranmasiUygula();
            await FinansDurumunuGuvenliGonderAsync(
                baglanti,
                tickNumarasi,
                cancellationToken);
        }

        await BilancolariKaydetAsync(cancellationToken);
        if (tickNumarasi % 5 == 0) SirketDurumOzetiniYazdir();
    }

    public async Task BilancolariKaydetVeYayinlaAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        await BilancolariKaydetAsync(cancellationToken);

        foreach (SirketBaglantisi baglanti in _baglantilar.Where(x => x.Bagli))
        {
            await FinansDurumunuGuvenliGonderAsync(
                baglanti,
                tickNumarasi,
                cancellationToken);
        }
    }

    public async Task BilancolariKaydetAsync(CancellationToken cancellationToken)
    {
        await _bilancoVeritabani.KaydetAsync(
            SirketKayitlari,
            cancellationToken);
    }

    public IReadOnlyList<SirketKaydi> HizmetSunabilenSirketleriBul(
        string hizmetKimligi,
        string hizmetSurumu)
    {
        if (!_hizmetKatalogu.HizmetVarMi(hizmetKimligi, hizmetSurumu))
            return [];

        return _baglantilar
            .Select(x => x.Kayit)
            .Where(x => x.BagliMi)
            .Where(x => x.HizmetSunuyorMu(hizmetKimligi, hizmetSurumu))
            .OrderByDescending(x => x.KodKalitesiPuani)
            .ThenByDescending(x => x.PerformansPuani)
            .ThenByDescending(x => x.GuvenlikPuani)
            .ThenBy(x => x.SonGecikmeMs)
            .ToList()
            .AsReadOnly();
    }

    public SirketKaydi? EnUygunSirketiBul(
        string hizmetKimligi,
        string hizmetSurumu) =>
        HizmetSunabilenSirketleriBul(hizmetKimligi, hizmetSurumu)
            .FirstOrDefault();

    private static void BaslangicProfiliniUygula(
        SirketKaydi kayit,
        SirketBaglantiAyari ayar)
    {
        kayit.Kasa = Math.Max(0, ayar.BaslangicKasasi);
        kayit.ItibarPuani = BaslangicPuani(ayar.BaslangicItibarPuani, kayit.ItibarPuani);
        kayit.GuvenilirlikPuani = BaslangicPuani(ayar.BaslangicGuvenilirlikPuani, kayit.GuvenilirlikPuani);
        kayit.KodKalitesiPuani = BaslangicPuani(ayar.BaslangicKodKalitesiPuani, kayit.KodKalitesiPuani);
        kayit.PerformansPuani = BaslangicPuani(ayar.BaslangicPerformansPuani, kayit.PerformansPuani);
        kayit.GuvenlikPuani = BaslangicPuani(ayar.BaslangicGuvenlikPuani, kayit.GuvenlikPuani);
        kayit.OrtalamaMusteriMemnuniyeti = BaslangicPuani(
            ayar.BaslangicMusteriMemnuniyeti,
            kayit.OrtalamaMusteriMemnuniyeti);
    }

    private static double BaslangicPuani(double? ayarDegeri, double varsayilanDeger)
    {
        if (!ayarDegeri.HasValue ||
            double.IsNaN(ayarDegeri.Value) ||
            double.IsInfinity(ayarDegeri.Value))
            return varsayilanDeger;

        return Math.Clamp(ayarDegeri.Value, 0, 100);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            KonsolKayitcisi.Uyari(
                $"Finans durumu gönderilemedi | Şirket: {baglanti.Kayit.SirketAdi} | " +
                $"Hata: {exception.Message}");
        }
    }

    private void SirketDurumOzetiniYazdir()
    {
        int bagliSirketSayisi = _baglantilar.Count(x => x.Kayit.BagliMi);
        int toplamHizmetBildirimi = _baglantilar.Sum(x => x.Kayit.Hizmetler.Count);
        decimal toplamSirketKasasi = _baglantilar.Sum(x => x.Kayit.Kasa);
        double ortalamaKalite = _baglantilar.Count == 0 ? 0 : _baglantilar.Average(x => x.Kayit.KodKalitesiPuani);
        double ortalamaPerformans = _baglantilar.Count == 0 ? 0 : _baglantilar.Average(x => x.Kayit.PerformansPuani);
        double ortalamaGuvenlik = _baglantilar.Count == 0 ? 0 : _baglantilar.Average(x => x.Kayit.GuvenlikPuani);
        int toplamBasariliSaldiri = _baglantilar.Sum(x => x.Kayit.BasariliSaldiriSayisi);
        int toplamEngellenenSaldiri = _baglantilar.Sum(x => x.Kayit.EngellenenSaldiriSayisi);

        KonsolKayitcisi.Bilgi(
            $"Şirket özeti | Bağlı: {bagliSirketSayisi}/{_baglantilar.Count} | " +
            $"Hizmet ilanı: {toplamHizmetBildirimi} | Toplam kasa: {toplamSirketKasasi:N2} | " +
            $"Kalite: {ortalamaKalite:N1} | Performans: {ortalamaPerformans:N1} | " +
            $"Güvenlik: {ortalamaGuvenlik:N1} | Yeni sezon saldırı E/B: " +
            $"{toplamEngellenenSaldiri}/{toplamBasariliSaldiri}");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await BilancolariKaydetAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            KonsolKayitcisi.Hata(
                $"Motor kapanırken şirket bilançoları kaydedilemedi: {exception.Message}");
        }

        foreach (SirketBaglantisi baglanti in _baglantilar)
            await baglanti.DisposeAsync();
    }
}
