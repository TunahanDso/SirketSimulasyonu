# V9.1 Temiz Ekonomi, Kapasite, Talep ve Panel Rehberi

Bu belge şirket simülasyonunun güncel V9.1 kurallarını açıklar. Eski V6, V7, V8 ve ilk V9 ekonomi belgeleri tarihsel kabul edilir. Çelişkide bu belge üstündür.

## 1. Tek seferlik tam reset

Motor ilk V9.1 açılışında eski çalışma dosyalarını `MotorVerileri/Arsiv/V9.1-reset-oncesi-*` klasörüne taşır ve temiz sezon başlatır.

Sıfırlananlar:

- Şirket kasaları ve bütün bilanço sayaçları
- Toplam gelir, gider, ceza, saldırı ve iş geçmişleri
- Yatırım seviyeleri
- Krediler, gecikmeler, temerrütler ve ödenemeyen giderler
- Eski geçici destekler ve Ugax finansman kaydı
- Uygulama, işletim sistemi ve protokol piyasa kayıtları
- Eski satın alınmış hizmet veya uygulama kapasiteleri
- Müşterilerin eski OS, uygulama, sadakat ve işlem geçmişleri
- Fiyat, finans, ceza, haber, talep ve tick geçmişleri

Korunanlar:

- Şirketlerin Rust, Go, Node.js ve Python kaynak kodları
- Şirket manifestlerinde ilan edilen hizmet, uygulama, OS ve protokol kodları
- `motor-ayarlari.json`
- 500 hizmet ve 200 kategori statik katalog tanımları

Reset işareti `MotorVerileri/v9.1-temiz-sezon-2.json` dosyasıdır. İkinci açılışta sezon ilerlemesi korunur.

## 2. Eşit başlangıç

Dört şirket de yeni sezonda şu profille başlar:

- Kasa: 100.000 TL
- İtibar: 70
- Güvenilirlik: 70
- Kod kalitesi: 70
- Performans: 70
- Güvenlik: 70
- Müşteri memnuniyeti: 70
- Borç: 0
- Yatırım: 0

Şirketlerin sonraki farkı sunucu kodu, gerçek iş başarısı, ürün stratejisi, kapasite tahsisi, fiyatlandırma ve yatırım kararlarından doğar.

## 3. Tek ekonomi otoritesi

V9.1’de tick ekonomisini yalnız `V9EkonomiYoneticisi` yürütür.

Eski katmanlar artık aynı tickte ikinci kez gider, ürün geliri, kredi faizi veya kapasite yazamaz. Kalıcı işletim deposu yalnız şu işleri yapar:

- Yönetim hesabı ve parola kaydı
- Hizmet aktif/pasif tercihi
- Uygulama ve işletim sistemi kayıtları
- Protokol kayıtları ve benimseme
- SLA teklif ve sözleşme kayıtları
- Yatırım ve kredi verisinin kalıcı saklanması

Panel JSON'u okumak ekonomiyi veya kapasiteyi değiştirmez.

## 4. Çevrimdışı şirket kuralı

Şirketler istedikleri zaman açılıp kapanabilir. Motor hiçbir zaman 4/4 şirket beklemez.

Çevrimdışı şirket:

- Hizmet işi alamaz
- Uygulama ve OS talebi karşılamaz
- Kullanıcıları aktif rakiplere veya bekleme durumuna geçer
- İşletme gideri ödemez
- Kredi taksit saati ilerlemez
- SLA ihlal cezası almaz; sözleşme süresi donar
- Yatırım, puan ve finans geçmişi değişmez

Şirket yeniden bağlandığı tickte piyasaya geri döner.

## 5. Sabit hizmet fiyatları

500 standart hizmetin fiyatını motor belirler. Şirket manifestindeki fiyat ve eski 8090 fiyat kayıtları geçersizdir.

Aynı hizmet bütün şirketlerde aynı ücrete sahiptir. Hizmet rekabeti şu ölçütlerden oluşur:

- Kod kalitesi
- Doğru sonuç
- Gecikme
- Güvenilirlik
- Güvenlik
- Fiziksel kapasite tahsisi
- Müşteri sadakati

8090 hizmet fiyatı uç noktası hata döndürür. Hizmet fiyatı arayüzde yalnız okunur gösterilir.

## 6. Fiziksel kapasite havuzu

Hizmet veya uygulama için kapasite satın alınmaz. Her şirketin tek fiziksel kapasite havuzu vardır.

Başlangıç fiziksel kapasitesi 2.500 birimdir. Şirket 8090 `Kapasite Tahsisi` panelinde bu havuzu şu varlıklar arasında böler:

- `hizmet:<hizmetKimliği>@<sürüm>`
- `urun:<ürünKimliği>`

Kurallar:

- Tahsis toplamı fiziksel havuzu aşamaz.
- Hizmette her 10 tahsis birimi yaklaşık bir eşzamanlı iş kapasitesidir.
- Uygulama veya OS'de her tahsis birimi yaklaşık 40 kullanıcı kapasitesidir.
- Sıfır tahsis gerçek sıfır kapasitedir.
- Kapasite satın alma API'leri kapalıdır.

