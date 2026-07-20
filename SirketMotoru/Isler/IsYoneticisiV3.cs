using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using SirketMotoru.Ag;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Protokol;
using SirketMotoru.Protokol.Mesajlar;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isler;

public sealed class IsYoneticisi
{
    private const int EnFazlaAdaySayisi = 3;
    private const int VarsayilanZamanAsimiMs = 5_000;
    private static readonly Regex ZorunluAlanDeseni = new(
        @"(?<alan>[A-Za-z_][A-Za-z0-9_-]*)\s+zorunludur",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly SirketYoneticisi _sirketYoneticisi;
    private readonly MusteriYoneticisi _musteriYoneticisi;
    private readonly SonucDogrulayicisi _sonucDogrulayicisi;
    private readonly ConcurrentDictionary<string, byte> _islenenIsler =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _rastgele = new();
    private readonly object _rastgeleKilidi = new();

    public IsYoneticisi(
        SirketYoneticisi sirketYoneticisi,
        MusteriYoneticisi musteriYoneticisi,
        SonucDogrulayicisi sonucDogrulayicisi)
    {
        _sirketYoneticisi = sirketYoneticisi ?? throw new ArgumentNullException(nameof(sirketYoneticisi));
        _musteriYoneticisi = musteriYoneticisi ?? throw new ArgumentNullException(nameof(musteriYoneticisi));
        _sonucDogrulayicisi = sonucDogrulayicisi ?? throw new ArgumentNullException(nameof(sonucDogrulayicisi));
    }

    public async Task<IsIslemeOzeti> TalepleriIsleAsync(
        long tickNumarasi,
        IReadOnlyList<HizmetTalebi> talepler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(talepler);
        IsIslemeOzeti ozet = new()
        {
            TickNumarasi = tickNumarasi,
            ToplamTalepSayisi = talepler.Count,
            KotuNiyetliIsSayisi = talepler.Count(x => x.KotuNiyetli)
        };

        foreach (HizmetTalebi talep in talepler)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsAtamaSonucu sonuc = await TalebiIsleAsync(tickNumarasi, talep, cancellationToken);
            OzetGuncelle(ozet, sonuc);
        }

        TickOzetiniYaz(ozet);
        return ozet;
    }

