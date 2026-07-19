# Son Güncellemeler — V6 Değişiklik Günlüğü

Bu belge son birkaç günlük büyük geliştirme serisinin ayrıntılı özetidir. Belgede yalnız nihai hedefler değil, hangi eski sorunların neden değiştirildiği de anlatılır.

---

## 1. Kodlama ile yönetim birbirinden ayrıldı

### Eski sorun

8090 üzerinden düğmeye basarak uygulama veya hizmet üretilebilmesi oyunun yazılım geliştirme amacını bozuyordu.

### Yeni kural

- hizmet kodu şirket sunucusunda yazılır,
- uygulama özellikleri şirket sunucusundaki gerçek hizmetlere bağlanır,
- protokol şirket tarafından kodlanır,
- 8090 yalnız yayımlama, fiyat, aktiflik, kapasite tahsisi ve finans yönetir.

### Sonuç

Sunucuda çalışan arka uç hizmeti ve kod tabanlı manifest olmadan hiçbir uygulama, abonelik ürünü veya özel protokol piyasaya çıkamaz.

---

## 2. Motorun kalıcı ticari kaydı otorite oldu

### Eski sorun

Şirket yeniden bağlandığında sunucudan gelen fiyat, aktiflik veya kapasite değerleri 8090'dan yapılan değişiklikleri ezebiliyordu.

### Yeni davranış

- sunucu değeri yalnız ilk varsayılandır,
- motor ilk kez görülen varlığı kaydeder,
- 8090 değişikliği kalıcı yönetilen değer olur,
- yeniden bağlantı yönetilen değeri sıfırlamaz,
- yeni hizmet/uygulama/protokol sunucudan gelen varsayılanla eklenir.

---

## 3. 8090 paneli yenileme ve sekme davranışı

### Eski sorunlar

- otomatik yenileme input değerini sıfırlıyordu,
- kullanıcı miktar yazarken alan kayboluyordu,
- düğmeler bazen çalışmıyordu,
- sekmeler aynı panelde kalıyordu.

### Yeni yaklaşım

- navigasyon bir kez kurulur,
- paneller `hidden` ile açılıp kapanır,
- canlı veri yalnız tablo ve metrik alanlarını günceller,
- aktif form alanı korunur,
- yönetim sonucu açık başarı/hata mesajı üretir.

---

## 4. 8080 Yazılım Borsası genişletildi

Yeni piyasa alanları:

- şirket değeri,
- uygulama ve abonelik pazarı,
- işletim sistemi pazarı,
- protokol pazarı,
- hizmet pazarı,
- kredi ve yatırım,
- SLA,
- kapasite,
- haber bülteni,
- müşteri istatistikleri.

Ana Sayfa bütün bölümleri alt alta gösterir; diğer menüler yalnız kendi panelini gösterir.

---

## 5. Şirket değeri kasadan ayrıldı

### Eski sorun

Kasası büyük olan şirket doğrudan en değerli şirket gibi görünüyordu.

### Yeni değerleme unsurları

- gelir geçmişi,
- uygulamalar,
- hizmetler,
- kullanıcılar,
- aboneler,
- kapasite,
- kalite,
- performans,
- güvenlik,
- güvenilirlik,
- ekosistem,
- protokol benimsenmesi,
- borç,
- teknik borç,
- risk ve kesinti geçmişi.

---

## 6. 20.000 kalıcı müşteri

Müşteri veritabanı 20.000 kayda genişletildi.

- eski müşteriler silinmez,
- yeni müşteriler eksik kimlik aralığına eklenir,
- her müşteri kalıcı geçmiş taşır,
- müşteri işletim sistemi seçer,
- müşteri uygulama kullanır ve değiştirir.

---

## 7. İşletim sistemi pazarı

Yeni zorunluluklar:

- her müşteri bir işletim sistemi kullanır,
- her uygulama bir işletim sistemi üzerinde çalışır,
- uygulama–OS bağlantısı protokol gerektirir,
- işletim sistemi müşterileri kalite, fiyat, kapasite ve kesintiye göre göç edebilir.

İşletim sistemi ve protokol bilgisi eksik eski uygulamalar pasife alınır.

---

## 8. Tunix OS

Tunix şirketine Rust ile gerçek çalışan `Tunix OS` eklendi.

Başlıca özellikler:

- süreç başlatma/durdurma/listeleme,
- CPU/RAM kaynak tahsisi,
- dosya sistemi,
- paket kurma/kaldırma,
- ağ yapılandırma,
- güncelleme kontrolü/kurulumu,
- log toplama,
- uygulama çalıştırma,
- kullanıcı doğrulama,
- istek güvenlik kontrolü.

Tunix sunucu sürümü `0.8.0` olarak güncellendi.

---

## 9. Tingram ve Tmail V6 geçişi

Tingram ve Tmail artık manifestte özel isimli hizmetler yerine motorun standart hizmet kimliklerini ilan eder.

Tingram:

- kategori: `sosyal-medya`,
- işletim sistemi: `tunix-os`,
- protokol: `tunix-tlink`.

Tmail:

- kategori: `eposta`,
- işletim sistemi: `tunix-os`,
- protokol: `tunix-tlink`.

---

## 10. 500 standart hizmet

Motorun bildiği temel hizmet sayısı 10'dan 500'e çıkarılacak V6 katalog yapısına geçirildi.

Model:

```text
25 aile × 20 işlev = 500
```

Hizmetler uygulama kategorileri, yatırımlar ve iş talepleri için ortak teknik sözleşme oluşturur.

---

## 11. 200 uygulama kategorisi

Kategori alanı serbest metin olmaktan çıkarıldı.

Model:

