using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SirketMotoru.Ayarlar;
using SirketMotoru.Hizmetler;
using SirketMotoru.Kayit;
using SirketMotoru.Sirketler;

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
    private readonly SirketYoneticisi _sirketler;
    private readonly HizmetKatalogu _katalog;
    private readonly KodTabanliSirketIsletimYoneticisi _isletim;
    private readonly EkosistemYoneticisi _ekosistem;
    private readonly V9EkonomiYoneticisi _v9;
    private readonly ConcurrentDictionary<string, OturumKaydi> _oturumlar = new(StringComparer.Ordinal);
    private TcpListener? _dinleyici;
    private CancellationTokenSource? _iptal;
    private Task? _gorev;
    private bool _baslatildi;
    private bool _disposed;

    public SirketYonetimSunucusu(
        MotorAyarlari ayarlar,
        SirketYoneticisi sirketler,
        HizmetKatalogu katalog,
        KodTabanliSirketIsletimYoneticisi isletim,
        EkosistemYoneticisi ekosistem,
        V9EkonomiYoneticisi v9)
    {
        _ayarlar = ayarlar ?? throw new ArgumentNullException(nameof(ayarlar));
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _katalog = katalog ?? throw new ArgumentNullException(nameof(katalog));
        _isletim = isletim ?? throw new ArgumentNullException(nameof(isletim));
        _ekosistem = ekosistem ?? throw new ArgumentNullException(nameof(ekosistem));
        _v9 = v9 ?? throw new ArgumentNullException(nameof(v9));
    }

    public Task BaslatAsync(CancellationToken cancellationToken)
    {
        if (_baslatildi) return Task.CompletedTask;
        _baslatildi = true;
        if (!_ayarlar.SirketYonetimAktif) return Task.CompletedTask;
        _iptal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _dinleyici = new TcpListener(IPAddress.Any, _ayarlar.SirketYonetimPortu);
        _dinleyici.Start(128);
        _gorev = KabulAsync(_iptal.Token);
        KonsolKayitcisi.Basari($"Şirket yönetim merkezi V9 yayında | Port: {_ayarlar.SirketYonetimPortu}");
        return Task.CompletedTask;
    }

    private async Task KabulAsync(CancellationToken ct)
    {
        if (_dinleyici is null) return;
        while (!ct.IsCancellationRequested)
        {
            try { _ = IstemciAsync(await _dinleyici.AcceptTcpClientAsync(ct), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested) { break; }
            catch (Exception hata) { KonsolKayitcisi.Uyari($"8090 kabul hatası: {hata.Message}"); }
        }
    }

    private async Task IstemciAsync(TcpClient istemci, CancellationToken ct)
    {
        using (istemci)
        {
            try
            {
                await using NetworkStream akis = istemci.GetStream();
                HttpIstegi? istek = await HttpOkuAsync(akis, ct);
                if (istek is null) { await MetinAsync(akis, 400, "Geçersiz istek.", ct); return; }
                await YonlendirAsync(akis, istek, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (Exception hata) { KonsolKayitcisi.Uyari($"8090 istemci hatası: {hata.Message}"); }
        }
    }

    private async Task YonlendirAsync(NetworkStream akis, HttpIstegi istek, CancellationToken ct)
    {
        string yol = istek.Yol.Split('?', 2)[0];
        if (istek.Metot == "GET" && yol is "/" or "/index.html")
        {
            await CevapAsync(akis, 200, "text/html; charset=utf-8", SirketYonetimHtml.Icerik, null, ct); return;
        }
        if (istek.Metot == "GET" && yol == "/api/saglik")
        {
            await JsonAsync(akis, 200, new { durum = "calisiyor", surum = "yonetim-v9", fiyat = "kategori-sinirli", kapasite = "tek-fiziksel-havuz" }, null, ct); return;
        }
        if (istek.Metot == "GET" && yol == "/favicon.ico") { await CevapAsync(akis, 204, "image/x-icon", "", null, ct); return; }

        if (istek.Metot == "POST" && yol == "/api/giris")
        {
            GirisIstegi? dto = Oku<GirisIstegi>(istek.Govde);
            if (dto is null) { await JsonAsync(akis, 400, IslemSonucu.Hata("Giriş JSON'u geçersiz."), null, ct); return; }
            IslemSonucu sonuc = await _isletim.GirisDogrulaAsync(dto, ct);
            if (!sonuc.Basarili) { await JsonAsync(akis, 401, sonuc, null, ct); return; }
            string sirket = Alan(sonuc.Veri, "sirketKimligi");
            if (string.IsNullOrWhiteSpace(sirket)) { await JsonAsync(akis, 500, IslemSonucu.Hata("Şirket çözülemedi."), null, ct); return; }
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _oturumlar[token] = new OturumKaydi { SirketKimligi = sirket, SonKullanma = DateTimeOffset.UtcNow.AddMinutes(OturumDakikasi()) };
            await JsonAsync(akis, 200, sonuc, new() { ["Set-Cookie"] = $"sirketOturumu={token}; Path=/; HttpOnly; SameSite=Strict" }, ct); return;
        }

        OturumKaydi? oturum = Oturum(istek.Basliklar);
        if (oturum is null)
        {
            await JsonAsync(akis, 401, IslemSonucu.Hata("Oturum bulunamadı."), new() { ["Set-Cookie"] = "sirketOturumu=; Path=/; Max-Age=0; HttpOnly" }, ct); return;
        }
        oturum.SonKullanma = DateTimeOffset.UtcNow.AddMinutes(OturumDakikasi());

        if (istek.Metot == "POST" && yol == "/api/cikis")
        {
            string? token = Cookie(istek.Basliklar, "sirketOturumu");
            if (!string.IsNullOrWhiteSpace(token)) _oturumlar.TryRemove(token, out _);
            await JsonAsync(akis, 200, IslemSonucu.Basari("Çıkış yapıldı."), new() { ["Set-Cookie"] = "sirketOturumu=; Path=/; Max-Age=0; HttpOnly" }, ct); return;
        }
        if (istek.Metot == "GET" && yol == "/api/durum")
        {
            await DurumAsync(akis, oturum.SirketKimligi, ct); return;
        }
        if (istek.Metot != "POST") { await MetinAsync(akis, 405, "POST gerekli.", ct); return; }

        IslemSonucu islem;
        switch (yol)
        {
            case "/api/parola":
                islem = await Calistir<ParolaDegistirIstegi>(istek, x => _isletim.ParolaDegistirAsync(oturum.SirketKimligi, x, ct)); break;
            case "/api/yatirim":
                islem = await Calistir<YatirimIstegi>(istek, x => _v9.YatirimSatinAlAsync(oturum.SirketKimligi, x, ct)); break;
            case "/api/kredi":
                islem = await Calistir<KrediIstegi>(istek, x => _v9.KrediCekAsync(oturum.SirketKimligi, x, ct)); break;
            case "/api/kapasite/tahsis":
                islem = await Calistir<V9KapasiteTahsisIstegi>(istek, x => _v9.KapasiteTahsisEtAsync(oturum.SirketKimligi, x, ct)); break;
            case "/api/hizmet/durum":
                islem = await Calistir<HizmetYayinDurumuIstegi>(istek, x => _isletim.HizmetYayinDurumuGuncelleAsync(oturum.SirketKimligi, x, ct)); break;
            case "/api/hizmet/fiyat":
                islem = IslemSonucu.Hata("Hizmet fiyatları motor tarafından sabittir."); break;
            case "/api/hizmet/kapasite":
            case "/api/uygulama/kapasite":
                islem = IslemSonucu.Hata("Kapasite satın alınamaz; toplam fiziksel havuzdan tahsis edilir."); break;
            case "/api/uygulama/yayinla":
                UygulamaYayinlaIstegi? yayin = Oku<UygulamaYayinlaIstegi>(istek.Govde);
                islem = yayin is null ? IslemSonucu.Hata("Yayın isteği geçersiz.") : await UygulamaYayinlaAsync(oturum.SirketKimligi, yayin, ct); break;
            case "/api/uygulama/guncelle":
                islem = await Calistir<UrunGuncelleIstegi>(istek, x => _v9.UrunFiyatiniGuncelleAsync(oturum.SirketKimligi, x, ct)); break;
            case "/api/uygulama/dagitim":
                islem = await Calistir<UrunDagitimGuncelleIstegi>(istek, x => _ekosistem.DagitimGuncelleAsync(oturum.SirketKimligi, x, ct)); break;
            case "/api/protokol/yayinla":
                islem = await Calistir<ProtokolYayinlaIstegi>(istek, x => _isletim.ProtokolYayinlaAsync(oturum.SirketKimligi, x, ct)); break;
            case "/api/protokol/benimse":
                islem = await Calistir<ProtokolBenimseIstegi>(istek, x => _isletim.ProtokolBenimseAsync(oturum.SirketKimligi, x, ct)); break;
            case "/api/sozlesme/kabul":
                islem = await Calistir<SozlesmeKabulIstegi>(istek, x => _isletim.SozlesmeKabulEtAsync(oturum.SirketKimligi, x, ct)); break;
            default:
                islem = IslemSonucu.Hata("Uç nokta bulunamadı."); break;
        }
        await JsonAsync(akis, islem.Basarili ? 200 : 400, islem, null, ct);
    }

    private async Task<IslemSonucu> UygulamaYayinlaAsync(string sirketKimligi, UygulamaYayinlaIstegi istek, CancellationToken ct)
    {
        SunulanUygulama? manifest = SunucuYayinManifestDeposu.Getir(sirketKimligi).Uygulamalar.FirstOrDefault(x =>
            x.UygulamaKimligi.Equals(istek.UygulamaKimligi?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (manifest is null) return IslemSonucu.Hata("Uygulama şirket manifestinde yok.");
        bool os = manifest.UrunTuru.Equals("isletim-sistemi", StringComparison.OrdinalIgnoreCase);
        if (!os && string.IsNullOrWhiteSpace(istek.IsletimSistemiKimligi)) return IslemSonucu.Hata("İşletim sistemi seçin.");
        if (string.IsNullOrWhiteSpace(istek.BaglantiProtokoluKimligi)) return IslemSonucu.Hata("Protokol seçin.");

        decimal fiyat = V9FiyatPolitikasi.Sinirla(manifest.UrunTuru, manifest.Kategori,
            istek.FiyatlandirmaModeli == "kullanim" ? istek.KullanimBasinaUcret : istek.AbonelikUcreti);
        if (istek.FiyatlandirmaModeli == "kullanim") { istek.KullanimBasinaUcret = fiyat; istek.AbonelikUcreti = 0; }
        else { istek.AbonelikUcreti = fiyat; if (istek.FiyatlandirmaModeli != "freemium") istek.KullanimBasinaUcret = 0; }

        SirketKaydi sirket = _sirketler.SirketKayitlari.First(x => x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
        decimal once = sirket.Kasa;
        IslemSonucu yayin = await _isletim.UygulamaYayinlaAsync(sirketKimligi, istek, ct);
        if (!yayin.Basarili) return yayin;
        string urunKimligi = Alan(yayin.Veri, "urunKimligi");
        if (string.IsNullOrWhiteSpace(urunKimligi)) return IslemSonucu.Hata("Ürün kimliği alınamadı.");
        await _v9.YayinMaliyetiniDengeleAsync(sirketKimligi, urunKimligi, once, ct);
        IslemSonucu dagitim = await _ekosistem.DagitimGuncelleAsync(sirketKimligi, new UrunDagitimGuncelleIstegi
        {
            UrunKimligi = urunKimligi,
            IsletimSistemiKimligi = os ? string.Empty : istek.IsletimSistemiKimligi,
            BaglantiProtokoluKimligi = istek.BaglantiProtokoluKimligi,
            Aktif = istek.AktifOlmasiIsteniyor
        }, ct);
        if (!dagitim.Basarili)
        {
            await _v9.UrunFiyatiniGuncelleAsync(sirketKimligi, new UrunGuncelleIstegi
            {
                UrunKimligi = urunKimligi, AbonelikUcreti = istek.AbonelikUcreti,
                KullanimBasinaUcret = istek.KullanimBasinaUcret, Aktif = false
            }, ct);
            return IslemSonucu.Hata("Ürün kaydedildi fakat dağıtım uyumsuz; pasif bırakıldı: " + dagitim.Aciklama);
        }
        return IslemSonucu.Basari("Ürün OS ve tek kanonik protokolle yayına alındı.", new { urunKimligi, fiyat });
    }

    private async Task DurumAsync(NetworkStream akis, string sirketKimligi, CancellationToken ct)
    {
        MotorHizmetFiyatlari.Uygula(_sirketler.SirketKayitlari);
        string temel = await _isletim.PanelJsonuOlusturAsync(sirketKimligi, ct);
        JsonObject kok = JsonNode.Parse(await _ekosistem.PanelJsonunuZenginlestirAsync(sirketKimligi, temel, ct)) as JsonObject ?? new();
        V9PazarDosyasi pazar = V9PazarDeposu.Getir();
        V9SirketPazarOzeti? ozet = pazar.SirketOzetleri.FirstOrDefault(x => x.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase));
        V9SirketKapasiteDurumu kapasite = _v9.KapasiteOzeti(sirketKimligi);
        kok["v9"] = JsonSerializer.SerializeToNode(new
        {
            pazar.SonTick,
            sirketOzeti = ozet,
            kapasite,
            isletimSistemiTalebi = pazar.IsletimSistemiTalebi,
            uygulamaTalepleri = pazar.UygulamaTalepleri.Values.OrderByDescending(x => x.BuTickTalep),
            hizmetTalepleri = pazar.HizmetTalepleri.Values.OrderByDescending(x => x.BuTickTalep),
            haberler = pazar.Haberler.OrderByDescending(x => x.TickNumarasi).Take(80),
            fiyatKurali = "Uygulama ve OS fiyatları kategori min-max aralığında; hizmet fiyatları sabit.",
            kapasiteKurali = "Kapasite satın alınmaz; fiziksel havuz tahsis edilir."
        }, JsonAyarlari);
        kok["yatirimMagazasi"] = JsonSerializer.SerializeToNode(_v9.YatirimMagazasi(sirketKimligi), JsonAyarlari);
        kok["krediPaketleri"] = JsonSerializer.SerializeToNode(_v9.KrediPaketleri(), JsonAyarlari);
        kok["protokolKataloguV9"] = JsonSerializer.SerializeToNode(Protokoller(sirketKimligi, kok), JsonAyarlari);

        JsonArray urunler = kok["isletim"]?["urunler"] as JsonArray ?? [];
        JsonObject araliklar = new();
        foreach (JsonObject urun in urunler.OfType<JsonObject>())
        {
            string kimlik = Str(urun["urunKimligi"]);
            V9FiyatAraligi a = V9FiyatPolitikasi.Aralik(Str(urun["urunTuru"]), Str(urun["kategori"]));
            araliklar[kimlik] = JsonSerializer.SerializeToNode(new { a.Min, a.Max, a.Adim, a.Onerilen });
        }
        foreach (SunulanUygulama uygulama in SunucuYayinManifestDeposu.Getir(sirketKimligi).Uygulamalar)
        {
            V9FiyatAraligi a = V9FiyatPolitikasi.Aralik(uygulama.UrunTuru, uygulama.Kategori);
            araliklar[$"manifest:{uygulama.UygulamaKimligi}"] = JsonSerializer.SerializeToNode(new { a.Min, a.Max, a.Adim, a.Onerilen });
        }
        kok["fiyatAraliklariV9"] = araliklar;
        await CevapAsync(akis, 200, "application/json; charset=utf-8", kok.ToJsonString(JsonAyarlari), null, ct);
    }

    private IReadOnlyList<object> Protokoller(string sirketKimligi, JsonObject panel)
    {
        List<OzelProtokolKaydi> piyasa;
        try { piyasa = panel["protokoller"]?.Deserialize<List<OzelProtokolKaydi>>(JsonAyarlari) ?? []; }
        catch { piyasa = []; }
        List<object> sonuc = [];
        HashSet<string> eslesen = new(StringComparer.OrdinalIgnoreCase);
        foreach (SunucuYayinManifesti manifest in SunucuYayinManifestDeposu.TumunuGetir())
        {
            string sahipAdi = _sirketler.SirketKayitlari.FirstOrDefault(x => x.SirketKimligi.Equals(manifest.SirketKimligi, StringComparison.OrdinalIgnoreCase))?.SirketAdi ?? manifest.SirketKimligi;
            foreach (SunulanOzelProtokol teknik in manifest.OzelProtokoller)
            {
                OzelProtokolKaydi? kayit = piyasa.FirstOrDefault(x => x.SahipSirketKimligi.Equals(manifest.SirketKimligi, StringComparison.OrdinalIgnoreCase) && x.ProtokolAdi.Equals(teknik.ProtokolAdi, StringComparison.OrdinalIgnoreCase) && x.Surum.Equals(teknik.Surum, StringComparison.OrdinalIgnoreCase));
                if (kayit is not null) eslesen.Add(kayit.ProtokolKimligi);
                bool sahibi = manifest.SirketKimligi.Equals(sirketKimligi, StringComparison.OrdinalIgnoreCase);
                bool benimsendi = sahibi || kayit?.BenimseyenSirketler.Contains(sirketKimligi, StringComparer.OrdinalIgnoreCase) == true;
                bool acik = kayit?.LisansModeli.Equals("acik", StringComparison.OrdinalIgnoreCase) == true;
                sonuc.Add(new
                {
                    protokolKimligi = teknik.ProtokolKimligi, piyasaKaydiKimligi = kayit?.ProtokolKimligi ?? "",
                    teknik.ProtokolAdi, teknik.Surum, teknik.Aciklama, sahipSirketKimligi = manifest.SirketKimligi,
                    sahipSirketAdi = sahipAdi, aktif = kayit?.Aktif ?? false, sahibi, benimsendi,
                    kullanilabilir = kayit?.Aktif == true && (sahibi || benimsendi || acik),
                    lisansModeli = kayit?.LisansModeli ?? "yayinlanmadi", benimsemeBedeli = kayit?.BenimsemeBedeli ?? 0,
                    tickLisansBedeli = kayit?.TickLisansBedeli ?? 0
                });
            }
        }
        return sonuc;
    }

    private OturumKaydi? Oturum(IReadOnlyDictionary<string, string> basliklar)
    {
        string? token = Cookie(basliklar, "sirketOturumu");
        if (string.IsNullOrWhiteSpace(token) || !_oturumlar.TryGetValue(token, out OturumKaydi? o)) return null;
        if (o.SonKullanma <= DateTimeOffset.UtcNow) { _oturumlar.TryRemove(token, out _); return null; }
        return o;
    }

    private int OturumDakikasi() => Math.Clamp(_ayarlar.SirketYonetimOturumDakika, 10, 1440);
    private static async Task<IslemSonucu> Calistir<T>(HttpIstegi istek, Func<T, Task<IslemSonucu>> islem) where T : class
    {
        T? dto = Oku<T>(istek.Govde); return dto is null ? IslemSonucu.Hata("İstek JSON'u geçersiz.") : await islem(dto);
    }
    private static T? Oku<T>(string json) where T : class { try { return JsonSerializer.Deserialize<T>(json, JsonAyarlari); } catch { return null; } }
    private static string Alan(object? veri, string alan) { try { return JsonSerializer.SerializeToNode(veri, JsonAyarlari)?[alan]?.GetValue<string>() ?? ""; } catch { return ""; } }
    private static string Str(JsonNode? n) { try { return n?.GetValue<string>() ?? ""; } catch { return ""; } }
    private static string? Cookie(IReadOnlyDictionary<string, string> basliklar, string ad)
    {
        if (!basliklar.TryGetValue("Cookie", out string? c)) return null;
        foreach (string p in c.Split(';', StringSplitOptions.RemoveEmptyEntries)) { string[] x = p.Trim().Split('=', 2); if (x.Length == 2 && x[0] == ad) return x[1]; }
        return null;
    }

    private static async Task<HttpIstegi?> HttpOkuAsync(NetworkStream akis, CancellationToken ct)
    {
        const int azami = 256 * 1024;
        byte[] tampon = new byte[4096]; using MemoryStream bellek = new(); int son = -1;
        while (bellek.Length < azami)
        {
            int n = await akis.ReadAsync(tampon, ct); if (n <= 0) break; bellek.Write(tampon, 0, n);
            son = BaslikSonu(bellek.GetBuffer(), (int)bellek.Length); if (son >= 0) break;
        }
        if (son < 0) return null;
        byte[] tum = bellek.ToArray(); string[] satir = Encoding.ASCII.GetString(tum, 0, son).Split("\r\n");
        string[] ilk = satir[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries); if (ilk.Length < 2) return null;
        Dictionary<string, string> basliklar = new(StringComparer.OrdinalIgnoreCase);
        foreach (string s in satir.Skip(1)) { int i = s.IndexOf(':'); if (i > 0) basliklar[s[..i].Trim()] = s[(i + 1)..].Trim(); }
        int uzunluk = basliklar.TryGetValue("Content-Length", out string? u) && int.TryParse(u, out int l) ? l : 0;
        if (uzunluk < 0 || uzunluk > azami) return null;
        int bas = son + 4;
        while (bellek.Length - bas < uzunluk) { int n = await akis.ReadAsync(tampon, ct); if (n <= 0) break; bellek.Write(tampon, 0, n); }
        tum = bellek.ToArray(); if (tum.Length - bas < uzunluk) return null;
        return new HttpIstegi(ilk[0].ToUpperInvariant(), ilk[1], basliklar, uzunluk == 0 ? "" : Encoding.UTF8.GetString(tum, bas, uzunluk));
    }
    private static int BaslikSonu(byte[] b, int n) { for (int i = 0; i <= n - 4; i++) if (b[i] == 13 && b[i + 1] == 10 && b[i + 2] == 13 && b[i + 3] == 10) return i; return -1; }
    private static Task MetinAsync(NetworkStream a, int k, string m, CancellationToken ct) => CevapAsync(a, k, "text/plain; charset=utf-8", m, null, ct);
    private static Task JsonAsync(NetworkStream a, int k, object v, Dictionary<string, string>? e, CancellationToken ct) => CevapAsync(a, k, "application/json; charset=utf-8", JsonSerializer.Serialize(v, JsonAyarlari), e, ct);
    private static async Task CevapAsync(NetworkStream akis, int kod, string tur, string govde, Dictionary<string, string>? ek, CancellationToken ct)
    {
        byte[] b = Encoding.UTF8.GetBytes(govde); string durum = kod switch { 200 => "OK", 204 => "No Content", 400 => "Bad Request", 401 => "Unauthorized", 405 => "Method Not Allowed", 500 => "Internal Server Error", _ => "Error" };
        StringBuilder h = new($"HTTP/1.1 {kod} {durum}\r\nContent-Type: {tur}\r\nContent-Length: {b.Length}\r\nCache-Control: no-store\r\nX-Frame-Options: DENY\r\nConnection: close\r\n");
        if (ek is not null) foreach ((string a, string d) in ek) h.Append(a).Append(": ").Append(d).Append("\r\n"); h.Append("\r\n");
        await akis.WriteAsync(Encoding.ASCII.GetBytes(h.ToString()), ct); if (b.Length > 0) await akis.WriteAsync(b, ct); await akis.FlushAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return; _disposed = true;
        try { _iptal?.Cancel(); _dinleyici?.Stop(); if (_gorev is not null) await _gorev; }
        catch (OperationCanceledException) { }
        finally { _iptal?.Dispose(); }
    }

    private sealed class OturumKaydi { public string SirketKimligi { get; init; } = ""; public DateTimeOffset SonKullanma { get; set; } }
    private sealed record HttpIstegi(string Metot, string Yol, IReadOnlyDictionary<string, string> Basliklar, string Govde);
}
