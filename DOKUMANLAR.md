# Proje Dokümanları

Bu dosya, Üç Kardeş Yazılım Şirketi Simülasyonu için okunması gereken teknik belgelerin ana indeksidir.

---

## Hızlı okuma sırası

### Oyuna yeni katılan şirket geliştiricisi

1. [SUNUCU_GUNCELLEME_V2_REHBERI.md](SUNUCU_GUNCELLEME_V2_REHBERI.md)
2. [HIZMET_SOZLESMELERI_V2.md](HIZMET_SOZLESMELERI_V2.md)
3. [SIBER_SALDIRI_SAVUNMA_REHBERI.md](SIBER_SALDIRI_SAVUNMA_REHBERI.md)
4. [UYGULAMA_MANIFESTI_V1.md](UYGULAMA_MANIFESTI_V1.md)
5. [SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)

### Motoru ve yeni ekosistemi çalıştıran kişi

1. [TINGRAM_TMAIL_ILOS_TECH_8080_PAKETI.md](TINGRAM_TMAIL_ILOS_TECH_8080_PAKETI.md)
2. [YAZILIM_BORSASI_V3_GUNCELLEME_REHBERI.md](YAZILIM_BORSASI_V3_GUNCELLEME_REHBERI.md)
3. [SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)
4. [SIBER_SALDIRI_SAVUNMA_REHBERI.md](SIBER_SALDIRI_SAVUNMA_REHBERI.md)
5. [UYGULAMA_MANIFESTI_V1.md](UYGULAMA_MANIFESTI_V1.md)

---

## Ana belgeler

### [TINGRAM_TMAIL_ILOS_TECH_8080_PAKETI.md](TINGRAM_TMAIL_ILOS_TECH_8080_PAKETI.md)

Son büyük ekosistem paketinin ana rehberi:

- Gelişmiş 8080 şirket değeri ve piyasa ekranı
- Uygulama/abonelik pazarı
- Protokol, banka, kredi, yatırım ve SLA tabloları
- Tunix 0.7.0
- Tingram ve Tmail gerçek hizmetleri
- Tlink özel protokolü
- Python tabanlı İlos Tech
- 10 ucuz temel İlos hizmeti
- İSosyal, İMail ve İLink
- Başlatma betiği
- 8090 ticari yayın adımları
- İlk açılış kontrol listesi
- Mevcut teknik sınırlar

### [YAZILIM_BORSASI_V3_GUNCELLEME_REHBERI.md](YAZILIM_BORSASI_V3_GUNCELLEME_REHBERI.md)

Bütün yeni oyun sistemlerinin genel özeti:

- 5.000 müşteri
- 10 temel hizmet
- Kalite/performans/güvenlik puanları
- Saldırı ekonomisi
- 8080 ve 8090
- Kod tabanlı uygulamalar
- Özel protokoller
- Abonelik ürünleri
- Yatırım ve kredi
- SLA sözleşmeleri
- Kalıcı veri mimarisi

### [SIBER_SALDIRI_SAVUNMA_REHBERI.md](SIBER_SALDIRI_SAVUNMA_REHBERI.md)

Şirketlerin saldırı yemesini önlemek için uygulama rehberi:

- Motor saldırı imzası
- Saldırı dalgaları ve olasılıklar
- Beş kontrollü saldırı türü
- Kabul edilen güvenlik hata kodları
- Finansal ve puan cezaları
- Rust, Go ve Node.js örnekleri
- Merkezi güvenlik katmanı
- Boyut, tekrar ve zaman limitleri

### [SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)

8090 şirket panelinin tam kullanım ve mimari rehberi:

- Kodlama ile yönetim arasındaki kesin sınır
- Giriş ve oturum
- Hizmet fiyat/aktiflik yönetimi
- Kodlanmış uygulama yayını
- Uygulama kapasitesi
- Özel protokol piyasası
- Altyapı yatırımları
- Banka ve kredi
- SLA sözleşmeleri
- HTTP uçları

### [UYGULAMA_MANIFESTI_V1.md](UYGULAMA_MANIFESTI_V1.md)

Şirket sunucularının uygulama ve protokol ilan standardı:

- `sirketTanitim` yeni alanları
- Uygulama manifesti
- Uygulama özelliği-hizmet bağlantısı
- Sosyal medya, e-posta, mesajlaşma ve diğer standartlar
- Uygulamalar arası bağımlılıklar
- Özel protokol manifesti

### [SUNUCU_GUNCELLEME_V2_REHBERI.md](SUNUCU_GUNCELLEME_V2_REHBERI.md)

Rust, Go ve Node.js şirket sunucularının motor v2 ile uyum rehberi.

### [HIZMET_SOZLESMELERI_V2.md](HIZMET_SOZLESMELERI_V2.md)

Motorun bağımsız doğruladığı 10 temel hizmetin kesin teknik sözleşmeleri.

---

## Şirket klasörleri

| Şirket | Dil | Klasör | Ürünler |
|---|---|---|---|
| Tunix | Rust | `Sirketler/Tunahan-Rustix/Tunix` | Tingram, Tmail, Tlink |
| Mudaf | Go | Kendi mevcut klasörü | Oyuncunun geliştirdikleri |
| Ugax | Node.js | Kendi mevcut klasörü | Oyuncunun geliştirdikleri |
| İlos Tech | Python | `Sirketler/Ilayda-Python/IlosTech` | İSosyal, İMail, İLink |

---

## Portlar

| Port | Sistem | Amaç |
|---:|---|---|
| 7001 | Tunix | Rust şirket sunucusu |
| 7002 | Mudaf | Go şirket sunucusu |
| 7003 | Ugax | Node.js şirket sunucusu |
| 7004 | İlos Tech | Python şirket sunucusu |
| 8080 | Yazılım Borsası | Şirket değeri, uygulamalar, abonelikler, protokoller ve piyasa |
| 8090 | Şirket İşletim Merkezi | Finansal ve ticari yönetim |

---

## Temel kural

```text
Kod ve işçilik şirket sunucusunda yapılır.
Motor teknik sonucu ve piyasa davranışını doğrular.
8080 salt-okunur piyasa ekranıdır.
8090 ticari ve finansal yönetim ekranıdır.
8090 hizmet, uygulama özelliği veya protokol kodu üretmez.
```

---

## Güncelleme sonrası asgari kontrol

```powershell
git pull --ff-only origin agent/tunix-matematik-topla
powershell -ExecutionPolicy Bypass -File .\BASLAT_TUNIX_ILOS_BORSA.ps1
```

Elle kontrol için:

```powershell
dotnet build .\SirketMotoru\SirketMotoru.csproj
cd .\Sirketler\Tunahan-Rustix\Tunix
cargo test
cd ..\..\..\Ilayda-Python\IlosTech
python -m unittest -v
```

---

## Belge bakım kuralı

Motor protokolü, hizmet şeması, uygulama standardı, saldırı davranışı, 8080 API'si veya 8090 uçları değiştiğinde ilgili belge aynı commit serisinde güncellenmelidir. Kod ile doküman birbiriyle çelişmemelidir.
