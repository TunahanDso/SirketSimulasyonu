# Gerçek Kapasite ve Sınırsız Yatırım Sistemi

Bu belge V6 şirket altyapısı, kapasite tahsisi, kapasite kullanımı, aşırı yük ve sınırsız yatırım sistemini açıklar.

En önemli değişiklik şudur:

```text
Hizmet ve uygulama kapasitesi ayrı ayrı yoktan üretilmez.
Bütün hizmetler ve uygulamalar şirketin tek gerçek fiziksel kapasite havuzunu paylaşır.
```

---

## 1. Eski modeldeki sorun

Eski modelde şirket:

- hizmet için kapasite satın alabiliyor,
- uygulama için kullanıcı kapasitesi satın alabiliyor,
- CPU/RAM/ağ yatırımı yapabiliyor,
- fakat bu değerler ortak fiziksel sınırda tam birleşmiyordu.

Bunun sonucu olarak şirketin gerçek sunucu gücünden daha fazla kapasite kâğıt üzerinde oluşturulabiliyordu.

V6 bu açığı kapatır.

---

## 2. Tek fiziksel kapasite havuzu

Her şirket için motor aşağıdaki ana değeri hesaplar:

```text
Gerçek Kapasite Birimi
```

Bu değer yalnız para ödenerek doğrudan satın alınamaz. Şunlardan oluşur:

- şirketin gerçekten ilan ettiği hizmetlerin temel kapasitesi,
- tamamlanmış CPU yatırımı,
- tamamlanmış RAM yatırımı,
- tamamlanmış ağ yatırımı,
- depolama yatırımı,
- yedekleme altyapısı,
- güvenlik altyapısı,
- kod kalitesi,
- performans puanı,
- güvenlik puanı,
- güvenilirlik puanı,
- mevcut teknik borç ve bakım baskısı.

Örnek kavramsal hesap:

```text
Gerçek havuz =
  taban sunucu gücü
  + hizmet altyapısı
  + CPU katkısı
  + RAM katkısı
  + ağ katkısı
  + depolama katkısı
  + yedek/güvenlik katkısı
  × teknik verimlilik
```

---

## 3. Kapasite türleri

### 3.1 Gerçek kapasite

Şirketin donanım, altyapı ve teknik yeteneğiyle gerçekten taşıyabildiği toplam yük.

### 3.2 İstenen tahsis

8090'dan hizmet veya uygulamaya ayrılması istenen kapasite.

### 3.3 Etkin tahsis

Gerçek havuz yeterliyse istenen tahsisin tamamı; yetersizse oransal olarak düşürülmüş kapasite.

### 3.4 Kullanılan kapasite

Aktif işler, kuyruk, uygulama kullanıcıları, işletim sistemi kullanıcıları ve çalışan özelliklerin o tickte gerçekten tükettiği kapasite.

### 3.5 Boş kapasite

```text
Boş kapasite = Gerçek kapasite - Kullanılan kapasite
```

### 3.6 Doluluk oranı

```text
Doluluk = Kullanılan kapasite / Gerçek kapasite
```

---

## 4. 8090'daki “kapasite al” ne anlama gelir?

V6'da “kapasite al” düğmesi yeni fiziksel sunucu oluşturmaz.

### Hizmet kapasitesi al

Şirketin gerçek havuzundan belirli hizmet için daha yüksek eşzamanlı işlem tahsisi talep eder.

Örnek:

```text
Gerçek şirket havuzu: 1.000 birim
Hizmet A talebi: 300 birim
Hizmet B talebi: 400 birim
Uygulamalar: 250 birim
Toplam talep: 950 birim
```

Bu durumda bütün tahsisler uygulanabilir.

Fakat toplam talep 1.500 olursa motor tahsisleri fiziksel havuza göre sınırlar. 8090'da istenen kapasite ile etkin kapasite ayrı gösterilmelidir.

### Uygulama kapasitesi al

Uygulama için daha fazla kullanıcı barındırma hakkı/tahsisi oluşturur. Bu tahsis:

