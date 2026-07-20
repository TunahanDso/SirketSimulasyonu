# V8 Ceza, Uyum, Katalog ve Panel Geçiş Rehberi

Bu belge Tunix, Mudaf, Ugax ve İlos Tech şirket sunucularının motor V8 kurallarına uyum sağlaması için bağlayıcı teknik rehberdir.

## 1. V8'in amacı

V6 ve V7 geçişlerinde motor 500 standart hizmete ve 200 uygulama kategorisine genişledi. Şirket sunucularının bir kısmı eski on hizmetlik sözleşmeyi, eski özel hizmet kimliklerini veya motorun artık üretmediği alanları beklemeye devam etti. Bu durum yüzlerce işte tekrarlı başarısızlık ve orantısız ceza oluşturdu.

V8 şu ayrımı kesinleştirir:

- Motorun eksik veya hatalı ürettiği istek şirket kusuru değildir.
- Sonuç doğrulayıcısı bulunmayan standart hizmet şirket kusuru değildir.
- Şirketin açıkça ilan etmediği bir hizmet için iş gönderilmesi şirket kusuru değildir.
- Şirketin ilan ettiği fakat gerçek yönlendiricisinde bulunmayan hizmet şirket kusurudur.
- Sahte kimlik, yanlış iş kimliği, negatif süre veya güvenlik ihlali ağır şirket kusurudur.

## 2. Adil Ceza V8

### 2.1 Normal hata akışı

Gerçek şirket kusurlarında:

1. Aynı tick içinde aynı şirket, hizmet ve hata kategorisinin ilk iki tekrarı uyarıdır.
2. Üçüncü ve sonraki tekrarlar küçük finansal ceza üretebilir.
3. Normal ceza şirket başına bir tickte toplam 75 TL'yi geçemez.
4. Ceza ile birlikte neden, hizmet, iş kimliği, uygulanan tutar ve affedilen tutar kaydedilir.

### 2.2 Motor veya sözleşme kusurları

Aşağıdaki örnekler şirket cezası oluşturmaz:

- Motorun zorunlu parametreyi göndermemesi.
- `GECERSIZ_ISTEK`, `EKSIK_ALAN`, `PARAMETRE_EKSIK` gibi açık sözleşme uyumsuzlukları.
- Standart V6/V8 hizmetinin özel sonuç doğrulayıcısının henüz yazılmamış olması.
- Motor katalog sürümü ile istek üreticisi arasında geçici geçiş uyumsuzluğu.

Bu olaylar ceza defterinde görünür fakat `motor-sozlesme-uyumsuzlugu` olarak işaretlenir ve finansal/puan cezası sıfırdır.

### 2.3 Ağır kusurlar

Şunlar geçiş affına girmez:

- Başka şirket kimliğiyle cevap verme.
- Yanlış iş kimliği veya sonuç bütünlüğünü bozma.
- Saldırıyı normal iş gibi çalıştırma.
- Gerçek güvenlik kaybı.
- Bilerek bozuk veya aşırı büyük JSON gönderme.

### 2.4 Eski ceza uzlaşması

Motor ilk V8 açılışında eski tarihsel toplam ceza bakiyesini bir kez yeniden değerlendirir. Aşırı birikmiş bakiye, şirketin tarihsel gelirine göre makul tavana çekilir. Geçmişte kasadan ne kadar gerçekten kesildiği kesin olarak ayrıştırılamadığı için nakit iadesi yapılmaz. Uzlaşma yalnız tarihsel ceza göstergesini düzeltir ve ikinci açılışta tekrar uygulanmaz.

Kalıcı dosya:

```text
MotorVerileri/ceza-v8.json
```

## 3. Şirket sunucularının zorunlu davranışları

### 3.1 Yalnız gerçekten çalışan hizmeti ilan edin

Manifestte bir hizmet varsa sunucu yönlendiricisi aynı kimlik ve sürümle bu hizmeti çalıştırabilmelidir.

Yanlış örnek:

```text
Manifest: metin.buyuk-harf@1.0
Sunucu cevabı: BILINMEYEN_HIZMET
```

Bu durum `ilan-kod-uyumsuzlugu` sayılır.

### 3.2 Standart kimlik kullanın

