# Motor Ortak İletişim Protokolü v0.1

## 1. Protokolün amacı

Motor Ortak İletişim Protokolü, şirket sunucularının merkezi oyun motoruyla güvenli ve anlaşılır biçimde iletişim kurmasını sağlayan herkese açık standarttır.

Bu protokol herhangi bir şirkete ait değildir.

Bütün şirketler, kullandıkları programlama dili ne olursa olsun bu protokole uymak zorundadır.

Şirketlerin kendi geliştireceği ticari protokoller bu protokolden ayrıdır.

---

## 2. Sistem rolleri

### Motor

Motor oyunun tek yetkili merkezidir.

Motor şunları yönetir:

* şirket kayıtları
* ekonomi
* müşteriler
* şirket bakiyeleri
* itibar
* hizmet kayıtları
* protokol kayıtları
* lisanslar
* görevler
* performans ölçümleri
* oyun tickleri
* resmî oyun veritabanı

### Şirket sunucusu

Her oyuncu kendi şirket sunucusunu yazar.

Şirket sunucusu şunlardan sorumludur:

* TCP bağlantısını kabul etmek
* motor mesajlarını okumak
* JSON mesajlarını ayrıştırmak
* geçerli cevaplar üretmek
* kendi hizmetlerini çalıştırmak
* kendi kapasitesini yönetmek
* motorun gönderdiği işleri işlemek
* hata durumlarını doğru şekilde bildirmek

---

## 3. Taşıma katmanı

İletişim TCP üzerinden yapılır.

Her şirket belirlenen bir portta TCP sunucusu açar.

Başlangıç portları:

* Rustix: `7001`
* Mudaf: `7002`
* Ugur Link: `7003`

İlk testlerde bütün şirketler aynı bilgisayarda çalışabilir:

```text
127.0.0.1
```

Daha sonra her şirket farklı bilgisayarda çalışırsa yerel ağ IP adresleri kullanılır.

Örnek:

```text
192.168.1.25
```

---

## 4. Mesaj biçimi

Bütün mesajlar JSON biçimindedir.

Her JSON mesajı tek satır olmalıdır.

Her mesajın sonunda yalnızca LF karakteri bulunmalıdır:

```text
\n
```

Örnek:

```json
{"mesajTuru":"merhaba","mesajKimligi":"mesaj-001","protokolSurumu":"0.1","motorKimligi":"ana-motor"}
```

Mesajın sonunda `\n` bulunmadığında motor mesajın tamamlandığını anlayamaz.

Metin kodlaması:

```text
UTF-8
```

Azami mesaj boyutu:

```text
65536 byte
```

---

## 5. Alan isimleri

Bütün alan isimleri küçük harfle başlamalı ve camelCase kullanılmalıdır.

Doğru:

```json
{
  "mesajTuru": "saglikKontrolu",
  "mesajKimligi": "mesaj-001",
  "istekKimligi": "saglik-001"
}
```

Yanlış:

```json
{
  "MesajTuru": "saglikKontrolu",
  "mesaj_turu": "saglikKontrolu",
  "mesaj-turu": "saglikKontrolu"
}
```

Türkçe karakterler alan isimlerinde kullanılmaz.

Doğru:

```text
sirketKimligi
```

Yanlış:

```text
şirketKimliği
```

---

## 6. Her mesajda bulunması gereken alanlar

Bütün mesajlarda en az şu üç alan bulunmalıdır:

```json
{
  "mesajTuru": "mesajın türü",
  "mesajKimligi": "benzersiz mesaj kimliği",
  "protokolSurumu": "0.1"
}
```

### mesajTuru

Mesajın ne amaçla gönderildiğini belirtir.

### mesajKimligi

Her mesajın benzersiz kimliğidir.

Örnek:

```text
mesaj-6f851b1de6894b43
```

### protokolSurumu

Kullanılan ortak motor protokolü sürümüdür.

İlk sürüm:

```text
0.1
```

---

# 7. Bağlantı sırası

Bir şirket motora bağlanırken aşağıdaki sıra uygulanır.

```text
Motor
  |
  | TCP bağlantısı
  v
Şirket sunucusu

Motor  -> merhaba
Şirket -> sirketTanitim
Motor  -> kayitSonucu
Motor  -> saglikKontrolu
Şirket -> saglikSonucu
```

Şirket mesajları bu sıraya uymalıdır.

---

# 8. Merhaba mesajı

Motor TCP bağlantısı kurulduktan sonra ilk olarak `merhaba` mesajı gönderir.

Motor tarafından gönderilir:

```json
{
  "mesajTuru": "merhaba",
  "mesajKimligi": "mesaj-000001",
  "protokolSurumu": "0.1",
  "motorKimligi": "ana-motor"
}
```

