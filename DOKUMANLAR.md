# Proje Dokümanları

Bu dosya, Üç Kardeş Yazılım Şirketi Simülasyonu için güncel teknik belgelerin ana indeksidir.

> **Güncel ana sürüm: V8.1 temiz sezon ve kalıcı tick mimarisi**  
> V8.1; eski deneysel bilanço verilerini temizler, hizmet ve uygulama tanımlarını korur, bütün şirketleri eşit başlangıç çizgisine alır ve global ticki şirket bağlantılarından bağımsız, kalıcı ve güvenli hâle getirir.

---

## 1. En güncel okuma sırası

### Motoru çalıştıracak kişi

1. [V8.1 Tam Şirket Reseti ve Tick Mimarisi](Dokumanlar/V8_1_TAM_RESET_VE_TICK_MIMARISI.md)
2. [V8 Ceza, Uyum, Katalog ve Panel Geçiş Rehberi](Dokumanlar/V8_CEZA_UYUM_KATALOG_VE_PANEL_GECIS_REHBERI.md)
3. [V7 Finans, Banka, SLA, Panolar ve Tunix Hizmetleri](Dokumanlar/V7_FINANS_BANKA_SLA_PANOLAR_VE_TUNIX_HIZMETLERI.md)
4. [V6 Mimari ve Ekonomi Rehberi](Dokumanlar/V6_MIMARI_VE_EKONOMI_REHBERI.md)
5. [Gerçek Kapasite ve Sınırsız Yatırım Sistemi](Dokumanlar/GERCEK_KAPASITE_VE_SINIRSIZ_YATIRIM_SISTEMI.md)
6. [Fiyat Pazarı ve Kullanıcı Göçü](Dokumanlar/FIYAT_PAZARI_VE_KULLANICI_GOCU.md)

### Mudaf, Ugax, İlos Tech veya Tunix sunucusunu geliştirecek oyuncu

1. [V8.1 Tam Şirket Reseti ve Tick Mimarisi](Dokumanlar/V8_1_TAM_RESET_VE_TICK_MIMARISI.md)
2. [V8 Ceza, Uyum, Katalog ve Panel Geçiş Rehberi](Dokumanlar/V8_CEZA_UYUM_KATALOG_VE_PANEL_GECIS_REHBERI.md)
3. [500 Hizmet ve 200 Uygulama Kategorisi Standardı](Dokumanlar/500_HIZMET_200_UYGULAMA_KATEGORISI_STANDARDI.md)
4. [Uygulama Manifesti V1](UYGULAMA_MANIFESTI_V1.md)
5. [Sunucu Güncelleme V2 Rehberi](SUNUCU_GUNCELLEME_V2_REHBERI.md)
6. [Şirket İşletim Merkezi 8090 Rehberi](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)

### Tunix geliştirecek kişi

1. [Tunix OS Teknik Rehberi](Dokumanlar/TUNIX_OS_TEKNIK_REHBERI.md)
2. [V8.1 Tam Şirket Reseti ve Tick Mimarisi](Dokumanlar/V8_1_TAM_RESET_VE_TICK_MIMARISI.md)
3. [V8 Ceza, Uyum, Katalog ve Panel Geçiş Rehberi](Dokumanlar/V8_CEZA_UYUM_KATALOG_VE_PANEL_GECIS_REHBERI.md)
4. [V7 Finans, Banka, SLA, Panolar ve Tunix Hizmetleri](Dokumanlar/V7_FINANS_BANKA_SLA_PANOLAR_VE_TUNIX_HIZMETLERI.md)
5. [500 Hizmet ve 200 Uygulama Kategorisi Standardı](Dokumanlar/500_HIZMET_200_UYGULAMA_KATEGORISI_STANDARDI.md)

---

## 2. V8.1 ana belgesi

### [V8.1 Tam Şirket Reseti ve Tick Mimarisi](Dokumanlar/V8_1_TAM_RESET_VE_TICK_MIMARISI.md)

Belgenin kapsamı:

- reset öncesi otomatik arşiv,
- hizmet ve uygulama tanımlarını koruyan tam şirket ekonomisi reseti,
- dört şirket için eşit 50.000 TL ve 50 taban puan,
- yatırım, kredi, ceza, saldırı ve finans geçmişinin temizlenmesi,
- satın alınmış kapasitenin sıfırlanması,
- müşteri OS ve uygulama geçmişinin temizlenmesi,
- herhangi bir şirket sayısıyla çalışan global tick,
- kapalı şirkete gelir, gider, kredi, ceza veya olay yazılmaması,
- kapalı uygulamaların o tickte piyasaya katılmaması,
- ayrı ve atomik `tick-saat.json`,
- yalnız tamamlanan tickin kalıcı saate yazılması,
- tick hatasında sessiz devam yerine güvenli duruş,
- geri dönüş ve arşiv kuralları.

V8.1 reset ve tick davranışında önceki bütün belgelerden üstündür.

---

## 3. V8 ceza ve panel belgesi

### [V8 Ceza, Uyum, Katalog ve Panel Geçiş Rehberi](Dokumanlar/V8_CEZA_UYUM_KATALOG_VE_PANEL_GECIS_REHBERI.md)

