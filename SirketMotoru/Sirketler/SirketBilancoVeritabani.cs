using System.Text;
using System.Text.Json;

namespace SirketMotoru.Sirketler;

public sealed class SirketBilancoVeritabani
{
    private static readonly JsonSerializerOptions JsonAyarlari =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,

            PropertyNameCaseInsensitive =
                true,

            WriteIndented =
                true
        };

    private readonly string _dosyaYolu;

    private readonly SemaphoreSlim _dosyaKilidi =
        new(1, 1);

    public string DosyaYolu =>
        _dosyaYolu;

    public SirketBilancoVeritabani()
        : this(VarsayilanDosyaYolunuBul())
    {
    }

    public SirketBilancoVeritabani(
        string dosyaYolu)
    {
        if (string.IsNullOrWhiteSpace(
                dosyaYolu))
        {
            throw new ArgumentException(
                "Şirket bilanço dosya yolu boş olamaz.",
                nameof(dosyaYolu));
        }

        _dosyaYolu =
            Path.GetFullPath(
                dosyaYolu);
    }

    public IReadOnlyDictionary<string, SirketBilancoKaydi>
        Yukle()
    {
        if (!File.Exists(
                _dosyaYolu))
        {
            return new Dictionary<string, SirketBilancoKaydi>(
                StringComparer.OrdinalIgnoreCase);
        }

        string json =
            File.ReadAllText(
                _dosyaYolu,
                Encoding.UTF8);

        if (string.IsNullOrWhiteSpace(
                json))
        {
            return new Dictionary<string, SirketBilancoKaydi>(
                StringComparer.OrdinalIgnoreCase);
        }

        SirketBilancoDosyasi? dosya =
            JsonSerializer.Deserialize<SirketBilancoDosyasi>(
                json,
                JsonAyarlari);

        if (dosya is null)
        {
            throw new InvalidOperationException(
                "Şirket bilanço dosyası okunamadı.");
        }

        return dosya.Sirketler
            .Where(
                kayit =>
                    kayit is not null &&
                    !string.IsNullOrWhiteSpace(
                        kayit.SirketKimligi))
            .GroupBy(
                kayit => kayit.SirketKimligi,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grup => grup.Key,
                grup => grup.Last(),
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task KaydetAsync(
        IEnumerable<SirketKaydi> sirketler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            sirketler);

        await _dosyaKilidi.WaitAsync(
            cancellationToken);

        string geciciDosyaYolu =
            _dosyaYolu + ".tmp";

        try
        {
            string? klasor =
                Path.GetDirectoryName(
                    _dosyaYolu);

            if (!string.IsNullOrWhiteSpace(
                    klasor))
            {
                Directory.CreateDirectory(
                    klasor);
            }

            SirketBilancoDosyasi dosya =
                new()
                {
                    Surum = 1,
                    GuncellenmeZamani =
                        DateTimeOffset.UtcNow,
                    Sirketler =
                        sirketler
                            .OrderBy(
                                sirket =>
                                    sirket.SirketKimligi,
                                StringComparer.OrdinalIgnoreCase)
                            .Select(
                                SirketBilancoKaydi.Olustur)
                            .ToList()
                };

            string json =
                JsonSerializer.Serialize(
                    dosya,
                    JsonAyarlari);

            await File.WriteAllTextAsync(
                geciciDosyaYolu,
                json,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                        false),
                cancellationToken);

            File.Move(
                geciciDosyaYolu,
                _dosyaYolu,
                overwrite:
                    true);
        }
        finally
        {
            try
            {
                if (File.Exists(
                        geciciDosyaYolu))
                {
                    File.Delete(
                        geciciDosyaYolu);
                }
            }
            catch
            {
                // Geçici dosya temizleme hatası ana kaydı bozmaz.
            }

            _dosyaKilidi.Release();
        }
    }

    private static string VarsayilanDosyaYolunuBul()
    {
        string[] olasiKlasorler =
        [
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "MotorVerileri"),

            Path.Combine(
                Directory.GetCurrentDirectory(),
                "..",
                "MotorVerileri"),

            Path.Combine(
                AppContext.BaseDirectory,
                "MotorVerileri"),

            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "MotorVerileri"),

            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "MotorVerileri"),

            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "MotorVerileri")
        ];

        foreach (string klasor in
                 olasiKlasorler)
        {
            string tamKlasor =
                Path.GetFullPath(
                    klasor);

            if (Directory.Exists(
                    tamKlasor) &&
                File.Exists(
                    Path.Combine(
                        tamKlasor,
                        "motor-ayarlari.json")))
            {
                return Path.Combine(
                    tamKlasor,
                    "sirket-bilancolari.json");
            }
        }

        string varsayilanKlasor =
            Path.GetFullPath(
                olasiKlasorler[0]);

        return Path.Combine(
            varsayilanKlasor,
            "sirket-bilancolari.json");
    }
}

public sealed class SirketBilancoDosyasi
{
    public int Surum { get; set; } = 1;

