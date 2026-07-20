using System.Text.Json;
using System.Text.Json.Nodes;
using SirketMotoru.Hizmetler;
using SirketMotoru.Isler;
using SirketMotoru.Isletim;
using SirketMotoru.Kayit;

namespace SirketMotoru.Musteriler;

public sealed class MusteriYoneticisi : IAsyncDisposable
{
    private const int VarsayilanMusteriSayisi = 20_000;
    private const int AzamiGercekSunucuTalebi = 1_000;

    private readonly MusteriVeritabani _veritabani;
    private readonly HizmetKatalogu _katalog;
    private readonly Random _rastgele;
    private readonly SemaphoreSlim _kayitKilidi = new(1, 1);
    private readonly Dictionary<string, Musteri> _indeks = new(StringComparer.OrdinalIgnoreCase);
    private long _sonKayitTicki;
    private bool _baslatildi;

    public IReadOnlyList<Musteri> Musteriler => _veritabani.Musteriler;
    public int AktifMusteriSayisi => Musteriler.Count(x => x.Aktif);
    public decimal ToplamMusteriBakiyesi => Musteriler.Sum(x => x.Bakiye);
    public decimal ToplamMusteriHarcamasi => Musteriler.Sum(x => x.ToplamHarcama);

    public MusteriYoneticisi(MusteriVeritabani musteriVeritabani, HizmetKatalogu hizmetKatalogu, int rastgeleTohum = 1881)
    {
        _veritabani = musteriVeritabani ?? throw new ArgumentNullException(nameof(musteriVeritabani));
        _katalog = hizmetKatalogu ?? throw new ArgumentNullException(nameof(hizmetKatalogu));
        _rastgele = new Random(rastgeleTohum);
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        if (_baslatildi) return;
        await _veritabani.YukleVeyaOlusturAsync(VarsayilanMusteriSayisi, cancellationToken);
        _indeks.Clear();
        foreach (Musteri musteri in Musteriler)
        {
            NormalizeEt(musteri);
            if (!_indeks.TryAdd(musteri.MusteriKimligi, musteri))
                throw new InvalidOperationException($"Tekrarlanan müşteri kimliği: {musteri.MusteriKimligi}");
        }
        _baslatildi = true;
        KonsolKayitcisi.Basari($"V9 müşteri pazarı hazır | Toplam: {Musteriler.Count:N0} | Aktif: {AktifMusteriSayisi:N0} | Pazar hizmet talebi hedefi: en az {AktifMusteriSayisi / 2:N0} | Gerçek sunucu örneği: {AzamiGercekSunucuTalebi:N0}");
    }

    public Musteri? MusteriyiBul(string musteriKimligi)
    {
        if (string.IsNullOrWhiteSpace(musteriKimligi)) return null;
        _indeks.TryGetValue(musteriKimligi.Trim(), out Musteri? musteri);
        return musteri;
    }

