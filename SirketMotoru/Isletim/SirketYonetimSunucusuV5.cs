using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private readonly EkosistemYoneticisi _ekosistem;
    private readonly FinansV7Yoneticisi _finans;
    private readonly ConcurrentDictionary<string, OturumKaydi> _oturumlar = new(StringComparer.Ordinal);
    private TcpListener? _dinleyici;
    private CancellationTokenSource? _iptal;
    private Task? _gorev;
    private bool _baslatildi;
    private bool _disposed;

    public SirketYonetimSunucusu(
        MotorAyarlari ayarlar,
        KodTabanliSirketIsletimYoneticisi isletim,
        EkosistemYoneticisi ekosistem,
        FinansV7Yoneticisi finans)
    {
        _ayarlar = ayarlar ?? throw new ArgumentNullException(nameof(ayarlar));
        _isletim = isletim ?? throw new ArgumentNullException(nameof(isletim));
        _ekosistem = ekosistem ?? throw new ArgumentNullException(nameof(ekosistem));
        _finans = finans ?? throw new ArgumentNullException(nameof(finans));
    }

    public Task BaslatAsync(CancellationToken cancellationToken)
    {
        if (_baslatildi) return Task.CompletedTask;
        _baslatildi = true;
        if (!_ayarlar.SirketYonetimAktif)
        {
            KonsolKayitcisi.Bilgi("Şirket yönetim kapısı kapalı.");
            return Task.CompletedTask;
        }
        _iptal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _dinleyici = new TcpListener(IPAddress.Any, _ayarlar.SirketYonetimPortu);
        _dinleyici.Start(128);
        _gorev = KabulDongusuAsync(_iptal.Token);
        KonsolKayitcisi.Basari($"Şirket yönetim merkezi V7 yayında | Port: {_ayarlar.SirketYonetimPortu}");
        return Task.CompletedTask;
    }

    private async Task KabulDongusuAsync(CancellationToken cancellationToken)
    {
        if (_dinleyici is null) return;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                TcpClient istemci = await _dinleyici.AcceptTcpClientAsync(cancellationToken);
                _ = IstemciyiYonetAsync(istemci, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                KonsolKayitcisi.Uyari($"8090 istemci kabul hatası: {e.Message}");
                try { await Task.Delay(150, cancellationToken); } catch { break; }
            }
        }
    }

    private async Task IstemciyiYonetAsync(TcpClient istemci, CancellationToken cancellationToken)
    {
        using (istemci)
        {
            istemci.NoDelay = true;
            try
            {
                await using NetworkStream akis = istemci.GetStream();
                HttpIstegi? istek = await HttpOkuAsync(akis, cancellationToken);
                if (istek is null)
                {
                    await MetinCevabiAsync(akis, 400, "Geçersiz istek.", cancellationToken);
                    return;
                }
                await YonlendirAsync(akis, istek, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (Exception e) { KonsolKayitcisi.Uyari($"8090 istemci hatası: {e.Message}"); }
        }
    }

    private async Task YonlendirAsync(NetworkStream akis, HttpIstegi istek, CancellationToken cancellationToken)
    {
        string yol = istek.Yol.Split('?', 2)[0];
        if (istek.Metot == "GET" && yol is "/" or "/index.html")
        {
            await CevapAsync(akis, 200, "text/html; charset=utf-8", SirketYonetimHtml.Icerik, null, cancellationToken);
            return;
        }
        if (istek.Metot == "GET" && yol == "/favicon.ico")
        {
            await CevapAsync(akis, 204, "image/x-icon", string.Empty, null, cancellationToken);
            return;
        }
        if (istek.Metot == "GET" && yol == "/api/saglik")
        {
            await JsonCevabiAsync(akis, 200, new
            {
                durum = "calisiyor",
                surum = "yonetim-v7",
                port = _ayarlar.SirketYonetimPortu,
                aktifOturum = _oturumlar.Count,
                fiyatPazariTicki = PazarFiyatDeposu.Getir().SonTick,
                finansTicki = FinansV7Deposu.Getir().SonTick
            }, null, cancellationToken);
            return;
        }

        if (istek.Metot == "POST" && yol == "/api/giris")
        {
            GirisIstegi? dto = JsonOku<GirisIstegi>(istek.Govde);
            if (dto is null)
            {
                await JsonCevabiAsync(akis, 400, IslemSonucu.Hata("Giriş JSON'u geçersiz."), null, cancellationToken);
                return;
            }
            IslemSonucu sonuc = await _isletim.GirisDogrulaAsync(dto, cancellationToken);
            if (!sonuc.Basarili)
            {
                await JsonCevabiAsync(akis, 401, sonuc, null, cancellationToken);
                return;
            }
            string kimlik = SirketKimliginiOku(sonuc.Veri);
            if (string.IsNullOrWhiteSpace(kimlik))
            {
                await JsonCevabiAsync(akis, 500, IslemSonucu.Hata("Oturum şirketi çözülemedi."), null, cancellationToken);
                return;
            }
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _oturumlar[token] = new OturumKaydi
            {
                SirketKimligi = kimlik,
                SonKullanmaZamani = DateTimeOffset.UtcNow.AddMinutes(OturumDakikasi())
            };
            await JsonCevabiAsync(akis, 200, sonuc, new()
            {
                ["Set-Cookie"] = $"sirketOturumu={token}; Path=/; HttpOnly; SameSite=Strict"
            }, cancellationToken);
            return;
        }

        OturumKaydi? oturum = OturumuGetir(istek.Basliklar);
        if (oturum is null)
        {
            await JsonCevabiAsync(akis, 401,
                IslemSonucu.Hata("Oturum bulunamadı. Yeniden giriş yapın."),
                new() { ["Set-Cookie"] = "sirketOturumu=; Path=/; Max-Age=0; HttpOnly; SameSite=Strict" },
                cancellationToken);
            return;
        }
        oturum.SonKullanmaZamani = DateTimeOffset.UtcNow.AddMinutes(OturumDakikasi());

        if (istek.Metot == "POST" && yol == "/api/cikis")
        {
            string? token = CookieDegeri(istek.Basliklar, "sirketOturumu");
            if (!string.IsNullOrWhiteSpace(token)) _oturumlar.TryRemove(token, out _);
            await JsonCevabiAsync(akis, 200, IslemSonucu.Basari("Çıkış yapıldı."),
                new() { ["Set-Cookie"] = "sirketOturumu=; Path=/; Max-Age=0; HttpOnly; SameSite=Strict" },
                cancellationToken);
            return;
        }

        if (istek.Metot == "GET" && yol == "/api/durum")
        {
            await DurumCevabiAsync(akis, oturum.SirketKimligi, cancellationToken);
            return;
        }

        if (istek.Metot != "POST")
        {
            await MetinCevabiAsync(akis, 405, "Bu uç POST bekliyor.", cancellationToken);
            return;
        }

        IslemSonucu sonucIslem;
        if (yol == "/api/sozlesme/kabul")
        {
            SozlesmeKabulIstegi? dto = JsonOku<SozlesmeKabulIstegi>(istek.Govde);
            if (dto is null) sonucIslem = IslemSonucu.Hata("SLA kabul isteği geçersiz.");
            else if (!_finans.SlaKabulEdilebilirMi(oturum.SirketKimligi, dto.TeklifKimligi, out string neden))
                sonucIslem = IslemSonucu.Hata(neden);
            else sonucIslem = await _isletim.SozlesmeKabulEtAsync(oturum.SirketKimligi, dto, cancellationToken);
        }
        else
        {
            sonucIslem = yol switch
            {
                "/api/parola" => await CalistirAsync<ParolaDegistirIstegi>(istek, x => _isletim.ParolaDegistirAsync(oturum.SirketKimligi, x, cancellationToken)),
                "/api/yatirim" => await CalistirAsync<YatirimIstegi>(istek, x => _isletim.YatirimSatinAlAsync(oturum.SirketKimligi, x, cancellationToken)),
                "/api/kredi" => await CalistirAsync<KrediIstegi>(istek, x => _isletim.KrediCekAsync(oturum.SirketKimligi, x, cancellationToken)),
                "/api/hizmet/fiyat" => await CalistirAsync<FiyatGuncelleIstegi>(istek, x => _isletim.HizmetFiyatiGuncelleAsync(oturum.SirketKimligi, x, cancellationToken)),
                "/api/hizmet/durum" => await CalistirAsync<HizmetYayinDurumuIstegi>(istek, x => _isletim.HizmetYayinDurumuGuncelleAsync(oturum.SirketKimligi, x, cancellationToken)),
                "/api/hizmet/kapasite" => await CalistirAsync<HizmetKapasiteIstegi>(istek, x => _isletim.HizmetKapasitesiArtirAsync(oturum.SirketKimligi, x, cancellationToken)),
                "/api/uygulama/yayinla" => await CalistirAsync<UygulamaYayinlaIstegi>(istek, x => _isletim.UygulamaYayinlaAsync(oturum.SirketKimligi, x, cancellationToken)),
                "/api/uygulama/guncelle" => await CalistirAsync<UrunGuncelleIstegi>(istek, x => _isletim.UygulamaGuncelleAsync(oturum.SirketKimligi, x, cancellationToken)),
                "/api/uygulama/kapasite" => await CalistirAsync<UrunKapasiteIstegi>(istek, x => _isletim.UygulamaKapasitesiArtirAsync(oturum.SirketKimligi, x, cancellationToken)),
                "/api/uygulama/dagitim" => await CalistirAsync<UrunDagitimGuncelleIstegi>(istek, x => _ekosistem.DagitimGuncelleAsync(oturum.SirketKimligi, x, cancellationToken)),
                "/api/protokol/yayinla" => await CalistirAsync<ProtokolYayinlaIstegi>(istek, x => _isletim.ProtokolYayinlaAsync(oturum.SirketKimligi, x, cancellationToken)),
                "/api/protokol/benimse" => await CalistirAsync<ProtokolBenimseIstegi>(istek, x => _isletim.ProtokolBenimseAsync(oturum.SirketKimligi, x, cancellationToken)),
                _ => IslemSonucu.Hata("Uç nokta bulunamadı.")
            };
        }
        await JsonCevabiAsync(akis, sonucIslem.Basarili ? 200 : 400, sonucIslem, null, cancellationToken);
    }

    private async Task DurumCevabiAsync(NetworkStream akis, string sirketKimligi, CancellationToken cancellationToken)
    {
        string temel = await _isletim.PanelJsonuOlusturAsync(sirketKimligi, cancellationToken);
        string zengin = await _ekosistem.PanelJsonunuZenginlestirAsync(sirketKimligi, temel, cancellationToken);
        JsonObject kok = JsonNode.Parse(zengin) as JsonObject ?? new();
        PazarFiyatDosyasi pazar = PazarFiyatDeposu.Getir();
        HashSet<string> sirketPazarlari = pazar.Urunler
            .Where(x => string.Equals(x.SirketKimligi, sirketKimligi, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.PazarKimligi)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        kok["fiyatPazari"] = JsonSerializer.SerializeToNode(new
        {
            pazar.SonTick,
            urunler = pazar.Urunler.Where(x => string.Equals(x.SirketKimligi, sirketKimligi, StringComparison.OrdinalIgnoreCase)),
            kategoriler = pazar.Kategoriler.Where(x => sirketPazarlari.Contains(x.PazarKimligi)),
            kural = "Fiyat serbesttir; talep serbest değildir. Aşırı fiyat kullanıcıyı rakibe kaçırır ve geliri chargeback ile geri alır."
        }, JsonAyarlari);

        FinansV7SirketKaydi finans = FinansV7Deposu.SirketGetir(sirketKimligi);
        V6PanoDurumu v6 = V6PanoDeposu.Getir();
        IEnumerable<object> haberler = FinansV7Deposu.Getir().Haberler
            .Where(x => string.IsNullOrWhiteSpace(x.SirketKimligi) || x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.TickNumarasi).Take(40)
            .Cast<object>()
            .Concat(v6.Haberler.Where(x => string.IsNullOrWhiteSpace(x.SirketKimligi) || x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.TickNumarasi).Take(40));

        kok["muhasebeV7"] = JsonSerializer.SerializeToNode(new
        {
            sonTick = finans.Tickler.LastOrDefault(),
            tickler = finans.Tickler.TakeLast(120),
            toplamKurtarmaKredisi = finans.KurtarmaKredisiSayisi
        }, JsonAyarlari);
        kok["bankaV7"] = JsonSerializer.SerializeToNode(_finans.BankaOzetiGetir(sirketKimligi), JsonAyarlari);
        kok["slaUygunluklari"] = JsonSerializer.SerializeToNode(_finans.SlaUygunluklariniGetir(sirketKimligi), JsonAyarlari);
        kok["haberBulteni"] = JsonSerializer.SerializeToNode(haberler, JsonAyarlari);
        kok["v6Sirket"] = JsonSerializer.SerializeToNode(v6.Sirketler.FirstOrDefault(x => x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase)), JsonAyarlari);
        await CevapAsync(akis, 200, "application/json; charset=utf-8", kok.ToJsonString(JsonAyarlari), null, cancellationToken);
    }

    private OturumKaydi? OturumuGetir(IReadOnlyDictionary<string, string> basliklar)
    {
        string? token = CookieDegeri(basliklar, "sirketOturumu");
        if (string.IsNullOrWhiteSpace(token) || !_oturumlar.TryGetValue(token, out OturumKaydi? kayit)) return null;
        if (kayit.SonKullanmaZamani <= DateTimeOffset.UtcNow)
        {
            _oturumlar.TryRemove(token, out _);
            return null;
        }
        return kayit;
    }

    private int OturumDakikasi() => Math.Clamp(_ayarlar.SirketYonetimOturumDakika, 10, 1_440);

    private static async Task<IslemSonucu> CalistirAsync<T>(HttpIstegi istek, Func<T, Task<IslemSonucu>> islem) where T : class
    {
        T? dto = JsonOku<T>(istek.Govde);
        return dto is null ? IslemSonucu.Hata("İstek JSON'u geçersiz.") : await islem(dto);
    }

    private static T? JsonOku<T>(string json) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonAyarlari); }
        catch { return null; }
    }

    private static string SirketKimliginiOku(object? veri)
    {
        try { return JsonSerializer.SerializeToNode(veri, JsonAyarlari)?["sirketKimligi"]?.GetValue<string>() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string? CookieDegeri(IReadOnlyDictionary<string, string> basliklar, string ad)
    {
        if (!basliklar.TryGetValue("Cookie", out string? cookie)) return null;
        foreach (string parca in cookie.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] ikili = parca.Trim().Split('=', 2);
            if (ikili.Length == 2 && string.Equals(ikili[0], ad, StringComparison.Ordinal)) return ikili[1];
        }
        return null;
    }

    private static async Task<HttpIstegi?> HttpOkuAsync(NetworkStream akis, CancellationToken cancellationToken)
    {
        const int azami = 256 * 1024;
        byte[] tampon = new byte[4096];
        using MemoryStream bellek = new();
        int baslikSonu = -1;
        while (bellek.Length < azami)
        {
            int okunan = await akis.ReadAsync(tampon, cancellationToken);
            if (okunan <= 0) break;
            bellek.Write(tampon, 0, okunan);
            baslikSonu = BaslikSonunuBul(bellek.GetBuffer(), (int)bellek.Length);
            if (baslikSonu >= 0) break;
        }
        if (baslikSonu < 0) return null;
        byte[] tum = bellek.ToArray();
        string baslikMetni = Encoding.ASCII.GetString(tum, 0, baslikSonu);
        string[] satirlar = baslikMetni.Split("\r\n", StringSplitOptions.None);
        string[] ilk = satirlar[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (ilk.Length < 2) return null;
        Dictionary<string, string> basliklar = new(StringComparer.OrdinalIgnoreCase);
        foreach (string satir in satirlar.Skip(1))
        {
            int ikiNokta = satir.IndexOf(':');
            if (ikiNokta <= 0) continue;
            basliklar[satir[..ikiNokta].Trim()] = satir[(ikiNokta + 1)..].Trim();
        }
        int uzunluk = 0;
        if (basliklar.TryGetValue("Content-Length", out string? u) &&
            (!int.TryParse(u, out uzunluk) || uzunluk < 0 || uzunluk > azami)) return null;
        int govdeBaslangici = baslikSonu + 4;
        while (bellek.Length - govdeBaslangici < uzunluk && bellek.Length < azami)
        {
            int okunan = await akis.ReadAsync(tampon, cancellationToken);
            if (okunan <= 0) break;
            bellek.Write(tampon, 0, okunan);
        }
        tum = bellek.ToArray();
        if (tum.Length - govdeBaslangici < uzunluk) return null;
        string govde = uzunluk == 0 ? string.Empty : Encoding.UTF8.GetString(tum, govdeBaslangici, uzunluk);
        return new HttpIstegi(ilk[0].ToUpperInvariant(), ilk[1], basliklar, govde);
    }

    private static int BaslikSonunuBul(byte[] veri, int uzunluk)
    {
        for (int i = 0; i <= uzunluk - 4; i++)
            if (veri[i] == 13 && veri[i + 1] == 10 && veri[i + 2] == 13 && veri[i + 3] == 10) return i;
        return -1;
    }

    private static Task MetinCevabiAsync(NetworkStream akis, int kod, string metin, CancellationToken ct) =>
        CevapAsync(akis, kod, "text/plain; charset=utf-8", metin, null, ct);

    private static Task JsonCevabiAsync(NetworkStream akis, int kod, object veri, Dictionary<string, string>? ek, CancellationToken ct) =>
        CevapAsync(akis, kod, "application/json; charset=utf-8", JsonSerializer.Serialize(veri, JsonAyarlari), ek, ct);

    private static async Task CevapAsync(NetworkStream akis, int kod, string tur, string govde, Dictionary<string, string>? ek, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(govde);
        string durum = kod switch
        {
            200 => "OK", 204 => "No Content", 400 => "Bad Request", 401 => "Unauthorized",
            405 => "Method Not Allowed", 500 => "Internal Server Error", _ => "Error"
        };
        StringBuilder b = new();
        b.Append($"HTTP/1.1 {kod} {durum}\r\nContent-Type: {tur}\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-store, no-cache, must-revalidate\r\nPragma: no-cache\r\nX-Content-Type-Options: nosniff\r\nX-Frame-Options: DENY\r\nConnection: close\r\n");
        if (ek is not null) foreach ((string ad, string deger) in ek) b.Append(ad).Append(": ").Append(deger).Append("\r\n");
        b.Append("\r\n");
        await akis.WriteAsync(Encoding.ASCII.GetBytes(b.ToString()), cancellationToken);
        if (bytes.Length > 0) await akis.WriteAsync(bytes, cancellationToken);
        await akis.FlushAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _iptal?.Cancel();
            _dinleyici?.Stop();
            if (_gorev is not null) await _gorev;
        }
        catch (OperationCanceledException) { }
        finally { _iptal?.Dispose(); }
    }

    private sealed class OturumKaydi
    {
        public string SirketKimligi { get; init; } = string.Empty;
        public DateTimeOffset SonKullanmaZamani { get; set; }
    }

    private sealed record HttpIstegi(
        string Metot,
        string Yol,
        IReadOnlyDictionary<string, string> Basliklar,
        string Govde);
}