    public async Task<IsAtamaSonucu> TalebiIsleAsync(
        long tickNumarasi,
        HizmetTalebi talep,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(talep);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(talep.IsKimligi))
            return IsAtamaSonucu.BasarisizSonuc(string.Empty, null, "Talebin iş kimliği bulunmuyor.");
        if (!_islenenIsler.TryAdd(talep.IsKimligi, 0))
            return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, null, "Aynı iş ikinci kez işlenmeye çalışıldı.");

        try
        {
            return await TalebiIsleIcAsync(tickNumarasi, talep, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception hata)
        {
            KonsolKayitcisi.Hata(
                $"İş işlenirken beklenmeyen hata oluştu | İş: {talep.IsKimligi} | Hata: {hata.Message}");
            return IsAtamaSonucu.BasarisizSonuc(
                talep.IsKimligi,
                talep.SecilenSirketKimligi,
                hata.Message);
        }
    }

    private async Task<IsAtamaSonucu> TalebiIsleIcAsync(
        long tickNumarasi,
        HizmetTalebi talep,
        CancellationToken cancellationToken)
    {
        Musteri? musteri = _musteriYoneticisi.MusteriyiBul(talep.MusteriKimligi);
        if (musteri is null)
            return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, null, $"Müşteri bulunamadı: {talep.MusteriKimligi}");
        if (!musteri.Aktif)
            return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, null, "Müşteri hesabı aktif değil.");

        decimal kullanilabilirButce = Math.Min(talep.AzamiButce, musteri.Bakiye);
        if (kullanilabilirButce <= 0)
        {
            _musteriYoneticisi.IslemSonucunuKaydet(talep, false, 0, 0, "Müşterinin kullanılabilir bakiyesi yok.");
            return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, null, "Müşteri bakiyesi yetersiz.");
        }

        IReadOnlyList<SirketAdayi> adaylar = SirketAdaylariniOlustur(musteri, talep, kullanilabilirButce);
        if (adaylar.Count == 0)
        {
            _musteriYoneticisi.IslemSonucunuKaydet(talep, false, 0, 0, "Uygun şirket bulunamadı.");
            KonsolKayitcisi.Uyari(
                $"İşe uygun şirket bulunamadı | İş: {talep.IsKimligi} | " +
                $"Hizmet: {talep.HizmetKimligi}@{talep.HizmetSurumu} | Bütçe: {kullanilabilirButce:N2}");
            return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, null, "Hizmeti sunabilen uygun şirket bulunamadı.");
        }

        SirketAdayi secilen = AgirlikliSirketSec(adaylar, talep);
        SirketKaydi sirket = secilen.Sirket;
        SunulanHizmet hizmet = secilen.Hizmet;
        SirketBaglantisi baglanti = secilen.Baglanti;
        decimal islemTutari = hizmet.BirimFiyat;
        int zamanAsimiMs = ZamanAsiminiHesapla(talep);

        talep.SecilenSirketKimligi = sirket.SirketKimligi;
        talep.TeklifEdilenTutar = islemTutari;
        sirket.IsBaslat(hizmet.HizmetKimligi, hizmet.HizmetSurumu);

        IsIstegiMesaji isIstegi = new()
        {
            MesajTuru = MesajTurleri.IsIstegi,
            IstekKimligi = $"istek-{Guid.NewGuid():N}",
            IsKimligi = talep.IsKimligi,
            TickNumarasi = tickNumarasi,
            MusteriKimligi = talep.MusteriKimligi,
            HizmetKimligi = talep.HizmetKimligi,
            HizmetSurumu = talep.HizmetSurumu,
            TeklifEdilenTutar = islemTutari,
            ZamanAsimiMs = zamanAsimiMs,
            IstekVerisiJson = talep.IstekVerisiJson,
            OlusturulmaZamani = DateTimeOffset.UtcNow
        };

        try
        {
            IsSonucuMesaji sirketSonucu = await baglanti.IsIstegiGonderVeSonucuBekleAsync(
                isIstegi,
                cancellationToken);
            if (talep.KotuNiyetli)
                return GuvenlikSinamasiniSonuclandir(talep, sirket, sirketSonucu, zamanAsimiMs);

            SonucDogrulamaSonucu dogrulama = _sonucDogrulayicisi.Dogrula(talep, sirketSonucu);
            if (!dogrulama.Gecerli)
            {
                if (MotorIstekSozlesmesiKusuruMu(talep, sirketSonucu, dogrulama))
                {
                    sirket.IsIptalEt();
                    _musteriYoneticisi.IslemSonucunuKaydet(
                        talep,
                        false,
                        0,
                        sirketSonucu.IslemSuresiMs,
                        dogrulama.Aciklama);
                    KonsolKayitcisi.Uyari(
                        $"MOTOR İSTEK SÖZLEŞMESİ KUSURU | Şirket cezalandırılmadı | " +
                        $"Şirket: {sirket.SirketAdi} | Hizmet: {talep.HizmetKimligi} | {dogrulama.Aciklama}");
                    return IsAtamaSonucu.BasarisizSonuc(
                        talep.IsKimligi,
                        sirket.SirketKimligi,
                        "Motorun oluşturduğu hizmet isteği sözleşmeye uygun değildi.",
                        sirketSonucu.IslemSuresiMs);
                }

                CezaV8Karari karar = CezaV8Deposu.SonucHatasiniDegerlendir(
                    sirket,
                    talep,
                    sirketSonucu,
                    dogrulama,
                    islemTutari);
                if (karar.PuanCezasiUygulansin)
                    sirket.BasarisizIsKaydet(karar.HataKodu, talep.ZorlukSeviyesi);
                else
                    sirket.IsIptalEt();
                if (karar.UygulananCeza > 0) sirket.CezaUygula(karar.UygulananCeza);

                _musteriYoneticisi.IslemSonucunuKaydet(
                    talep,
                    false,
                    0,
                    sirketSonucu.IslemSuresiMs,
                    dogrulama.Aciklama);
                KonsolKayitcisi.Uyari(
                    $"Şirket sonucu doğrulanamadı | Şirket: {sirket.SirketAdi} | İş: {talep.IsKimligi} | " +
                    $"Kategori: {karar.Kategori} | Şirket kusuru: {karar.SirketKusuru} | " +
                    $"Ceza: {karar.UygulananCeza:N2} | Affedilen: {karar.AffedilenCeza:N2} | " +
                    $"Sebep: {dogrulama.Aciklama}");
                return IsAtamaSonucu.BasarisizSonuc(
                    talep.IsKimligi,
                    sirket.SirketKimligi,
                    dogrulama.Aciklama,
                    sirketSonucu.IslemSuresiMs);
            }

            if (!_musteriYoneticisi.MusteridenOdemeAl(musteri.MusteriKimligi, islemTutari))
            {
                sirket.IsIptalEt();
                _musteriYoneticisi.IslemSonucunuKaydet(
                    talep,
                    false,
                    0,
                    sirketSonucu.IslemSuresiMs,
                    "Sonuç doğrulandı ancak müşteri ödemesi alınamadı.");
                return IsAtamaSonucu.BasarisizSonuc(
                    talep.IsKimligi,
                    sirket.SirketKimligi,
                    "Müşteri ödemesi alınamadı.",
                    sirketSonucu.IslemSuresiMs);
            }

            double memnuniyet = MemnuniyetPuaniHesapla(
                sirketSonucu.IslemSuresiMs,
                zamanAsimiMs,
                talep.ZorlukSeviyesi);
            sirket.BasariliIsKaydet(
                islemTutari,
                sirketSonucu.IslemSuresiMs,
                memnuniyet,
                talep.ZorlukSeviyesi,
                zamanAsimiMs);
            _musteriYoneticisi.IslemSonucunuKaydet(
                talep,
                true,
                islemTutari,
                sirketSonucu.IslemSuresiMs,
                "İş motor tarafından doğrulandı ve ödeme tamamlandı.");

            KonsolKayitcisi.Basari(
                $"İş tamamlandı | İş: {talep.IsKimligi} | Şirket: {sirket.SirketAdi} | " +
                $"Hizmet: {talep.HizmetKimligi} | Zorluk: {talep.ZorlukSeviyesi} | " +
                $"Ödeme: {islemTutari:N2} | Süre: {sirketSonucu.IslemSuresiMs:N2} ms | " +
                $"Kalite: {sirket.KodKalitesiPuani:N1} | Performans: {sirket.PerformansPuani:N1}");
            return IsAtamaSonucu.BasariliSonuc(
                talep.IsKimligi,
                sirket.SirketKimligi,
                islemTutari,
                sirketSonucu.IslemSuresiMs,
                sirketSonucu.SonucVerisiJson);
        }
        catch (TimeoutException hata)
        {
            if (talep.KotuNiyetli)
            {
                decimal kayip = decimal.Round(talep.OlasiGuvenlikKaybi * 1.25m, 2);
                CezaV8Deposu.GuvenlikKaybiniKaydet(sirket, talep, kayip, "Kötü niyetli istek sunucuyu zaman aşımına uğrattı.");
                sirket.SaldiriBasariliKaydet(kayip, talep.ZorlukSeviyesi);
                _musteriYoneticisi.IslemSonucunuKaydet(talep, false, 0, zamanAsimiMs, "Kötü niyetli istek sunucuyu zaman aşımına uğrattı.");
                return IsAtamaSonucu.GuvenlikSonucu(talep.IsKimligi, sirket.SirketKimligi, false, kayip, zamanAsimiMs, "Kötü niyetli istek zaman aşımı oluşturdu.");
            }

            CezaV8Karari karar = CezaV8Deposu.OperasyonHatasiniDegerlendir(
                sirket, talep, "ZAMAN_ASIMI", hata.Message, islemTutari, true);
            if (karar.PuanCezasiUygulansin) sirket.ZamanAsimiKaydet(talep.ZorlukSeviyesi);
            else sirket.IsIptalEt();
            if (karar.UygulananCeza > 0) sirket.CezaUygula(karar.UygulananCeza);
            _musteriYoneticisi.IslemSonucunuKaydet(talep, false, 0, 0, "Şirket zaman aşımına uğradı.");
            return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, sirket.SirketKimligi, $"Zaman aşımı: {hata.Message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            sirket.IsIptalEt();
            throw;
        }
        catch (Exception hata)
        {
            if (talep.KotuNiyetli)
            {
                decimal kayip = talep.OlasiGuvenlikKaybi;
                CezaV8Deposu.GuvenlikKaybiniKaydet(sirket, talep, kayip, "Kötü niyetli istek sunucu hatasına yol açtı.");
                sirket.SaldiriBasariliKaydet(kayip, talep.ZorlukSeviyesi);
                _musteriYoneticisi.IslemSonucunuKaydet(talep, false, 0, 0, "Kötü niyetli istek sunucu hatasına yol açtı.");
                return IsAtamaSonucu.GuvenlikSonucu(talep.IsKimligi, sirket.SirketKimligi, false, kayip, 0, "Saldırı sunucu hatası oluşturdu.");
            }

            CezaV8Karari karar = CezaV8Deposu.OperasyonHatasiniDegerlendir(
                sirket, talep, "SUNUCU_HATASI", hata.Message, islemTutari, false);
            if (karar.PuanCezasiUygulansin) sirket.BasarisizIsKaydet("sunucu-hatasi", talep.ZorlukSeviyesi);
            else sirket.IsIptalEt();
            if (karar.UygulananCeza > 0) sirket.CezaUygula(karar.UygulananCeza);
            _musteriYoneticisi.IslemSonucunuKaydet(talep, false, 0, 0, hata.Message);
            return IsAtamaSonucu.BasarisizSonuc(talep.IsKimligi, sirket.SirketKimligi, hata.Message);
        }
    }

    private static bool MotorIstekSozlesmesiKusuruMu(
        HizmetTalebi talep,
        IsSonucuMesaji sonuc,
        SonucDogrulamaSonucu dogrulama)
    {
        if (sonuc.Basarili) return false;
        string kod = sonuc.HataKodu?.Trim() ?? string.Empty;
        if (kod is not ("ISTEK_VERISI_GECERSIZ" or "GECERSIZ_ISTEK" or "EKSIK_ALAN"))
            return false;

        Match eslesme = ZorunluAlanDeseni.Match(dogrulama.Aciklama ?? string.Empty);
        if (!eslesme.Success) return false;
        string alan = eslesme.Groups["alan"].Value;
        try
        {
            using JsonDocument belge = JsonDocument.Parse(talep.IstekVerisiJson);
            if (belge.RootElement.ValueKind != JsonValueKind.Object) return true;
            if (!belge.RootElement.TryGetProperty(alan, out JsonElement deger)) return true;
            return deger.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                   deger.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(deger.GetString());
        }
        catch
        {
            return true;
        }
    }

    private IsAtamaSonucu GuvenlikSinamasiniSonuclandir(
        HizmetTalebi talep,
        SirketKaydi sirket,
        IsSonucuMesaji sonuc,
        int zamanAsimiMs)
    {
        if (GuvenlikReddiMi(sonuc))
        {
            sirket.SaldiriEngellendiKaydet(talep.ZorlukSeviyesi, sonuc.IslemSuresiMs, zamanAsimiMs);
            _musteriYoneticisi.IslemSonucunuKaydet(talep, false, 0, sonuc.IslemSuresiMs, "Şirket kötü niyetli isteği güvenli biçimde reddetti.");
            return IsAtamaSonucu.GuvenlikSonucu(talep.IsKimligi, sirket.SirketKimligi, true, 0, sonuc.IslemSuresiMs, "Kötü niyetli iş güvenli biçimde engellendi.");
        }

        decimal kayip = talep.OlasiGuvenlikKaybi;
        CezaV8Deposu.GuvenlikKaybiniKaydet(sirket, talep, kayip, "Şirket saldırı isteğini normal iş gibi çalıştırdı.");
        sirket.SaldiriBasariliKaydet(kayip, talep.ZorlukSeviyesi);
        _musteriYoneticisi.IslemSonucunuKaydet(talep, false, 0, sonuc.IslemSuresiMs, "Şirket kötü niyetli isteği fark edemedi.");
        return IsAtamaSonucu.GuvenlikSonucu(talep.IsKimligi, sirket.SirketKimligi, false, kayip, sonuc.IslemSuresiMs, "Şirket saldırı isteğini normal iş gibi çalıştırdı.");
    }

    private IReadOnlyList<SirketAdayi> SirketAdaylariniOlustur(
        Musteri musteri,
        HizmetTalebi talep,
        decimal kullanilabilirButce)
    {
        List<(SirketBaglantisi Baglanti, SirketKaydi Sirket, SunulanHizmet Hizmet)> uygunlar = [];
        foreach (SirketBaglantisi baglanti in _sirketYoneticisi.BaglantilariGetir())
        {
            SirketKaydi sirket = baglanti.Kayit;
            if (!baglanti.Bagli || !sirket.YeniIsAlabilirMi) continue;
            SunulanHizmet? hizmet = sirket.HizmetiBul(talep.HizmetKimligi, talep.HizmetSurumu);
            if (hizmet is null || !hizmet.Aktif || hizmet.BirimFiyat <= 0 ||
                hizmet.BirimFiyat > kullanilabilirButce ||
                !sirket.HizmetIcinKapasiteVarMi(talep.HizmetKimligi, talep.HizmetSurumu))
                continue;
            uygunlar.Add((baglanti, sirket, hizmet));
        }
        if (uygunlar.Count == 0) return [];

        decimal enUcuz = uygunlar.Min(x => x.Hizmet.BirimFiyat);
        List<SirketAdayi> adaylar = [];
        foreach ((SirketBaglantisi baglanti, SirketKaydi sirket, SunulanHizmet hizmet) in uygunlar)
        {
            double fiyat = FiyatPuaniHesapla(enUcuz, hizmet.BirimFiyat);
            double itibar = Sinirla(sirket.ItibarPuani, 0, 100);
            double guvenilirlik = Sinirla(sirket.GuvenilirlikPuani, 0, 100);
            double kalite = Sinirla(sirket.KodKalitesiPuani, 0, 100);
            double performans = Sinirla(sirket.PerformansPuani, 0, 100);
            double guvenlik = Sinirla(sirket.GuvenlikPuani, 0, 100);
            double hiz = Sinirla(sirket.HizPuaniHesapla(), 0, 100);
            double kapasite = Sinirla(sirket.KapasitePuaniHesapla(hizmet), 0, 100);
            double sadakat = string.Equals(
                musteri.TercihEdilenSirketKimligi,
                sirket.SirketKimligi,
                StringComparison.OrdinalIgnoreCase) ? 100 : 50;
            double toplam = fiyat * 0.10 + itibar * 0.10 + guvenilirlik * 0.13 +
                            kalite * 0.25 + performans * 0.15 + hiz * 0.05 +
                            guvenlik * 0.12 + kapasite * 0.05 + sadakat * 0.05;
            adaylar.Add(new SirketAdayi
            {
                Baglanti = baglanti,
                Sirket = sirket,
                Hizmet = hizmet,
                FiyatPuani = fiyat,
                ItibarPuani = itibar,
                GuvenilirlikPuani = guvenilirlik,
                KodKalitesiPuani = kalite,
                PerformansPuani = performans,
                GuvenlikPuani = guvenlik,
                HizPuani = hiz,
                KapasitePuani = kapasite,
                SadakatPuani = sadakat,
                ToplamPuan = toplam
            });
        }
        return adaylar.OrderByDescending(x => x.ToplamPuan)
            .ThenBy(x => x.Hizmet.BirimFiyat)
            .Take(EnFazlaAdaySayisi)
            .ToList();
    }

    private SirketAdayi AgirlikliSirketSec(IReadOnlyList<SirketAdayi> adaylar, HizmetTalebi talep)
    {
        if (adaylar.Count == 0) throw new InvalidOperationException("Şirket seçimi için aday bulunmuyor.");
        if (adaylar.Count == 1) return adaylar[0];
        double Agirlik(SirketAdayi aday) => !talep.KotuNiyetli
            ? Math.Max(1, aday.ToplamPuan)
            : Math.Max(1, (101 - aday.GuvenlikPuani) * 0.75 +
                          (101 - aday.KodKalitesiPuani) * 0.20 +
                          aday.KapasitePuani * 0.05);
        double toplam = adaylar.Sum(Agirlik);
        double secim;
        lock (_rastgeleKilidi) secim = _rastgele.NextDouble() * toplam;
        foreach (SirketAdayi aday in adaylar)
        {
            secim -= Agirlik(aday);
            if (secim <= 0) return aday;
        }
        return adaylar[^1];
    }

    private static bool GuvenlikReddiMi(IsSonucuMesaji sonuc)
    {
        if (sonuc.Basarili) return false;
        string kod = sonuc.HataKodu?.Trim() ?? string.Empty;
        return kod.Equals("GUVENLIK_REDDI", StringComparison.OrdinalIgnoreCase) ||
               kod.Equals("ISTEK_GUVENLI_DEGIL", StringComparison.OrdinalIgnoreCase) ||
               kod.Equals("KOTU_NIYETLI_ISTEK", StringComparison.OrdinalIgnoreCase);
    }

    private static double FiyatPuaniHesapla(decimal enUcuz, decimal fiyat) =>
        enUcuz <= 0 || fiyat <= 0 ? 0 : Sinirla((double)(enUcuz / fiyat) * 100, 0, 100);

    private static int ZamanAsiminiHesapla(HizmetTalebi talep) =>
        talep.ZamanAsimiMs > 0
            ? Math.Clamp(talep.ZamanAsimiMs, 500, 30_000)
            : VarsayilanZamanAsimiMs;

    private static double MemnuniyetPuaniHesapla(
        double islemSuresiMs,
        int zamanAsimiMs,
        int zorlukSeviyesi)
    {
        if (zamanAsimiMs <= 0) return 40;
        double oran = Math.Clamp(islemSuresiMs / zamanAsimiMs, 0, 1);
        return Math.Clamp(
            100 - Math.Pow(oran, 0.60) * 100 - Math.Max(0, zorlukSeviyesi - 3) * 1.5,
            0,
            100);
    }

    private static double Sinirla(double deger, double alt, double ust) =>
        double.IsNaN(deger) || double.IsInfinity(deger) ? alt : Math.Clamp(deger, alt, ust);

    private static void OzetGuncelle(IsIslemeOzeti ozet, IsAtamaSonucu sonuc)
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
            ozet.ToplamIslemSuresiMs += Math.Max(0, sonuc.IslemSuresiMs);
            return;
        }
        if (sonuc.Basarili)
        {
            ozet.BasariliIsSayisi++;
            ozet.ToplamCiro += sonuc.IslemTutari;
            ozet.ToplamIslemSuresiMs += Math.Max(0, sonuc.IslemSuresiMs);
            return;
        }
        ozet.BasarisizIsSayisi++;
        if (sonuc.SonucAciklamasi.Contains("zaman aşımı", StringComparison.OrdinalIgnoreCase))
            ozet.ZamanAsimiSayisi++;
        if (sonuc.SonucAciklamasi.Contains("uygun şirket", StringComparison.OrdinalIgnoreCase))
            ozet.SirketBulunamayanIsSayisi++;
        if (sonuc.SonucAciklamasi.Contains("bakiye", StringComparison.OrdinalIgnoreCase) ||
            sonuc.SonucAciklamasi.Contains("bütçe", StringComparison.OrdinalIgnoreCase))
            ozet.ButceYetersizIsSayisi++;
    }

    private static void TickOzetiniYaz(IsIslemeOzeti ozet)
    {
        KonsolKayitcisi.Bilgi(
            $"İş tick özeti | Tick: {ozet.TickNumarasi} | Talep: {ozet.ToplamTalepSayisi} | " +
            $"Başarılı: {ozet.BasariliIsSayisi} | Başarısız: {ozet.BasarisizIsSayisi} | " +
            $"Şüpheli: {ozet.KotuNiyetliIsSayisi} | Engellenen saldırı: {ozet.EngellenenSaldiriSayisi} | " +
            $"Başarılı saldırı: {ozet.BasariliSaldiriSayisi} | Güvenlik kaybı: {ozet.ToplamGuvenlikKaybi:N2} | " +
            $"Zaman aşımı: {ozet.ZamanAsimiSayisi} | Şirket yok: {ozet.SirketBulunamayanIsSayisi} | " +
            $"Ciro: {ozet.ToplamCiro:N2} | Başarı oranı: {ozet.BasariOrani:N2}%");
    }
}
