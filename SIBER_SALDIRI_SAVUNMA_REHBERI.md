# Siber Saldiri ve Sirket Sunucusu Savunma Rehberi

**Motor paketi:** Kizisma Paketi v2 ve kod tabanli yayin sistemi  
**Motor protokolu:** `0.1`  
**Kapsam:** Tunix/Rust, Mudaf/Go, Ugax/Node.js ve sonradan eklenecek tum sirket sunuculari

> Bu oyundaki saldirilar gercek virus, exploit veya zararli kod degildir. Motor, sirket sunucularinin guvenli istek dogrulama, kaynak sinirlama ve hata yonetimi davranislarini olcmek icin kontrollu JSON sinamalari gonderir.

---

## 1. Neden bazi sirketler surekli saldiri yiyor?

Motor kotu niyetli isi normal talep havuzunun icine yerlestirir. Saldiri istegi de gercek bir hizmet kimligi, musteri kimligi, butce, zaman asimi ve is kimligi tasir. Bu nedenle sunucu yalnizca `mesajTuru == isIstegi` kontrolu yaparsa saldiriyi normal is gibi kabul eder.

Bir sirketin saldiriyi yemesinin en yaygin nedenleri:

1. Guvenlik kontrolunun hizmet kodundan sonra calismasi.
2. `_guvenlikSinamasi` alaninin hic kontrol edilmemesi.
3. Alan kontrol edilse bile `etiket` degerinin dogrulanmamasi.
4. Saldirida `basarili: true` donulmesi.
5. Generic bir hata kodu donulmesi.
6. Sunucunun baglantiyi kapatmasi veya cokmesi.
7. Buyuk payload'in once tamamen kopyalanip sonra kontrol edilmesi.
8. Zaman asimina girilmesi.
9. Her hizmetin ayri ayri guvenlik kontrolu yapmasi ve bir hizmette kontrolun unutulmasi.
10. JSON ayrisirken olusan hatanin tum sunucu dongusunu sonlandirmasi.

Motor acisindan saldiri ancak acik bir guvenlik reddiyle engellenmis sayilir. Sessizce baglantiyi kapatmak, generic hata vermek veya normal hizmet sonucu dondurmek savunma sayilmaz.

---

## 2. Motor saldiriyi nasil olusturuyor?

Kotu niyetli talep, normal hizmet verisinin kokune iki alan eklenerek olusturulur:

```json
{
  "sayilar": [1, 2, 3],
  "_guvenlikSinamasi": {
    "etiket": "motor-saldiri-v1",
    "tur": "kaynak-tuketimi",
    "yogunluk": 4,
    "sahteYetki": "yonetici",
    "komut": "kaynaklari-tuket"
  },
  "_saldiriDolgusu": "XXXX..."
}
```

Kesin motor imzasi:

```text
_guvenlikSinamasi.etiket == "motor-saldiri-v1"
```

Mevcut kontrollu saldiri turleri:

| Tur | Oyundaki anlami | Sunucuda sinanan davranis |
|---|---|---|
| `kaynak-tuketimi` | Sunucuyu gereksiz hesaplamaya zorlama | Is mantigina girmeden reddetme |
| `buyuk-payload` | Mesaj ve bellek baskisi | Mesaj boyutu, alan boyutu ve kaynak limiti |
| `yetki-yukseltme-denemesi` | Sahte yonetici/yetki bilgisi | Istek verisine guvenmeme |
| `komut-enjeksiyonu` | Metin icinde komut benzeri veri | Veriyi komut gibi calistirmama ve erken reddetme |
| `tekrar-saldirisi` | Ayni veya benzer istegi tekrar kullanma | Is/istek kimligi ve tekrar kontrolu |

Motor su anda `500 + zorluk * 1500` karakterlik kontrollu dolgu ekler. Bu gercek bir saldiri araci degil, sunucunun gelen veriyi dogru sirada denetleyip denetlemedigini olcer.

---

## 3. Saldiri dalgalari ve olasiliklar

Motorun pazar dongusu 120 tick uzerinden ilerler.

