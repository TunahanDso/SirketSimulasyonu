# Tunix OS Teknik Rehberi

Bu belge, Tunix şirket sunucusunda Rust ile geliştirilen `Tunix OS` ürününün teknik yapısını, motor manifestini, standart hizmetlerini, Tingram ve Tmail ile ilişkisini açıklar.

Tunix OS yalnızca motor ekranında oluşturulmuş bir ticari ürün değildir. Gerçek çalışan Rust hizmetleri bulunan, şirket sunucusundan manifest olarak ilan edilen bir işletim sistemi ürünüdür.

---

## 1. Konum ve sürüm

| Alan | Değer |
|---|---|
| Şirket | Tunix |
| Dil | Rust |
| Klasör | `Sirketler/Tunahan-Rustix/Tunix` |
| Port | `7001` |
| Şirket kimliği | `tunahan-tunix` |
| Sunucu sürümü | `0.8.0` |
| İşletim sistemi kimliği | `tunix-os` |
| İşletim sistemi sürümü | `1.0.0` |
| Bağlantı protokolü | `tunix-tlink` |

Ana kaynak dosyaları:

- `src/main.rs`
- `src/protocol.rs`
- `src/v6_manifest.rs`
- `src/services/tunix_os.rs`
- `src/services/platform_release.rs`
- `src/services/mod.rs`

---

## 2. Tunix OS ürün manifesti

Tunix OS manifestte şu temel alanlarla ilan edilir:

```json
{
  "uygulamaKimligi": "tunix-os",
  "uygulamaAdi": "Tunix OS",
  "surum": "1.0.0",
  "kategori": "isletim-sistemi",
  "urunTuru": "isletim-sistemi",
  "dagitimModeli": "sunucu-ve-istemci",
  "desteklenenProtokoller": ["tunix-tlink"],
  "desteklenenPlatformlar": ["tunix-os"],
  "mimariler": ["x86_64", "aarch64"]
}
```

Motor ürünü kabul etmeden önce:

- kategori standardını,
- zorunlu işletim sistemi hizmetlerini,
- şirketin bu hizmetleri gerçekten ilan edip etmediğini,
- Tlink protokolünün aktifliğini,
- uygulama/işletim sistemi bağlantı uyumluluğunu denetler.

---

## 3. Gerçek çalışan işletim sistemi hizmetleri

### Süreç yönetimi

| Hizmet | Görev |
|---|---|
| `isletim.surec-baslat` | Uygulama için yeni süreç oluşturur |
| `isletim.surec-durdur` | Çalışan süreci durdurur |
| `isletim.surec-listele` | Aktif süreçleri listeler |

Süreç kaydı uygulama kimliği, CPU birimi, RAM miktarı, başlangıç zamanı ve durum alanlarını taşır.

### Kaynak yönetimi

| Hizmet | Görev |
|---|---|
| `isletim.kaynak-ata` | Sürece CPU ve RAM tahsis eder |
| `isletim.kaynak-birak` | Süreç tahsisini serbest bırakır |

Bu hizmetler yalnız uygulama işlevi değildir. Motor tarafında CPU ve RAM yatırımlarının teknik ön koşulu olarak da kullanılabilir.

### Dosya sistemi

`isletim.dosya-sistemi` aşağıdaki işlemleri destekler:

- `yaz`,
- `oku`,
- `sil`,
- `listele`.

İstek boyutu ve dosya içeriği sınırlandırılır. Dosya yolu ve içerik doğrudan doğrulanır.

### Paket yönetimi

| Hizmet | Görev |
|---|---|
| `isletim.paket-kur` | Uygulama paketini sisteme kurar |
| `isletim.paket-kaldir` | Paketi sistemden kaldırır |

`isletim.uygulama-calistir`, paket kurulu değilse uygulamayı çalıştırmaz.

### Ağ yönetimi

`isletim.ag-yapilandir`, ağ profili ve adres bilgisini kaydeder. Ağ yatırımlarının teknik ön koşulu olarak kullanılır.

### Güncelleme ve bakım

| Hizmet | Görev |
|---|---|
| `isletim.guncelleme-kontrol` | Mevcut ve son sürümü karşılaştırır |
| `isletim.guncelleme-kur` | Belirtilen sürümü kurar |
| `isletim.log-topla` | Son sistem olaylarını döndürür |

### Güvenlik ve kimlik

| Hizmet | Görev |
|---|---|
| `kimlik.kullanici-dogrula` | Tunix OS kullanıcı kimliğini doğrular |
| `guvenlik.istek-dogrula` | Kaynak ve istek boyutuna göre risk puanı üretir |

Güvenlik denetimi `../`, `<script` ve aşırı büyük istek gibi belirtileri yüksek risk olarak değerlendirir.

---

