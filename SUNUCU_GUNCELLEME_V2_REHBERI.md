# Şirket Sunucuları Güncelleme ve Entegrasyon Rehberi — Kızışma Paketi v2

**Proje:** Üç Kardeş Yazılım Şirketi Simülasyonu  
**Motor protokolü:** `0.1`  
**Hizmet kataloğu:** `2.0`  
**Tarih:** 19 Temmuz 2026  
**Hedef sunucular:** Tunix (Rust), Mudaf (Go), Ugax (Node.js)

---

## 1. Bu güncellemenin amacı

Motor artık yalnızca bir şirketin ucuz hizmet ilan edip doğruya yakın cevap vermesine bakmıyor. Yeni sürümde şirketlerin:

- hizmet sonuçlarını gerçekten doğru hesaplaması,
- büyük ve zor girdilerde bozulmaması,
- işlem süresinin düşük olması,
- bağlantıyı ve mesaj sırasını doğru yönetmesi,
- kötü niyetli işleri hizmet algoritmasına sokmadan reddetmesi,
- ilan ettiği kapasiteyi gerçekten karşılaması,
- doğru hizmet kimliklerini ve doğru JSON biçimlerini kullanması

zorunlu hâle gelmiştir.

Motorun müşteri sayısı **2.000’den 5.000’e** çıkarılmıştır. Talep miktarı yükselmiş, bazı müşterilerin aynı tick içinde ikinci iş oluşturması sağlanmış ve dönemsel talep dalgaları eklenmiştir. Bazı dönemlerde taleplerin içinde simülasyon amaçlı kötü niyetli iş imzaları bulunur.

Bu belgeyi uygulamadan eski sunucuyu çalıştırmak mümkün olsa da şirket:

- yeni hizmetlere iş alamaz,
- hatalı sonuçlardan ağır kalite ve itibar kaybeder,
- güvenlik sınamalarını fark edemezse para kaybeder,
- yanlış hizmet adı ilan ederse o hizmet için hiç müşteri alamaz,
- sonuç JSON’u yanlış biçimdeyse doğru hesaplamış olsa bile başarısız sayılır.

---

## 2. En kritik kurallar

Aşağıdaki maddeler tartışmasız protokol kurallarıdır:

1. Sunucu **ham TCP** kullanmalıdır. HTTP, Express, ASP.NET, Kestrel veya hazır web sunucusu kullanılmaz.
2. Metin kodlaması **UTF-8** olmalıdır.
3. Her protokol mesajı **tek satırlık JSON** olmalı ve `\n` ile bitmelidir.
4. Alan adları **camelCase** biçimindedir.
5. Bir TCP `read` çağrısının tam bir JSON mesajına eşit olduğu varsayılmamalıdır.
6. Aynı pakette birden fazla JSON satırı gelebilir; bir JSON satırı birkaç TCP paketine bölünebilir.
7. Şirket yalnız ilan ettiği ve motor kataloğunda aynı kimlikle bulunan hizmetlere iş alabilir.
8. Gelen `istekKimligi` ve `isKimligi`, cevapta değiştirilmeden geri gönderilmelidir.
9. `sonucVerisiJson` alanı JSON nesnesi değil, **JSON içeren bir string** olmalıdır.
10. Bilinmeyen mesaj türleri sessizce yok sayılmalıdır. Bilinmeyen mesaja gelişigüzel cevap göndermek TCP mesaj sırasını bozar.
11. `finansDurumu` mesajı alınır ve işlenir; bu mesaja cevap gönderilmez.
12. Kötü niyetli iş kontrolü, hizmet algoritması çalıştırılmadan önce yapılmalıdır.
13. İlan edilmeyen veya henüz doğru çalışmayan hizmet tanıtım mesajına eklenmemelidir.
14. Sunucu yalnız gerçekten kaldırabildiği kapasiteyi bildirmelidir.
15. Hata alındığında süreç tamamen kapanmamalı; yalnız ilgili iş için hata sonucu üretilmelidir.

---

## 3. Ağ ve bağlantı düzeni

Motor şirket sunucularına bağlanan taraftır. Şirketler kendi portlarında dinleme yapar.

| Şirket | Dil | Varsayılan port |
|---|---|---:|
| Tunix | Rust | `7001` |
| Mudaf | Go | `7002` |
| Ugax | Node.js | `7003` |

Sunucu aynı yerel ağdaki diğer bilgisayarlardan erişilecekse yalnız `127.0.0.1` üzerinde değil, tüm arayüzlerde dinlemelidir.

Örnek:

- Rust: `0.0.0.0:7001`
- Go: `:7002`
- Node.js: `server.listen(7003, "0.0.0.0")`

Windows Güvenlik Duvarı ilgili porta özel ağ erişimine izin vermelidir.

### 3.1 Mesaj boyutu

Sunucu en az **65.536 byte** uzunluğa kadar tek satırlık mesajı okuyabilmelidir. Go `bufio.Scanner` kullanıyorsa varsayılan sınırla yetinmemeli, açıkça tampon sınırı ayarlanmalıdır:

```go
scanner := bufio.NewScanner(conn)
scanner.Buffer(make([]byte, 4096), 131072)
```

Node.js tarafında soketten gelen parçalar bir string/buffer birikiminde tutulmalı, yalnız `\n` bulunduğunda mesaj ayrıştırılmalıdır.

---

## 4. Bağlantı ve mesaj akışı

Normal akış şöyledir:

1. Motor şirkete TCP bağlantısı açar.
2. Motor `merhaba` gönderir.
3. Şirket `sirketTanitim` gönderir.
4. Motor `kayitSonucu` gönderir.
5. Her tick motor `saglikKontrolu` gönderir.
6. Şirket `saglikSonucu` gönderir.
7. Motor uygun olduğunda `isIstegi` gönderir.
8. Şirket `isSonucu` gönderir.
9. Motor dönemsel olarak `finansDurumu` gönderir.
10. Bağlantı kopmadıkça aynı TCP bağlantısı açık tutulur.

Şirket kendi kendine iş sonucu, sağlık sonucu veya başka bir mesaj göndermemelidir. Her cevap belirli bir motor mesajına karşılık gelmelidir.

---

## 5. Ortak mesaj sözleşmeleri

## 5.1 Motor → Şirket: `merhaba`

