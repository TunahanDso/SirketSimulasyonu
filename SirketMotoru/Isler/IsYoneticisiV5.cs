using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using SirketMotoru.Ag;
using SirketMotoru.Isletim;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Protokol;
using SirketMotoru.Protokol.Mesajlar;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isler;

public sealed class IsYoneticisi
{
    private static readonly Regex ZorunluAlan = new(@"(?<alan>[A-Za-z_][A-Za-z0-9_-]*)\s+zorunludur", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly SirketYoneticisi _sirketler;
    private readonly MusteriYoneticisi _musteriler;
    private readonly SonucDogrulayicisi _dogrulayici;
    private readonly ConcurrentDictionary<string, byte> _islenen = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _rastgele = new(20260729);
    private readonly object _rastgeleKilidi = new();

    public IsYoneticisi(SirketYoneticisi sirketler, MusteriYoneticisi musteriler, SonucDogrulayicisi dogrulayici)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _musteriler = musteriler ?? throw new ArgumentNullException(nameof(musteriler));
        _dogrulayici = dogrulayici ?? throw new ArgumentNullException(nameof(dogrulayici));
    }

    public async Task<IsIslemeOzeti> TalepleriIsleAsync(long tick, IReadOnlyList<HizmetTalebi> talepler, CancellationToken ct)
    {
        IsIslemeOzeti ozet = new()
        {
            TickNumarasi = tick,
            ToplamTalepSayisi = talepler.Count,
            KotuNiyetliIsSayisi = talepler.Count(x => x.KotuNiyetli)
        };
        foreach (HizmetTalebi talep in talepler)
        {
            ct.ThrowIfCancellationRequested();
            Ozetle(ozet, await TalebiIsleAsync(tick, talep, ct));
        }
        KonsolKayitcisi.Bilgi(
            $"V9.3 iş özeti | Örnek {ozet.ToplamTalepSayisi:N0} | Başarılı {ozet.BasariliIsSayisi:N0} | " +
            $"Başarısız {ozet.BasarisizIsSayisi:N0} | Düşük marjlı ciro {ozet.ToplamCiro:N2} TL");
        return ozet;
    }