Motorun tanıdığı hizmet kimlikleri 8090 üzerindeki **500 Hizmet Kataloğu** panelinden görülür. Özel şirket içi fonksiyon adı doğrudan standart hizmet yerine kullanılamaz. Özel kod, standart hizmet kimliğinin arka uç implementasyonu olabilir.

### 3.3 Başarılı yanıt kuralları

Başarılı sonuçta:

- `istekKimligi` boş olmamalı.
- `isKimligi` motorun gönderdiği değerle aynı olmalı.
- `sirketKimligi` gerçek şirket kimliği olmalı.
- `islemSuresiMs` negatif olmamalı.
- `sonucVerisi` geçerli ve sınırlı JSON olmalı.

### 3.4 Eksik istek alanı

Motor gerekli alanı göndermediyse sunucu çökmemelidir. Açık hata kodu dönmelidir:

```json
{
  "basarili": false,
  "hataKodu": "EKSIK_ALAN",
  "hataMesaji": "gonderen alanı zorunludur"
}
```

V8 bu cevabı motor/sözleşme uyumsuzluğu olarak kaydeder. Fakat şirket, kendi uygulamasından gelen geçerli kullanıcı isteklerinde aynı alanları doğru işlemeye devam etmelidir.

## 4. 500 standart hizmet kataloğu

8090'daki **500 Hizmet Kataloğu** paneli her şirket için şunları gösterir:

- Hizmet kimliği ve sürümü.
- Hizmet ailesi.
- Açıklama ve zaman aşımı.
- Şirketin hizmete sahip olup olmadığı.
- Aktif/pasif durumu.
- Şirket fiyatı ve kapasitesi.
- Kaç uygulamanın bu hizmeti kullandığı.

Filtreler:

- Tüm hizmetler.
- Şirketin sahip olduğu hizmetler.
- Eksik hizmetler.
- Hizmet ailesi.
- Kimlik veya açıklama araması.

Bu panel kod üretmez. Şirket sunucusunda implementasyon tamamlandıktan sonra manifestte ilan edilir.

## 5. 200 uygulama kategorisi

8090'daki **200 Uygulama Kategorisi** paneli şunları gösterir:

- Kategori kimliği ve adı.
- Sektör ve ürün türü.
- Zorunlu hizmetler.
- Şirkette eksik olan zorunlu hizmetler.
- Asgari, önerilen ve azami hizmet sayısı.
- İzin verilen ek hizmet aileleri.
- Taban ve kullanıcı başına kapasite tüketimi.
- Şirketin o kategorideki uygulamaları.
- Şirketin teknik olarak hazır olup olmadığı.

Uygulama yalnız kategori zorunluluklarını karşılıyorsa teknik olarak geçerlidir. Fazladan hizmet yalnız kategoriye uygun aileden gelirse kalite katkısı sağlar. Gereksiz veya ilgisiz hizmet kalite artışı sağlamaz.

## 6. İşletim sistemi ve protokol zorunluluğu

Her normal uygulama:

- En az bir işletim sistemi üzerinde çalışmalıdır.
- Uygulama ile işletim sistemi arasındaki bağlantı için bir protokol seçmelidir.
- İşletim sistemi ve protokol piyasada aktif olmalıdır.
- Zorunlu uygulama hizmetleri aktif olmalıdır.

Bu bilgiler eksikse motor ürünü silmez; kullanıcı ve gelir geçmişini koruyarak pasife alır. Şirket 8090'daki **Ürünler & Dağıtım** panelinden işletim sistemi ve protokolü seçip tekrar aktif edebilir.

## 7. Şirket bazlı geçiş kontrol listeleri

### 7.1 Tunix

- Tunix paket ve sunucu sürümü `0.8.0` olarak eşitlenmiştir.
- Tunix OS, Tingram, Tmail ve Tlink standart kimliklerle çalışmalıdır.
- 42 Rust testi geçmektedir.
- Eski `tunix.*` özel kimlik sabitleri geriye dönük kod içinde kalabilir; standart manifestte kullanılmamalıdır.
- Yeni 24 metin, dizi, veri, arama ve bildirim hizmetinin yönlendiricide gerçekten çalıştığı doğrulanmalıdır.