```text
20 sektör × 10 arketip = 200 kategori
```

Her kategori:

- zorunlu hizmetleri,
- uygun ek hizmetleri,
- hizmet sayısı sınırlarını,
- kalite katkısını,
- kapasite tüketimini tanımlar.

---

## 12. Uygulama içindeki hizmet kombinasyonu

Her uygulamanın altında gerçek hizmetler bulunmak zorundadır.

- zorunlu çekirdek eksikse uygulama çalışmaz,
- uygun opsiyonel hizmetler kaliteyi artırır,
- alakasız hizmetler kalite kazandırmaz,
- fazla hizmet bakım ve kapasite giderini artırır.

---

## 13. Fiyat pazarı ve kullanıcı göçü

### Eski açık

Aşırı yüksek fiyatlı uygulama kullanıcı kaybetmiyor, hatta kullanıcı kazanabiliyordu.

### Yeni sistem

- kategori referans fiyatı,
- rakip medyanı,
- kalite/fiyat oranı,
- müşteri bütçesi,
- fiyat hassasiyeti,
- kapasite birlikte değerlendirilir.

Aşırı pahalı ürün:

- yeni kullanıcı kazanamaz,
- mevcut kullanıcı kaybeder,
- kullanıcı rakibe göç eder,
- itibar kaybeder,
- hayalî gelir chargeback ile geri alınır.

---

## 14. Tek gerçek kapasite havuzu

Hizmet ve uygulama kapasitesi artık bağımsız sanal sayı değildir.

Şirketin tek fiziksel havuzu:

- hizmetlere,
- uygulamalara,
- işletim sistemine,
- aktif işlere,
- kullanıcılara paylaşılır.

8090 kapasite işlemi fiziksel güç satın almak değil, mevcut havuzdan tahsis istemektir.

---

## 15. Aşırı yük ve kesinti

Gerçek kapasite aşılırsa:

- kuyruk,
- gecikme,
- zaman aşımı,
- performans kaybı,
- güvenilirlik kaybı,
- uygulama duraksaması,
- hizmet duraksaması,
- işletim sistemi kesintisi,
- kullanıcı kaybı,
- finansal zarar oluşabilir.

---

## 16. Sınırsız yatırımlar

Yatırım seviyesi 20 ile sınırlı değildir.

Yeni seviyeler:

- daha pahalı,
- daha uzun süreli,
- teknik hizmet ön koşullu,
- satın alındığı anda değil tamamlandığında etkili.

Para tek başına altyapı oluşturamaz.

---

## 17. Şirket olayları ve haber bülteni

Motor önemli olayları kalıcı haber kaydına dönüştürür.

Örnekler:

- kapasite aşımı,
- yatırım tamamlanması,
- bloke yatırım,
- gelir sıçraması,
- finansal daralma,
- uygulama pasifleştirme,
- kullanıcı göçü,
- fiyat şoku,
- siber saldırı,
- SLA ihlali.

Bu olaylar 8080 Haber Bülteni panelinde gösterilir.

---

## 18. Son tick geliri

8080 şirket kartındaki ana gelir artık tarihsel birikmiş net gelir değildir.

```text
Son tick net geliri = Güncel tarihsel net gelir - Önceki tick tarihsel net gelir
```

Tarihsel toplamlar ayrı alanlarda tutulur.

---

## 19. Grafikler

Yeni grafik verileri:

- tick net geliri,
- kasa,
- şirket değeri,
- aktif kullanıcı,
- gerçek kapasite,
- kullanılan kapasite,
- kategori bazlı uygulama pazar payı,
- işletim sistemi pazar payı.

---

## 20. Müşteri CV'si

Her müşteri artık şu kalıcı bilgileri taşır:

- meslek profili,
- gelir segmenti,
- işletim sistemi,
- uygulamalar,
- kullanım sayıları,
- uygulama memnuniyeti,
- uygulama değişim sayısı,
- hizmet kullanım geçmişi,
- harcama ve işlem geçmişi.

---

## 21. Banka ve finans

Şirketler:

- kredi çekebilir,
- taksit öder,
- temerrüde düşebilir,
- kredi notu kaybedebilir,
- pazar dışı gelir iadesi nedeniyle ödenemeyen gider oluşturabilir.

Ugax için tek seferlik 500.000 TL finansman desteği migrasyon katmanında kayıtlıdır.

---

## 22. Siber saldırı ve operasyon riski

Şirketler doğru kod yazsa bile tamamen risksiz değildir.

Riskler:

- saldırı dalgaları,
- kapasite baskısı,
- bakım hatası,
- teknik borç,
- bağımlı uygulama kesintisi,
- işletim sistemi kesintisi,
- protokol uyumsuzluğu,
- beklenmeyen pazar olayı.

---

## 23. Kalıcı kayıt dosyaları

Yeni/önemli çalışma dosyaları:

- `sirket-isletim.json`,
- `kod-tabanli-yayinlar.json`,
- `ekosistem.json`,
- `pazar-fiyat.json`,
- `ekonomi-v6.json`,
- `musteriler.json`.

Bu dosyalar Git'e eklenmemeli, motor çalışma geçmişi olarak saklanmalıdır.

---

## 24. Güncel doğrulama durumu

Bu değişiklik serisi çok sayıda yeni C# ve Rust dosyası içerir. Kodun son dal hâli yerel makinede aşağıdaki komutlarla doğrulanmalıdır:

```powershell
git pull --ff-only origin agent/tunix-matematik-topla

dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj

cd .\Sirketler\Tunahan-Rustix\Tunix
cargo test
```

Derleme hatası görülürse motoru üretim kayıtlarıyla çalıştırmadan önce hata düzeltilmelidir.
