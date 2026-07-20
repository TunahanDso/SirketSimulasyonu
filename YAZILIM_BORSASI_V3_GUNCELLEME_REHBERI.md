# Yazilim Borsasi v3 Kapsamli Guncelleme Rehberi

**Proje:** Uc Kardes Yazilim Sirketi Simulasyonu  
**Dal:** `agent/tunix-matematik-topla`  
**Motor:** C# / .NET 8  
**Sirketler:** Tunix-Rust, Mudaf-Go, Ugax-Node.js

Bu belge, Kizisma Paketi v2 ile kod tabanli sirket isletim/yayin guncellemelerinin tek kaynak ozetidir.

---

## 1. Oyunun yeni ana fikri

Oyun artik yalnizca su donguden ibaret degildir:

```text
musteri istek gonderir -> sirket cevap verir -> para kazanir
```

Yeni ana dongu:

```text
Oyuncu kod yazar
-> sunucu hizmet/uygulama/protokol ilan eder
-> motor teknik gercegi dogrular
-> oyuncu 8090'dan ticari karar verir
-> musteriler kalite/fiyat/performansa gore secim yapar
-> altyapi, kredi, abonelik, SLA ve saldiri ekonomisi calisir
-> sirket degeri ve piyasa konumu degisir
```

Kodlama ve iscilik oyuncuda kalir. Motor ve 8090, kodun yerine gecmez.

---

## 2. Surumde uygulanan temel degisiklikler

### Musteri sistemi

- Merkezi musteri veritabani hedefi: 5.000 kayit
- Mevcut kayitlar korunarak eksik sayi tamamlanir
- Musteriler kalici kimlik ve davranis verileri tasir
- Talep donemleri ve pazar donguleri bulunur
- Bazi musteri turleri ayni tickte ikinci talep olusturabilir

### Hizmet katalogu

Toplam 10 temel motor hizmeti:

```text
matematik.topla
matematik.carp
veri.ortalama-hesapla
metin.kelime-say
metin.karakter-say
veri.medyan-hesapla
veri.standart-sapma
dizi.sirala
matematik.asal-carpanlar
metin.frekans-analizi
```

### Sirket puanlari

- Kod kalitesi
- Performans
- Guvenlik
- Itibar
- Guvenilirlik
- Musteri memnuniyeti
- Kapasite ve hiz

### Guvenlik

- Kontrollu kotu niyetli isler
- Saldiri dalgalari
- Guvenlik reddi protokolu
- Finansal kayip
- Guvenlik ve kalite cezalari
- Savunmasiz sirketlere daha yuksek hedef agirligi

### Sirket isletimi

- 8090 sirket girisi
- Hizmet fiyat ve aktiflik yonetimi
- Altyapi yatirimlari
- Kredi sistemi
- Abonelik/kullanim/lisans ekonomisi
- SLA sozlesmeleri
- Piyasa olaylari
- Sirket degerlemesi

### Kod tabanli yayin

- Uygulama manifestleri
- Kategori standartlari
- Cok ozellikli uygulamalar
- Uygulamalar arasi bagimliliklar
- Ozel protokol manifestleri
- Panelden kodsuz urun/protokol uretiminin kapatilmasi

---

## 3. 5.000 musterilik pazar

Motor ilk acilista musteri veritabanini yukler. Kayit sayisi hedefin altindaysa mevcut musterileri silmeden yeni kalici kayitlar olusturur.

Musteri turleri:

- Bireysel
- Kucuk isletme
- Orta olcekli isletme
- Kurumsal
- Kamu kurumu

Talep davranisi musteri turune gore degisir.

Ikinci talep olasiliklari:

| Musteri turu | Ikinci talep |
|---|---:|
| Bireysel | %3 |
| Kucuk isletme | %10 |
| Orta olcekli | %18 |
| Kurumsal | %30 |
| Kamu | %22 |

Pazar dongusu 120 tick boyunca farkli talep carpanlari uygular. Yogun donemde talep carpaninin 1,80 seviyesine cikmasi mumkundur.

Sonuc:

- Kapasitesi dusuk sirketler is kacirabilir.
- Dusuk fiyatli sirket asiri yuk altinda kalabilir.
- Sunucu performansi ve kuyruk yonetimi ekonomik karar haline gelir.

