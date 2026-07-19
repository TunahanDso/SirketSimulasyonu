using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SirketMotoru.Ayarlar;
using SirketMotoru.Isler;
using SirketMotoru.Kayit;
using SirketMotoru.Protokol;
using SirketMotoru.Protokol.Mesajlar;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Ag;

public sealed class SirketBaglantisi : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,

            PropertyNameCaseInsensitive =
                true,

            WriteIndented =
                false
        };

    private readonly MotorAyarlari _motorAyarlari;

    private readonly SirketBaglantiAyari _sirketAyari;

    /*
     * Sağlık kontrolü, iş isteği ve finans güncellemesi aynı
     * TCP akışını kullanır. Cevapların birbirine karışmaması
     * için bağlantıda aynı anda tek protokol işlemi yürütülür.
     */
    private readonly SemaphoreSlim _istekCevapKilidi =
        new(1, 1);

    private readonly SemaphoreSlim _baglantiKilidi =
        new(1, 1);

    private TcpClient? _tcpClient;

    private StreamReader? _okuyucu;

    private StreamWriter? _yazici;

    private bool _disposed;

    public SirketKaydi Kayit { get; }

    public bool Bagli =>
        !_disposed &&
        _tcpClient is not null &&
        _tcpClient.Connected &&
        _okuyucu is not null &&
        _yazici is not null;

    public SirketBaglantisi(
        MotorAyarlari motorAyarlari,
        SirketBaglantiAyari sirketAyari)
    {
        ArgumentNullException.ThrowIfNull(
            motorAyarlari);

        ArgumentNullException.ThrowIfNull(
            sirketAyari);

        if (string.IsNullOrWhiteSpace(
                sirketAyari.SirketKimligi))
        {
            throw new ArgumentException(
                "Şirket kimliği boş olamaz.",
                nameof(sirketAyari));
        }

        if (string.IsNullOrWhiteSpace(
                sirketAyari.SirketAdi))
        {
            throw new ArgumentException(
                "Şirket adı boş olamaz.",
                nameof(sirketAyari));
        }

        if (string.IsNullOrWhiteSpace(
                sirketAyari.Adres))
        {
            throw new ArgumentException(
                "Şirket sunucu adresi boş olamaz.",
                nameof(sirketAyari));
        }

        if (sirketAyari.Port is <= 0 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sirketAyari),
                "Şirket portu 1-65535 arasında olmalıdır.");
        }

        _motorAyarlari =
            motorAyarlari;

        _sirketAyari =
            sirketAyari;

        Kayit =
            new SirketKaydi
            {
                SirketKimligi =
                    sirketAyari.SirketKimligi,

                SirketAdi =
                    sirketAyari.SirketAdi,

                Durum =
                    SirketDurumu.BagliDegil
            };
    }

    public async Task<bool> BaglanVeKaydetAsync(
        CancellationToken cancellationToken)
    {
        DisposeEdilmediginiDogrula();

        await _baglantiKilidi.WaitAsync(
            cancellationToken);

        try
        {
            await BaglantiyiKapatIcAsync();

            Kayit.Durum =
                SirketDurumu.Baglaniyor;

            try
            {
                KonsolKayitcisi.Bilgi(
                    $"{_sirketAyari.SirketAdi} " +
                    $"sunucusuna bağlanılıyor: " +
                    $"{_sirketAyari.Adres}:" +
                    $"{_sirketAyari.Port}");

                _tcpClient =
                    new TcpClient
                    {
                        NoDelay = true
                    };

                using CancellationTokenSource zamanAsimi =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(
                            cancellationToken);

                zamanAsimi.CancelAfter(
                    TimeSpan.FromMilliseconds(
                        _motorAyarlari
                            .BaglantiZamanAsimiMs));

                await _tcpClient.ConnectAsync(
                    _sirketAyari.Adres,
                    _sirketAyari.Port,
                    zamanAsimi.Token);

                NetworkStream agAkisi =
                    _tcpClient.GetStream();

                _okuyucu =
                    new StreamReader(
                        agAkisi,
                        new UTF8Encoding(
                            encoderShouldEmitUTF8Identifier:
                                false),
                        detectEncodingFromByteOrderMarks:
                            false,
                        bufferSize:
                            4096,
                        leaveOpen:
                            true);

                _yazici =
                    new StreamWriter(
                        agAkisi,
                        new UTF8Encoding(
                            encoderShouldEmitUTF8Identifier:
                                false),
                        bufferSize:
                            4096,
                        leaveOpen:
                            true)
                    {
                        AutoFlush = true,
                        NewLine = "\n"
                    };

                await _istekCevapKilidi.WaitAsync(
                    cancellationToken);

                try
                {
                    await MerhabaGonderAsync(
                        cancellationToken);

                    SirketTanitimMesaji tanitim =
                        await SirketTanitiminiOkuAsync(
                            cancellationToken);

                    SirketTanitiminiDogrula(
                        tanitim);

                    SirketKaydiniGuncelle(
                        tanitim);

                    await KayitSonucuGonderAsync(
                        basarili:
                            true,
                        aciklama:
                            "Şirket motor tarafından " +
                            "başarıyla kaydedildi.",
                        cancellationToken);
                }
                finally
                {
                    _istekCevapKilidi.Release();
                }

                Kayit.Durum =
                    SirketDurumu.Bagli;

                Kayit.SonBaglantiZamani =
                    DateTimeOffset.UtcNow;

                string hizmetMetni =
                    Kayit.Hizmetler.Count == 0
                        ? "Henüz hizmet bildirilmedi"
                        : string.Join(
                            ", ",
                            Kayit.Hizmetler.Select(
                                hizmet =>
                                    $"{hizmet.HizmetKimligi}@" +
                                    $"{hizmet.HizmetSurumu}"));

                KonsolKayitcisi.Basari(
                    $"{Kayit.SirketAdi} motora bağlandı. " +
                    $"Sunucu sürümü: " +
                    $"{Kayit.SunucuSurumu} | " +
                    $"Hizmetler: {hizmetMetni}");

                return true;
            }
            catch (OperationCanceledException)
                when (!cancellationToken
                    .IsCancellationRequested)
            {
                Kayit.Durum =
                    SirketDurumu.CevapVermiyor;

                Kayit.BasarisizKontrolSayisi++;

                KonsolKayitcisi.Uyari(
                    $"{_sirketAyari.SirketAdi} " +
                    "bağlantı zaman aşımına uğradı.");

                await BaglantiyiKapatIcAsync();

                return false;
            }
            catch (Exception exception)
            {
                Kayit.Durum =
                    SirketDurumu.Hatali;

                Kayit.BasarisizKontrolSayisi++;

                KonsolKayitcisi.Uyari(
                    $"{_sirketAyari.SirketAdi} " +
                    $"bağlanamadı: {exception.Message}");

                await BaglantiyiKapatIcAsync();

                return false;
            }
        }
        finally
        {
            _baglantiKilidi.Release();
        }
    }

    public async Task<bool> SaglikKontrolEtAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        DisposeEdilmediginiDogrula();

        if (!Bagli)
        {
            return false;
        }

        await _istekCevapKilidi.WaitAsync(
            cancellationToken);

        try
        {
            if (!Bagli)
            {
                return false;
            }

            string istekKimligi =
                $"saglik-{tickNumarasi}-" +
                $"{Guid.NewGuid():N}";

            SaglikKontroluMesaji mesaj =
                new()
                {
                    MesajTuru =
                        MesajTurleri.SaglikKontrolu,

                    MesajKimligi =
                        YeniMesajKimligi(),

                    ProtokolSurumu =
                        _motorAyarlari
                            .ProtokolSurumu,

                    IstekKimligi =
                        istekKimligi,

                    TickNumarasi =
                        tickNumarasi
                };

            Stopwatch kronometre =
                Stopwatch.StartNew();

            try
            {
                await MesajGonderAsync(
                    mesaj,
                    cancellationToken);

                string cevapSatiri =
                    await MesajOkuAsync(
                        _motorAyarlari
                            .MesajZamanAsimiMs,
                        cancellationToken);

                kronometre.Stop();

                SaglikSonucuMesaji? cevap =
                    JsonSerializer
                        .Deserialize<SaglikSonucuMesaji>(
                            cevapSatiri,
                            JsonAyarlari);

                if (cevap is null)
                {
                    throw new InvalidOperationException(
                        "Sağlık cevabı ayrıştırılamadı.");
                }

                if (!string.Equals(
                        cevap.MesajTuru,
                        MesajTurleri.SaglikSonucu,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Beklenmeyen mesaj türü: " +
                        $"{cevap.MesajTuru}");
                }

                if (!string.Equals(
                        cevap.IstekKimligi,
                        istekKimligi,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Sağlık cevabındaki istek " +
                        "kimliği uyuşmuyor.");
                }

                Kayit.Durum =
                    SirketDurumu.Calisiyor;

                Kayit.SonGecikmeMs =
                    kronometre
                        .Elapsed
                        .TotalMilliseconds;

                Kayit.SonCevapZamani =
                    DateTimeOffset.UtcNow;

                Kayit.BasariliKontrolSayisi++;

                Kayit.AktifBaglantiSayisi =
                    Math.Max(
                        0,
                        cevap.AktifBaglanti);

                Kayit.KuyrukUzunlugu =
                    Math.Max(
                        0,
                        cevap.KuyrukUzunlugu);

                KonsolKayitcisi.Basari(
                    $"{Kayit.SirketAdi,-16} | " +
                    $"ÇALIŞIYOR | " +
                    $"{Kayit.SonGecikmeMs,8:F2} ms | " +
                    $"Bağlantı: " +
                    $"{Kayit.AktifBaglantiSayisi} | " +
                    $"Kuyruk: " +
                    $"{Kayit.KuyrukUzunlugu}");

                return true;
            }
            catch (OperationCanceledException)
                when (!cancellationToken
                    .IsCancellationRequested)
            {
                kronometre.Stop();

                Kayit.Durum =
                    SirketDurumu.CevapVermiyor;

                Kayit.BasarisizKontrolSayisi++;

                KonsolKayitcisi.Uyari(
                    $"{Kayit.SirketAdi} sağlık kontrolüne " +
                    "zamanında cevap vermedi.");

                await BaglantiyiKapatGuvenliAsync();

                return false;
            }
            catch (Exception exception)
            {
                kronometre.Stop();

                Kayit.Durum =
                    SirketDurumu.Hatali;

                Kayit.BasarisizKontrolSayisi++;

                KonsolKayitcisi.Uyari(
                    $"{Kayit.SirketAdi} sağlık kontrolü " +
                    $"başarısız: {exception.Message}");

                await BaglantiyiKapatGuvenliAsync();

                return false;
            }
        }
        finally
        {
            _istekCevapKilidi.Release();
        }
    }

    public async Task<IsSonucuMesaji>
        IsIstegiGonderVeSonucuBekleAsync(
            IsIstegiMesaji isIstegi,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            isIstegi);

        DisposeEdilmediginiDogrula();

        isIstegi.Dogrula();

        if (!Bagli)
        {
            throw new InvalidOperationException(
                $"{Kayit.SirketAdi} bağlantısı açık değil.");
        }

        if (!string.Equals(
                Kayit.SirketKimligi,
                _sirketAyari.SirketKimligi,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Bağlantıdaki şirket kimliği geçersiz.");
        }

        await _istekCevapKilidi.WaitAsync(
            cancellationToken);

        Stopwatch kronometre =
            Stopwatch.StartNew();

        try
        {
            if (!Bagli)
            {
                throw new InvalidOperationException(
                    $"{Kayit.SirketAdi} bağlantısı kapandı.");
            }

            KonsolKayitcisi.Bilgi(
                $"İş gönderiliyor | " +
                $"Şirket: {Kayit.SirketAdi} | " +
                $"İş: {isIstegi.IsKimligi} | " +
                $"Hizmet: " +
                $"{isIstegi.HizmetKimligi}@" +
                $"{isIstegi.HizmetSurumu} | " +
                $"Tutar: " +
                $"{isIstegi.TeklifEdilenTutar:N2}");

            await MesajGonderAsync(
                isIstegi,
                cancellationToken);

            string cevapSatiri =
                await MesajOkuAsync(
                    isIstegi.ZamanAsimiMs,
                    cancellationToken);

            kronometre.Stop();

            IsSonucuMesaji? sonuc =
                JsonSerializer
                    .Deserialize<IsSonucuMesaji>(
                        cevapSatiri,
                        JsonAyarlari);

            if (sonuc is null)
            {
                throw new InvalidOperationException(
                    "Şirket iş sonucu ayrıştırılamadı.");
            }

            sonuc.Dogrula();

            IsSonucuMesajiniDogrula(
                isIstegi,
                sonuc);

            Kayit.SonCevapZamani =
                DateTimeOffset.UtcNow;

            Kayit.SonGecikmeMs =
                kronometre
                    .Elapsed
                    .TotalMilliseconds;

            if (Kayit.Durum ==
                SirketDurumu.Bagli)
            {
                Kayit.Durum =
                    SirketDurumu.Calisiyor;
            }

            KonsolKayitcisi.Bilgi(
                $"İş cevabı alındı | " +
                $"Şirket: {Kayit.SirketAdi} | " +
                $"İş: {isIstegi.IsKimligi} | " +
                $"Başarılı bildirimi: " +
                $"{sonuc.Basarili} | " +
                $"Gerçek ağ süresi: " +
                $"{kronometre.Elapsed.TotalMilliseconds:N2} ms | " +
                $"Şirketin bildirdiği süre: " +
                $"{sonuc.IslemSuresiMs:N2} ms");

            return sonuc;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            kronometre.Stop();

            Kayit.Durum =
                SirketDurumu.CevapVermiyor;

            Kayit.BasarisizKontrolSayisi++;

            KonsolKayitcisi.Uyari(
                $"İş zaman aşımına uğradı | " +
                $"Şirket: {Kayit.SirketAdi} | " +
                $"İş: {isIstegi.IsKimligi} | " +
                $"Sınır: {isIstegi.ZamanAsimiMs} ms");

            await BaglantiyiKapatGuvenliAsync();

            throw new TimeoutException(
                $"{Kayit.SirketAdi}, " +
                $"{isIstegi.IsKimligi} işine " +
                $"{isIstegi.ZamanAsimiMs} ms içinde " +
                "cevap vermedi.");
        }
        catch
        {
            kronometre.Stop();

            throw;
        }
        finally
        {
            _istekCevapKilidi.Release();
        }
    }

    public async Task FinansDurumuGonderAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        DisposeEdilmediginiDogrula();

        if (!Bagli)
        {
            return;
        }

        FinansDurumuMesaji mesaj =
            new()
            {
                MesajTuru =
                    MesajTurleri.FinansDurumu,

                MesajKimligi =
                    YeniMesajKimligi(),

                ProtokolSurumu =
                    _motorAyarlari.ProtokolSurumu,

                SirketKimligi =
                    Kayit.SirketKimligi,

                TickNumarasi =
                    tickNumarasi,

                Kasa =
                    Kayit.Kasa,

                ToplamGelir =
                    Kayit.ToplamGelir,

                ToplamIade =
                    Kayit.ToplamIade,

                ToplamCeza =
                    Kayit.ToplamCeza,

                BekleyenOdeme =
                    Kayit.BekleyenOdeme,

                NetGelir =
                    Kayit.NetGelir,

                TamamlananIsSayisi =
                    Kayit.TamamlananIsSayisi,

                BasarisizIsSayisi =
                    Kayit.BasarisizIsSayisi,

                ZamanAsiminaUgrayanIsSayisi =
                    Kayit.ZamanAsiminaUgrayanIsSayisi,

                IptalEdilenIsSayisi =
                    Kayit.IptalEdilenIsSayisi,

                ItibarPuani =
                    Kayit.ItibarPuani,

                GuvenilirlikPuani =
                    Kayit.GuvenilirlikPuani,

                OrtalamaMusteriMemnuniyeti =
                    Kayit.OrtalamaMusteriMemnuniyeti,

                GuncellenmeZamani =
                    DateTimeOffset.UtcNow
            };

        mesaj.Dogrula();

        await _istekCevapKilidi.WaitAsync(
            cancellationToken);

        try
        {
            if (!Bagli)
            {
                return;
            }

            await MesajGonderAsync(
                mesaj,
                cancellationToken);

            KonsolKayitcisi.Bilgi(
                $"Finans durumu gönderildi | " +
                $"Şirket: {Kayit.SirketAdi} | " +
                $"Tick: {tickNumarasi} | " +
                $"Kasa: {Kayit.Kasa:N2} | " +
                $"Net gelir: {Kayit.NetGelir:N2}");
        }
        finally
        {
            _istekCevapKilidi.Release();
        }
    }

    public async Task BaglantiyiKapatAsync()
    {
        await _baglantiKilidi.WaitAsync();

        try
        {
            await BaglantiyiKapatIcAsync();
        }
        finally
        {
            _baglantiKilidi.Release();
        }
    }

    private async Task MerhabaGonderAsync(
        CancellationToken cancellationToken)
    {
        MerhabaMesaji mesaj =
            new()
            {
                MesajTuru =
                    MesajTurleri.Merhaba,

                MesajKimligi =
                    YeniMesajKimligi(),

                ProtokolSurumu =
                    _motorAyarlari
                        .ProtokolSurumu,

                MotorKimligi =
                    _motorAyarlari
                        .MotorKimligi
            };

        await MesajGonderAsync(
            mesaj,
            cancellationToken);
    }

    private async Task<SirketTanitimMesaji>
        SirketTanitiminiOkuAsync(
            CancellationToken cancellationToken)
    {
        string mesajSatiri =
            await MesajOkuAsync(
                _motorAyarlari.MesajZamanAsimiMs,
                cancellationToken);

        SirketTanitimMesaji? tanitim =
            JsonSerializer
                .Deserialize<SirketTanitimMesaji>(
                    mesajSatiri,
                    JsonAyarlari);

        if (tanitim is null)
        {
            throw new InvalidOperationException(
                "Şirket tanıtım mesajı okunamadı.");
        }

        if (!string.Equals(
                tanitim.MesajTuru,
                MesajTurleri.SirketTanitim,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Şirket tanıtımı beklenirken " +
                $"{tanitim.MesajTuru} mesajı geldi.");
        }

        return tanitim;
    }

    private void SirketTanitiminiDogrula(
        SirketTanitimMesaji tanitim)
    {
        if (!string.Equals(
                tanitim.SirketKimligi,
                _sirketAyari.SirketKimligi,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Şirket kimliği uyuşmuyor. " +
                $"Beklenen: " +
                $"{_sirketAyari.SirketKimligi}, " +
                $"gelen: {tanitim.SirketKimligi}");
        }

        if (string.IsNullOrWhiteSpace(
                tanitim.SirketAdi))
        {
            throw new InvalidOperationException(
                "Şirket adı boş olamaz.");
        }

        if (tanitim.Hizmetler is null)
        {
            throw new InvalidOperationException(
                "Şirket hizmet listesi göndermedi.");
        }
    }

    private void SirketKaydiniGuncelle(
        SirketTanitimMesaji tanitim)
    {
        Kayit.SirketKimligi =
            tanitim.SirketKimligi.Trim();

        Kayit.SirketAdi =
            tanitim.SirketAdi.Trim();

        Kayit.SunucuSurumu =
            tanitim.SunucuSurumu?.Trim() ??
            string.Empty;

        Kayit.Hizmetler =
            HizmetleriTemizle(
                tanitim.Hizmetler);

        Kayit.Durum =
            SirketDurumu.Bagli;

        Kayit.SonCevapZamani =
            DateTimeOffset.UtcNow;
    }

    private async Task KayitSonucuGonderAsync(
        bool basarili,
        string aciklama,
        CancellationToken cancellationToken)
    {
        KayitSonucuMesaji mesaj =
            new()
            {
                MesajTuru =
                    MesajTurleri.KayitSonucu,

                MesajKimligi =
                    YeniMesajKimligi(),

                ProtokolSurumu =
                    _motorAyarlari
                        .ProtokolSurumu,

                Basarili =
                    basarili,

                SirketKimligi =
                    _sirketAyari
                        .SirketKimligi,

                Aciklama =
                    aciklama
            };

        await MesajGonderAsync(
            mesaj,
            cancellationToken);
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
            JsonSerializer.Serialize(
                mesaj,
                JsonAyarlari);

        int mesajBoyutu =
            Encoding.UTF8.GetByteCount(
                json);

        if (mesajBoyutu >
            _motorAyarlari.AzamiMesajBoyutuByte)
        {
            throw new InvalidOperationException(
                $"Mesaj boyutu sınırı aşıldı: " +
                $"{mesajBoyutu} byte.");
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        await _yazici.WriteLineAsync(
            json.AsMemory(),
            cancellationToken);

        await _yazici.FlushAsync(
            cancellationToken);
    }

    private async Task<string> MesajOkuAsync(
        int zamanAsimiMs,
        CancellationToken cancellationToken)
    {
        if (_okuyucu is null)
        {
            throw new InvalidOperationException(
                "Şirket bağlantısı açık değil.");
        }

        if (zamanAsimiMs <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(zamanAsimiMs),
                "Mesaj zaman aşımı sıfırdan büyük olmalıdır.");
        }

        using CancellationTokenSource zamanAsimi =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        zamanAsimi.CancelAfter(
            TimeSpan.FromMilliseconds(
                zamanAsimiMs));

        string? satir =
            await _okuyucu.ReadLineAsync(
                zamanAsimi.Token);

        if (satir is null)
        {
            throw new IOException(
                "Şirket bağlantıyı kapattı.");
        }

        int mesajBoyutu =
            Encoding.UTF8.GetByteCount(
                satir);

        if (mesajBoyutu >
            _motorAyarlari.AzamiMesajBoyutuByte)
        {
            throw new InvalidOperationException(
                $"Gelen mesaj çok büyük: " +
                $"{mesajBoyutu} byte.");
        }

        if (string.IsNullOrWhiteSpace(
                satir))
        {
            throw new InvalidOperationException(
                "Şirket boş mesaj gönderdi.");
        }

        return satir;
    }

    private void IsSonucuMesajiniDogrula(
        IsIstegiMesaji istek,
        IsSonucuMesaji sonuc)
    {
        if (!string.Equals(
                sonuc.MesajTuru,
                MesajTurleri.IsSonucu,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"İş sonucu beklenirken " +
                $"{sonuc.MesajTuru} mesajı geldi.");
        }

        if (!string.Equals(
                sonuc.IstekKimligi,
                istek.IstekKimligi,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"İş sonucunun istek kimliği uyuşmuyor. " +
                $"Beklenen: {istek.IstekKimligi} | " +
                $"Gelen: {sonuc.IstekKimligi}");
        }

        if (!string.Equals(
                sonuc.IsKimligi,
                istek.IsKimligi,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"İş sonucunun iş kimliği uyuşmuyor. " +
                $"Beklenen: {istek.IsKimligi} | " +
                $"Gelen: {sonuc.IsKimligi}");
        }

        if (!string.Equals(
                sonuc.SirketKimligi,
                Kayit.SirketKimligi,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"İş sonucunun şirket kimliği uyuşmuyor. " +
                $"Beklenen: {Kayit.SirketKimligi} | " +
                $"Gelen: {sonuc.SirketKimligi}");
        }
    }

    private static List<SunulanHizmet> HizmetleriTemizle(
        IEnumerable<SunulanHizmet>? hizmetler)
    {
        if (hizmetler is null)
        {
            return [];
        }

        Dictionary<string, SunulanHizmet> temizHizmetler =
            new(
                StringComparer.OrdinalIgnoreCase);

        foreach (SunulanHizmet hizmet in
                 hizmetler)
        {
            if (hizmet is null ||
                string.IsNullOrWhiteSpace(
                    hizmet.HizmetKimligi) ||
                string.IsNullOrWhiteSpace(
                    hizmet.HizmetSurumu) ||
                hizmet.BirimFiyat <= 0 ||
                hizmet.AzamiEszamanliIs <= 0)
            {
                continue;
            }

            string hizmetKimligi =
                hizmet.HizmetKimligi.Trim();

            string hizmetSurumu =
                hizmet.HizmetSurumu.Trim();

            string anahtar =
                $"{hizmetKimligi}@{hizmetSurumu}";

            temizHizmetler[anahtar] =
                new SunulanHizmet
                {
                    HizmetKimligi =
                        hizmetKimligi,

                    HizmetSurumu =
                        hizmetSurumu,

                    BirimFiyat =
                        decimal.Round(
                            hizmet.BirimFiyat,
                            2),

                    AzamiEszamanliIs =
                        hizmet.AzamiEszamanliIs,

                    Aktif =
                        hizmet.Aktif
                };
        }

        return temizHizmetler
            .Values
            .OrderBy(
                hizmet => hizmet.HizmetKimligi,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                hizmet => hizmet.HizmetSurumu,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string YeniMesajKimligi()
    {
        return
            $"mesaj-{Guid.NewGuid():N}";
    }

    private void DisposeEdilmediginiDogrula()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private async Task BaglantiyiKapatGuvenliAsync()
    {
        await _baglantiKilidi.WaitAsync();

        try
        {
            await BaglantiyiKapatIcAsync();
        }
        finally
        {
            _baglantiKilidi.Release();
        }
    }

    private async Task BaglantiyiKapatIcAsync()
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
            // Kapatma sırasında yazıcı hatası yok sayılır.
        }

        try
        {
            _okuyucu?.Dispose();
        }
        catch
        {
            // Kapatma sırasında okuyucu hatası yok sayılır.
        }

        try
        {
            _tcpClient?.Dispose();
        }
        catch
        {
            // Kapatma sırasında TCP hatası yok sayılır.
        }

        _yazici =
            null;

        _okuyucu =
            null;

        _tcpClient =
            null;

        if (Kayit.Durum is
            SirketDurumu.Bagli or
            SirketDurumu.Calisiyor or
            SirketDurumu.Baglaniyor)
        {
            Kayit.Durum =
                SirketDurumu.BagliDegil;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        await _baglantiKilidi.WaitAsync();

        try
        {
            await BaglantiyiKapatIcAsync();
        }
        finally
        {
            _baglantiKilidi.Release();

            _istekCevapKilidi.Dispose();

            _baglantiKilidi.Dispose();
        }
    }
}
