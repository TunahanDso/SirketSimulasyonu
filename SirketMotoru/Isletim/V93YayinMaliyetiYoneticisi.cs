using System.Reflection;
using System.Text;
using System.Text.Json;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Isletim;

public sealed class V93YayinMaliyetiDosyasi
{
    public int Surum { get; set; } = 93;
    public bool BaslangicEnvanteriAlindi { get; set; }
    public HashSet<string> UcretlendirilenUrunler { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UcretlendirilenProtokoller { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class V93YayinMaliyetiYoneticisi
{
    private readonly SirketYoneticisi _sirketler;
    private readonly object _temel;
    private readonly FieldInfo _veriAlani;
    private readonly FieldInfo _kilitAlani;
    private readonly MethodInfo _kaydetMetodu;
    private readonly string _dosyaYolu;
    private V93YayinMaliyetiDosyasi _veri = new();

    public V93YayinMaliyetiYoneticisi(
        SirketYoneticisi sirketler,
        KodTabanliSirketIsletimYoneticisi isletim,
        string motorVerileriKlasoru)
    {
        _sirketler = sirketler ?? throw new ArgumentNullException(nameof(sirketler));
        FieldInfo temelAlani = typeof(KodTabanliSirketIsletimYoneticisi)
            .GetField("_temel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.3 yayın maliyeti köprüsü kurulamadı.");
        _temel = temelAlani.GetValue(isletim)
            ?? throw new InvalidOperationException("V9.3 temel işletim yöneticisi boş.");
        Type tur = _temel.GetType();
        _veriAlani = tur.GetField("_veri", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.3 yayın maliyeti verisi bulunamadı.");
        _kilitAlani = tur.GetField("_kilit", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.3 yayın maliyeti kilidi bulunamadı.");
        _kaydetMetodu = tur.GetMethod("TumunuKaydetAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("V9.3 yayın maliyeti kayıt metodu bulunamadı.");
        _dosyaYolu = Path.Combine(Path.GetFullPath(motorVerileriKlasoru), "yayin-maliyetleri-v93.json");
    }

    public async Task BaslatAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_dosyaYolu))
        {
            string json = await File.ReadAllTextAsync(_dosyaYolu, cancellationToken);
            _veri = JsonSerializer.Deserialize<V93YayinMaliyetiDosyasi>(json) ?? new();
        }
        _veri.UcretlendirilenUrunler ??= new(StringComparer.OrdinalIgnoreCase);
        _veri.UcretlendirilenProtokoller ??= new(StringComparer.OrdinalIgnoreCase);

        if (!_veri.BaslangicEnvanteriAlindi)
        {
            SirketIsletimDosyasi dosya = IsletimVerisi();
            foreach (SirketIsletimDurumu durum in dosya.Sirketler)
                foreach (UrunKaydi urun in durum.Urunler)
                    _veri.UcretlendirilenUrunler.Add(urun.UrunKimligi);
            foreach (OzelProtokolKaydi protokol in dosya.Protokoller)
                _veri.UcretlendirilenProtokoller.Add(protokol.ProtokolKimligi);
            _veri.BaslangicEnvanteriAlindi = true;
            await DosyayiKaydetAsync(cancellationToken);
        }

        KonsolKayitcisi.Basari(
            "V9.3 yayın maliyetleri hazır | Yeni uygulama 6.000+, OS 18.000, protokol 8.000 TL toplam lansman yatırımı.");
    }

    public async Task UygulaAsync(long tick, CancellationToken cancellationToken)
    {
        SemaphoreSlim kilit = (SemaphoreSlim)(_kilitAlani.GetValue(_temel)
            ?? throw new InvalidOperationException("V9.3 yayın maliyeti kilidi boş."));
        await kilit.WaitAsync(cancellationToken);
        try
        {
            SirketIsletimDosyasi dosya = IsletimVerisi();
            bool degisti = false;
            foreach (SirketIsletimDurumu durum in dosya.Sirketler)
            {
                SirketKaydi? sirket = _sirketler.SirketKayitlari.FirstOrDefault(x =>
                    x.SirketKimligi.Equals(durum.SirketKimligi, StringComparison.OrdinalIgnoreCase));
                if (sirket is null) continue;

                foreach (UrunKaydi urun in durum.Urunler.Where(x => !_veri.UcretlendirilenUrunler.Contains(x.UrunKimligi)))
                {
                    decimal hedef = HedefUrunMaliyeti(urun.UrunTuru);
                    decimal eskiHedef = EskiUrunMaliyeti(urun.UrunTuru);
                    decimal ekBedel = Math.Max(0, hedef - eskiHedef);
                    if (sirket.Kasa < ekBedel)
                    {
                        urun.Aktif = false;
                        IslemEkle(durum, tick, "lansman-bekliyor",
                            $"{urun.UrunAdi} için {ekBedel:N2} TL ek lansman sermayesi yetersiz; ürün pasif bırakıldı.", 0);
                        continue;
                    }

                    sirket.Kasa -= ekBedel;
                    durum.ToplamYatirimHarcamasi += ekBedel;
                    urun.ToplamGider += ekBedel;
                    _veri.UcretlendirilenUrunler.Add(urun.UrunKimligi);
                    IslemEkle(durum, tick, "urun-lansman",
                        $"{urun.UrunAdi} toplam {hedef:N2} TL lansman yatırımıyla piyasaya çıktı.", -ekBedel);
                    degisti = true;
                }
            }

            foreach (OzelProtokolKaydi protokol in dosya.Protokoller
                         .Where(x => !_veri.UcretlendirilenProtokoller.Contains(x.ProtokolKimligi)))
            {
                SirketKaydi? sirket = _sirketler.SirketKayitlari.FirstOrDefault(x =>
                    x.SirketKimligi.Equals(protokol.SahipSirketKimligi, StringComparison.OrdinalIgnoreCase));
                SirketIsletimDurumu? durum = dosya.Sirketler.FirstOrDefault(x =>
                    x.SirketKimligi.Equals(protokol.SahipSirketKimligi, StringComparison.OrdinalIgnoreCase));
                if (sirket is null || durum is null) continue;
                const decimal hedef = 8_000m;
                const decimal eski = 500m;
                const decimal ek = hedef - eski;
                if (sirket.Kasa < ek)
                {
                    protokol.Aktif = false;
                    IslemEkle(durum, tick, "protokol-bekliyor",
                        $"{protokol.ProtokolAdi} için {ek:N2} TL ek geliştirme/yayın sermayesi yetersiz; protokol pasif.", 0);
                    continue;
                }

                sirket.Kasa -= ek;
                durum.ToplamYatirimHarcamasi += ek;
                _veri.UcretlendirilenProtokoller.Add(protokol.ProtokolKimligi);
                IslemEkle(durum, tick, "protokol-lansman",
                    $"{protokol.ProtokolAdi} toplam {hedef:N2} TL geliştirme ve yayın yatırımıyla piyasaya çıktı.", -ek);
                degisti = true;
            }

            if (degisti)
            {
                await IsletimiKaydetAsync(cancellationToken);
                await DosyayiKaydetAsync(cancellationToken);
            }
        }
        finally
        {
            kilit.Release();
        }
    }

    private SirketIsletimDosyasi IsletimVerisi() =>
        (SirketIsletimDosyasi)(_veriAlani.GetValue(_temel)
            ?? throw new InvalidOperationException("V9.3 işletim verisi boş."));

    private static decimal HedefUrunMaliyeti(string? tur) => (tur ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "isletim-sistemi" => 18_000m,
        "platform" => 10_000m,
        "altyapi" => 8_500m,
        "yapay-zeka" => 9_000m,
        _ => 6_000m
    };

    private static decimal EskiUrunMaliyeti(string? tur) => (tur ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "isletim-sistemi" => 3_000m,
        "platform" => 2_500m,
        "altyapi" => 2_000m,
        _ => 1_000m
    };

    private static void IslemEkle(
        SirketIsletimDurumu durum,
        long tick,
        string tur,
        string aciklama,
        decimal tutar)
    {
        durum.SonIslemler.Add(new IsletimIslemKaydi
        {
            IslemKimligi = $"v93-{Guid.NewGuid():N}",
            TickNumarasi = tick,
            IslemTuru = tur,
            Aciklama = aciklama,
            Tutar = tutar,
            Zaman = DateTimeOffset.Now
        });
        if (durum.SonIslemler.Count > 300)
            durum.SonIslemler.RemoveRange(0, durum.SonIslemler.Count - 300);
    }

    private async Task IsletimiKaydetAsync(CancellationToken cancellationToken)
    {
        object? sonuc = _kaydetMetodu.Invoke(_temel, [cancellationToken]);
        if (sonuc is Task gorev) await gorev;
    }

    private async Task DosyayiKaydetAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        string tmp = _dosyaYolu + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(_veri, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }), new UTF8Encoding(false), cancellationToken);
        File.Move(tmp, _dosyaYolu, overwrite: true);
    }
}
