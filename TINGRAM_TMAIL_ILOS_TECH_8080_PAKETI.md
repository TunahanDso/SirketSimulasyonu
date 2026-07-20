# Tingram, Tmail, Tlink, İlos Tech ve 8080 Yazılım Borsası Paketi

**Dal:** `agent/tunix-matematik-topla`  
**Motor:** C# / .NET 8  
**Tunix:** Rust / `7001`  
**İlos Tech:** Python / `7004`  
**Canlı Yazılım Borsası:** `8080`  
**Şirket İşletim Merkezi:** `8090`

Bu paket dört büyük geliştirmeyi birlikte getirir:

1. `8080` ekranının şirket değeri, uygulama, abonelik, protokol, kredi, yatırım ve SLA verilerini gösteren tam yazılım borsasına dönüşmesi.
2. Tunix sunucusunda gerçek kodla **Tingram** sosyal medya uygulamasının geliştirilmesi.
3. Tunix sunucusunda gerçek kodla **Tmail** e-posta uygulaması ve **Tlink** özel protokolünün geliştirilmesi.
4. İlayda adına Python tabanlı **İlos Tech** şirketinin, **İSosyal**, **İMail** ve **İLink** ekosistemiyle oyuna eklenmesi.

---

## 1. Değişmeyen temel oyun kuralı

```text
Kodlama ve işçilik şirket sunucusunda yapılır.
Motor teknik gerçekliği ve sonuçları doğrular.
8080 salt-okunur piyasa ekranıdır.
8090 finansal ve ticari yönetim ekranıdır.
```

`8080` ve `8090` hiçbir uygulama özelliği kodlamaz. Tingram, Tmail, Tlink, İSosyal, İMail ve İLink özelliklerinin gerçek kodları şirket sunucularındadır.

Teknik ve ticari yayın birbirinden ayrıdır:

```text
Sunucu çalışır
→ sirketTanitim manifesti motora gider
→ motor zorunlu özellikleri kontrol eder
→ uygulama 8090'da "sunucuda kodlandı" olarak görünür
→ şirket oyuncusu fiyat/model seçerek piyasaya açar
→ uygulama ve abonelik verileri 8080'de yayınlanır
```

---

## 2. Gelişmiş 8080 Yazılım Borsası

Yeni pano sınıfları:

```text
SirketMotoru/CanliPano/YazilimBorsasiSunucusu.cs
SirketMotoru/CanliPano/YazilimBorsasiHtml.cs
```

Motor artık eski temel pano yerine bu borsa sunucusunu açar.

### Genel piyasa göstergeleri

- Toplam şirket değeri
- Toplam şirket kasası
- Toplam net gelir
- Toplam borç
- Toplam abone
- Teknik olarak kodlanmış uygulama sayısı
- Ticari olarak piyasaya açılmış uygulama sayısı
- Teknik ve ticari özel protokol sayıları
- Bağlı şirket sayısı
- Toplam hizmet ilanı
- Engellenen ve başarılı saldırılar
- Toplam güvenlik kaybı
- Aktif müşteri sayısı

### Şirket değer listesi

Her şirket kartında:

- Şirket değeri
- Tahmini hisse fiyatı
- Kasa ve borç
- Kredi notu
- Abone sayısı
- Kod kalitesi
- Performans
- Güvenlik
- İtibar
- Güvenilirlik
- Müşteri memnuniyeti
- Teknik/ticari uygulamalar
- Özel protokoller
- Yapılmış altyapı yatırımları
- Sunucu bağlantısı ve gecikmesi

bulunur.

### Uygulama ve abonelik pazarı

Her uygulama için:

- Teknik uygulama kimliği
- Sahip şirket
- Kategori
- Standart doğrulama sonucu
- Ticari yayın durumu
- Fiyatlandırma modeli
- Abonelik fiyatı
- Kullanım başına fiyat
- Aktif kullanıcı
- Satın alınan kullanıcı kapasitesi
- Ürün kalitesi
- Ürün memnuniyeti
- Toplam gelir
- Desteklenen protokoller

izlenir.

### Özel protokol piyasası

- Protokol adı ve kimliği
- Sahip şirket
- Sürüm
- Şema kimliği ve özeti
- Teknik geçerlilik
- Ticari yayın durumu
- Gerçek yetkinlik hizmetleri
- Uyumlu protokoller
- Piyasa protokol kimliği

### Finans ve banka

