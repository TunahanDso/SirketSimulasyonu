# Pano v4.1 ve 10.000 Müşterilik Yoğun Pazar

Bu paket, yazılım borsasının izleme ve yönetim ekranlarını kararlı hâle getirir; müşteri pazarını 10.000 kalıcı kayda çıkarır ve talep/pazar hareketlerini belirgin biçimde yoğunlaştırır.

## 1. 8090 Şirket Yönetim Merkezi v2

Eski panel her üç saniyede bütün sayfayı yeniden oluşturduğu için:

- sayı alanları sıfırlanıyor,
- odak ve imleç kayboluyor,
- seçim kutuları eski değerine dönüyor,
- hızlı tıklamalarda aynı işlem iki kez gönderilebiliyor,
- liste sırası değiştiğinde indeks tabanlı butonlar yanlış kayda gidebiliyordu.

Yeni panelde:

- veri 900 ms aralıkla arka planda alınır,
- kullanıcı bir `input` veya `select` alanındayken form DOM'u yeniden yazılmaz,
- yazılan değerler taslak belleğinde korunur,
- odak bırakıldığında son sunucu durumu uygulanır,
- işlemler hizmet/ürün/protokol kimliğiyle gönderilir,
- işlem devam ederken buton tekrar kullanılamaz,
- sunucunun gerçek başarı veya hata açıklaması ekranda gösterilir,
- sekme ve form durumu otomatik yenilemede kaybolmaz.

8090 hâlâ kod yazmaz. Yalnız sunucunun `sirketTanitim` manifestinde ilan ettiği hizmetleri, uygulamaları ve protokolleri yönetir.

## 2. 8080 Yazılım Borsası v4.1

Yeni ilk sekme `Ana Sayfa`dır. TV üzerinde tek görünümde şu bölümler alt alta gösterilir:

1. Şirket değer listesi
2. Uygulama ve abonelik pazarı
3. Özel protokol pazarı
4. Temel hizmet pazarı
5. Finans ve banka
6. SLA sözleşmeleri
7. Yatırım seviyeleri
8. Güvenlik sıralaması
9. Son tick özeti
10. Canlı motor akışı

Ayrıntılı sekmeler korunur. Canlı veri isteği yaklaşık 450 ms aralıkla yapılır ve önceki istek bitmeden ikinci istek başlatılmaz.

## 3. Yeni şirket değeri yaklaşımı

8080 ve 8090 ekranlarında şirket değeri artık kasanın doğrudan kopyası değildir. Kasa yalnız yüzde 15 likidite ağırlığıyla değerlendirilir.

Değeri yükselten ana bileşenler:

- tamamlanmış iş geçmişi,
- net gelir geçmişi,
- aktif ürün sayısı ve ürün kârlılığı,
- aktif kullanıcı ve abone sayısı,
- satın alınmış kullanıcı kapasitesi,
- hizmet sayısı ve hizmet kapasitesi,
- kod kalitesi,
- performans,
- güvenlik,
- itibar,
- güvenilirlik,
- müşteri memnuniyeti,
- ekosistem puanı,
- piyasadaki özel protokoller,
- yatırım seviyeleri,
- başarılı ve aktif SLA sözleşmeleri.

Değeri azaltan bileşenler:

- aktif kredi borcu,
- ödenemeyen giderler,
- temerrüt geçmişi,
- sözleşme ihlalleri.

Bu nedenle kasası düşük fakat iyi ürünleri, yüksek abonesi ve güçlü teknik geçmişi bulunan şirket değerli kalabilir. Büyük kasası olan fakat ürünü, kapasitesi ve güvenilir geçmişi bulunmayan şirket otomatik olarak piyasanın en değerlisi olmaz.

## 4. 10.000 kalıcı müşteri

Motor açıldığında müşteri veritabanı 10.000 kayda tamamlanır.

- Var olan ilk 5.000 müşteri korunur.
- Yeni kayıtlar `musteri-05001` ile `musteri-10000` arasında eklenir.
- Eski bakiye, sadakat ve işlem geçmişleri silinmez.
- Müşteri dosyasını elle silmek gerekmez.

## 5. Yoğun talep sistemi

Yeni pazar sistemi:

- temel talep ihtimalini yükseltir,
- pazar döngüsü çarpanlarını büyütür,
- kurumsal ve kamu müşterilerinin tek tickte iki veya üç ayrı hizmet isteyebilmesini sağlar,
- ileri hizmetlerde yüksek zorluk ihtimalini artırır,
- yoğunluğu motoru tamamen kilitlemeden sınırlamak için tick başına en fazla 900 iş üretir.

Üst sınır yalnız güvenlik önlemidir. Normal koşullarda talep, müşteri davranışından doğal olarak oluşur.

## 6. Rastgele pazar hareketleri

Yaklaşık her üç tickte bir yeni olay başlama ihtimali vardır. Olaylar üç ila yedi tick sürer.

Örnekler:

- Kurumsal dijitalleşme ihalesi
- Veri analizi talep patlaması
- Viral sosyal platform dalgası
- Kamu yazılım alım dönemi
- E-posta sağlayıcı göçü
- Siber tehdit dalgası
- Yoğun sezon
- Kısa piyasa durgunluğu

Bu olaylar talep çarpanını ve bazı durumlarda siber saldırı riskini değiştirir. Olay başladığında motor konsoluna `PAZAR HAREKETİ` kaydı düşer ve 8080 canlı akışında görünür.

## 7. Güncelleme ve test

Motoru ve şirket sunucularını durdurun. Repo kökünde:

```powershell
git pull --ff-only origin agent/tunix-matematik-topla

dotnet build .\SirketMotoru\SirketMotoru.csproj
```

Derleme geçerse:

```powershell
powershell -ExecutionPolicy Bypass -File .\BASLAT_TUNIX_ILOS_BORSA.ps1
```

Beklenen açılış kayıtları:

```text
Müşteri veritabanı genişletildi | Eski: 5000 | Yeni: 10000
Yoğun pazar müşteri yöneticisi hazır | Toplam müşteri: 10000
Gelişmiş yazılım borsası yayında | Port: 8080
Şirket yönetim kapısı yayında | Port: 8090
```

Tarayıcıda eski HTML önbelleği kalmışsa iki sayfada da bir kez `Ctrl+F5` kullanın.

## 8. Kontrol listesi

### 8090

- Bir fiyat alanına değer yazın ve 10 saniye bekleyin; değer kaybolmamalıdır.
- `Fiyatı kaydet` düğmesine basın; gerçek başarı mesajı görünmelidir.
- Hizmeti pasife alıp yeniden aktife alın.
- Uygulama fiyatlarını değiştirin.
- Kapasite satın alın.
- Kredi tutarı yazarken otomatik yenilemenin alanı sıfırlamadığını doğrulayın.

### 8080

- İlk sekmenin `Ana Sayfa` olduğunu doğrulayın.
- Bütün piyasa bölümlerinin tek sayfada alt alta geldiğini kontrol edin.
- Şirket değer sıralamasının yalnız kasaya göre oluşmadığını kontrol edin.
- Üst bölümde API gecikmesinin yaklaşık yarım saniyede bir değiştiğini görün.

### Motor

- Toplam müşteri sayısı 10.000 olmalıdır.
- Tick talep sayısı eski sürümden belirgin biçimde yüksek olmalıdır.
- `PAZAR HAREKETİ` kayıtları birkaç tick içinde görünmelidir.
- Tick süresi 10 saniyeyi aşarsa konsol uyarısı izlenmeli; gerekirse tick başına 900 iş üst sınırı daha sonra donanıma göre ayarlanmalıdır.
