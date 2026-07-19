# Proje Dokümanları

Bu dosya, Üç Kardeş Yazılım Şirketi Simülasyonu için güncel teknik belgelerin ana indeksidir.

> **Güncel ana sürüm: V7 geliştirme dalı**  
> V7; V6'nın 500 hizmet, 200 kategori, 20.000 müşteri, işletim sistemi/protokol ve gerçek kapasite sisteminin üzerine tick muhasebesi, banka notu, otomatik kurtarma kredisi, SLA uygunluğu, scroll korumalı panolar, grafikler, haber bülteni ve genişletilmiş Tunix hizmet paketini ekler.

---

## 1. En güncel okuma sırası

### Motoru çalıştıracak kişi

1. [V7 Finans, Banka, SLA, Panolar ve Tunix Hizmetleri](Dokumanlar/V7_FINANS_BANKA_SLA_PANOLAR_VE_TUNIX_HIZMETLERI.md)
2. [Son Güncellemeler — V6 Değişiklik Günlüğü](Dokumanlar/SON_GUNCELLEMELER_V6_DEGISIKLIK_GUNLUGU.md)
3. [V6 Mimari ve Ekonomi Rehberi](Dokumanlar/V6_MIMARI_VE_EKONOMI_REHBERI.md)
4. [V6 Geçiş, Derleme ve Test Rehberi](Dokumanlar/V6_GECIS_DERLEME_VE_TEST_REHBERI.md)
5. [Gerçek Kapasite ve Sınırsız Yatırım Sistemi](Dokumanlar/GERCEK_KAPASITE_VE_SINIRSIZ_YATIRIM_SISTEMI.md)
6. [Fiyat Pazarı ve Kullanıcı Göçü](Dokumanlar/FIYAT_PAZARI_VE_KULLANICI_GOCU.md)

### Şirket sunucusu geliştirecek oyuncu

1. [500 Hizmet ve 200 Uygulama Kategorisi Standardı](Dokumanlar/500_HIZMET_200_UYGULAMA_KATEGORISI_STANDARDI.md)
2. [Uygulama Manifesti V1](UYGULAMA_MANIFESTI_V1.md)
3. [Sunucu Güncelleme V2 Rehberi](SUNUCU_GUNCELLEME_V2_REHBERI.md)
4. [Siber Saldırı Savunma Rehberi](SIBER_SALDIRI_SAVUNMA_REHBERI.md)
5. [Şirket İşletim Merkezi 8090 Rehberi](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)

### Tunix geliştirecek kişi

1. [Tunix OS Teknik Rehberi](Dokumanlar/TUNIX_OS_TEKNIK_REHBERI.md)
2. [V7 Finans, Banka, SLA, Panolar ve Tunix Hizmetleri](Dokumanlar/V7_FINANS_BANKA_SLA_PANOLAR_VE_TUNIX_HIZMETLERI.md)
3. [500 Hizmet ve 200 Uygulama Kategorisi Standardı](Dokumanlar/500_HIZMET_200_UYGULAMA_KATEGORISI_STANDARDI.md)
4. [V6 Geçiş, Derleme ve Test Rehberi](Dokumanlar/V6_GECIS_DERLEME_VE_TEST_REHBERI.md)

---

## 2. V7 ana belgesi

### [V7 Finans, Banka, SLA, Panolar ve Tunix Hizmetleri](Dokumanlar/V7_FINANS_BANKA_SLA_PANOLAR_VE_TUNIX_HIZMETLERI.md)

Belgenin kapsamı:

- kalıcı tick muhasebesi,
- gelir ve gider kalemleri,
- son tick gelir/gider/net ayrımı,
- sürdürülebilir işletme maliyeti,
- yapay ödenemeyen gider normalizasyonu,
- otomatik yüksek faizli kurtarma kredisi,
- kredi notu, kredi sınıfı ve kredi limiti,
- 8090 ayrıntılı gelir–gider paneli,
- SLA hizmet ve kapasite uygunluğu,
- 8080 şirket finans karşılaştırması,
- gelir/gider/net çizgi grafikleri,
- kategori pazar payı pasta grafikleri,
- scroll konumu koruyan canlı yenileme,
- 8080 ve 8090 haber bülteni,
- Tunix'e eklenen 24 gerçek Rust hizmeti,
- V6 genel JSON sonuç doğrulaması,
- kalıcı V7 dosyaları ve test komutları.

---

## 3. V6 temel belgeleri

### [V6 Mimari ve Ekonomi Rehberi](Dokumanlar/V6_MIMARI_VE_EKONOMI_REHBERI.md)

- motor kayıtlarının ticari otoritesi,
- 500 hizmet,
- 200 kategori,
- işletim sistemi/protokol zorunluluğu,
- gerçek kapasite havuzu,
- aşırı yük ve kesinti,
- yatırımlar,
- fiyat pazarı,
- şirket değeri,
- kalıcı veri mimarisi.

### [500 Hizmet ve 200 Uygulama Kategorisi Standardı](Dokumanlar/500_HIZMET_200_UYGULAMA_KATEGORISI_STANDARDI.md)

- 25 hizmet ailesi × 20 işlev,
- 20 sektör × 10 uygulama arketipi,
- zorunlu hizmet kombinasyonları,
- uygun ve uygunsuz ek hizmetler,
- kategori doğrulaması,
- kalite ve kapasite etkisi.

