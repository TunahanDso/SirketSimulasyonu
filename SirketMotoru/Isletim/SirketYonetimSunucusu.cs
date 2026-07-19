using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SirketMotoru.Ayarlar;
using SirketMotoru.Kayit;

namespace SirketMotoru.Isletim;

public sealed class SirketYonetimSunucusu : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly MotorAyarlari _ayarlar;
    private readonly KodTabanliSirketIsletimYoneticisi _isletim;
    private readonly ConcurrentDictionary<string, OturumKaydi> _oturumlar =
        new(StringComparer.Ordinal);

    private TcpListener? _dinleyici;
    private CancellationTokenSource? _iptal;
    private Task? _gorev;
    private bool _baslatildi;
    private bool _disposed;

    public SirketYonetimSunucusu(
        MotorAyarlari ayarlar,
        KodTabanliSirketIsletimYoneticisi isletim)
    {
        _ayarlar = ayarlar ?? throw new ArgumentNullException(nameof(ayarlar));
        _isletim = isletim ?? throw new ArgumentNullException(nameof(isletim));
    }

    public Task BaslatAsync(CancellationToken cancellationToken)
    {
        if (_baslatildi)
        {
            return Task.CompletedTask;
        }

        _baslatildi = true;

        if (!_ayarlar.SirketYonetimAktif)
        {
            KonsolKayitcisi.Bilgi("Şirket yönetim kapısı ayarlardan kapalı.");
            return Task.CompletedTask;
        }

        _iptal = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        _dinleyici = new TcpListener(
            IPAddress.Any,
            _ayarlar.SirketYonetimPortu);
        _dinleyici.Start(128);
        _gorev = KabulDongusuAsync(_iptal.Token);

        KonsolKayitcisi.Basari(
            $"Şirket yönetim kapısı yayında | " +
            $"Port: {_ayarlar.SirketYonetimPortu} | " +
            "Rol: izleme, finans ve koddan ilan edilen yayınları yönetme");

        foreach (string adres in YayinAdresleri())
        {
            KonsolKayitcisi.Bilgi($"Şirket yönetim adresi: {adres}");
        }

        return Task.CompletedTask;
    }

    private async Task KabulDongusuAsync(
        CancellationToken cancellationToken)
    {
        if (_dinleyici is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                TcpClient istemci = await _dinleyici.AcceptTcpClientAsync(
                    cancellationToken);
                _ = IstemciyiYonetAsync(istemci, cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                KonsolKayitcisi.Uyari(
                    $"Şirket yönetim istemci kabul hatası: " +
                    exception.Message);

                try
                {
                    await Task.Delay(250, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task IstemciyiYonetAsync(
        TcpClient istemci,
        CancellationToken cancellationToken)
    {
        using (istemci)
        {
            istemci.NoDelay = true;

            try
            {
                await using NetworkStream akis = istemci.GetStream();
                HttpIstegi? istek = await IstekOkuAsync(
                    akis,
                    cancellationToken);

                if (istek is null)
                {
                    await MetinCevabiAsync(
                        akis,
                        400,
                        "Bad Request",
                        "Geçersiz HTTP isteği.",
                        cancellationToken);
                    return;
                }

                await YonlendirAsync(akis, istek, cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                KonsolKayitcisi.Uyari(
                    $"Şirket yönetim istemci hatası: {exception.Message}");
            }
        }
    }

    private async Task YonlendirAsync(
        NetworkStream akis,
        HttpIstegi istek,
        CancellationToken cancellationToken)
    {
        string yol = istek.Yol.Split('?', 2)[0];

        if (istek.Metot == "GET" && yol == "/")
        {
            await CevapGonderAsync(
                akis,
                200,
                "OK",
                "text/html; charset=utf-8",
                SirketYonetimHtml.Icerik,
                null,
                cancellationToken);
            return;
        }

        if (istek.Metot == "GET" && yol == "/api/saglik")
        {
            await JsonCevabiAsync(
                akis,
                200,
                new
                {
                    durum = "calisiyor",
                    port = _ayarlar.SirketYonetimPortu,
                    aktifOturum = _oturumlar.Count,
                    rol = "koddan-ilan-edilen-yayinlari-yonetir"
                },
                null,
                cancellationToken);
            return;
        }

        if (istek.Metot == "POST" && yol == "/api/giris")
        {
            GirisIstegi? giris = JsonOku<GirisIstegi>(istek.Govde);

            if (giris is null)
            {
                await JsonCevabiAsync(
                    akis,
                    400,
                    IslemSonucu.Hata("Giriş verisi geçersiz."),
                    null,
                    cancellationToken);
                return;
            }

            IslemSonucu sonuc = await _isletim.GirisDogrulaAsync(
                giris,
                cancellationToken);

            if (!sonuc.Basarili)
            {
                await JsonCevabiAsync(
                    akis,
                    401,
                    sonuc,
                    null,
                    cancellationToken);
                return;
            }

            string sirketKimligi = SirketKimliginiOku(sonuc.Veri);
            if (string.IsNullOrWhiteSpace(sirketKimligi))
            {
                await JsonCevabiAsync(
                    akis,
                    500,
                    IslemSonucu.Hata("Şirket oturumu oluşturulamadı."),
                    null,
                    cancellationToken);
                return;
            }

            string token = Convert.ToHexString(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            _oturumlar[token] = new OturumKaydi
            {
                SirketKimligi = sirketKimligi,
                SonKullanmaZamani = DateTimeOffset.UtcNow.AddMinutes(
                    Math.Clamp(
                        _ayarlar.SirketYonetimOturumDakika,
                        10,
                        1_440))
            };

            await JsonCevabiAsync(
                akis,
                200,
                sonuc,
                new Dictionary<string, string>
                {
                    ["Set-Cookie"] =
                        $"sirketOturumu={token}; Path=/; HttpOnly; SameSite=Strict"
                },
                cancellationToken);
            return;
        }

        OturumKaydi? oturum = OturumuGetir(istek.Basliklar);
        if (oturum is null)
        {
            await JsonCevabiAsync(
                akis,
                401,
                IslemSonucu.Hata("Oturum bulunamadı. Yeniden giriş yapın."),
                new Dictionary<string, string>
                {
                    ["Set-Cookie"] =
                        "sirketOturumu=; Path=/; Max-Age=0; HttpOnly; SameSite=Strict"
                },
                cancellationToken);
            return;
        }

        oturum.SonKullanmaZamani = DateTimeOffset.UtcNow.AddMinutes(
            Math.Clamp(
                _ayarlar.SirketYonetimOturumDakika,
                10,
                1_440));

        if (istek.Metot == "POST" && yol == "/api/cikis")
        {
            string? token = CookieDegeri(
                istek.Basliklar,
                "sirketOturumu");

            if (!string.IsNullOrWhiteSpace(token))
            {
                _oturumlar.TryRemove(token, out _);
            }

            await JsonCevabiAsync(
                akis,
                200,
                IslemSonucu.Basari("Çıkış yapıldı."),
                new Dictionary<string, string>
                {
                    ["Set-Cookie"] =
                        "sirketOturumu=; Path=/; Max-Age=0; HttpOnly; SameSite=Strict"
                },
                cancellationToken);
            return;
        }

        if (istek.Metot == "GET" && yol == "/api/durum")
        {
            string json = await _isletim.PanelJsonuOlusturAsync(
                oturum.SirketKimligi,
                cancellationToken);
            await HamJsonCevabiAsync(
                akis,
                200,
                json,
                cancellationToken);
            return;
        }

        if (istek.Metot != "POST")
        {
            await MetinCevabiAsync(
                akis,
                405,
                "Method Not Allowed",
                "Bu uç nokta POST bekliyor.",
                cancellationToken);
            return;
        }

        IslemSonucu islemSonucu = yol switch
        {
            "/api/parola" =>
                await CalistirAsync<ParolaDegistirIstegi>(
                    istek,
                    dto => _isletim.ParolaDegistirAsync(
                        oturum.SirketKimligi,
                        dto,
                        cancellationToken)),

            "/api/yatirim" =>
                await CalistirAsync<YatirimIstegi>(
                    istek,
                    dto => _isletim.YatirimSatinAlAsync(
                        oturum.SirketKimligi,
                        dto,
                        cancellationToken)),

            "/api/kredi" =>
                await CalistirAsync<KrediIstegi>(
                    istek,
                    dto => _isletim.KrediCekAsync(
                        oturum.SirketKimligi,
                        dto,
                        cancellationToken)),

            "/api/hizmet/fiyat" =>
                await CalistirAsync<FiyatGuncelleIstegi>(
                    istek,
                    dto => _isletim.HizmetFiyatiGuncelleAsync(
                        oturum.SirketKimligi,
                        dto,
                        cancellationToken)),

            "/api/hizmet/durum" =>
                await CalistirAsync<HizmetYayinDurumuIstegi>(
                    istek,
                    dto => _isletim.HizmetYayinDurumuGuncelleAsync(
                        oturum.SirketKimligi,
                        dto,
                        cancellationToken)),

            "/api/uygulama/yayinla" =>
                await CalistirAsync<UygulamaYayinlaIstegi>(
                    istek,
                    dto => _isletim.UygulamaYayinlaAsync(
                        oturum.SirketKimligi,
                        dto,
                        cancellationToken)),

            "/api/uygulama/guncelle" =>
                await CalistirAsync<UrunGuncelleIstegi>(
                    istek,
                    dto => _isletim.UygulamaGuncelleAsync(
                        oturum.SirketKimligi,
                        dto,
                        cancellationToken)),

            "/api/uygulama/kapasite" =>
                await CalistirAsync<UrunKapasiteIstegi>(
                    istek,
                    dto => _isletim.UygulamaKapasitesiArtirAsync(
                        oturum.SirketKimligi,
                        dto,
                        cancellationToken)),

            "/api/protokol/yayinla" =>
                await CalistirAsync<ProtokolYayinlaIstegi>(
                    istek,
                    dto => _isletim.ProtokolYayinlaAsync(
                        oturum.SirketKimligi,
                        dto,
                        cancellationToken)),

            "/api/protokol/benimse" =>
                await CalistirAsync<ProtokolBenimseIstegi>(
                    istek,
                    dto => _isletim.ProtokolBenimseAsync(
                        oturum.SirketKimligi,
                        dto,
                        cancellationToken)),

            "/api/sozlesme/kabul" =>
                await CalistirAsync<SozlesmeKabulIstegi>(
                    istek,
                    dto => _isletim.SozlesmeKabulEtAsync(
                        oturum.SirketKimligi,
                        dto,
                        cancellationToken)),

            _ => IslemSonucu.Hata("Uç nokta bulunamadı.")
        };

        int durumKodu = yol is
            "/api/parola" or
            "/api/yatirim" or
            "/api/kredi" or
            "/api/hizmet/fiyat" or
            "/api/hizmet/durum" or
            "/api/uygulama/yayinla" or
            "/api/uygulama/guncelle" or
            "/api/uygulama/kapasite" or
            "/api/protokol/yayinla" or
            "/api/protokol/benimse" or
            "/api/sozlesme/kabul"
                ? (islemSonucu.Basarili ? 200 : 400)
                : 404;

        await JsonCevabiAsync(
            akis,
            durumKodu,
            islemSonucu,
            null,
            cancellationToken);
    }

    private static async Task<IslemSonucu> CalistirAsync<T>(
        HttpIstegi istek,
        Func<T, Task<IslemSonucu>> islem)
        where T : class
    {
        T? dto = JsonOku<T>(istek.Govde);
        if (dto is null)
        {
            return IslemSonucu.Hata("İstek JSON'u geçersiz.");
        }

        return await islem(dto);
    }

    private OturumKaydi? OturumuGetir(
        IReadOnlyDictionary<string, string> basliklar)
    {
        string? token = CookieDegeri(basliklar, "sirketOturumu");

        if (string.IsNullOrWhiteSpace(token) ||
            !_oturumlar.TryGetValue(token, out OturumKaydi? oturum))
        {
            return null;
        }

        if (oturum.SonKullanmaZamani <= DateTimeOffset.UtcNow)
        {
            _oturumlar.TryRemove(token, out _);
            return null;
        }

        return oturum;
    }

    private static string? CookieDegeri(
        IReadOnlyDictionary<string, string> basliklar,
        string ad)
    {
        if (!basliklar.TryGetValue("cookie", out string? cookie))
        {
            return null;
        }

        foreach (string parca in cookie.Split(';'))
        {
            string[] cift = parca.Trim().Split('=', 2);
            if (cift.Length == 2 &&
                string.Equals(cift[0], ad, StringComparison.Ordinal))
            {
                return cift[1];
            }
        }

        return null;
    }

    private static string SirketKimliginiOku(object? veri)
    {
        if (veri is null)
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument belge = JsonDocument.Parse(
                JsonSerializer.Serialize(veri, JsonAyarlari));

            return belge.RootElement.TryGetProperty(
                       "sirketKimligi",
                       out JsonElement alan)
                ? alan.GetString() ?? string.Empty
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static T? JsonOku<T>(string json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonAyarlari);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<HttpIstegi?> IstekOkuAsync(
        NetworkStream akis,
        CancellationToken cancellationToken)
    {
        const int azamiBaslik = 32_768;
        const int azamiGovde = 1_048_576;
        byte[] tampon = new byte[4_096];
        using MemoryStream ham = new();
        int baslikSonu = -1;

        while (ham.Length < azamiBaslik)
        {
            int okunan = await akis.ReadAsync(tampon, cancellationToken);
            if (okunan == 0)
            {
                return null;
            }

            ham.Write(tampon, 0, okunan);
            byte[] mevcut = ham.GetBuffer();
            baslikSonu = BaslikSonunuBul(mevcut, (int)ham.Length);
            if (baslikSonu >= 0)
            {
                break;
            }
        }

        if (baslikSonu < 0)
        {
            return null;
        }

        byte[] tum = ham.ToArray();
        string baslikMetni = Encoding.ASCII.GetString(
            tum,
            0,
            baslikSonu);
        string[] satirlar = baslikMetni.Split(
            "\r\n",
            StringSplitOptions.None);

        if (satirlar.Length == 0)
        {
            return null;
        }

        string[] ilk = satirlar[0].Split(
            ' ',
            3,
            StringSplitOptions.RemoveEmptyEntries);

        if (ilk.Length != 3)
        {
            return null;
        }

        Dictionary<string, string> basliklar =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string satir in satirlar.Skip(1))
        {
            int ikiNokta = satir.IndexOf(':');
            if (ikiNokta <= 0)
            {
                continue;
            }

            basliklar[satir[..ikiNokta].Trim().ToLowerInvariant()] =
                satir[(ikiNokta + 1)..].Trim();
        }

        int icerikUzunlugu = 0;
        if (basliklar.TryGetValue(
                "content-length",
                out string? uzunlukMetni) &&
            (!int.TryParse(uzunlukMetni, out icerikUzunlugu) ||
             icerikUzunlugu is < 0 or > azamiGovde))
        {
            return null;
        }

        int govdeBaslangici = baslikSonu + 4;
        using MemoryStream govde = new();
        int hazirGovde = Math.Max(0, tum.Length - govdeBaslangici);

        if (hazirGovde > 0)
        {
            govde.Write(
                tum,
                govdeBaslangici,
                Math.Min(hazirGovde, icerikUzunlugu));
        }

        while (govde.Length < icerikUzunlugu)
        {
            int kalan = Math.Min(
                tampon.Length,
                icerikUzunlugu - (int)govde.Length);
            int okunan = await akis.ReadAsync(
                tampon.AsMemory(0, kalan),
                cancellationToken);

            if (okunan == 0)
            {
                return null;
            }

            govde.Write(tampon, 0, okunan);
        }

        return new HttpIstegi
        {
            Metot = ilk[0].ToUpperInvariant(),
            Yol = ilk[1],
            Basliklar = basliklar,
            Govde = Encoding.UTF8.GetString(govde.ToArray())
        };
    }

    private static int BaslikSonunuBul(byte[] veri, int uzunluk)
    {
        for (int i = 0; i <= uzunluk - 4; i++)
        {
            if (veri[i] == 13 &&
                veri[i + 1] == 10 &&
                veri[i + 2] == 13 &&
                veri[i + 3] == 10)
            {
                return i;
            }
        }

        return -1;
    }

    private static Task JsonCevabiAsync(
        NetworkStream akis,
        int kod,
        object veri,
        IReadOnlyDictionary<string, string>? ekBasliklar,
        CancellationToken cancellationToken) =>
        CevapGonderAsync(
            akis,
            kod,
            DurumMetni(kod),
            "application/json; charset=utf-8",
            JsonSerializer.Serialize(veri, JsonAyarlari),
            ekBasliklar,
            cancellationToken);

    private static Task HamJsonCevabiAsync(
        NetworkStream akis,
        int kod,
        string json,
        CancellationToken cancellationToken) =>
        CevapGonderAsync(
            akis,
            kod,
            DurumMetni(kod),
            "application/json; charset=utf-8",
            json,
            null,
            cancellationToken);

    private static Task MetinCevabiAsync(
        NetworkStream akis,
        int kod,
        string durum,
        string metin,
        CancellationToken cancellationToken) =>
        CevapGonderAsync(
            akis,
            kod,
            durum,
            "text/plain; charset=utf-8",
            metin,
            null,
            cancellationToken);

    private static async Task CevapGonderAsync(
        NetworkStream akis,
        int kod,
        string durum,
        string icerikTuru,
        string govde,
        IReadOnlyDictionary<string, string>? ekBasliklar,
        CancellationToken cancellationToken)
    {
        byte[] govdeBaytlari = Encoding.UTF8.GetBytes(govde);
        StringBuilder baslik = new();
        baslik.Append($"HTTP/1.1 {kod} {durum}\r\n");
        baslik.Append($"Content-Type: {icerikTuru}\r\n");
        baslik.Append($"Content-Length: {govdeBaytlari.Length}\r\n");
        baslik.Append("Connection: close\r\n");
        baslik.Append("Cache-Control: no-store\r\n");
        baslik.Append("X-Content-Type-Options: nosniff\r\n");
        baslik.Append("X-Frame-Options: DENY\r\n");

        if (ekBasliklar is not null)
        {
            foreach ((string ad, string deger) in ekBasliklar)
            {
                baslik.Append($"{ad}: {deger}\r\n");
            }
        }

        baslik.Append("\r\n");
        byte[] baslikBaytlari = Encoding.ASCII.GetBytes(
            baslik.ToString());

        await akis.WriteAsync(baslikBaytlari, cancellationToken);
        await akis.WriteAsync(govdeBaytlari, cancellationToken);
        await akis.FlushAsync(cancellationToken);
    }

    private static string DurumMetni(int kod) => kod switch
    {
        200 => "OK",
        400 => "Bad Request",
        401 => "Unauthorized",
        404 => "Not Found",
        405 => "Method Not Allowed",
        500 => "Internal Server Error",
        _ => "OK"
    };

    private IEnumerable<string> YayinAdresleri()
    {
        HashSet<string> adresler =
            new(StringComparer.OrdinalIgnoreCase)
            {
                $"http://localhost:{_ayarlar.SirketYonetimPortu}/"
            };

        foreach (NetworkInterface ag in
                 NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ag.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (UnicastIPAddressInformation adres in
                     ag.GetIPProperties().UnicastAddresses)
            {
                if (adres.Address.AddressFamily ==
                    AddressFamily.InterNetwork)
                {
                    adresler.Add(
                        $"http://{adres.Address}:" +
                        $"{_ayarlar.SirketYonetimPortu}/");
                }
            }
        }

        return adresler.OrderBy(adres => adres);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _iptal?.Cancel();
            _dinleyici?.Stop();

            if (_gorev is not null)
            {
                await _gorev;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _iptal?.Dispose();
        }
    }

    private sealed class HttpIstegi
    {
        public string Metot { get; init; } = string.Empty;
        public string Yol { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, string> Basliklar { get; init; } =
            new Dictionary<string, string>();
        public string Govde { get; init; } = string.Empty;
    }

    private sealed class OturumKaydi
    {
        public string SirketKimligi { get; init; } = string.Empty;
        public DateTimeOffset SonKullanmaZamani { get; set; }
    }
}
