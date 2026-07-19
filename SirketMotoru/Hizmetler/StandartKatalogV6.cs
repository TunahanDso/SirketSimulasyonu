using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SirketMotoru.Protokol;
using SirketMotoru.Sirketler;

namespace SirketMotoru.Hizmetler;

public sealed class UygulamaKategoriTanimi
{
    public string KategoriKimligi { get; init; } = string.Empty;
    public string KategoriAdi { get; init; } = string.Empty;
    public string Sektor { get; init; } = "genel";
    public string UrunTuru { get; init; } = "uygulama";
    public IReadOnlyList<string> ZorunluHizmetler { get; init; } = [];
    public IReadOnlySet<string> IzinliEkHizmetAileleri { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public int AsgariHizmetSayisi { get; init; }
    public int OnerilenHizmetSayisi { get; init; }
    public int AzamiHizmetSayisi { get; init; } = 40;
    public double EkHizmetKaliteKatkisi { get; init; } = 1.25;
    public double TabanKapasiteTuketimi { get; init; }
    public double KullaniciBasinaKapasiteTuketimi { get; init; }
}

public sealed class UygulamaKategoriDogrulamaSonucu
{
    public bool Gecerli { get; init; }
    public string KategoriKimligi { get; init; } = string.Empty;
    public IReadOnlyList<string> EksikZorunluHizmetler { get; init; } = [];
    public IReadOnlyList<string> UygunEkHizmetler { get; init; } = [];
    public IReadOnlyList<string> UygunsuzHizmetler { get; init; } = [];
    public double HizmetKalitePuani { get; init; }
    public double KapasiteTuketimPuani { get; init; }
}

public static class StandartKatalogV6
{
    private const string AilelerGzipBase64 = "H4sIAHIhXWoC/21ZSZLkOA78S56HH5ivjOUBkhgKFrUZl+hWtPXfxwFwi6w6VJRAUSSxuoP5z9dOyeKf81///d9XOq+Nvv7zNVO4+D/nKeD/6dzwu58LfnPEjz89frdzpeDSzl/sOW3Eg3d+UZBFdnfwL/2N3zMk2khn2uUmfoN5eODlYqJjoZBMpEvmUKTN8CHo2OQE/twnd1C8T/7ysgH7VWmxh9/sbubz/fWNZW1wrEvd0jwtlt361sPAbxuXMw1TjjPstLm3NTZBfLgtBbtZPK4hY8qxODGRC5uNSZ7pDq48LueRY8pBTHBYnLPMh9HdW5axPjpvlnNbsn7sXXBmypuojb2qnsGuwZbnNxQ7TISq0fFq8FvGRN0U6/X9cUro8c3KJzgEhvF2c7uFyjfvQIF8sqGIj2A9608H6+w+DurznL15Uniwvvnuwvm2Sabgs2eiYHQL3j/fax5WW9xmko2XY1vO9iWHvCjMJMYerTjZ423D5rxodLhHszUU8WXDiMPmII83vd1u/HmkIOG6BtqhVx+ApogGdyxscbbIwmeCQaILuj3sEE09le4fWflyosH3Nsp/jk+0WlH1z1ogANSvnm49vc8w/oOCZ3t4G+HBvX0kj/YwN3nJHjkzb+Td5kyVfL5D9rLXecywdY+rzygb3O/dvmmS+7zBlG525txqcPax+gmWwqu8G5r7s6eLUhf7XCh/bmQWu9bIKyPRPZyWA6hgaHHjV5hhKPHTbRNed1+l08MKOUjOqXDbw4nRVXQX0ltq1JPehq0rc6u4FWOL1mb0iKS2YXtqCKksWqKSILZ2WY29I+m1m9XOu9OQucKJKGAr6tNgwjKw2qJ/ETM7SDYqI9FJML6IEwVJJO8md8Mq9HDjBytiz/Hx6S4lFQEKBbu2CWFywWmavCIZixyDfXSo7InA6cJ52OGA3RuaVsbqiYrULDnRighJrr5vcptRF3QRqGBQVWgYdbP9OURIrhfWglnjGW8Ss67IF5h9sGsdqYZt8mCoMlRMi+rXp3NgSWJ1q90nB2/RQwX98qIbRVTC8XCLpOHtDrW82rYafXIcUwJ4DAPjcUOeCHslt5WXwBjDkMAyzBDYoWEwOGrKIeEGgLWh4AxqON3sjdS/EsfBWvY6AVzdWryPZZ/7nHLMWjZ4jYvGgmh9CzfUjniGMf91AJgqNZ8iwHwMM5HbbjCo2BEVI6l1GH/6e1oAVCgGj8RYXAvnsJ3b32SAZLLM6TNqpOGwqTBSIoPNIFZk59azAz4ALibQBc8psEX6hfPvYhKR+lGKWIJBJV1Un5f8tofahFFqNAnL3ZMiLoMrf/f7ZfJta1w1uZKohWO8HyyyGkYOIREc8pEyjFhrvL6v0iL1dgj5YrTJleRj9EMo9wFVTwOhqv0o6KVy4UtyLrZcNbAr4KFmqqlAS8PfEiX+vG4q1b/kTeRjquP0wMXu51E4BEgILah8g0fqQNcNsHyYl0YaP7YK0+PiIyIGviNlfH7a2cfMUAoozxFhHQQD1ZpQVUlAiTXFulNIMKbutBdktT9Av/GEB80SuUoRCldT/sHxtEkCnwHMZ4X1omaLDoBtkrxXUOk5VuRqFykKTUISMxLLUREq0daS3qJEAgzUggNUJCmC+ghX6Og3Aw3bEjRjrB4/0vfK8dklrTZD4O6xC0zvhGSDOvwxlTWICm41HMOxI1NqQXDhskoRGNNL/aNpO48hv8rAECZlpO4wVgWgOsdUk6Jdd3ukcbky0mvWDEhq1oGlUIzB67iR4Ifh0yIfHPRNrHTE0VIqOBywO8Oy0uzUfS2SnnsCmKBIVEcXsdECFWfrJeqwPw0HKQP1W2TM6obOxedghv6DJkS0kCGu5mkcQXVZPud0skUgy6PhQIn8ACrch+GUWA8mS25mx7PRJOb6VyLWg6rQHSmymiPay366CXKrqSIlFKNaU6MDzUTWDB+UkbpXlat/IthjfylCP8jD3ZTGAU87WtCbPpjyhaDrrAkHWc8P2GBZ6EINB+xsJycRm5hitzi46M0ckGtZrVDcKZVmHPT57nNRKJkDNHmh+JxOCr1ISLSPnhK5Z9fvSfDMxxgwcB3eu3PskOfziUZ6GKADlXK7S9FL9ESbrwFPZuLeLhUifZuI4Jtz7YJRwPoq4CsuDH0ht9hMd/oIevwHt94fzWhvU25ooTHvfjJO1OiwNy1h1DW/gPFqVACF9YP7omCL603pGbixRr9ZuhaQvmOtwFT6hNrmtuauDrS0EYLTdxGxdDMdhhmAufwFVEVnCslLWrMH4OoKGA4JV5PwyjBF7Z1QTo22K0Mf0+rfbZcPrScQxWPLYyJL/dP+7pvBDJhu3tYXTpVc68ZUaC2/ip8NeoGo8kEFLPGiFn/DYFkw00BmmNknuyzuWOtnL+sT4rfgM8DYMD/eXI+6JmtMuj4Qz+ekMfkL3kRb7tqlwrZaBKaC6lL34kfHWW8LDQcKrRIOKvQ+QGVhM9/lQokt9AJHOlvZVsnFQegrqNzgu7MYPJdP1EL1g2K/Oq+IZWp92WaDXYhVi2ZVrLca6Zn36SDXvDOjOjj9viPDx6ArDS1ij+04trx1aCiZCR54DHE19i7M59CnAC5LGfsWgHkpHeEwLlFeN2hDYzenA33HNqYQstDLDriuYm9VmLFI6DemxXee3MjyfzIF3IGzP31ekYyjlXN4Tl2mP71DF3VMY8RFHtvfMvSjakUC9KB1d7sbUftceHFXbpAO8lWauc3FzlqHoRNoyHDaptVxJumSnnxVK7GqZPKTWjZYCWca0U7FC8UEdK10Dg873qAijEZ0kM5PC5s/z4CEhloqoy5XcirXWwt+Kozw1Ud3yZNendZMJ6dsP5LKQ4C9G9qCq3foVUaI3ZtfwVKYHxcyUSR1pVrHMOnWHltYu8Ylqzj33FCxUSYVOQtsWVa9lKgL9apAW8DIc3fBOJIrFs0l+rhuqfG9j6qOgz+u7WozLQ1TFWhF4F+u1WW9HOPWTGB5O1dTb/w/+bLiBf3We0fpkXf2nh3Zf6uw32UTttsfb+aqV4vUs7icrSQW65j4kqemsT/BNLv4cGH/C1zzox+wh1xqeLkB8SkT4weqX++fy+bKEGrtx9mnUC/ZgZzhF6f9PN5xFVqT35yth14eDrKQgpJW9YRM3fePmkLw5JK77iFPuf7Bgkn0IZfRsJdLPUZZaKmq0khPeaAwZ2kvB6jTHne8dNGB2tb/2cPBXiilHyWgjLSIR7IekWYhii0rxsHWGo2DsotScLvTB8XfP7r/3a3qjQ9l0IImFnqRmAl9vul/nmjOQvXIb7Ess0xeQPu4K5y/xkKyoLMdxPncdzc0aYiJY34OE3b0V6W3mzIaxUF5hMd4Wsnl8keJisvI4G2i2YsyMX/cholc79Y/uAgaRfeABZt/2oBrF/6J9g9zzsEOanCStzvpzqn1Dla40uI1FfgeVjgXG+tJ3PR3WEAJsa88dsEriuXYzCz8x5MxbN42vNSRY+/c4CwxYvbPS/a+6p+0+EhIILvLDaJmUxGifXnp1AatuZOLWHkXhr2yVcx1bsBdNmmwi2N3vbJeDvQr/4nvMscD3lLQBrgRxsgEXt2Yn+jihrW06y7Xbt///h+3QaxYUR0AAA==";
    private const string KategorilerGzipBase64 = "H4sIAHIhXWoC/61YyW7cRhD9lQHPan6AblmcXCJEsI0ARuBDzbBFtdhkM70I4Bj6Fv1CdMnJN0n/leq9OUNG44kvGnZVL1Wvdn2pQO5uqZ5GqqrLL5USagJOetpMYNfQVJfVB0fcXDniRWWkGT7iH+SYqTUcekuV9K/q8s+qYz1nXS00buhJI1qJG5A9SnHDeO1/SEs1k0j1z9WtGBoqGRHcKDyYGdAxxQ53U90xThXrSQdTQ3X1+aK6gZ5xZnWIIqQ300H8AOmF3TLeMMl6/OyDVq25p4M/BwNwpt1nTxXccVB46vPDRUVHoXQG5h3x6zMx8bcF7Ys15XQgndFGmUxF0fNCjYDai0FLwVfVp1G4xqq/rP2S1lbRQu+o7JUnvT72Zyucb63dJ0mqH3OCzQuG6MzQGOKlL2QPJxS7kYjcKhyFShmSZSiiT3jALB5bw40mDULqNI2Y/GjJm58j+UxYnDD1ZDqUPq7Y4JX0qxEmlDwz92wg9wjcmq5RvdK8x8bHG5iGLQzs0PyUaLYDibGVXf1joJypZLiwtoeTdSNR0ZHqIvwTnY0gmSo4oqE9rd3fRF1DQSeJ3f5lEIpgL9C6BQuNwyJtiFD8kE+chUS8sEanmWIOK8gNqNutANkkmBJLwihkgYa1YK3FyPFmZfeuQVFoac8sQzEB+hnZ027mOhaDuCAjdChTguLX56+esbn2jNMgmWwKT/mriGSmNO0KqBJDgZOSaKpGpksOugeKZbwTJ2pDBxSnJw4wswrLctI/CJE5MhYOLCx3lEzCvZHRuLbkzafnfwL93EiB7p71rsw5zLO5sTIwa2aMAC9CZkWZczVZcZrVYHGvzh6ZQ5GqyGp13Mk+YfHT+6vv2yoE6hZaDoNmhPpsmbTsDEcG2zG0CzcLiLzZJSx7QKqiCaBcFagcc4J8f32m96dkp0V3nBlDBjxKBT3+4k3EEfZsls5rLWFQsNNMDGQLioM+IUke5IhUGQowYh5VtNNCOgyY4i7SlI3dIhhe/n59dJzNh8CZoXN4LIEUGLUyku6i8OmZGtPlAB0BZ5JIdK5ZXBXpLlthJyULGrQEo5mhr/gCG4yS3Wch+8zT0gGS4eJ01TytxKjZsVvYH0bXMswW1YITAf0jkl6e5lDObgkoFr6AiDEdw+WIHv2qYCghW0N24AV907Fm/Ik2iBQcNaUzGZezbupZMkKu9ISlNc2I790I2ZtcgJD7+ujZm+vEnuETT2V0/iOPFqytQYtkLQsOViFdAlSwJkR14HM9ai7aAPQBLOvesNTBLeQn5yusoYK4h7OzWNrmE0wvT8P53Tp2wXW4PTSnJc06/uG2pL2nsj2OMtipvTGozfvttUxcQBEGOjepmD3WyLn2V89fkfhd1FdUHShvKTPV3ZYDxdHnJmvaN8vuaaoX+ooJ++fjQPgdyacHwJrOZWDY3Om0XPTupT782yf0mf9bzcoaE4f22eC21K67UtwyW06OgXn38mg5a9ic7g++5tW4pgPmotS3Bzq2GOWcshwV39iThTJbeMmid6TOxCcNC4fCNsm260dwfICXR/7y1H03PG5x8IAjNLBMNPTelCPdelebmvw3JvgER+5Vc0pYHukyIjdswNK1gMgvjvHNeDDs4aBhJSI+JrbQYWOWCutyoCyPL8k9bFzI1Nqt4REj5dQppujauLizgbfkIb8F1v/3ED9E11Kgg4jRjkR7msnYdAuClmRj0e52INtMPbWFD9N62cwuu0PyIN+OfX6woUJ3SHP//WxxauTV5YC9YHQYVDg6OqahkcCuitFVsOYx4HNRZh+ljJFKzK6oUt7jmn+FicKlexR5YpmJrbBNbrR3ZuzAWivytpS3lLB+D7n8IDfU3x1aHmNtoD5sZCkWLnkhNVqP7Qu2pHsq70HhmJsc9+HhX1a5ea8xFgAA";