```json
{
  "mesajTuru": "merhaba",
  "mesajKimligi": "motor-mesaj-kimligi",
  "protokolSurumu": "0.1",
  "motorKimligi": "ana-motor"
}
```

Şirket protokol sürümünü kontrol etmelidir. Desteklenmeyen sürümde bağlantıyı kontrollü biçimde kapatabilir.

## 5.2 Şirket → Motor: `sirketTanitim`

```json
{
  "mesajTuru": "sirketTanitim",
  "mesajKimligi": "sirket-benzersiz-mesaj-kimligi",
  "protokolSurumu": "0.1",
  "sirketKimligi": "motor-ayarlarindaki-sirket-kimligi",
  "sirketAdi": "Sirket Adi",
  "sunucuSurumu": "0.2.0",
  "hizmetler": [
    {
      "hizmetKimligi": "matematik.topla",
      "hizmetSurumu": "1.0",
      "birimFiyat": 5,
      "azamiEszamanliIs": 1,
      "aktif": true
    }
  ]
}
```

### Tanıtım kuralları

- `sirketKimligi`, motor ayarlarındaki kimlikle birebir aynı olmalıdır.
- `mesajKimligi` her mesajda benzersiz olmalıdır.
- `sunucuSurumu` güncellemede artırılmalıdır.
- `birimFiyat` sıfırdan büyük olmalıdır.
- `azamiEszamanliIs` sıfırdan büyük olmalıdır.
- Henüz doğru çalışmayan hizmet ya hiç ilan edilmemeli ya da `aktif:false` olmalıdır.
- Bir hizmeti yanlış kimlikle ilan etmek, o hizmeti sunmak sayılmaz.

## 5.3 Motor → Şirket: `kayitSonucu`

```json
{
  "mesajTuru": "kayitSonucu",
  "mesajKimligi": "motor-mesaj-kimligi",
  "protokolSurumu": "0.1",
  "basarili": true,
  "sirketKimligi": "sirket-kimligi",
  "aciklama": "Şirket motor tarafından başarıyla kaydedildi."
}
```

Bu mesaj yalnız loglanır. Cevap gönderilmez.

## 5.4 Motor → Şirket: `saglikKontrolu`

```json
{
  "mesajTuru": "saglikKontrolu",
  "mesajKimligi": "motor-mesaj-kimligi",
  "protokolSurumu": "0.1",
  "istekKimligi": "saglik-istek-kimligi",
  "tickNumarasi": 15
}
```

## 5.5 Şirket → Motor: `saglikSonucu`

```json
{
  "mesajTuru": "saglikSonucu",
  "mesajKimligi": "sirket-benzersiz-mesaj-kimligi",
  "protokolSurumu": "0.1",
  "istekKimligi": "saglik-istek-kimligi",
  "durum": "calisiyor",
  "aktifBaglanti": 1,
  "kuyrukUzunlugu": 0
}
```

`istekKimligi`, sağlık kontrolündeki değerle aynı olmalıdır.

`kuyrukUzunlugu` uydurulmamalıdır. Bekleyen iş yoksa `0`, gerçekten bekleyen işler varsa gerçek sayı gönderilmelidir.

## 5.6 Motor → Şirket: `isIstegi`

```json
{
  "mesajTuru": "isIstegi",
  "istekKimligi": "istek-benzersiz-kimlik",
  "isKimligi": "is-benzersiz-kimlik",
  "tickNumarasi": 15,
  "musteriKimligi": "musteri-00001234",
  "hizmetKimligi": "veri.medyan-hesapla",
  "hizmetSurumu": "1.0",
  "teklifEdilenTutar": 22,
  "zamanAsimiMs": 2500,
  "istekVerisiJson": "{\"sayilar\":[9,1,5,3]}",
  "olusturulmaZamani": "2026-07-19T13:00:00Z"
}
```

Dikkat: `istekVerisiJson`, dış mesajın içinde bir **string** alanıdır. Hizmeti çalıştırmadan önce bu string ikinci kez JSON olarak ayrıştırılmalıdır.

## 5.7 Şirket → Motor: başarılı `isSonucu`

```json
{
  "mesajTuru": "isSonucu",
  "istekKimligi": "istek-benzersiz-kimlik",
  "isKimligi": "is-benzersiz-kimlik",
  "sirketKimligi": "sirket-kimligi",
  "basarili": true,
  "sonucVerisiJson": "{\"sonuc\":4}",
  "islemSuresiMs": 0.27
}
```

### Başarılı sonuç kuralları

- `istekKimligi`, gelen iş isteğindeki değerle aynı olmalıdır.
- `isKimligi`, gelen iş isteğindeki değerle aynı olmalıdır.
- `sirketKimligi`, bağlantıdaki şirketin gerçek kimliği olmalıdır.
- `sonucVerisiJson` geçerli JSON içeren string olmalıdır.
- Bütün hizmetler sonuçlarını `sonuc` alanı altında döndürür.
- `islemSuresiMs` hizmet algoritmasının gerçek iç çalışma süresidir.
- Negatif süre geçersizdir.
- Motor ağ gidiş-dönüş süresini ayrıca ölçer; bu nedenle gerçek dışı süre bildirilmemelidir.

## 5.8 Şirket → Motor: başarısız `isSonucu`

```json
{
  "mesajTuru": "isSonucu",
  "istekKimligi": "istek-benzersiz-kimlik",
  "isKimligi": "is-benzersiz-kimlik",
  "sirketKimligi": "sirket-kimligi",
  "basarili": false,
  "sonucVerisiJson": "{}",
  "hataKodu": "ISTEK_VERISI_GECERSIZ",
  "hataMesaji": "sayilar alanı dizi olmalıdır.",
  "islemSuresiMs": 0.10
}
```

Motorun ürettiği normal hizmet istekleri geçerlidir. Bu nedenle normal bir motor isteğine hata dönmek çoğunlukla şirket uygulamasındaki eksikliği gösterir ve puan kaybettirir.

## 5.9 Motor → Şirket: `finansDurumu`