---

## 4. Yeni zor hizmetler

### `veri.medyan-hesapla`

- Buyuk ondalik diziler
- Tek/cift eleman sayisi
- Siralama ve hassasiyet

### `veri.standart-sapma`

- Populasyon standart sapmasi
- Buyuk veri dizileri
- Ondalik hassasiyeti

### `dizi.sirala`

- Artan veya azalan yon
- Yuzlerce eleman
- Negatif ve tekrarli sayilar

### `matematik.asal-carpanlar`

- Tekrarli asal carpanlar
- Carpim dogrulamasi
- Gercek asallik kontrolu

### `metin.frekans-analizi`

- Buyuk metin
- Kelime frekans haritasi
- Anahtar/degerlerin tamamini dogrulama

Motor sonucu kendisi hesaplar; sirketin bildirimine guvenmez.

Ayrintili istek/sonuc semalari:

```text
HIZMET_SOZLESMELERI_V2.md
SUNUCU_GUNCELLEME_V2_REHBERI.md
```

---

## 5. Musteri sirket secimi

Normal is secim agirliklari:

| Olcut | Agirlik |
|---|---:|
| Kod kalitesi | %25 |
| Performans | %15 |
| Gercek hiz | %5 |
| Guvenilirlik | %13 |
| Guvenlik | %12 |
| Fiyat | %10 |
| Itibar | %10 |
| Kapasite | %5 |
| Sadakat | %5 |

Bu sistemde en ucuz sirket otomatik kazanmaz.

Ornek:

```text
Sirket A: 5 TL, kalite 30, guvenlik 20
Sirket B: 8 TL, kalite 85, guvenlik 90
```

Bir cok musteri icin Sirket B daha uygun aday olabilir.

Kotu niyetli islerde ise secim savunmasiz sirketlere dogru agirliklanir.

---

## 6. Puanlarin zorlasmasi

Basarili islerin itibar ve guvenilirlik artisi cok kucuktur. Binlerce kolay is yaparak puanlari hizla 100'e cikarmak zorlastirilmistir.

Dogru zor is:

- Kod kalitesini yavasca yukari tasir.
- Gercek sure performansi etkiler.
- Musteri memnuniyetine katkida bulunur.

Yanlis sonuc:

- Kod kalitesini basaridan daha hizli dusurur.
- Itibar ve guvenilirligi azaltir.
- Islem ucretine bagli ceza olusturabilir.

Zaman asimi:

- Performansi sert dusurur.
- Guvenilirlik ve itibari azaltir.
- Musteri memnuniyetini sifir gozlemine ceker.

Puanlar her tick cok hafif bicimde 50 merkezine dogru asinabilir. Bu nedenle eski basari sonsuza kadar sirketi tasimaz.

---

## 7. Kontrollu siber saldirilar

Motorun mevcut saldiri turleri:

```text
kaynak-tuketimi
buyuk-payload
yetki-yukseltme-denemesi
komut-enjeksiyonu
tekrar-saldirisi
```

Motor imzasi:

```text
_guvenlikSinamasi.etiket = motor-saldiri-v1
```

Kabul edilen red kodlari:

```text
GUVENLIK_REDDI
ISTEK_GUVENLI_DEGIL
KOTU_NIYETLI_ISTEK
```

Saldiri engellenmezse:

- Kasa kaybi
- Guvenlik puani kaybi
- Kod kalitesi kaybi
- Itibar ve guvenilirlik cezasi
- Basarili saldiri sayaci
- Musteri memnuniyeti kaybi

Tam uygulama rehberi:

```text
SIBER_SALDIRI_SAVUNMA_REHBERI.md
```

---

## 8. 8090 Sirket Isletim Merkezi

Canli borsa:

```text
http://MOTOR_IP:8080/
```

Sirket yonetimi:

```text
http://MOTOR_IP:8090/
```

8090 uzerinden:

- Giris/parola
- Hizmet fiyati
- Hizmet aktif/pasif
- Kodlanmis uygulama yayini
- Uygulama fiyati ve aktifligi
- Uygulama kapasitesi
- Altyapi yatirimi
- Kredi
- Kodlanmis protokol yayini
- Protokol benimseme
- SLA kabul etme