    public async Task<IsAtamaSonucu> TalebiIsleAsync(long tick, HizmetTalebi talep, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(talep.IsKimligi) || !_islenen.TryAdd(talep.IsKimligi, 0))
            return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, null, "Geçersiz veya tekrarlanan iş.");

        Musteri? musteri = _musteriler.MusteriyiBul(talep.MusteriKimligi);
        if (musteri is null || !musteri.Aktif)
            return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, null, "Müşteri aktif değil.");

        decimal butce = Math.Min(talep.AzamiButce, musteri.Bakiye);
        IReadOnlyList<SirketAdayi> adaylar = Adaylar(musteri, talep, butce);
        if (adaylar.Count == 0)
        {
            _musteriler.IslemSonucunuKaydet(talep, false, 0, 0, "Piyasada uygun veya boş kapasiteli arz yok.");
            return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, null, "Hizmeti karşılayan boş kapasiteli şirket bulunamadı.");
        }

        SirketAdayi aday = Sec(adaylar, talep.KotuNiyetli);
        SirketKaydi sirket = aday.Sirket;
        if (!V93HizmetKapasiteDeposu.RezerveEt(
                sirket.SirketKimligi,
                talep.HizmetKimligi,
                talep.ZorlukSeviyesi,
                talep.KotuNiyetli,
                out int kapasiteMaliyeti))
        {
            _musteriler.IslemSonucunuKaydet(talep, false, 0, 0, "Şirketin ortak hizmet havuzu dolu.");
            return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, sirket.SirketKimligi, "Hizmet kapasitesi dolu.");
        }

        decimal ucret = MotorHizmetFiyatlari.Fiyat(talep.HizmetKimligi, talep.HizmetSurumu);
        int zamanAsimi = talep.ZamanAsimiMs > 0 ? Math.Clamp(talep.ZamanAsimiMs, 500, 30_000) : 5_000;
        talep.SecilenSirketKimligi = sirket.SirketKimligi;
        talep.TeklifEdilenTutar = ucret;
        sirket.IsBaslat(talep.HizmetKimligi, talep.HizmetSurumu);

        IsIstegiMesaji istek = new()
        {
            MesajTuru = MesajTurleri.IsIstegi,
            IstekKimligi = $"istek-{Guid.NewGuid():N}",
            IsKimligi = talep.IsKimligi,
            TickNumarasi = tick,
            MusteriKimligi = talep.MusteriKimligi,
            HizmetKimligi = talep.HizmetKimligi,
            HizmetSurumu = talep.HizmetSurumu,
            TeklifEdilenTutar = ucret,
            ZamanAsimiMs = zamanAsimi,
            IstekVerisiJson = talep.IstekVerisiJson,
            OlusturulmaZamani = DateTimeOffset.UtcNow
        };

        try
        {
            IsSonucuMesaji cevap = await aday.Baglanti.IsIstegiGonderVeSonucuBekleAsync(istek, ct);
            if (talep.KotuNiyetli) return SiberSonuc(tick, talep, sirket, cevap, zamanAsimi);

            SonucDogrulamaSonucu dogrulama = _dogrulayici.Dogrula(talep, cevap);
            if (!dogrulama.Gecerli)
            {
                if (MotorKusuru(talep, cevap, dogrulama))
                {
                    sirket.IsIptalEt();
                    _musteriler.IslemSonucunuKaydet(talep, false, 0, cevap.IslemSuresiMs, dogrulama.Aciklama);
                    return IsAtamaSonucu.BasarisizSonuc(
                        talep.IsKimligi,
                        sirket.SirketKimligi,
                        $"Motor istek kusuru; şirket etkilenmedi. Kapasite tüketimi: {kapasiteMaliyeti}.",
                        cevap.IslemSuresiMs);
                }
                return Basarisiz(talep, sirket, ucret, cevap.IslemSuresiMs, dogrulama.Aciklama, false, 1.0);
            }

            if (!_musteriler.MusteridenOdemeAl(talep.MusteriKimligi, ucret))
            {
                sirket.IsIptalEt();
                return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, sirket.SirketKimligi, "Ödeme alınamadı.");
            }

            double memnuniyet = Math.Clamp(100 - cevap.IslemSuresiMs / Math.Max(1, zamanAsimi) * 70, 25, 100);
            sirket.BasariliIsKaydet(ucret, cevap.IslemSuresiMs, memnuniyet, talep.ZorlukSeviyesi, zamanAsimi);
            _musteriler.IslemSonucunuKaydet(talep, true, ucret, cevap.IslemSuresiMs, $"Tam ödeme yapıldı. Kapasite: {kapasiteMaliyeti}.");
            V9HizmetSonucDeposu.Kaydet(Anahtar(talep), sirket.SirketKimligi, sirket.SirketAdi, true, ucret);
            return IsAtamaSonucu.BasariliSonuc(talep.IsKimligi, sirket.SirketKimligi, ucret, cevap.IslemSuresiMs, cevap.SonucVerisiJson);
        }
        catch (TimeoutException hata)
        {
            if (talep.KotuNiyetli) return SiberIhlal(tick, talep, sirket, zamanAsimi, "Saldırı zaman aşımı oluşturdu.");
            return Basarisiz(talep, sirket, ucret, zamanAsimi, $"Zaman aşımı: {hata.Message}", true, 1.15);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sirket.IsIptalEt();
            throw;
        }
        catch (Exception hata)
        {
            if (talep.KotuNiyetli) return SiberIhlal(tick, talep, sirket, 0, "Saldırı sunucu hatasına yol açtı.");
            return Basarisiz(talep, sirket, ucret, 0, hata.Message, false, 1.25);
        }
    }

    private IsAtamaSonucu Basarisiz(
        HizmetTalebi talep,
        SirketKaydi sirket,
        decimal tam,
        double sure,
        string aciklama,
        bool zamanAsimi,
        double carpan)
    {
        decimal yarim = decimal.Round(tam * 0.5m, 2);
        decimal odeme = _musteriler.MusteridenOdemeAl(talep.MusteriKimligi, yarim) ? yarim : 0;
        if (odeme > 0)
        {
            sirket.Kasa += odeme;
            sirket.ToplamGelir += odeme;
        }
        sirket.IsIptalEt();
        sirket.IptalEdilenIsSayisi = Math.Max(0, sirket.IptalEdilenIsSayisi - 1);
        if (zamanAsimi) sirket.ZamanAsiminaUgrayanIsSayisi++; else sirket.BasarisizIsSayisi++;
        sirket.SonBasarisizIsZamani = DateTimeOffset.UtcNow;
        double s = Math.Clamp((0.7 + talep.ZorlukSeviyesi * 0.12) * carpan, 0.75, 1.7);
        sirket.ItibarPuani = Math.Max(0, sirket.ItibarPuani - 0.035 * s);
        sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - 0.060 * s);
        sirket.KodKalitesiPuani = Math.Max(0, sirket.KodKalitesiPuani - 0.040 * s);
        sirket.PerformansPuani = Math.Max(0, sirket.PerformansPuani - (zamanAsimi ? 0.075 : 0.025) * s);
        sirket.OrtalamaMusteriMemnuniyeti = Math.Max(0, sirket.OrtalamaMusteriMemnuniyeti - 0.045 * s);
        _musteriler.IslemSonucunuKaydet(talep, false, odeme, sure, aciklama);
        V9HizmetSonucDeposu.Kaydet(Anahtar(talep), sirket.SirketKimligi, sirket.SirketAdi, false, odeme);
        return new IsAtamaSonucu
        {
            Basarili = false,
            IsKimligi = talep.IsKimligi,
            SirketKimligi = sirket.SirketKimligi,
            IslemTutari = odeme,
            IslemSuresiMs = Math.Max(0, sure),
            SonucAciklamasi = aciklama
        };
    }

    private IsAtamaSonucu SiberSonuc(long tick, HizmetTalebi talep, SirketKaydi sirket, IsSonucuMesaji cevap, int zamanAsimi)
    {
        if (!cevap.Basarili && new[] { "GUVENLIK_REDDI", "ISTEK_GUVENLI_DEGIL", "KOTU_NIYETLI_ISTEK" }
                .Contains(cevap.HataKodu ?? "", StringComparer.OrdinalIgnoreCase))
        {
            sirket.SaldiriEngellendiKaydet(talep.ZorlukSeviyesi, cevap.IslemSuresiMs, zamanAsimi);
            _musteriler.IslemSonucunuKaydet(talep, false, 0, cevap.IslemSuresiMs, "Saldırı engellendi.");
            V93SiberOlayDeposu.Kaydet(tick, sirket.SirketKimligi, sirket.SirketAdi, talep.HizmetKimligi, talep.ZorlukSeviyesi, true, 0);
            return IsAtamaSonucu.GuvenlikSonucu(talep.IsKimligi, sirket.SirketKimligi, true, 0, cevap.IslemSuresiMs, "Saldırı engellendi.");
        }
        return SiberIhlal(tick, talep, sirket, cevap.IslemSuresiMs, "Saldırı normal iş gibi çalıştırıldı.");
    }

    private IsAtamaSonucu SiberIhlal(long tick, HizmetTalebi talep, SirketKaydi sirket, double sure, string aciklama)
    {
        decimal istenen = Math.Clamp(talep.OlasiGuvenlikKaybi * 1.8m, 150m, 15_000m);
        decimal kasaTavani = Math.Max(500m, Math.Max(0, sirket.Kasa) * (talep.ZorlukSeviyesi >= 9 ? 0.18m : 0.12m));
        decimal kayip = Math.Min(Math.Max(0, sirket.Kasa), Math.Min(istenen, kasaTavani));
        sirket.IsIptalEt();
        sirket.IptalEdilenIsSayisi = Math.Max(0, sirket.IptalEdilenIsSayisi - 1);
        sirket.BasarisizIsSayisi++;
        sirket.BasariliSaldiriSayisi++;
        sirket.Kasa -= kayip;
        sirket.ToplamGuvenlikKaybi += kayip;
        double agirlik = Math.Clamp(0.8 + talep.ZorlukSeviyesi * 0.08, 1, 1.7);
        sirket.GuvenlikPuani = Math.Max(0, sirket.GuvenlikPuani - 0.30 * agirlik);
        sirket.GuvenilirlikPuani = Math.Max(0, sirket.GuvenilirlikPuani - 0.18 * agirlik);
        sirket.ItibarPuani = Math.Max(0, sirket.ItibarPuani - 0.12 * agirlik);
        _musteriler.IslemSonucunuKaydet(talep, false, 0, sure, aciklama);
        V93SiberOlayDeposu.Kaydet(tick, sirket.SirketKimligi, sirket.SirketAdi, talep.HizmetKimligi, talep.ZorlukSeviyesi, false, -kayip);
        return IsAtamaSonucu.GuvenlikSonucu(talep.IsKimligi, sirket.SirketKimligi, false, kayip, sure, aciklama);
    }

    private IReadOnlyList<SirketAdayi> Adaylar(Musteri musteri, HizmetTalebi talep, decimal butce)
    {
        List<SirketAdayi> sonuc = [];
        foreach (SirketBaglantisi baglanti in _sirketler.BaglantilariGetir())
        {
            SirketKaydi s = baglanti.Kayit;
            SunulanHizmet? h = s.HizmetiBul(talep.HizmetKimligi, talep.HizmetSurumu);
            decimal fiyat = MotorHizmetFiyatlari.Fiyat(talep.HizmetKimligi, talep.HizmetSurumu);
            if (!baglanti.Bagli || h is null || !h.Aktif || fiyat > butce ||
                !V93HizmetKapasiteDeposu.SigabilirMi(s.SirketKimligi, talep.HizmetKimligi, talep.ZorlukSeviyesi, talep.KotuNiyetli))
                continue;

            double sadakat = string.Equals(musteri.TercihEdilenSirketKimligi, s.SirketKimligi, StringComparison.OrdinalIgnoreCase) ? 100 : 50;
            double puan = s.KodKalitesiPuani * .28 + s.PerformansPuani * .19 + s.GuvenlikPuani * .18 +
                s.GuvenilirlikPuani * .15 + s.ItibarPuani * .08 + s.KapasitePuaniHesapla(h) * .07 + sadakat * .05;
            sonuc.Add(new SirketAdayi
            {
                Baglanti = baglanti,
                Sirket = s,
                Hizmet = h,
                FiyatPuani = 100,
                ItibarPuani = s.ItibarPuani,
                GuvenilirlikPuani = s.GuvenilirlikPuani,
                KodKalitesiPuani = s.KodKalitesiPuani,
                PerformansPuani = s.PerformansPuani,
                GuvenlikPuani = s.GuvenlikPuani,
                HizPuani = s.HizPuaniHesapla(),
                KapasitePuani = s.KapasitePuaniHesapla(h),
                SadakatPuani = sadakat,
                ToplamPuan = puan
            });
        }
        return sonuc.OrderByDescending(x => x.ToplamPuan).Take(4).ToList();
    }

    private SirketAdayi Sec(IReadOnlyList<SirketAdayi> adaylar, bool saldiri)
    {
        if (adaylar.Count == 1) return adaylar[0];
        double Agirlik(SirketAdayi x) => saldiri
            ? Math.Max(1, 105 - x.GuvenlikPuani + (100 - x.KodKalitesiPuani) * .35)
            : Math.Max(1, x.ToplamPuan);
        double secim;
        lock (_rastgeleKilidi) secim = _rastgele.NextDouble() * adaylar.Sum(Agirlik);
        foreach (SirketAdayi a in adaylar)
        {
            secim -= Agirlik(a);
            if (secim <= 0) return a;
        }
        return adaylar[^1];
    }

    private static bool MotorKusuru(HizmetTalebi talep, IsSonucuMesaji cevap, SonucDogrulamaSonucu dogrulama)
    {
        if (cevap.Basarili || cevap.HataKodu is not ("ISTEK_VERISI_GECERSIZ" or "GECERSIZ_ISTEK" or "EKSIK_ALAN"))
            return false;
        Match m = ZorunluAlan.Match(dogrulama.Aciklama ?? "");
        if (!m.Success) return false;
        try
        {
            using JsonDocument d = JsonDocument.Parse(talep.IstekVerisiJson);
            return !d.RootElement.TryGetProperty(m.Groups["alan"].Value, out JsonElement v) ||
                v.ValueKind == JsonValueKind.Null ||
                v.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(v.GetString());
        }
        catch { return true; }
    }

    private static string Anahtar(HizmetTalebi talep) => $"{talep.HizmetKimligi}@{talep.HizmetSurumu}";

    private static void Ozetle(IsIslemeOzeti ozet, IsAtamaSonucu sonuc)
    {
        if (sonuc.GuvenlikOlayi)
        {
            if (sonuc.SaldiriEngellendi) ozet.EngellenenSaldiriSayisi++;
            else
            {
                ozet.BasariliSaldiriSayisi++;
                ozet.ToplamGuvenlikKaybi += sonuc.GuvenlikKaybi;
            }
            ozet.BasarisizIsSayisi++;
        }
        else if (sonuc.Basarili) ozet.BasariliIsSayisi++;
        else ozet.BasarisizIsSayisi++;
        ozet.ToplamCiro += Math.Max(0, sonuc.IslemTutari);
        ozet.ToplamIslemSuresiMs += Math.Max(0, sonuc.IslemSuresiMs);
    }
}