### [Gerçek Kapasite ve Sınırsız Yatırım Sistemi](Dokumanlar/GERCEK_KAPASITE_VE_SINIRSIZ_YATIRIM_SISTEMI.md)

- tek fiziksel kapasite havuzu,
- hizmet ve uygulama kapasite tahsisi,
- işletim sistemi kapasitesi,
- aşırı yük eşikleri,
- kesinti sonuçları,
- sınırsız ve giderek zorlaşan yatırımlar,
- teknik ön koşullar,
- gecikmeli yatırım tamamlanması.

### [8080–8090 V6 Panoları, Haberler, Grafikler ve Müşteri CV](Dokumanlar/8080_8090_V6_PANOLAR_HABERLER_GRAFIKLER_VE_MUSTERI_CV.md)

V6 pano alanlarını açıklar. Son tick muhasebesi, banka ve yeni scroll davranışında V7 belgesi üstündür.

### [Tunix OS Teknik Rehberi](Dokumanlar/TUNIX_OS_TEKNIK_REHBERI.md)

- Rust süreç yönetimi,
- kaynak tahsisi,
- dosya sistemi,
- paket yönetimi,
- ağ ve güncelleme,
- kimlik ve güvenlik,
- Tingram/Tmail/Tlink entegrasyonu.

### [Fiyat Pazarı ve Kullanıcı Göçü](Dokumanlar/FIYAT_PAZARI_VE_KULLANICI_GOCU.md)

- kategori referans fiyatları,
- adil fiyat,
- aşırı fiyat kullanıcı kaybı,
- rakibe göç,
- fiyat şoku,
- chargeback,
- kalıcı pazar kaydı.

---

## 4. Destekleyici belgeler

- [Siber Saldırı Savunma Rehberi](SIBER_SALDIRI_SAVUNMA_REHBERI.md)
- [Şirket İşletim Merkezi 8090 Rehberi](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)
- [Uygulama Manifesti V1](UYGULAMA_MANIFESTI_V1.md)
- [Sunucu Güncelleme V2 Rehberi](SUNUCU_GUNCELLEME_V2_REHBERI.md)
- [İlk 10 Hizmet Sözleşmesi](HIZMET_SOZLESMELERI_V2.md)
- [Kalıcı Kayıt, Hesaplar ve Destekler](KALICI_KAYIT_HESAPLAR_VE_DESTEKLER.md)

`HIZMET_SOZLESMELERI_V2.md` ilk 10 çekirdek hizmetin sıkı teknik sözleşmesini korur. Genel katalog artık 500 hizmettir.

---

## 5. Tarihsel belgeler

Aşağıdaki dosyalar projenin önceki sürümlerini açıklar; güncel davranışta V7/V6 belgeleri üstündür:

- `PANO_V4_1_10000_MUSTERI_REHBERI.md`
- `YAZILIM_BORSASI_V3_GUNCELLEME_REHBERI.md`
- `TINGRAM_TMAIL_ILOS_TECH_8080_PAKETI.md`

---

## 6. Şirketler ve portlar

| Şirket | Dil | Port | Güncel ürünler |
|---|---|---:|---|
| Tunix | Rust | 7001 | Tunix OS, Tingram, Tmail, Tlink ve geniş standart hizmet paketi |
| Mudaf | Go | 7002 | Oyuncunun kodladığı hizmetler ve uygulamalar |
| Ugax | Node.js | 7003 | Oyuncunun kodladığı hizmetler ve uygulamalar |
| İlos Tech | Python | 7004 | İSosyal, İMail, İLink ve temel hizmetler |

| Port | Sistem | Amaç |
|---:|---|---|
| 8080 | Yazılım Borsası | TV ana sayfası, finans, grafikler, pazarlar, haberler ve müşteri CV |
| 8090 | Şirket Yönetim Merkezi | Ayrıntılı muhasebe, banka, kapasite, ürün, SLA ve şirket haberleri |

---

## 7. Temel kurallar

```text
Kod ve işçilik şirket sunucusunda yapılır.
Motor teknik sonucu, gerçek kapasiteyi, muhasebeyi ve piyasa davranışını doğrular.
8080 salt-okunur piyasa ve haber ekranıdır.
8090 şirkete özel ticari, finansal ve kapasite yönetim ekranıdır.
Kredi girişi gelir sayılmaz.
Sunucu manifesti ilk varsayılandır; 8090 motor kaydı daha sonra ticari otorite olur.
```

---

## 8. Güncelleme sonrası test

```powershell
git pull --ff-only origin agent/tunix-matematik-topla

dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj

Push-Location .\Sirketler\Tunahan-Rustix\Tunix
cargo test
Pop-Location
```

Build geçtikten sonra motoru ve şirket sunucularını başlatın. Tarayıcıda eski kaynak görünürse 8080 ve 8090 için bir kez `Ctrl+F5` kullanın.

---

## 9. Belge bakım kuralı

Motor protokolü, hizmet kataloğu, kategori sözleşmesi, kapasite, yatırım, fiyat pazarı, muhasebe, banka, SLA, müşteri modeli, 8080 veya 8090 değiştiğinde ilgili belge aynı commit serisinde güncellenmelidir. Derlenmemiş veya test edilmemiş davranış, doğrulanmış gibi yazılmamalıdır.