    private static readonly Dictionary<string, UygulamaKategoriTanimi> Kategoriler = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Kilit = new();

    public static IReadOnlyCollection<UygulamaKategoriTanimi> UygulamaKategorileri { get { BaslatKategoriler(); return Kategoriler.Values.ToList().AsReadOnly(); } }

    public static HizmetKatalogAyarlari Genislet(HizmetKatalogAyarlari tohum)
    {
        ArgumentNullException.ThrowIfNull(tohum);
        Dictionary<string, HizmetTanimi> sonuc = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string[]> aileler = JsonSerializer.Deserialize<Dictionary<string, string[]>>(Coz(AilelerGzipBase64))
            ?? throw new InvalidOperationException("V6 hizmet aileleri okunamadı.");
        int sira = 0;
        foreach ((string aile, string[] islemler) in aileler)
        {
            foreach (string islem in islemler)
            {
                string kimlik = $"{aile}.{islem}";
                sonuc[$"{kimlik}@1.0"] = new HizmetTanimi
                {
                    HizmetKimligi = kimlik,
                    HizmetSurumu = "1.0",
                    Aciklama = $"{aile} ailesinde {islem.Replace('-', ' ')} işlevini gerçekleştirir.",
                    ZamanAsimiMs = 2_000 + sira % 5 * 500,
                    AzamiIstekBoyutuByte = new[] { 8_192, 16_384, 32_768, 49_152, 65_536 }[sira % 5],
                    Aktif = true
                };
                sira++;
            }
        }
        if (sonuc.Count != 500) throw new InvalidOperationException($"V6 standart hizmet kataloğu 500 olmalı; oluşan: {sonuc.Count}.");
        BaslatKategoriler();
        return new HizmetKatalogAyarlari { KatalogSurumu = "6.0", Hizmetler = sonuc.Values.OrderBy(x => x.HizmetKimligi, StringComparer.OrdinalIgnoreCase).ToList() };
    }

