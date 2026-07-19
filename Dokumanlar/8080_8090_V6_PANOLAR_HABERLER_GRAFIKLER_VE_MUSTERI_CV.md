# 8080–8090 V6 Panoları, Haberler, Grafikler ve Müşteri CV Rehberi

Bu belge V6 ile 8080 Yazılım Borsası ve 8090 Şirket İşletim Merkezi'nde gösterilecek yeni verileri ve ekranların görev sınırlarını açıklar.

---

## 1. Ekranların kesin görev ayrımı

### 8080 — Yazılım Borsası

Salt-okunur genel piyasa ve TV ekranıdır.

Gösterir:

- bütün şirketler,
- şirket değerleri,
- son tick gelirleri,
- uygulamalar,
- işletim sistemleri,
- protokoller,
- hizmet pazarı,
- kategori pazar payları,
- gerçek kapasite,
- yatırımlar,
- haberler,
- müşteri istatistikleri.

8080'den şirket verisi değiştirilemez.

### 8090 — Şirket İşletim Merkezi

Oturum açan şirketin kendi yönetim ekranıdır.

Yönetir:

- hizmet fiyatı,
- hizmet aktifliği,
- hizmet kapasite tahsisi,
- uygulama fiyatı,
- uygulama aktifliği,
- uygulama kapasite tahsisi,
- işletim sistemi ve protokol dağıtım ayarı,
- yatırım,
- kredi,
- SLA,
- ticari protokol işlemleri.

8090 kod üretmez ve uygulama özelliği eklemez.

---

## 2. 8080 Ana Sayfa davranışı

`Ana Sayfa` bütün ana panoları alt alta gösterir. TV'de tek sayfadan izleme için tasarlanır.

Önerilen sıralama:

1. motor ve tick özeti,
2. şirket değer tablosu,
3. şirketlerin son tick finans sonucu,
4. gerçek kapasite ve doluluk,
5. işletim sistemi pazarı,
6. uygulama pazarı,
7. kategori pazar payı grafikleri,
8. hizmet pazarı,
9. protokol pazarı,
10. yatırım ve kredi piyasası,
11. haber bülteni,
12. müşteri ve işletim sistemi özeti.

Diğer menü düğmeleri yalnız kendi panel grubunu gösterir.

---

## 3. Net gelir alanının yeni anlamı

Eski `netGelir` şirketin tarih boyunca biriken toplam sonucunu gösterebiliyordu. Bu değer şirket kartında anlık performans gibi okununca yanıltıcı oluyordu.

V6 şirket kartında gösterilecek ana finans alanı:

```text
Bir Önceki Tick Net Geliri
```

Hesap:

```text
Son tick sonucu = Güncel tarihsel net gelir - Önceki tickte kaydedilen tarihsel net gelir
```

Ayrı alanlar:

- son tick net geliri,
- tarihsel toplam gelir,
- tarihsel net gelir,
- toplam gider,
- kasa,
- şirket değeri,
- toplam borç.

Böylece şirketin o anda iyi mi kötü mü gittiği tek bakışta anlaşılır.

---

## 4. Şirket kartı V6 metrikleri

Her şirket için önerilen alanlar:

### Finans

- kasa,
- bir önceki tick net geliri,
- toplam gelir,
- toplam gider,
- borç,
- şirket değeri,
- tahmini hisse fiyatı.

### Teknik

- kod kalitesi,
- performans,
- güvenlik,
- güvenilirlik,
- müşteri memnuniyeti,
- teknik borç,
- operasyon riski.

### Kapasite

- gerçek kapasite,
- tahsis edilen kapasite,
- kullanılan kapasite,
- boş kapasite,
- doluluk,
- aktif iş,
- kuyruk,
- aktif uygulama kullanıcısı.

### Ekosistem

- hizmet sayısı,
- uygulama sayısı,
- işletim sistemi sayısı,
- protokol sayısı,
- abone sayısı,
- tamamlanan iş,
- başarısız iş,
- engellenen saldırı,
- başarılı saldırı.

---

## 5. Finans grafikleri

8080 şirket geçmişinde son tickler için çizgi grafikler yayınlanır.

Önerilen seriler:

- tick net geliri,
- kasa,
- şirket değeri,
- aktif kullanıcı,
- gerçek kapasite,
- kullanılan kapasite.

`EkonomiV6Yoneticisi` şirket başına son 180 ticki saklar. Böylece TV ekranında kısa ve orta dönem eğilim izlenebilir.

Grafikte birikmiş net gelirin doğrudan çizilmesi yerine tick geliri kullanılır; aksi hâlde grafik sürekli yukarı veya aşağı giden anlamsız bir toplam gösterir.

---

## 6. Kategori bazlı pasta grafikler

Her uygulama kategorisi için ayrı pazar payı grafiği oluşturulur.

Örnek:

```text
Kategori: sosyal-medya
Tingram: %52
İSosyal: %31
Mudaf Social: %17
```

Pay hesabı:

```text
Uygulama payı = Uygulamanın aktif kullanıcı sayısı / Kategorideki toplam aktif kullanıcı
```

Grafikte yalnız aktif ve kategori doğrulaması geçerli uygulamalar yer alır.

Her dilim için:

- uygulama adı,
- şirket adı,
- aktif kullanıcı,
- yüzde pay,
- fiyat,
- hizmet kalite puanı gösterilebilir.

Kategori toplam kullanıcısı sıfırsa grafik yerine `Henüz aktif kullanıcı yok` mesajı gösterilir.

---

## 7. İşletim sistemi pazar grafiği

20.000 müşterinin işletim sistemi dağılımı ayrıca gösterilir.

Her işletim sistemi için:

- sistem adı,
- şirket,
- aktif müşteri,
- pazar payı,
- kapasite,
- doluluk,
- fiyat,
- kalite,
- kesinti durumu.

İşletim sistemi aşırı yük veya kesinti yaşarsa kullanıcı göçü aynı grafikte sonraki ticklerde görünür.

---

## 8. Kapasite grafikleri

Şirket bazında:

- gerçek kapasite,
- tahsis edilen kapasite,
- kullanılan kapasite.

Aynı grafikte üç seri gösterilebilir.

Yorum:

- kullanılan kapasite gerçek kapasiteye yaklaşıyorsa risk büyür,
- tahsis gerçek kapasiteden büyükse şirket fazla söz vermiştir,
- gerçek kapasite büyürken kullanım büyümüyor ise yatırım atıl kalabilir,
- kullanım kapasiteyi aşıyorsa kesinti ve kullanıcı kaybı beklenir.

---

## 9. Haber bülteni paneli

Motor içindeki önemli olaylar 8080'de süslenmiş haber kartları olarak yayınlanır.

Haber kaynakları:

- büyük gelir artışı,
- büyük finansal kayıp,
- kapasite aşımı,
- uygulama kesintisi,
- işletim sistemi çökmesi,
- hizmet duraksatması,
- siber saldırı,
- yatırım başlangıcı,
- yatırım tamamlanması,
- teknik ön koşul nedeniyle bloke yatırım,
- kredi temerrüdü,
- SLA kazanımı veya ihlali,
- büyük kullanıcı göçü,
- fiyat şoku,
- yeni uygulama yayını,
- yeni protokol yayını,
- şirket değerinde sert değişim.

Haber alanları:

```json
{
  "tickNumarasi": 42,
  "baslik": "Tunix kapasite sınırına dayandı",
  "ozet": "Kullanım 1240, gerçek havuz 1000. Tingram duraksama riski altında.",
  "tur": "kapasite",
  "onem": "kritik",
  "sirketAdi": "Tunix",
  "finansalEtki": -12500
}
```

Önem seviyeleri:

- `iyi`,
- `normal`,
- `uyari`,
- `kritik`.

Haber bülteni son 250 önemli olayı saklar.

---

## 10. Müşteri CV'si

Her müşteri kalıcı bir dijital profil/CV taşır.

Ana alanlar:

- müşteri kimliği,
- müşteri adı,
- müşteri türü,
- meslek profili,
- gelir segmenti,
- bakiye,
- tick harcama bütçesi,
- kullandığı işletim sistemi,
- işletim sistemi memnuniyeti,
- işletim sistemi değişim sayısı,
- kullandığı uygulamalar,
- uygulama kullanım sayıları,
- uygulama memnuniyetleri,
- toplam uygulama değişim sayısı,
- tercih edilen şirket,
- hizmet kullanım geçmişi,
- toplam harcama,
- başarılı ve başarısız işlem sayıları,
- son işlem zamanı.