    public IReadOnlyList<HizmetTalebi> TickTalepleriniOlustur(long tickNumarasi)
    {
        BaslatilmisOlmasiniDogrula();
        V9PazarDosyasi pazar = V9PazarDeposu.Getir();
        List<V9TalepKaydi> talepler = pazar.HizmetTalepleri.Values.Where(x => x.BuTickTalep > 0).ToList();
        List<V9TalepKaydi> arzli = talepler.Where(x => x.ArzKapasitesi > 0).ToList();
        if (arzli.Count > 0) talepler = arzli;
        if (talepler.Count == 0)
        {
            talepler = _katalog.Hizmetler.Where(x => x.Aktif).Select(x => new V9TalepKaydi
            {
                Anahtar = $"{x.HizmetKimligi}@{x.HizmetSurumu}", Ad = x.HizmetKimligi, Tur = "hizmet", BuTickTalep = 1
            }).ToList();
        }
        if (talepler.Count == 0) return [];

        int pazarTalebi = Math.Max(AktifMusteriSayisi / 2, pazar.HizmetTalepleri.Values.Sum(x => x.BuTickTalep));
        int ornekSayisi = Math.Min(AzamiGercekSunucuTalebi, Math.Min(AktifMusteriSayisi, Math.Max(500, pazarTalebi / 10)));
        List<Musteri> aktif = Musteriler.Where(x => x.Aktif && x.Bakiye > 0 && x.TickBasinaHarcamaButcesi > 0).ToList();
        if (aktif.Count == 0) return [];
        ornekSayisi = Math.Min(ornekSayisi, aktif.Count);

        double toplamAgirlik = talepler.Sum(x => Math.Max(1, x.BuTickTalep));
        List<HizmetTalebi> sonuc = new(ornekSayisi);
        int baslangic = (int)(Math.Abs(tickNumarasi * 997L) % aktif.Count);
        const int adim = 19;
        for (int i = 0; i < ornekSayisi; i++)
        {
            Musteri musteri = aktif[(baslangic + i * adim) % aktif.Count];
            V9TalepKaydi secilen = AgirlikliTalepSec(talepler, toplamAgirlik);
            string[] parca = secilen.Anahtar.Split('@', 2);
            string hizmetKimligi = parca[0];
            string hizmetSurumu = parca.Length > 1 ? parca[1] : "1.0";
            HizmetTanimi? tanim = _katalog.Hizmetler.FirstOrDefault(x => x.HizmetKimligi.Equals(hizmetKimligi, StringComparison.OrdinalIgnoreCase) && x.HizmetSurumu.Equals(hizmetSurumu, StringComparison.OrdinalIgnoreCase));
            if (tanim is null) continue;

            int zorluk = ZorlukSeviyesiSec(hizmetKimligi);
            bool kotuNiyetli = KotuNiyetliIsMi(tickNumarasi, zorluk);
            string json = IstekVerisiOlustur(hizmetKimligi, zorluk, musteri.MusteriKimligi);
            string saldiriTuru = string.Empty;
            decimal guvenlikKaybi = 0;
            if (kotuNiyetli)
            {
                saldiriTuru = KotuNiyetTuruSec(tickNumarasi + i);
                json = GuvenlikSinamasiEkle(json, saldiriTuru, zorluk);
                bool nadirBuyuk = _rastgele.NextDouble() < 0.006;
                guvenlikKaybi = nadirBuyuk ? decimal.Round(1_500m + (decimal)_rastgele.NextDouble() * 3_500m, 2) : decimal.Round(20m + (decimal)_rastgele.NextDouble() * 330m, 2);
            }

            decimal butce = decimal.Round(Math.Min(musteri.Bakiye, Math.Max(25m, musteri.TickBasinaHarcamaButcesi)), 2);
            sonuc.Add(new HizmetTalebi
            {
                IsKimligi = $"is-{tickNumarasi:D8}-{Guid.NewGuid():N}", MusteriKimligi = musteri.MusteriKimligi,
                HizmetKimligi = hizmetKimligi, HizmetSurumu = hizmetSurumu, OlusturulmaTicki = tickNumarasi,
                AzamiButce = butce, IstekVerisiJson = json, ZamanAsimiMs = tanim.ZamanAsimiMs,
                ZorlukSeviyesi = zorluk, KotuNiyetli = kotuNiyetli, KotuNiyetTuru = saldiriTuru,
                OlasiGuvenlikKaybi = guvenlikKaybi, Durum = IsDurumu.Olusturuldu, OlusturulmaZamani = DateTimeOffset.UtcNow
            });
        }
        KonsolKayitcisi.Bilgi($"V9 HİZMET PAZARI | Pazar talebi: {pazarTalebi:N0} | Sunucuya gönderilen temsilî iş: {sonuc.Count:N0} | Şüpheli iş: {sonuc.Count(x => x.KotuNiyetli):N0} | Trendli hizmet: {talepler.Count:N0}");
        return sonuc;
    }

    public bool MusteridenOdemeAl(string musteriKimligi, decimal tutar)
    {
        Musteri? musteri = MusteriyiBul(musteriKimligi);
        if (musteri is null || !musteri.OdemeYapabilirMi(tutar)) return false;
        musteri.OdemeYap(tutar);
        return true;
    }

    public void MusteriyeIadeYap(string musteriKimligi, decimal tutar)
    {
        if (tutar < 0) throw new ArgumentOutOfRangeException(nameof(tutar));
        Musteri musteri = MusteriyiBul(musteriKimligi) ?? throw new InvalidOperationException($"Müşteri bulunamadı: {musteriKimligi}");
        musteri.Bakiye += tutar;
        musteri.ToplamHarcama = Math.Max(0, musteri.ToplamHarcama - tutar);
    }

