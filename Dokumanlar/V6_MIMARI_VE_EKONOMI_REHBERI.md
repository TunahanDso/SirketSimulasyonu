# V6 Mimari ve Ekonomi Rehberi

Bu belge, Üç Kardeş Yazılım Şirketi Simülasyonu'nda son büyük güncelleme serisiyle kurulan V6 mimarisini tek yerde açıklar.

V6'nın temel amacı, oyunu yalnızca hizmet çağrılarının puanlandığı bir yarıştan çıkarıp gerçek bir yazılım ekonomisi simülasyonuna dönüştürmektir. Şirketler kod yazar, hizmet ve uygulama manifesti yayımlar, motor bunları teknik olarak doğrular, fiziksel kapasiteyi tüketir, fiyat ve kullanıcı pazarını işletir, risk ve olay üretir.

---

## 1. Değişmeyen ana kural

```text
Kod ve işçilik şirket sunucusunda yapılır.
8080 yalnız gözlem ve piyasa ekranıdır.
8090 yalnız yönetim, fiyat, tahsis, finans ve yayın kontrolüdür.
8090 yeni hizmet, uygulama özelliği veya protokol kodu üretmez.
```

Bir uygulamanın piyasaya çıkması için şirket sunucusunun bağlı olması, uygulamanın kod tabanlı manifestte ilan edilmesi ve manifestteki bütün zorunlu hizmetlerin gerçekten şirket tarafından yayımlanması gerekir.

---

## 2. V6'nın ana bileşenleri

| Katman | Sorumluluk |
|---|---|
| Şirket sunucuları | Gerçek hizmet kodu, uygulama kodu, işletim sistemi, protokol ve teknik manifest |
| `HizmetKatalogu` | Motorun tanıdığı standart hizmet sözleşmeleri |
| `StandartKatalogV6` | 500 standart hizmeti ve 200 uygulama kategorisini deterministik üretir |
| `KodTabanliSirketIsletimYoneticisi` | Sunucu manifestini piyasadaki ticari ürün kaydıyla eşler |
| `EkosistemYoneticisi` | İşletim sistemi, protokol, platform ve bağımlılık uyumluluğunu denetler |
| `EkonomiV6Yoneticisi` | Gerçek kapasite havuzu, yatırım süreci, müşteri CV'si, haber ve grafik verileri |
| `PazarFiyatYoneticisi` | Kategori bazlı adil fiyat, talep, kullanıcı kaçışı ve rakibe göç |
| `PazarGelirDuzeltmeYoneticisi` | Pazar dışı fiyatlardan yazılan hayalî geliri iade/chargeback ile geri alır |
| 8080 | Salt-okunur yazılım borsası ve TV panosu |
| 8090 | Şirketin kendi ticari ve altyapı yönetim merkezi |

---

## 3. Motor kayıtları ile şirket manifesti arasındaki otorite

Şirket sunucusundan gelen değerler yalnızca ilk varsayılandır.

### İlk kez görülen hizmet veya uygulama

Motor şu değerleri sunucudan alır ve kalıcı kayda yazar:

- fiyat,
- aktiflik,
- ilk kapasite,
- hizmet sürümü,
- uygulama kategorisi,
- özellik/hizmet listesi,
- platform ve protokol desteği.

### Daha önce kayıtlı hizmet veya uygulama yeniden bağlanırsa

Motorun kalıcı kaydı üstün gelir:

- 8090'dan değiştirilmiş fiyat korunur,
- 8090'dan değiştirilmiş aktiflik korunur,
- satın alınan/tahsis edilen kapasite korunur,
- geçmiş kullanıcı ve gelir kaydı korunur,
- yalnız manifestte gerçekten yeni bir varlık varsa yeni varsayılan kayıt oluşturulur.

Bu nedenle şirket sunucusunu kapatıp açmak, motoru yeniden başlatmak veya manifesti yeniden göndermek ticari ayarları sıfırlamaz.

---

## 4. 500 standart hizmet

V6 kataloğu 25 hizmet ailesinin her birinde 20 işlev üretir. Toplam tam olarak 500 standart hizmet vardır.

Başlıca aileler:

- kimlik,
- profil,
- sosyal,
- e-posta,
- mesajlaşma,
- dosya,
- medya,
- arama,
- analitik,
- bildirim,
- ödeme,
- ticaret,
- veritabanı,
- güvenlik,
- yapay zekâ,
- konum,
- takvim,
- belge,
- iş akışı,
- geliştirme,
- işletim sistemi,
- IoT,
- eğitim,
- sağlık,
- oyun.

