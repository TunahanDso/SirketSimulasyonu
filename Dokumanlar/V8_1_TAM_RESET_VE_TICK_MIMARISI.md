# V8.1 Tam Şirket Reseti ve Tick Mimarisi

## Amaç

V8.1, V5–V8 geliştirme sürecinde biriken deneysel bilanço, ceza, saldırı, yatırım, kredi ve tick kayıtlarının yeni ekonomiyi bozmasını engellemek için temiz bir sezon başlatır.

Bu işlem şirket sunucularındaki kodu silmez. Hizmet ve uygulama tanımları korunur; yalnız motorun tuttuğu eski finansal ve operasyonel ilerleme sıfırlanır.

## İlk açılışta yapılan tek seferlik reset

Motor ilk V8.1 açılışında mevcut çalışma dosyalarını şu klasöre kopyalar:

```text
MotorVerileri/Arsiv/Reset-oncesi-YYYYMMDD-HHMMSS/
```

Arşiv tamamlanmadan reset uygulanmaz.

Tek seferlik işlem şu işaret dosyalarıyla korunur:

```text
MotorVerileri/v8.1-tam-sirket-reset-v2.json
MotorVerileri/v8.1-adil-baslangic-v1.json
```

Bu dosyalar bulunduğu sürece sonraki motor açılışlarında şirket ilerlemesi yeniden sıfırlanmaz.

## Korunan veriler

- şirket sunucularındaki hizmet kodları ve manifestleri,
- hizmet kimliği ve sürümü,
- 8090 üzerinden yönetilen hizmet fiyatı ve aktiflik tercihi,
- uygulama kimliği, adı, kategorisi ve fiyatlandırması,
- uygulamanın işletim sistemi, platform ve protokol bağlantıları,
- özel protokol tanımları,
- uygulama–ürün eşlemeleri,
- 8090 hesapları ve değiştirilmiş parolalar,
- 20.000 müşterinin kimliği, türü, meslek profili, gelir segmenti ve mevcut bakiyesi.

## Sıfırlanan veriler

- şirket kasaları ve bütün tarihsel gelir/gider sayaçları,
- toplam ceza, iş, saldırı ve zaman aşımı geçmişi,
- bütün yatırım seviyeleri ve devam eden yatırım süreçleri,
- hizmetlere satın alınmış ek kapasite,
- uygulamalara satın alınmış kullanıcı kapasitesi,
- krediler, kurtarma kredileri, temerrütler ve kredi notu geçmişi,
- SLA sözleşmeleri, teklif takvimi ve şirket olayları,
- uygulama aktif kullanıcıları,
- ürün gelir/gider, edinilen/kaybedilen kullanıcı geçmişi,
- müşteri işletim sistemi ve uygulama tercih geçmişi,
- fiyat şoku, ceza, finans, haber ve grafik geçmişi,
- eski tick saati.

## Adil başlangıç

Temiz sezonun ilk açılışında dört şirket de aynı noktadan başlar:

```text
Başlangıç kasası: 50.000 TL
İtibar: 50
Güvenilirlik: 50
Kod kalitesi: 50
Performans: 50
Güvenlik: 50
Müşteri memnuniyeti: 50
Kredi notu: 650
Yatırım seviyesi: 0
Aktif kredi: 0
```

Bu dengeleme yalnız bir kez uygulanır.

## Bağlantı bazlı şirket davranışı

Motor herhangi bir şirket sayısını beklemez.

- Bir şirket bağlıysa oyuna katılır.
- Bir şirket sonradan açılırsa sağlık kontrolünü geçtiği tickte oyuna katılır.
- Bir şirket kapalıysa global tick devam eder.
- Kapalı şirket iş alamaz.
- Kapalı şirketin uygulamaları o tickte piyasada aktif rakip sayılmaz.
- Kapalı şirkete gelir, gider, kredi faizi, yatırım ilerlemesi, ceza, olay veya puan değişimi yazılmaz.
- Kapalı şirketin finans ve grafik geçmişine sahte tick satırı eklenmez.
- Şirket yeniden bağlandığında kaldığı finansal durumla devam eder.

Hizmet ve uygulama tanımları kapalıyken silinmez; yalnız piyasa katılımı donar.

## Kalıcı tick saati

Tick numarasının tek otoritesi:

```text
MotorVerileri/tick-saat.json
```

Kurallar:

1. Motor açılırken son başarıyla tamamlanan tick okunur.
2. Yeni tick `sonTamamlananTick + 1` olarak başlar.
3. Tick içindeki bütün katmanlar başarıyla tamamlanmadan saat dosyası yazılmaz.
4. Tickte beklenmeyen hata oluşursa tick numarası ilerletilmez.
5. Motor kısmi ve belirsiz durumla sessizce devam etmek yerine güvenli biçimde durur.
6. Yeniden açıldığında son tamamlanan tickten devam eder.

Bu yapı finans dosyasını veya panel verisini tick saati olarak kullanmaz.

## Tick sırası

```text
1. Son tamamlanan tick + 1 hesaplanır
2. Şirketler için reconnect ve sağlık kontrolü yapılır
3. Kapalı kalan şirketlerin bilanço/işletim/finans/V6 grafik anlığı alınır
4. Kapalı şirket uygulamaları o tick için piyasadan çıkarılır
5. Müşteri, OS, ekosistem ve kapasite hesapları çalışır
6. İşletim, fiyat pazarı ve müşteri talepleri çalışır
7. Yalnız bağlı şirketler iş alır
8. V6 grafik ve ekonomi kapanışı yapılır
9. Kapalı şirketlerin anlığı geri yüklenir
10. Finans kapanışı yapılır
11. Kapalı şirketlere oluşabilecek otomatik kredi/haber yan etkileri tekrar temizlenir
12. Bilançolar ve müşteri kayıtları yazılır
13. Tick başarıyla tamamlandıysa tick-saat.json atomik güncellenir
```

## İlk çalıştırma kontrolü

İlk V8.1 açılışında aşağıdaki satırlar beklenir:

```text
V8.1 TAM ŞİRKET RESETİ tamamlandı
V8.1 adil başlangıç uygulandı | Her şirket: 50.000,00 TL | Taban puan: 50
Kalıcı tick saati hazır | Son tamamlanan tick: 0
Tick sistemi V8.1 başlatıldı | ... | Bağlı şirketler oynar, kapalı şirketler donar
```

İkinci açılışta reset yerine şu tür satırlar görünmelidir:

```text
V8.1 tam şirket reseti daha önce uygulanmış; tekrar çalıştırılmadı.
V8.1 adil başlangıç daha önce uygulanmış; şirket ilerlemesi korunuyor.
Kalıcı tick saati hazır | Son tamamlanan tick: <önceki tick>
```

## Derleme ve test

```powershell
git pull --ff-only origin agent/tunix-matematik-topla

dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj

Push-Location .\Sirketler\Tunahan-Rustix\Tunix
cargo test
Pop-Location
```

V8.1 tick ve reset kodu yerel derleme geçmeden doğrulanmış sayılmaz.

## Geri dönüş

Reset öncesi kayıtlar otomatik arşivdedir. Geri dönüş gerekirse:

1. Motor kapatılır.
2. Güncel `MotorVerileri` dosyaları ayrıca yedeklenir.
3. İstenen arşiv klasöründeki dosyalar kontrollü biçimde geri kopyalanır.
4. V8.1 işaret dosyaları bilinçli olarak ele alınır.

Bütün arşivi körlemesine geri kopyalamak yeni tick mimarisiyle eski ekonomi kayıtlarını tekrar karıştırabilir.