    public void IslemSonucunuKaydet(HizmetTalebi talep, bool basarili, decimal odenenTutar, double tamamlanmaSuresiMs, string sonucAciklamasi)
    {
        ArgumentNullException.ThrowIfNull(talep);
        Musteri musteri = MusteriyiBul(talep.MusteriKimligi) ?? throw new InvalidOperationException($"İşlem müşterisi bulunamadı: {talep.MusteriKimligi}");
        musteri.IslemKaydet(new MusteriIslemKaydi
        {
            IsKimligi = talep.IsKimligi, TickNumarasi = talep.OlusturulmaTicki,
            HizmetKimligi = talep.HizmetKimligi, HizmetSurumu = talep.HizmetSurumu,
            SirketKimligi = talep.SecilenSirketKimligi ?? string.Empty,
            OdenenTutar = Math.Max(0, odenenTutar), Basarili = basarili,
            TamamlanmaSuresiMs = Math.Max(0, tamamlanmaSuresiMs), OlusturulmaZamani = DateTimeOffset.UtcNow,
            SonucAciklamasi = sonucAciklamasi?.Trim() ?? string.Empty
        });
        if (!talep.KotuNiyetli && !string.IsNullOrWhiteSpace(talep.SecilenSirketKimligi))
        {
            if (basarili) musteri.TercihEdilenSirketKimligi = talep.SecilenSirketKimligi;
            else if (musteri.TercihEdilenSirketKimligi?.Equals(talep.SecilenSirketKimligi, StringComparison.OrdinalIgnoreCase) == true && _rastgele.NextDouble() < 0.08)
                musteri.TercihEdilenSirketKimligi = null;
        }
    }

    public void TickBasindaMusterileriGuncelle(long tickNumarasi)
    {
        BaslatilmisOlmasiniDogrula();
        foreach (Musteri musteri in Musteriler.Where(x => x.Aktif))
        {
            int periyot = musteri.MusteriTuru switch
            {
                MusteriTuru.Bireysel => 15, MusteriTuru.KucukIsletme => 8,
                MusteriTuru.OrtaOlcekliIsletme => 6, MusteriTuru.Kurumsal => 5,
                MusteriTuru.KamuKurumu => 7, _ => 15
            };
            if (tickNumarasi <= 0 || tickNumarasi % periyot != 0) continue;
            decimal gelir = musteri.MusteriTuru switch
            {
                MusteriTuru.Bireysel => _rastgele.Next(50, 151), MusteriTuru.KucukIsletme => _rastgele.Next(250, 701),
                MusteriTuru.OrtaOlcekliIsletme => _rastgele.Next(900, 2_201), MusteriTuru.Kurumsal => _rastgele.Next(4_000, 9_001),
                MusteriTuru.KamuKurumu => _rastgele.Next(7_000, 16_001), _ => 50
            };
            musteri.Bakiye += gelir;
        }
    }

    public async Task GerekirseKaydetAsync(long tickNumarasi, CancellationToken cancellationToken)
    {
        if (tickNumarasi - _sonKayitTicki < 5) return;
        await KaydetAsync(cancellationToken);
        _sonKayitTicki = tickNumarasi;
    }

    public async Task KaydetAsync(CancellationToken cancellationToken)
    {
        BaslatilmisOlmasiniDogrula();
        await _kayitKilidi.WaitAsync(cancellationToken);
        try { await _veritabani.KaydetAsync(cancellationToken); }
        finally { _kayitKilidi.Release(); }
    }

    private V9TalepKaydi AgirlikliTalepSec(IReadOnlyList<V9TalepKaydi> talepler, double toplam)
    {
        double secim = _rastgele.NextDouble() * Math.Max(1, toplam);
        foreach (V9TalepKaydi talep in talepler)
        {
            secim -= Math.Max(1, talep.BuTickTalep);
            if (secim <= 0) return talep;
        }
        return talepler[^1];
    }

    private string IstekVerisiOlustur(string hizmetKimligi, int zorluk, string musteriKimligi)
    {
        int[] sayilar = Enumerable.Range(0, 4 + zorluk * 6).Select(_ => _rastgele.Next(-10_000, 10_001)).ToArray();
        string[] havuz = ["tunix", "motor", "veri", "uygulama", "guvenlik", "sistem", "musteri", "hizmet"];
        string metin = string.Join(' ', Enumerable.Range(0, 8 + zorluk * 8).Select(_ => havuz[_rastgele.Next(havuz.Length)]));
        JsonObject kok = new()
        {
            ["sayilar"] = JsonSerializer.SerializeToNode(sayilar), ["sayi"] = Math.Abs(sayilar.Sum()) + 2,
            ["metin"] = metin, ["yon"] = _rastgele.Next(2) == 0 ? "artan" : "azalan",
            ["kullaniciKimligi"] = musteriKimligi, ["musteriKimligi"] = musteriKimligi,
            ["surecKimligi"] = $"surec-{Guid.NewGuid():N}", ["islem"] = hizmetKimligi.Split('.').LastOrDefault() ?? hizmetKimligi,
            ["komut"] = hizmetKimligi, ["anahtar"] = $"anahtar-{zorluk}", ["deger"] = metin,
            ["veri"] = JsonSerializer.SerializeToNode(new { sayilar, metin }), ["mesaj"] = metin, ["hedef"] = musteriKimligi,
            ["alici"] = $"{musteriKimligi}@ornek.local", ["konu"] = "V9 hizmet isteği", ["icerik"] = metin,
            ["dosyaAdi"] = $"v9-{zorluk}.txt", ["sorgu"] = metin, ["limit"] = 10 + zorluk * 5
        };
        return kok.ToJsonString();
    }

