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
        Func<long> tickGetir)
    {
        _ayarlar = ayarlar ?? throw new ArgumentNullException(nameof(ayarlar));
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _musteriler = musteriler ?? throw new ArgumentNullException(nameof(musteriler));
        _katalog = katalog ?? throw new ArgumentNullException(nameof(katalog));
        _isletim = isletim ?? throw new ArgumentNullException(nameof(isletim));
        _tickGetir = tickGetir ?? throw new ArgumentNullException(nameof(tickGetir));
    }

    public Task BaslatAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_baslatildi) return Task.CompletedTask;
        _baslatildi = true;

        if (!_ayarlar.CanliPanoAktif)
        {
            KonsolKayitcisi.Bilgi("Yazılım borsası ayarlardan kapalı.");
            return Task.CompletedTask;
        }

        _iptal = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _dinleyici = new TcpListener(IPAddress.Any, _ayarlar.CanliPanoPortu);
        _dinleyici.Start(128);
        _gorev = KabulDongusuAsync(_iptal.Token);

        KonsolKayitcisi.Basari(
            $"Gelişmiş yazılım borsası yayında | Port: {_ayarlar.CanliPanoPortu}");
        foreach (string adres in YayinAdresleri())
        {
            KonsolKayitcisi.Bilgi($"Yazılım borsası adresi: {adres}");
        }

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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                KonsolKayitcisi.Uyari($"Borsa istemci kabul hatası: {exception.Message}");
                try { await Task.Delay(200, cancellationToken); }
                catch (OperationCanceledException) { break; }
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
                using StreamReader okuyucu = new(
                    akis,
                    Encoding.ASCII,
                    false,
                    4096,
                    true);

                string? ilkSatir = await okuyucu.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(ilkSatir) || ilkSatir.Length > 8192)
                {
                    await CevapAsync(akis, 400, "Bad Request", "text/plain; charset=utf-8", "Geçersiz istek.", cancellationToken);
                    return;
                }

                for (int i = 0; i < 100; i++)
                {
                    string? baslik = await okuyucu.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrEmpty(baslik)) break;
                    if (baslik.Length > 8192)
                    {
                        await CevapAsync(akis, 431, "Request Header Fields Too Large", "text/plain; charset=utf-8", "Başlık çok büyük.", cancellationToken);
                        return;
                    }
                }

                string[] parcalar = ilkSatir.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (parcalar.Length != 3 || !string.Equals(parcalar[0], "GET", StringComparison.OrdinalIgnoreCase))
                {
                    await CevapAsync(akis, 405, "Method Not Allowed", "text/plain; charset=utf-8", "Yalnız GET desteklenir.", cancellationToken, "Allow: GET\r\n");
                    return;
                }

                string yol = parcalar[1].Split('?', 2)[0];
                switch (yol)
                {
                    case "/":
                    case "/index.html":
                        await CevapAsync(akis, 200, "OK", "text/html; charset=utf-8", YazilimBorsasiHtml.Icerik, cancellationToken);
                        break;
                    case "/api/durum":
                        await CevapAsync(akis, 200, "OK", "application/json; charset=utf-8", await DurumJsonuOlusturAsync(cancellationToken), cancellationToken);
                        break;
                    case "/api/saglik":
                        await CevapAsync(
                            akis,
                            200,
                            "OK",
                            "application/json; charset=utf-8",
                            JsonSerializer.Serialize(new
                            {
                                durum = "calisiyor",
                                tickNumarasi = _tickGetir(),
                                sunucuZamani = DateTimeOffset.UtcNow,
                                surum = "borsa-v4"
                            }, JsonAyarlari),
                            cancellationToken);
                        break;
                    case "/favicon.ico":
                        await CevapAsync(akis, 204, "No Content", "image/x-icon", string.Empty, cancellationToken);
                        break;
                    default:
                        await CevapAsync(akis, 404, "Not Found", "text/plain; charset=utf-8", "Sayfa bulunamadı.", cancellationToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (Exception exception)
            {
                KonsolKayitcisi.Uyari($"Borsa istemci hatası: {exception.Message}");
            }
        }
    }

    private async Task<string> DurumJsonuOlusturAsync(CancellationToken cancellationToken)
    {
        long tick = _tickGetir();
        PanoPiyasaDurumu piyasa = CanliPanoDurumDeposu.Getir();
        IReadOnlyList<SirketMotoru.Ag.SirketBaglantisi> baglantilar =
            _sirketler.BaglantilariGetir();
        Dictionary<string, SirketBaglantiAyari> ayarlar = _ayarlar.Sirketler
            .ToDictionary(x => x.SirketKimligi, StringComparer.OrdinalIgnoreCase);

        JsonArray borsaSirketleri = [];
        JsonArray uygulamaPiyasasi = [];
        JsonArray protokolPiyasasi = [];
        JsonArray krediPiyasasi = [];
        JsonArray sozlesmePiyasasi = [];
        JsonArray yatirimPiyasasi = [];
        HashSet<string> eklenenProtokoller = new(StringComparer.OrdinalIgnoreCase);

        foreach (SirketMotoru.Ag.SirketBaglantisi baglanti in baglantilar)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SirketKaydi kayit = baglanti.Kayit;
            ayarlar.TryGetValue(kayit.SirketKimligi, out SirketBaglantiAyari? ayar);

            JsonObject panel = new();
            try
            {
                string panelJson = await _isletim.PanelJsonuOlusturAsync(
                    kayit.SirketKimligi,
                    cancellationToken);
                panel = JsonNode.Parse(panelJson) as JsonObject ?? new JsonObject();
            }
            catch (Exception exception)
            {
                panel["borsaOkumaHatasi"] = exception.Message;
            }

            JsonObject isletim = panel["isletim"] as JsonObject ?? new JsonObject();
            JsonObject kodYayinlari = panel["kodTabanliYayinlar"] as JsonObject ?? new JsonObject();
            JsonArray urunler = isletim["urunler"] as JsonArray ?? [];
            JsonArray krediler = isletim["krediler"] as JsonArray ?? [];
            JsonArray sozlesmeler = isletim["sozlesmeler"] as JsonArray ?? [];
            JsonObject yatirimlar = isletim["yatirimSeviyeleri"] as JsonObject ?? new JsonObject();
            JsonArray teknikUygulamalar = kodYayinlari["uygulamalar"] as JsonArray ?? [];
            JsonArray teknikProtokoller = kodYayinlari["protokoller"] as JsonArray ?? [];

            borsaSirketleri.Add(new JsonObject
            {
                ["sirketKimligi"] = kayit.SirketKimligi,
                ["sirketAdi"] = kayit.SirketAdi,
                ["sunucuAdresi"] = ayar?.Adres ?? string.Empty,
                ["sunucuPortu"] = ayar?.Port ?? 0,
                ["sunucuSurumu"] = kayit.SunucuSurumu,
                ["durum"] = kayit.Durum.ToString(),
                ["bagli"] = baglanti.Bagli,
                ["sonGecikmeMs"] = kayit.SonGecikmeMs,
                ["kasa"] = kayit.Kasa,
                ["netGelir"] = kayit.NetGelir,
                ["toplamGelir"] = kayit.ToplamGelir,
                ["toplamCeza"] = kayit.ToplamCeza,
                ["toplamGuvenlikKaybi"] = kayit.ToplamGuvenlikKaybi,
                ["itibarPuani"] = kayit.ItibarPuani,
                ["guvenilirlikPuani"] = kayit.GuvenilirlikPuani,
                ["kodKalitesiPuani"] = kayit.KodKalitesiPuani,
                ["performansPuani"] = kayit.PerformansPuani,
                ["guvenlikPuani"] = kayit.GuvenlikPuani,
                ["ortalamaMusteriMemnuniyeti"] = kayit.OrtalamaMusteriMemnuniyeti,
                ["tamamlananIsSayisi"] = kayit.TamamlananIsSayisi,
                ["basarisizIsSayisi"] = kayit.BasarisizIsSayisi,
                ["zamanAsiminaUgrayanIsSayisi"] = kayit.ZamanAsiminaUgrayanIsSayisi,
                ["engellenenSaldiriSayisi"] = kayit.EngellenenSaldiriSayisi,
                ["basariliSaldiriSayisi"] = kayit.BasariliSaldiriSayisi,
                ["hizmetler"] = JsonSerializer.SerializeToNode(kayit.Hizmetler, JsonAyarlari),
                ["isletim"] = isletim.DeepClone(),
                ["kodTabanliYayinlar"] = kodYayinlari.DeepClone()
            });

            Dictionary<string, JsonObject> urunIndeksi = urunler
                .OfType<JsonObject>()
                .Where(x => !string.IsNullOrWhiteSpace(x["urunKimligi"]?.GetValue<string>()))
                .ToDictionary(
                    x => x["urunKimligi"]!.GetValue<string>(),
                    x => x,
                    StringComparer.OrdinalIgnoreCase);

            foreach (JsonObject teknikKayit in teknikUygulamalar.OfType<JsonObject>())
            {
                JsonObject uygulama = teknikKayit["uygulama"] as JsonObject ?? new JsonObject();
                string urunKimligi = teknikKayit["urunKimligi"]?.GetValue<string>() ?? string.Empty;
                urunIndeksi.TryGetValue(urunKimligi, out JsonObject? urun);
                uygulamaPiyasasi.Add(new JsonObject
                {
                    ["sirketKimligi"] = kayit.SirketKimligi,
                    ["sirketAdi"] = kayit.SirketAdi,
                    ["uygulama"] = uygulama.DeepClone(),
                    ["dogrulama"] = teknikKayit["dogrulama"]?.DeepClone(),
                    ["sunucuBagli"] = teknikKayit["sunucuBagli"]?.DeepClone(),
                    ["piyasada"] = teknikKayit["piyasada"]?.DeepClone(),
                    ["urun"] = urun?.DeepClone()
                });
            }

            foreach (JsonObject teknikKayit in teknikProtokoller.OfType<JsonObject>())
            {
                JsonObject protokol = teknikKayit["protokol"] as JsonObject ?? new JsonObject();
                string piyasaKimligi = teknikKayit["piyasaProtokolKimligi"]?.GetValue<string>() ?? string.Empty;
                string anahtar = $"{kayit.SirketKimligi}/{protokol["protokolKimligi"]}/{protokol["surum"]}";
                if (eklenenProtokoller.Add(anahtar))
                {
                    protokolPiyasasi.Add(new JsonObject
                    {
                        ["sirketKimligi"] = kayit.SirketKimligi,
                        ["sirketAdi"] = kayit.SirketAdi,
                        ["teknikProtokol"] = protokol.DeepClone(),
                        ["gecerli"] = teknikKayit["gecerli"]?.DeepClone(),
                        ["piyasada"] = teknikKayit["piyasada"]?.DeepClone(),
                        ["piyasaProtokolKimligi"] = piyasaKimligi
                    });
                }
            }

            foreach (JsonObject kredi in krediler.OfType<JsonObject>())
            {
                krediPiyasasi.Add(new JsonObject
                {
                    ["sirketKimligi"] = kayit.SirketKimligi,
                    ["sirketAdi"] = kayit.SirketAdi,
                    ["kredi"] = kredi.DeepClone()
                });
            }
            foreach (JsonObject sozlesme in sozlesmeler.OfType<JsonObject>())
            {
                sozlesmePiyasasi.Add(new JsonObject
                {
                    ["sirketKimligi"] = kayit.SirketKimligi,
                    ["sirketAdi"] = kayit.SirketAdi,
                    ["sozlesme"] = sozlesme.DeepClone()
                });
            }
            foreach ((string ad, JsonNode? seviye) in yatirimlar)
            {
                yatirimPiyasasi.Add(new JsonObject
                {
                    ["sirketKimligi"] = kayit.SirketKimligi,
                    ["sirketAdi"] = kayit.SirketAdi,
                    ["yatirimTuru"] = ad,
                    ["seviye"] = seviye?.DeepClone()
                });
            }
        }

        List<JsonObject> sirketListesi = borsaSirketleri.OfType<JsonObject>().ToList();
        decimal toplamDeger = sirketListesi.Sum(x => DecimalDegeri(x["isletim"]?["sirketDegeri"]));
        decimal toplamBorc = sirketListesi.Sum(x => DecimalDegeri(x["isletim"]?["toplamBorc"]));
        int toplamAbone = sirketListesi.Sum(x => IntDegeri(x["isletim"]?["toplamAboneSayisi"]));
        int piyasadakiUygulama = uygulamaPiyasasi.OfType<JsonObject>()
            .Count(x => BoolDegeri(x["piyasada"]));
        int piyasadakiProtokol = protokolPiyasasi.OfType<JsonObject>()
            .Count(x => BoolDegeri(x["piyasada"]));

        JsonArray hizmetPiyasasi = HizmetPiyasasiniOlustur(piyasa);
        JsonObject cevap = new()
        {
            ["motor"] = JsonSerializer.SerializeToNode(new
            {
                _ayarlar.MotorKimligi,
                _ayarlar.ProtokolSurumu,
                tickNumarasi = tick,
                tickSuresiSaniye = _ayarlar.TickSuresiSaniye,
                katalogSurumu = _katalog.KatalogSurumu,
                baslangicZamani = _baslangic,
                calismaSuresiSaniye = Math.Max(0, (DateTimeOffset.UtcNow - _baslangic).TotalSeconds),
                sunucuZamani = DateTimeOffset.UtcNow,
                panoPortu = _ayarlar.CanliPanoPortu,
                yonetimPortu = _ayarlar.SirketYonetimPortu,
                borsaSurumu = "4.0"
            }, JsonAyarlari),
            ["genel"] = JsonSerializer.SerializeToNode(new
            {
                toplamSirketSayisi = baglantilar.Count,
                bagliSirketSayisi = baglantilar.Count(x => x.Bagli),
                toplamSirketKasasi = _sirketler.SirketKayitlari.Sum(x => x.Kasa),
                toplamNetGelir = _sirketler.SirketKayitlari.Sum(x => x.NetGelir),
                toplamSirketDegeri = toplamDeger,
                toplamBorc,
                toplamAbone,
                teknikUygulamaSayisi = uygulamaPiyasasi.Count,
                piyasadakiUygulamaSayisi = piyasadakiUygulama,
                teknikProtokolSayisi = protokolPiyasasi.Count,
                piyasadakiProtokolSayisi = piyasadakiProtokol,
                toplamHizmetIlani = _sirketler.SirketKayitlari.Sum(x => x.Hizmetler.Count),
                toplamGuvenlikKaybi = _sirketler.SirketKayitlari.Sum(x => x.ToplamGuvenlikKaybi),
                toplamEngellenenSaldiri = _sirketler.SirketKayitlari.Sum(x => x.EngellenenSaldiriSayisi),
                toplamBasariliSaldiri = _sirketler.SirketKayitlari.Sum(x => x.BasariliSaldiriSayisi),
                toplamMusteriSayisi = _musteriler.Musteriler.Count,
                aktifMusteriSayisi = _musteriler.AktifMusteriSayisi,
                toplamMusteriBakiyesi = _musteriler.ToplamMusteriBakiyesi,
                toplamMusteriHarcamasi = _musteriler.ToplamMusteriHarcamasi
            }, JsonAyarlari),
            ["piyasa"] = JsonSerializer.SerializeToNode(piyasa, JsonAyarlari),
            ["sirketler"] = borsaSirketleri,
            ["hizmetPiyasasi"] = hizmetPiyasasi,
            ["uygulamaPiyasasi"] = uygulamaPiyasasi,
            ["protokolPiyasasi"] = protokolPiyasasi,
            ["krediPiyasasi"] = krediPiyasasi,
            ["sozlesmePiyasasi"] = sozlesmePiyasasi,
            ["yatirimPiyasasi"] = yatirimPiyasasi,
            ["olaylar"] = JsonSerializer.SerializeToNode(
                KonsolKayitcisi.SonKayitlariGetir(300),
                JsonAyarlari)
        };

        return cevap.ToJsonString(JsonAyarlari);
    }

    private JsonArray HizmetPiyasasiniOlustur(PanoPiyasaDurumu piyasa)
    {
        JsonArray sonuc = [];
        foreach (HizmetTanimi hizmet in _katalog.Hizmetler
                     .OrderBy(x => x.HizmetKimligi, StringComparer.OrdinalIgnoreCase))
        {
            PanoTalepGrubu? talep = piyasa.TalepGruplari.FirstOrDefault(x =>
                string.Equals(x.HizmetKimligi, hizmet.HizmetKimligi, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.HizmetSurumu, hizmet.HizmetSurumu, StringComparison.OrdinalIgnoreCase));

            var saglayicilar = _sirketler.SirketKayitlari
                .Select(sirket => new
                {
                    Sirket = sirket,
                    Hizmet = sirket.Hizmetler.FirstOrDefault(x =>
                        x.Aktif &&
                        string.Equals(x.HizmetKimligi, hizmet.HizmetKimligi, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.HizmetSurumu, hizmet.HizmetSurumu, StringComparison.OrdinalIgnoreCase))
                })
                .Where(x => x.Hizmet is not null)
                .Select(x => new
                {
                    x.Sirket.SirketKimligi,
                    x.Sirket.SirketAdi,
                    x.Hizmet!.BirimFiyat,
                    x.Hizmet.AzamiEszamanliIs,
                    bagli = x.Sirket.BagliMi,
                    x.Sirket.KodKalitesiPuani,
                    x.Sirket.PerformansPuani,
                    x.Sirket.GuvenlikPuani
                })
                .OrderBy(x => x.BirimFiyat)
                .ToList();

            sonuc.Add(JsonSerializer.SerializeToNode(new
            {
                hizmet.HizmetKimligi,
                hizmet.HizmetSurumu,
                hizmet.Aciklama,
                hizmet.Aktif,
                hizmet.ZamanAsimiMs,
                talepSayisi = talep?.TalepSayisi ?? 0,
                kotuNiyetliTalepSayisi = talep?.KotuNiyetliTalepSayisi ?? 0,
                ortalamaZorlukSeviyesi = talep?.OrtalamaZorlukSeviyesi ?? 0,
                toplamTalepButcesi = talep?.ToplamAzamiButce ?? 0,
                saglayiciSayisi = saglayicilar.Count,
                enUcuzFiyat = saglayicilar.Count == 0 ? (decimal?)null : saglayicilar.Min(x => x.BirimFiyat),
                enPahaliFiyat = saglayicilar.Count == 0 ? (decimal?)null : saglayicilar.Max(x => x.BirimFiyat),
                saglayicilar
            }, JsonAyarlari));
        }
        return sonuc;
    }

    private static decimal DecimalDegeri(JsonNode? node)
    {
        try { return node?.GetValue<decimal>() ?? 0; }
        catch { return 0; }
    }

    private static int IntDegeri(JsonNode? node)
    {
        try { return node?.GetValue<int>() ?? 0; }
        catch { return 0; }
    }

    private static bool BoolDegeri(JsonNode? node)
    {
        try { return node?.GetValue<bool>() ?? false; }
        catch { return false; }
    }

    private static async Task CevapAsync(
        NetworkStream akis,
        int kod,
        string durum,
        string icerikTuru,
        string govde,
        CancellationToken cancellationToken,
        string ekBasliklar = "")
    {
        byte[] govdeBaytlari = Encoding.UTF8.GetBytes(govde);
        string baslik =
            $"HTTP/1.1 {kod} {durum}\r\n" +
            $"Content-Type: {icerikTuru}\r\n" +
            $"Content-Length: {govdeBaytlari.Length}\r\n" +
            "Cache-Control: no-store, no-cache, must-revalidate\r\n" +
            "Pragma: no-cache\r\n" +
            "X-Content-Type-Options: nosniff\r\n" +
            "X-Frame-Options: DENY\r\n" +
            "Connection: close\r\n" +
            ekBasliklar +
            "\r\n";
        await akis.WriteAsync(Encoding.ASCII.GetBytes(baslik), cancellationToken);
        if (govdeBaytlari.Length > 0)
        {
            await akis.WriteAsync(govdeBaytlari, cancellationToken);
        }
        await akis.FlushAsync(cancellationToken);
    }

    private IEnumerable<string> YayinAdresleri()
    {
        HashSet<string> adresler = new(StringComparer.OrdinalIgnoreCase)
        {
            $"http://localhost:{_ayarlar.CanliPanoPortu}/"
        };
        try
        {
            foreach (IPAddress adres in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (adres.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(adres))
                {
                    adresler.Add($"http://{adres}:{_ayarlar.CanliPanoPortu}/");
                }
            }
        }
        catch { }
        return adresler.OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
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
}
