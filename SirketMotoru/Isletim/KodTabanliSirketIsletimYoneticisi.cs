using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SirketMotoru.Kayit;
using SirketMotoru.Protokol;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class KodTabanliSirketIsletimYoneticisi : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonAyarlari = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly UygulamaStandardi[] Standartlar =
    [
        new()
        {
            Kategori = "sosyal-medya",
            Ad = "Sosyal Medya Platformu",
            ZorunluOzellikler =
            [
                "kimlik.dogrula",
                "sosyal.profil.getir",
                "sosyal.gonderi.olustur",
                "sosyal.akisi.getir",
                "sosyal.etkilesim"
            ],
            OpsiyonelOzellikler =
            [
                "sosyal.yorum",
                "sosyal.mesajlasma",
                "sosyal.moderasyon",
                "sosyal.bildirim",
                "sosyal.arama"
            ]
        },
        new()
        {
            Kategori = "eposta",
            Ad = "E-posta Platformu",
            ZorunluOzellikler =
            [
                "kimlik.dogrula",
                "eposta.gonder",
                "eposta.gelen-kutusu",
                "eposta.ara",
                "eposta.spam-kontrol"
            ],
            OpsiyonelOzellikler =
            [
                "eposta.ek-yukle",
                "eposta.klasor",
                "eposta.filtre",
                "eposta.takvim-baglantisi"
            ]
        },
        new()
        {
            Kategori = "mesajlasma",
            Ad = "Mesajlaşma Platformu",
            ZorunluOzellikler =
            [
                "kimlik.dogrula",
                "mesaj.gonder",
                "mesaj.sohbet-getir",
                "mesaj.teslim-durumu",
                "mesaj.kullanici-durumu"
            ],
            OpsiyonelOzellikler =
            [
                "mesaj.grup",
                "mesaj.dosya",
                "mesaj.arama",
                "mesaj.sifreleme"
            ]
        },
        new()
        {
            Kategori = "bulut-depolama",
            Ad = "Bulut Depolama Platformu",
            ZorunluOzellikler =
            [
                "kimlik.dogrula",
                "dosya.yukle",
                "dosya.indir",
                "dosya.listele",
                "dosya.sil"
            ],
            OpsiyonelOzellikler =
            [
                "dosya.paylas",
                "dosya.surumle",
                "dosya.ara",
                "dosya.yedekle"
            ]
        },
        new()
        {
            Kategori = "e-ticaret",
            Ad = "E-ticaret Platformu",
            ZorunluOzellikler =
            [
                "kimlik.dogrula",
                "urun.listele",
                "sepet.guncelle",
                "siparis.olustur",
                "odeme.dogrula"
            ],
            OpsiyonelOzellikler =
            [
                "stok.yonet",
                "kargo.takip",
                "iade.olustur",
                "kampanya.uygula"
            ]
        },
        new()
        {
            Kategori = "api",
            Ad = "API Ürünü",
            ZorunluOzellikler =
            [
                "kimlik.dogrula",
                "api.istek-isle",
                "api.kullanim-olc",
                "api.hata-yonet"
            ],
            OpsiyonelOzellikler =
            [
                "api.oran-sinirla",
                "api.webhook",
                "api.anahtar-yonet"
            ]
        },
        new()
        {
            Kategori = "analitik",
            Ad = "Analitik Platformu",
            ZorunluOzellikler =
            [
                "kimlik.dogrula",
                "analitik.veri-al",
                "analitik.sorgula",
                "analitik.raporla"
            ],
            OpsiyonelOzellikler =
            [
                "analitik.gosterge-paneli",
                "analitik.disari-aktar",
                "analitik.alarm"
            ]
        },
        new()
        {
            Kategori = "guvenlik",
            Ad = "Güvenlik Platformu",
            ZorunluOzellikler =
            [
                "kimlik.dogrula",
                "guvenlik.tara",
                "guvenlik.olay-kaydet",
                "guvenlik.risk-puanla"
            ],
            OpsiyonelOzellikler =
            [
                "guvenlik.engelle",
                "guvenlik.uyari",
                "guvenlik.raporla"
            ]
        },
        new()
        {
            Kategori = "gelistirici-araci",
            Ad = "Geliştirici Aracı",
            ZorunluOzellikler =
            [
                "kimlik.dogrula",
                "gelistirici.proje-isle",
                "gelistirici.sonuc-getir"
            ],
            OpsiyonelOzellikler =
            [
                "gelistirici.eklenti",
                "gelistirici.isbirligi",
                "gelistirici.surumle"
            ]
        }
    ];

    private readonly SirketIsletimYoneticisi _temel;
    private readonly SirketYoneticisi _sirketler;
    private readonly string _dosyaYolu;
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private KodTabanliYayinDosyasi _yayinlar = new();
    private bool _baslatildi;
    private bool _disposed;

    public KodTabanliSirketIsletimYoneticisi(
        SirketYoneticisi sirketler,
        string motorVerileriKlasoru)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        _temel = new SirketIsletimYoneticisi(sirketler, motorVerileriKlasoru);
        _dosyaYolu = Path.Combine(
            Path.GetFullPath(motorVerileriKlasoru),
            "kod-tabanli-yayinlar.json");
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        await _temel.BaslatAsync(cancellationToken);
        await _kilit.WaitAsync(cancellationToken);

        try
        {
            if (_baslatildi)
            {
                return;
            }

            if (File.Exists(_dosyaYolu))
            {
                string json = await File.ReadAllTextAsync(
                    _dosyaYolu,
                    cancellationToken);

                _yayinlar = JsonSerializer.Deserialize<KodTabanliYayinDosyasi>(
                                json,
                                JsonAyarlari)
                            ?? new KodTabanliYayinDosyasi();
            }

            KayitlariTamamla();
            HizmetEzmeDegerleriniUygula();
            await KaydetKilitsizAsync(cancellationToken);
            _baslatildi = true;

            KonsolKayitcisi.Basari(
                "Kod tabanlı yayın sistemi hazır. " +
                "8090 yalnız sunucunun ilan ettiği uygulama, hizmet ve " +
                "protokolleri ticari olarak yönetecek.");
        }
        finally
        {
            _kilit.Release();
        }
    }

    public Task<IslemSonucu> GirisDogrulaAsync(
        GirisIstegi istek,
        CancellationToken cancellationToken) =>
        _temel.GirisDogrulaAsync(istek, cancellationToken);

    public Task<IslemSonucu> ParolaDegistirAsync(
        string sirketKimligi,
        ParolaDegistirIstegi istek,
        CancellationToken cancellationToken) =>
        _temel.ParolaDegistirAsync(sirketKimligi, istek, cancellationToken);

    public Task<IslemSonucu> YatirimSatinAlAsync(
        string sirketKimligi,
        YatirimIstegi istek,
        CancellationToken cancellationToken) =>
        _temel.YatirimSatinAlAsync(sirketKimligi, istek, cancellationToken);

    public Task<IslemSonucu> KrediCekAsync(
        string sirketKimligi,
        KrediIstegi istek,
        CancellationToken cancellationToken) =>
        _temel.KrediCekAsync(sirketKimligi, istek, cancellationToken);

    public Task<IslemSonucu> HizmetFiyatiGuncelleAsync(
        string sirketKimligi,
        FiyatGuncelleIstegi istek,
        CancellationToken cancellationToken) =>
        _temel.HizmetFiyatiGuncelleAsync(
            sirketKimligi,
            istek,
            cancellationToken);

    public async Task<IslemSonucu> HizmetYayinDurumuGuncelleAsync(
        string sirketKimligi,
        HizmetYayinDurumuIstegi istek,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);

        try
        {
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            SunulanHizmet? hizmet = sirket.Hizmetler.FirstOrDefault(h =>
                string.Equals(
                    h.HizmetKimligi,
                    istek.HizmetKimligi?.Trim(),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    h.HizmetSurumu,
                    istek.HizmetSurumu?.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (hizmet is null)
            {
                return IslemSonucu.Hata(
                    "Bu hizmet şirket sunucusunun son tanıtım mesajında yok.");
            }

            string anahtar = HizmetAnahtari(
                hizmet.HizmetKimligi,
                hizmet.HizmetSurumu);
            SirketYayinAyarlari ayarlar = Ayarlar(sirketKimligi);
            ayarlar.HizmetAktiflikEzmeDegerleri[anahtar] = istek.Aktif;
            hizmet.Aktif = istek.Aktif;

            await KaydetKilitsizAsync(cancellationToken);
            await _sirketler.BilancolariKaydetAsync(cancellationToken);

            return IslemSonucu.Basari(
                istek.Aktif
                    ? "Hizmet ticari yayına alındı."
                    : "Hizmet ticari olarak durduruldu.");
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async Task<IslemSonucu> UygulamaYayinlaAsync(
        string sirketKimligi,
        UygulamaYayinlaIstegi istek,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);

        try
        {
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            if (!sirket.BagliMi)
            {
                return IslemSonucu.Hata(
                    "Uygulama piyasaya açılırken şirket sunucusu bağlı olmalıdır.");
            }

            SunucuYayinManifesti manifest =
                SunucuYayinManifestDeposu.Getir(sirketKimligi);
            SunulanUygulama? uygulama = manifest.Uygulamalar.FirstOrDefault(u =>
                string.Equals(
                    u.UygulamaKimligi,
                    istek.UygulamaKimligi?.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (uygulama is null)
            {
                return IslemSonucu.Hata(
                    "Uygulama sunucu tarafından koddan ilan edilmiyor.");
            }

            UygulamaDogrulamaSonucu dogrulama =
                UygulamayiDogrula(sirket, uygulama);

            if (!dogrulama.Gecerli)
            {
                return IslemSonucu.Hata(
                    "Uygulama standardı geçemedi: " +
                    string.Join(" | ", dogrulama.Hatalar));
            }

            SirketYayinAyarlari ayarlar = Ayarlar(sirketKimligi);
            if (ayarlar.UygulamaUrunEslemeleri.ContainsKey(
                    uygulama.UygulamaKimligi))
            {
                return IslemSonucu.Hata(
                    "Bu kodlanmış uygulama zaten piyasaya açılmış.");
            }

            UygulamaOzelligi? anaOzellik = AnaOzelligiBul(uygulama);
            if (anaOzellik is null)
            {
                return IslemSonucu.Hata(
                    "Uygulamanın çalıştırılabilir bir özellik hizmeti yok.");
            }

            UrunOlusturIstegi urunIstegi = new()
            {
                UrunAdi = uygulama.UygulamaAdi,
                Kategori = uygulama.Kategori,
                FiyatlandirmaModeli = istek.FiyatlandirmaModeli,
                AbonelikUcreti = istek.AbonelikUcreti,
                KullanimBasinaUcret = istek.KullanimBasinaUcret,
                ArkaUcHizmetKimligi = anaOzellik.HizmetKimligi,
                ArkaUcHizmetSurumu = anaOzellik.HizmetSurumu,
                ProtokolKimligi = uygulama.DesteklenenProtokoller.FirstOrDefault()
                                   ?? string.Empty
            };

            IslemSonucu sonuc = await _temel.UrunOlusturAsync(
                sirketKimligi,
                urunIstegi,
                cancellationToken);

            if (!sonuc.Basarili)
            {
                return sonuc;
            }

            if (sonuc.Veri is not UrunKaydi urun)
            {
                return IslemSonucu.Hata(
                    "Uygulama kaydı oluşturuldu fakat ürün kimliği alınamadı.");
            }

            ayarlar.UygulamaUrunEslemeleri[uygulama.UygulamaKimligi] =
                urun.UrunKimligi;
            await KaydetKilitsizAsync(cancellationToken);

            return IslemSonucu.Basari(
                "Sunucuda kodlanan uygulama piyasaya açıldı.",
                new
                {
                    uygulama.UygulamaKimligi,
                    urun.UrunKimligi,
                    dogrulama.Durum
                });
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async Task<IslemSonucu> UygulamaGuncelleAsync(
        string sirketKimligi,
        UrunGuncelleIstegi istek,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);

        try
        {
            if (!Ayarlar(sirketKimligi)
                    .UygulamaUrunEslemeleri
                    .Values
                    .Contains(istek.UrunKimligi, StringComparer.OrdinalIgnoreCase))
            {
                return IslemSonucu.Hata(
                    "Bu ürün koddan ilan edilmiş bir uygulamayla eşleşmiyor.");
            }

            return await _temel.UrunGuncelleAsync(
                sirketKimligi,
                istek,
                cancellationToken);
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async Task<IslemSonucu> UygulamaKapasitesiArtirAsync(
        string sirketKimligi,
        UrunKapasiteIstegi istek,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);

        try
        {
            if (!Ayarlar(sirketKimligi)
                    .UygulamaUrunEslemeleri
                    .Values
                    .Contains(istek.UrunKimligi, StringComparer.OrdinalIgnoreCase))
            {
                return IslemSonucu.Hata(
                    "Kapasite yalnız koddan ilan edilmiş uygulamalara alınabilir.");
            }

            return await _temel.UrunKapasitesiArtirAsync(
                sirketKimligi,
                istek,
                cancellationToken);
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async Task<IslemSonucu> ProtokolYayinlaAsync(
        string sirketKimligi,
        ProtokolYayinlaIstegi istek,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);

        try
        {
            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            if (!sirket.BagliMi)
            {
                return IslemSonucu.Hata(
                    "Protokol piyasaya açılırken şirket sunucusu bağlı olmalıdır.");
            }

            SunucuYayinManifesti manifest =
                SunucuYayinManifestDeposu.Getir(sirketKimligi);
            SunulanOzelProtokol? protokol = manifest.OzelProtokoller.FirstOrDefault(p =>
                string.Equals(
                    p.ProtokolKimligi,
                    istek.ProtokolKimligi?.Trim(),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    p.Surum,
                    istek.Surum?.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (protokol is null)
            {
                return IslemSonucu.Hata(
                    "Protokol şirket sunucusu tarafından koddan ilan edilmiyor.");
            }

            List<string> eksikler = protokol.Yetkinlikler
                .Where(yetkinlik => !HizmetVarMi(
                    sirket,
                    yetkinlik.HizmetKimligi,
                    yetkinlik.HizmetSurumu))
                .Select(yetkinlik =>
                    $"{yetkinlik.YetkinlikKimligi} -> " +
                    $"{yetkinlik.HizmetKimligi}@{yetkinlik.HizmetSurumu}")
                .ToList();

            if (eksikler.Count > 0)
            {
                return IslemSonucu.Hata(
                    "Protokol yetkinliklerinin hizmet kodları eksik: " +
                    string.Join(" | ", eksikler));
            }

            string anahtar = ProtokolAnahtari(
                protokol.ProtokolKimligi,
                protokol.Surum);
            SirketYayinAyarlari ayarlar = Ayarlar(sirketKimligi);

            if (ayarlar.ProtokolPiyasaEslemeleri.ContainsKey(anahtar))
            {
                return IslemSonucu.Hata(
                    "Bu kodlanmış protokol sürümü zaten piyasada.");
            }

            IslemSonucu sonuc = await _temel.ProtokolOlusturAsync(
                sirketKimligi,
                new ProtokolOlusturIstegi
                {
                    ProtokolAdi = protokol.ProtokolAdi,
                    Surum = protokol.Surum,
                    Aciklama = protokol.Aciklama,
                    LisansModeli = istek.LisansModeli,
                    BenimsemeBedeli = istek.BenimsemeBedeli,
                    TickLisansBedeli = istek.TickLisansBedeli
                },
                cancellationToken);

            if (!sonuc.Basarili)
            {
                return sonuc;
            }

            if (sonuc.Veri is not OzelProtokolKaydi piyasaProtokolu)
            {
                return IslemSonucu.Hata(
                    "Protokol yayınlandı fakat piyasa kimliği alınamadı.");
            }

            ayarlar.ProtokolPiyasaEslemeleri[anahtar] =
                piyasaProtokolu.ProtokolKimligi;
            await KaydetKilitsizAsync(cancellationToken);

            return IslemSonucu.Basari(
                "Sunucuda kodlanan özel protokol piyasaya açıldı.",
                new
                {
                    teknikProtokolKimligi = protokol.ProtokolKimligi,
                    piyasaProtokolu.ProtokolKimligi,
                    protokol.Surum
                });
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async Task<IslemSonucu> ProtokolBenimseAsync(
        string sirketKimligi,
        ProtokolBenimseIstegi istek,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);

        try
        {
            bool kodMevcut = _yayinlar.Sirketler.Values.Any(ayarlar =>
                ayarlar.ProtokolPiyasaEslemeleri.Values.Contains(
                    istek.ProtokolKimligi,
                    StringComparer.OrdinalIgnoreCase));

            if (!kodMevcut)
            {
                return IslemSonucu.Hata(
                    "Bu protokol kod tabanlı bir sunucu manifestine bağlı değil.");
            }

            return await _temel.ProtokolBenimseAsync(
                sirketKimligi,
                istek,
                cancellationToken);
        }
        finally
        {
            _kilit.Release();
        }
    }

    public Task<IslemSonucu> SozlesmeKabulEtAsync(
        string sirketKimligi,
        SozlesmeKabulIstegi istek,
        CancellationToken cancellationToken) =>
        _temel.SozlesmeKabulEtAsync(sirketKimligi, istek, cancellationToken);

    public async Task<string> PanelJsonuOlusturAsync(
        string sirketKimligi,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);

        try
        {
            HizmetEzmeDegerleriniUygula();
            string temelJson = await _temel.PanelJsonuOlusturAsync(
                sirketKimligi,
                cancellationToken);
            JsonObject kok = JsonNode.Parse(temelJson) as JsonObject
                             ?? new JsonObject();

            SirketKaydi sirket = SirketZorunlu(sirketKimligi);
            SunucuYayinManifesti manifest =
                SunucuYayinManifestDeposu.Getir(sirketKimligi);
            SirketYayinAyarlari ayarlar = Ayarlar(sirketKimligi);

            List<object> uygulamalar = manifest.Uygulamalar
                .Select(uygulama =>
                {
                    UygulamaDogrulamaSonucu dogrulama =
                        UygulamayiDogrula(sirket, uygulama);
                    ayarlar.UygulamaUrunEslemeleri.TryGetValue(
                        uygulama.UygulamaKimligi,
                        out string? urunKimligi);

                    return (object)new
                    {
                        uygulama,
                        dogrulama,
                        urunKimligi = urunKimligi ?? string.Empty,
                        piyasada = !string.IsNullOrWhiteSpace(urunKimligi),
                        sunucuBagli = sirket.BagliMi
                    };
                })
                .ToList();

            List<object> protokoller = manifest.OzelProtokoller
                .Select(protokol =>
                {
                    string anahtar = ProtokolAnahtari(
                        protokol.ProtokolKimligi,
                        protokol.Surum);
                    ayarlar.ProtokolPiyasaEslemeleri.TryGetValue(
                        anahtar,
                        out string? piyasaKimligi);
                    List<string> eksikler = protokol.Yetkinlikler
                        .Where(yetkinlik => !HizmetVarMi(
                            sirket,
                            yetkinlik.HizmetKimligi,
                            yetkinlik.HizmetSurumu))
                        .Select(yetkinlik => yetkinlik.YetkinlikKimligi)
                        .ToList();

                    return (object)new
                    {
                        protokol,
                        gecerli = eksikler.Count == 0,
                        eksikYetkinlikler = eksikler,
                        piyasaProtokolKimligi = piyasaKimligi ?? string.Empty,
                        piyasada = !string.IsNullOrWhiteSpace(piyasaKimligi)
                    };
                })
                .ToList();

            List<object> hizmetler = sirket.Hizmetler
                .OrderBy(h => h.HizmetKimligi, StringComparer.OrdinalIgnoreCase)
                .Select(hizmet => (object)new
                {
                    hizmet.HizmetKimligi,
                    hizmet.HizmetSurumu,
                    hizmet.BirimFiyat,
                    hizmet.AzamiEszamanliIs,
                    hizmet.Aktif,
                    uygulamalardaKullaniliyor = manifest.Uygulamalar.Any(u =>
                        u.Ozellikler.Any(o =>
                            string.Equals(
                                o.HizmetKimligi,
                                hizmet.HizmetKimligi,
                                StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(
                                o.HizmetSurumu,
                                hizmet.HizmetSurumu,
                                StringComparison.OrdinalIgnoreCase)))
                })
                .ToList();

            kok["kodTabanliYayinlar"] = JsonSerializer.SerializeToNode(
                new
                {
                    manifestGuncellenmeZamani = manifest.GuncellenmeZamani,
                    uygulamalar,
                    protokoller,
                    hizmetler,
                    uygulamaStandartlari = Standartlar,
                    kural =
                        "8090 kod veya özellik üretmez. Yalnız sunucunun " +
                        "sirketTanitim mesajında ilan ettiği teknik yayınları yönetir."
                },
                JsonAyarlari);

            return kok.ToJsonString(JsonAyarlari);
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async Task TickCalistirAsync(
        long tickNumarasi,
        CancellationToken cancellationToken)
    {
        await _kilit.WaitAsync(cancellationToken);

        try
        {
            HizmetEzmeDegerleriniUygula();
            await GecersizUygulamalariDurdurAsync(cancellationToken);
            await KaydetKilitsizAsync(cancellationToken);
        }
        finally
        {
            _kilit.Release();
        }

        await _temel.TickCalistirAsync(tickNumarasi, cancellationToken);
    }

    private async Task GecersizUygulamalariDurdurAsync(
        CancellationToken cancellationToken)
    {
        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
        {
            SirketYayinAyarlari ayarlar = Ayarlar(sirket.SirketKimligi);
            if (ayarlar.UygulamaUrunEslemeleri.Count == 0)
            {
                continue;
            }

            string panelJson = await _temel.PanelJsonuOlusturAsync(
                sirket.SirketKimligi,
                cancellationToken);
            JsonNode? kok = JsonNode.Parse(panelJson);
            JsonArray? urunler = kok?["isletim"]?["urunler"] as JsonArray;
            SunucuYayinManifesti manifest =
                SunucuYayinManifestDeposu.Getir(sirket.SirketKimligi);

            foreach ((string uygulamaKimligi, string urunKimligi) in
                     ayarlar.UygulamaUrunEslemeleri.ToList())
            {
                JsonObject? urun = urunler?
                    .OfType<JsonObject>()
                    .FirstOrDefault(u => string.Equals(
                        u["urunKimligi"]?.GetValue<string>(),
                        urunKimligi,
                        StringComparison.OrdinalIgnoreCase));

                if (urun is null)
                {
                    ayarlar.UygulamaUrunEslemeleri.Remove(uygulamaKimligi);
                    continue;
                }

                SunulanUygulama? uygulama = manifest.Uygulamalar.FirstOrDefault(u =>
                    string.Equals(
                        u.UygulamaKimligi,
                        uygulamaKimligi,
                        StringComparison.OrdinalIgnoreCase));
                bool teknikOlarakCalisiyor =
                    sirket.BagliMi &&
                    uygulama is not null &&
                    UygulamayiDogrula(sirket, uygulama).Gecerli &&
                    uygulama.Ozellikler
                        .Where(o => o.Zorunlu)
                        .All(o => HizmetAktifMi(
                            sirket,
                            o.HizmetKimligi,
                            o.HizmetSurumu));

                bool aktif = urun["aktif"]?.GetValue<bool>() ?? false;
                if (teknikOlarakCalisiyor || !aktif)
                {
                    continue;
                }

                await _temel.UrunGuncelleAsync(
                    sirket.SirketKimligi,
                    new UrunGuncelleIstegi
                    {
                        UrunKimligi = urunKimligi,
                        AbonelikUcreti =
                            urun["abonelikUcreti"]?.GetValue<decimal>() ?? 0,
                        KullanimBasinaUcret =
                            urun["kullanimBasinaUcret"]?.GetValue<decimal>() ?? 0,
                        Aktif = false
                    },
                    cancellationToken);

                KonsolKayitcisi.Uyari(
                    $"Uygulama teknik kod/özellik koşulunu kaybetti ve durduruldu | " +
                    $"Şirket: {sirket.SirketAdi} | Uygulama: {uygulamaKimligi}");
            }
        }
    }

    private UygulamaDogrulamaSonucu UygulamayiDogrula(
        SirketKaydi sirket,
        SunulanUygulama uygulama)
    {
        List<string> hatalar = [];
        List<string> uyarilar = [];
        UygulamaStandardi? standart = Standartlar.FirstOrDefault(s =>
            string.Equals(
                s.Kategori,
                uygulama.Kategori,
                StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<string> zorunlu =
            standart?.ZorunluOzellikler ?? ["kimlik.dogrula"];
        IReadOnlyList<string> opsiyonel =
            standart?.OpsiyonelOzellikler ?? [];

        if (standart is null)
        {
            uyarilar.Add(
                "Bu kategori için hazır uygulama standardı yok; temel kimlik " +
                "ve hizmet eşleşmesi uygulanıyor.");
        }

        HashSet<string> ozellikKimlikleri = uygulama.Ozellikler
            .Select(o => o.OzellikKimligi)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string gerekli in zorunlu)
        {
            if (!ozellikKimlikleri.Contains(gerekli))
            {
                hatalar.Add($"Zorunlu özellik eksik: {gerekli}");
            }
        }

        foreach (UygulamaOzelligi ozellik in uygulama.Ozellikler)
        {
            if (!HizmetVarMi(
                    sirket,
                    ozellik.HizmetKimligi,
                    ozellik.HizmetSurumu))
            {
                hatalar.Add(
                    $"{ozellik.OzellikKimligi} özelliğinin hizmet kodu ilan " +
                    $"edilmiyor: {ozellik.HizmetKimligi}@{ozellik.HizmetSurumu}");
            }
        }

        foreach (UygulamaBagimliligi bagimlilik in uygulama.Bagimliliklar)
        {
            SunucuYayinManifesti hedefManifest =
                SunucuYayinManifestDeposu.Getir(bagimlilik.SirketKimligi);
            SunulanUygulama? hedef = hedefManifest.Uygulamalar.FirstOrDefault(u =>
                string.Equals(
                    u.UygulamaKimligi,
                    bagimlilik.UygulamaKimligi,
                    StringComparison.OrdinalIgnoreCase));

            bool uygun = hedef is not null &&
                         SurumYeterliMi(hedef.Surum, bagimlilik.AsgariSurum);

            if (!uygun && bagimlilik.Zorunlu)
            {
                hatalar.Add(
                    $"Zorunlu uygulama bağlantısı yok: " +
                    $"{bagimlilik.SirketKimligi}/{bagimlilik.UygulamaKimligi} " +
                    $">= {bagimlilik.AsgariSurum}");
            }
            else if (!uygun)
            {
                uyarilar.Add(
                    $"Opsiyonel uygulama bağlantısı kullanılamıyor: " +
                    $"{bagimlilik.SirketKimligi}/{bagimlilik.UygulamaKimligi}");
            }
        }

        return new UygulamaDogrulamaSonucu
        {
            Gecerli = hatalar.Count == 0,
            Durum = hatalar.Count == 0
                ? "Sunucuda kodlanmış özellikler uygulama standardını karşılıyor."
                : "Uygulama standardı karşılanmıyor.",
            Hatalar = hatalar.AsReadOnly(),
            Uyarilar = uyarilar.AsReadOnly(),
            ZorunluOzellikler = zorunlu,
            OpsiyonelOzellikler = opsiyonel
        };
    }

    private static UygulamaOzelligi? AnaOzelligiBul(
        SunulanUygulama uygulama)
    {
        UygulamaStandardi? standart = Standartlar.FirstOrDefault(s =>
            string.Equals(
                s.Kategori,
                uygulama.Kategori,
                StringComparison.OrdinalIgnoreCase));

        foreach (string kimlik in standart?.ZorunluOzellikler ?? [])
        {
            UygulamaOzelligi? ozellik = uygulama.Ozellikler.FirstOrDefault(o =>
                string.Equals(
                    o.OzellikKimligi,
                    kimlik,
                    StringComparison.OrdinalIgnoreCase));

            if (ozellik is not null &&
                !string.Equals(
                    kimlik,
                    "kimlik.dogrula",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ozellik;
            }
        }

        return uygulama.Ozellikler.FirstOrDefault(o => o.Zorunlu)
               ?? uygulama.Ozellikler.FirstOrDefault();
    }

    private void KayitlariTamamla()
    {
        _yayinlar.Sirketler ??=
            new Dictionary<string, SirketYayinAyarlari>(
                StringComparer.OrdinalIgnoreCase);

        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
        {
            _ = Ayarlar(sirket.SirketKimligi);
        }
    }

    private void HizmetEzmeDegerleriniUygula()
    {
        foreach (SirketKaydi sirket in _sirketler.SirketKayitlari)
        {
            SirketYayinAyarlari ayarlar = Ayarlar(sirket.SirketKimligi);

            foreach (SunulanHizmet hizmet in sirket.Hizmetler)
            {
                string anahtar = HizmetAnahtari(
                    hizmet.HizmetKimligi,
                    hizmet.HizmetSurumu);

                if (ayarlar.HizmetAktiflikEzmeDegerleri.TryGetValue(
                        anahtar,
                        out bool aktif))
                {
                    hizmet.Aktif = aktif;
                }
            }
        }
    }

    private SirketYayinAyarlari Ayarlar(string sirketKimligi)
    {
        if (_yayinlar.Sirketler.TryGetValue(
                sirketKimligi,
                out SirketYayinAyarlari? ayarlar))
        {
            return ayarlar;
        }

        ayarlar = new SirketYayinAyarlari();
        _yayinlar.Sirketler[sirketKimligi] = ayarlar;
        return ayarlar;
    }

    private SirketKaydi SirketZorunlu(string sirketKimligi) =>
        _sirketler.SirketKayitlari.FirstOrDefault(s =>
            string.Equals(
                s.SirketKimligi,
                sirketKimligi,
                StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("Şirket bulunamadı.");

    private static bool HizmetVarMi(
        SirketKaydi sirket,
        string hizmetKimligi,
        string hizmetSurumu) =>
        sirket.Hizmetler.Any(h =>
            string.Equals(
                h.HizmetKimligi,
                hizmetKimligi,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                h.HizmetSurumu,
                hizmetSurumu,
                StringComparison.OrdinalIgnoreCase));

    private static bool HizmetAktifMi(
        SirketKaydi sirket,
        string hizmetKimligi,
        string hizmetSurumu) =>
        sirket.Hizmetler.Any(h =>
            h.Aktif &&
            string.Equals(
                h.HizmetKimligi,
                hizmetKimligi,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                h.HizmetSurumu,
                hizmetSurumu,
                StringComparison.OrdinalIgnoreCase));

    private static bool SurumYeterliMi(
        string mevcut,
        string asgari)
    {
        if (Version.TryParse(mevcut, out Version? mevcutSurum) &&
            Version.TryParse(asgari, out Version? asgariSurum))
        {
            return mevcutSurum >= asgariSurum;
        }

        return string.Compare(
                   mevcut,
                   asgari,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string HizmetAnahtari(
        string hizmetKimligi,
        string hizmetSurumu) =>
        $"{hizmetKimligi.Trim()}@{hizmetSurumu.Trim()}";

    private static string ProtokolAnahtari(
        string protokolKimligi,
        string surum) =>
        $"{protokolKimligi.Trim()}@{surum.Trim()}";

    private async Task KaydetKilitsizAsync(
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        _yayinlar.GuncellenmeZamani = DateTimeOffset.UtcNow;
        string gecici = _dosyaYolu + ".tmp";

        await File.WriteAllTextAsync(
            gecici,
            JsonSerializer.Serialize(_yayinlar, JsonAyarlari),
            new UTF8Encoding(false),
            cancellationToken);

        File.Move(gecici, _dosyaYolu, overwrite: true);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_baslatildi)
            {
                await _kilit.WaitAsync();
                try
                {
                    await KaydetKilitsizAsync(CancellationToken.None);
                }
                finally
                {
                    _kilit.Release();
                }
            }
        }
        finally
        {
            _disposed = true;
            _kilit.Dispose();
            await _temel.DisposeAsync();
        }
    }
}