    private string GuvenlikSinamasiEkle(string json, string tur, int zorluk)
    {
        JsonObject kok = JsonNode.Parse(json) as JsonObject ?? new();
        kok["_guvenlikSinamasi"] = new JsonObject { ["etiket"] = "motor-saldiri-v9", ["tur"] = tur, ["yogunluk"] = zorluk, ["komut"] = "kaynaklari-tuket" };
        kok["_saldiriDolgusu"] = new string('X', 150 + zorluk * 250);
        return kok.ToJsonString();
    }

    private bool KotuNiyetliIsMi(long tick, int zorluk)
    {
        int dongu = (int)(Math.Abs(tick) % 160);
        bool dalga = dongu is >= 78 and < 86 or >= 132 and < 138;
        double olasilik = dalga ? 0.018 + zorluk * 0.002 : 0.002 + zorluk * 0.0005;
        return _rastgele.NextDouble() < Math.Clamp(olasilik, 0, 0.035);
    }

    private static string KotuNiyetTuruSec(long deger)
    {
        string[] turler = ["kaynak-tuketimi", "buyuk-payload", "yetki-denemesi", "komut-enjeksiyonu", "tekrar-saldirisi"];
        return turler[(int)(Math.Abs(deger) % turler.Length)];
    }

    private int ZorlukSeviyesiSec(string hizmetKimligi)
    {
        bool ileri = hizmetKimligi.Contains("analitik", StringComparison.OrdinalIgnoreCase) || hizmetKimligi.Contains("yapay", StringComparison.OrdinalIgnoreCase) || hizmetKimligi.Contains("guvenlik", StringComparison.OrdinalIgnoreCase);
        int zorluk = ileri ? _rastgele.Next(2, 6) : _rastgele.Next(1, 5);
        if (_rastgele.NextDouble() < 0.07) zorluk = 5;
        return zorluk;
    }

    private static void NormalizeEt(Musteri musteri)
    {
        musteri.Bakiye = Math.Max(0, musteri.Bakiye);
        musteri.TickBasinaHarcamaButcesi = Math.Max(25, musteri.TickBasinaHarcamaButcesi);
        musteri.TalepOlusturmaOlasiligi = Math.Clamp(musteri.TalepOlusturmaOlasiligi, 0, 1);
        musteri.Tercihler ??= new MusteriTercihleri();
        musteri.Tercihler.NormalizeEt();
        musteri.HizmetKullanimSayilari ??= new(StringComparer.OrdinalIgnoreCase);
        musteri.IslemGecmisi ??= [];
        musteri.KullandigiUygulamalar ??= [];
        musteri.UygulamaKullanimSayilari ??= new(StringComparer.OrdinalIgnoreCase);
        musteri.UygulamaMemnuniyetleri ??= new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(musteri.MeslekProfili))
            musteri.MeslekProfili = musteri.MusteriTuru switch { MusteriTuru.KamuKurumu => "Kamu kurumu", MusteriTuru.Kurumsal => "Kurumsal yönetici", MusteriTuru.OrtaOlcekliIsletme => "Orta ölçekli işletme", MusteriTuru.KucukIsletme => "Küçük işletme sahibi", _ => "Bireysel kullanıcı" };
        if (string.IsNullOrWhiteSpace(musteri.GelirSegmenti))
            musteri.GelirSegmenti = musteri.TickBasinaHarcamaButcesi switch { < 150 => "düşük", < 750 => "orta", < 3_000 => "üst", _ => "kurumsal" };
    }

    private void BaslatilmisOlmasiniDogrula()
    {
        if (!_baslatildi) throw new InvalidOperationException("Müşteri yöneticisi henüz başlatılmadı.");
    }

    public async ValueTask DisposeAsync()
    {
        if (!_baslatildi) { _kayitKilidi.Dispose(); return; }
        try { await KaydetAsync(CancellationToken.None); }
        catch (Exception hata) { KonsolKayitcisi.Hata($"Müşteri verileri kaydedilemedi: {hata.Message}"); }
        finally { _kayitKilidi.Dispose(); }
    }
}
