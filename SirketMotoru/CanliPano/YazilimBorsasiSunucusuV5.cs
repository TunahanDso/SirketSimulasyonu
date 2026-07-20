using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SirketMotoru.Ayarlar;
using SirketMotoru.Hizmetler;
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
    private readonly Func<long> _tick;
    private readonly DateTimeOffset _baslangic = DateTimeOffset.UtcNow;
    private TcpListener? _dinleyici;
    private CancellationTokenSource? _iptal;
    private Task? _gorev;
    private bool _disposed;

    public YazilimBorsasiSunucusu(
        MotorAyarlari ayarlar,
        SirketYoneticisi sirketler,
        MusteriYoneticisi musteriler,
        HizmetKatalogu katalog,
        KodTabanliSirketIsletimYoneticisi isletim,
        V9EkonomiYoneticisi _,
        Func<long> tickGetir)
    {
        _ayarlar = ayarlar ?? throw new ArgumentNullException(nameof(ayarlar));
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _musteriler = musteriler ?? throw new ArgumentNullException(nameof(musteriler));
        _katalog = katalog ?? throw new ArgumentNullException(nameof(katalog));
        _isletim = isletim ?? throw new ArgumentNullException(nameof(isletim));
        _tick = tickGetir ?? throw new ArgumentNullException(nameof(tickGetir));
    }

    public Task BaslatAsync(CancellationToken ct)
    {
        if (!_ayarlar.CanliPanoAktif) return Task.CompletedTask;
        _iptal = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _dinleyici = new TcpListener(IPAddress.Any, _ayarlar.CanliPanoPortu);
        _dinleyici.Start(128);
        _gorev = KabulAsync(_iptal.Token);
        KonsolKayitcisi.Basari($"Yazılım borsası V9 yayında | Port: {_ayarlar.CanliPanoPortu}");
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
            catch (Exception hata) { KonsolKayitcisi.Uyari($"8080 kabul hatası: {hata.Message}"); }
        }
    }

    private async Task IstemciAsync(TcpClient istemci, CancellationToken ct)
    {
        using (istemci)
        {
            try
            {
                await using NetworkStream akis = istemci.GetStream();
                using StreamReader r = new(akis, Encoding.ASCII, false, 4096, true);
                string? ilk = await r.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(ilk)) return;
                for (int i = 0; i < 100; i++) if (string.IsNullOrEmpty(await r.ReadLineAsync(ct))) break;
                string[] p = ilk.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2 || p[0] != "GET") { await CevapAsync(akis, 405, "text/plain; charset=utf-8", "Yalnız GET.", ct); return; }
                string yol = p[1].Split('?', 2)[0];
                if (yol is "/" or "/index.html") await CevapAsync(akis, 200, "text/html; charset=utf-8", YazilimBorsasiHtml.Icerik, ct);
                else if (yol == "/api/durum") await CevapAsync(akis, 200, "application/json; charset=utf-8", await DurumAsync(ct), ct);
                else if (yol == "/api/saglik") await CevapAsync(akis, 200, "application/json; charset=utf-8", JsonSerializer.Serialize(new { durum = "calisiyor", surum = "borsa-v9", tick = _tick() }, JsonAyarlari), ct);
                else if (yol == "/favicon.ico") await CevapAsync(akis, 204, "image/x-icon", "", ct);
                else await CevapAsync(akis, 404, "text/plain; charset=utf-8", "Bulunamadı.", ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (Exception hata) { KonsolKayitcisi.Uyari($"8080 istemci hatası: {hata.Message}"); }
        }
    }

    private async Task<string> DurumAsync(CancellationToken ct)
    {
        long tick = _tick();
        V9PazarDosyasi pazar = V9PazarDeposu.Getir();
        JsonArray sirketler = [], uygulamalar = [], protokoller = [];
        Dictionary<string, V9SirketPazarOzeti> ozetler = pazar.SirketOzetleri.ToDictionary(x => x.SirketKimligi, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, V9SirketKapasiteDurumu> kapasiteler = pazar.SirketKapasiteleri;

        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
        {
            JsonObject panel;
            try { panel = JsonNode.Parse(await _isletim.PanelJsonuOlusturAsync(sirket.SirketKimligi, ct)) as JsonObject ?? new(); }
            catch (Exception hata) { panel = new JsonObject { ["hata"] = hata.Message }; }
            JsonObject isletim = panel["isletim"] as JsonObject ?? new();
            JsonObject kod = panel["kodTabanliYayinlar"] as JsonObject ?? new();
            ozetler.TryGetValue(sirket.SirketKimligi, out V9SirketPazarOzeti? ozet);
            kapasiteler.TryGetValue(sirket.SirketKimligi, out V9SirketKapasiteDurumu? kapasite);
            decimal borc = isletim["krediler"] is JsonArray krediler
                ? krediler.OfType<JsonObject>().Where(x => Bool(x["aktif"])).Sum(x => Dec(x["kalanBorc"]))
                : 0;
            sirketler.Add(JsonSerializer.SerializeToNode(new
            {
                sirket.SirketKimligi, sirket.SirketAdi, bagli = sirket.BagliMi, sirket.Kasa,
                sirket.ItibarPuani, sirket.GuvenilirlikPuani, sirket.KodKalitesiPuani,
                sirket.PerformansPuani, sirket.GuvenlikPuani, sirket.OrtalamaMusteriMemnuniyeti,
                hizmetGeliri = ozet?.HizmetGeliri ?? 0, uygulamaGeliri = ozet?.UygulamaGeliri ?? 0,
                abonelikGeliri = ozet?.AbonelikGeliri ?? 0, protokolGeliri = ozet?.ProtokolGeliri ?? 0,
                isletmeGideri = ozet?.IsletmeGideri ?? 0, finansmanGideri = ozet?.FinansmanGideri ?? 0,
                netKazanc = ozet?.NetKazanc ?? 0, borc,
                toplamKapasite = kapasite?.ToplamFizikselKapasite ?? 0,
                ayrilmisKapasite = kapasite?.AyrilmisKapasite ?? 0,
                kullanilanKapasite = kapasite?.KullanilanKapasite ?? 0,
                sirketDegeri = Dec(isletim["sirketDegeri"])
            }, JsonAyarlari));

            Dictionary<string, JsonObject> urunIndeksi = (isletim["urunler"] as JsonArray ?? [])
                .OfType<JsonObject>().Where(x => !string.IsNullOrWhiteSpace(Str(x["urunKimligi"])))
                .ToDictionary(x => Str(x["urunKimligi"]), x => x, StringComparer.OrdinalIgnoreCase);
            foreach (JsonObject teknik in (kod["uygulamalar"] as JsonArray ?? []).OfType<JsonObject>())
            {
                JsonObject manifest = teknik["uygulama"] as JsonObject ?? new();
                string urunKimligi = Str(teknik["urunKimligi"]);
                urunIndeksi.TryGetValue(urunKimligi, out JsonObject? urun);
                uygulamalar.Add(new JsonObject
                {
                    ["sirketKimligi"] = sirket.SirketKimligi, ["sirketAdi"] = sirket.SirketAdi,
                    ["manifest"] = manifest.DeepClone(), ["dogrulama"] = teknik["dogrulama"]?.DeepClone(),
                    ["piyasada"] = teknik["piyasada"]?.DeepClone(), ["urun"] = urun?.DeepClone(),
                    ["gelir"] = urun is null ? 0 : Dec(urun["toplamGelir"]),
                    ["gider"] = urun is null ? 0 : Dec(urun["toplamGider"])
                });
            }
            foreach (JsonObject p in (kod["protokoller"] as JsonArray ?? []).OfType<JsonObject>())
                protokoller.Add(new JsonObject { ["sirketAdi"] = sirket.SirketAdi, ["protokol"] = p["protokol"]?.DeepClone(), ["piyasada"] = p["piyasada"]?.DeepClone() });
        }

        List<object> cv = MusteriCvleri(tick);
        List<object> hizmetPazari = pazar.HizmetTalepleri.Values.OrderByDescending(x => x.BuTickTalep).Select(x => (object)new
        {
            x.Anahtar, x.Ad, x.BuTickTalep, x.KarsilananTalep, x.ArzKapasitesi, x.KarsilanamayanTalep,
            x.KarsilanmaOrani, x.DegisimYuzdesi, x.AktifTrend, x.TrendCarpani, x.Gecmis, x.ArzDagilimi,
            sabitFiyat = MotorHizmetFiyatlari.Fiyat(x.Anahtar.Split('@')[0], x.Anahtar.Contains('@') ? x.Anahtar.Split('@')[1] : "1.0")
        }).ToList();
        List<object> haberler = pazar.Haberler.OrderByDescending(x => x.TickNumarasi).Take(100).Cast<object>()
            .Concat(KonsolKayitcisi.SonKayitlariGetir(120).Select(x => (object)new { baslik = x.Seviye, aciklama = x.Mesaj, tickNumarasi = tick, tur = "motor" }))
            .ToList();

        var cevap = new
        {
            motor = new { _ayarlar.MotorKimligi, tickNumarasi = tick, baslangicZamani = _baslangic, musteriSayisi = _musteriler.Musteriler.Count, borsaSurumu = "9.0" },
            genel = new
            {
                toplamSirket = _sirketler.SirketKayitlari.Count, bagliSirket = _sirketler.SirketKayitlari.Count(x => x.BagliMi),
                toplamKasa = _sirketler.SirketKayitlari.Sum(x => x.Kasa), aktifMusteri = _musteriler.AktifMusteriSayisi,
                hizmetTalebi = pazar.HizmetTalepleri.Values.Sum(x => x.BuTickTalep),
                hizmetKarsilanan = pazar.HizmetTalepleri.Values.Sum(x => x.KarsilananTalep),
                osTalebi = pazar.IsletimSistemiTalebi.BuTickTalep, osKarsilanan = pazar.IsletimSistemiTalebi.KarsilananTalep,
                uygulamaKategoriSayisi = pazar.UygulamaTalepleri.Count,
                toplamUygulamaGeliri = pazar.SirketOzetleri.Sum(x => x.UygulamaGeliri),
                toplamHizmetGeliri = pazar.SirketOzetleri.Sum(x => x.HizmetGeliri),
                toplamGider = pazar.SirketOzetleri.Sum(x => x.IsletmeGideri + x.FinansmanGideri),
                toplamNet = pazar.SirketOzetleri.Sum(x => x.NetKazanc)
            },
            sirketler,
            uygulamalar,
            protokoller,
            isletimSistemiPazari = pazar.IsletimSistemiTalebi,
            uygulamaPazari = pazar.UygulamaTalepleri.Values.OrderByDescending(x => x.BuTickTalep),
            hizmetPazari,
            musteriCvleri = cv,
            haberler,
            olaylar = KonsolKayitcisi.SonKayitlariGetir(250)
        };
        return JsonSerializer.Serialize(cevap, JsonAyarlari);
    }

    private List<object> MusteriCvleri(long tick)
    {
        List<Musteri> aktif = _musteriler.Musteriler.Where(x => x.Aktif).ToList();
        if (aktif.Count == 0) return [];
        int baslangic = (int)(Math.Abs(tick * 137L) % aktif.Count);
        List<object> sonuc = [];
        for (int i = 0; i < Math.Min(200, aktif.Count); i++)
        {
            Musteri m = aktif[(baslangic + i * 97) % aktif.Count];
            sonuc.Add(new
            {
                m.MusteriKimligi, m.MusteriAdi, musteriTuru = m.MusteriTuru.ToString(), m.MeslekProfili,
                m.GelirSegmenti, m.Bakiye, m.IsletimSistemiKimligi, m.IsletimSistemiSirketKimligi,
                uygulamalar = m.KullandigiUygulamalar, m.ToplamHarcama, m.BasariliIsSayisi, m.BasarisizIsSayisi,
                m.TercihEdilenSirketKimligi, m.IsletimSistemiMemnuniyeti, m.ToplamUygulamaDegisimSayisi
            });
        }
        return sonuc;
    }

    private static string Str(JsonNode? n) { try { return n?.GetValue<string>() ?? ""; } catch { return ""; } }
    private static decimal Dec(JsonNode? n) { try { return n?.GetValue<decimal>() ?? 0; } catch { return 0; } }
    private static bool Bool(JsonNode? n) { try { return n?.GetValue<bool>() ?? false; } catch { return false; } }
    private static async Task CevapAsync(NetworkStream akis, int kod, string tur, string govde, CancellationToken ct)
    {
        byte[] b = Encoding.UTF8.GetBytes(govde); string durum = kod switch { 200 => "OK", 204 => "No Content", 404 => "Not Found", 405 => "Method Not Allowed", _ => "Error" };
        string h = $"HTTP/1.1 {kod} {durum}\r\nContent-Type: {tur}\r\nContent-Length: {b.Length}\r\nCache-Control: no-store\r\nX-Frame-Options: DENY\r\nConnection: close\r\n\r\n";
        await akis.WriteAsync(Encoding.ASCII.GetBytes(h), ct); if (b.Length > 0) await akis.WriteAsync(b, ct); await akis.FlushAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return; _disposed = true;
        try { _iptal?.Cancel(); _dinleyici?.Stop(); if (_gorev is not null) await _gorev; }
        catch (OperationCanceledException) { }
        finally { _iptal?.Dispose(); }
    }
}