    public DateTimeOffset GuncellenmeZamani { get; set; } =
        DateTimeOffset.UtcNow;

    public List<SirketBilancoKaydi> Sirketler { get; set; } =
        [];
}

public sealed class SirketBilancoKaydi
{
    public string SirketKimligi { get; set; } =
        string.Empty;

    public string SirketAdi { get; set; } =
        string.Empty;

    public decimal Kasa { get; set; }

    public decimal ToplamGelir { get; set; }

    public decimal ToplamIade { get; set; }

    public decimal ToplamCeza { get; set; }

    public decimal BekleyenOdeme { get; set; }

    public double ItibarPuani { get; set; } = 50;

    public double GuvenilirlikPuani { get; set; } = 50;

    public double OrtalamaMusteriMemnuniyeti { get; set; } = 50;

    public int TamamlananIsSayisi { get; set; }

    public int BasarisizIsSayisi { get; set; }

    public int ZamanAsiminaUgrayanIsSayisi { get; set; }

    public int IptalEdilenIsSayisi { get; set; }

    public int ReddedilenIsSayisi { get; set; }

    public long ToplamIslemSuresiMs { get; set; }

    public DateTimeOffset? SonBasariliIsZamani { get; set; }

    public DateTimeOffset? SonBasarisizIsZamani { get; set; }

    public static SirketBilancoKaydi Olustur(
        SirketKaydi sirket)
    {
        ArgumentNullException.ThrowIfNull(
            sirket);

        return new SirketBilancoKaydi
        {
            SirketKimligi =
                sirket.SirketKimligi,
            SirketAdi =
                sirket.SirketAdi,
            Kasa =
                sirket.Kasa,
            ToplamGelir =
                sirket.ToplamGelir,
            ToplamIade =
                sirket.ToplamIade,
            ToplamCeza =
                sirket.ToplamCeza,
            BekleyenOdeme =
                sirket.BekleyenOdeme,
            ItibarPuani =
                sirket.ItibarPuani,
            GuvenilirlikPuani =
                sirket.GuvenilirlikPuani,
            OrtalamaMusteriMemnuniyeti =
                sirket.OrtalamaMusteriMemnuniyeti,
            TamamlananIsSayisi =
                sirket.TamamlananIsSayisi,
            BasarisizIsSayisi =
                sirket.BasarisizIsSayisi,
            ZamanAsiminaUgrayanIsSayisi =
                sirket.ZamanAsiminaUgrayanIsSayisi,
            IptalEdilenIsSayisi =
                sirket.IptalEdilenIsSayisi,
            ReddedilenIsSayisi =
                sirket.ReddedilenIsSayisi,
            ToplamIslemSuresiMs =
                sirket.ToplamIslemSuresiMs,
            SonBasariliIsZamani =
                sirket.SonBasariliIsZamani,
            SonBasarisizIsZamani =
                sirket.SonBasarisizIsZamani
        };
    }

    public void Uygula(
        SirketKaydi sirket)
    {
        ArgumentNullException.ThrowIfNull(
            sirket);

        sirket.Kasa =
            Math.Max(0, Kasa);
        sirket.ToplamGelir =
            Math.Max(0, ToplamGelir);
        sirket.ToplamIade =
            Math.Max(0, ToplamIade);
        sirket.ToplamCeza =
            Math.Max(0, ToplamCeza);
        sirket.BekleyenOdeme =
            Math.Max(0, BekleyenOdeme);
        sirket.ItibarPuani =
            SinirlaPuani(ItibarPuani);
        sirket.GuvenilirlikPuani =
            SinirlaPuani(GuvenilirlikPuani);
        sirket.OrtalamaMusteriMemnuniyeti =
            SinirlaPuani(
                OrtalamaMusteriMemnuniyeti);
        sirket.TamamlananIsSayisi =
            Math.Max(0, TamamlananIsSayisi);
        sirket.BasarisizIsSayisi =
            Math.Max(0, BasarisizIsSayisi);
        sirket.ZamanAsiminaUgrayanIsSayisi =
            Math.Max(
                0,
                ZamanAsiminaUgrayanIsSayisi);
        sirket.IptalEdilenIsSayisi =
            Math.Max(0, IptalEdilenIsSayisi);
        sirket.ReddedilenIsSayisi =
            Math.Max(0, ReddedilenIsSayisi);
        sirket.ToplamIslemSuresiMs =
            Math.Max(0, ToplamIslemSuresiMs);
        sirket.SonBasariliIsZamani =
            SonBasariliIsZamani;
        sirket.SonBasarisizIsZamani =
            SonBasarisizIsZamani;
        sirket.AktifIsSayisi =
            0;
    }

    private static double SinirlaPuani(
        double puan)
    {
        if (double.IsNaN(puan) ||
            double.IsInfinity(puan))
        {
            return 50;
        }

        return Math.Clamp(
            puan,
            0,
            100);
    }
}