8090 uzerinden yapilamayanlar:

- Hizmet algoritmasi yazma
- Uygulama ozelligi ekleme
- Protokol semasi yazma
- Kod kalitesini satin alma
- Sunucuda olmayan uygulamayi uretme

Tam rehber:

```text
SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md
```

---

## 9. Kod tabanli uygulamalar

Yeni `sirketTanitim` yapisi:

```json
{
  "mesajTuru": "sirketTanitim",
  "hizmetler": [],
  "uygulamalar": [],
  "ozelProtokoller": []
}
```

Uygulama:

- Kalici teknik kimlik
- Ad ve surum
- Kategori
- Gercek hizmetlere bagli ozellikler
- Zorunlu/opsiyonel ozellikler
- Diger uygulama bagimliliklari
- Desteklenen protokoller

alanlarini tasir.

Motor, uygulamayi piyasaya acmadan once:

- Sunucu baglantisi
- Manifest kaydi
- Kategori standardi
- Zorunlu ozellikler
- Hizmet kimligi/surumu
- Bagimliliklar
- Protokol baglari

kontrollerini yapar.

Manifest rehberi:

```text
UYGULAMA_MANIFESTI_V1.md
```

---

## 10. Mevcut uygulama standartlari

| Kategori | Zorunlu ozellik sayisi |
|---|---:|
| Sosyal medya | 5 |
| E-posta | 5 |
| Mesajlasma | 5 |
| Bulut depolama | 5 |
| E-ticaret | 5 |
| API urunu | 4 |
| Analitik | 4 |
| Guvenlik | 4 |
| Gelistirici araci | 3 |

Standartlar isim kontrolunden ibaret degildir. Her standart ozellik gercek bir `hizmetKimligi@surum` kaydina baglanmalidir.

Ornek:

```text
Ozellik: eposta.gonder
Gercek hizmet: mudaf.eposta.gonder@1.0
```

---

## 11. Uygulamalar arasi ekosistem

Uygulamalar baska uygulamalara baglanabilir.

Ornekler:

```text
Tunix Social -> Ugax Messaging
Mudaf Commerce -> Tunix Mail
Ugax Analytics -> Tunix Social
```

Bagimlilik kaydi:

- Hedef sirket
- Hedef uygulama
- Asgari surum
- Kullanilan protokol
- Zorunlu/opsiyonel

Zorunlu bagimlilik kaybolursa uygulama dogrulamasi veya ticari yayin etkilenir.

Bu sistem sirketlerin:

- Kendi ekosistemini kurmasina
- Rakip hizmete bagimli kalmasina
- Ortaklik yapmasina
- Protokolunu piyasaya dayatmasina

zemin hazirlar.

---

## 12. Ozel protokoller

Protokol kodu ve semasi sirket sunucusunda olusturulur.

Manifestte:

- Protokol kimligi
- Ad
- Surum
- Sema kimligi
- Sema ozeti
- Yetkinlikler
- Yetkinliklerin hizmet baglari
- Uyumlu protokoller

bulunur.

8090 yalniz:

- Acik/lisans/abonelik modelini
- Benimseme bedelini
- Tick lisans bedelini
- Piyasa aktifligini

 yonetir.

---

## 13. Abonelik ve urun ekonomisi

Kodlanmis uygulama piyasaya acildiktan sonra motor:

- Yeni kullanici talebi
- Aktif kullanici
- Kullanici kapasitesi
- Abone kaybi
- Memnuniyet
- Abonelik geliri
- Kullanim geliri
- Kullanici basina gider
- Sunucu baglantisi
- Arka uc hizmet varligi
- Pazarlama ve destek yatirimi

uzerinden ekonomik simulasyon yapar.

Fiyatlandirma modelleri:

```text
abonelik
kullanim
freemium
lisans
```

Kod veya zorunlu arka uc kaybolursa uygulama gelir kaybeder ve musteri kaybi yasayabilir.

---

## 14. Altyapi yatirimlari

Yatirimlar:

```text
cpu
ram
ag
depolama
guvenlik
yedek
destek
pazarlama
satis
arge
```

Ekonomik ilke:

> Para kodun yerine gecmez. Para, dogru yazilmis kodun daha fazla yuk ve musteri tasimasini saglar.