- Şirket değeri ve hisse tahmini
- Kasa ve borç
- Kredi notu
- Abonelik ve ürün gelirleri
- İşletme ve finansman giderleri
- Aktif/kapanmış krediler
- Ana para, kalan borç, taksit, faiz ve gecikme

### SLA ve yatırımlar

- Aktif/geçmiş sözleşmeler
- Tick ödemesi
- İhlal cezası
- Başarılı ve ihlal edilen tick sayıları
- CPU, RAM, ağ, depolama, güvenlik, yedekleme, destek, satış, pazarlama ve Ar-Ge seviyeleri

### Güvenlik

- Şirket güvenlik sıralaması
- Engellenen saldırılar
- Başarılı saldırılar
- Finansal güvenlik kaybı
- Risk seviyesi
- Son 300 motor olayı

---

## 3. Tunix 0.7.0 ekosistemi

Tunix artık toplam 26 hizmet ilan eder:

- 10 temel motor hizmeti
- 7 Tingram/kimlik hizmeti
- 6 Tmail hizmeti
- 3 Tlink yetkinlik hizmeti

### Güvenlik

Tüm hizmetler tek merkezi güvenlik kapısından geçer:

1. Azami TCP satır boyutu
2. JSON zarfı
3. Motor saldırı imzası
4. Son 5.000 iş için tekrar kimliği koruması
5. Hizmet yönlendirme
6. Hizmet verisi doğrulama
7. Gerçek işlem süresi
8. Standart `isSonucu`

Saldırı cevabı:

```json
{
  "basarili": false,
  "hataKodu": "GUVENLIK_REDDI"
}
```

Tekrar cevabı:

```json
{
  "basarili": false,
  "hataKodu": "KOTU_NIYETLI_ISTEK"
}
```

---

## 4. Tingram

**Uygulama kimliği:** `tunix-tingram`  
**Kategori:** `sosyal-medya`  
**Sürüm:** `1.0`

### Zorunlu özellikler

| Standart özellik | Gerçek Tunix hizmeti |
|---|---|
| `kimlik.dogrula` | `tunix.kimlik.dogrula` |
| `sosyal.profil.getir` | `tunix.tingram.profil.getir` |
| `sosyal.gonderi.olustur` | `tunix.tingram.gonderi.olustur` |
| `sosyal.akisi.getir` | `tunix.tingram.akisi.getir` |
| `sosyal.etkilesim` | `tunix.tingram.etkilesim` |

### Opsiyonel özellikler

| Özellik | Hizmet |
|---|---|
| Yorum | `tunix.tingram.yorum` |
| Arama | `tunix.tingram.arama` |

Tingram şu anda:

- kullanıcı profili oluşturur/getirir,
- gönderi oluşturur,
- kronolojik akış üretir,
- beğeni/alkış/destek etkileşimlerini tutar,
- yorum sonucu üretir,
- gönderi metninde arama yapar.

Veriler Tunix process belleğinde, thread-safe `Mutex` korumalı platform durumunda tutulur. Sunucu kapanınca mevcut ilk sürümün uygulama içi kayıtları sıfırlanır. Kalıcı şirket ekonomisi ise motor dosyalarında korunur.

Tingram, Tmail bildirim bağlantısını opsiyonel bağımlılık olarak ve Tlink'i desteklenen protokol olarak ilan eder.

---

## 5. Tmail

**Uygulama kimliği:** `tunix-tmail`  
**Kategori:** `eposta`  
**Sürüm:** `1.0`

### Zorunlu özellikler

| Standart özellik | Gerçek Tunix hizmeti |
|---|---|
| `kimlik.dogrula` | `tunix.kimlik.dogrula` |
| `eposta.gonder` | `tunix.tmail.gonder` |
| `eposta.gelen-kutusu` | `tunix.tmail.gelen-kutusu` |
| `eposta.ara` | `tunix.tmail.ara` |
| `eposta.spam-kontrol` | `tunix.tmail.spam-kontrol` |

### Opsiyonel özellikler

| Özellik | Hizmet |
|---|---|
| Ek yükleme | `tunix.tmail.ek-yukle` |
| Klasörler | `tunix.tmail.klasor` |

Tmail:

- gönderen/alıcı/konu/gövde doğrulaması yapar,
- e-postayı gelen kutusuna kaydeder,
- kullanıcıya göre gelen kutusunu getirir,
- konu/gövde/gönderen alanlarında arama yapar,
- kontrollü spam puanı hesaplar,
- 25 MB'a kadar ek meta verisi oluşturur,
- standart klasörleri döndürür.

