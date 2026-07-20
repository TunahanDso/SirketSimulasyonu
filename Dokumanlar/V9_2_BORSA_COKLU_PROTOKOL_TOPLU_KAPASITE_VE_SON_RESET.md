# V9.2 Borsa, Çoklu Protokol, Toplu Kapasite ve Son Reset

## 1. Sürüm yaklaşımı

V9.2, V9.1 temiz motorunun üstüne yapılan kontrollü bir güncellemedir. Motor mimarisi yeniden kurulmamıştır.

Bu sürüm açılırken kullanıcı talebiyle **son kez** bütün çalışma verileri sıfırlanır. Bundan sonraki sürümlerde:

- mevcut şirket kasaları,
- yatırımlar,
- krediler,
- ürünler,
- müşteri tercihleri,
- piyasa ve tick geçmişi

korunarak migrasyon ve sürüm güncellemesi yapılmalıdır. Yeni bir tam sıfırlama varsayılan çözüm değildir.

Son reset işareti:

```text
MotorVerileri/v9.2-son-temiz-sezon-3.json
```

Reset öncesi arşiv:

```text
MotorVerileri/Arsiv/V9.2-son-reset-oncesi-YYYYMMDD-HHMMSS/
```

Şirket sunucu kodları, manifestler, `motor-ayarlari.json` ve `hizmet-katalogu.json` korunur.

## 2. Tek fiziksel kapasite havuzu

Her şirket başlangıçta 2.500 fiziksel kapasiteye sahiptir. Bu sayı ayrı ayrı hizmet, uygulama veya işletim sistemi kapasitesi değildir.

```text
Toplam fiziksel kapasite
= hizmetler için ayrılan ortak havuz
+ uygulamalara ayrılan paylar
+ işletim sistemlerine ayrılan paylar
+ boş kapasite
```

Yeni uygulama veya işletim sistemi yayınlamak yeni kapasite oluşturmaz. Ürün yayımlandıktan sonra fiziksel havuzdan pay ayrılmadıysa kullanıcı kapasitesi sıfır kalabilir.

Toplam kapasite yalnız altyapı yatırımlarıyla büyür:

- CPU,
- RAM,
- ağ,
- depolama,
- yedek sistem,
- güvenlik,
- Ar-Ge ve ilgili altyapı yatırımları.

## 3. 8090 kapasite paneli

Hizmetler artık tek tek kapasite sliderına sahip değildir.

Oyuncu yalnız şunları ayarlar:

1. bütün aktif hizmetler için ortak hizmet havuzu,
2. her uygulamanın kapasite payı,
3. her işletim sisteminin kapasite payı.

Motor ortak hizmet havuzunu, o tickteki hizmet talebine göre aktif hizmetlere otomatik dağıtır. Talebi yüksek hizmet daha yüksek iç pay alır.

Backend, toplam tahsisin fiziksel kapasiteyi aşmasına izin vermez.

## 4. Ürünleri yeniden düzenleme

8090 Uygulama & OS panelinde yayımlanmış her ürünün altında **Baştan düzenle** düğmesi bulunur.

Bu düzenleyiciyle:

- fiyat,
- aktif/pasif yayın durumu,
- uygulamanın çalışacağı işletim sistemi,
- bağlantı protokolleri

yeniden seçilebilir.

Ürünün kodu, kategorisi ve sunduğu hizmetler şirket sunucusundaki manifestten gelir. 8090 kod üretmez veya teknik özellik uydurmaz.

## 5. Çoklu protokol

İşletim sistemleri birden fazla aktif protokolü aynı anda destekleyebilir.

Örnek:

```text
Tunix OS
- Tlink
- İLink
- başka bir açık/benimsenmiş protokol
```

Bir uygulama da bir veya daha fazla protokol seçebilir. Yayın için:

- seçilen protokollerin piyasada aktif olması,
- şirket tarafından kullanılabilir olması,
- seçilen işletim sisteminin bütün seçilen protokolleri desteklemesi

gerekir.

Eski tek `baglantiProtokoluKimligi` kayıtları otomatik olarak yeni protokol listesine dönüştürülür.

## 6. 8080 şirketler borsası

8080 Borsa sekmesi şirketleri değerlerine göre sıralar.

Gösterilen başlıca veriler:

- şirket değeri,
- tahmini hisse fiyatı,
- toplam piyasa değeri içindeki pay,
- kasa ve borç,
- tick gelirleri,
- tick giderleri,
- tick net sonucu,
- hizmet ve ürün gelirleri,
- toplam ve kullanılan kapasite,
- itibar, güvenilirlik, kalite, performans, güvenlik ve memnuniyet puanları.

Şirket değeri; kasa, düzenli ürün geliri, aktif kullanıcılar, toplam fiziksel kapasite, kalite puanları, yatırımlar ve borç etkilerinden oluşur.

## 7. Haber ve olay sistemi

V9.2 olay merkezi çok sayıda farklı olay üretebilir:

- viral kullanıcı dalgası,
- kurumsal anlaşma,
- kamu ihalesi,
- ödül,
- personel transferi veya ayrılığı,
- kısa süreli kesinti,
- altyapı arızası,
- güvenlik denetimi,
- küçük veri sızıntısı şüphesi,
- pazarlama başarısı,
- tedarikçi indirimi,
- lisans ve hukuk giderleri,
- rakip fiyat baskısı,
- nadir büyük altyapı kazası.

Normal olayların etkisi çoğunlukla yüzlerce TL seviyesindedir. Büyük etkiler nadirdir. Negatif olay şirket kasasından daha fazla para kesemez; olay sistemi borç sarmalı oluşturmaz.

8080 ve 8090 haber ekranlarında:

- manşet,
- flaş gelişme rozeti,
- olay ikonu,
- şirket adı,
- tick,
- finansal etki,
- olay kategorisi

gösterilir.

## 8. Maliyet dengesi

V9.2 maliyetleri V9.1'e göre bir miktar artırır, fakat eski milyarlık borç hatasına dönmez.

Ek işletme maliyeti:

- küçük sabit operasyon payı,
- mevcut işletme giderinin sınırlı yüzdesi,
- kullanılan kapasitenin küçük payı,
- aktif kullanıcıların küçük payı

üzerinden hesaplanır ve tick başına üst sınırı vardır.

Otomatik kurtarma kredisi yoktur. Kredi yalnız oyuncu tarafından kullanılır ve gecikmede borç kendi kendine katlanmaz.

## 9. Kardeş şirketler için kontrol listesi

Mudaf, Ugax, İlos Tech ve Tunix:

- manifestte ilan ettiği hizmeti gerçek yönlendiricide çalıştırmalıdır,
- uygulamaları 200 standart kategoriden biriyle ilan etmelidir,
- zorunlu kategori hizmetlerini sunmalıdır,
- OS ve protokol dağıtımını 8090'dan seçmelidir,
- yeni ürün için kapasiteyi toplam fiziksel havuzdan ayırmalıdır,
- protokol kimliklerinde teknik kanonik kimliği kullanmalıdır.

## 10. Test

```cmd
git pull --ff-only origin agent/tunix-matematik-topla
dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj
```

İlk çalıştırmada:

```text
V9.2 SON TAM RESET tamamlandı
V9.2 ekosistem hazır
V9.2 olay merkezi hazır
V9.2 tick sistemi başladı
Yazılım borsası V9.2 yayında
Şirket yönetim merkezi V9.2 yayında
```

satırları beklenir.
