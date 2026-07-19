using SirketMotoru.Hizmetler;
using SirketMotoru.Protokol;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public static class PanelKatalogV8
{
    public static object HizmetKataloguOlustur(HizmetKatalogu katalog, SirketKaydi sirket)
    {
        ArgumentNullException.ThrowIfNull(katalog);
        ArgumentNullException.ThrowIfNull(sirket);
        SunucuYayinManifesti manifest = SunucuYayinManifestDeposu.Getir(sirket.SirketKimligi);

        var hizmetler = katalog.Hizmetler
            .OrderBy(x => x.HizmetKimligi, StringComparer.OrdinalIgnoreCase)
            .Select(tanim =>
            {
                SunulanHizmet? sahip = sirket.Hizmetler.FirstOrDefault(x =>
                    x.HizmetKimligi.Equals(tanim.HizmetKimligi, StringComparison.OrdinalIgnoreCase) &&
                    x.HizmetSurumu.Equals(tanim.HizmetSurumu, StringComparison.OrdinalIgnoreCase));
                int uygulamaKullanimi = manifest.Uygulamalar.Count(u => u.Ozellikler.Any(o =>
                    o.HizmetKimligi.Equals(tanim.HizmetKimligi, StringComparison.OrdinalIgnoreCase) &&
                    o.HizmetSurumu.Equals(tanim.HizmetSurumu, StringComparison.OrdinalIgnoreCase)));
                return new
                {
                    tanim.HizmetKimligi,
                    tanim.HizmetSurumu,
                    tanim.Aciklama,
                    tanim.Aktif,
                    tanim.ZamanAsimiMs,
                    hizmetAilesi = Aile(tanim.HizmetKimligi),
                    sirketSahip = sahip is not null,
                    sirketAktif = sahip?.Aktif ?? false,
                    sirketFiyati = sahip?.BirimFiyat ?? 0,
                    sirketKapasitesi = sahip?.AzamiEszamanliIs ?? 0,
                    uygulamaKullanimi
                };
            })
            .ToList();

        return new
        {
            toplam = hizmetler.Count,
            sirketinSahipOldugu = hizmetler.Count(x => x.sirketSahip),
            sirketinAktifSundugu = hizmetler.Count(x => x.sirketAktif),
            uygulamalardaKullanilan = hizmetler.Count(x => x.uygulamaKullanimi > 0),
            aileler = hizmetler.GroupBy(x => x.hizmetAilesi, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    aile = g.Key,
                    toplam = g.Count(),
                    sahip = g.Count(x => x.sirketSahip),
                    aktif = g.Count(x => x.sirketAktif)
                })
                .OrderBy(x => x.aile)
                .ToList(),
            hizmetler
        };
    }

    public static object UygulamaKategorileriOlustur(SirketKaydi sirket)
    {
        ArgumentNullException.ThrowIfNull(sirket);
        SunucuYayinManifesti manifest = SunucuYayinManifestDeposu.Getir(sirket.SirketKimligi);
        HashSet<string> sahipHizmetler = sirket.Hizmetler
            .Where(x => x.Aktif)
            .Select(x => x.HizmetKimligi)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var kategoriler = StandartKatalogV6.UygulamaKategorileri
            .OrderBy(x => x.KategoriKimligi, StringComparer.OrdinalIgnoreCase)
            .Select(kategori =>
            {
                var sirketUygulamalari = manifest.Uygulamalar
                    .Where(x => x.Kategori.Equals(kategori.KategoriKimligi, StringComparison.OrdinalIgnoreCase))
                    .Select(x => new
                    {
                        x.UygulamaKimligi,
                        x.UygulamaAdi,
                        x.Surum,
                        x.UrunTuru,
                        hizmetSayisi = x.Ozellikler.Count,
                        zorunluOzellikSayisi = x.Ozellikler.Count(o => o.Zorunlu),
                        desteklenenPlatformlar = x.DesteklenenPlatformlar,
                        desteklenenProtokoller = x.DesteklenenProtokoller
                    })
                    .ToList();
                List<string> eksik = kategori.ZorunluHizmetler
                    .Where(x => !sahipHizmetler.Contains(x))
                    .ToList();
                return new
                {
                    kategori.KategoriKimligi,
                    kategori.KategoriAdi,
                    kategori.Sektor,
                    kategori.UrunTuru,
                    kategori.ZorunluHizmetler,
                    izinliEkHizmetAileleri = kategori.IzinliEkHizmetAileleri.OrderBy(x => x).ToList(),
                    kategori.AsgariHizmetSayisi,
                    kategori.OnerilenHizmetSayisi,
                    kategori.AzamiHizmetSayisi,
                    kategori.EkHizmetKaliteKatkisi,
                    kategori.TabanKapasiteTuketimi,
                    kategori.KullaniciBasinaKapasiteTuketimi,
                    eksikZorunluHizmetler = eksik,
                    sirketTeknikOlarakHazir = eksik.Count == 0,
                    sirketUygulamalari
                };
            })
            .ToList();

        return new
        {
            toplam = kategoriler.Count,
            sirketinUygulamaYayinladigi = kategoriler.Count(x => x.sirketUygulamalari.Count > 0),
            sirketinTeknikOlarakHazirOldugu = kategoriler.Count(x => x.sirketTeknikOlarakHazir),
            sektorler = kategoriler.GroupBy(x => x.Sektor, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    sektor = g.Key,
                    kategoriSayisi = g.Count(),
                    hazir = g.Count(x => x.sirketTeknikOlarakHazir),
                    uygulamali = g.Count(x => x.sirketUygulamalari.Count > 0)
                })
                .OrderBy(x => x.sektor)
                .ToList(),
            kategoriler
        };
    }

    private static string Aile(string hizmetKimligi)
    {
        int nokta = hizmetKimligi.IndexOf('.');
        return nokta <= 0 ? "genel" : hizmetKimligi[..nokta];
    }
}