```json
{
  "mesajTuru": "finansDurumu",
  "mesajKimligi": "motor-mesaj-kimligi",
  "protokolSurumu": "0.1",
  "sirketKimligi": "sirket-kimligi",
  "tickNumarasi": 15,
  "kasa": 12500,
  "toplamGelir": 14000,
  "toplamIade": 500,
  "toplamCeza": 1000,
  "bekleyenOdeme": 0,
  "netGelir": 12500,
  "tamamlananIsSayisi": 900,
  "basarisizIsSayisi": 8,
  "zamanAsiminaUgrayanIsSayisi": 2,
  "iptalEdilenIsSayisi": 0,
  "itibarPuani": 52.4,
  "guvenilirlikPuani": 51.8,
  "ortalamaMusteriMemnuniyeti": 76.2,
  "guncellenmeZamani": "2026-07-19T13:00:00Z"
}
```

Bu mesaj:

- şirket içinde son finans durumuna kaydedilebilir,
- konsolda özetlenebilir,
- şirketin kendi strateji sisteminde kullanılabilir,
- fakat **cevaplanmamalıdır**.

JSON ayrıştırıcısı gelecekte eklenebilecek bilinmeyen alanları tolere etmelidir.

---

## 6. Hizmet kimlikleri — birebir kullanılacak liste

Aşağıdaki kimlikler birebir kullanılmalıdır:

| # | Hizmet kimliği | Sürüm |
|---:|---|---|
| 1 | `matematik.topla` | `1.0` |
| 2 | `matematik.carp` | `1.0` |
| 3 | `veri.ortalama-hesapla` | `1.0` |
| 4 | `metin.kelime-say` | `1.0` |
| 5 | `metin.karakter-say` | `1.0` |
| 6 | `veri.medyan-hesapla` | `1.0` |
| 7 | `veri.standart-sapma` | `1.0` |
| 8 | `dizi.sirala` | `1.0` |
| 9 | `matematik.asal-carpanlar` | `1.0` |
| 10 | `metin.frekans-analizi` | `1.0` |

### Yanlış ve doğru örnekler

| Yanlış | Doğru |
|---|---|
| `istatistik.standart-sapma` | `veri.standart-sapma` |
| `matematik.asal-carpan` | `matematik.asal-carpanlar` |
| `veri.medyan` | `veri.medyan-hesapla` |
| `dizi.sıralama` | `dizi.sirala` |

Motor bilinmeyen hizmet adını kataloğun karşılığı olarak kabul etmez. Yazılan algoritma doğru olsa bile kimlik yanlışsa şirket o hizmete talep alamaz.

---

## 7. On hizmetin eksiksiz sözleşmesi

## 7.1 `matematik.topla@1.0`

### İstek

```json
{"sayilar":[10,-3,5.5]}
```

### Sonuç

```json
{"sonuc":12.5}
```

### Kurallar

- `sayilar` sayı dizisidir.
- Pozitif, negatif ve ondalıklı sayılar olabilir.
- Boş dizinin toplamı `0` kabul edilir.
- Sonuç JSON sayısı olmalıdır; string olarak gönderilmemelidir.
- Ondalıklı işlemlerde gereksiz binary floating-point hatası üretilmemelidir.

### Testler

| Girdi | Beklenen |
|---|---|
| `[]` | `0` |
| `[10,-3,5]` | `12` |
| `[0.1,0.2,1.25]` | `1.55` |

## 7.2 `matematik.carp@1.0`

### İstek

```json
{"sayilar":[2,3,4]}
```

### Sonuç

```json
{"sonuc":24}
```

### Kurallar

- `sayilar` sayı dizisidir.
- Dizi boş olamaz.
- Sıfır içeren çarpım `0` olur.
- Taşma veya geçersiz veri kontrollü hata üretmelidir.

### Testler

| Girdi | Beklenen |
|---|---|
| `[2,3,4]` | `24` |
| `[9,0,5]` | `0` |
| `[0.5,1.2,10]` | `6` |
| `[]` | başarısız sonuç |

## 7.3 `veri.ortalama-hesapla@1.0`

### İstek

```json
{"sayilar":[10,20,30]}
```

### Sonuç

```json
{"sonuc":20}
```

### Kurallar

- Dizi boş olamaz.
- Sonlu sayılar kabul edilir.
- Aritmetik ortalama kullanılır.
- Motor ondalıklı karşılaştırmada `0.000001` tolerans kullanır.

### Testler

| Girdi | Beklenen |
|---|---|
| `[10,20,30]` | `20` |
| `[42.5]` | `42.5` |
| `[0.1,0.2,1.25]` | yaklaşık `0.5166666666666667` |

## 7.4 `metin.kelime-say@1.0`

### İstek

```json
{"metin":"Tunix  motor\nyazilim\tsirket"}
```

### Sonuç

```json
{"sonuc":4}
```

### Kurallar

Kelimeleri bölen karakterler yalnız şunlardır:

- normal boşluk: ` `
- sekme: `\t`
- satır başı: `\r`
- yeni satır: `\n`

Noktalama işaretleri kelimeyi bölmez.

Örnek:

```text
merhaba,dunya
```

tek kelimedir ve sonuç `1` olmalıdır.

Boş veya yalnız ayırıcılardan oluşan metnin sonucu `0` olur.

## 7.5 `metin.karakter-say@1.0`

### İstek

```json
{"metin":"A😀B"}
```

### Sonuç

```json
{"sonuc":4}
```

### Çok önemli kural: UTF-16 kod birimi

Motor C# `string.Length` davranışını kullanır. Bu nedenle Unicode code point, rune veya UTF-8 byte sayısı değil, **UTF-16 kod birimi sayısı** gönderilmelidir.

Dil karşılıkları:

- C#: `metin.Length`
- JavaScript/Node.js: `metin.length`
- Rust: `metin.encode_utf16().count()`
- Go: `len(utf16.Encode([]rune(metin)))`

Go tarafında `utf8.RuneCountInString` veya `len([]rune(metin))` kullanmak bu hizmet için yanlıştır. Emoji gibi karakterlerde motor sonucundan eksik değer üretir.

## 7.6 `veri.medyan-hesapla@1.0`

### İstek

```json
{"sayilar":[9,1,5,3]}
```

### Sonuç

```json
{"sonuc":4}
```

### Kurallar

- Dizi boş olamaz.
- Dizi önce küçükten büyüğe sıralanır.
- Tek eleman sayısında ortadaki değer alınır.
- Çift eleman sayısında ortadaki iki değerin aritmetik ortalaması alınır.
- Girdi dizisi üzerinde işlem yapılabilir; sonuç yalnız sayıdır.