Toplam havuz yalnız altyapı yatırımlarıyla büyür.

## 7. Sınırsız fakat ağırlaşan yatırımlar

Yatırım seviyesinde sabit üst sınır yoktur. Her seviye bir önceki seviyeden yaklaşık 1,32 kat pahalıdır.

Yatırım aileleri:

- CPU
- RAM
- Ağ
- Depolama
- Güvenlik
- Yedek sunucu
- Destek
- Pazarlama
- Satış
- Ar-Ge

CPU, RAM, ağ, depolama, güvenlik, yedek ve Ar-Ge fiziksel kapasiteyi artırır. Bakım giderleri eski sürüme göre ciddi ölçüde azaltılmıştır.

## 8. Gider ve borç kuralları

İşletme gideri yalnız bağlı şirket için hesaplanır. Hafif formül şu unsurlardan oluşur:

- Düşük taban gider
- Aktif hizmet sayısı
- Aktif ürün sayısı
- Tahsis edilmiş ve gerçekten kullanılan kapasite
- Aktif kullanıcı sayısı
- Hafif yatırım bakımı

İlan edilmiş fakat kullanılmayan hizmetler şirketi borç batağına sürüklemez.

Otomatik kurtarma kredisi yoktur. Kredi yalnız oyuncu isterse kullanılır:

- 5.000–250.000 TL
- Sabit toplam yüzde 8 faiz
- 20 taksit
- Her 5 tickte ödeme
- Gecikmede borç büyümez
- Yalnız küçük kredi notu ve güvenilirlik kaybı oluşur

Ödenemeyen gider 20.000 TL tavanına sahiptir ve sonraki ticklerde sönümlenir.

## 9. Başarısız işler

Normal şirket kusurlu başarısız işlerde:

- Müşteri mümkünse sabit hizmet ücretinin yarısını öder.
- Şirket yarım gelir kazanır.
- İtibar, güvenilirlik ve kalite küçük adımlarla düşer.
- Tek hata şirketi bir anda çökertmez.

Motor eksik zorunlu alan gönderirse bu motor sözleşme kusurudur:

- Şirkete puan cezası yoktur.
- Şirkete finansal ceza yoktur.
- Müşteriden ödeme alınmaz.

Gerçek kimlik sahteciliği veya ciddi güvenlik ihlali ayrı değerlendirilir.

## 10. Siber saldırı maliyetleri

Normal başarılı saldırı kaybı çoğunlukla 20–350 TL arasındadır. Çok nadir büyük olaylar 1.500–5.000 TL aralığına çıkabilir.

Güvenlik yatırımı, güvenlik puanı ve sunucunun saldırıyı doğru reddetmesi riski azaltır. Her saldırı şirketi finansal olarak yok etmez.

## 11. Trendli hizmet talebi

20.000 müşterilik hizmet pazarında her tick en az yaklaşık 10.000 hizmet talebi modellenir.

TCP şirket sunucularına performans nedeniyle en fazla yaklaşık 1.000 temsilî iş gönderilir. Temsilî iş başarı oranı bütün pazara ölçeklenir.

Talep her tick sıfırdan rastgele kurulmaz:

- Önceki tick talebinin yüzde 78–82'si korunur.
- Yeni hedef kademeli eklenir.
- Ara sıra 6–15 tick süren yükselen veya düşen trendler oluşur.
- Trend bittiğinde talep yavaşça normale döner.

## 12. İşletim sistemi ve uygulama pazarı

Bütün 20.000 aktif müşteri işletim sistemi talep eder.

Aktif OS seçimi şu unsurlara bağlıdır:

- Fiyat
- Şirket kalite, performans ve güvenliği
- OS ürün kalitesi
- Boş kullanıcı kapasitesi
- Önceki müşteri tercihi

Müşteri seçimi kalıcı kayda yazılır ve her tick rastgele değişmez.

Aktif uygulama kategorilerinde müşteriler bir uygulama seçer. Uygulama tercihi, kullanım sayısı ve memnuniyet müşteri CV'sinde saklanır.

## 13. Uygulama ve OS fiyatları

Yalnız uygulama ve işletim sistemi fiyatları oyuncu tarafından belirlenebilir. Her ürün türü/kategori için motorun min–max aralığı vardır.

Örnek aralıklar:

- İşletim sistemi: 15–120 TL
- Sosyal, mesajlaşma ve e-posta: 1–60 TL
- Oyun: 2–80 TL
- Güvenlik: 8–180 TL
- Yapay zekâ: 15–250 TL

8090 fiyat alanı slider olarak gösterilir. Backend sınır dışı değeri otomatik olarak en yakın geçerli değere çeker.

## 14. Uygulama geliri

Aktif kullanıcı sayısı yalnız gerçek 20.000 müşteri dağılımından gelir.

Gelir modelleri:

- Abonelik: aktif kullanıcı × fiyat / 30
- Freemium: düşük abonelik dönüşümü + sınırlı kullanım geliri
- Kullanım: aktif kullanım oranına göre gelir
- Lisans/tek seferlik: yeni kazanılan kullanıcı üzerinden gelir

Gelir aynı tickte şirket kasasına, ürün toplam gelirine ve 8090 muhasebe özetine yazılır.

## 15. OS ve protokol bağlantısı

Her uygulama şu ikisini seçmeden aktif olamaz:

- Aktif bir işletim sistemi
- Aktif kanonik protokol

Protokolün teknik kimliği tek otoritedir. “Kendi protokolüm” ve “piyasa protokolü” ayrı bağlantı nesneleri değildir. Piyasa kaydı yalnız lisans/sahiplik bilgisidir.

Bir şirket başka şirketin OS ve protokolünü seçip şartları sağladığında uygulamayı tek işlemle yayına alabilir.

## 16. SLA sistemi

SLA teklifi şu şartları taşır:

- Gerekli hizmet ailesi
- Asgari kalite
- Asgari performans
- Asgari güvenlik
- Gerekli gerçek hizmet kapasitesi
- Tick ödemesi
- Süre

8090 teklifi kabul etmeden önce uygunluğu gösterir. Backend şartları tekrar doğrular.

Çevrimdışı şirketin SLA süresi ve finansı donar. Bağlı şirket şartları sağlarsa tick ödemesi alır. İhlal cezası eski sürüme göre küçüktür ve tick başına 10–150 TL ile sınırlıdır.

## 17. 8090 panelleri

V9.1 yönetim merkezinde:

- Genel şirket özeti
- Ayrıntılı arz-talep
- Fiziksel kapasite tahsisi
- Uygulama ve OS yayını
- 500 hizmet kataloğu
- 200 kategori kataloğu
- Tek kanonik protokol pazarı
- SLA uygunluğu
- Sınırsız yatırım mağazası
- Gelir-gider ve isteğe bağlı banka
- Parola yönetimi

bulunur.

Giriş yapılmadan `/api/durum` çağrılmaz. 401 cevabında sayfa yeniden yüklenmez. Tek DOM kökü kullanılır ve iç tabloların scroll konumları korunur.

## 18. 8080 panelleri

V9.1 borsa ekranında:

- Ana sayfa piyasa özeti
- Hizmet, uygulama ve OS arz-talebi
- Trend grafikleri
- Şirket gelir, gider, net ve kapasite karşılaştırması
- Uygulama pazarı
- İşletim sistemi pazarı
- Sabit fiyatlı hizmet pazarı
- Kalıcı müşteri CV örnekleri
- Haber bülteni
- Canlı motor akışı

bulunur.

Yalnız açık panel render edilir. Window ve iç tablo scroll konumları ayrı ayrı korunur. Haber şeridi metin uzunluğuna göre yaklaşık 180–520 saniyede akar ve fare üzerine gelince durur.

## 19. Şirketlerin yapması gerekenler

### Tunix

- Tunix OS, Tingram, Tmail ve Tlink manifestleri korunur.
- Hizmet fiyatı manifestten kontrol edilemez.
- OS ve uygulama için 8090 kapasite tahsisi yapılmalıdır.
- Tlink piyasaya tek kanonik kimlikle açılmalıdır.

### Mudaf

- Manifestte ilan edilen her hizmet Go yönlendiricisinde gerçekten çalışmalıdır.
- Uygulama yayınında aktif OS ve aktif protokol seçilmelidir.
- Rakip Tunix OS ve Tlink seçimi teknik olarak geçerlidir.
- Kategori zorunlu hizmetleri 8090 kategori panelinden kontrol edilmelidir.

### Ugax

- `BILINMEYEN_HIZMET` veya JavaScript `ReferenceError` üreten ilanlar düzeltilmeli ya da manifestten kaldırılmalıdır.
- Eski 500.000 TL geçici destek yoktur.
- Kapasite ve yatırım diğer şirketlerle aynı kurallara tabidir.

### İlos Tech

- Eski özel hizmet kimlikleri 500 standart hizmet kimlikleriyle eşleştirilmelidir.
- İSosyal ve İMail kategori zorunlu hizmetlerini sağlamalıdır.
- İLink tek kanonik protokol olarak yayınlanmalıdır.

## 20. Derleme ve test

Repo güncellendikten sonra:

```powershell
git pull --ff-only origin agent/tunix-matematik-topla

dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj
```

Tunix:

```powershell
Push-Location .\Sirketler\Tunahan-Rustix\Tunix
cargo test
Pop-Location
```

Motor:

```powershell
powershell -ExecutionPolicy Bypass -File .\BASLAT_TUNIX_ILOS_BORSA.ps1
```

İlk açılışta V9.1 reset, eşit başlangıç, yan etkisiz işletim deposu, V9.1 ekonomi ve V9.1 tick mesajları görülmelidir.