---

## 6. Tlink

**Protokol kimliği:** `tunix-tlink`  
**Ad:** `Tlink`  
**Sürüm:** `1.0`  
**Şema:** `tlink-json-envelope-v1`

Gerçek yetkinlik hizmetleri:

| Yetkinlik | Hizmet |
|---|---|
| Kimlik/sürüm müzakeresi | `tunix.tlink.kimlik` |
| Paket oluşturma | `tunix.tlink.paketle` |
| Paket doğrulama | `tunix.tlink.dogrula` |

Paket yapısı:

```json
{
  "protokol": "tunix-tlink",
  "surum": "1.0",
  "kaynak": "tingram",
  "hedef": "tmail",
  "veri": {
    "olay": "yeni-bildirim"
  },
  "butunluk": 12345
}
```

Tlink, İlos Tech'in `ilos-ilink` protokolüyle uyumlu protokol olarak ilan edilir. Bu ilk sürümde uyumluluk ortak JSON zarfı ve yetkinlik eşleme düzeyindedir; şirketler arası gerçek uzak çağrı yönlendirmesi sonraki entegrasyon paketidir.

---

## 7. İlos Tech

**Şirket kimliği:** `ilayda-ilos-tech`  
**Şirket adı:** `İlos Tech`  
**Dil:** Python 3.11+  
**Port:** `7004`

### Başlangıç profili

Yalnız ilk kalıcı bilanço oluşturulurken:

| Değer | Başlangıç |
|---|---:|
| Kasa | 40.000 TL |
| Kod kalitesi | 84 |
| Performans | 82 |
| Güvenlik | 88 |
| Güvenilirlik | 80 |
| İtibar | 72 |
| Memnuniyet | 76 |

Sonraki açılışlarda kayıtlı bilanço ve gerçek oyun sonuçları kullanılır.

### Temel motor hizmetleri

İlos Tech, motorun bağımsız doğruladığı 10 temel hizmetin tamamını sunar. Fiyatları:

```text
matematik.topla                 2 TL
matematik.carp                  3 TL
veri.ortalama-hesapla           4 TL
metin.kelime-say                2 TL
metin.karakter-say              2 TL
veri.medyan-hesapla             5 TL
veri.standart-sapma             6 TL
dizi.sirala                     5 TL
matematik.asal-carpanlar        7 TL
metin.frekans-analizi           5 TL
```

Ucuz fiyatın yanında:

- yüksek eşzamanlı kapasite,
- merkezi saldırı reddi,
- tekrar koruması,
- UTF-16 karakter sayımı,
- popülasyon standart sapması,
- gerçek süre ölçümü,
- dizi/metin/sayı doğrulaması

uygulanır.

---

## 8. İSosyal

**Uygulama kimliği:** `ilos-isosyal`

Zorunlu hizmetler:

```text
ilos.kimlik.dogrula
ilos.isosyal.profil.getir
ilos.isosyal.gonderi.olustur
ilos.isosyal.akisi.getir
ilos.isosyal.etkilesim
```

Opsiyonel:

```text
ilos.isosyal.yorum
ilos.isosyal.arama
```

İSosyal, İMail'e opsiyonel uygulama bağlantısı ve İLink desteği ilan eder.

---

## 9. İMail

**Uygulama kimliği:** `ilos-imail`

Zorunlu hizmetler:

```text
ilos.kimlik.dogrula
ilos.imail.gonder
ilos.imail.gelen-kutusu
ilos.imail.ara
ilos.imail.spam-kontrol
```

Opsiyonel:

```text
ilos.imail.ek-yukle
ilos.imail.klasor
```

---

## 10. İLink

**Protokol kimliği:** `ilos-ilink`

Yetkinlikler:

```text
ilos.ilink.kimlik
ilos.ilink.paketle
ilos.ilink.dogrula
```

İLink, `tunix-tlink` protokolünü uyumlu protokol olarak ilan eder.

---

## 11. Teknik yayın ve ticari yayın

Kod ve manifestler repoda hazırdır. Ancak şirket oyuncularının ticari kararları otomatik verilmez.

### Tunix için 8090

Tunix hesabıyla:

1. `Tingram` uygulamasını piyasaya açın.
2. `Tmail` uygulamasını piyasaya açın.
3. Abonelik/kullanım fiyatlarını belirleyin.
4. `Tlink` protokolünü lisans modeliyle yayınlayın.
5. Kullanıcı kapasitesi ve altyapı satın alın.

Önerilen başlangıç dengesi:

| Ürün | Model | Abonelik | Kullanım |
|---|---|---:|---:|
| Tingram | freemium | 12 TL | 0,05 TL |
| Tmail | abonelik | 18 TL | 0,08 TL |
| Tlink | karma lisans | benimseme 1.500 TL | tick lisansı 40 TL |

### İlos Tech için 8090

İlos Tech hesabıyla:

| Ürün | Model | Abonelik | Kullanım |
|---|---|---:|---:|
| İSosyal | freemium | 8 TL | 0,03 TL |
| İMail | abonelik | 12 TL | 0,04 TL |
| İLink | açık/karma | isteğe göre | isteğe göre |

Bu fiyatlar yalnız başlangıç önerisidir. Şirket oyuncusu 8090 üzerinden değiştirebilir.

---

## 12. Çalıştırma

### Güncellemeyi çekme

```powershell
git pull --ff-only origin agent/tunix-matematik-topla
```

### Her şeyi ayrı terminallerde açan betik

Repo kökünde:

```powershell
powershell -ExecutionPolicy Bypass -File .\BASLAT_TUNIX_ILOS_BORSA.ps1
```

Bu betik:

1. Tunix için `cargo test` ve `cargo run`,
2. İlos Tech için Python testleri ve `start.py`,
3. Motor için `dotnet run`

komutlarını ayrı pencerelerde başlatır.

Mudaf ve Ugax kendi bilgisayarlarında ayrıca açık olmalıdır.

### Elle çalıştırma

Tunix:

```powershell
cd .\Sirketler\Tunahan-Rustix\Tunix
cargo test
cargo run
```

İlos Tech:

```powershell
cd .\Sirketler\Ilayda-Python\IlosTech
python -m unittest -v
python .\start.py
```

Motor:

```powershell
cd <repo-koku>
dotnet build .\SirketMotoru\SirketMotoru.csproj
dotnet run --project .\SirketMotoru\SirketMotoru.csproj
```

Adresler:

```text
http://localhost:8080/  → Yazılım Borsası
http://localhost:8090/  → Şirket İşletim Merkezi
```

---

## 13. İlk açılış kontrol listesi

- [ ] `cargo test` geçti.
- [ ] Python testleri geçti.
- [ ] `.NET` motoru derlendi.
- [ ] Tunix `0.7.0` olarak bağlandı.
- [ ] İlos Tech `7004` üzerinden bağlandı.
- [ ] Motor dört şirket gördü.
- [ ] 8080'de şirket değer listesinde İlos Tech göründü.
- [ ] Tingram ve Tmail teknik uygulama olarak göründü.
- [ ] İSosyal ve İMail teknik uygulama olarak göründü.
- [ ] Tlink ve İLink teknik protokol olarak göründü.
- [ ] 8090'da uygulamalar kategori standardını geçti.
- [ ] Şirket oyuncuları uygulamaları ticari olarak piyasaya açtı.
- [ ] 8080 uygulama tablosunda fiyat, kapasite, kullanıcı ve gelir alanları göründü.

---

## 14. Mevcut sınırlar

Bu pakette gerçek olarak tamamlananlar:

- uygulama özelliklerinin şirket sunucusunda kodlanması,
- teknik manifest,
- kategori standardı,
- ticari yayın yönetimi,
- uygulama kullanıcı/gelir simülasyonu,
- protokol yetkinliklerinin gerçek hizmetlere bağlanması,
- gelişmiş 8080 piyasa görünümü.

Henüz ayrı geliştirme isteyenler:

- uygulama içi verilerin diskte kalıcı saklanması,
- motorun özel uygulama özelliklerine otomatik sentetik kullanıcı işlemleri göndermesi,
- iki şirket sunucusu arasında gerçek doğrudan servis çağrısı,
- Tlink/İLink için tam ortak kanonik imza standardı,
- gerçek hisse alım-satım emir defteri,
- uygulama bazlı gizli kalite testleri.

Bu sınırlar uygulamaların kodlanmış olmadığı anlamına gelmez; ilk sürümde motor ekonomisi ile uygulama içi işlem/veri kalıcılığının kapsamının ayrıldığını gösterir.