- uygulama kategorisinin taban tüketimine,
- kullanıcı başına tüketimine,
- uygulamadaki özellik sayısına,
- kullanılan işletim sistemi ve protokole,
- gerçek şirket havuzuna bağlıdır.

Kâğıt üzerinde 100.000 kullanıcı kapasitesi istenebilir; fiziksel havuz bunu taşımıyorsa etkin kapasite düşer ve aşırı yük başlar.

---

## 5. Hizmet kapasite tüketimi

Her hizmetin temel eşzamanlı işlem kapasitesi vardır.

Motor tahsis talebini kapasite birimine dönüştürür. Ağır hizmetler daha fazla kaynak tüketebilir.

Örnek etkiler:

- basit metin sayımı düşük CPU tüketir,
- video işleme yüksek CPU ve ağ tüketir,
- dosya yükleme ağ ve depolama tüketir,
- yapay zekâ hizmeti CPU/RAM tüketir,
- veritabanı yedekleme depolama ve ağ tüketir,
- işletim sistemi süreç yönetimi genel havuz üzerinde ek baskı oluşturur.

---

## 6. Uygulama kapasite tüketimi

Her uygulama kategorisi şu iki katsayıyı taşır:

- taban kapasite tüketimi,
- aktif kullanıcı başına kapasite tüketimi.

Örnek:

```text
Sosyal medya:
- akış üretir,
- gönderi saklar,
- etkileşim işler,
- bildirim üretir,
- medya ve arama kullanabilir.
```

Bu nedenle sosyal medya kullanıcı başı tüketimi basit bir hesap makinesi uygulamasından daha yüksektir.

Ek hizmetler kaliteyi artırabilir fakat kapasite tüketimini ve bakım maliyetini de artırır.

---

## 7. İşletim sistemi kapasitesi

İşletim sistemi ayrı bir uygulama gibi görünse de platform katmanı olduğu için etkisi daha geniştir.

İşletim sistemi kapasitesi:

- sistem kullanıcılarını,
- çalışan uygulama süreçlerini,
- paketleri,
- dosya sistemini,
- ağ profillerini,
- uygulama–OS protokol trafiğini taşır.

İşletim sistemi aşırı yüklenirse yalnız kendi müşterileri değil, üzerinde çalışan bütün uygulamalar etkilenebilir.

Muhtemel sonuçlar:

- uygulama başlatma gecikmesi,
- süreç duraksaması,
- paket kurulum hatası,
- dosya sistemi hatası,
- uygulama kesintisi,
- kullanıcıların başka işletim sistemine göçü.

---

## 8. Aşırı yük eşikleri

Örnek yorumlama:

| Doluluk | Durum | Beklenen etki |
|---:|---|---|
| `%0–74` | Rahat | Normal işletim |
| `%75–94` | Yüksek | Gecikme ve risk artışı |
| `%95–109` | Kritik sınıra yakın | Kuyruk ve hata oranı artar |
| `%110–149` | Aşırı yük | Kullanıcı kaybı ve otomatik duraksatma |
| `%150+` | Çöküş riski | Hizmet, uygulama veya OS katmanı durabilir |

Motor küçük bir rastgele operasyon baskısı da ekleyebilir. Böylece tam sınırda çalışan sistem her tick aynı sonucu vermez.

---

## 9. Aşırı yük sonuçları

Aşırı yük durumunda motor şunları uygulayabilir:

- aktif iş kuyruğu artışı,
- zaman aşımı,
- gecikme artışı,
- performans puanı kaybı,
- güvenilirlik puanı kaybı,
- müşteri memnuniyeti kaybı,
- kullanıcı kaybı,
- uygulama duraksatması,
- hizmet duraksatması,
- işletim sistemi kesintisi,
- SLA ihlali,
- finansal zarar,
- haber bültenine olay eklenmesi.

---

## 10. Yatırım sistemi

Yatırım seviyesi artık 20 ile sınırlı değildir.

```text
Azami seviye: teknik olarak sınırsız
```

Fakat her seviye önceki seviyeden daha zor olmalıdır.

### Maliyet artışı