## 4. Tunix OS durum modeli

Rust süreci içinde tutulan başlıca durumlar:

- süreç tablosu,
- süreç kaynak tahsisleri,
- sanal dosyalar,
- kurulu paketler,
- kayıtlı kullanıcılar,
- ağ profilleri,
- sistem logları.

Durum erişimi `Mutex` ile korunur. Hizmetler eş zamanlı motor istekleri altında aynı tablo üzerinde güvenli çalışır.

---

## 5. Tingram ile ilişki

Tingram artık özel `tunix.tingram.*` kimliklerini manifestte ilan etmek yerine motorun standart hizmet kimliklerini kullanır.

Zorunlu hizmetler:

- `kimlik.oturum-dogrula`
- `profil.profil-getir`
- `sosyal.gonderi-olustur`
- `sosyal.akisi-getir`
- `sosyal.etkilesim-kaydet`

Opsiyonel kalite hizmetleri:

- `sosyal.yorum-ekle`
- `sosyal.icerik-ara`

Dağıtım koşulları:

```text
İşletim sistemi: tunix-os
Protokol: tunix-tlink
Mimariler: x86_64, aarch64
Kategori: sosyal-medya
```

Tingram, Tmail'i opsiyonel uygulama bağımlılığı olarak ilan eder. Bu bağlantı Tlink üzerinden kurulur.

---

## 6. Tmail ile ilişki

Tmail standart e-posta hizmetlerini kullanır.

Zorunlu hizmetler:

- `kimlik.oturum-dogrula`
- `eposta.gonder`
- `eposta.gelen-kutusu`
- `eposta.ara`
- `eposta.spam-kontrol`

Opsiyonel hizmetler:

- `eposta.ek-yukle`
- `eposta.klasor-olustur`

Dağıtım koşulları:

```text
İşletim sistemi: tunix-os
Protokol: tunix-tlink
Kategori: eposta
```

Tmail'in gerçek Rust kodu; gönderme, gelen kutusu, arama, spam değerlendirmesi, ek ve klasör işlemlerini uygular.

---

## 7. Tlink protokolü

Tlink özel bir protokoldür fakat yetkinlikleri motorun standart hizmetlerine bağlanır.

| Tlink yetkinliği | Standart hizmet |
|---|---|
| Kimlik doğrulama | `kimlik.token-dogrula` |
| Paketleme | `dosya.sikistir` |
| Bağlantı doğrulama | `guvenlik.baglanti-dogrula` |

Rust yönlendiricisi bu standart kimlikleri mevcut Tlink koduna bağlar.

Tlink zarfı genel olarak şu kavramları taşır:

- protokol kimliği,
- sürüm,
- kaynak,
- hedef,
- veri,
- bütünlük bilgisi.

---

## 8. Motor ile kayıt akışı

1. Motor `merhaba` mesajı gönderir.
2. Tunix `sirketTanitim` döndürür.
3. Tanıtımda hizmetler, Tunix OS, Tingram, Tmail ve Tlink bulunur.
4. Motor yalnız katalogda olan hizmetleri kabul eder.
5. Uygulama manifestleri kod tabanlı yayın deposuna yazılır.
6. 8090'dan ticari yayın ve kapasite tahsisi yapılır.
7. Motor kayıtlı ticari ayarları sonraki bağlantılarda korur.

---

## 9. Testler

```powershell
cd .\Sirketler\Tunahan-Rustix\Tunix
cargo test
cargo run
```

Kontrol edilmesi gerekenler:

- bütün Rust testlerinin geçmesi,
- konsolda `TUNIX OS · TINGRAM · TMAIL · TLINK` başlığının görünmesi,
- sunucu sürümünün `0.8.0` olması,
- üç uygulamanın ilan edilmesi,
- motorun hizmetleri reddetmemesi,
- Tunix OS ürününün 8090'da görünmesi,
- Tingram ve Tmail'in `tunix-os` ve `tunix-tlink` ile uyumlu görünmesi.

---

## 10. Örnek süreç isteği

```json
{
  "uygulamaKimligi": "tunix-tingram",
  "cpuBirimi": 2,
  "ramMb": 512
}
```

`isletim.surec-baslat` başarılı olduğunda süreç kimliği ve `calisiyor` durumu döner.

---

## 11. Bilinen sınırlar

Tunix OS şu anda oyun içi işletim sistemi simülasyonudur; gerçek makinenin kernel'i değildir. Bununla birlikte oyunun amacı açısından:

- hizmetleri gerçek Rust koduyla çalıştırır,
- uygulama süreç ve paket durumunu tutar,
- motor taleplerine gerçek yanıt üretir,
- kapasite ve yatırım ekonomisine teknik ön koşul sağlar,
- platform/protokol ekosisteminde bağımsız ürün olarak yarışır.
