using System.Reflection;
using System.Text.Json.Nodes;
using SirketMotoru.Protokol;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

/// <summary>
/// Eski sürümlerde oluşan protokol-{guid} piyasa kimliklerini, şirketlerin
/// manifestte ilan ettiği tek teknik kimliğe dönüştürür. Lisans kaydı motorun
/// içinde kalır; uygulama ve işletim sistemi bağlantısı her yerde kanonik kimliği kullanır.
/// </summary>
public sealed class ProtokolKimlikUzlastiricisi
{
    private readonly KodTabanliSirketIsletimYoneticisi _isletim;
    private readonly EkosistemYoneticisi _ekosistem;
    private readonly SirketYoneticisi _sirketler;
    private readonly FieldInfo _ekosistemVeriAlani;
    private readonly FieldInfo _ekosistemKilitAlani;
    private readonly MethodInfo _ekosistemKaydetMetodu;

    public ProtokolKimlikUzlastiricisi(
        KodTabanliSirketIsletimYoneticisi isletim,
        EkosistemYoneticisi ekosistem,
        SirketYoneticisi sirketler)
    {
        _isletim = isletim ?? throw new ArgumentNullException(nameof(isletim));
        _ekosistem = ekosistem ?? throw new ArgumentNullException(nameof(ekosistem));
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));

        Type tur = typeof(EkosistemYoneticisi);
        _ekosistemVeriAlani = tur.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Ekosistem protokol veri alanı bulunamadı.");
        _ekosistemKilitAlani = tur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Ekosistem protokol kilidi bulunamadı.");
        _ekosistemKaydetMetodu = tur.GetMethod("KaydetKilitsizAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Ekosistem protokol kayıt metodu bulunamadı.");
    }

    public async Task UygulaAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, string> eskiKimliktenKanonige = await EslemeOlusturAsync(cancellationToken);
        if (eskiKimliktenKanonige.Count == 0) return;

        SemaphoreSlim kilit = (SemaphoreSlim)(_ekosistemKilitAlani.GetValue(_ekosistem)
            ?? throw new InvalidOperationException("Ekosistem kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try
        {
            EkosistemDosyasi veri = (EkosistemDosyasi)(_ekosistemVeriAlani.GetValue(_ekosistem)
                ?? throw new InvalidOperationException("Ekosistem verisi boş."));
            bool degisti = false;
            foreach (SirketEkosistemAyarlari sirket in veri.Sirketler.Values)
            {
                foreach (UrunDagitimAyari ayar in sirket.Urunler.Values)
                {
                    if (eskiKimliktenKanonige.TryGetValue(
                            ayar.BaglantiProtokoluKimligi,
                            out string? kanonik) &&
                        !string.Equals(ayar.BaglantiProtokoluKimligi, kanonik, StringComparison.OrdinalIgnoreCase))
                    {
                        ayar.BaglantiProtokoluKimligi = kanonik;
                        degisti = true;
                    }
                }
            }

            if (!degisti) return;
            object? sonuc = _ekosistemKaydetMetodu.Invoke(_ekosistem, [cancellationToken]);
            if (sonuc is Task gorev) await gorev;
            KonsolKayitcisi.Basari("Protokol kimlikleri tek kanonik piyasa kimliğinde uzlaştırıldı.");
        }
        finally
        {
            kilit.Release();
        }
    }

    private async Task<Dictionary<string, string>> EslemeOlusturAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, string> sonuc = new(StringComparer.OrdinalIgnoreCase);
        SirketKaydi? ilk = _sirketler.SirketKayitlari.FirstOrDefault();
        if (ilk is null) return sonuc;

        JsonObject panel;
        try
        {
            panel = JsonNode.Parse(await _isletim.PanelJsonuOlusturAsync(
                ilk.SirketKimligi,
                cancellationToken)) as JsonObject ?? new();
        }
        catch
        {
            return sonuc;
        }

        List<OzelProtokolKaydi> piyasa = [];
        try
        {
            piyasa = panel["protokoller"]?.Deserialize<List<OzelProtokolKaydi>>() ?? [];
        }
        catch
        {
            return sonuc;
        }

        foreach (SunucuYayinManifesti manifest in SunucuYayinManifestDeposu.TumunuGetir())
        {
            foreach (SunulanOzelProtokol teknik in manifest.OzelProtokoller)
            {
                OzelProtokolKaydi? kayit = piyasa.FirstOrDefault(x =>
                    x.SahipSirketKimligi.Equals(manifest.SirketKimligi, StringComparison.OrdinalIgnoreCase) &&
                    x.ProtokolAdi.Equals(teknik.ProtokolAdi, StringComparison.OrdinalIgnoreCase) &&
                    x.Surum.Equals(teknik.Surum, StringComparison.OrdinalIgnoreCase));
                if (kayit is null) continue;
                sonuc[kayit.ProtokolKimligi] = teknik.ProtokolKimligi;
                sonuc[teknik.ProtokolKimligi] = teknik.ProtokolKimligi;
            }
        }

        return sonuc;
    }
}