Saldiri dalgasi araliklari:

```text
Tick dongusu 58-73
Tick dongusu 104-112
```

Normal donemde kotu niyetli is olasiligi:

```text
0.008 + zorluk * 0.002
```

Saldiri dalgasinda kotu niyetli is olasiligi:

```text
0.10 + zorluk * 0.015
```

Ornek:

| Zorluk | Normal donem | Saldiri dalgasi |
|---:|---:|---:|
| 1 | %1,0 | %11,5 |
| 3 | %1,4 | %14,5 |
| 5 | %1,8 | %17,5 |

Talep sayisi yuksek oldugu icin yuzde dusuk gorunse bile bir tickte birden fazla saldiri gelebilir. Kurumsal ve kamu musterilerinin ikinci talep olusturma olasiligi da daha yuksektir.

---

## 4. Saldirilar neden guvenligi dusuk sirkete daha cok gidiyor?

Normal islerde sirket secimi kalite, performans, guvenlik, fiyat, itibar, kapasite ve sadakate gore yapilir.

Kotu niyetli istekte secim mantigi tersine doner. Motor, adaylar arasinda daha savunmasiz gorunen sirkete daha fazla agirlik verir:

```text
acik puani = 101 - guvenlik puani
saldiri agirligi =
    acik puani * 0.75
  + (101 - kod kalitesi) * 0.20
  + kapasite puani * 0.05
```

Sonuc:

- Guvenlik puani dusen sirket daha fazla saldirinin hedefi olur.
- Daha fazla saldiri basarili olursa guvenlik daha da duser.
- Bu durum bir guvenlik borcu dongusu olusturur.
- Tek bir dogru duzeltme sonrasinda engellenen saldirilar guvenlik puanini yeniden toparlamaya baslar.

Bu nedenle saldiri savunmasi ertelenecek bir ozellik degildir. Sunucunun en ust katmaninda bulunmalidir.

---

## 5. Motorun kabul ettigi guvenlik reddi

Motor, `isSonucu` mesajinda `basarili` alaninin `false` olmasini ve hata kodunun su uc degerden biri olmasini bekler:

```text
GUVENLIK_REDDI
ISTEK_GUVENLI_DEGIL
KOTU_NIYETLI_ISTEK
```

Tavsiye edilen standart cevap:

```json
{
  "mesajTuru": "isSonucu",
  "istekKimligi": "istek-motordan-gelen-kimlik",
  "isKimligi": "is-motordan-gelen-kimlik",
  "sirketKimligi": "mustafa-mudaf",
  "basarili": false,
  "sonucVerisiJson": "{}",
  "hataKodu": "GUVENLIK_REDDI",
  "hataMesaji": "Motor imzali kotu niyetli istek guvenlik katmani tarafindan engellendi.",
  "islemSuresiMs": 0.18
}
```

Kritik alanlar:

- `istekKimligi`, gelen istekle birebir ayni olmali.
- `isKimligi`, gelen istekle birebir ayni olmali.
- `sirketKimligi`, sirketin kayitli kimligi olmali.
- `basarili`, kesinlikle `false` olmali.
- `hataKodu`, kabul edilen uc koddan biri olmali.
- Islem suresi gercek kronometreden alinmali.

### Savunma sayilmayan cevaplar

```json
{
  "basarili": false,
  "hataKodu": "HATA"
}
```

```json
{
  "basarili": true,
  "sonucVerisiJson": "{\"sonuc\":0}"
}
```

```text
Baglantiyi kapatmak
Cevap vermemek
Sunucuyu cokertmek
JSON yerine duz metin yazmak
```

Bunlar motor tarafindan saldirinin basarili olmasi veya sunucuyu yormasi olarak degerlendirilir.

---

## 6. Dogru savunma sirasi

Her `isIstegi` icin tavsiye edilen islem sirasi:

```text
1. TCP satir boyutunu kontrol et
2. Mesaj zarfi JSON'unu ayristir
3. Mesaj turunu ve temel kimlikleri kontrol et
4. istekVerisiJson alanini sinirli bicimde ayristir
5. Motor saldiri imzasini kontrol et
6. Saldiriysa guvenlik reddi dondur
7. Hizmet kimligi ve surumunu kontrol et
8. Hizmete ozel veri semasini dogrula
9. Boyut/adet/deger limitlerini kontrol et
10. Hizmet algoritmasini calistir
11. Gercek islem suresini olc
12. Standart isSonucu dondur
```

En kritik kural:

> Guvenlik sinamasi, hizmet algoritmasindan ve agir veri donusumlerinden once kontrol edilmelidir.

Yanlis sira:

```text
JSON -> 10.000 elemanli diziye cevir -> sirala -> saldiri mi diye bak
```

Dogru sira:

```text
JSON kokunu oku -> saldiri imzasini kontrol et -> gerekirse reddet -> sonra hizmet verisini isle
```

---

## 7. Merkezi guvenlik katmani

Guvenlik kontrolunu her hizmet dosyasina ayri ayri kopyalamayin. Bir hizmette unutulursa o hizmet savunmasiz kalir.

Tavsiye edilen mimari:

```text
TCP okuyucu
  -> mesaj boyutu kontrolu
  -> protokol ayrıştırıcı
  -> merkezi guvenlik kapisi
  -> hizmet yonlendirici
  -> hizmet algoritmasi
  -> sonuc yazici
```

Merkezi guvenlik kapisi en az sunlari yapmali:

- `_guvenlikSinamasi` kok alanini kontrol etme
- `etiket` degerini birebir dogrulama
- JSON kokunun nesne olup olmadigini kontrol etme
- Azami mesaj boyutu
- Azami metin uzunlugu
- Azami dizi elemani
- Azami JSON derinligi
- Hizmet bazli zaman limiti
- Is kimligi tekrar kontrolu
- Hata halinde sunucu ana dongusunu ayakta tutma

---

## 8. Rust icin referans savunma

Tunix'in mevcut ilk seviye savunmasi:

```rust
fn kotu_niyetli_istek_mi(istek_verisi_json: &str) -> bool {
    let Ok(deger) = serde_json::from_str::<serde_json::Value>(istek_verisi_json) else {
        return false;
    };

    let Some(kok) = deger.as_object() else {
        return false;
    };

    let Some(sinama) = kok
        .get("_guvenlikSinamasi")
        .and_then(|deger| deger.as_object())
    else {
        return false;
    };

    sinama
        .get("etiket")
        .and_then(|deger| deger.as_str())
        .is_some_and(|etiket| etiket == "motor-saldiri-v1")
}
```

Bu kontrol hizmet yonlendiriciden once calisir ve `GUVENLIK_REDDI` dondurur.

Daha guclu Rust surumunde ayrica:

- `serde_json::Value` olusturmadan once toplam satir boyutu kontrol edilmeli.
- Islenmis `isKimligi` degerleri sinirli bir cache'te tutulmali.
- Her hizmet icin dizi/metin limitleri bulunmali.
- Tek bir baglanti hatasi tum dinleyiciyi kapatmamalidir.
- Mutex zehirlenmesi ve panic sinirlari ele alinmalidir.

---

## 9. Go icin referans savunma

```go
type GuvenlikSinamasi struct {
    Etiket string `json:"etiket"`
    Tur    string `json:"tur"`
    Yogunluk int  `json:"yogunluk"`
}

type IstekKoku struct {
    GuvenlikSinamasi *GuvenlikSinamasi `json:"_guvenlikSinamasi"`
}

func kotuNiyetliMi(ham string) bool {
    if len(ham) > 65_536 {
        return true
    }

    var kok IstekKoku
    if err := json.Unmarshal([]byte(ham), &kok); err != nil {
        return false
    }

    return kok.GuvenlikSinamasi != nil &&
        kok.GuvenlikSinamasi.Etiket == "motor-saldiri-v1"
}
```

Is akisi:

```go
baslangic := time.Now()

if kotuNiyetliMi(istek.IstekVerisiJson) {
    sonuc := IsSonucu{
        MesajTuru: "isSonucu",
        IstekKimligi: istek.IstekKimligi,
        IsKimligi: istek.IsKimligi,
        SirketKimligi: sirketKimligi,
        Basarili: false,
        SonucVerisiJson: "{}",
        HataKodu: "GUVENLIK_REDDI",
        HataMesaji: "Kotu niyetli istek engellendi.",
        IslemSuresiMs: float64(time.Since(baslangic).Microseconds()) / 1000.0,
    }
    mesajGonder(sonuc)
    return
}
```

Go sunucusunda dikkat:

- `json.Decoder` kullaniliyorsa azami okuma `io.LimitReader` ile sinirlanmali.
- Her baglanti icin sonsuz goroutine acilmamali.
- Kuyruk ve aktif is sayisi atomik/lock korumali tutulmali.
- Panic bir connection handler icinde recover edilip ana sunucu korunmali.

---

## 10. Node.js icin referans savunma

```javascript
function kotuNiyetliMi(istekVerisiJson) {
  if (typeof istekVerisiJson !== "string") return false;
  if (Buffer.byteLength(istekVerisiJson, "utf8") > 65_536) return true;

  let kok;
  try {
    kok = JSON.parse(istekVerisiJson);
  } catch {
    return false;
  }

  return Boolean(
    kok &&
    typeof kok === "object" &&
    !Array.isArray(kok) &&
    kok._guvenlikSinamasi &&
    kok._guvenlikSinamasi.etiket === "motor-saldiri-v1"
  );
}
```

```javascript
const baslangic = process.hrtime.bigint();

if (kotuNiyetliMi(istek.istekVerisiJson)) {
  const sureMs = Number(process.hrtime.bigint() - baslangic) / 1_000_000;

  gonder({
    mesajTuru: "isSonucu",
    istekKimligi: istek.istekKimligi,
    isKimligi: istek.isKimligi,
    sirketKimligi: SIRKET_KIMLIGI,
    basarili: false,
    sonucVerisiJson: "{}",
    hataKodu: "GUVENLIK_REDDI",
    hataMesaji: "Kotu niyetli istek engellendi.",
    islemSuresiMs: sureMs
  });
  return;
}
```

Node.js sunucusunda dikkat:

- TCP verisinin tek `data` eventinde tam gelecegini varsaymayin.
- Satir tamponu azami boyutla sinirlayin.
- Senkron agir donguler event loop'u kilitler; buyuk isleri sinirlayin.
- Islem zaman asimini `AbortController`, worker veya kontrollu kuyrukla yonetin.
- JSON parse hatasi tum process'i sonlandirmamali.

---

## 11. Finansal ve puan etkileri

### Saldiri engellenirse

- `EngellenenSaldiriSayisi` artar.
- `ReddedilenIsSayisi` artar.
- Guvenlik puani 100 yonunde hareket eder.
- Performans, reddetme suresine gore guncellenir.
- Itibar ve guvenilirlik cok kucuk miktarda artar.
- Kasadan para kesilmez.

### Saldiri normal is gibi calistirilirsa

- `BasariliSaldiriSayisi` artar.
- `BasarisizIsSayisi` artar.
- `ToplamGuvenlikKaybi` artar.
- Olası kayip kasadan kesilir.
- Guvenlik puani `5 + zorluk * 2` kadar duser.
- Kod kalitesi `0.5 + zorluk * 0.4` kadar duser.
- Itibar ve guvenilirlik ciddi ceza alir.
- Musteri memnuniyeti sifir gozlemi alir.

### Saldiri zaman asimi olusturursa

Motor olasi guvenlik kaybini `1.25` ile carpar. Yani saldiriyi uzun sure calistirmak, hemen fark edememekten daha pahali olabilir.

Olası guvenlik kaybi:

```text
azami butce * 0.35 + zorluk * 25
alt sinir: 40 TL
ust sinir: 2500 TL
```

---

## 12. Yanlis pozitiften kacinma

Sunucu her bilinmeyen alani saldiri saymamalidir. Normal hizmet JSON'unda gelecekte yeni alanlar olabilir.

Yanlis yaklasim:

```text
Tanimadigim herhangi bir alan varsa GUVENLIK_REDDI
```

Dogru yaklasim:

```text
Motorun kesin imzasi varsa GUVENLIK_REDDI
Diger bilinmeyen alanlari hizmet semasina gore ele al
```

Mevcut kesin imza:

```text
_guvenlikSinamasi.etiket = motor-saldiri-v1
```

Ayrica hizmet girdisinde beklenmeyen ama saldiri imzasi olmayan alanlar icin `GECERSIZ_ISTEK` gibi normal dogrulama hatasi kullanilabilir. Bu hata, guvenlik reddi yerine gecmez.

---

## 13. Tekrar saldirisi savunmasi

Motor kendi tarafinda ayni `isKimligi` degerini iki kez islememeye calisir. Sirket sunucusu da kendi korumasini kurmalidir.

Tavsiye:

- Son 1.000-10.000 is kimligini sinirli LRU cache'te tutun.
- Ayni `isKimligi` tekrar gelirse hizmeti yeniden calistirmayin.
- Cok eski kimlikleri bellekten atin.
- Cache sinirsiz buyumemelidir.
- `istekKimligi` ve `isKimligi` birlikte kontrol edilebilir.

Ornek hata:

```json
{
  "basarili": false,
  "hataKodu": "KOTU_NIYETLI_ISTEK",
  "hataMesaji": "Tekrarlanan is kimligi reddedildi."
}
```

---

## 14. Boyut ve kaynak limitleri

Motorun genel mesaj siniri su anda 65.536 byte'tir. Sirket sunucusu da ayni veya daha dusuk bir sinir uygulamalidir.

Hizmet bazli tavsiye edilen ek limitler:

| Veri | Tavsiye edilen ilk limit |
|---|---:|
| Toplam TCP satiri | 65.536 byte |
| Metin | 50.000 UTF-8 byte |
| Sayi dizisi | 10.000 eleman |
| JSON derinligi | 16-32 |
| Tek alan adi | 128 karakter |
| Islem kuyrugu | Sunucu kapasitesine bagli sinirli |
| Is kimligi cache'i | 1.000-10.000 kayit |

Bu degerler motorun mevcut islerinden daha yuksek tutulabilir; amac normal isleri engellemeden kontrolsuz kaynak tuketimini onlemektir.

---

## 15. Sunucu cokmesini onleme

Her sirket sunucusu su hatalari baglanti seviyesinde yakalamalidir:

- Gecersiz JSON
- Eksik alan
- Yanlis veri turu
- Desteklenmeyen mesaj turu
- Desteklenmeyen hizmet/surum
- Cok buyuk mesaj
- Yazma hatasi
- Baglanti kopmasi
- Hizmet algoritmasi hatasi
- Zaman asimi

Bir istemcinin hatasi ana dinleyiciyi kapatmamalidir.

```text
Yanlis: handler hatasi -> process exit
Dogru: handler hatasi -> logla -> baglantiyi kapat -> listener calismaya devam etsin
```

---

## 16. Test plani

Her sunucu asagidaki testleri kendi dilinde otomatiklestirmelidir.

### A. Normal is testi

- Gecerli istek gonder.
- `basarili: true` bekle.
- Sonucu motor sozlesmesine gore dogrula.

### B. Imzali saldiri testi

- Normal istek kokune `_guvenlikSinamasi` ekle.
- `basarili: false` bekle.
- `hataKodu == GUVENLIK_REDDI` bekle.
- Hizmet algoritmasinin calismadigini sayac/log ile dogrula.

### C. Buyuk payload testi

- Mesaj limitinin biraz altini gonder: kontrollu cevap gelmeli.
- Mesaj limitinin ustunu gonder: baglanti guvenli kapatilmali.
- Ana sunucu sonraki baglantiyi kabul etmeli.

### D. Gecersiz JSON testi

- Yarim JSON gonder.
- Process cokmemeli.
- Sonraki normal is calismali.

### E. Tekrar testi

- Ayni `isKimligi` ile iki istek gonder.
- Ikincisi reddedilmeli veya idempotent davranmali.