Müşteri CV'si `musteriler.json` içinde kalıcıdır.

---

## 11. Müşteri uygulama seçimi

Müşteri uygulama seçerken şu unsurlar etkili olur:

- uygulamanın işletim sistemiyle uyumu,
- kategori,
- fiyat,
- müşteri bütçesi,
- hizmet kalite puanı,
- aktif kullanıcı/popülerlik,
- kapasite,
- kesinti geçmişi,
- memnuniyet,
- şirket itibarı.

Müşteri uygulama değiştirebilir. Bu değişim CV'de sayaç ve son değişim ticki olarak tutulur.

---

## 12. 8080 müşteri görünümü

20.000 müşterinin tamamını aynı anda TV'de göstermek doğru değildir.

8080'de:

- toplam müşteri,
- aktif müşteri,
- işletim sistemi kullanan,
- işletim sistemi bekleyen,
- toplam müşteri bakiyesi,
- toplam harcama,
- gelir segmentleri,
- en çok kullanılan uygulamalar,
- örnek müşteri CV kartları gösterilir.

API, örnek veya sayfalı müşteri CV listesi yayımlayabilir.

Örnek uç:

```text
GET /api/musteri-cv
```

---

## 13. 8090 kapasite ekranı

8090'da şirket yalnız kendi kapasitesini görür.

Gösterilecek alanlar:

- gerçek kapasite,
- tahsis talebi,
- etkin tahsis,
- kullanılan kapasite,
- boş kapasite,
- doluluk,
- hizmet başına tahsis,
- uygulama başına tahsis,
- işletim sistemi yükü,
- devam eden yatırım,
- bloke yatırım,
- yatırım bitiş ticki,
- teknik ön koşul.

Kapasite artırma formu otomatik yenileme sırasında sıfırlanmamalıdır. Kullanıcı yazı yazarken form alanları korunur; veri tabloları arka planda güncellenir.

---

## 14. 8090 uygulama ekranı

Her uygulama satırında:

- uygulama adı,
- kategori,
- ürün türü,
- aktif/pasif,
- fiyat,
- aktif kullanıcı,
- istenen kapasite,
- etkin kapasite,
- kategori doğrulaması,
- eksik zorunlu hizmet,
- işletim sistemi,
- bağlantı protokolü,
- aşırı yük,
- duraksatma nedeni,
- hizmet kalite puanı.

Şirket buradan fiyatı ve ticari durumu değiştirebilir; kategori hizmetlerini buradan ekleyemez. Hizmet değişikliği şirket kodunda yapılır.

---

## 15. Canlı yenileme kuralları

### 8080

TV odaklı olduğu için yaklaşık 450–600 ms aralıkla veri yenileyebilir.

### 8090

Form güvenliği daha önemlidir.

- aktif input değeri korunur,
- odak korunur,
- seçim kutusu işlem sırasında değişmez,
- kullanıcı yazarken panel yeniden oluşturulmaz,
- düğme sonucu ekranda açıkça gösterilir,
- canlı tablolar ayrı DOM alanında güncellenir.

---

## 16. API'de V6 alanları

8080 `/api/durum` cevabına eklenmesi gereken ana alanlar:

```text
v6
v6.sirketler
v6.uygulamalar
v6.kategoriPazarPaylari
v6.haberler
v6.musteriCvOrnekleri
```

Şirket kartı için son tick geliri V6 tick geçmişinin son kaydından okunur.

---

## 17. TV'de okunabilirlik

- grafik başlıkları büyük olmalı,
- son tick geliri pozitif/negatif açık işaret taşımalı,
- pasta grafiklerde yüzde ve kullanıcı sayısı birlikte görünmeli,
- kritik haberler üstte olmalı,
- kayan içerik yerine dikey bölümler tercih edilmeli,
- Ana Sayfa tüm panoları alt alta göstermeli,
- menü panelleri yalnız seçilen bölümü göstermeli.
