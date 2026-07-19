using System.Reflection;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class PazarGelirDuzeltmeYoneticisi
{
    private readonly SirketYoneticisi _sirketler;
    private readonly object _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _kilitAlani;
    private readonly MethodInfo _kaydetMetodu;
    private readonly MethodInfo _degerleMetodu;
    private readonly Dictionary<string, UrunAnlikGoruntusu> _anlikGoruntuler =
        new(StringComparer.OrdinalIgnoreCase);

    public PazarGelirDuzeltmeYoneticisi(
        KodTabanliSirketIsletimYoneticisi isletim,
        SirketYoneticisi sirketler)
    {
        ArgumentNullException.ThrowIfNull(isletim);
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));

        FieldInfo temelAlani = typeof(KodTabanliSirketIsletimYoneticisi).GetField(
            "_temel",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Pazar gelir köprüsü kurulamadı: _temel alanı bulunamadı.");
        _temel = temelAlani.GetValue(isletim)
            ?? throw new InvalidOperationException(
                "Pazar gelir köprüsü kurulamadı: temel yönetici boş.");

        Type temelTur = _temel.GetType();
        _veriAlani = temelTur.GetField(
            "_veri",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Pazar gelir köprüsü kurulamadı: _veri alanı bulunamadı.");
        _kilitAlani = temelTur.GetField(
            "_kilit",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Pazar gelir köprüsü kurulamadı: _kilit alanı bulunamadı.");
        _kaydetMetodu = temelTur.GetMethod(
            "TumunuKaydetAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Pazar gelir köprüsü kurulamadı: kayıt metodu bulunamadı.");
        _degerleMetodu = temelTur.GetMethod(
            "Degerle",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Pazar gelir köprüsü kurulamadı: değerleme metodu bulunamadı.");
    }

    public async Task TickOncesiHazirlaAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = TemelKilidiGetir();
        await kilit.WaitAsync(cancellationToken);
        try
        {
            _anlikGoruntuler.Clear();
            SirketIsletimDosyasi veri = TemelVeriyiGetir();
            foreach (SirketIsletimDurumu durum in veri.Sirketler)
            {
                foreach (UrunKaydi urun in durum.Urunler)
                {
                    _anlikGoruntuler[urun.UrunKimligi] = new UrunAnlikGoruntusu
                    {
                        TickNumarasi = tickNumarasi,
                        SirketKimligi = durum.SirketKimligi,
                        UrunKimligi = urun.UrunKimligi,
                        AktifKullaniciSayisi = urun.AktifKullaniciSayisi,
                        ToplamGelir = urun.ToplamGelir
                    };
                }
            }
        }
        finally
        {
            kilit.Release();
        }
    }

    public async Task TickSonrasiDuzeltAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = TemelKilidiGetir();
        await kilit.WaitAsync(cancellationToken);
        try
        {
            SirketIsletimDosyasi veri = TemelVeriyiGetir();
            Dictionary<string, UrunFiyatDurumu> fiyatlar =
                PazarFiyatDeposu.Getir().Urunler.ToDictionary(
                    x => x.UrunKimligi,
                    StringComparer.OrdinalIgnoreCase);

            decimal toplamIade = 0;
            int etkilenenUrun = 0;

            foreach (SirketIsletimDurumu durum in veri.Sirketler)
            {
                SirketKaydi? sirket = SirketBul(durum.SirketKimligi);
                if (sirket is null) continue;

                foreach (UrunKaydi urun in durum.Urunler)
                {
                    if (!_anlikGoruntuler.TryGetValue(
                            urun.UrunKimligi,
                            out UrunAnlikGoruntusu? onceki) ||
                        !fiyatlar.TryGetValue(
                            urun.UrunKimligi,
                            out UrunFiyatDurumu? fiyat))
                    {
                        continue;
                    }

                    decimal buTickGeliri = Math.Max(
                        0,
                        urun.ToplamGelir - onceki.ToplamGelir);
                    if (buTickGeliri <= 0) continue;

                    double kabulCarpani = GelirKabulCarpani(fiyat.FiyatOrani);
                    decimal kabulEdilen = decimal.Round(
                        buTickGeliri * (decimal)kabulCarpani,
                        2);
                    decimal iade = Math.Max(0, buTickGeliri - kabulEdilen);
                    if (iade <= 0) continue;

                    urun.ToplamGelir = Math.Max(0, urun.ToplamGelir - iade);
                    durum.ToplamUrunGeliri = Math.Max(
                        0,
                        durum.ToplamUrunGeliri - iade);
                    if (urun.FiyatlandirmaModeli is "abonelik" or "freemium")
                    {
                        durum.ToplamAbonelikGeliri = Math.Max(
                            0,
                            durum.ToplamAbonelikGeliri - iade);
                    }

                    sirket.ToplamGelir = Math.Max(
                        0,
                        sirket.ToplamGelir - iade);
                    decimal kasadanAlinan = Math.Min(
                        Math.Max(0, sirket.Kasa),
                        iade);
                    sirket.Kasa -= kasadanAlinan;
                    decimal odenemeyen = iade - kasadanAlinan;
                    if (odenemeyen > 0)
                    {
                        durum.OdenemeyenGider += odenemeyen;
                        durum.KrediNotu = Math.Clamp(
                            durum.KrediNotu - 12,
                            300,
                            900);
                        sirket.GuvenilirlikPuani = Math.Max(
                            0,
                            sirket.GuvenilirlikPuani - 0.8);
                    }

                    durum.SonIslemler.Add(new IsletimIslemKaydi
                    {
                        IslemKimligi = $"fiyat-iade-{Guid.NewGuid():N}",
                        TickNumarasi = tickNumarasi,
                        IslemTuru = "fiyat-iadesi",
                        Aciklama =
                            $"{urun.UrunAdi}: pazar dışı fiyat nedeniyle " +
                            $"{iade:N2} TL müşteri iadesi/chargeback uygulandı. " +
                            $"Fiyat oranı ×{fiyat.FiyatOrani:N2}.",
                        Tutar = -iade
                    });
                    if (durum.SonIslemler.Count > 250)
                    {
                        durum.SonIslemler = durum.SonIslemler
                            .TakeLast(250)
                            .ToList();
                    }

                    toplamIade += iade;
                    etkilenenUrun++;
                }

                durum.ToplamAboneSayisi = durum.Urunler
                    .Where(x => x.Aktif)
                    .Sum(x => x.AktifKullaniciSayisi);
                _degerleMetodu.Invoke(null, [sirket, durum]);
            }

            if (etkilenenUrun > 0)
            {
                KonsolKayitcisi.Uyari(
                    $"FİYAT İADESİ | Tick {tickNumarasi} | " +
                    $"Ürün: {etkilenenUrun} | Toplam chargeback: {toplamIade:N2} TL");
            }

            await KaydetTemelAsync(cancellationToken);
            _anlikGoruntuler.Clear();
        }
        finally
        {
            kilit.Release();
        }
    }

    private static double GelirKabulCarpani(double fiyatOrani) =>
        fiyatOrani switch
        {
            <= 1.30 => 1.00,
            <= 1.55 => 0.72,
            <= 1.85 => 0.42,
            <= 2.25 => 0.16,
            <= 3.00 => 0.035,
            _ => 0.00
        };

    private SemaphoreSlim TemelKilidiGetir() =>
        (SemaphoreSlim)(_kilitAlani.GetValue(_temel)
            ?? throw new InvalidOperationException(
                "İşletim kilidi bulunamadı."));

    private SirketIsletimDosyasi TemelVeriyiGetir() =>
        (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel)
            ?? throw new InvalidOperationException(
                "İşletim verisi bulunamadı."));

    private SirketKaydi? SirketBul(string kimlik) =>
        _sirketler.SirketKayitlari.FirstOrDefault(
            x => string.Equals(
                x.SirketKimligi,
                kimlik,
                StringComparison.OrdinalIgnoreCase));

    private async Task KaydetTemelAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            object? sonuc = _kaydetMetodu.Invoke(
                _temel,
                [cancellationToken]);
            if (sonuc is Task task) await task;
        }
        catch (TargetInvocationException e)
            when (e.InnerException is not null)
        {
            throw e.InnerException;
        }
    }

    private sealed class UrunAnlikGoruntusu
    {
        public long TickNumarasi { get; init; }
        public string SirketKimligi { get; init; } = string.Empty;
        public string UrunKimligi { get; init; } = string.Empty;
        public int AktifKullaniciSayisi { get; init; }
        public decimal ToplamGelir { get; init; }
    }
}
