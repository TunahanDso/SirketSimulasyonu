using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public static class BaslangicMigrasyonlari
{
    private const string IlosSirketKimligi = "ilayda-ilos-tech";
    private const string IlosKullaniciAdi = "ilos";
    private const string IlosGeciciParola = "ilos123";
    private const string UgaxSirketKimligi = "ugur-ugax";
    private const string UgaxDestekKimligi = "ugax-finansman-destegi-500000-v1";
    private const decimal UgaxDestekTutari = 500_000m;

    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task YonetimHesaplariniHazirlaAsync(
        string motorVerileriKlasoru,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);

        string dosyaYolu = Path.Combine(
            Path.GetFullPath(motorVerileriKlasoru),
            "sirket-isletim.json");

        SirketIsletimDosyasi veri = await IsletimDosyasiniOkuAsync(
            dosyaYolu,
            cancellationToken);

        veri.Hesaplar ??= [];
        veri.Sirketler ??= [];
        veri.Protokoller ??= [];
        veri.SozlesmeTeklifleri ??= [];
        veri.PiyasaOlaylari ??= [];

        SirketHesabi? hesap = veri.Hesaplar.FirstOrDefault(
            h => string.Equals(
                h.SirketKimligi,
                IlosSirketKimligi,
                StringComparison.OrdinalIgnoreCase));

        bool degisti = false;

        if (hesap is null)
        {
            hesap = YeniIlosHesabi();
            veri.Hesaplar.Add(hesap);
            degisti = true;
        }
        else if (hesap.ParolaDegistirilmeli)
        {
            ParolayiAyarla(hesap, IlosKullaniciAdi, IlosGeciciParola);
            degisti = true;
        }

        if (!degisti)
        {
            return;
        }

        await AtomikKaydetAsync(dosyaYolu, veri, cancellationToken);

        KonsolKayitcisi.Uyari(
            "İlos Tech yönetim hesabı hazır | " +
            $"Kullanıcı: {IlosKullaniciAdi} | " +
            $"Geçici parola: {IlosGeciciParola} | " +
            "İlk girişten sonra parola değiştirilmelidir.");
    }

    public static async Task TekSeferlikFinansmanDestekleriniUygulaAsync(
        SirketYoneticisi sirketYoneticisi,
        string motorVerileriKlasoru,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sirketYoneticisi);
        ArgumentException.ThrowIfNullOrWhiteSpace(motorVerileriKlasoru);

        string dosyaYolu = Path.Combine(
            Path.GetFullPath(motorVerileriKlasoru),
            "tek-seferlik-destekler.json");

        TekSeferlikDestekDosyasi destekler = await DestekDosyasiniOkuAsync(
            dosyaYolu,
            cancellationToken);

        destekler.UygulananDestekler ??= [];

        if (destekler.UygulananDestekler.Any(
                d => string.Equals(
                    d.DestekKimligi,
                    UgaxDestekKimligi,
                    StringComparison.OrdinalIgnoreCase)))
        {
            KonsolKayitcisi.Bilgi(
                "Ugax 500.000 TL finansman desteği daha önce uygulanmış; " +
                "yeniden eklenmedi.");
            return;
        }

        SirketKaydi? ugax = sirketYoneticisi.SirketKayitlari.FirstOrDefault(
            s => string.Equals(
                s.SirketKimligi,
                UgaxSirketKimligi,
                StringComparison.OrdinalIgnoreCase));

        if (ugax is null)
        {
            KonsolKayitcisi.Uyari(
                "Ugax finansman desteği uygulanamadı: şirket kaydı bulunamadı.");
            return;
        }

        ugax.Kasa += UgaxDestekTutari;

        // Önce gerçek şirket bilançosu diske yazılır. Bu işlem başarısız olursa
        // destek işareti oluşturulmaz ve sonraki açılışta güvenle yeniden denenir.
        await sirketYoneticisi.BilancolariKaydetAsync(cancellationToken);

        destekler.UygulananDestekler.Add(new TekSeferlikDestekKaydi
        {
            DestekKimligi = UgaxDestekKimligi,
            SirketKimligi = UgaxSirketKimligi,
            Tutar = UgaxDestekTutari,
            UygulanmaZamani = DateTimeOffset.UtcNow
        });

        await AtomikKaydetAsync(dosyaYolu, destekler, cancellationToken);

        KonsolKayitcisi.Basari(
            $"TEK SEFERLİK FİNANSMAN | Ugax kasasına " +
            $"{UgaxDestekTutari:N2} TL aktarıldı | " +
            $"Yeni kasa: {ugax.Kasa:N2} TL");
    }

    private static SirketHesabi YeniIlosHesabi()
    {
        SirketHesabi hesap = new()
        {
            SirketKimligi = IlosSirketKimligi,
            ParolaDegistirilmeli = true,
            OlusturulmaZamani = DateTimeOffset.UtcNow
        };

        ParolayiAyarla(hesap, IlosKullaniciAdi, IlosGeciciParola);
        return hesap;
    }

    private static void ParolayiAyarla(
        SirketHesabi hesap,
        string kullaniciAdi,
        string parola)
    {
        string tuz = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        hesap.KullaniciAdi = kullaniciAdi;
        hesap.ParolaTuzu = tuz;
        hesap.ParolaOzeti = Ozetle(tuz, parola);
        hesap.ParolaDegistirilmeli = true;
    }

    private static string Ozetle(string tuz, string parola) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{tuz}:{parola}")));

    private static async Task<SirketIsletimDosyasi> IsletimDosyasiniOkuAsync(
        string dosyaYolu,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(dosyaYolu))
        {
            return new SirketIsletimDosyasi();
        }

        string json = await File.ReadAllTextAsync(
            dosyaYolu,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new SirketIsletimDosyasi();
        }

        return JsonSerializer.Deserialize<SirketIsletimDosyasi>(
                   json,
                   JsonAyarlari)
               ?? new SirketIsletimDosyasi();
    }

    private static async Task<TekSeferlikDestekDosyasi> DestekDosyasiniOkuAsync(
        string dosyaYolu,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(dosyaYolu))
        {
            return new TekSeferlikDestekDosyasi();
        }

        string json = await File.ReadAllTextAsync(
            dosyaYolu,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new TekSeferlikDestekDosyasi();
        }

        return JsonSerializer.Deserialize<TekSeferlikDestekDosyasi>(
                   json,
                   JsonAyarlari)
               ?? new TekSeferlikDestekDosyasi();
    }

    private static async Task AtomikKaydetAsync<T>(
        string dosyaYolu,
        T veri,
        CancellationToken cancellationToken)
    {
        string? klasor = Path.GetDirectoryName(dosyaYolu);
        if (!string.IsNullOrWhiteSpace(klasor))
        {
            Directory.CreateDirectory(klasor);
        }

        string geciciDosya = dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(
            geciciDosya,
            JsonSerializer.Serialize(veri, JsonAyarlari),
            new UTF8Encoding(false),
            cancellationToken);

        File.Move(geciciDosya, dosyaYolu, overwrite: true);
    }

    private sealed class TekSeferlikDestekDosyasi
    {
        public int Surum { get; set; } = 1;
        public List<TekSeferlikDestekKaydi> UygulananDestekler { get; set; } = [];
    }

    private sealed class TekSeferlikDestekKaydi
    {
        public string DestekKimligi { get; set; } = string.Empty;
        public string SirketKimligi { get; set; } = string.Empty;
        public decimal Tutar { get; set; }
        public DateTimeOffset UygulanmaZamani { get; set; }
    }
}
