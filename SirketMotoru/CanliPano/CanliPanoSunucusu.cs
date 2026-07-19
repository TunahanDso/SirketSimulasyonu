using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SirketMotoru.Ayarlar;
using SirketMotoru.Hizmetler;
using SirketMotoru.Kayit;
using SirketMotoru.Musteriler;
using SirketMotoru.Sirketler;

namespace SirketMotoru.CanliPano;

public sealed class CanliPanoSunucusu : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

    private readonly MotorAyarlari _ayarlar;
    private readonly SirketYoneticisi _sirketYoneticisi;
    private readonly MusteriYoneticisi _musteriYoneticisi;
    private readonly HizmetKatalogu _hizmetKatalogu;
    private readonly Func<long> _tickNumarasiniGetir;
    private readonly DateTimeOffset _baslangicZamani = DateTimeOffset.UtcNow;

    private TcpListener? _dinleyici;
    private CancellationTokenSource? _sunucuIptalKaynagi;
    private Task? _sunucuGorevi;
    private bool _baslatildi;
    private bool _disposed;

    public CanliPanoSunucusu(
        MotorAyarlari ayarlar,
        SirketYoneticisi sirketYoneticisi,
        MusteriYoneticisi musteriYoneticisi,
        HizmetKatalogu hizmetKatalogu,
        Func<long> tickNumarasiniGetir)
    {
        ArgumentNullException.ThrowIfNull(ayarlar);
        ArgumentNullException.ThrowIfNull(sirketYoneticisi);
        ArgumentNullException.ThrowIfNull(musteriYoneticisi);
        ArgumentNullException.ThrowIfNull(hizmetKatalogu);
        ArgumentNullException.ThrowIfNull(tickNumarasiniGetir);
        _ayarlar = ayarlar;
        _sirketYoneticisi = sirketYoneticisi;
        _musteriYoneticisi = musteriYoneticisi;
        _hizmetKatalogu = hizmetKatalogu;
        _tickNumarasiniGetir = tickNumarasiniGetir;
    }

    public Task BaslatAsync(CancellationToken cancellationToken)
    {
        DisposeEdilmediginiDogrula();

        if (_baslatildi)
        {
            return Task.CompletedTask;
        }

        _baslatildi = true;

        if (!_ayarlar.CanliPanoAktif)
        {
            KonsolKayitcisi.Bilgi(
                "Yerel ağ canlı panosu ayarlardan kapalı.");
            return Task.CompletedTask;
        }

        _sunucuIptalKaynagi =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        _dinleyici =
            new TcpListener(
                IPAddress.Any,
                _ayarlar.CanliPanoPortu);
        _dinleyici.Start(128);
        _sunucuGorevi =
            IstemciKabulDongusuAsync(
                _sunucuIptalKaynagi.Token);

        KonsolKayitcisi.Basari(
            $"Canlı borsa panosu yayında | " +
            $"Port: {_ayarlar.CanliPanoPortu} | " +
            $"Yenileme: {_ayarlar.CanliPanoYenilemeMs} ms");

        foreach (string adres in YayinAdresleriniGetir())
        {
            KonsolKayitcisi.Bilgi(
                $"Canlı pano adresi: {adres}");
        }

        return Task.CompletedTask;
    }

    private async Task IstemciKabulDongusuAsync(
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
                TcpClient istemci =
                    await _dinleyici.AcceptTcpClientAsync(
                        cancellationToken);
                _ = IstemciyiYonetAsync(
                    istemci,
                    cancellationToken);
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
                    $"Canlı pano istemci kabul hatası: " +
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
                using StreamReader okuyucu =
                    new(
                        akis,
                        Encoding.ASCII,
                        false,
                        4096,
                        true);
                string? istekSatiri =
                    await okuyucu.ReadLineAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(istekSatiri) ||
                    istekSatiri.Length > 8_192)
                {
                    await CevapGonderAsync(
                        akis,
                        400,
                        "Bad Request",
                        "text/plain; charset=utf-8",
                        "Geçersiz HTTP isteği.",
                        cancellationToken);
                    return;
                }

                for (int i = 0; i < 100; i++)
                {
                    string? baslik =
                        await okuyucu.ReadLineAsync(cancellationToken);

                    if (string.IsNullOrEmpty(baslik))
                    {
                        break;
                    }

                    if (baslik.Length > 8_192)
                    {
                        await CevapGonderAsync(
                            akis,
                            431,
                            "Request Header Fields Too Large",
                            "text/plain; charset=utf-8",
                            "HTTP başlığı çok büyük.",
                            cancellationToken);
                        return;
                    }
                }

                string[] parcalar =
                    istekSatiri.Split(
                        ' ',
                        3,
                        StringSplitOptions.RemoveEmptyEntries);

                if (parcalar.Length != 3 ||
                    !string.Equals(
                        parcalar[0],
                        "GET",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await CevapGonderAsync(
                        akis,
                        405,
                        "Method Not Allowed",
                        "text/plain; charset=utf-8",
                        "Yalnızca GET destekleniyor.",
                        cancellationToken,
                        "Allow: GET\r\n");
                    return;
                }

                string yol = parcalar[1].Split('?', 2)[0];

                switch (yol)
                {
                    case "/":
                    case "/index.html":
                        await CevapGonderAsync(
                            akis,
                            200,
                            "OK",
                            "text/html; charset=utf-8",
                            CanliPanoHtml.Icerik,
                            cancellationToken);
                        break;
                    case "/api/durum":
                        await CevapGonderAsync(
                            akis,
                            200,
                            "OK",
                            "application/json; charset=utf-8",
                            DurumJsonuOlustur(),
                            cancellationToken);
                        break;
                    case "/api/saglik":
                        await CevapGonderAsync(
                            akis,
                            200,
                            "OK",
                            "application/json; charset=utf-8",
                            JsonSerializer.Serialize(
                                new
                                {
                                    durum = "calisiyor",
                                    tickNumarasi = _tickNumarasiniGetir(),
                                    sunucuZamani = DateTimeOffset.UtcNow
                                },
                                JsonAyarlari),
                            cancellationToken);
                        break;
                    case "/favicon.ico":
                        await CevapGonderAsync(
                            akis,
                            204,
                            "No Content",
                            "image/x-icon",
                            string.Empty,
                            cancellationToken);
                        break;
                    default:
                        await CevapGonderAsync(
                            akis,
                            404,
                            "Not Found",
                            "text/plain; charset=utf-8",
                            "Sayfa bulunamadı.",
                            cancellationToken);
                        break;
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
            catch (Exception exception)
            {
                KonsolKayitcisi.Uyari(
                    $"Canlı pano istemci hatası: " +
                    exception.Message);
            }
        }
    }

    private string DurumJsonuOlustur()
    {
        long tickNumarasi = _tickNumarasiniGetir();
        PanoPiyasaDurumu piyasa = CanliPanoDurumDeposu.Getir();
        IReadOnlyList<SirketMotoru.Ag.SirketBaglantisi> baglantilar =
            _sirketYoneticisi.BaglantilariGetir();
        Dictionary<string, SirketBaglantiAyari> sirketAyarlari =
            _ayarlar.Sirketler.ToDictionary(
                sirket => sirket.SirketKimligi,
                StringComparer.OrdinalIgnoreCase);

        var sirketler =
            baglantilar
                .Select(
                    baglanti =>
                    {
                        SirketKaydi kayit = baglanti.Kayit;
                        sirketAyarlari.TryGetValue(
                            kayit.SirketKimligi,
                            out SirketBaglantiAyari? ayar);

                        return new
                        {
                            kayit.SirketKimligi,
                            kayit.SirketAdi,
                            sunucuAdresi = ayar?.Adres ?? string.Empty,
                            sunucuPortu = ayar?.Port ?? 0,
                            kayit.SunucuSurumu,
                            durum = kayit.Durum.ToString(),
                            bagli = baglanti.Bagli,
                            kayit.SonGecikmeMs,
                            kayit.SonCevapZamani,
                            kayit.SonBaglantiZamani,
                            kayit.AktifBaglantiSayisi,
                            kayit.KuyrukUzunlugu,
                            kayit.Kasa,
                            kayit.ToplamGelir,
                            kayit.ToplamIade,
                            kayit.ToplamCeza,
                            kayit.BekleyenOdeme,
                            kayit.NetGelir,
                            kayit.ToplamGuvenlikKaybi,
                            kayit.ItibarPuani,
                            kayit.GuvenilirlikPuani,
                            kayit.KodKalitesiPuani,
                            kayit.PerformansPuani,
                            kayit.GuvenlikPuani,
                            kayit.OrtalamaMusteriMemnuniyeti,
                            kayit.AktifIsSayisi,
                            kayit.TamamlananIsSayisi,
                            kayit.BasarisizIsSayisi,
                            kayit.ZamanAsiminaUgrayanIsSayisi,
                            kayit.IptalEdilenIsSayisi,
                            kayit.ReddedilenIsSayisi,
                            kayit.EngellenenSaldiriSayisi,
                            kayit.BasariliSaldiriSayisi,
                            kayit.OrtalamaIsTutari,
                            kayit.OrtalamaIslemSuresiMs,
                            kayit.IsBasariOrani,
                            kayit.SonBasariliIsZamani,
                            kayit.SonBasarisizIsZamani,
                            hizmetler = kayit.Hizmetler
                                .OrderBy(
                                    hizmet => hizmet.HizmetKimligi,
                                    StringComparer.OrdinalIgnoreCase)
                                .Select(
                                    hizmet =>
                                        new
                                        {
                                            hizmet.HizmetKimligi,
                                            hizmet.HizmetSurumu,
                                            hizmet.BirimFiyat,
                                            hizmet.AzamiEszamanliIs,
                                            hizmet.Aktif
                                        })
                                .ToList()
                        };
                    })
                .OrderByDescending(sirket => sirket.NetGelir)
                .ThenByDescending(sirket => sirket.KodKalitesiPuani)
                .ToList();

        var hizmetPiyasasi =
            _hizmetKatalogu.Hizmetler
                .OrderBy(
                    hizmet => hizmet.HizmetKimligi,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    hizmet => hizmet.HizmetSurumu,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    hizmet =>
                    {
                        PanoTalepGrubu? talep =
                            piyasa.TalepGruplari.FirstOrDefault(
                                grup =>
                                    string.Equals(
                                        grup.HizmetKimligi,
                                        hizmet.HizmetKimligi,
                                        StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(
                                        grup.HizmetSurumu,
                                        hizmet.HizmetSurumu,
                                        StringComparison.OrdinalIgnoreCase));
                        var saglayicilar =
                            _sirketYoneticisi.SirketKayitlari
                                .Select(
                                    sirket =>
                                        new
                                        {
                                            Sirket = sirket,
                                            Hizmet = sirket.HizmetiBul(
                                                hizmet.HizmetKimligi,
                                                hizmet.HizmetSurumu)
                                        })
                                .Where(
                                    aday =>
                                        aday.Hizmet is not null &&
                                        aday.Hizmet.Aktif)
                                .Select(
                                    aday =>
                                        new
                                        {
                                            aday.Sirket.SirketKimligi,
                                            aday.Sirket.SirketAdi,
                                            aday.Hizmet!.BirimFiyat,
                                            aday.Hizmet.AzamiEszamanliIs,
                                            bagli = aday.Sirket.BagliMi,
                                            aday.Sirket.SonGecikmeMs,
                                            aday.Sirket.ItibarPuani,
                                            aday.Sirket.GuvenilirlikPuani,
                                            aday.Sirket.KodKalitesiPuani,
                                            aday.Sirket.PerformansPuani,
                                            aday.Sirket.GuvenlikPuani
                                        })
                                .OrderBy(aday => aday.BirimFiyat)
                                .ThenByDescending(
                                    aday => aday.KodKalitesiPuani)
                                .ToList();

                        return new
                        {
                            hizmet.HizmetKimligi,
                            hizmet.HizmetSurumu,
                            hizmet.Aciklama,
                            hizmet.Aktif,
                            hizmet.ZamanAsimiMs,
                            hizmet.AzamiIstekBoyutuByte,
                            talepSayisi = talep?.TalepSayisi ?? 0,
                            kotuNiyetliTalepSayisi =
                                talep?.KotuNiyetliTalepSayisi ?? 0,
                            ortalamaZorlukSeviyesi =
                                talep?.OrtalamaZorlukSeviyesi ?? 0,
                            toplamTalepButcesi =
                                talep?.ToplamAzamiButce ?? 0,
                            ortalamaTalepButcesi =
                                talep?.OrtalamaAzamiButce ?? 0,
                            enYuksekTalepButcesi =
                                talep?.EnYuksekAzamiButce ?? 0,
                            saglayiciSayisi = saglayicilar.Count,
                            enUcuzFiyat =
                                saglayicilar.Count == 0
                                    ? (decimal?)null
                                    : saglayicilar.Min(
                                        saglayici => saglayici.BirimFiyat),
                            enPahaliFiyat =
                                saglayicilar.Count == 0
                                    ? (decimal?)null
                                    : saglayicilar.Max(
                                        saglayici => saglayici.BirimFiyat),
                            saglayicilar
                        };
                    })
                .ToList();

        int bagliSirketSayisi =
            baglantilar.Count(baglanti => baglanti.Bagli);
        int toplamHizmetIlani =
            _sirketYoneticisi.SirketKayitlari.Sum(
                sirket => sirket.Hizmetler.Count);
        decimal toplamSirketKasasi =
            _sirketYoneticisi.SirketKayitlari.Sum(
                sirket => sirket.Kasa);
        decimal toplamNetGelir =
            _sirketYoneticisi.SirketKayitlari.Sum(
                sirket => sirket.NetGelir);
        decimal toplamGuvenlikKaybi =
            _sirketYoneticisi.SirketKayitlari.Sum(
                sirket => sirket.ToplamGuvenlikKaybi);
        int toplamTamamlananIs =
            _sirketYoneticisi.SirketKayitlari.Sum(
                sirket => sirket.TamamlananIsSayisi);
        int toplamBasarisizIs =
            _sirketYoneticisi.SirketKayitlari.Sum(
                sirket =>
                    sirket.BasarisizIsSayisi +
                    sirket.ZamanAsiminaUgrayanIsSayisi);
        int toplamEngellenenSaldiri =
            _sirketYoneticisi.SirketKayitlari.Sum(
                sirket => sirket.EngellenenSaldiriSayisi);
        int toplamBasariliSaldiri =
            _sirketYoneticisi.SirketKayitlari.Sum(
                sirket => sirket.BasariliSaldiriSayisi);

        object durum =
            new
            {
                motor =
                    new
                    {
                        _ayarlar.MotorKimligi,
                        _ayarlar.ProtokolSurumu,
                        tickNumarasi,
                        tickSuresiSaniye = _ayarlar.TickSuresiSaniye,
                        katalogSurumu = _hizmetKatalogu.KatalogSurumu,
                        baslangicZamani = _baslangicZamani,
                        calismaSuresiSaniye =
                            Math.Max(
                                0,
                                (DateTimeOffset.UtcNow - _baslangicZamani)
                                    .TotalSeconds),
                        sunucuZamani = DateTimeOffset.UtcNow,
                        panoPortu = _ayarlar.CanliPanoPortu,
                        panoYenilemeMs = _ayarlar.CanliPanoYenilemeMs
                    },
                genel =
                    new
                    {
                        toplamSirketSayisi = baglantilar.Count,
                        bagliSirketSayisi,
                        toplamHizmetIlani,
                        toplamSirketKasasi,
                        toplamNetGelir,
                        toplamGuvenlikKaybi,
                        toplamEngellenenSaldiri,
                        toplamBasariliSaldiri,
                        toplamTamamlananIs,
                        toplamBasarisizIs,
                        toplamMusteriSayisi =
                            _musteriYoneticisi.Musteriler.Count,
                        aktifMusteriSayisi =
                            _musteriYoneticisi.AktifMusteriSayisi,
                        toplamMusteriBakiyesi =
                            _musteriYoneticisi.ToplamMusteriBakiyesi,
                        toplamMusteriHarcamasi =
                            _musteriYoneticisi.ToplamMusteriHarcamasi
                    },
                piyasa,
                sirketler,
                hizmetPiyasasi,
                olaylar = KonsolKayitcisi.SonKayitlariGetir(250)
            };

        return JsonSerializer.Serialize(durum, JsonAyarlari);
    }

    private static async Task CevapGonderAsync(
        NetworkStream akis,
        int durumKodu,
        string durumMetni,
        string icerikTuru,
        string icerik,
        CancellationToken cancellationToken,
        string ekBasliklar = "")
    {
        byte[] icerikBaytlari = Encoding.UTF8.GetBytes(icerik);
        string baslik =
            $"HTTP/1.1 {durumKodu} {durumMetni}\r\n" +
            $"Content-Type: {icerikTuru}\r\n" +
            $"Content-Length: {icerikBaytlari.Length}\r\n" +
            "Cache-Control: no-store, no-cache, must-revalidate\r\n" +
            "Pragma: no-cache\r\n" +
            "X-Content-Type-Options: nosniff\r\n" +
            "Connection: close\r\n" +
            ekBasliklar +
            "\r\n";
        byte[] baslikBaytlari = Encoding.ASCII.GetBytes(baslik);
        await akis.WriteAsync(baslikBaytlari, cancellationToken);

        if (icerikBaytlari.Length > 0)
        {
            await akis.WriteAsync(icerikBaytlari, cancellationToken);
        }

        await akis.FlushAsync(cancellationToken);
    }

    private IReadOnlyList<string> YayinAdresleriniGetir()
    {
        HashSet<string> adresler =
            new(StringComparer.OrdinalIgnoreCase)
            {
                $"http://localhost:{_ayarlar.CanliPanoPortu}/"
            };

        try
        {
            foreach (IPAddress adres in
                     Dns.GetHostEntry(Dns.GetHostName())
                         .AddressList
                         .Where(
                             adres =>
                                 adres.AddressFamily ==
                                 AddressFamily.InterNetwork &&
                                 !IPAddress.IsLoopback(adres)))
            {
                adresler.Add(
                    $"http://{adres}:{_ayarlar.CanliPanoPortu}/");
            }
        }
        catch
        {
        }

        return adresler
            .OrderBy(
                adres => adres,
                StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private void DisposeEdilmediginiDogrula()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sunucuIptalKaynagi?.Cancel();

        try
        {
            _dinleyici?.Stop();
        }
        catch
        {
        }

        if (_sunucuGorevi is not null)
        {
            try
            {
                await _sunucuGorevi;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _sunucuIptalKaynagi?.Dispose();
    }
}