Şirket bu mesajı aldıktan sonra motorun protokol sürümünü kontrol etmelidir.

Şirket desteklemediği bir protokol sürümü alırsa bağlantıyı kabul etmemelidir.

---

# 9. Şirket tanıtım mesajı

Şirket, motorun `merhaba` mesajına karşılık kendi kimlik bilgilerini gönderir.

Şirket tarafından gönderilir:

```json
{
  "mesajTuru": "sirketTanitim",
  "mesajKimligi": "mesaj-000002",
  "protokolSurumu": "0.1",
  "sirketKimligi": "tunahan-rustix",
  "sirketAdi": "Rustix",
  "sunucuSurumu": "0.1"
}
```

### sirketKimligi

Motor ayarlarında bulunan şirket kimliğiyle tamamen aynı olmalıdır.

Rustix:

```text
tunahan-rustix
```

Mudaf:

```text
mustafa-mudaf
```

Ugur Link:

```text
ugur-link
```

Kimlik yanlış gönderilirse motor şirketi kabul etmez.

---

# 10. Kayıt sonucu mesajı

Motor, şirket bilgilerini kontrol ettikten sonra kayıt sonucunu gönderir.

Başarılı kayıt:

```json
{
  "mesajTuru": "kayitSonucu",
  "mesajKimligi": "mesaj-000003",
  "protokolSurumu": "0.1",
  "basarili": true,
  "sirketKimligi": "tunahan-rustix",
  "aciklama": "Şirket motor tarafından başarıyla kaydedildi."
}
```

Başarısız kayıt örneği:

```json
{
  "mesajTuru": "kayitSonucu",
  "mesajKimligi": "mesaj-000003",
  "protokolSurumu": "0.1",
  "basarili": false,
  "sirketKimligi": "bilinmeyen-sirket",
  "aciklama": "Şirket kimliği motor kayıtlarıyla uyuşmuyor."
}
```

Şirket kayıt başarısızsa hizmet sunmaya başlamamalıdır.

---

# 11. Sağlık kontrolü

Motor, şirket sunucusunun çalışıp çalışmadığını ölçmek için sağlık kontrolü gönderir.

Motor tarafından gönderilir:

```json
{
  "mesajTuru": "saglikKontrolu",
  "mesajKimligi": "mesaj-000004",
  "protokolSurumu": "0.1",
  "istekKimligi": "saglik-1-a14f",
  "tickNumarasi": 1
}
```

Şirket cevap olarak aynı `istekKimligi` değerini geri göndermelidir.

Şirket tarafından gönderilir:

```json
{
  "mesajTuru": "saglikSonucu",
  "mesajKimligi": "mesaj-000005",
  "protokolSurumu": "0.1",
  "istekKimligi": "saglik-1-a14f",
  "durum": "calisiyor",
  "aktifBaglanti": 1,
  "kuyrukUzunlugu": 0
}
```

### durum

İlk sürümde kullanılabilecek değerler:

```text
calisiyor
yogun
bakimda
hatali
```

### aktifBaglanti

Şirket sunucusundaki mevcut aktif bağlantı sayısıdır.

### kuyrukUzunlugu

İşlenmeyi bekleyen görev sayısıdır.

Motor bu değerleri ilk sürümde bilgi amaçlı alır.

Motor performans puanını yalnızca şirketin bildirdiği değerlere göre vermez. Gecikmeyi ve hata oranını kendisi ölçer.

---

# 12. İstek kimliği kuralı

Motorun cevap beklediği mesajlarda `istekKimligi` bulunur.

Şirket cevabında aynı kimliği geri göndermek zorundadır.

Motor:

```json
{
  "mesajTuru": "saglikKontrolu",
  "istekKimligi": "saglik-917"
}
```

Şirket:

```json
{
  "mesajTuru": "saglikSonucu",
  "istekKimligi": "saglik-917"
}
```

Şirket farklı bir istek kimliği gönderirse cevap geçersiz sayılır.

---

# 13. Zaman aşımı

Şirket, motor mesajlarına belirlenen süre içerisinde cevap vermelidir.

İlk sürüm zaman aşımı:

```text
3000 milisaniye
```

Şirket üç saniye içinde cevap vermezse:

* cevap vermiyor olarak işaretlenir
* bağlantı kapatılabilir
* başarısız kontrol sayısı yükselir
* sonraki tickte yeniden bağlantı denenir

---

# 14. Bağlantı kuralları

Şirket sunucusu:

* aynı bağlantı üzerinden birden fazla mesaj kabul etmelidir
* her sağlık kontrolünden sonra bağlantıyı kapatmamalıdır
* motor bağlantıyı kapatmadığı sürece dinlemeye devam etmelidir
* gelen verinin tek seferde tam mesaj olacağını varsaymamalıdır
* TCP paketlerinin parçalanabileceğini bilmelidir
* `\n` görülene kadar veriyi tamponda biriktirmelidir
* bir okumada birden fazla mesaj gelebileceğini hesaba katmalıdır

