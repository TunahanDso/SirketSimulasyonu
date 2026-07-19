using System.Globalization;
using System.Text.Json;
using SirketMotoru.Protokol.Mesajlar;

namespace SirketMotoru.Isler;

public sealed class SonucDogrulayicisi
{
    private const double OndalikToleransi = 0.000001;

    private readonly JsonSerializerOptions _jsonAyarlari =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public SonucDogrulamaSonucu Dogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sirketSonucu)
    {
        ArgumentNullException.ThrowIfNull(talep);
        ArgumentNullException.ThrowIfNull(sirketSonucu);

        SonucDogrulamaSonucu temelDogrulama =
            TemelAlanlariDogrula(talep, sirketSonucu);

        if (!temelDogrulama.Gecerli)
        {
            return temelDogrulama;
        }

        if (!sirketSonucu.Basarili)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Şirket işi başarısız bildirdi: " +
                $"{sirketSonucu.HataKodu ?? "hata-kodu-yok"} | " +
                $"{sirketSonucu.HataMesaji ?? "Açıklama yok."}",
                gelenSonucJson: sirketSonucu.SonucVerisiJson);
        }

        try
        {
            return talep.HizmetKimligi
                .Trim()
                .ToLowerInvariant() switch
                {
                    "matematik.topla" =>
                        MatematikToplamaSonucunuDogrula(talep, sirketSonucu),
                    "matematik.carp" =>
                        MatematikCarpmaSonucunuDogrula(talep, sirketSonucu),
                    "veri.ortalama-hesapla" =>
                        OrtalamaSonucunuDogrula(talep, sirketSonucu),
                    "metin.kelime-say" =>
                        KelimeSayisiSonucunuDogrula(talep, sirketSonucu),
                    "metin.karakter-say" =>
                        KarakterSayisiSonucunuDogrula(talep, sirketSonucu),
                    "veri.medyan-hesapla" =>
                        MedyanSonucunuDogrula(talep, sirketSonucu),
                    "veri.standart-sapma" =>
                        StandartSapmaSonucunuDogrula(talep, sirketSonucu),
                    "dizi.sirala" =>
                        SiralamaSonucunuDogrula(talep, sirketSonucu),
                    "matematik.asal-carpanlar" =>
                        AsalCarpanSonucunuDogrula(talep, sirketSonucu),
                    "metin.frekans-analizi" =>
                        FrekansAnaliziSonucunuDogrula(talep, sirketSonucu),
                    _ => SonucDogrulamaSonucu.Basarisiz(
                        $"Hizmet için sonuç doğrulayıcı bulunamadı: " +
                        $"{talep.HizmetKimligi}@{talep.HizmetSurumu}",
                        gelenSonucJson: sirketSonucu.SonucVerisiJson)
                };
        }
        catch (JsonException exception)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Şirket sonucu veya istek verisi geçerli JSON değil: " +
                exception.Message,
                gelenSonucJson: sirketSonucu.SonucVerisiJson);
        }
        catch (FormatException exception)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Sonuç veri biçimi geçersiz: {exception.Message}",
                gelenSonucJson: sirketSonucu.SonucVerisiJson);
        }
        catch (OverflowException exception)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Sonuç hesaplanırken sayısal taşma oluştu: " +
                exception.Message,
                gelenSonucJson: sirketSonucu.SonucVerisiJson);
        }
        catch (Exception exception)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Sonuç doğrulama sırasında beklenmeyen hata oluştu: " +
                exception.Message,
                gelenSonucJson: sirketSonucu.SonucVerisiJson);
        }
    }

    private static SonucDogrulamaSonucu TemelAlanlariDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sirketSonucu)
    {
        if (string.IsNullOrWhiteSpace(sirketSonucu.IstekKimligi))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Şirket sonucundaki istek kimliği boş.");
        }

        if (string.IsNullOrWhiteSpace(sirketSonucu.IsKimligi))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Şirket sonucundaki iş kimliği boş.");
        }

        if (!string.Equals(
                talep.IsKimligi,
                sirketSonucu.IsKimligi,
                StringComparison.Ordinal))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"İş kimliği uyuşmuyor. Beklenen: {talep.IsKimligi} | " +
                $"Gelen: {sirketSonucu.IsKimligi}");
        }

        if (string.IsNullOrWhiteSpace(sirketSonucu.SirketKimligi))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Şirket sonucu, şirket kimliği içermiyor.");
        }

        if (string.IsNullOrWhiteSpace(talep.SecilenSirketKimligi))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Talebe henüz bir şirket atanmamış.");
        }

        if (!string.Equals(
                talep.SecilenSirketKimligi,
                sirketSonucu.SirketKimligi,
                StringComparison.OrdinalIgnoreCase))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Sonucu gönderen şirket uyuşmuyor. " +
                $"Beklenen: {talep.SecilenSirketKimligi} | " +
                $"Gelen: {sirketSonucu.SirketKimligi}");
        }

        if (sirketSonucu.IslemSuresiMs < 0)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Şirket negatif işlem süresi bildirdi.");
        }

        if (sirketSonucu.Basarili &&
            string.IsNullOrWhiteSpace(sirketSonucu.SonucVerisiJson))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Başarılı olarak bildirilen sonuçta veri bulunmuyor.");
        }

        return SonucDogrulamaSonucu.Basarili(
            "Temel mesaj alanları geçerli.");
    }

    private SonucDogrulamaSonucu MatematikToplamaSonucunuDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sonuc)
    {
        decimal beklenen =
            SayiDizisiniDecimalOku(talep.IstekVerisiJson).Sum();
        decimal gelen = SonuctanDecimalOku(sonuc.SonucVerisiJson);
        return DecimalSonucuKarsilastir(
            "Toplama",
            beklenen,
            gelen,
            sonuc.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu MatematikCarpmaSonucunuDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sonuc)
    {
        decimal[] sayilar =
            SayiDizisiniDecimalOku(talep.IstekVerisiJson);

        if (sayilar.Length == 0)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Çarpma isteğindeki sayı dizisi boş.");
        }

        decimal beklenen = 1;

        foreach (decimal sayi in sayilar)
        {
            beklenen *= sayi;
        }

        decimal gelen = SonuctanDecimalOku(sonuc.SonucVerisiJson);
        return DecimalSonucuKarsilastir(
            "Çarpma",
            beklenen,
            gelen,
            sonuc.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu OrtalamaSonucunuDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sonuc)
    {
        double[] sayilar = SayiDizisiniDoubleOku(talep.IstekVerisiJson);

        if (sayilar.Length == 0)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Ortalama isteğindeki sayı dizisi boş.");
        }

        double beklenen = sayilar.Average();
        double gelen = SonuctanDoubleOku(sonuc.SonucVerisiJson);
        return DoubleSonucuKarsilastir(
            "Ortalama",
            beklenen,
            gelen,
            sonuc.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu KelimeSayisiSonucunuDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sonuc)
    {
        string metin = MetniOku(talep.IstekVerisiJson, "metin");
        long beklenen = KelimelereAyir(metin).LongCount();
        long gelen = SonuctanInt64Oku(sonuc.SonucVerisiJson);
        return Int64SonucuKarsilastir(
            "Kelime sayısı",
            beklenen,
            gelen,
            sonuc.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu KarakterSayisiSonucunuDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sonuc)
    {
        string metin = MetniOku(talep.IstekVerisiJson, "metin");
        long beklenen = metin.Length;
        long gelen = SonuctanInt64Oku(sonuc.SonucVerisiJson);
        return Int64SonucuKarsilastir(
            "Karakter sayısı",
            beklenen,
            gelen,
            sonuc.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu MedyanSonucunuDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sonuc)
    {
        double[] sayilar = SayiDizisiniDoubleOku(talep.IstekVerisiJson);

        if (sayilar.Length == 0)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Medyan isteğindeki sayı dizisi boş.");
        }

        Array.Sort(sayilar);
        int orta = sayilar.Length / 2;
        double beklenen =
            sayilar.Length % 2 == 1
                ? sayilar[orta]
                : (sayilar[orta - 1] + sayilar[orta]) / 2.0;
        double gelen = SonuctanDoubleOku(sonuc.SonucVerisiJson);
        return DoubleSonucuKarsilastir(
            "Medyan",
            beklenen,
            gelen,
            sonuc.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu StandartSapmaSonucunuDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sonuc)
    {
        double[] sayilar = SayiDizisiniDoubleOku(talep.IstekVerisiJson);

        if (sayilar.Length == 0)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Standart sapma isteğindeki sayı dizisi boş.");
        }

        double ortalama = sayilar.Average();
        double varyans = sayilar.Sum(
            sayi =>
            {
                double fark = sayi - ortalama;
                return fark * fark;
            }) / sayilar.Length;
        double beklenen = Math.Sqrt(varyans);
        double gelen = SonuctanDoubleOku(sonuc.SonucVerisiJson);
        return DoubleSonucuKarsilastir(
            "Standart sapma",
            beklenen,
            gelen,
            sonuc.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu SiralamaSonucunuDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sonuc)
    {
        using JsonDocument istek = JsonDocument.Parse(talep.IstekVerisiJson);
        double[] beklenen =
            DiziAlaniniDoubleOku(istek.RootElement, "sayilar");
        string yon = ZorunluMetinAlaniniOku(istek.RootElement, "yon");

        Array.Sort(beklenen);

        if (string.Equals(yon, "azalan", StringComparison.OrdinalIgnoreCase))
        {
            Array.Reverse(beklenen);
        }
        else if (!string.Equals(
                     yon,
                     "artan",
                     StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException(
                "Sıralama yönü 'artan' veya 'azalan' olmalıdır.");
        }

        double[] gelen = SonuctanDoubleDizisiOku(sonuc.SonucVerisiJson);

        if (beklenen.Length != gelen.Length)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Sıralama sonucu uzunluğu yanlış. " +
                $"Beklenen: {beklenen.Length} | Gelen: {gelen.Length}",
                JsonSerializer.Serialize(new { sonuc = beklenen }),
                sonuc.SonucVerisiJson);
        }

        for (int i = 0; i < beklenen.Length; i++)
        {
            if (!YaklasikEsitMi(beklenen[i], gelen[i]))
            {
                return SonucDogrulamaSonucu.Basarisiz(
                    $"Sıralama sonucu {i}. öğede yanlış. " +
                    $"Beklenen: {beklenen[i]:R} | Gelen: {gelen[i]:R}",
                    JsonSerializer.Serialize(new { sonuc = beklenen }),
                    sonuc.SonucVerisiJson);
            }
        }

        return SonucDogrulamaSonucu.Basarili(
            "Sıralama sonucu doğru.",
            JsonSerializer.Serialize(new { sonuc = beklenen }),
            sonuc.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu AsalCarpanSonucunuDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sonuc)
    {
        using JsonDocument istek = JsonDocument.Parse(talep.IstekVerisiJson);
        long sayi = Int64DegeriniOku(
            ZorunluAlanGetir(istek.RootElement, "sayi"));

        if (sayi < 2)
        {
            throw new FormatException(
                "Asal çarpanlara ayrılacak sayı en az 2 olmalıdır.");
        }

        long[] beklenen = AsalCarpanlariBul(sayi).ToArray();
        long[] gelen = SonuctanInt64DizisiOku(sonuc.SonucVerisiJson);

        if (!beklenen.SequenceEqual(gelen))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Asal çarpan dizisi yanlış veya sıralı değil.",
                JsonSerializer.Serialize(new { sonuc = beklenen }),
                sonuc.SonucVerisiJson);
        }

        return SonucDogrulamaSonucu.Basarili(
            "Asal çarpanlar doğru.",
            JsonSerializer.Serialize(new { sonuc = beklenen }),
            sonuc.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu FrekansAnaliziSonucunuDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sonuc)
    {
        string metin = MetniOku(talep.IstekVerisiJson, "metin");
        Dictionary<string, long> beklenen =
            KelimelereAyir(metin)
                .Select(kelime => kelime.ToLowerInvariant())
                .GroupBy(
                    kelime => kelime,
                    StringComparer.Ordinal)
                .ToDictionary(
                    grup => grup.Key,
                    grup => grup.LongCount(),
                    StringComparer.Ordinal);
        Dictionary<string, long> gelen =
            SonuctanFrekansSozluguOku(sonuc.SonucVerisiJson);

        bool esit =
            beklenen.Count == gelen.Count &&
            beklenen.All(
                cift =>
                    gelen.TryGetValue(cift.Key, out long adet) &&
                    adet == cift.Value);

        if (!esit)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Kelime frekans haritası yanlış.",
                JsonSerializer.Serialize(new { sonuc = beklenen }),
                sonuc.SonucVerisiJson);
        }

        return SonucDogrulamaSonucu.Basarili(
            "Kelime frekans analizi doğru.",
            JsonSerializer.Serialize(new { sonuc = beklenen }),
            sonuc.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu DecimalSonucuKarsilastir(
        string ad,
        decimal beklenen,
        decimal gelen,
        string gelenJson)
    {
        string beklenenJson =
            JsonSerializer.Serialize(
                new { sonuc = beklenen },
                _jsonAyarlari);

        return beklenen == gelen
            ? SonucDogrulamaSonucu.Basarili(
                $"{ad} sonucu doğru.",
                beklenenJson,
                gelenJson)
            : SonucDogrulamaSonucu.Basarisiz(
                $"{ad} sonucu yanlış. Beklenen: {beklenen} | Gelen: {gelen}",
                beklenenJson,
                gelenJson);
    }

    private SonucDogrulamaSonucu DoubleSonucuKarsilastir(
        string ad,
        double beklenen,
        double gelen,
        string gelenJson)
    {
        string beklenenJson =
            JsonSerializer.Serialize(
                new { sonuc = beklenen },
                _jsonAyarlari);

        return YaklasikEsitMi(beklenen, gelen)
            ? SonucDogrulamaSonucu.Basarili(
                $"{ad} sonucu doğru.",
                beklenenJson,
                gelenJson)
            : SonucDogrulamaSonucu.Basarisiz(
                $"{ad} sonucu yanlış. " +
                $"Beklenen: {beklenen:R} | Gelen: {gelen:R}",
                beklenenJson,
                gelenJson);
    }

    private SonucDogrulamaSonucu Int64SonucuKarsilastir(
        string ad,
        long beklenen,
        long gelen,
        string gelenJson)
    {
        string beklenenJson =
            JsonSerializer.Serialize(
                new { sonuc = beklenen },
                _jsonAyarlari);

        return beklenen == gelen
            ? SonucDogrulamaSonucu.Basarili(
                $"{ad} doğru.",
                beklenenJson,
                gelenJson)
            : SonucDogrulamaSonucu.Basarisiz(
                $"{ad} yanlış. Beklenen: {beklenen} | Gelen: {gelen}",
                beklenenJson,
                gelenJson);
    }

    private static decimal[] SayiDizisiniDecimalOku(string json)
    {
        using JsonDocument belge = JsonDocument.Parse(json);
        JsonElement dizi = ZorunluAlanGetir(belge.RootElement, "sayilar");

        if (dizi.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("'sayilar' alanı dizi olmalıdır.");
        }

        return dizi.EnumerateArray()
            .Select(DecimalDegeriniOku)
            .ToArray();
    }

    private static double[] SayiDizisiniDoubleOku(string json)
    {
        using JsonDocument belge = JsonDocument.Parse(json);
        return DiziAlaniniDoubleOku(belge.RootElement, "sayilar");
    }

    private static double[] DiziAlaniniDoubleOku(
        JsonElement kok,
        string alanAdi)
    {
        JsonElement dizi = ZorunluAlanGetir(kok, alanAdi);

        if (dizi.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"'{alanAdi}' alanı dizi olmalıdır.");
        }

        return dizi.EnumerateArray()
            .Select(DoubleDegeriniOku)
            .ToArray();
    }

    private static string MetniOku(string json, string alanAdi)
    {
        using JsonDocument belge = JsonDocument.Parse(json);
        return ZorunluMetinAlaniniOku(belge.RootElement, alanAdi);
    }

    private static decimal SonuctanDecimalOku(string sonucJson)
    {
        using JsonDocument belge = JsonDocument.Parse(sonucJson);
        return DecimalDegeriniOku(
            ZorunluAlanGetir(belge.RootElement, "sonuc"));
    }

    private static double SonuctanDoubleOku(string sonucJson)
    {
        using JsonDocument belge = JsonDocument.Parse(sonucJson);
        return DoubleDegeriniOku(
            ZorunluAlanGetir(belge.RootElement, "sonuc"));
    }

    private static long SonuctanInt64Oku(string sonucJson)
    {
        using JsonDocument belge = JsonDocument.Parse(sonucJson);
        return Int64DegeriniOku(
            ZorunluAlanGetir(belge.RootElement, "sonuc"));
    }

    private static double[] SonuctanDoubleDizisiOku(string sonucJson)
    {
        using JsonDocument belge = JsonDocument.Parse(sonucJson);
        return DiziAlaniniDoubleOku(belge.RootElement, "sonuc");
    }

    private static long[] SonuctanInt64DizisiOku(string sonucJson)
    {
        using JsonDocument belge = JsonDocument.Parse(sonucJson);
        JsonElement dizi = ZorunluAlanGetir(belge.RootElement, "sonuc");

        if (dizi.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("'sonuc' alanı dizi olmalıdır.");
        }

        return dizi.EnumerateArray()
            .Select(Int64DegeriniOku)
            .ToArray();
    }

    private static Dictionary<string, long> SonuctanFrekansSozluguOku(
        string sonucJson)
    {
        using JsonDocument belge = JsonDocument.Parse(sonucJson);
        JsonElement sonuc = ZorunluAlanGetir(belge.RootElement, "sonuc");

        if (sonuc.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException(
                "Frekans analizindeki 'sonuc' alanı nesne olmalıdır.");
        }

        Dictionary<string, long> sozluk =
            new(StringComparer.Ordinal);

        foreach (JsonProperty ozellik in sonuc.EnumerateObject())
        {
            sozluk[ozellik.Name] = Int64DegeriniOku(ozellik.Value);
        }

        return sozluk;
    }

    private static decimal DecimalDegeriniOku(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetDecimal(out decimal sayisalDeger))
        {
            return sayisalDeger;
        }

        if (element.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                element.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal metinDegeri))
        {
            return metinDegeri;
        }

        throw new FormatException(
            "Sayısal değer decimal biçiminde okunamadı.");
    }

    private static double DoubleDegeriniOku(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetDouble(out double sayisalDeger))
        {
            return sayisalDeger;
        }

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(
                element.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double metinDegeri))
        {
            return metinDegeri;
        }

        throw new FormatException(
            "Sayısal değer double biçiminde okunamadı.");
    }

    private static long Int64DegeriniOku(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt64(out long sayisalSonuc))
        {
            return sayisalSonuc;
        }

        if (element.ValueKind == JsonValueKind.String &&
            long.TryParse(
                element.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long metinSonucu))
        {
            return metinSonucu;
        }

        throw new FormatException(
            "Değer tam sayı olmalıdır.");
    }

    private static string ZorunluMetinAlaniniOku(
        JsonElement kok,
        string alanAdi)
    {
        JsonElement alan = ZorunluAlanGetir(kok, alanAdi);

        if (alan.ValueKind != JsonValueKind.String)
        {
            throw new FormatException(
                $"'{alanAdi}' alanı metin olmalıdır.");
        }

        return alan.GetString() ?? string.Empty;
    }

    private static JsonElement ZorunluAlanGetir(
        JsonElement kok,
        string alanAdi)
    {
        if (kok.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException(
                "JSON kök değeri nesne olmalıdır.");
        }

        foreach (JsonProperty ozellik in kok.EnumerateObject())
        {
            if (string.Equals(
                    ozellik.Name,
                    alanAdi,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ozellik.Value;
            }
        }

        throw new FormatException(
            $"Zorunlu JSON alanı bulunamadı: {alanAdi}");
    }

    private static IEnumerable<string> KelimelereAyir(string metin)
    {
        if (string.IsNullOrWhiteSpace(metin))
        {
            return [];
        }

        return metin.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
    }

    private static IEnumerable<long> AsalCarpanlariBul(long sayi)
    {
        long kalan = sayi;

        for (long bolen = 2; bolen <= kalan / bolen; bolen += bolen == 2 ? 1 : 2)
        {
            while (kalan % bolen == 0)
            {
                yield return bolen;
                kalan /= bolen;
            }
        }

        if (kalan > 1)
        {
            yield return kalan;
        }
    }

    private static bool YaklasikEsitMi(double beklenen, double gelen)
    {
        if (double.IsNaN(beklenen) ||
            double.IsNaN(gelen) ||
            double.IsInfinity(beklenen) ||
            double.IsInfinity(gelen))
        {
            return false;
        }

        double fark = Math.Abs(beklenen - gelen);
        double olcek =
            Math.Max(
                1,
                Math.Max(Math.Abs(beklenen), Math.Abs(gelen)));
        return fark <= OndalikToleransi * olcek;
    }
}