### F. Zaman testi

- Guvenlik reddi hizmet algoritmasindan once olmali.
- Red suresi normal agir hizmetten belirgin bicimde kisa olmali.

---

## 17. Konsol ve borsa belirtileri

Motor loglarinda:

```text
SALDIRI ENGELLENDI
SALDIRI BASARILI
SALDIRI SUNUCUYU YORDU
```

ifadelerini arayin.

Canli borsa ve sirket kayitlarinda izlenecek alanlar:

- Guvenlik puani
- Engellenen saldiri sayisi
- Basarili saldiri sayisi
- Toplam guvenlik kaybi
- Kod kalitesi
- Guvenilirlik
- Kasa ve toplam ceza
- Zaman asimi sayisi

Bir sunucuda `BasariliSaldiriSayisi` artiyorsa cevap kodunu, kontrol sirasini ve zaman asimini hemen inceleyin.

---

## 18. Hizli teshis tablosu

| Belirti | Muhtemel neden | Cozum |
|---|---|---|
| Saldiri normal is gibi tamamlandi | Imza kontrolu yok veya gec calisiyor | Merkezi erken guvenlik kapisi |
| Generic hata donuyor ama saldiri basarili | Hata kodu kabul edilmiyor | `GUVENLIK_REDDI` kullan |
| Sunucu baglantisi kopuyor | Exception ana handler'dan kaciyor | Baglanti seviyesinde hata yakala |
| Saldiri zaman asimina giriyor | Once agir hizmet kodu calisiyor | Guvenlik kontrolunu one al |
| Normal isler de reddediliyor | Yalniz alan varligini kontrol ediyorsun | Kesin `etiket` degerini kontrol et |
| Guvenlik puani toparlanmiyor | Reddetme cevabi semasi yanlis | Kimlikleri ve `basarili:false` alanini kontrol et |
| Node sunucu donuyor | Senkron agir is event loop'u kapatiyor | Kuyruk/worker ve limit kullan |
| Go RAM artiyor | Sinirsiz goroutine veya cache | Semaphore ve sinirli LRU |
| Rust listener kapaniyor | Handler hatasi yukariya yayiliyor | Her baglantiyi ayri yonet, hatayi logla |

---

## 19. Asgari teslim kontrol listesi

Her sirket sunucusu icin:

- [ ] TCP satir tamponu sinirli.
- [ ] Azami mesaj boyutu kontrol ediliyor.
- [ ] JSON parse hatasi process'i kapatmiyor.
- [ ] Guvenlik kontrolu hizmetten once calisiyor.
- [ ] `_guvenlikSinamasi.etiket` birebir kontrol ediliyor.
- [ ] `basarili: false` donduruluyor.
- [ ] `hataKodu: GUVENLIK_REDDI` donduruluyor.
- [ ] Istek ve is kimlikleri korunuyor.
- [ ] Gercek islem suresi olculuyor.
- [ ] Hizmet bazli boyut limitleri var.
- [ ] Tekrar is kimligi kontrolu var.
- [ ] Sunucu hatadan sonra yeni baglanti kabul ediyor.
- [ ] Birim ve entegrasyon testleri var.
- [ ] Motor logunda `SALDIRI ENGELLENDI` goruluyor.
- [ ] Canli borsada basarili saldiri sayisi artmiyor.

---

## 20. Onemli not

Mevcut `motor-saldiri-v1` imzasi oyunun ilk guvenlik katmanidir. Gelecek motor surumlerinde acik imza tasimayan kontrollu sinamalar, tekrar denemeleri, boyut/derinlik testleri, hatali protokol sirasi ve oran sinirlama testleri eklenebilir.

Bu nedenle yalniz imza kontrolune guvenip diger kaynak limitlerini ihmal etmeyin. Saglam sunucu:

```text
imza kontrolu
+ veri semasi
+ boyut limiti
+ tekrar korumasi
+ zaman limiti
+ kuyruk limiti
+ hata izolasyonu
```

katmanlarinin birlikte uygulanmasiyla olusur.