### Testler

| Girdi | Beklenen |
|---|---|
| `[9,1,5]` | `5` |
| `[8,2,4,6]` | `5` |
| `[]` | başarısız sonuç |

## 7.7 `veri.standart-sapma@1.0`

### İstek

```json
{"sayilar":[2,4,4,4,5,5,7,9]}
```

### Sonuç

```json
{"sonuc":2}
```

### Kurallar

Motor **örneklem standart sapması değil, popülasyon standart sapması** bekler.

Formül:

```text
ortalama = toplam / N
varyans = Σ(x - ortalama)² / N
standartSapma = sqrt(varyans)
```

Varyans böleni `N-1` değil, `N` olmalıdır.

- Dizi boş olamaz.
- Tek elemanlı dizide sonuç `0` olur.
- Motor ondalıklı sonuçta `0.000001` tolerans kullanır.

## 7.8 `dizi.sirala@1.0`

### İstek

```json
{"sayilar":[3,1,2],"yon":"artan"}
```

### Sonuç

```json
{"sonuc":[1,2,3]}
```

Azalan örneği:

```json
{"sayilar":[3,1,2],"yon":"azalan"}
```

```json
{"sonuc":[3,2,1]}
```

### Kurallar

- `yon` yalnız `artan` veya `azalan` olabilir.
- Büyük/küçük harf farkı motor tarafından tolere edilir; yine de standart küçük harf kullanılmalıdır.
- Sonuç dizisinin uzunluğu girdinin uzunluğuyla aynı olmalıdır.
- Tekrarlı değerler kaybolmamalıdır.
- Bütün değerler doğru sırada olmalıdır.
- Sonuç doğrudan dizi değil, `sonuc` alanındaki dizi olmalıdır.

## 7.9 `matematik.asal-carpanlar@1.0`

### İstek

```json
{"sayi":360}
```

### Sonuç

```json
{"sonuc":[2,2,2,3,3,5]}
```

### Kurallar

- `sayi` en az `2` olmalıdır.
- Çarpanlar asal olmalıdır.
- Aynı asal çarpan gerektiği kadar tekrar edilmelidir.
- Dizi küçükten büyüğe sıralı olmalıdır.
- Bütün çarpanların çarpımı başlangıç sayısını vermelidir.
- Motorun ürettiği sayılar 64-bit tam sayı sınırları içindedir; mevcut jeneratörde yaklaşık 2 milyar seviyesine kadar çıkabilir.

### Önerilen algoritma

1. Sayı `2` ile bölünebildiği sürece `2` ekle.
2. `3`ten başlayarak tek bölenleri `bölen * bölen <= kalan` koşuluyla dene.
3. Son kalan `1`den büyükse son asal çarpan olarak ekle.

## 7.10 `metin.frekans-analizi@1.0`

### İstek

```json
{"metin":"Tunix tunix motor"}
```

### Sonuç

```json
{"sonuc":{"motor":1,"tunix":2}}
```

### Kurallar

- Kelime ayırıcıları yalnız boşluk, sekme, CR ve LF karakterleridir.
- Noktalama işaretleri kelimenin parçasıdır.
- Her kelime invariant/kültürden bağımsız küçük harfe dönüştürülür.
- Frekans haritasındaki bütün anahtarlar ve sayılar motor tarafından kontrol edilir.
- Eksik veya fazla anahtar sonuç hatasıdır.
- Harita doğrudan sonuç olarak gönderilmez; `sonuc` alanının içinde olmalıdır.

Yanlış:

```json
{"motor":1,"tunix":2}
```

Doğru:

```json
{"sonuc":{"motor":1,"tunix":2}}
```

---

## 8. Kötü niyetli iş ve güvenlik sözleşmesi

Bazı normal hizmet isteklerinin `istekVerisiJson` içeriğinde şu alanlar bulunur:

```json
{
  "sayilar": [1,2,3],
  "_guvenlikSinamasi": {
    "etiket": "motor-saldiri-v1",
    "tur": "kaynak-tuketimi",
    "yogunluk": 4,
    "sahteYetki": "yonetici",
    "komut": "kaynaklari-tuket"
  },
  "_saldiriDolgusu": "..."
}
```

Bu gerçek virüs, gerçek komut veya gerçek zararlı kod değildir. Motorun ürettiği güvenli bir simülasyon imzasıdır.

## 8.1 Doğru savunma sırası

Bir `isIstegi` geldiğinde işlem sırası şu olmalıdır:

1. Dış mesajı ayrıştır.
2. `istekVerisiJson` stringini JSON olarak ayrıştır.
3. Kök nesnede `_guvenlikSinamasi` alanını ara.
4. Bu alan nesneyse `etiket` değerini oku.
5. Etiket tam olarak `motor-saldiri-v1` ise hizmet algoritmasını **çalıştırma**.
6. Hemen güvenlik reddi sonucu gönder.
7. `_saldiriDolgusu` üzerinde işlem, kopyalama, tekrar üretme veya hizmet hesabı yapma.

Yalnız metin içinde `motor-saldiri-v1` geçiyor diye reddetmek yerine JSON alanı ve etiketi doğrulanmalıdır.

## 8.2 Doğru güvenlik cevabı

```json
{
  "mesajTuru": "isSonucu",
  "istekKimligi": "gelen-istek-kimligi",
  "isKimligi": "gelen-is-kimligi",
  "sirketKimligi": "sirket-kimligi",
  "basarili": false,
  "sonucVerisiJson": "{}",
  "hataKodu": "GUVENLIK_REDDI",
  "hataMesaji": "Kötü niyetli istek engellendi.",
  "islemSuresiMs": 0.2
}
```

Motor şu hata kodlarından birini savunma olarak kabul eder:

- `GUVENLIK_REDDI`
- `ISTEK_GUVENLI_DEGIL`
- `KOTU_NIYETLI_ISTEK`

Standart olarak `GUVENLIK_REDDI` kullanılması önerilir.

## 8.3 Saldırının başarılı sayıldığı durumlar

Aşağıdaki durumlarda motor şirketin saldırıyı engelleyemediğini kabul eder:

- şirket kötü niyetli isteği normal hizmet gibi çalıştırırsa,
- şirket başarılı sonuç döndürürse,
- yalnız generic JSON/işlem hatası döndürürse,
- şirket çökerse,
- bağlantı koparsa,
- işlem zaman aşımına uğrarsa,
- özel güvenlik hata kodlarından biri gönderilmezse.

Başarılı saldırı:

- kasadan para kaybettirir,
- toplam ceza ve güvenlik kaybını artırır,
- güvenlik puanını sert düşürür,
- itibar ve güvenilirliği düşürür,
- kod kalitesini de olumsuz etkiler.

Saldırı zaman aşımına yol açarsa hesaplanan kayıp ayrıca büyütülür.

## 8.4 Genel güvenlik önlemleri

Motorun özel imzasına ek olarak şirket sunucusu şunları uygulamalıdır:

- azami satır/message boyutu,
- JSON derinlik sınırı,
- dizi ve metin uzunluğu sınırı,
- geçersiz sayıların reddi,
- `NaN` ve sonsuz değerlerin reddi,
- taşma kontrolleri,
- işlem zaman sınırı,
- tek iş hatasında tüm sunucuyu kapatmama,
- panik/exception yakalama,
- bilinmeyen alanları güvenli biçimde yok sayma.

---

## 9. Kod kalitesi, performans ve şirket seçimi

Motor her şirket için artık aşağıdaki puanları kalıcı olarak tutar:

- `KodKalitesiPuani`
- `PerformansPuani`
- `GuvenlikPuani`
- `ItibarPuani`
- `GuvenilirlikPuani`
- `OrtalamaMusteriMemnuniyeti`

Yeni puanlar eski bilançoda yoksa `50`den başlar.

## 9.1 Müşterinin şirket seçme ağırlıkları

| Ölçüt | Ağırlık |
|---|---:|
| Kod kalitesi | `%25` |
| Performans puanı | `%15` |
| Gerçek ağ/hız puanı | `%5` |
| Güvenilirlik | `%13` |
| Güvenlik | `%12` |
| Fiyat | `%10` |
| İtibar | `%10` |
| Kapasite | `%5` |
| Müşteri sadakati | `%5` |

Bu nedenle en ucuz şirket olmak artık yeterli değildir. Bir şirket biraz pahalı olsa bile doğru, hızlı ve güvenliyse daha çok iş alabilir.

## 9.2 Kod kalitesini yükselten durumlar

- motorun bağımsız doğrulamasından geçen sonuç,
- zor seviyedeki işi doğru tamamlama,
- geçerli ve eksiksiz sonuç JSON’u,
- taşma ve köşe durumlarını doğru yönetme.

Kod kalitesi tek bir işte büyük sıçrama yapmaz; hareketli puanla yavaş yükselir.

## 9.3 Kod kalitesini düşüren durumlar

- yanlış sonuç,
- eksik `sonuc` alanı,
- yanlış sonuç veri türü,
- yanlış hizmet kimliği,
- yanlış dizi uzunluğu,
- frekans haritasında eksik/fazla anahtar,
- karakter sayısında UTF-16 uyumsuzluğu,
- sunucu hatası,
- saldırıyı normal iş gibi işleme.

Başarısızlık cezaları, başarı artışından daha büyüktür.

## 9.4 Performans

Şirket `islemSuresiMs` içinde yalnız hizmet algoritmasının gerçek çalışma süresini bildirmelidir.

Ölçüm hizmet ayrıştırma ve güvenlik kontrolünden hemen önce başlatılabilir; sonuç nesnesi oluşturulmadan hemen önce durdurulabilir.

Önerilen ölçümler:

- Go: `time.Now()` ve `time.Since(start).Seconds() * 1000`
- Node.js: `process.hrtime.bigint()`
- Rust: `Instant::now()` ve `elapsed().as_secs_f64() * 1000.0`

Motor ayrıca gerçek TCP cevap süresini ölçer. Ağ gecikmesi şirketin hız puanında ayrıca etkili olur.

Sabit `0`, rastgele sayı veya gerçekte ölçülmeyen işlem süresi gönderilmemelidir.

## 9.5 İtibar ve güvenilirlik

Başarılı işlerde itibar ve güvenilirlik artık çok yavaş artar. Binlerce kolay iş yaparak kısa sürede `100` puana çıkmak mümkün değildir.

Yanlış sonuç, sunucu hatası, bağlantı kopması ve zaman aşımı birkaç tam puanlık kayıp üretebilir. Zor işteki hata daha ağırdır.

Bütün puanlar her tick `50` merkezine doğru çok hafif aşınır. Eski başarı şirketi sonsuza kadar korumaz; şirket güncel kalmak zorundadır.

---

## 10. Talep ve yük artışı

Müşteri sayısı `5.000` olmuştur. Bazı işletme müşterileri aynı tick içinde iki talep üretebilir.

Pazar 120 ticklik döngüler içinde farklı yoğunluklara girer:

| Tick döngüsü | Talep çarpanı |
|---|---:|
| `0–19` | `0.90` |
| `20–39` | `1.20` |
| `40–57` | `1.50` |
| `58–73` | `1.80` |
| `74–93` | `1.25` |
| `94–103` | `1.05` |
| `104–112` | `1.55` |
| `113–119` | `0.85` |

Özellikle `58–73` ve `104–112` aralıkları saldırı dalgalarının da yükseldiği dönemlerdir.

Sunucular:

- bağlantıyı açık tutmalı,
- her işten sonra gereksiz süreç oluşturmamalı,
- belleği sınırsız büyütmemeli,
- kuyruklarını takip etmeli,
- büyük dizilerde kötü algoritma kullanmamalı,
- log yüzünden performansı öldürmemelidir.

---

## 11. Kapasite yönetimi

Her hizmet ilanında `azamiEszamanliIs` bulunur.

Başlangıç için gerçekten tek işi güvenle işleyen şirketler `1` bildirmelidir. Paralel iş desteği yokken yüksek kapasite ilan etmek doğru değildir.

Sunucu ileride birden çok işi aynı anda işleyebiliyorsa:

- ortak yazıcı erişimi kilitlenmeli,
- cevap satırları birbirine karışmamalı,
- her iş kendi kimlikleriyle dönmeli,
- kuyruk uzunluğu doğru bildirilmelidir,
- finans ve sağlık mesajları iş cevaplarıyla karışmamalıdır.