    public static bool KategoriBul(string? kimlik, out UygulamaKategoriTanimi? kategori)
    {
        BaslatKategoriler();
        return Kategoriler.TryGetValue(kimlik?.Trim() ?? string.Empty, out kategori);
    }

    public static UygulamaKategoriDogrulamaSonucu UygulamayiDogrula(SunulanUygulama uygulama, SirketKaydi sirket)
    {
        BaslatKategoriler();
        if (!Kategoriler.TryGetValue(uygulama.Kategori?.Trim() ?? string.Empty, out UygulamaKategoriTanimi? kategori))
            return new UygulamaKategoriDogrulamaSonucu { Gecerli = false, KategoriKimligi = uygulama.Kategori, EksikZorunluHizmetler = ["Motorun tanıdığı 200 kategoriden biri seçilmedi."] };

        HashSet<string> uygulamaHizmetleri = uygulama.Ozellikler.Select(x => x.HizmetKimligi?.Trim() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> sirketHizmetleri = sirket.Hizmetler.Where(x => x.Aktif).Select(x => x.HizmetKimligi)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<string> eksik = kategori.ZorunluHizmetler.Where(x => !uygulamaHizmetleri.Contains(x) || !sirketHizmetleri.Contains(x)).ToList();
        List<string> uygunEk = [];
        List<string> uygunsuz = [];
        foreach (string h in uygulamaHizmetleri.Except(kategori.ZorunluHizmetler, StringComparer.OrdinalIgnoreCase))
        {
            string aile = h.Split('.', 2)[0];
            if (kategori.IzinliEkHizmetAileleri.Contains(aile)) uygunEk.Add(h); else uygunsuz.Add(h);
        }
        int toplam = uygulamaHizmetleri.Count;
        bool adetUygun = toplam >= kategori.AsgariHizmetSayisi && toplam <= kategori.AzamiHizmetSayisi;
        double kalite = Math.Clamp(50 + uygunEk.Count * kategori.EkHizmetKaliteKatkisi - uygunsuz.Count * 2.5 - eksik.Count * 15, 0, 100);
        return new UygulamaKategoriDogrulamaSonucu
        {
            Gecerli = eksik.Count == 0 && adetUygun,
            KategoriKimligi = kategori.KategoriKimligi,
            EksikZorunluHizmetler = eksik.AsReadOnly(),
            UygunEkHizmetler = uygunEk.AsReadOnly(),
            UygunsuzHizmetler = uygunsuz.AsReadOnly(),
            HizmetKalitePuani = kalite,
            KapasiteTuketimPuani = kategori.TabanKapasiteTuketimi + Math.Max(0, toplam - kategori.AsgariHizmetSayisi) * 1.5
        };
    }

    private static void BaslatKategoriler()
    {
        if (Kategoriler.Count == 200) return;
        lock (Kilit)
        {
            if (Kategoriler.Count == 200) return;
            using JsonDocument doc = JsonDocument.Parse(Coz(KategorilerGzipBase64));
            JsonElement root = doc.RootElement;
            Dictionary<string, string> sektorler = root.GetProperty("sectors").EnumerateObject().ToDictionary(
                x => x.Name, x => x.Value.ValueKind == JsonValueKind.Null ? string.Empty : x.Value.GetString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
            List<JsonElement> arketipler = root.GetProperty("archetypes").EnumerateObject().Select(x => x.Value.Clone()).ToList();
            foreach ((string sektor, string sektorHizmeti) in sektorler)
            {
                foreach (JsonElement a in arketipler)
                {
                    string temel = a.GetProperty("ad").GetString() ?? "Uygulama";
                    string temelKimlik = root.GetProperty("archetypes").EnumerateObject().First(x => x.Value.GetRawText() == a.GetRawText()).Name;
                    string kimlik = sektor == "genel" ? temelKimlik : $"{sektor}.{temelKimlik}";
                    List<string> zorunlu = a.GetProperty("req").EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList();
                    if (!string.IsNullOrWhiteSpace(sektorHizmeti) && !zorunlu.Contains(sektorHizmeti, StringComparer.OrdinalIgnoreCase)) zorunlu.Add(sektorHizmeti);
                    HashSet<string> aileler = a.GetProperty("families").EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(sektorHizmeti)) aileler.Add(sektorHizmeti.Split('.', 2)[0]);
                    Kategoriler[kimlik] = new UygulamaKategoriTanimi
                    {
                        KategoriKimligi = kimlik,
                        KategoriAdi = sektor == "genel" ? temel : $"{sektor} {temel}",
                        Sektor = sektor,
                        UrunTuru = a.GetProperty("urunTuru").GetString() ?? "uygulama",
                        ZorunluHizmetler = zorunlu.AsReadOnly(),
                        IzinliEkHizmetAileleri = aileler,
                        AsgariHizmetSayisi = zorunlu.Count,
                        OnerilenHizmetSayisi = Math.Min(20, zorunlu.Count + 5),
                        AzamiHizmetSayisi = 40,
                        EkHizmetKaliteKatkisi = 1.25,
                        TabanKapasiteTuketimi = 10 + zorunlu.Count * 3,
                        KullaniciBasinaKapasiteTuketimi = 0.0025 + zorunlu.Count * 0.00035
                    };
                }
            }
            if (Kategoriler.Count != 200) throw new InvalidOperationException($"V6 uygulama kategorisi sayısı 200 olmalı; oluşan: {Kategoriler.Count}.");
        }
    }

    private static string Coz(string base64)
    {
        using MemoryStream kaynak = new(Convert.FromBase64String(base64));
        using GZipStream gzip = new(kaynak, CompressionMode.Decompress);
        using StreamReader okuyucu = new(gzip, Encoding.UTF8);
        return okuyucu.ReadToEnd();
    }
}