Ornek:

- CPU: eszamanli is kapasitesi
- RAM: hizmet ve urun kapasitesi
- Ag: baglanti ve kullanici kapasitesi
- Depolama: e-posta/bulut/sosyal kapasitesi
- Yedek: kesinti ve SLA dayanimi
- Destek: abone kaybi
- Pazarlama: kullanici kazanimi
- Satis: sozlesme kabiliyeti

Maliyet seviye karesiyle artar.

---

## 15. Banka sistemi

Kredi turleri:

- Isletme
- Altyapi
- Ar-Ge
- Acil likidite

Kredi karari:

- Kredi notu
- Paket alt/ust siniri
- Mevcut borc
- Sirket degeri
- Kasa

uzerinden verilir.

Taksitler ticklerde otomatik kesilir. Odenemeyen taksit:

- Borcu buyutur
- Kredi notunu dusurur
- Temerrut sayisini arttirir
- Guvenilirligi azaltir

Kredi, asiri riskli buyumeyi mumkun kilar; yanlis kullanilirsa sirketi borc dongusune sokar.

---

## 16. SLA sozlesmeleri

Motor periyodik sozlesme teklifleri olusturabilir.

Kosullar:

- Asgari kod kalitesi
- Asgari performans
- Asgari guvenlik
- Gerekli kapasite
- Sure
- Tick odemesi
- Ihlal cezasi

Basarili tickte duzenli gelir; ihlalde ceza ve puan kaybi olur.

SLA, sirketlerin yalniz anlik is degil uzun vadeli hizmet guvencesi vermesini saglar.

---

## 17. Piyasa olaylari

Mevcut olay ornekleri:

- Dijital donusum dalgasi
- Siber guvenlik panigi
- Bulut maliyeti artisi
- Sosyal medya patlamasi
- Ekonomik durgunluk
- E-posta gecis sezonu

Olaylar:

- Kategori talebini
- Kullanici kazanimi
- Isletme giderini

gecici olarak degistirebilir.

---

## 18. Sirket degeri

Degerlemede kullanilan ana unsurlar:

```text
kasa
+ duzenli gelir
+ aktif kullanici
+ aktif urun
+ aktif sozlesme
+ kod kalitesi
+ performans
+ guvenlik
+ itibar
+ ekosistem
- borc
- odenemeyen gider
```

Tahmini hisse fiyati su anda sirket degerinden uretilen gosterge niteligindedir.

Bu surumde tam oyuncular arasi hisse alis-satis borsasi tamamlanmis degildir.

---

## 19. Kalici veri mimarisi

### `musteriler.json`

Musteri kimlikleri, bakiyeler, davranis ve islem gecmisi.

### `sirket-bilancolari.json`

Sirket kasasi, puanlari, is ve saldiri istatistikleri.

### `sirket-isletim.json`

Krediler, yatirimlar, urun/abonelik ekonomisi, sozlesmeler ve piyasa olaylari.

### `kod-tabanli-yayinlar.json`

Hizmet aktiflik ayarlari, uygulama-urun ve protokol-piyasa eslemeleri.

### `hizmet-katalogu.json`

Motorun bagimsiz dogruladigi temel is hizmetleri.

### `motor-ayarlari.json`

Portlar, zaman asimlari, sirket IP/portlari ve genel motor ayarlari.

---

## 20. Geriye uyumluluk

Eski sunucu yalniz:

```json
{
  "hizmetler": []
}
```

ile calismaya devam edebilir.

Yeni uygulama veya protokol sunmak isteyen sunucu:

```json
{
  "hizmetler": [],
  "uygulamalar": [],
  "ozelProtokoller": []
}
```

alanlarini eklemelidir.

Alanlar gonderilmezse:

- Mevcut temel hizmetler calisir.
- 8090 hizmet fiyat/aktiflik islemleri kullanilabilir.
- Yeni uygulama/protokol piyasaya sunulamaz.

---

## 21. Guncelleme gecis sirasi

### Motor sahibi

```powershell
git pull --ff-only origin agent/tunix-matematik-topla
dotnet build .\SirketMotoru\SirketMotoru.csproj
dotnet run --project .\SirketMotoru\SirketMotoru.csproj
```

Beklenen portlar:

```text
8080 Canli borsa
8090 Sirket Isletim Merkezi
```

### Sirket sahipleri

1. Yeni rehberleri oku.
2. Guvenlik reddini uygula.
3. Mevcut 10 hizmeti test et.
4. Uygulama/protokol yapacaksa manifest modellerini ekle.
5. Sunucu surumunu arttir.
6. Sunucuyu yeniden baslat.
7. Motor tanitim kaydini kontrol et.
8. 8090'a girip fiyat ve aktiflikleri kontrol et.

---

## 22. Uctan uca test plani

### Motor

- [ ] `dotnet build` basarili.
- [ ] 5.000 musteri yukleniyor.
- [ ] Katalog 10 hizmet gosteriyor.
- [ ] 8080 aciliyor.
- [ ] 8090 aciliyor.
- [ ] Bilanco ve isletim dosyalari yaziliyor.

### Her sirket

- [ ] Motor baglantisi kuruyor.
- [ ] Saglik kontrolune cevap veriyor.
- [ ] 10 hizmetten ilan ettikleri dogru calisiyor.
- [ ] Sonuclar motor dogrulamasini geciyor.
- [ ] Saldiri isteginde `GUVENLIK_REDDI` donuyor.
- [ ] Zaman asimi veya process cokmesi yok.
- [ ] 8090'da yalniz kendi sirketini goruyor.
- [ ] Fiyat degisikligi yeni islerde kullaniliyor.
- [ ] Pasif hizmet is almiyor.

### Uygulama

- [ ] Uygulama sunucu manifestinde.
- [ ] Zorunlu ozellikler tam.
- [ ] Her ozellik gercek hizmete bagli.
- [ ] Piyasaya acma standardi geciyor.
- [ ] Fiyat ve kapasite guncelleniyor.
- [ ] Zorunlu hizmet kapaninca yayin etkileniyor.

### Finans

- [ ] Yatirim kasadan dusuyor.
- [ ] Kredi kasaya geciyor.
- [ ] Taksitler tickte kesiliyor.
- [ ] Odenemeyen taksit kredi notunu dusuruyor.
- [ ] SLA odeme/cezasi calisiyor.

---

## 23. Mevcut sinirlar ve tamamlanmamis buyuk sistemler

Asagidakiler tasarim hedefidir; bu rehber yazildigi anda tam oyuncu sistemi olarak tamamlanmis kabul edilmemelidir:

- Gercek oyuncular arasi hisse alis-satisi
- Temettu ve ortaklik yonetimi
- Sirket satin alma/birlesme
- Tam iflas, haciz ve yeniden yapilandirma
- Calisan transferi ve departman simulasyonu
- Oyuncularin teklif verdigi gelismis ihale sistemi
- Konsorsiyumlar
- Medya/haber aciklamasi ve manipulasyon denetimi
- Imzasiz gelismis guvenlik sinamalari
- Tum yeni uygulama ozelliklerinin motor tarafindan islevsel uctan uca test edilmesi
- Ozel protokollerin semantik uyumluluk testleri

Bu maddeler mevcut temel omurga uzerine sonraki paketlerde eklenebilir.

---

## 24. Ilgili belgeler

```text
SUNUCU_GUNCELLEME_V2_REHBERI.md
HIZMET_SOZLESMELERI_V2.md
UYGULAMA_MANIFESTI_V1.md
SIBER_SALDIRI_SAVUNMA_REHBERI.md
SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md
```

---

## 25. Ana oyun ilkeleri

1. Hazir sunucu sistemi yok; herkes kendi sunucusunu yazar.
2. Kod kalitesi gercek sonuclarla olculur.
3. Performans gercek sureyle olculur.
4. Guvenlik hata koduyla degil davranisla kazanilir.
5. Para kod satin almaz; kapasite ve isletme imkani satin alir.
6. Uygulama tek isim degil, birden fazla kodlanmis ozelliktir.
7. Ozel protokol yalniz ilan degil, gercek hizmet yetkinliklerine baglidir.
8. 8090 yonetimdir; gelistirme degildir.
9. Verilen sozler SLA ile ekonomik sorumluluga donusur.
10. En cok hizmet yazan degil, en iyi yazilim sirketini kuran kazanir.