### 7.2 Mudaf

- Manifestte ilan edilen her Go hizmeti gerçek yönlendiricide bulunmalıdır.
- Uygulamalar için motorun 200 kategorisinden doğru kimlik seçilmelidir.
- Uygulama manifestinde zorunlu hizmetler, işletim sistemi desteği ve protokol açıkça ilan edilmelidir.
- Eski özel kategori adları doğrudan kullanılıyorsa 8090 kategori kataloğundan standart karşılığı seçilmelidir.
- Çok yüksek uygulama fiyatı kullanıcı kazanımı sağlamaz; kategori fiyat pazarı kullanıcıları rakiplere göç ettirir.

### 7.3 Ugax

- Daha önce manifestte ilan edilip `BILINMEYEN_HIZMET` dönen hizmetler acilen ya implement edilmeli ya da manifestten çıkarılmalıdır.
- Node.js yönlendirici hizmet kimliği ve sürümü birebir eşleştirmelidir.
- 500.000 TL finansman desteği teknik uyumsuzluğu çözmez; ilan-kod eşleşmesi zorunludur.
- Hata cevapları geçerli tek satır JSON olmalıdır.

### 7.4 İlos Tech

- Python sunucusunun on çekirdek hizmeti korunmalıdır.
- İSosyal ve İMail için standart kategori zorunlu hizmetleri 8090'dan kontrol edilmelidir.
- İLink, uygulama–işletim sistemi bağlantısında seçilebilir aktif protokol olmalıdır.
- Yeni hizmet eklenirken `test_server.py` içine en az bir sözleşme testi eklenmelidir.

## 8. 8080 V8 davranışı

8080 salt okunur piyasa ekranıdır.

- Yalnız açık sekme render edilir.
- Gizli paneller tick güncellemesinde yeniden oluşturulmaz.
- Ana Sayfa bütün panelleri sırayla tek görünümde oluşturur.
- Scroll sırasında veri bellekte bekletilir ve konum korunur.
- **Cezalar & Uyum** paneli şirketlerin uygulanan ve affedilen cezalarını gösterir.
- Haber şeridi içerik uzunluğuna göre 150–420 saniye arasında akar.
- Haber şeridi üzerine gelince veya duraklatma düğmesine basınca durur.

## 9. 8090 V8 davranışı

8090 ticari ve operasyonel yönetim ekranıdır.

- Giriş yapılmadan `/api/durum` çağrılmaz.
- Oturum yokken sayfa kendini yenilemez.
- Oturum süresi dolarsa login ekranına döner; tarayıcı reload edilmez.
- Yalnız açık panel render edilir.
- Form taslakları korunur.
- 500 hizmet ve 200 kategori katalogları yalnız bilgilendirme ve planlama içindir.
- Fiyat, aktiflik, kapasite, dağıtım, kredi, yatırım, protokol ve SLA işlemleri kalıcı motor kayıtlarını günceller.

## 10. Kabul testi

Her şirket şu sırayla test edilmelidir:

1. Sunucu birim testlerini çalıştır.
2. Motoru başlat.
3. Şirketin motora kaydolduğunu doğrula.
4. 8090'a giriş yap.
5. 500 hizmet kataloğunda sahip olunan hizmetleri kontrol et.
6. 200 kategori panelinde hedef kategori eksiklerini kontrol et.
7. Manifestte ilan edilen her hizmet için en az bir gerçek motor işi bekle.
8. Ceza panelinde motor/sözleşme hatalarının sıfır finansal ceza aldığını doğrula.
9. Gerçek ilan-kod uyumsuzluğunda ilk iki tekrarın uyarı olduğunu doğrula.
10. Normal tick cezasının şirket başına 75 TL'yi aşmadığını doğrula.

## 11. Sürüm notu

V8 geçişi büyük bir protokol ve ekonomi değişikliğidir. Mudaf, Ugax ve İlos Tech geliştiricileri bu belgeyi okumadan yeni manifest yayınlamamalıdır. Şirket sunucusu motorun değişikliklerinden habersiz kaldığında motor artık körlemesine ağır ceza yazmaz; fakat uyumsuzluk ceza panelinde görünmeye devam eder ve şirketin hizmeti iş kazanamaz.