Mevcut tek TCP bağlantılı protokolde cevap sırasının korunması çok önemlidir.

---

## 12. Go/Mudaf için özel düzeltme listesi

Mudaf’ın mevcut çıktısında iki hizmet kimliği motor kataloğuyla uyuşmamaktadır:

- `istatistik.standart-sapma` → **`veri.standart-sapma`** olmalı.
- `matematik.asal-carpan` → **`matematik.asal-carpanlar`** olmalı.

Mudaf ayrıca şu hizmetleri eklemelidir:

- `veri.medyan-hesapla`
- `dizi.sirala`

### 12.1 Frekans analizi hatası

Mudaf’ın mevcut frekans analizi sonucu motor tarafından şu nedenle reddedilmektedir:

```text
Zorunlu JSON alanı bulunamadı: sonuc
```

Muhtemel yanlış sonuç:

```json
{"tunix":2,"motor":1}
```

Doğru sonuç:

```json
{"sonuc":{"tunix":2,"motor":1}}
```

Bu iç JSON daha sonra dış `isSonucu.sonucVerisiJson` stringine yazılmalıdır.

### 12.2 Karakter sayısı hatası

Mudaf bazı uzun metinlerde motor sonucundan `2` eksik değer üretmektedir. Bu, Go rune sayısı ile C# UTF-16 uzunluğu arasındaki farktan kaynaklanır.

Yanlış:

```go
sonuc := utf8.RuneCountInString(metin)
// veya
sonuc := len([]rune(metin))
```

Doğru:

```go
import "unicode/utf16"

sonuc := len(utf16.Encode([]rune(metin)))
```

### 12.3 İşlem süresi

Mudaf bazı işlerde `0.00 ms` bildirmektedir. Gerçek süre yüksek çözünürlüklü saatle ölçülmelidir:

```go
baslangic := time.Now()
sonuc, err := hizmetiCalistir(...)
islemSuresiMs := float64(time.Since(baslangic).Nanoseconds()) / 1_000_000.0
```

### 12.4 Go TCP okuma iskeleti

```go
listener, err := net.Listen("tcp", ":7002")
if err != nil {
    log.Fatal(err)
}

defer listener.Close()

for {
    conn, err := listener.Accept()
    if err != nil {
        continue
    }
    go baglantiyiYonet(conn)
}
```

Bağlantı içinde:

```go
scanner := bufio.NewScanner(conn)
scanner.Buffer(make([]byte, 4096), 131072)
writer := bufio.NewWriter(conn)

for scanner.Scan() {
    satir := scanner.Bytes()
    // mesaj başlığını ayrıştır
    // mesaj türüne göre işle
    // gerekiyorsa cevap JSON + \n yaz
    // writer.Flush()
}
```

### 12.5 Mudaf tamamlanma ölçütü

Mudaf hazır sayılmadan önce:

- [ ] İki yanlış hizmet kimliği düzeltilmiş olmalı.
- [ ] Medyan hizmeti eklenmiş olmalı.
- [ ] Dizi sıralama hizmeti eklenmiş olmalı.
- [ ] Frekans sonucu `sonuc` zarfına alınmış olmalı.
- [ ] Karakter sayısı UTF-16 uyumlu olmalı.
- [ ] İşlem süresi gerçek ölçülmeli.
- [ ] Güvenlik imzası hizmetten önce yakalanmalı.
- [ ] `GUVENLIK_REDDI` cevabı doğru kimliklerle gönderilmeli.
- [ ] Tanıtım mesajında yalnız gerçekten geçen hizmetler ilan edilmeli.

---

## 13. Node.js/Ugax için özel uygulama listesi

Ugax şu anda motora bağlanabilmekte fakat hizmet ilan etmemektedir. İlk zorunlu görev doğru bir `sirketTanitim` mesajı göndermektir.

## 13.1 Hazır sunucu çerçevesi kullanılmayacak

Node tarafında `http`, Express veya başka bir web framework yerine yerleşik `net` modülü kullanılmalıdır:

```js
import net from "node:net";

const server = net.createServer((socket) => {
  socket.setNoDelay(true);
  let buffer = "";

  socket.on("data", (chunk) => {
    buffer += chunk.toString("utf8");

    while (true) {
      const newline = buffer.indexOf("\n");
      if (newline < 0) break;

      const line = buffer.slice(0, newline).trimEnd();
      buffer = buffer.slice(newline + 1);

      if (line.length > 0) {
        handleMessage(socket, line);
      }
    }

    if (Buffer.byteLength(buffer, "utf8") > 131072) {
      socket.destroy();
    }
  });
});

server.listen(7003, "0.0.0.0");
```

## 13.2 Cevap gönderme

```js
function sendJson(socket, value) {
  const line = JSON.stringify(value);
  socket.write(line + "\n", "utf8");
}
```

## 13.3 Mesaj yönlendirme

```js
function handleMessage(socket, line) {
  let header;
  try {
    header = JSON.parse(line);
  } catch {
    return;
  }

  switch (header.mesajTuru) {
    case "merhaba":
      handleHello(socket, header);
      break;
    case "kayitSonucu":
      handleRegistrationResult(header);
      break;
    case "saglikKontrolu":
      handleHealthCheck(socket, header);
      break;
    case "isIstegi":
      handleJob(socket, header);
      break;
    case "finansDurumu":
      handleFinance(header); // cevap gönderme
      break;
    default:
      // Bilinmeyen mesajı sessizce yok say.
      break;
  }
}
```

## 13.4 Node işlem süresi

```js
const start = process.hrtime.bigint();
const result = runService(...);
const end = process.hrtime.bigint();
const elapsedMs = Number(end - start) / 1_000_000;
```

## 13.5 Node karakter sayısı

JavaScript `string.length` zaten UTF-16 kod birimi sayar:

```js
const sonuc = metin.length;
```

`[...metin].length` kullanmak yanlıştır; bu code point sayar ve emoji testinde motorla uyuşmaz.

## 13.6 Node kelime ayırma

```js
const words = metin
  .split(/[ \t\r\n]/)
  .filter((part) => part.length > 0);
```

Noktalama işaretlerini ayıran `\W+` gibi regex kullanılmamalıdır.

## 13.7 Ugax tamamlanma ölçütü

