# Proje Dokümanları

Bu dosya, Üç Kardeş Yazılım Şirketi Simülasyonu için güncel teknik belgelerin ana indeksidir.

> **Güncel ana sürüm: V9.1 temiz motor**  
> V9.1; eski üst üste binmiş ekonomi katmanlarını kaldırır, şirket verilerini yeniden sıfırlar, hizmet fiyatlarını sabitler, tek fiziksel kapasite havuzu kurar ve 20.000 müşterilik trendli pazarı tek ekonomi defterinden işletir.

---

## 1. Önce okunacak ana belge

### [V9.1 Temiz Ekonomi, Kapasite, Talep ve Panel Rehberi](Dokumanlar/V9_1_TEMIZ_EKONOMI_KAPASITE_TALEP_VE_PANOLAR.md)

Bu belge şu konularda önceki bütün belgelerden üstündür:

- V9.1 tek seferlik tam reset,
- dört şirket için eşit 100.000 TL ve 70 taban puan,
- tek ekonomi ve yan etkisiz işletim deposu,
- çevrimdışı şirketlerin ekonomik olarak donması,
- sabit hizmet fiyatları,
- kapasite satın alımının kaldırılması,
- tek fiziksel havuz ve 8090 tahsis sliderları,
- hafif giderler ve otomatik kredinin kaldırılması,
- sabit faizli isteğe bağlı kredi,
- başarısız işte yarım ödeme ve kademeli puan kaybı,
- düşük siber kayıp,
- en az yaklaşık 10.000 hizmet talebi,
- 20.000 müşterinin OS ve uygulama tercihleri,
- kategoriye özel uygulama/OS fiyat aralıkları,
- gerçek uygulama ve abonelik geliri,
- tek kanonik protokol kimliği,
- bağımsız ve hafif SLA akışı,
- kararlı 8080 ve 8090 panelleri.

---

## 2. Okuma sırası

### Motoru çalıştıracak kişi

1. [V9.1 Temiz Ekonomi, Kapasite, Talep ve Panel Rehberi](Dokumanlar/V9_1_TEMIZ_EKONOMI_KAPASITE_TALEP_VE_PANOLAR.md)
2. [500 Hizmet ve 200 Uygulama Kategorisi Standardı](Dokumanlar/500_HIZMET_200_UYGULAMA_KATEGORISI_STANDARDI.md)
3. [Tunix OS Teknik Rehberi](Dokumanlar/TUNIX_OS_TEKNIK_REHBERI.md)
4. [Uygulama Manifesti V1](UYGULAMA_MANIFESTI_V1.md)
5. [Sunucu Güncelleme V2 Rehberi](SUNUCU_GUNCELLEME_V2_REHBERI.md)

### Mudaf, Ugax, İlos Tech veya Tunix sunucusunu geliştirecek oyuncu

1. [V9.1 Temiz Ekonomi, Kapasite, Talep ve Panel Rehberi](Dokumanlar/V9_1_TEMIZ_EKONOMI_KAPASITE_TALEP_VE_PANOLAR.md)
2. [500 Hizmet ve 200 Uygulama Kategorisi Standardı](Dokumanlar/500_HIZMET_200_UYGULAMA_KATEGORISI_STANDARDI.md)
3. [Uygulama Manifesti V1](UYGULAMA_MANIFESTI_V1.md)
4. [Sunucu Güncelleme V2 Rehberi](SUNUCU_GUNCELLEME_V2_REHBERI.md)
5. [Şirket İşletim Merkezi 8090 Rehberi](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)

---

## 3. Güncel V9.1 kuralları