Katalog dosyasındaki çekirdek hizmetler başlangıç tohumu olarak korunur; motor açılışta `StandartKatalogV6.Genislet(...)` ile tam kataloğu oluşturur ve sayının 500 olduğunu doğrular.

---

## 5. 200 uygulama kategorisi

Kategori artık serbest metin değildir. Motor 20 sektör ile 10 uygulama arketipini birleştirerek tam 200 kategori üretir.

Örnek temel kategoriler:

- sosyal medya,
- e-posta,
- mesajlaşma,
- bulut depolama,
- e-ticaret,
- analitik,
- güvenlik,
- geliştirici aracı,
- işletim sistemi,
- oyun.

Aynı arketipler finans, sağlık, eğitim, kamu, üretim, perakende, lojistik, medya ve diğer sektörlere uygulanabilir.

Her kategori şunları tanımlar:

- zorunlu hizmetler,
- izin verilen ek hizmet aileleri,
- asgari hizmet sayısı,
- önerilen hizmet sayısı,
- azami hizmet sayısı,
- ek hizmetlerin kalite katkısı,
- taban kapasite tüketimi,
- kullanıcı başına kapasite tüketimi.

Kategori standardına uymayan uygulama otomatik olarak pasife alınır; geçmiş kaydı silinmez.

---

## 6. İşletim sistemi ve protokol zorunluluğu

Her müşteri bir işletim sistemi kullanır ve bunu zaman içinde değiştirebilir.

Her normal uygulama:

1. en az bir işletim sistemi/platform üzerinde çalışmalı,
2. uygulama ile işletim sistemi arasındaki bağlantı için bir protokol ilan etmeli,
3. seçilen işletim sisteminin aynı protokolü desteklemesi gerekir.

İşletim sistemi ürünleri de en az bir aktif protokol ilan etmeden müşteri kabul edemez.

Eksik işletim sistemi veya protokol yapılandırması olan eski uygulamalar silinmez; motor bunları pasife alır. Şirket 8090'dan uygun platform ve protokolü seçip yeniden yayına alabilir.

---

## 7. Tek gerçek kapasite havuzu

V6'da uygulama kapasitesi ve hizmet kapasitesi birbirinden bağımsız şekilde yoktan üretilmez.

Her şirketin bir fiziksel kapasite havuzu vardır. Bu havuz şu unsurlardan oluşur:

- sunucudan gelen temel hizmet kapasitesi,
- tamamlanmış CPU yatırımları,
- tamamlanmış RAM yatırımları,
- ağ yatırımları,
- depolama yatırımları,
- yedekleme ve güvenlik etkileri,
- kod kalitesi,
- performans,
- güvenlik,
- güvenilirlik.

Hizmet veya uygulama için 8090'dan “kapasite al” işlemi fiziksel donanım üretmez. Yalnızca mevcut fiziksel havuzdan o varlığa tahsis talebi oluşturur.

Gösterilen ana değerler:

- gerçek toplam kapasite,
- tahsis edilen kapasite,
- kullanılan kapasite,
- boş kapasite,
- doluluk oranı,
- hizmet baz kapasitesi,
- hizmet tahsis kapasitesi,
- uygulama kullanıcı tahsisi.

---

## 8. Aşırı yük ve duraksatma

Kullanım gerçek havuzu aşarsa motor şu sonuçları üretir:

- gecikme artışı,
- kuyruk büyümesi,
- performans puanı kaybı,
- güvenilirlik puanı kaybı,
- kullanıcı memnuniyeti kaybı,
- uygulamanın otomatik duraksatılması,
- kritik durumda hizmetlerin ve işletim sisteminin geçici durması,
- SLA ve itibar cezası,
- haber bültenine olay düşmesi.

İyi kod ve doğru yatırım riski azaltır; hiçbir şirketi sonsuza kadar tamamen risksiz hâle getirmez.

---

## 9. Sınırsız fakat ağırlaşan yatırımlar

Yatırım seviyesi 20 ile sınırlı değildir. Teknik olarak sınırsızdır.

Ancak her yeni seviye:

- bir önceki seviyeden daha pahalıdır,
- tamamlanması daha uzun sürer,
- belirli bir standart hizmetin şirkette gerçekten çalışmasını gerektirir,
- satın alındığı anda değil, inşaat/uygulama süreci bittikten sonra kapasiteye eklenir.

Örnek ön koşullar:

| Yatırım | Gerekli çalışan hizmet |
|---|---|
| CPU | `isletim.kaynak-ata` |
| RAM | `isletim.surec-listele` |
| Ağ | `isletim.ag-yapilandir` |
| Depolama | `dosya.yukle` |
| Güvenlik | `guvenlik.saldiri-tespit` |
| Yedekleme | `veritabani.yedek-al` |
| Destek | `bildirim.gonder` |
| Pazarlama | `analitik.kullanici-yolu` |
| Satış | `ticaret.siparis-olustur` |
| Ar-Ge | `gelistirme.test-calistir` |

Para ödenmiş olsa bile zorunlu teknik hizmet yoksa yatırım gerçek kapasiteye eklenmez ve “bloke” durumda kalır.

---

## 10. Fiyat pazarı

Her kategori kendi fiyat pazarına sahiptir. Motor şunları birlikte değerlendirir:

- kategori referans fiyatı,
- rakiplerin medyan fiyatı,
- ürün kalitesi,
- şirket itibarı,
- müşteri bütçesi,
- fiyat hassasiyeti,
- kapasite ve kesinti geçmişi.

Aşırı pahalı ürün:

- yeni kullanıcı kazanamaz,
- kullanıcı kaybeder,
- kullanıcıları uygun rakibe göç eder,
- itibar ve şirket değeri kaybeder,
- pazar dışı gelir chargeback ile geri alınır.

Aşırı ucuz ürün kullanıcı çekebilir fakat kullanıcı başı altyapı gideri, aşırı yük ve zarar riski oluşturur.

Ayrıntı: [FIYAT_PAZARI_VE_KULLANICI_GOCU.md](FIYAT_PAZARI_VE_KULLANICI_GOCU.md)

---

## 11. Şirket değeri

Şirket değeri yalnız kasaya bağlı değildir. Değerleme şunları içerir:

- gelir geçmişi,
- son tick performansı,
- aktif kullanıcı ve abone sayısı,
- hizmet ve uygulama sayısı,
- gerçek kapasite,
- boş kapasite,
- ürün kalitesi,
- kod kalitesi,
- güvenlik ve güvenilirlik,
- müşteri memnuniyeti,
- ekosistem ve protokol benimsenmesi,
- borçlar,
- teknik borç,
- kesinti ve olay geçmişi.

---

## 12. Tick sırası

V6 tick sırası özetle şöyledir:

1. müşterilerin tick başı durumu güncellenir,
2. şirket bağlantıları ve sağlık kontrolleri çalışır,
3. işletim sistemi/protokol pazarı hazırlanır,
4. müşteriler işletim sistemi seçer veya değiştirir,
5. V6 kategori ve gerçek kapasite denetimi uygulanır,
6. fiyat öncesi gelir/kullanıcı anlık görüntüsü alınır,
7. ürün ve işletme ekonomisi çalışır,
8. fiyat pazarı kullanıcı kaçışı ve göç uygular,
9. pazar dışı gelirler chargeback ile geri alınır,
10. müşteri hizmet talepleri oluşturulur,
11. işler şirketlere atanır ve gerçek sunucularda çalıştırılır,
12. bilançolar kaydedilir,
13. müşteri CV'leri, haberler ve grafik geçmişi güncellenir.

---

## 13. Kalıcı dosyalar

Motor çalışma verileri Git'e yazılmamalıdır. Başlıca kalıcı çalışma dosyaları:

- `musteriler.json`,
- şirket bilanço kayıtları,
- `sirket-isletim.json`,
- `kod-tabanli-yayinlar.json`,
- `ekosistem.json`,
- `pazar-fiyat.json`,
- `ekonomi-v6.json`.

Bu dosyalar motor yeniden başlatıldığında geçmişi korur.

---

## 14. Derleme ve test

```powershell
git pull --ff-only origin agent/tunix-matematik-topla

dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj

cd .\Sirketler\Tunahan-Rustix\Tunix
cargo test
cargo run
```

V6 büyük bir geçiştir. Derleme başarılı olmadan üretim kayıtlarıyla motoru uzun süre çalıştırmayın. İlk açılış öncesinde çalışma JSON dosyalarının yedeğini almak önerilir.