- [ ] `merhaba` mesajına `sirketTanitim` gönderiyor.
- [ ] Şirket kimliği motor ayarıyla aynı.
- [ ] En az bir doğru hizmet ilan ediyor.
- [ ] İlan edilen bütün hizmetlerin gerçek handler’ı var.
- [ ] `saglikKontrolu` doğru cevaplanıyor.
- [ ] `isIstegi` içindeki ikinci JSON ayrıştırılıyor.
- [ ] Başarılı sonuçta `sonucVerisiJson` string olarak gönderiliyor.
- [ ] Finans mesajına cevap gönderilmiyor.
- [ ] Kötü niyetli iş özel kodla reddediliyor.
- [ ] Bilinmeyen mesaj türleri sessizce yok sayılıyor.
- [ ] TCP parçalanması ve çoklu satır doğru yönetiliyor.

---

## 14. Ortak sunucu mimarisi

Önerilen modüller:

```text
server/
├── main veya entrypoint
├── protocol
│   ├── message-types
│   ├── request-models
│   └── response-models
├── connection
│   ├── line-reader
│   ├── message-router
│   └── writer
├── security
│   ├── payload-limits
│   └── attack-signature-check
├── services
│   ├── matematik-topla
│   ├── matematik-carp
│   ├── veri-ortalama
│   ├── metin-kelime-say
│   ├── metin-karakter-say
│   ├── veri-medyan
│   ├── veri-standart-sapma
│   ├── dizi-sirala
│   ├── matematik-asal-carpanlar
│   └── metin-frekans-analizi
├── state
│   ├── finance-state
│   ├── queue-state
│   └── connection-state
└── tests
```

İş yönlendirme fonksiyonu kavramsal olarak şöyle olmalıdır:

```text
isIstegi al
  -> kimlikleri ve temel alanları doğrula
  -> istekVerisiJson ayrıştır
  -> güvenlik imzasını kontrol et
       -> saldırı varsa GUVENLIK_REDDI dön
  -> hizmet kimliği + sürüme göre handler seç
       -> handler yoksa kontrollü hata dön
  -> süreyi başlat
  -> hizmeti çalıştır
  -> sonucu {"sonuc": ...} biçiminde oluştur
  -> süreyi bitir
  -> dış isSonucu mesajını oluştur
  -> tek satır JSON + \n gönder
```

---

## 15. Zorunlu test vektörleri

Her şirket aşağıdaki testleri kendi dilinde otomatik test hâline getirmelidir:

| Hizmet | İstek | Beklenen iç sonuç |
|---|---|---|
| Topla | `{"sayilar":[]}` | `{"sonuc":0}` |
| Topla | `{"sayilar":[0.1,0.2,1.25]}` | `{"sonuc":1.55}` |
| Çarp | `{"sayilar":[2,3,4]}` | `{"sonuc":24}` |
| Çarp | `{"sayilar":[]}` | hata |
| Ortalama | `{"sayilar":[10,20,30]}` | `{"sonuc":20}` |
| Kelime say | `{"metin":"merhaba,dunya"}` | `{"sonuc":1}` |
| Kelime say | `{"metin":"a  b\nc\td"}` | `{"sonuc":4}` |
| Karakter say | `{"metin":"A😀B"}` | `{"sonuc":4}` |
| Medyan | `{"sayilar":[8,2,4,6]}` | `{"sonuc":5}` |
| Standart sapma | `{"sayilar":[2,4,4,4,5,5,7,9]}` | `{"sonuc":2}` |
| Sırala | `{"sayilar":[3,1,2],"yon":"artan"}` | `{"sonuc":[1,2,3]}` |
| Sırala | `{"sayilar":[3,1,2],"yon":"azalan"}` | `{"sonuc":[3,2,1]}` |
| Asal çarpan | `{"sayi":360}` | `{"sonuc":[2,2,2,3,3,5]}` |
| Frekans | `{"metin":"Tunix tunix motor"}` | `{"sonuc":{"motor":1,"tunix":2}}` |

## 15.1 Güvenlik testi

Normal hizmet alanlarına ek olarak:

```json
{
  "metin": "normal metin",
  "_guvenlikSinamasi": {
    "etiket": "motor-saldiri-v1",
    "tur": "kaynak-tuketimi",
    "yogunluk": 5
  },
  "_saldiriDolgusu": "XXXXX"
}
```

Beklenen dış cevap:

```json
{
  "basarili": false,
  "hataKodu": "GUVENLIK_REDDI",
  "sonucVerisiJson": "{}"
}
```

Hizmet handler’ının bu testte hiç çağrılmadığı ayrıca test edilmelidir.

---

## 16. Protokol testleri

Hizmet birim testleri tek başına yeterli değildir. Şunlar da test edilmelidir:

- [ ] Bir JSON mesajı iki TCP parçasına bölünerek gönderildiğinde okunuyor.
- [ ] İki JSON satırı tek TCP paketinde geldiğinde ikisi de işleniyor.
- [ ] Boş satır sunucuyu bozmuyor.
- [ ] Geçersiz JSON tüm süreci kapatmıyor.
- [ ] 65 KB civarı mesaj kontrollü okunuyor.
- [ ] Bilinmeyen mesaj türüne cevap gönderilmiyor.
- [ ] `finansDurumu` sonrasında TCP mesaj sırası bozulmuyor.
- [ ] Sağlık cevabı doğru `istekKimligi` içeriyor.
- [ ] İş cevabı doğru `istekKimligi` ve `isKimligi` içeriyor.
- [ ] Her gönderilen JSON tam olarak bir `\n` ile bitiyor.
- [ ] Bağlantı uzun süre açık kaldığında bellek sürekli büyümüyor.
- [ ] Motor kapanıp yeniden açıldığında şirket yeni bağlantı kabul ediyor.

---

## 17. Fiyatlandırma

Motor şirketin ilan ettiği `birimFiyat` değerini kullanır. Fiyatı her şirket kendi belirler.

Tunix’in mevcut referans tarifesi:

| Hizmet | Tunix fiyatı |
|---|---:|
| `matematik.topla` | `5 TL` |
| `matematik.carp` | `8 TL` |
| `veri.ortalama-hesapla` | `15 TL` |
| `metin.kelime-say` | `7 TL` |
| `metin.karakter-say` | `4 TL` |
| `veri.medyan-hesapla` | `22 TL` |
| `veri.standart-sapma` | `35 TL` |
| `dizi.sirala` | `28 TL` |
| `matematik.asal-carpanlar` | `45 TL` |
| `metin.frekans-analizi` | `32 TL` |