```text
Kod ve gerçek hizmet işçiliği şirket sunucusunda yapılır.
Motor teknik sonucu, kapasiteyi, muhasebeyi ve piyasayı doğrular.
Manifestte ilan edilen hizmet gerçek yönlendiricide çalışmalıdır.
500 standart hizmetin fiyatı motor tarafından sabittir.
Hizmet ve uygulama kapasitesi satın alınamaz.
Şirket toplam fiziksel kapasitesini hizmet, uygulama ve OS arasında tahsis eder.
Toplam kapasite yalnız altyapı yatırımlarıyla büyür.
Uygulama ve işletim sistemi fiyatları kategori min–max aralığında yönetilir.
Her uygulama aktif OS ve kanonik protokol seçmeden çalışamaz.
Bir şirketin kapalı olması global ticki durdurmaz.
Kapalı şirket iş alamaz; gideri, kredisi, SLA'sı ve ürün ekonomisi donar.
Otomatik kurtarma kredisi yoktur.
Kredi girişi gelir sayılmaz.
8080 salt okunur piyasa ekranıdır.
8090 şirkete özel ticari ve altyapı yönetim ekranıdır.
```

---

## 4. Güncel panolar

| Port | Sistem | Kapsam |
|---:|---|---|
| 8080 | Yazılım Borsası V9.1 | Trendli arz-talep, şirket finansı, uygulama/OS/hizmet pazarı, grafikler, müşteri CV, haber ve canlı akış |
| 8090 | Şirket Yönetim Merkezi V9.1 | Finans, banka, kapasite tahsisi, uygulama/OS yayını, 500 hizmet, 200 kategori, protokoller, SLA ve yatırımlar |

Her iki panel de tek DOM kökü kullanır. Yalnız açık panel render edilir. Window ve iç tablo scroll konumları canlı yenilemede korunur.

---

## 5. Şirketler ve portlar

| Şirket | Dil | Port | Başlıca ürünler |
|---|---|---:|---|
| Tunix | Rust | 7001 | Tunix OS, Tingram, Tmail, Tlink ve standart hizmet paketi |
| Mudaf | Go | 7002 | Oyuncunun kodladığı hizmetler, uygulamalar ve platformlar |
| Ugax | Node.js | 7003 | Oyuncunun kodladığı hizmetler, uygulamalar ve platformlar |
| İlos Tech | Python | 7004 | İSosyal, İMail, İLink ve standart hizmetler |

---

## 6. Destekleyici belgeler

- [500 Hizmet ve 200 Uygulama Kategorisi Standardı](Dokumanlar/500_HIZMET_200_UYGULAMA_KATEGORISI_STANDARDI.md)
- [Tunix OS Teknik Rehberi](Dokumanlar/TUNIX_OS_TEKNIK_REHBERI.md)
- [Siber Saldırı Savunma Rehberi](SIBER_SALDIRI_SAVUNMA_REHBERI.md)
- [Şirket İşletim Merkezi 8090 Rehberi](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)
- [Uygulama Manifesti V1](UYGULAMA_MANIFESTI_V1.md)
- [Sunucu Güncelleme V2 Rehberi](SUNUCU_GUNCELLEME_V2_REHBERI.md)
- [İlk 10 Hizmet Sözleşmesi](HIZMET_SOZLESMELERI_V2.md)

`HIZMET_SOZLESMELERI_V2.md` ilk 10 çekirdek hizmetin sıkı sonuç doğrulamasını korur. Genel motor kataloğu 500 hizmettir.

Eski V6–V8 belgeleri tarihsel tasarım kararlarını anlamak için tutulur; ekonomi, reset, kapasite, kredi, fiyat ve panel davranışında V9.1 belgesi esas alınır.

---

## 7. Güncelleme sonrası test

```powershell
git pull --ff-only origin agent/tunix-matematik-topla

dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj

Push-Location .\Sirketler\Tunahan-Rustix\Tunix
cargo test
Pop-Location
```

Motor derlemesi ve şirket testleri geçmeden V9.1 davranışı doğrulanmış kabul edilmez. İlk gerçek çalıştırmada otomatik arşiv ve `v9.1-temiz-sezon-2.json` işaretli tam reset uygulanır.
