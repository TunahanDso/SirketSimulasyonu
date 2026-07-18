using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SirketMotoru.Ayarlar;
using SirketMotoru.Kayit;
using SirketMotoru.Protokol;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Ag;

public sealed class SirketBaglantisi : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly MotorAyarlari _motorAyarlari;
    private readonly SirketBaglantiAyari _sirketAyari;

    private TcpClient? _tcpClient;
    private StreamReader? _okuyucu;
    private StreamWriter? _yazici;

    public SirketKaydi Kayit { get; }

    public bool Bagli =>
        _tcpClient is not null &&
        _tcpClient.Connected &&
        _okuyucu is not null &&
        _yazici is not null;

    public SirketBaglantisi(
        MotorAyarlari motorAyarlari,
        SirketBaglantiAyari sirketAyari)
    {
        _motorAyarlari = motorAyarlari;
        _sirketAyari = sirketAyari;

        Kayit = new SirketKaydi
        {
            SirketKimligi = sirketAyari.SirketKimligi,
            SirketAdi = sirketAyari.SirketAdi,
            Durum = SirketDurumu.BagliDegil
        };
    }

    public async Task<bool> BaglanVeKaydetAsync(
        CancellationToken cancellationToken)
    {
        await BaglantiyiKapatAsync();

        Kayit.Durum = SirketDurumu.Baglaniyor;

        try
        {
            KonsolKayitcisi.Bilgi(
                $"{_sirketAyari.SirketAdi} sunucusuna bağlanılıyor: " +
                $"{_sirketAyari.Adres}:{_sirketAyari.Port}");

            _tcpClient = new TcpClient
            {
                NoDelay = true
            };

            using CancellationTokenSource zamanAsimi =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            zamanAsimi.CancelAfter(
                TimeSpan.FromMilliseconds(
                    _motorAyarlari.BaglantiZamanAsimiMs));

            await _tcpClient.ConnectAsync(
                _sirketAyari.Adres,
                _sirketAyari.Port,
                zamanAsimi.Token);

            NetworkStream agAkisi = _tcpClient.GetStream();

            _okuyucu = new StreamReader(
                agAkisi,
                new UTF8Encoding(false),
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            _yazici = new StreamWriter(
                agAkisi,
                new UTF8Encoding(false),
                bufferSize: 4096,
                leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\n"
            };

            await MerhabaGonderAsync(cancellationToken);

            SirketTanitimMesaji tanitim =
                await SirketTanitiminiOkuAsync(cancellationToken);

            if (tanitim.SirketKimligi != _sirketAyari.SirketKimligi)
            {
                throw new InvalidOperationException(
                    $"Şirket kimliği uyuşmuyor. " +
                    $"Beklenen: {_sirketAyari.SirketKimligi}, " +
                    $"gelen: {tanitim.SirketKimligi}");
            }

            Kayit.SirketKimligi = tanitim.SirketKimligi;
            Kayit.SirketAdi = tanitim.SirketAdi;
            Kayit.SunucuSurumu = tanitim.SunucuSurumu;
            Kayit.Durum = SirketDurumu.Bagli;

            await KayitSonucuGonderAsync(
                basarili: true,
                aciklama: "Şirket motor tarafından başarıyla kaydedildi.",
                cancellationToken);

            KonsolKayitcisi.Basari(
                $"{Kayit.SirketAdi} motora bağlandı. " +
                $"Sunucu sürümü: {Kayit.SunucuSurumu}");

            return true;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            Kayit.Durum = SirketDurumu.CevapVermiyor;
            Kayit.BasarisizKontrolSayisi++;

            KonsolKayitcisi.Uyari(
                $"{_sirketAyari.SirketAdi} bağlantı zaman aşımına uğradı.");

            await BaglantiyiKapatAsync();
            return false;
        }
        catch (Exception exception)
        {
            Kayit.Durum = SirketDurumu.Hatali;
            Kayit.BasarisizKontrolSayisi++;

            KonsolKayitcisi.Uyari(
                $"{_sirketAyari.SirketAdi} bağlanamadı: {exception.Message}");

            await BaglantiyiKapatAsync();
            return false;
        }
    }

    public async Task<bool> SaglikKontrolEtAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        if (!Bagli)
        {
            return false;
        }

        string istekKimligi =
            $"saglik-{tickNumarasi}-{Guid.NewGuid():N}";

        SaglikKontroluMesaji mesaj = new()
        {
            MesajTuru = MesajTurleri.SaglikKontrolu,
            MesajKimligi = YeniMesajKimligi(),
            ProtokolSurumu = _motorAyarlari.ProtokolSurumu,
            IstekKimligi = istekKimligi,
            TickNumarasi = tickNumarasi
        };

        Stopwatch kronometre = Stopwatch.StartNew();

        try
        {
            await MesajGonderAsync(mesaj, cancellationToken);

            string cevapSatiri =
                await MesajOkuAsync(cancellationToken);

            kronometre.Stop();

            SaglikSonucuMesaji? cevap =
                JsonSerializer.Deserialize<SaglikSonucuMesaji>(
                    cevapSatiri,
                    JsonAyarlari);

            if (cevap is null)
            {
                throw new InvalidOperationException(
                    "Sağlık cevabı ayrıştırılamadı.");
            }

            if (cevap.MesajTuru != MesajTurleri.SaglikSonucu)
            {
                throw new InvalidOperationException(
                    $"Beklenmeyen mesaj türü: {cevap.MesajTuru}");
            }

            if (cevap.IstekKimligi != istekKimligi)
            {
                throw new InvalidOperationException(
                    "Sağlık cevabındaki istek kimliği uyuşmuyor.");
            }

            Kayit.Durum = SirketDurumu.Calisiyor;
            Kayit.SonGecikmeMs = kronometre.Elapsed.TotalMilliseconds;
            Kayit.SonCevapZamani = DateTimeOffset.Now;
            Kayit.BasariliKontrolSayisi++;
            Kayit.AktifBaglantiSayisi = cevap.AktifBaglanti;
            Kayit.KuyrukUzunlugu = cevap.KuyrukUzunlugu;

            KonsolKayitcisi.Basari(
                $"{Kayit.SirketAdi,-16} | ÇALIŞIYOR | " +
                $"{Kayit.SonGecikmeMs,8:F2} ms | " +
                $"Bağlantı: {Kayit.AktifBaglantiSayisi} | " +
                $"Kuyruk: {Kayit.KuyrukUzunlugu}");

            return true;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            kronometre.Stop();

            Kayit.Durum = SirketDurumu.CevapVermiyor;
            Kayit.BasarisizKontrolSayisi++;

            KonsolKayitcisi.Uyari(
                $"{Kayit.SirketAdi} sağlık kontrolüne zamanında cevap vermedi.");

            await BaglantiyiKapatAsync();
            return false;
        }
        catch (Exception exception)
        {
            kronometre.Stop();

            Kayit.Durum = SirketDurumu.Hatali;
            Kayit.BasarisizKontrolSayisi++;

            KonsolKayitcisi.Uyari(
                $"{Kayit.SirketAdi} sağlık kontrolü başarısız: " +
                exception.Message);

            await BaglantiyiKapatAsync();
            return false;
        }
    }

    private async Task MerhabaGonderAsync(
        CancellationToken cancellationToken)
    {
        MerhabaMesaji mesaj = new()
        {
            MesajTuru = MesajTurleri.Merhaba,
            MesajKimligi = YeniMesajKimligi(),
            ProtokolSurumu = _motorAyarlari.ProtokolSurumu,
            MotorKimligi = _motorAyarlari.MotorKimligi
        };

        await MesajGonderAsync(mesaj, cancellationToken);
    }

    private async Task<SirketTanitimMesaji> SirketTanitiminiOkuAsync(
        CancellationToken cancellationToken)
    {
        string mesajSatiri =
            await MesajOkuAsync(cancellationToken);

        SirketTanitimMesaji? tanitim =
            JsonSerializer.Deserialize<SirketTanitimMesaji>(
                mesajSatiri,
                JsonAyarlari);

        if (tanitim is null)
        {
            throw new InvalidOperationException(
                "Şirket tanıtım mesajı okunamadı.");
        }

        if (tanitim.MesajTuru != MesajTurleri.SirketTanitim)
        {
            throw new InvalidOperationException(
                $"Şirket tanıtımı beklenirken " +
                $"{tanitim.MesajTuru} mesajı geldi.");
        }

        if (string.IsNullOrWhiteSpace(tanitim.SirketKimligi))
        {
            throw new InvalidOperationException(
                "Şirket kimliği boş olamaz.");
        }

        return tanitim;
    }

    private async Task KayitSonucuGonderAsync(
        bool basarili,
        string aciklama,
        CancellationToken cancellationToken)
    {
        KayitSonucuMesaji mesaj = new()
        {
            MesajTuru = MesajTurleri.KayitSonucu,
            MesajKimligi = YeniMesajKimligi(),
            ProtokolSurumu = _motorAyarlari.ProtokolSurumu,
            Basarili = basarili,
            SirketKimligi = _sirketAyari.SirketKimligi,
            Aciklama = aciklama
        };

        await MesajGonderAsync(mesaj, cancellationToken);
    }

    private async Task MesajGonderAsync<T>(
        T mesaj,
        CancellationToken cancellationToken)
    {
        if (_yazici is null)
        {
            throw new InvalidOperationException(
                "Şirket bağlantısı açık değil.");
        }

        string json =
            JsonSerializer.Serialize(mesaj, JsonAyarlari);

        int mesajBoyutu =
            Encoding.UTF8.GetByteCount(json);

        if (mesajBoyutu > _motorAyarlari.AzamiMesajBoyutuByte)
        {
            throw new InvalidOperationException(
                $"Mesaj boyutu sınırı aşıldı: {mesajBoyutu} byte.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        await _yazici.WriteLineAsync(json);
        await _yazici.FlushAsync(cancellationToken);
    }

    private async Task<string> MesajOkuAsync(
        CancellationToken cancellationToken)
    {
        if (_okuyucu is null)
        {
            throw new InvalidOperationException(
                "Şirket bağlantısı açık değil.");
        }

        using CancellationTokenSource zamanAsimi =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        zamanAsimi.CancelAfter(
            TimeSpan.FromMilliseconds(
                _motorAyarlari.MesajZamanAsimiMs));

        string? satir =
            await _okuyucu.ReadLineAsync(zamanAsimi.Token);

        if (satir is null)
        {
            throw new IOException(
                "Şirket bağlantıyı kapattı.");
        }

        int mesajBoyutu =
            Encoding.UTF8.GetByteCount(satir);

        if (mesajBoyutu > _motorAyarlari.AzamiMesajBoyutuByte)
        {
            throw new InvalidOperationException(
                $"Gelen mesaj çok büyük: {mesajBoyutu} byte.");
        }

        if (string.IsNullOrWhiteSpace(satir))
        {
            throw new InvalidOperationException(
                "Şirket boş mesaj gönderdi.");
        }

        return satir;
    }

    private static string YeniMesajKimligi()
    {
        return $"mesaj-{Guid.NewGuid():N}";
    }

    private async Task BaglantiyiKapatAsync()
    {
        try
        {
            if (_yazici is not null)
            {
                await _yazici.DisposeAsync();
            }
        }
        catch
        {
            // Kapatma hataları ilk sürümde yok sayılıyor.
        }

        try
        {
            _okuyucu?.Dispose();
        }
        catch
        {
            // Kapatma hataları ilk sürümde yok sayılıyor.
        }

        try
        {
            _tcpClient?.Dispose();
        }
        catch
        {
            // Kapatma hataları ilk sürümde yok sayılıyor.
        }

        _yazici = null;
        _okuyucu = null;
        _tcpClient = null;

        if (Kayit.Durum is SirketDurumu.Bagli or SirketDurumu.Calisiyor)
        {
            Kayit.Durum = SirketDurumu.BagliDegil;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await BaglantiyiKapatAsync();
    }
}