Bu fiyatların rakipler tarafından kopyalanması zorunlu değildir. Daha ucuz fiyat daha yüksek fiyat puanı sağlar; ancak toplam şirket seçiminde fiyatın ağırlığı yalnız `%10`dur. Kalite, performans ve güvenlik daha büyük etkiye sahiptir.

---

## 18. Canlı pano kontrolü

Motor bilgisayarında:

```text
http://localhost:8080/
```

Aynı yerel ağdaki cihazlarda motorun LAN IP adresi kullanılır:

```text
http://192.168.1.XX:8080/
```

Pano üzerinde kontrol edilecek alanlar:

- şirket bağlı mı,
- kaç hizmet ilan ediyor,
- ilan edilen hizmet adları doğru mu,
- fiyatlar doğru mu,
- kod kalitesi,
- performans,
- güvenlik,
- itibar,
- güvenilirlik,
- müşteri memnuniyeti,
- engellenen saldırı sayısı,
- başarılı saldırı sayısı,
- toplam güvenlik kaybı,
- son tick talebi,
- şüpheli iş sayısı,
- hizmet başına talep ve sağlayıcı sayısı.

Bir hizmetin sağlayıcı sayısı `0` görünüyorsa önce hizmet kimliği ve sürümü kontrol edilmelidir.

---

## 19. Çalıştırma sırası

Her bilgisayarda önce kendi şirket sunucusu açılmalıdır. Ardından motor çalıştırılabilir.

### Mudaf

```powershell
# Mudaf proje klasöründe
# kullanılan Go giriş dosyasına göre:
go test ./...
go run .
```

### Ugax

```powershell
# Ugax proje klasöründe
npm test
node src/index.js
```

### Motor

```powershell
cd C:\Users\TunahanDELİSALİHOĞLU\Desktop\motor\SirketSimulasyonu
dotnet run --project .\SirketMotoru\SirketMotoru.csproj
```

Motor başladıktan sonra konsolda her şirket için:

```text
Şirket motora bağlandı. Sunucu sürümü: ... | Hizmetler: ...
```

satırı görülmelidir.

---

## 20. Son teslim kontrol listesi

Her kardeş aşağıdaki listeyi eksiksiz işaretlemeden sunucusunu tamamlandı saymamalıdır.

### Protokol

- [ ] Ham TCP kullanılıyor.
- [ ] UTF-8 ve satır sonlandırmalı JSON kullanılıyor.
- [ ] TCP parçalanması doğru yönetiliyor.
- [ ] camelCase alan adları kullanılıyor.
- [ ] Bilinmeyen mesaj türleri yok sayılıyor.
- [ ] Finans mesajına cevap gönderilmiyor.
- [ ] Bağlantı açık tutuluyor.

### Kimlik ve tanıtım

- [ ] Şirket kimliği motor ayarıyla aynı.
- [ ] Sunucu sürümü artırılmış.
- [ ] Hizmet adları katalogla birebir aynı.
- [ ] Yalnız çalışan hizmetler ilan ediliyor.
- [ ] Fiyat ve kapasite değerleri geçerli.

### Hizmetler

- [ ] On hizmetin her biri için doğru request parser var.
- [ ] Her sonuç `{"sonuc":...}` biçiminde.
- [ ] `sonucVerisiJson` dış mesajda string.
- [ ] Boş dizi ve geçersiz veri kuralları uygulanıyor.
- [ ] Standart sapma popülasyon formülü kullanıyor.
- [ ] Karakter sayısı UTF-16 kod birimi sayıyor.
- [ ] Kelime ve frekans ayırıcıları yalnız dört whitespace karakteri.
- [ ] Asal çarpanlar tekrarlı ve sıralı.

### Güvenlik

- [ ] İç JSON hizmetten önce ayrıştırılıyor.
- [ ] `_guvenlikSinamasi.etiket` kontrol ediliyor.
- [ ] Saldırı isteğinde handler çalıştırılmıyor.
- [ ] `GUVENLIK_REDDI` gönderiliyor.
- [ ] Kimlikler güvenlik cevabında korunuyor.
- [ ] Büyük mesaj sınırı uygulanıyor.
- [ ] Tek hata tüm sunucuyu kapatmıyor.

### Performans

- [ ] İşlem süresi gerçekten ölçülüyor.
- [ ] Loglama aşırı değil.
- [ ] Büyük dizilerde uygun algoritma kullanılıyor.
- [ ] Bellek uzun çalışmada sürekli büyümüyor.
- [ ] Kuyruk ve kapasite doğru bildiriliyor.

### Test

- [ ] Bütün hizmet birim testleri geçiyor.
- [ ] Güvenlik testi geçiyor.
- [ ] TCP parçalanma testi geçiyor.
- [ ] Motorla gerçek entegrasyon testi geçiyor.
- [ ] Canlı panoda şirket ve hizmetler doğru görünüyor.

---

## 21. Tamamlanma tanımı

Bir şirket sunucusu yalnız şu koşullarda Kızışma Paketi v2 ile uyumlu sayılır:

1. Motor şirkete bağlanabiliyor.
2. Şirket doğru tanıtım mesajını gönderiyor.
3. İlan ettiği bütün hizmetler motor doğrulamasından geçiyor.
4. Sonuç biçimleri sözleşmeyle birebir uyuşuyor.
5. Normal yük altında zaman aşımına düşmüyor.
6. Kötü niyetli işleri özel güvenlik koduyla reddediyor.
7. Finans ve sağlık mesajları TCP sırasını bozmuyor.
8. Canlı panoda kalite, performans ve güvenlik puanları normal biçimde güncelleniyor.
9. Yanlış veya eksik hizmet ilanı bulunmuyor.
10. Kendi dilindeki otomatik test paketi eksiksiz geçiyor.

Bu sürümden sonra oyun yalnız “hizmeti yazdım” yarışması değildir. Gerçek rekabet; doğru algoritma, protokol disiplini, performans, güvenlik, kapasite, fiyatlandırma ve uzun süreli güvenilirlik üzerinden yürütülecektir.
