using System.Reflection;

namespace SirketMotoru.Isletim;

/// <summary>
/// V9.3 kapasite düzeltmesi. Eski tahsis x40 kullanıcı çarpanını etkisizleştirir;
/// ürün kullanıcı tavanını doğrudan ayrılan fiziksel pay yapar ve ticklik hizmet
/// tüketimini aynı şirket kapasite raporuna ekler.
/// </summary>
public sealed class V93KapasiteDengeleyicisi
{
    private readonly object _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _temelKilitAlani;
    private readonly MethodInfo _temelKaydetMetodu;
    private readonly object _v9;
    private readonly FieldInfo _pazarAlani;
    private readonly MethodInfo _pazarKaydetMetodu;

    public V93KapasiteDengeleyicisi(
        KodTabanliSirketIsletimYoneticisi isletim,
        V9EkonomiYoneticisi v9)
    {
        ArgumentNullException.ThrowIfNull(isletim);
        ArgumentNullException.ThrowIfNull(v9);

        FieldInfo temelAlani = typeof(KodTabanliSirketIsletimYoneticisi)
            .GetField("_temel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.3 işletim köprüsü kurulamadı.");
        _temel = temelAlani.GetValue(isletim)
            ?? throw new InvalidOperationException("V9.3 temel işletim yöneticisi boş.");
        Type temelTuru = _temel.GetType();
        _veriAlani = temelTuru.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.3 işletim veri alanı bulunamadı.");
        _temelKilitAlani = temelTuru.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.3 işletim kilidi bulunamadı.");
        _temelKaydetMetodu = temelTuru.GetMethod("TumunuKaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.3 işletim kayıt metodu bulunamadı.");

        _v9 = v9;
        Type v9Turu = v9.GetType();
        _pazarAlani = v9Turu.GetField("_pazar", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.3 pazar alanı bulunamadı.");
        _pazarKaydetMetodu = v9Turu.GetMethod("KaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.3 pazar kayıt metodu bulunamadı.");
    }

    public async Task UrunKapasiteleriniFizikselYapAsync(CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = (SemaphoreSlim)(_temelKilitAlani.GetValue(_temel)
            ?? throw new InvalidOperationException("V9.3 işletim kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try
        {
            SirketIsletimDosyasi veri = (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel)
                ?? throw new InvalidOperationException("V9.3 işletim verisi boş."));
            V9PazarDosyasi pazar = Pazar();

            foreach (SirketIsletimDurumu durum in veri.Sirketler)
            {
                if (!pazar.SirketKapasiteleri.TryGetValue(durum.SirketKimligi, out V9SirketKapasiteDurumu? kapasite))
                    continue;

                foreach (UrunKaydi urun in durum.Urunler)
                {
                    int tahsis = kapasite.Tahsisler.TryGetValue($"urun:{urun.UrunKimligi}", out int pay)
                        ? Math.Max(0, pay)
                        : 0;
                    urun.TabanKullaniciKapasitesi = 0;
                    urun.SatinAlinanKullaniciKapasitesi = 0;
                    urun.AltyapiKapasiteBonusu = 0;
                    urun.KullaniciKapasitesi = urun.Aktif ? tahsis : 0;
                    if (urun.AktifKullaniciSayisi > urun.KullaniciKapasitesi)
                    {
                        urun.ToplamKaybedilenKullanici += urun.AktifKullaniciSayisi - urun.KullaniciKapasitesi;
                        urun.AktifKullaniciSayisi = urun.KullaniciKapasitesi;
                    }
                }
            }

            await TemelKaydetAsync(cancellationToken);
        }
        finally
        {
            kilit.Release();
        }
    }

    public async Task RaporuGuncelleAsync(CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = (SemaphoreSlim)(_temelKilitAlani.GetValue(_temel)
            ?? throw new InvalidOperationException("V9.3 işletim kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try
        {
            SirketIsletimDosyasi veri = (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel)
                ?? throw new InvalidOperationException("V9.3 işletim verisi boş."));
            V9PazarDosyasi pazar = Pazar();

            foreach ((string sirketKimligi, V9SirketKapasiteDurumu kapasite) in pazar.SirketKapasiteleri)
            {
                SirketIsletimDurumu? durum = veri.Sirketler.FirstOrDefault(x =>
                    x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
                int urunKullanimi = durum?.Urunler.Where(x => x.Aktif).Sum(x => Math.Max(0, x.AktifKullaniciSayisi)) ?? 0;
                V93HizmetKapasiteAnligi hizmet = V93HizmetKapasiteDeposu.Getir(sirketKimligi);

                kapasite.UrunKullanilanKapasite = urunKullanimi;
                kapasite.HizmetKullanilanKapasite = hizmet.KullanilanHizmetKapasitesi;
                kapasite.HizmetReddedilenIsSayisi = hizmet.ReddedilenIsSayisi;
                kapasite.KullanilanKapasite = Math.Max(0, urunKullanimi + hizmet.KullanilanHizmetKapasitesi);

                V9SirketPazarOzeti? ozet = pazar.SirketOzetleri.FirstOrDefault(x =>
                    x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
                if (ozet is not null)
                {
                    ozet.KullanilanKapasite = kapasite.KullanilanKapasite;
                    ozet.HizmetKullanilanKapasite = kapasite.HizmetKullanilanKapasite;
                    ozet.UrunKullanilanKapasite = kapasite.UrunKullanilanKapasite;
                }
            }

            V9PazarDeposu.Guncelle(pazar);
            await PazarKaydetAsync(cancellationToken);
        }
        finally
        {
            kilit.Release();
        }
    }

    private V9PazarDosyasi Pazar() =>
        (V9PazarDosyasi)(_pazarAlani.GetValue(_v9)
            ?? throw new InvalidOperationException("V9.3 pazar verisi boş."));

    private async Task TemelKaydetAsync(CancellationToken cancellationToken)
    {
        object? sonuc = _temelKaydetMetodu.Invoke(_temel, [cancellationToken]);
        if (sonuc is Task gorev) await gorev;
    }

    private async Task PazarKaydetAsync(CancellationToken cancellationToken)
    {
        object? sonuc = _pazarKaydetMetodu.Invoke(_v9, [cancellationToken]);
        if (sonuc is Task gorev) await gorev;
    }
}
