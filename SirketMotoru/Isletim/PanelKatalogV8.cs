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

        return katalog.Hizmetler
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
                    sahipMi = sahip is not null,
                    sirketSahip = sahip is not null,
                    sirketAktif = sahip?.Aktif ?? false,
                    birimFiyat = sahip is null
                        ? MotorHizmetFiyatlari.Fiyat(tanim.HizmetKimligi, tanim.HizmetSurumu)
                        : MotorHizmetFiyatlari.Fiyat(sahip.HizmetKimligi, sahip.HizmetSurumu),
                    sirketFiyati = sahip is null
                        ? 0
                        : MotorHizmetFiyatlari.Fiyat(sahip.HizmetKimligi, sahip.HizmetSurumu),
                    sirketKapasitesi = sahip?.AzamiEszamanliIs ?? 0,
                    uygulamaKullanimi
                };
            })
            .ToList();
    }

    public static object UygulamaKategorileriOlustur(SirketKaydi sirket)
    {
        ArgumentNullException.ThrowIfNull(sirket);
        SunucuYayinManifesti manifest = SunucuYayinManifestDeposu.Getir(sirket.SirketKimligi);
        HashSet<string> sahipHizmetler = sirket.Hizmetler
            .Where(x => x.Aktif)
            .Select(x => x.HizmetKimligi)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return StandartKatalogV6.UygulamaKategorileri
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
                    teknikOlarakHazirMi = eksik.Count == 0,
                    sirketTeknikOlarakHazir = eksik.Count == 0,
                    sirketUygulamalari
                };
            })
            .ToList();
    }

    private static string Aile(string hizmetKimligi)
    {
        int nokta = hizmetKimligi.IndexOf('.');
        return nokta <= 0 ? "genel" : hizmetKimligi[..nokta];
    }
}