TCP mesaj sınırlarını korumaz.

Örneğin motor şu mesajı gönderse bile:

```json
{"mesajTuru":"saglikKontrolu"}\n
```

şirket bunu parça parça alabilir:

```text
{"mesajT
```

```text
uru":"saglikKont
```

```text
rolu"}\n
```

Şirket bu parçaları birleştirmek zorundadır.

---

# 15. Yasaklanan hazır sistemler

Oyuncular yüksek seviyeli hazır sunucu çatıları kullanamaz.

Yasak örnekler:

* ASP.NET
* Express
* NestJS
* Fastify
* Gin
* Fiber
* Actix Web
* Rocket
* Spring
* hazır WebSocket sunucuları
* hazır RPC sistemleri
* hazır mikroservis platformları

Standart dil kütüphaneleri kullanılabilir.

İzin verilen örnekler:

* C# `TcpClient`
* Rust `std::net`
* Go `net`
* Node.js `net`

Oyuncu aşağıdaki sistemleri kendisi oluşturmalıdır:

* bağlantı kabulü
* mesaj tamponu
* satır ayırma
* JSON ayrıştırma
* bağlantı yönetimi
* zaman aşımı
* eşzamanlı istemci yönetimi
* hata mesajları
* görev kuyruğu

---

# 16. Motor protokolü ve şirket protokolleri arasındaki fark

## Motor Ortak Protokolü

Motor Ortak Protokolü oyuna bağlanmak için zorunludur.

Bu protokol:

* ücretsizdir
* herkese açıktır
* motor tarafından yönetilir
* hiçbir şirkete ait değildir
* satılamaz
* lisanslanamaz
* rakiplere kapatılamaz

Motor Ortak Protokolü bir ülkenin temel yol kuralları gibidir.

Bütün şirketler aynı kuralları kullanarak motorla konuşur.

## Şirket protokolleri

Şirketler oyun içerisinde kendi ticari protokollerini geliştirebilir.

Örnek:

```text
Rustix SecureLink
Mudaf FastTransfer
Ugur Connect
```

Şirket protokolleri şunları yapabilir:

* şirket hizmetlerini birbirine bağlamak
* başka şirketlere lisanslanmak
* ücretli veya ücretsiz olmak
* belirli hizmetlerde zorunlu tutulmak
* daha hızlı iletişim sunmak
* daha güvenli iletişim sunmak
* daha düşük veri kullanmak
* şirket ekosistemini büyütmek

Ancak hiçbir şirket protokolü Motor Ortak Protokolünün yerine geçemez.

---

# 17. Şirket protokolü geliştirme örneği

Rustix aşağıdaki protokolü geliştirebilir:

```text
Protokol adı: RustLink
Sürüm: 1.0
Sahip: Rustix
Amaç: Rustix hizmetleri arasında güvenli veri aktarımı
```

Rustix daha sonra şu hizmetleri çıkarabilir:

* Rustix Mail
* Rustix Drive
* Rustix Calendar
* Rustix Payment
* Rustix Auth

Bu hizmetlerin tamamı RustLink kullanabilir.

Örneğin Rustix Mail yalnızca Rustix Auth üzerinden giriş kabul edebilir.

Rustix Drive dosya bağlantılarını yalnızca RustLink biçiminde oluşturabilir.

Başka şirketler Rustix kullanıcılarına erişmek için RustLink lisansı almak zorunda kalabilir.

Bu durumda Rustix kendi ekosistemini kurmuş olur.

---

# 18. Piyasayı protokole zorlama

Bir şirket piyasayı doğrudan emir vererek kendi protokolüne zorlayamaz.

Bunun yerine güçlü bir ekosistem oluşturarak fiilen zorlayabilir.

Örnek süreç:

1. Rustix güçlü bir kimlik doğrulama hizmeti çıkarır.
2. Birçok müşteri Rustix Auth kullanmaya başlar.
3. Rustix diğer hizmetlerini RustLink protokolüne bağlar.
4. Başka şirketler Rustix müşterilerine ulaşmak ister.
5. Bunun için RustLink desteği eklemek zorunda kalırlar.
6. RustLink piyasada yaygın standart hâline gelir.
7. Rustix lisans ve işlem geliri elde eder.

Motor bu süreci aşağıdaki ölçülerle değerlendirir:

* protokolü kullanan hizmet sayısı
* protokolü kullanan şirket sayısı
* protokole bağlı müşteri sayısı
* işlem hacmi
* güvenlik puanı
* gecikme
* hata oranı
* lisans fiyatı
* entegrasyon kolaylığı
* alternatif protokollerin gücü