Maliyet doğrusal değil, üstel/katlanarak artar.

Örnek kavramsal formül:

```text
Yeni seviye maliyeti = taban maliyet × seviye^1,35 × risk katsayısı
```

### Süre artışı

Yatırım satın alındığında etkisi aynı tickte başlamaz.

```text
Tamamlanma süresi ≈ 2 + seviye^1,18 / 2 tick
```

Seviye yükseldikçe proje daha uzun sürer.

### Teknik ön koşul

Para yeterli olsa bile gerekli hizmet şirkette yoksa yatırım uygulanmaz.

---

## 11. Yatırım ön koşulları

| Yatırım | Ön koşul hizmeti | Neden |
|---|---|---|
| CPU | `isletim.kaynak-ata` | Kaynak planlama ve süreç tahsisi gerekir |
| RAM | `isletim.surec-listele` | Bellek kullanan süreçler izlenmelidir |
| Ağ | `isletim.ag-yapilandir` | Ağ altyapısı yönetilmelidir |
| Depolama | `dosya.yukle` | Depolama katmanı gerçekten çalışmalıdır |
| Güvenlik | `guvenlik.saldiri-tespit` | Güvenlik yatırımı gerçek savunma hizmetine bağlıdır |
| Yedekleme | `veritabani.yedek-al` | Yedek sistemi kodlanmış olmalıdır |
| Destek | `bildirim.gonder` | Kullanıcı iletişimi gerekir |
| Pazarlama | `analitik.kullanici-yolu` | Ölçülemeyen pazarlama yükseltilemez |
| Satış | `ticaret.siparis-olustur` | Satış altyapısı gerekir |
| Ar-Ge | `gelistirme.test-calistir` | Test ve geliştirme sistemi gerekir |

---

## 12. Yatırım durumları

### Bekliyor

Şirket yatırım seviyesini satın almıştır fakat süreç henüz değerlendirilmemiştir.

### Bloke

Gerekli hizmet eksiktir. Para ödenmiş olsa bile yatırım kapasiteye eklenmez.

### İnşaat/uygulama

Teknik ön koşul sağlanmıştır. Yatırım belirlenen bitiş tickini bekler.

### Tamamlandı

Yatırım gerçek kapasite hesabına katılır.

---

## 13. Kalıcı kayıt

V6 yatırım ve kapasite durumu `ekonomi-v6.json` içinde korunur.

Saklanan başlıca alanlar:

- tamamlanan yatırım seviyeleri,
- devam eden yatırım süreçleri,
- bloke yatırımlar,
- istenen hizmet tahsisleri,
- istenen uygulama tahsisleri,
- gerçek kapasite,
- tahsis edilen kapasite,
- kullanılan kapasite,
- tick finans geçmişi.

Motor yeniden başladığında yatırım süreleri ve tahsis talepleri unutulmaz.

---

## 14. 8090'da gösterilmesi gereken alanlar

Şirket altyapı ekranı:

- gerçek kapasite birimi,
- tahsis edilen kapasite,
- kullanılan kapasite,
- boş kapasite,
- doluluk yüzdesi,
- aktif iş,
- kuyruk,
- toplam uygulama kullanıcısı,
- hizmet bazında istenen/etkin tahsis,
- uygulama bazında istenen/etkin kullanıcı kapasitesi,
- devam eden yatırımlar,
- bitiş ticki,
- bloke yatırım nedeni,
- gerekli teknik hizmet.

---

## 15. Stratejik sonuç

Oyuncu artık yalnız para biriktirerek kazanamaz.

Başarılı altyapı stratejisi:

1. gerekli yönetim hizmetlerini gerçekten kodlamak,
2. yatırımı satın almak,
3. yatırımın tamamlanmasını beklemek,
4. fiziksel havuzu büyütmek,
5. hizmet ve uygulamalara dengeli tahsis yapmak,
6. boş kapasite bırakmak,
7. yoğunluk ve kesinti riskini izlemek,
8. fiyat ve kullanıcı büyümesini altyapıyla aynı hızda tutmak.