- motor/sözleşme kusuru ile şirket kusurunun ayrılması,
- ilk iki tekrar için uyarı dönemi,
- şirket başına tickte 75 TL normal ceza tavanı,
- ağır kimlik ve güvenlik kusurları,
- kalıcı ceza defteri,
- 8080 Cezalar & Uyum paneli,
- 8090 ceza, 500 hizmet ve 200 kategori panelleri,
- kararlı giriş ve yalnız açık paneli render eden yenileme,
- yavaşlatılmış haber şeridi,
- Tunix, Mudaf, Ugax ve İlos Tech geçiş listeleri.

---

## 4. V7 finans belgesi

### [V7 Finans, Banka, SLA, Panolar ve Tunix Hizmetleri](Dokumanlar/V7_FINANS_BANKA_SLA_PANOLAR_VE_TUNIX_HIZMETLERI.md)

- tick bazlı gelir–gider defteri,
- son tick gelir/gider/net ayrımı,
- sürdürülebilir işletme maliyeti,
- kredi notu, kredi sınıfı ve kredi limiti,
- otomatik kurtarma kredisi,
- SLA hizmet ve kapasite uygunluğu,
- grafikler ve haber bülteni,
- Tunix geniş hizmet paketi.

Kapalı şirketin finansal olarak donması ve temiz sezon resetinde V8.1 belgesi üstündür.

---

## 5. V6 ekosistem belgeleri

- [V6 Mimari ve Ekonomi Rehberi](Dokumanlar/V6_MIMARI_VE_EKONOMI_REHBERI.md)
- [500 Hizmet ve 200 Uygulama Kategorisi Standardı](Dokumanlar/500_HIZMET_200_UYGULAMA_KATEGORISI_STANDARDI.md)
- [Gerçek Kapasite ve Sınırsız Yatırım Sistemi](Dokumanlar/GERCEK_KAPASITE_VE_SINIRSIZ_YATIRIM_SISTEMI.md)
- [Tunix OS Teknik Rehberi](Dokumanlar/TUNIX_OS_TEKNIK_REHBERI.md)
- [Fiyat Pazarı ve Kullanıcı Göçü](Dokumanlar/FIYAT_PAZARI_VE_KULLANICI_GOCU.md)
- [V6 Geçiş, Derleme ve Test Rehberi](Dokumanlar/V6_GECIS_DERLEME_VE_TEST_REHBERI.md)

Bu belgeler 500 hizmet, 200 kategori, OS/protokol zorunluluğu, gerçek kapasite, yatırımlar, aşırı yük, fiyat pazarı ve Tunix OS mimarisini açıklar.

---

## 6. Destekleyici belgeler

- [Siber Saldırı Savunma Rehberi](SIBER_SALDIRI_SAVUNMA_REHBERI.md)
- [Şirket İşletim Merkezi 8090 Rehberi](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)
- [Uygulama Manifesti V1](UYGULAMA_MANIFESTI_V1.md)
- [Sunucu Güncelleme V2 Rehberi](SUNUCU_GUNCELLEME_V2_REHBERI.md)
- [İlk 10 Hizmet Sözleşmesi](HIZMET_SOZLESMELERI_V2.md)
- [Kalıcı Kayıt, Hesaplar ve Destekler](KALICI_KAYIT_HESAPLAR_VE_DESTEKLER.md)

`HIZMET_SOZLESMELERI_V2.md` ilk 10 çekirdek hizmetin sıkı teknik sözleşmesini korur. Genel katalog 500 hizmettir.

---

## 7. Şirketler ve portlar

| Şirket | Dil | Port | Ürünler |
|---|---|---:|---|
| Tunix | Rust | 7001 | Tunix OS, Tingram, Tmail, Tlink ve standart hizmet paketi |
| Mudaf | Go | 7002 | Oyuncunun kodladığı hizmetler ve uygulamalar |
| Ugax | Node.js | 7003 | Oyuncunun kodladığı hizmetler ve uygulamalar |
| İlos Tech | Python | 7004 | İSosyal, İMail, İLink ve temel hizmetler |

| Port | Sistem | Amaç |
|---:|---|---|
| 8080 | Yazılım Borsası V8 | Piyasa, finans, grafikler, cezalar, haberler ve müşteri CV |
| 8090 | Şirket Yönetim Merkezi V8 | Muhasebe, banka, kapasite, ürün, SLA, katalog ve ceza yönetimi |

---

## 8. Temel kurallar

```text
Kod ve işçilik şirket sunucusunda yapılır.
Motor teknik sonucu, gerçek kapasiteyi, muhasebeyi ve piyasayı doğrular.
Manifestte ilan edilen hizmet gerçek yönlendiricide çalışmalıdır.
Sunucu manifesti ilk varsayılandır; 8090 motor kaydı ticari otoritedir.
Bir şirketin kapalı olması global ticki durdurmaz.
Kapalı şirket iş alamaz ve finansal/operasyonel olarak donar.
Kredi girişi gelir sayılmaz.
8080 salt-okunur piyasa ekranıdır.
8090 şirkete özel yönetim ekranıdır.
```

---

## 9. Güncelleme sonrası test

```powershell
git pull --ff-only origin agent/tunix-matematik-topla

dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj

Push-Location .\Sirketler\Tunahan-Rustix\Tunix
cargo test
Pop-Location
```

İlk V8.1 çalıştırmada otomatik arşiv ve tek seferlik reset uygulanır. Derlenmemiş veya yerel çalıştırmayla doğrulanmamış davranış, doğrulanmış kabul edilmez.
