using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SirketMotoru.Ayarlar;
using SirketMotoru.Hizmetler;
using SirketMotoru.Isler;
using SirketMotoru.Isletim;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Sirketler;

namespace SirketMotoru.CanliPano;

public sealed class YazilimBorsasiSunucusu : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly MotorAyarlari _ayarlar;
    private readonly SirketYoneticisi _sirketler;
    private readonly MusteriYoneticisi _musteriler;
    private readonly HizmetKatalogu _katalog;
    private readonly KodTabanliSirketIsletimYoneticisi _isletim;
    private readonly FinansV7Yoneticisi _finans;
    private readonly Func<long> _tickGetir;
    private readonly DateTimeOffset _baslangic = DateTimeOffset.UtcNow;
    private TcpListener? _dinleyici;
    private CancellationTokenSource? _iptal;
    private Task? _gorev;
    private bool _baslatildi;
    private bool _disposed;

    public YazilimBorsasiSunucusu(
        MotorAyarlari ayarlar,
        SirketYoneticisi sirketler,
        MusteriYoneticisi musteriler,
        HizmetKatalogu katalog,
        KodTabanliSirketIsletimYoneticisi isletim,
        FinansV7Yoneticisi finans,
        Func<long> tickGetir)
    {
        _ayarlar = ayarlar ?? throw new ArgumentNullException(nameof(ayarlar));
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _musteriler = musteriler ?? throw new ArgumentNullException(nameof(musteriler));
        _katalog = katalog ?? throw new ArgumentNullException(nameof(katalog));
        _isletim = isletim ?? throw new ArgumentNullException(nameof(isletim));
        _finans = finans ?? throw new ArgumentNullException(nameof(finans));
        _tickGetir = tickGetir ?? throw new ArgumentNullException(nameof(tickGetir));
    }

    public Task BaslatAsync(CancellationToken cancellationToken)
    {
        if (_baslatildi) return Task.CompletedTask;
        _baslatildi = true;
        if (!_ayarlar.CanliPanoAktif) return Task.CompletedTask;
        _iptal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _dinleyici = new TcpListener(IPAddress.Any, _ayarlar.CanliPanoPortu);
        _dinleyici.Start(128);
        _gorev = KabulDongusuAsync(_iptal.Token);
        KonsolKayitcisi.Basari($"Yazılım borsası V8 yayında | Port: {_ayarlar.CanliPanoPortu}");
        foreach (string adres in YayinAdresleri()) KonsolKayitcisi.Bilgi($"Yazılım borsası adresi: {adres}");
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
                KonsolKayitcisi.Uyari($"Borsa istemci kabul hatası: {e.Message}");
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
                using StreamReader okuyucu = new(akis, Encoding.ASCII, false, 4096, true);
                string? ilk = await okuyucu.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(ilk) || ilk.Length > 8192)
                {
                    await CevapAsync(akis, 400, "Bad Request", "text/plain; charset=utf-8", "Geçersiz istek.", cancellationToken);
                    return;
                }
                for (int i = 0; i < 100; i++)
                {
                    string? h = await okuyucu.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrEmpty(h)) break;
                }
                string[] parcalar = ilk.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (parcalar.Length != 3 || !string.Equals(parcalar[0], "GET", StringComparison.OrdinalIgnoreCase))
                {
                    await CevapAsync(akis, 405, "Method Not Allowed", "text/plain; charset=utf-8", "Yalnız GET desteklenir.", cancellationToken);
                    return;
                }
                string yol = parcalar[1].Split('?', 2)[0];
                switch (yol)
                {
                    case "/": case "/index.html":
                        await CevapAsync(akis, 200, "OK", "text/html; charset=utf-8", YazilimBorsasiHtml.Icerik, cancellationToken); break;
                    case "/api/durum":
                        await CevapAsync(akis, 200, "OK", "application/json; charset=utf-8", await DurumJsonuOlusturAsync(cancellationToken), cancellationToken); break;
                    case "/api/saglik":
                        await CevapAsync(akis, 200, "OK", "application/json; charset=utf-8", JsonSerializer.Serialize(new { durum = "calisiyor", tickNumarasi = _tickGetir(), surum = "borsa-v8" }, JsonAyarlari), cancellationToken); break;
                    case "/favicon.ico":
                        await CevapAsync(akis, 204, "No Content", "image/x-icon", string.Empty, cancellationToken); break;
                    default:
                        await CevapAsync(akis, 404, "Not Found", "text/plain; charset=utf-8", "Sayfa bulunamadı.", cancellationToken); break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (Exception e) { KonsolKayitcisi.Uyari($"Borsa istemci hatası: {e.Message}"); }
        }
    }

    private async Task<string> DurumJsonuOlusturAsync(CancellationToken cancellationToken)
    {
        long tick = _tickGetir();
        PanoPiyasaDurumu piyasa = CanliPanoDurumDeposu.Getir();
        V6PanoDurumu v6 = V6PanoDeposu.Getir();
        FinansV7Dosyasi finans = FinansV7Deposu.Getir();
        IReadOnlyList<SirketMotoru.Ag.SirketBaglantisi> baglantilar = _sirketler.BaglantilariGetir();
        Dictionary<string, SirketMotoru.Ag.SirketBaglantisi> baglantiIndeksi = baglantilar.ToDictionary(x => x.Kayit.SirketKimligi, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, SirketBaglantiAyari> ayarlar = _ayarlar.Sirketler.ToDictionary(x => x.SirketKimligi, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IsletimSistemiPazarKaydi> sistemIndeksi = IsletimSistemiPazarDeposu.Getir().ToDictionary(x => x.UygulamaKimligi, StringComparer.OrdinalIgnoreCase);

        JsonArray borsaSirketleri = [], uygulamaPiyasasi = [], protokolPiyasasi = [], krediPiyasasi = [], sozlesmePiyasasi = [], yatirimPiyasasi = [];
        foreach (SirketKaydi kayit in _sirketler.SirketKayitlari)
        {
            cancellationToken.ThrowIfCancellationRequested();
            baglantiIndeksi.TryGetValue(kayit.SirketKimligi, out SirketMotoru.Ag.SirketBaglantisi? baglanti);
            ayarlar.TryGetValue(kayit.SirketKimligi, out SirketBaglantiAyari? ayar);
            JsonObject panel;
            try { panel = JsonNode.Parse(await _isletim.PanelJsonuOlusturAsync(kayit.SirketKimligi, cancellationToken)) as JsonObject ?? new(); }
            catch (Exception e) { panel = new JsonObject { ["borsaOkumaHatasi"] = e.Message }; }
            JsonObject isletim = panel["isletim"] as JsonObject ?? new();
            JsonObject kod = panel["kodTabanliYayinlar"] as JsonObject ?? new();
            JsonArray urunler = isletim["urunler"] as JsonArray ?? [], krediler = isletim["krediler"] as JsonArray ?? [], sozlesmeler = isletim["sozlesmeler"] as JsonArray ?? [];
            JsonObject yatirimlar = isletim["yatirimSeviyeleri"] as JsonObject ?? new();
            JsonArray teknikUygulamalar = kod["uygulamalar"] as JsonArray ?? [], teknikProtokoller = kod["protokoller"] as JsonArray ?? [];
            FinansV7SirketKaydi finansSirket = finans.Sirketler.FirstOrDefault(x => x.SirketKimligi.Equals(kayit.SirketKimligi, StringComparison.OrdinalIgnoreCase)) ?? new();
            FinansV7TickKaydi? sonFinans = finansSirket.Tickler.LastOrDefault();
            V6SirketDurumu? v6Sirket = v6.Sirketler.FirstOrDefault(x => x.SirketKimligi.Equals(kayit.SirketKimligi, StringComparison.OrdinalIgnoreCase));
            FinansV7BankaOzeti banka = _finans.BankaOzetiGetir(kayit.SirketKimligi);
            CezaV8SirketOzeti ceza = CezaV8Deposu.SirketOzeti(kayit);

            borsaSirketleri.Add(new JsonObject
            {
                ["sirketKimligi"] = kayit.SirketKimligi, ["sirketAdi"] = kayit.SirketAdi,
                ["sunucuAdresi"] = ayar?.Adres ?? string.Empty, ["sunucuPortu"] = ayar?.Port ?? 0,
                ["sunucuSurumu"] = kayit.SunucuSurumu, ["durum"] = kayit.Durum.ToString(),
                ["bagli"] = baglanti?.Bagli ?? kayit.BagliMi, ["sonGecikmeMs"] = kayit.SonGecikmeMs,
                ["kasa"] = kayit.Kasa, ["tickGelir"] = sonFinans?.ToplamGelir ?? 0,
                ["tickGider"] = sonFinans?.ToplamGider ?? 0, ["tickNetKazanc"] = sonFinans?.TickNetKazanc ?? 0,
                ["kurtarmaKredisi"] = sonFinans?.KurtarmaKredisiGirisi ?? 0,
                ["krediNotu"] = banka.KrediNotu, ["krediSinifi"] = banka.KrediNotuSinifi,
                ["toplamBorc"] = banka.AktifBorc, ["kullanilabilirKrediLimiti"] = banka.KullanilabilirLimit,
                ["itibarPuani"] = kayit.ItibarPuani, ["guvenilirlikPuani"] = kayit.GuvenilirlikPuani,
                ["kodKalitesiPuani"] = kayit.KodKalitesiPuani, ["performansPuani"] = kayit.PerformansPuani,
                ["guvenlikPuani"] = kayit.GuvenlikPuani, ["ortalamaMusteriMemnuniyeti"] = kayit.OrtalamaMusteriMemnuniyeti,
                ["tamamlananIsSayisi"] = kayit.TamamlananIsSayisi, ["basarisizIsSayisi"] = kayit.BasarisizIsSayisi,
                ["engellenenSaldiriSayisi"] = kayit.EngellenenSaldiriSayisi, ["basariliSaldiriSayisi"] = kayit.BasariliSaldiriSayisi,
                ["hizmetler"] = JsonSerializer.SerializeToNode(kayit.Hizmetler, JsonAyarlari),
                ["isletim"] = isletim.DeepClone(), ["kodTabanliYayinlar"] = kod.DeepClone(),
                ["v6"] = JsonSerializer.SerializeToNode(v6Sirket, JsonAyarlari),
                ["finansGecmisi"] = JsonSerializer.SerializeToNode(finansSirket.Tickler.TakeLast(120), JsonAyarlari),
                ["cezaV8"] = JsonSerializer.SerializeToNode(ceza, JsonAyarlari)
            });

            Dictionary<string, JsonObject> urunIndeksi = urunler.OfType<JsonObject>()
                .Where(x => !string.IsNullOrWhiteSpace(Str(x["urunKimligi"])))
                .GroupBy(x => Str(x["urunKimligi"]), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
            foreach (JsonObject teknik in teknikUygulamalar.OfType<JsonObject>())
            {
                JsonObject uygulama = teknik["uygulama"] as JsonObject ?? new();
                string urunKimligi = Str(teknik["urunKimligi"]), uygulamaKimligi = Str(uygulama["uygulamaKimligi"]);
                urunIndeksi.TryGetValue(urunKimligi, out JsonObject? urun);
                sistemIndeksi.TryGetValue(uygulamaKimligi, out IsletimSistemiPazarKaydi? sistem);
                uygulamaPiyasasi.Add(new JsonObject
                {
                    ["sirketKimligi"] = kayit.SirketKimligi, ["sirketAdi"] = kayit.SirketAdi,
                    ["uygulama"] = uygulama.DeepClone(), ["dogrulama"] = teknik["dogrulama"]?.DeepClone(),
                    ["sunucuBagli"] = teknik["sunucuBagli"]?.DeepClone(), ["piyasada"] = teknik["piyasada"]?.DeepClone(),
                    ["urun"] = urun?.DeepClone(), ["gercekIsletimSistemiMusterisi"] = sistem?.AktifMusteriSayisi ?? 0
                });
            }
            foreach (JsonObject teknik in teknikProtokoller.OfType<JsonObject>())
                protokolPiyasasi.Add(new JsonObject { ["sirketKimligi"] = kayit.SirketKimligi, ["sirketAdi"] = kayit.SirketAdi, ["teknikProtokol"] = teknik["protokol"]?.DeepClone(), ["gecerli"] = teknik["gecerli"]?.DeepClone(), ["piyasada"] = teknik["piyasada"]?.DeepClone() });
            foreach (JsonObject kredi in krediler.OfType<JsonObject>()) krediPiyasasi.Add(new JsonObject { ["sirketKimligi"] = kayit.SirketKimligi, ["sirketAdi"] = kayit.SirketAdi, ["kredi"] = kredi.DeepClone() });
            foreach (JsonObject sozlesme in sozlesmeler.OfType<JsonObject>()) sozlesmePiyasasi.Add(new JsonObject { ["sirketKimligi"] = kayit.SirketKimligi, ["sirketAdi"] = kayit.SirketAdi, ["sozlesme"] = sozlesme.DeepClone() });
            foreach ((string ad, JsonNode? seviye) in yatirimlar) yatirimPiyasasi.Add(new JsonObject { ["sirketKimligi"] = kayit.SirketKimligi, ["sirketAdi"] = kayit.SirketAdi, ["yatirimTuru"] = ad, ["seviye"] = seviye?.DeepClone() });
        }

        List<JsonObject> sirketListesi = borsaSirketleri.OfType<JsonObject>().ToList();
        List<object> haberler = FinansV7Deposu.Getir().Haberler.OrderByDescending(x => x.TickNumarasi).Take(100).Cast<object>()
            .Concat(v6.Haberler.OrderByDescending(x => x.TickNumarasi).Take(100)).ToList();
        JsonObject cevap = new()
        {
            ["motor"] = JsonSerializer.SerializeToNode(new { _ayarlar.MotorKimligi, _ayarlar.ProtokolSurumu, tickNumarasi = tick, tickSuresiSaniye = _ayarlar.TickSuresiSaniye, katalogSurumu = _katalog.KatalogSurumu, baslangicZamani = _baslangic, sunucuZamani = DateTimeOffset.UtcNow, panoPortu = _ayarlar.CanliPanoPortu, yonetimPortu = _ayarlar.SirketYonetimPortu, borsaSurumu = "8.0" }, JsonAyarlari),
            ["genel"] = JsonSerializer.SerializeToNode(new
            {
                toplamSirketSayisi = _sirketler.SirketKayitlari.Count,
                bagliSirketSayisi = _sirketler.SirketKayitlari.Count(x => x.BagliMi),
                toplamSirketKasasi = _sirketler.SirketKayitlari.Sum(x => x.Kasa),
                toplamTickGeliri = sirketListesi.Sum(x => Dec(x["tickGelir"])),
                toplamTickGideri = sirketListesi.Sum(x => Dec(x["tickGider"])),
                toplamTickNeti = sirketListesi.Sum(x => Dec(x["tickNetKazanc"])),
                toplamSirketDegeri = sirketListesi.Sum(x => Dec(x["isletim"]?["sirketDegeri"])),
                toplamBorc = sirketListesi.Sum(x => Dec(x["toplamBorc"])),
                toplamAbone = sirketListesi.Sum(x => Int(x["isletim"]?["toplamAboneSayisi"])),
                teknikUygulamaSayisi = uygulamaPiyasasi.Count,
                piyasadakiUygulamaSayisi = uygulamaPiyasasi.OfType<JsonObject>().Count(x => Bool(x["piyasada"])),
                toplamHizmetIlani = _sirketler.SirketKayitlari.Sum(x => x.Hizmetler.Count),
                toplamMusteriSayisi = _musteriler.Musteriler.Count,
                aktifMusteriSayisi = _musteriler.AktifMusteriSayisi,
                isletimSistemiKullananMusteri = IsletimSistemiPazarDeposu.Getir().Sum(x => x.AktifMusteriSayisi),
                isletimSistemiBekleyenMusteri = Math.Max(0, _musteriler.AktifMusteriSayisi - IsletimSistemiPazarDeposu.Getir().Sum(x => x.AktifMusteriSayisi))
            }, JsonAyarlari),
            ["piyasa"] = JsonSerializer.SerializeToNode(piyasa, JsonAyarlari),
            ["sirketler"] = borsaSirketleri,
            ["hizmetPiyasasi"] = HizmetPiyasasiniOlustur(piyasa),
            ["uygulamaPiyasasi"] = uygulamaPiyasasi,
            ["protokolPiyasasi"] = protokolPiyasasi,
            ["isletimSistemiPazari"] = JsonSerializer.SerializeToNode(IsletimSistemiPazarDeposu.Getir(), JsonAyarlari),
            ["krediPiyasasi"] = krediPiyasasi,
            ["sozlesmePiyasasi"] = sozlesmePiyasasi,
            ["yatirimPiyasasi"] = yatirimPiyasasi,
            ["kategoriPazarPaylari"] = JsonSerializer.SerializeToNode(v6.KategoriPazarPaylari, JsonAyarlari),
            ["musteriCvOrnekleri"] = JsonSerializer.SerializeToNode(v6.MusteriCvOrnekleri, JsonAyarlari),
            ["haberBulteni"] = JsonSerializer.SerializeToNode(haberler, JsonAyarlari),
            ["cezaPiyasasi"] = JsonSerializer.SerializeToNode(CezaV8Deposu.GenelDurum(_sirketler.SirketKayitlari), JsonAyarlari),
            ["olaylar"] = JsonSerializer.SerializeToNode(KonsolKayitcisi.SonKayitlariGetir(250), JsonAyarlari)
        };
        return cevap.ToJsonString(JsonAyarlari);
    }

    private JsonArray HizmetPiyasasiniOlustur(PanoPiyasaDurumu piyasa)
    {
        JsonArray sonuc = [];
        foreach (HizmetTanimi hizmet in _katalog.Hizmetler.OrderBy(x => x.HizmetKimligi, StringComparer.OrdinalIgnoreCase))
        {
            PanoTalepGrubu? talep = piyasa.TalepGruplari.FirstOrDefault(x => string.Equals(x.HizmetKimligi, hizmet.HizmetKimligi, StringComparison.OrdinalIgnoreCase) && string.Equals(x.HizmetSurumu, hizmet.HizmetSurumu, StringComparison.OrdinalIgnoreCase));
            var saglayicilar = _sirketler.SirketKayitlari.Select(s => new { Sirket = s, Hizmet = s.Hizmetler.FirstOrDefault(x => x.Aktif && string.Equals(x.HizmetKimligi, hizmet.HizmetKimligi, StringComparison.OrdinalIgnoreCase) && string.Equals(x.HizmetSurumu, hizmet.HizmetSurumu, StringComparison.OrdinalIgnoreCase)) }).Where(x => x.Hizmet is not null).Select(x => new { x.Sirket.SirketKimligi, x.Sirket.SirketAdi, x.Hizmet!.BirimFiyat, x.Hizmet.AzamiEszamanliIs, bagli = x.Sirket.BagliMi, x.Sirket.KodKalitesiPuani, x.Sirket.PerformansPuani, x.Sirket.GuvenlikPuani }).OrderBy(x => x.BirimFiyat).ToList();
            sonuc.Add(JsonSerializer.SerializeToNode(new { hizmet.HizmetKimligi, hizmet.HizmetSurumu, hizmet.Aciklama, hizmet.Aktif, hizmet.ZamanAsimiMs, talepSayisi = talep?.TalepSayisi ?? 0, kotuNiyetliTalepSayisi = talep?.KotuNiyetliTalepSayisi ?? 0, ortalamaZorlukSeviyesi = talep?.OrtalamaZorlukSeviyesi ?? 0, toplamTalepButcesi = talep?.ToplamAzamiButce ?? 0, saglayiciSayisi = saglayicilar.Count, saglayicilar }, JsonAyarlari));
        }
        return sonuc;
    }

    private static string Str(JsonNode? n) { try { return n?.GetValue<string>() ?? string.Empty; } catch { return string.Empty; } }
    private static int Int(JsonNode? n) { try { return n?.GetValue<int>() ?? 0; } catch { return 0; } }
    private static decimal Dec(JsonNode? n) { try { return n?.GetValue<decimal>() ?? 0; } catch { return 0; } }
    private static bool Bool(JsonNode? n) { try { return n?.GetValue<bool>() ?? false; } catch { return false; } }

    private static async Task CevapAsync(NetworkStream akis, int kod, string durum, string tur, string govde, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(govde);
        string baslik = $"HTTP/1.1 {kod} {durum}\r\nContent-Type: {tur}\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-store, no-cache, must-revalidate\r\nPragma: no-cache\r\nX-Content-Type-Options: nosniff\r\nX-Frame-Options: DENY\r\nConnection: close\r\n\r\n";
        await akis.WriteAsync(Encoding.ASCII.GetBytes(baslik), cancellationToken);
        if (bytes.Length > 0) await akis.WriteAsync(bytes, cancellationToken);
        await akis.FlushAsync(cancellationToken);
    }

    private IEnumerable<string> YayinAdresleri()
    {
        HashSet<string> adresler = new(StringComparer.OrdinalIgnoreCase) { $"http://localhost:{_ayarlar.CanliPanoPortu}/" };
        try { foreach (IPAddress adres in Dns.GetHostEntry(Dns.GetHostName()).AddressList) if (adres.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(adres)) adresler.Add($"http://{adres}:{_ayarlar.CanliPanoPortu}/"); } catch { }
        return adresler.OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { _iptal?.Cancel(); _dinleyici?.Stop(); if (_gorev is not null) await _gorev; }
        catch (OperationCanceledException) { }
        finally { _iptal?.Dispose(); }
    }
}