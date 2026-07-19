using System.Text.Json;
using SirketMotoru.Protokol.Mesajlar;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isler;

public sealed class CezaV8Kaydi
{
    public string KayitKimligi { get; set; } = Guid.NewGuid().ToString("N");
    public long TickNumarasi { get; set; }
    public string SirketKimligi { get; set; } = string.Empty;
    public string SirketAdi { get; set; } = string.Empty;
    public string IsKimligi { get; set; } = string.Empty;
    public string HizmetKimligi { get; set; } = string.Empty;
    public string HataKodu { get; set; } = string.Empty;
    public string Kategori { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public bool SirketKusuru { get; set; }
    public bool PuanCezasiUygulandi { get; set; }
    public decimal TalepEdilenCeza { get; set; }
    public decimal UygulananCeza { get; set; }
    public decimal AffedilenCeza { get; set; }
    public DateTimeOffset Zaman { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CezaV8Dosyasi
{
    public int Surum { get; set; } = 8;
    public bool LegacyUzlasmaTamamlandi { get; set; }
    public long SonTick { get; set; }
    public DateTimeOffset GuncellenmeZamani { get; set; } = DateTimeOffset.UtcNow;
    public List<CezaV8Kaydi> Kayitlar { get; set; } = [];
}

public sealed class CezaV8Karari
{
    public bool SirketKusuru { get; init; }
    public bool PuanCezasiUygulansin { get; init; }
    public string HataKodu { get; init; } = "uyari";
    public string Kategori { get; init; } = "uyari";
    public string Aciklama { get; init; } = string.Empty;
    public decimal TalepEdilenCeza { get; init; }
    public decimal UygulananCeza { get; init; }
    public decimal AffedilenCeza => Math.Max(0, TalepEdilenCeza - UygulananCeza);
}

public sealed class CezaV8NedenOzeti
{
    public string Kategori { get; init; } = string.Empty;
    public int OlaySayisi { get; init; }
    public decimal UygulananCeza { get; init; }
    public decimal AffedilenCeza { get; init; }
}

public sealed class CezaV8SirketOzeti
{
    public string SirketKimligi { get; init; } = string.Empty;
    public string SirketAdi { get; init; } = string.Empty;
    public long SonTick { get; init; }
    public decimal TarihselToplamCeza { get; init; }
    public decimal BuTickUygulananCeza { get; init; }
    public decimal BuTickAffedilenCeza { get; init; }
    public decimal V8ToplamUygulananCeza { get; init; }
    public decimal V8ToplamAffedilenCeza { get; init; }
    public int BuTickOlaySayisi { get; init; }
    public int SirketKusuruSayisi { get; init; }
    public int MotorVeyaSozlesmeKusuruSayisi { get; init; }
    public decimal TickCezaTavani { get; init; }
    public IReadOnlyList<CezaV8NedenOzeti> Nedenler { get; init; } = [];
    public IReadOnlyList<CezaV8Kaydi> SonKayitlar { get; init; } = [];
}

public static class CezaV8Deposu
{
    private const decimal SirketTickCezaTavani = 75m;
    private const int AzamiKayit = 4_000;
    private static readonly object Kilit = new();
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly Dictionary<string, decimal> TickUygulanan = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> TickTekrarSayaci = new(StringComparer.OrdinalIgnoreCase);
    private static CezaV8Dosyasi _dosya = new();
    private static string _dosyaYolu = string.Empty;
    private static long _tick;

    public static void Baslat(string motorVerileriKlasoru)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);
        lock (Kilit)
        {
            _dosyaYolu = Path.Combine(Path.GetFullPath(motorVerileriKlasoru), "ceza-v8.json");
            try
            {
                if (File.Exists(_dosyaYolu))
                {
                    string json = File.ReadAllText(_dosyaYolu);
                    _dosya = JsonSerializer.Deserialize<CezaV8Dosyasi>(json, JsonAyarlari) ?? new();
                }
            }
            catch
            {
                _dosya = new CezaV8Dosyasi();
            }

            _dosya.Kayitlar ??= [];
            _tick = _dosya.SonTick;
            KaydetKilitsiz();
        }
    }

    public static void LegacyCezalariUzlastir(IEnumerable<SirketKaydi> sirketler)
    {
        ArgumentNullException.ThrowIfNull(sirketler);
        lock (Kilit)
        {
            if (_dosya.LegacyUzlasmaTamamlandi) return;

            foreach (SirketKaydi sirket in sirketler)
            {
                decimal onceki = Math.Max(0, sirket.ToplamCeza);
                decimal adilTavan = decimal.Round(Math.Max(250m, Math.Max(0, sirket.ToplamGelir) * 0.001m), 2);
                decimal yeni = Math.Min(onceki, adilTavan);
                decimal uzlasilan = Math.Max(0, onceki - yeni);
                if (uzlasilan <= 0) continue;

                sirket.ToplamCeza = yeni;
                _dosya.Kayitlar.Add(new CezaV8Kaydi
                {
                    TickNumarasi = 0,
                    SirketKimligi = sirket.SirketKimligi,
                    SirketAdi = sirket.SirketAdi,
                    Kategori = "legacy-ceza-uzlasmasi",
                    HataKodu = "V8_GECIS_UZLASMASI",
                    Aciklama = "V8 öncesinde motor sözleşme eksikleri ve tekrarlı işler nedeniyle birikmiş aşırı tarihsel ceza yeniden değerlendirildi. Nakit iadesi yapılmadı; yalnız tarihsel ceza bakiyesi düzeltildi.",
                    SirketKusuru = false,
                    TalepEdilenCeza = onceki,
                    UygulananCeza = yeni,
                    AffedilenCeza = uzlasilan
                });
            }

            _dosya.LegacyUzlasmaTamamlandi = true;
            Kirp();
            KaydetKilitsiz();
        }
    }

    public static void TickBaslat(long tickNumarasi)
    {
        lock (Kilit)
        {
            _tick = tickNumarasi;
            TickUygulanan.Clear();
            TickTekrarSayaci.Clear();
        }
    }

    public static CezaV8Karari SonucHatasiniDegerlendir(
        SirketKaydi sirket,
        HizmetTalebi talep,
        IsSonucuMesaji sirketSonucu,
        SonucDogrulamaSonucu dogrulama,
        decimal islemTutari)
    {
        ArgumentNullException.ThrowIfNull(sirket);
        ArgumentNullException.ThrowIfNull(talep);
        ArgumentNullException.ThrowIfNull(sirketSonucu);
        ArgumentNullException.ThrowIfNull(dogrulama);

        string kod = (sirketSonucu.HataKodu ?? string.Empty).Trim().ToUpperInvariant();
        string metin = $"{dogrulama.Aciklama} {sirketSonucu.HataMesaji}".Trim();
        bool kimlikVeyaSahtecilik = Icerir(metin, "kimliği uyuşmuyor", "şirket kimliği", "sahte", "negatif işlem süresi");
        bool motorSozlesmeKusuru =
            Icerir(metin, "doğrulayıcı bulunamadı", "alanı zorunludur", "zorunlu alan", "parametre eksik", "istek verisi") ||
            kod is "GECERSIZ_ISTEK" or "EKSIK_ALAN" or "ZORUNLU_ALAN_EKSIK" or "PARAMETRE_EKSIK" or "MOTOR_SOZLESME_HATASI";
        bool ilanUyumsuzlugu = kod is "BILINMEYEN_HIZMET" or "HIZMET_BULUNAMADI";

        string kategori;
        bool sirketKusuru;
        bool agir;
        decimal istenen;

        if (motorSozlesmeKusuru)
        {
            kategori = "motor-sozlesme-uyumsuzlugu";
            sirketKusuru = false;
            agir = false;
            istenen = 0;
        }
        else if (kimlikVeyaSahtecilik)
        {
            kategori = "kimlik-ve-sonuc-butunlugu";
            sirketKusuru = true;
            agir = true;
            istenen = decimal.Round(Math.Clamp(Math.Max(10m, islemTutari * 0.25m), 10m, 50m), 2);
        }
        else if (ilanUyumsuzlugu)
        {
            kategori = "ilan-kod-uyumsuzlugu";
            sirketKusuru = true;
            agir = false;
            istenen = decimal.Round(Math.Clamp(Math.Max(1m, islemTutari * 0.03m), 1m, 12m), 2);
        }
        else
        {
            kategori = "gecersiz-veya-basarisiz-sonuc";
            sirketKusuru = true;
            agir = false;
            istenen = decimal.Round(Math.Clamp(Math.Max(1m, islemTutari * 0.05m), 1m, 20m), 2);
        }

        return KararOlusturVeKaydet(sirket, talep, kod, kategori, metin, sirketKusuru, agir, istenen);
    }

    public static CezaV8Karari OperasyonHatasiniDegerlendir(
        SirketKaydi sirket,
        HizmetTalebi talep,
        string hataKodu,
        string aciklama,
        decimal islemTutari,
        bool zamanAsimi)
    {
        string kategori = zamanAsimi ? "zaman-asimi" : "sunucu-hatasi";
        decimal istenen = decimal.Round(Math.Clamp(Math.Max(1m, islemTutari * (zamanAsimi ? 0.02m : 0.03m)), 1m, 10m), 2);
        return KararOlusturVeKaydet(sirket, talep, hataKodu, kategori, aciklama, true, false, istenen);
    }

    public static void GuvenlikKaybiniKaydet(SirketKaydi sirket, HizmetTalebi talep, decimal kayip, string aciklama)
    {
        lock (Kilit)
        {
            Ekle(new CezaV8Kaydi
            {
                TickNumarasi = _tick,
                SirketKimligi = sirket.SirketKimligi,
                SirketAdi = sirket.SirketAdi,
                IsKimligi = talep.IsKimligi,
                HizmetKimligi = talep.HizmetKimligi,
                HataKodu = "GUVENLIK_KAYBI",
                Kategori = "basarili-saldiri",
                Aciklama = aciklama,
                SirketKusuru = true,
                PuanCezasiUygulandi = true,
                TalepEdilenCeza = kayip,
                UygulananCeza = kayip,
                AffedilenCeza = 0
            });
        }
    }

    public static void TickBitir(long tickNumarasi)
    {
        lock (Kilit)
        {
            _dosya.SonTick = Math.Max(_dosya.SonTick, tickNumarasi);
            _dosya.GuncellenmeZamani = DateTimeOffset.UtcNow;
            Kirp();
            KaydetKilitsiz();
        }
    }

    public static CezaV8SirketOzeti SirketOzeti(SirketKaydi sirket)
    {
        ArgumentNullException.ThrowIfNull(sirket);
        lock (Kilit)
        {
            List<CezaV8Kaydi> tum = _dosya.Kayitlar
                .Where(x => x.SirketKimligi.Equals(sirket.SirketKimligi, StringComparison.OrdinalIgnoreCase))
                .ToList();
            List<CezaV8Kaydi> buTick = tum.Where(x => x.TickNumarasi == _tick).ToList();
            return new CezaV8SirketOzeti
            {
                SirketKimligi = sirket.SirketKimligi,
                SirketAdi = sirket.SirketAdi,
                SonTick = _tick,
                TarihselToplamCeza = sirket.ToplamCeza,
                BuTickUygulananCeza = buTick.Sum(x => x.UygulananCeza),
                BuTickAffedilenCeza = buTick.Sum(x => x.AffedilenCeza),
                V8ToplamUygulananCeza = tum.Sum(x => x.UygulananCeza),
                V8ToplamAffedilenCeza = tum.Sum(x => x.AffedilenCeza),
                BuTickOlaySayisi = buTick.Count,
                SirketKusuruSayisi = tum.Count(x => x.SirketKusuru),
                MotorVeyaSozlesmeKusuruSayisi = tum.Count(x => !x.SirketKusuru),
                TickCezaTavani = SirketTickCezaTavani,
                Nedenler = tum.GroupBy(x => x.Kategori, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new CezaV8NedenOzeti
                    {
                        Kategori = g.Key,
                        OlaySayisi = g.Count(),
                        UygulananCeza = g.Sum(x => x.UygulananCeza),
                        AffedilenCeza = g.Sum(x => x.AffedilenCeza)
                    })
                    .OrderByDescending(x => x.OlaySayisi)
                    .ToList(),
                SonKayitlar = tum.OrderByDescending(x => x.TickNumarasi).ThenByDescending(x => x.Zaman).Take(100).ToList()
            };
        }
    }

    public static object GenelDurum(IEnumerable<SirketKaydi> sirketler)
    {
        List<CezaV8SirketOzeti> ozetler = sirketler.Select(SirketOzeti).ToList();
        return new
        {
            surum = 8,
            sonTick = _tick,
            sirketTickCezaTavani = SirketTickCezaTavani,
            buTickUygulanan = ozetler.Sum(x => x.BuTickUygulananCeza),
            buTickAffedilen = ozetler.Sum(x => x.BuTickAffedilenCeza),
            toplamUygulanan = ozetler.Sum(x => x.V8ToplamUygulananCeza),
            toplamAffedilen = ozetler.Sum(x => x.V8ToplamAffedilenCeza),
            sirketler = ozetler,
            kural = "Motor veya istek sözleşmesi kusuru şirkete yazılmaz. Gerçek şirket kusurunda ilk tekrarlar uyarıdır ve şirket başına tick cezası 75 TL ile sınırlıdır."
        };
    }

    private static CezaV8Karari KararOlusturVeKaydet(
        SirketKaydi sirket,
        HizmetTalebi talep,
        string hataKodu,
        string kategori,
        string aciklama,
        bool sirketKusuru,
        bool agir,
        decimal istenen)
    {
        lock (Kilit)
        {
            string tekrarAnahtari = $"{sirket.SirketKimligi}|{talep.HizmetKimligi}|{kategori}";
            int tekrar = TickTekrarSayaci.TryGetValue(tekrarAnahtari, out int mevcutTekrar) ? mevcutTekrar + 1 : 1;
            TickTekrarSayaci[tekrarAnahtari] = tekrar;

            decimal uygulanan = 0;
            if (sirketKusuru && istenen > 0 && (agir || tekrar > 2))
            {
                decimal kullanilan = TickUygulanan.TryGetValue(sirket.SirketKimligi, out decimal mevcut) ? mevcut : 0;
                decimal kalan = Math.Max(0, SirketTickCezaTavani - kullanilan);
                uygulanan = Math.Min(istenen, kalan);
                TickUygulanan[sirket.SirketKimligi] = kullanilan + uygulanan;
            }

            CezaV8Karari karar = new()
            {
                SirketKusuru = sirketKusuru,
                PuanCezasiUygulansin = sirketKusuru && (agir || tekrar > 2),
                HataKodu = string.IsNullOrWhiteSpace(hataKodu) ? kategori : hataKodu,
                Kategori = kategori,
                Aciklama = aciklama,
                TalepEdilenCeza = istenen,
                UygulananCeza = uygulanan
            };

            Ekle(new CezaV8Kaydi
            {
                TickNumarasi = _tick,
                SirketKimligi = sirket.SirketKimligi,
                SirketAdi = sirket.SirketAdi,
                IsKimligi = talep.IsKimligi,
                HizmetKimligi = talep.HizmetKimligi,
                HataKodu = karar.HataKodu,
                Kategori = karar.Kategori,
                Aciklama = karar.Aciklama,
                SirketKusuru = karar.SirketKusuru,
                PuanCezasiUygulandi = karar.PuanCezasiUygulansin,
                TalepEdilenCeza = karar.TalepEdilenCeza,
                UygulananCeza = karar.UygulananCeza,
                AffedilenCeza = karar.AffedilenCeza
            });
            return karar;
        }
    }

    private static void Ekle(CezaV8Kaydi kayit)
    {
        _dosya.Kayitlar.Add(kayit);
        if (_dosya.Kayitlar.Count > AzamiKayit) _dosya.Kayitlar.RemoveRange(0, _dosya.Kayitlar.Count - AzamiKayit);
    }

    private static bool Icerir(string metin, params string[] desenler) =>
        desenler.Any(x => metin.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static void Kirp()
    {
        if (_dosya.Kayitlar.Count > AzamiKayit) _dosya.Kayitlar.RemoveRange(0, _dosya.Kayitlar.Count - AzamiKayit);
    }

    private static void KaydetKilitsiz()
    {
        if (string.IsNullOrWhiteSpace(_dosyaYolu)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
            string gecici = _dosyaYolu + ".tmp";
            File.WriteAllText(gecici, JsonSerializer.Serialize(_dosya, JsonAyarlari));
            File.Move(gecici, _dosyaYolu, true);
        }
        catch
        {
            // Ceza defteri oyunu durdurmamalıdır; sonraki tickte yeniden denenir.
        }
    }
}