---

# 19. Protokol sahipliği

Şirket protokolü motor kayıt sistemine kaydedilir.

Örnek kayıt:

```json
{
  "protokolKimligi": "rustix-rustlink",
  "protokolAdi": "RustLink",
  "sahipSirketKimligi": "tunahan-rustix",
  "surum": "1.0",
  "durum": "gelistiriliyor",
  "lisansTuru": "ucretli",
  "lisansUcreti": 5000
}
```

Motor kayıt altına alınmamış protokolleri resmî ticari ürün olarak kabul etmez.

---

# 20. Protokol yaşam döngüsü

Bir şirket protokolü aşağıdaki aşamalardan geçer:

```text
taslak
gelistiriliyor
testEdiliyor
onaylandi
yayinda
eski
kaldirildi
```

### taslak

Yalnızca fikir ve teknik tasarım vardır.

### gelistiriliyor

Şirket protokolü kodlamaktadır.

### testEdiliyor

Motor protokolü performans ve güvenlik testlerine sokar.

### onaylandi

Protokol teknik olarak çalışır durumdadır.

### yayinda

Diğer hizmetler ve şirketler protokolü kullanabilir.

### eski

Daha yeni bir sürüm yayımlanmıştır.

### kaldirildi

Protokol artık yeni bağlantılar için kullanılamaz.

---

# 21. Protokol lisans türleri

Bir şirket protokolü aşağıdaki lisans biçimlerinden birini kullanabilir:

```text
acik
ucretsiz
tekSeferlik
aylik
kullanimBasina
gelirPaylasimli
sadeceKendiSirketi
```

### açık

Bütün şirketler ücretsiz kullanabilir ve kendi uygulamalarını geliştirebilir.

### ücretsiz

Kullanmak ücretsizdir ancak protokol şirkete aittir.

### tek seferlik

Şirket bir kez ödeme yaparak protokol desteği kazanır.

### aylık

Şirket her oyun ayı lisans ücreti öder.

### kullanım başına

Her işlem için protokol sahibine ödeme yapılır.

### gelir paylaşımlı

Protokolü kullanan hizmetin gelirinden belirli bir yüzde alınır.

### sadece kendi şirketi

Protokol dış şirketlere kapalıdır.

---

# 22. Güvenlik sınırı

Şirketler rakiplerinin sunucularına saldırabilir ancak gerçek bilgisayarlara zarar veremez.

Oyun içindeki saldırılar yalnızca motorun belirlediği sınırlar içerisinde gerçekleştirilir.

Yasaktır:

* bilgisayar dosyalarını silmek
* işletim sistemine zarar vermek
* gerçek şifreleri ele geçirmek
* oyun klasörü dışına erişmek
* kalıcı zararlı yazılım bırakmak
* ağdaki oyun dışı cihazlara saldırmak
* gerçek hizmetleri engellemek

İzin verilen oyun içi saldırılar daha sonra motorun görev ve güvenlik sistemi üzerinden tanımlanacaktır.

---

# 23. İlk şirket sunucusunun minimum görevi

İlk testte şirket sunucusunun yalnızca şunları yapması yeterlidir:

1. Kendi portunda TCP sunucusu açmak.
2. Motor bağlantısını kabul etmek.
3. `merhaba` mesajını okumak.
4. `sirketTanitim` mesajı göndermek.
5. `kayitSonucu` mesajını okumak.
6. `saglikKontrolu` mesajlarını dinlemek.
7. Her sağlık kontrolüne `saglikSonucu` göndermek.
8. Bağlantıyı açık tutmak.
9. Geçersiz JSON geldiğinde çökmemek.
10. Motor kapandığında yeni bağlantı beklemeye devam etmek.

Bu aşamada ekonomi, hizmet ve şirket protokolü kodlanmayacaktır.

İlk hedef yalnızca motorla kesintisiz iletişim kurmaktır.

---

# 24. İlk başarı kriteri

Her şirket motorla bağlandıktan sonra art arda en az 20 sağlık kontrolüne doğru cevap vermelidir.

Başarılı motor ekranı şu biçimde görünmelidir:

```text
================ TICK 20 ================

Rustix           | ÇALIŞIYOR | 8.24 ms
Mudaf            | ÇALIŞIYOR | 10.61 ms
Ugur Link        | ÇALIŞIYOR | 13.08 ms
```

Bir şirketin ilk aşamayı tamamlaması için:

* bağlantısı kopmamalı
* istek kimliği doğru olmalı
* mesaj türü doğru olmalı
* JSON geçerli olmalı
* cevap üç saniyeden kısa sürmeli
* 20 sağlık kontrolü arka arkaya başarılı olmalıdır
