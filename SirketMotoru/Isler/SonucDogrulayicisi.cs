using System.Globalization;
using System.Text.Json;
using SirketMotoru.Protokol.Mesajlar;

namespace SirketMotoru.Isler;

public sealed class SonucDogrulayicisi
{
    private const double OndalikToleransi =
        0.000001;

    private readonly JsonSerializerOptions _jsonAyarlari =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public SonucDogrulamaSonucu Dogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sirketSonucu)
    {
        ArgumentNullException.ThrowIfNull(
            talep);

        ArgumentNullException.ThrowIfNull(
            sirketSonucu);

        SonucDogrulamaSonucu temelDogrulama =
            TemelAlanlariDogrula(
                talep,
                sirketSonucu);

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
                gelenSonucJson:
                    sirketSonucu.SonucVerisiJson);
        }

        try
        {
            return talep.HizmetKimligi
                .Trim()
                .ToLowerInvariant() switch
            {
                "matematik.topla" =>
                    MatematikToplamaSonucunuDogrula(
                        talep,
                        sirketSonucu),

                "matematik.carp" =>
                    MatematikCarpmaSonucunuDogrula(
                        talep,
                        sirketSonucu),

                "veri.ortalama-hesapla" =>
                    OrtalamaSonucunuDogrula(
                        talep,
                        sirketSonucu),

                "metin.kelime-say" =>
                    KelimeSayisiSonucunuDogrula(
                        talep,
                        sirketSonucu),

                "metin.karakter-say" =>
                    KarakterSayisiSonucunuDogrula(
                        talep,
                        sirketSonucu),

                _ =>
                    SonucDogrulamaSonucu.Basarisiz(
                        $"Hizmet için sonuç doğrulayıcı bulunamadı: " +
                        $"{talep.HizmetKimligi}@{talep.HizmetSurumu}",
                        gelenSonucJson:
                            sirketSonucu.SonucVerisiJson)
            };
        }
        catch (JsonException exception)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Şirket sonucu veya istek verisi geçerli JSON değil: " +
                $"{exception.Message}",
                gelenSonucJson:
                    sirketSonucu.SonucVerisiJson);
        }
        catch (FormatException exception)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Sonuç veri biçimi geçersiz: " +
                $"{exception.Message}",
                gelenSonucJson:
                    sirketSonucu.SonucVerisiJson);
        }
        catch (OverflowException exception)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Sonuç hesaplanırken sayısal taşma oluştu: " +
                $"{exception.Message}",
                gelenSonucJson:
                    sirketSonucu.SonucVerisiJson);
        }
        catch (Exception exception)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Sonuç doğrulama sırasında beklenmeyen hata oluştu: " +
                $"{exception.Message}",
                gelenSonucJson:
                    sirketSonucu.SonucVerisiJson);
        }
    }

    private static SonucDogrulamaSonucu TemelAlanlariDogrula(
        HizmetTalebi talep,
        IsSonucuMesaji sirketSonucu)
    {
        if (string.IsNullOrWhiteSpace(
                sirketSonucu.IstekKimligi))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Şirket sonucundaki istek kimliği boş.");
        }

        if (string.IsNullOrWhiteSpace(
                sirketSonucu.IsKimligi))
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
                $"İş kimliği uyuşmuyor. " +
                $"Beklenen: {talep.IsKimligi} | " +
                $"Gelen: {sirketSonucu.IsKimligi}");
        }

        if (string.IsNullOrWhiteSpace(
                sirketSonucu.SirketKimligi))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Şirket sonucu, şirket kimliği içermiyor.");
        }

        if (string.IsNullOrWhiteSpace(
                talep.SecilenSirketKimligi))
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
            string.IsNullOrWhiteSpace(
                sirketSonucu.SonucVerisiJson))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Başarılı olarak bildirilen sonuçta veri bulunmuyor.");
        }

        return SonucDogrulamaSonucu.Basarili(
            "Temel mesaj alanları geçerli.");
    }

    private SonucDogrulamaSonucu
        MatematikToplamaSonucunuDogrula(
            HizmetTalebi talep,
            IsSonucuMesaji sirketSonucu)
    {
        using JsonDocument istekBelgesi =
            JsonDocument.Parse(
                talep.IstekVerisiJson);

        JsonElement sayilarElementi =
            ZorunluAlanGetir(
                istekBelgesi.RootElement,
                "sayilar");

        if (sayilarElementi.ValueKind !=
            JsonValueKind.Array)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Toplama isteğindeki 'sayilar' alanı dizi değil.");
        }

        decimal beklenenSonuc = 0;

        foreach (JsonElement sayiElementi in
                 sayilarElementi.EnumerateArray())
        {
            beklenenSonuc +=
                DecimalDegeriniOku(
                    sayiElementi);
        }

        decimal gelenSonuc =
            SonuctanDecimalOku(
                sirketSonucu.SonucVerisiJson);

        string beklenenJson =
            JsonSerializer.Serialize(
                new
                {
                    sonuc = beklenenSonuc
                },
                _jsonAyarlari);

        if (beklenenSonuc != gelenSonuc)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Toplama sonucu yanlış. " +
                $"Beklenen: {beklenenSonuc} | " +
                $"Gelen: {gelenSonuc}",
                beklenenJson,
                sirketSonucu.SonucVerisiJson);
        }

        return SonucDogrulamaSonucu.Basarili(
            "Toplama sonucu doğru.",
            beklenenJson,
            sirketSonucu.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu
        MatematikCarpmaSonucunuDogrula(
            HizmetTalebi talep,
            IsSonucuMesaji sirketSonucu)
    {
        using JsonDocument istekBelgesi =
            JsonDocument.Parse(
                talep.IstekVerisiJson);

        JsonElement sayilarElementi =
            ZorunluAlanGetir(
                istekBelgesi.RootElement,
                "sayilar");

        if (sayilarElementi.ValueKind !=
            JsonValueKind.Array)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Çarpma isteğindeki 'sayilar' alanı dizi değil.");
        }

        decimal beklenenSonuc = 1;

        int sayiAdedi = 0;

        foreach (JsonElement sayiElementi in
                 sayilarElementi.EnumerateArray())
        {
            beklenenSonuc *=
                DecimalDegeriniOku(
                    sayiElementi);

            sayiAdedi++;
        }

        if (sayiAdedi == 0)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Çarpma isteğindeki sayı dizisi boş.");
        }

        decimal gelenSonuc =
            SonuctanDecimalOku(
                sirketSonucu.SonucVerisiJson);

        string beklenenJson =
            JsonSerializer.Serialize(
                new
                {
                    sonuc = beklenenSonuc
                },
                _jsonAyarlari);

        if (beklenenSonuc != gelenSonuc)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Çarpma sonucu yanlış. " +
                $"Beklenen: {beklenenSonuc} | " +
                $"Gelen: {gelenSonuc}",
                beklenenJson,
                sirketSonucu.SonucVerisiJson);
        }

        return SonucDogrulamaSonucu.Basarili(
            "Çarpma sonucu doğru.",
            beklenenJson,
            sirketSonucu.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu
        OrtalamaSonucunuDogrula(
            HizmetTalebi talep,
            IsSonucuMesaji sirketSonucu)
    {
        using JsonDocument istekBelgesi =
            JsonDocument.Parse(
                talep.IstekVerisiJson);

        JsonElement sayilarElementi =
            ZorunluAlanGetir(
                istekBelgesi.RootElement,
                "sayilar");

        if (sayilarElementi.ValueKind !=
            JsonValueKind.Array)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Ortalama isteğindeki 'sayilar' alanı dizi değil.");
        }

        double toplam = 0;

        int sayiAdedi = 0;

        foreach (JsonElement sayiElementi in
                 sayilarElementi.EnumerateArray())
        {
            toplam +=
                DoubleDegeriniOku(
                    sayiElementi);

            sayiAdedi++;
        }

        if (sayiAdedi == 0)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                "Ortalama isteğindeki sayı dizisi boş.");
        }

        double beklenenSonuc =
            toplam / sayiAdedi;

        double gelenSonuc =
            SonuctanDoubleOku(
                sirketSonucu.SonucVerisiJson);

        string beklenenJson =
            JsonSerializer.Serialize(
                new
                {
                    sonuc = beklenenSonuc
                },
                _jsonAyarlari);

        if (!YaklasikEsitMi(
                beklenenSonuc,
                gelenSonuc))
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Ortalama sonucu yanlış. " +
                $"Beklenen: {beklenenSonuc:R} | " +
                $"Gelen: {gelenSonuc:R}",
                beklenenJson,
                sirketSonucu.SonucVerisiJson);
        }

        return SonucDogrulamaSonucu.Basarili(
            "Ortalama sonucu doğru.",
            beklenenJson,
            sirketSonucu.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu
        KelimeSayisiSonucunuDogrula(
            HizmetTalebi talep,
            IsSonucuMesaji sirketSonucu)
    {
        using JsonDocument istekBelgesi =
            JsonDocument.Parse(
                talep.IstekVerisiJson);

        string metin =
            ZorunluMetinAlaniniOku(
                istekBelgesi.RootElement,
                "metin");

        int beklenenSonuc =
            KelimeSayisiniHesapla(
                metin);

        long gelenSonuc =
            SonuctanInt64Oku(
                sirketSonucu.SonucVerisiJson);

        string beklenenJson =
            JsonSerializer.Serialize(
                new
                {
                    sonuc = beklenenSonuc
                },
                _jsonAyarlari);

        if (beklenenSonuc != gelenSonuc)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Kelime sayısı yanlış. " +
                $"Beklenen: {beklenenSonuc} | " +
                $"Gelen: {gelenSonuc}",
                beklenenJson,
                sirketSonucu.SonucVerisiJson);
        }

        return SonucDogrulamaSonucu.Basarili(
            "Kelime sayısı doğru.",
            beklenenJson,
            sirketSonucu.SonucVerisiJson);
    }

    private SonucDogrulamaSonucu
        KarakterSayisiSonucunuDogrula(
            HizmetTalebi talep,
            IsSonucuMesaji sirketSonucu)
    {
        using JsonDocument istekBelgesi =
            JsonDocument.Parse(
                talep.IstekVerisiJson);

        string metin =
            ZorunluMetinAlaniniOku(
                istekBelgesi.RootElement,
                "metin");

        int beklenenSonuc =
            metin.Length;

        long gelenSonuc =
            SonuctanInt64Oku(
                sirketSonucu.SonucVerisiJson);

        string beklenenJson =
            JsonSerializer.Serialize(
                new
                {
                    sonuc = beklenenSonuc
                },
                _jsonAyarlari);

        if (beklenenSonuc != gelenSonuc)
        {
            return SonucDogrulamaSonucu.Basarisiz(
                $"Karakter sayısı yanlış. " +
                $"Beklenen: {beklenenSonuc} | " +
                $"Gelen: {gelenSonuc}",
                beklenenJson,
                sirketSonucu.SonucVerisiJson);
        }

        return SonucDogrulamaSonucu.Basarili(
            "Karakter sayısı doğru.",
            beklenenJson,
            sirketSonucu.SonucVerisiJson);
    }

    private static decimal SonuctanDecimalOku(
        string sonucJson)
    {
        using JsonDocument belge =
            JsonDocument.Parse(
                sonucJson);

        JsonElement sonucElementi =
            ZorunluAlanGetir(
                belge.RootElement,
                "sonuc");

        return DecimalDegeriniOku(
            sonucElementi);
    }

    private static double SonuctanDoubleOku(
        string sonucJson)
    {
        using JsonDocument belge =
            JsonDocument.Parse(
                sonucJson);

        JsonElement sonucElementi =
            ZorunluAlanGetir(
                belge.RootElement,
                "sonuc");

        return DoubleDegeriniOku(
            sonucElementi);
    }

    private static long SonuctanInt64Oku(
        string sonucJson)
    {
        using JsonDocument belge =
            JsonDocument.Parse(
                sonucJson);

        JsonElement sonucElementi =
            ZorunluAlanGetir(
                belge.RootElement,
                "sonuc");

        if (sonucElementi.ValueKind ==
                JsonValueKind.Number &&
            sonucElementi.TryGetInt64(
                out long sayisalSonuc))
        {
            return sayisalSonuc;
        }

        if (sonucElementi.ValueKind ==
                JsonValueKind.String &&
            long.TryParse(
                sonucElementi.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long metinSonucu))
        {
            return metinSonucu;
        }

        throw new FormatException(
            "'sonuc' alanı tam sayı olmalıdır.");
    }

    private static decimal DecimalDegeriniOku(
        JsonElement element)
    {
        if (element.ValueKind ==
                JsonValueKind.Number &&
            element.TryGetDecimal(
                out decimal sayisalDeger))
        {
            return sayisalDeger;
        }

        if (element.ValueKind ==
                JsonValueKind.String &&
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

    private static double DoubleDegeriniOku(
        JsonElement element)
    {
        if (element.ValueKind ==
                JsonValueKind.Number &&
            element.TryGetDouble(
                out double sayisalDeger))
        {
            return sayisalDeger;
        }

        if (element.ValueKind ==
                JsonValueKind.String &&
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

    private static string ZorunluMetinAlaniniOku(
        JsonElement kok,
        string alanAdi)
    {
        JsonElement alan =
            ZorunluAlanGetir(
                kok,
                alanAdi);

        if (alan.ValueKind !=
            JsonValueKind.String)
        {
            throw new FormatException(
                $"'{alanAdi}' alanı metin olmalıdır.");
        }

        return alan.GetString() ??
               string.Empty;
    }

    private static JsonElement ZorunluAlanGetir(
        JsonElement kok,
        string alanAdi)
    {
        if (kok.ValueKind !=
            JsonValueKind.Object)
        {
            throw new FormatException(
                "JSON kök değeri nesne olmalıdır.");
        }

        foreach (JsonProperty ozellik in
                 kok.EnumerateObject())
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
            $"Zorunlu JSON alanı bulunamadı: " +
            $"{alanAdi}");
    }

    private static int KelimeSayisiniHesapla(
        string metin)
    {
        if (string.IsNullOrWhiteSpace(
                metin))
        {
            return 0;
        }

        return metin.Split(
                [
                    ' ',
                    '\t',
                    '\r',
                    '\n'
                ],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Length;
    }

    private static bool YaklasikEsitMi(
        double beklenen,
        double gelen)
    {
        if (double.IsNaN(
                beklenen) ||
            double.IsNaN(
                gelen) ||
            double.IsInfinity(
                beklenen) ||
            double.IsInfinity(
                gelen))
        {
            return false;
        }

        double fark =
            Math.Abs(
                beklenen - gelen);

        double olcek =
            Math.Max(
                1,
                Math.Max(
                    Math.Abs(beklenen),
                    Math.Abs(gelen)));

        return fark <=
               OndalikToleransi * olcek;
    }
}