# Sirket Isletim Merkezi 8090 Tam Rehberi

**Adres:** `http://MOTOR_IP:8090/`  
**Yerel adres:** `http://localhost:8090/`  
**Amac:** Sirket sunucusunda kodlanan teknik varliklarin ticari ve finansal yonetimi

> 8090 bir kodlama ekrani, uygulama olusturucu veya hazir servis magazasi degildir. Hizmet, uygulama ozelligi ve ozel protokol oyuncunun kendi sirket sunucusunda kodlanir. Panel yalnizca motorun teknik olarak gordugu bu varliklari yonetir.

---

## 1. Temel ayrim

Oyunda iki ayri alan vardir:

### A. Oyuncunun sirket sunucusu

Oyuncu burada kendi diliyle gercek kod yazar:

- Rust / Tunix
- Go / Mudaf
- Node.js / Ugax
- Sonradan oyuna eklenecek diger diller

Sunucunun sorumluluklari:

- Ham TCP sunucusu
- Motor protokolu
- Hizmet algoritmalari
- Uygulama ozellikleri
- Veri saklama ve kendi uygulama mantigi
- Guvenlik kontrolleri
- Performans ve kuyruk yonetimi
- Ozel protokol implementasyonu
- Uygulamalar arasi baglantilar
- `sirketTanitim` manifesti

### B. 8090 Sirket Isletim Merkezi

Oyuncu burada teknik varligin ticari kararlarini yonetir:

- Hizmet fiyatini degistirme
- Hizmeti ticari olarak aktif/pasif yapma
- Kodlanmis uygulamayi piyasaya acma
- Uygulama abonelik/kullanim/lisans fiyatini ayarlama
- Uygulamayi durdurma veya yeniden acma
- Kullanici kapasitesi satin alma
- CPU, RAM, ag, depolama ve diger altyapi yatirimlari
- Kredi cekme
- Borc ve kredi notunu izleme
- SLA sozlesmesi kabul etme
- Kodlanmis ozel protokolu piyasaya acma
- Baska sirket protokolunu lisanslama
- Gelir, gider, abone, sirket degeri ve islem gecmisini izleme

---

## 2. Kesin kural: panelden yazilim uretilemez

Panel su islemleri yapamaz:

```text
Yeni hizmet kodu yazmak
Yeni uygulama ozelligi eklemek
Sosyal medya akisi algoritmasi olusturmak
E-posta gonderme kodu olusturmak
Veritabani yazmak
Yeni protokol semasi uretmek
Eksik zorunlu ozelligi otomatik tamamlamak
Kod kalitesini para ile dogrudan yukseltmek
Yanlis algoritmayi dogru hale getirmek
```

Bir uygulama panelde gorunebilmek icin once sirket sunucusunun `sirketTanitim` mesajinda bulunmalidir.

```json
{
  "mesajTuru": "sirketTanitim",
  "hizmetler": [],
  "uygulamalar": [],
  "ozelProtokoller": []
}
```

Panelde serbest metinle uygulama adi/kategori girerek urun olusturma yolu yoktur. Piyasaya acma istegi yalnizca sunucudan gelen kalici `uygulamaKimligi` ile yapilir.

---

## 3. Giris ve oturum

Her sirket yalniz kendi hesabina girer. Motor ilk calismada eksik hesaplari olusturur ve gecici giris bilgilerini konsola yazar.

Ilk giristen sonra parola degistirilmelidir.

Oturum davranisi:

- Cookie adi: `sirketOturumu`
- `HttpOnly`
- `SameSite=Strict`
- Varsayilan oturum suresi: 180 dakika
- Motor ayarlarinda 10-1440 dakika arasinda sinirlanir
- Cikis yapildiginda cookie silinir

Bu yerel ag oyunu icin tasarlanmistir. Portu internete dogrudan acmayin.

---

## 4. Panel bolumleri

### Genel durum

Gosterilen temel degerler:

- Sirket adi ve kimligi
- Baglanti durumu
- Kasa
- Net gelir
- Kod kalitesi
- Performans
- Guvenlik
- Itibar
- Guvenilirlik
- Musteri memnuniyeti
- Tamamlanan/basarisiz is
- Kredi notu
- Toplam borc
- Toplam abone
- Sirket degeri
- Tahmini hisse fiyati

### Altyapi

Sirket kazandigi para veya krediyle yatirim yapabilir.

### Banka

Kredi paketleri, borc ve taksitler yonetilir.

### Hizmetler

Sunucunun ilan ettigi hizmetler gorulur. Fiyat ve ticari aktiflik degistirilebilir.

### Uygulamalar

Sunucuda kodlanan uygulamalar ve motor standart durumu gorulur. Standardi gecen uygulama piyasaya acilabilir.

### Protokoller

Sunucuda kodlanan ozel protokoller piyasaya sunulur; diger sirketlerin protokolleri lisanslanabilir.

### Sozlesmeler

Motorun olusturdugu SLA teklifleri gorulur ve kosullari saglayan sirket tarafindan kabul edilir.

### Islem gecmisi

Yatirim, kredi, fiyat, yayin, kapasite ve sozlesme islemleri gorulur.

---

## 5. Hizmet yonetimi

Bir hizmet once sirket sunucusunda kodlanir ve `hizmetler` listesinde ilan edilir:

```json
{
  "hizmetKimligi": "mudaf.eposta.gonder",
  "hizmetSurumu": "1.0",
  "birimFiyat": 12,
  "azamiEszamanliIs": 3,
  "aktif": true
}
```

8090 uzerinden:

- Birim fiyat degistirilebilir.
- Hizmet aktif/pasif yapilabilir.
- Sunucu yeniden baglandiginda paneldeki ticari ezme degerleri yeniden uygulanir.

### Fiyat guncelleme ucu

```text
POST /api/hizmet/fiyat
```

```json
{
  "hizmetKimligi": "mudaf.eposta.gonder",
  "hizmetSurumu": "1.0",
  "yeniFiyat": 18.5
}
```

### Aktif/pasif ucu

```text
POST /api/hizmet/durum
```

```json
{
  "hizmetKimligi": "mudaf.eposta.gonder",
  "hizmetSurumu": "1.0",
  "aktif": false
}
```

Pasif hizmet:

- Yeni normal is almaz.
- Uygulamanin zorunlu ozelligiyse ilgili uygulamanin ticari yayini durabilir.
- Fiyat tablosunda tutulabilir fakat musteri secimine girmez.

Panel yalniz sirketin son tanitim mesajinda bulunan hizmeti yonetebilir.

---

## 6. Kod tabanli uygulama yayini

Uygulama, birden fazla gercek hizmetin tek bir standart altinda birlesmesidir.

Ornek sosyal medya manifesti:

```json
{
  "uygulamaKimligi": "tunix-social",
  "uygulamaAdi": "Tunix Social",
  "surum": "1.0",
  "kategori": "sosyal-medya",
  "ozellikler": [
    {
      "ozellikKimligi": "kimlik.dogrula",
      "hizmetKimligi": "tunix.kimlik.dogrula",
      "hizmetSurumu": "1.0",
      "zorunlu": true
    },
    {
      "ozellikKimligi": "sosyal.gonderi.olustur",
      "hizmetKimligi": "tunix.sosyal.gonderi.olustur",
      "hizmetSurumu": "1.0",
      "zorunlu": true
    }
  ],
  "bagimliliklar": [],
  "desteklenenProtokoller": []
}
```

Motor piyasaya acmadan once:

1. Sirket sunucusunun bagli olmasini ister.
2. Uygulama kimligini sunucu manifestinde arar.
3. Kategori standardini bulur.
4. Zorunlu ozellik kimliklerini kontrol eder.
5. Her ozelligin baglandigi hizmeti sirket hizmet listesinde arar.
6. Hizmet surumunu kontrol eder.
7. Zorunlu uygulama bagimliliklarini kontrol eder.
8. Teknik dogrulama gecerliyse ticari urun kaydi acar.

### Piyasaya acma ucu

```text
POST /api/uygulama/yayinla
```

```json
{
  "uygulamaKimligi": "tunix-social",
  "fiyatlandirmaModeli": "abonelik",
  "abonelikUcreti": 24.9,
  "kullanimBasinaUcret": 0
}
```

Desteklenen fiyatlandirma modelleri:

```text
abonelik
kullanim
freemium
lisans
```

Panel uygulama adini, kategorisini veya ozelliklerini uretmez; bunlari sunucu manifestinden alir.

---

## 7. Uygulama aktifligi ve fiyat yonetimi

Piyasaya acilmis uygulama icin:

```text
POST /api/uygulama/guncelle
```

```json
{
  "urunKimligi": "urun-...",
  "abonelikUcreti": 29.9,
  "kullanimBasinaUcret": 0.02,
  "aktif": true
}
```

Bu islem:

- Uygulama kodunu degistirmez.
- Ozellik eklemez veya cikarmaz.
- Yalniz fiyat ve ticari aktiflik bilgisini degistirir.

Sunucu manifestinde zorunlu bir ozellik kaybolursa motor uygulamayi ticari olarak durdurabilir. Oyuncu eksik kodu yeniden sunucuda yazip ilan etmeden panelden zorla acamaz.

---

## 8. Uygulama kapasitesi

```text
POST /api/uygulama/kapasite
```

```json
{
  "urunKimligi": "urun-...",
  "eklenecekKapasite": 1000
}
```

Kapasite yalniz kod tabanli manifest ile piyasaya acilmis uygulamalara satin alinabilir.

Kapasite artisi:

- Daha fazla aktif kullanici tasimaya izin verir.
- Kasa harcar.
- Uygulama kod kalitesini arttirmaz.
- Sunucu kapaliysa hizmet veremez.
- Altyapi veya uygulama mimarisi kotuyse performans sorunlarini otomatik cozmez.

Kategoriye gore kapasite maliyeti farklidir. Bulut depolama ve sosyal medya, kullanici basina daha pahali kapasite tuketir.

---

## 9. Uygulama standartlari

Motorun mevcut standart kategorileri:

- `sosyal-medya`
- `eposta`
- `mesajlasma`
- `bulut-depolama`
- `e-ticaret`
- `api`
- `analitik`
- `guvenlik`
- `gelistirici-araci`

Her kategori zorunlu ve opsiyonel ozelliklere sahiptir.

### Sosyal medya zorunlu

```text
kimlik.dogrula
sosyal.profil.getir
sosyal.gonderi.olustur
sosyal.akisi.getir
sosyal.etkilesim
```

### E-posta zorunlu

```text
kimlik.dogrula
eposta.gonder
eposta.gelen-kutusu
eposta.ara
eposta.spam-kontrol
```

### Mesajlasma zorunlu

```text
kimlik.dogrula
mesaj.gonder
mesaj.sohbet-getir
mesaj.teslim-durumu
mesaj.kullanici-durumu
```

### Bulut depolama zorunlu

```text
kimlik.dogrula
dosya.yukle
dosya.indir
dosya.listele
dosya.sil
```

### E-ticaret zorunlu

```text
kimlik.dogrula
urun.listele
sepet.guncelle
siparis.olustur
odeme.dogrula
```

Butun standartlar ve manifest ornekleri icin:

```text
UYGULAMA_MANIFESTI_V1.md
```

---

## 10. Uygulamalar arasi baglantilar

Bir uygulama baska bir sirket uygulamasina baglanabilir:

```json
{
  "sirketKimligi": "ugur-ugax",
  "uygulamaKimligi": "ugax-messaging",
  "asgariSurum": "1.2",
  "protokolKimligi": "ugax-realtime-protocol",
  "zorunlu": true
}
```

Kullanim alanlari:

- Sosyal medya uygulamasinin baska mesajlasma sistemini kullanmasi
- E-ticaret uygulamasinin baska e-posta sisteminden bildirim gondermesi
- Analitik uygulamasinin baska platformdan veri almasi
- Bir sirket protokolunun piyasada standart haline gelmesi

Zorunlu bagimlilik bulunmazsa veya calismiyorsa uygulama standardi gecmeyebilir ya da yayini durabilir.

---

## 11. Ozel protokol yayini

Ozel protokol de panelde yazilmaz. Sirket sunucusu protokol manifestini ilan eder:

```json
{
  "protokolKimligi": "tunix-event-v1",
  "protokolAdi": "Tunix Event Protocol",
  "surum": "1.0",
  "semaKimligi": "tunix-event-schema",
  "semaOzeti": "sha256-...",
  "yetkinlikler": [
    {
      "yetkinlikKimligi": "event.yayinla",
      "hizmetKimligi": "tunix.event.yayinla",
      "hizmetSurumu": "1.0"
    }
  ]
}
```

Motor:

- Protokolu sunucu manifestinde arar.
- Yetkinliklerin gercek hizmetlere bagli oldugunu kontrol eder.
- Sonra 8090 uzerinden lisans modelinin belirlenmesine izin verir.

### Protokol piyasaya acma

```text
POST /api/protokol/yayinla
```

```json
{
  "protokolKimligi": "tunix-event-v1",
  "surum": "1.0",
  "lisansModeli": "abonelik",
  "benimsemeBedeli": 2500,
  "tickLisansBedeli": 75
}
```

Panel protokol semasini veya yetkinliklerini olusturmaz.

---

## 12. Altyapi yatirimlari

Mevcut yatirim turleri:

| Tur | Etki | Azami seviye | Taban maliyet |
|---|---|---:|---:|
| `cpu` | Hizmet kapasitesi | 20 | 2.500 TL |
| `ram` | Hizmet ve urun kapasitesi | 20 | 2.000 TL |
| `ag` | Eszamanli is ve kullanici kapasitesi | 20 | 2.250 TL |
| `depolama` | E-posta, sosyal medya ve bulut kapasitesi | 20 | 1.500 TL |
| `guvenlik` | Guvenlik olaylarinin ekonomik etkisi | 10 | 3.500 TL |
| `yedek` | Kesinti ve SLA dayanimi | 5 | 8.000 TL |
| `destek` | Abone kaybini azaltma | 10 | 3.000 TL |
| `pazarlama` | Yeni kullanici kazanimi | 10 | 4.000 TL |
| `satis` | Sozlesme ve degerleme | 10 | 4.500 TL |
| `arge` | Yeni urun baslangic kosullari | 10 | 5.000 TL |

Seviye maliyeti:

```text
taban maliyet * (mevcut seviye + 1)^2
```

Yatirimlar yanlis kodu duzeltmez. Yalniz dogru yazilmis sistemin daha fazla yuk tasimasina veya ekonomik zarari azaltmasina yardim eder.

---

## 13. Banka ve kredi sistemi

### Isletme kredisi

```text
Tutar: 1.000-50.000 TL
Tick faiz orani: %0,8
Taksit: 12
Odeme araligi: 5 tick
Asgari kredi notu: 450
```

### Altyapi kredisi

```text
Tutar: 5.000-150.000 TL
Tick faiz orani: %0,6
Taksit: 16
Odeme araligi: 5 tick
Asgari kredi notu: 550
```

### Ar-Ge kredisi

```text
Tutar: 10.000-250.000 TL
Tick faiz orani: %0,5
Taksit: 20
Odeme araligi: 5 tick
Asgari kredi notu: 650
```

### Acil likidite

```text
Tutar: 500-20.000 TL
Tick faiz orani: %1,5
Taksit: 8
Odeme araligi: 3 tick
Asgari kredi notu: 300
```

Kredi limiti sirket degeri, kasa ve mevcut borca gore hesaplanir.

Taksit odenirse:

- Kasa azalir.
- Kalan borc azalir.
- Kredi notu yavasca artar.

Taksit odenemezse:

- Gecikme sayisi artar.
- Borca ek faiz uygulanir.
- Temerrut sayisi artar.
- Kredi notu ciddi duser.
- Guvenilirlik zarar gorur.

---

## 14. SLA sozlesmeleri

Motor belirli araliklarla sozlesme teklifleri olusturur.

Tekliflerde:

- Kategori
- Sure
- Tick odemesi
- Ihlal cezasi
- Asgari kalite
- Asgari performans
- Asgari guvenlik
- Gerekli kapasite
- Son kabul ticki

bulunur.

Kabul ucu:

```text
POST /api/sozlesme/kabul
```

```json
{
  "teklifKimligi": "teklif-..."
}
```

Sirket kosullari saglamiyorsa motor kabul etmez.

Sozlesme aktifken her tick:

- Kosullar saglaniyorsa odeme gelir.
- Kosullar saglanmiyorsa ihlal cezasi kesilir.
- Kalite, performans, guvenlik ve kapasite birlikte degerlendirilir.
- Fazla ihlal sozlesmeyi sonlandirabilir.

---

## 15. Kalici veri dosyalari

MotorVerileri klasorunde:

### `sirket-bilancolari.json`

- Kasa
- Gelir
- Ceza
- Guvenlik kaybi
- Puanlar
- Is sayilari

### `sirket-isletim.json`

- Hesaplar
- Yatirimlar
- Krediler
- Urun ekonomisi
- Protokoller
- Sozlesmeler
- Piyasa olaylari
- Islem gecmisi

### `kod-tabanli-yayinlar.json`

- Hizmet aktiflik ezmeleri
- Uygulama manifesti ile ticari urun eslemeleri
- Protokol manifesti ile piyasa kaydi eslemeleri

Bu dosyalari oyun sirasinda elle degistirmeyin. Motor atomik/gecici dosya mantigiyla kaydetmeye calisir.

---

## 16. HTTP uclari

### Oturum

```text
POST /api/giris
POST /api/cikis
POST /api/parola
```

### Durum

```text
GET /api/saglik
GET /api/durum
```

### Finans ve altyapi

```text
POST /api/yatirim
POST /api/kredi
```

### Hizmetler

```text
POST /api/hizmet/fiyat
POST /api/hizmet/durum
```

### Kod tabanli uygulamalar

```text
POST /api/uygulama/yayinla
POST /api/uygulama/guncelle
POST /api/uygulama/kapasite
```

### Kod tabanli protokoller

```text
POST /api/protokol/yayinla
POST /api/protokol/benimse
```

### Sozlesme

```text
POST /api/sozlesme/kabul
```

Eski serbest olusturma uclari kullanilmaz:

```text
/api/urun/olustur
/api/protokol/olustur
```

---

## 17. Tipik oyuncu akisi

### Yeni hizmet

```text
1. Kendi sunucunda hizmeti kodla.
2. Testlerini yaz.
3. sirketTanitim.hizmetler listesine ekle.
4. Sunucuyu yeniden baslat/bagla.
5. Motorun hizmeti gordugunu kontrol et.
6. 8090'dan fiyatini belirle.
7. Gerekirse kapasite/altypai yatirimi yap.
8. Hizmeti ticari aktif yap.
```

### Yeni uygulama

```text
1. Uygulamanin butun zorunlu ozelliklerini kodla.
2. Her ozelligi gercek bir hizmet kimligine bagla.
3. Uygulama manifestini sirketTanitim'a ekle.
4. Bagimliliklari ve protokolleri belirt.
5. Sunucuyu motora yeniden tanit.
6. 8090'da standart durumunu kontrol et.
7. Fiyatlandirma modelini sec.
8. Piyasaya ac.
9. Kapasite, abone ve memnuniyeti izle.
```

### Yeni ozel protokol

```text
1. Protokol semasini ve isleyicilerini sunucuda kodla.
2. Yetkinlikleri gercek hizmetlere bagla.
3. Protokol manifestini ilan et.
4. Motor dogrulamasini gec.
5. 8090'dan lisans modelini belirle.
6. Piyasaya ac.
```

---

## 18. Guvenlik uyarilari

- 8090'i internete acmayin.
- Gecici parolalari degistirin.
- `MotorVerileri` klasorunu oyun disinda paylasmayin.
- Sirket hesaplari birbirine verilmemelidir.
- Panelde yapilan her islem kasa ve oyun sonucunu etkiler.
- Tarayici konsolundan API cagrisi yapmak kurallari atlatmaz; sunucu tarafinda sirket kimligi ve manifest dogrulamasi vardir.
- Kodlanmamis uygulama veya protokol, dogrudan HTTP istegiyle piyasaya acilamaz.

---

## 19. Sorun giderme

| Belirti | Kontrol |
|---|---|
| Uygulama panelde gorunmuyor | `sirketTanitim.uygulamalar` alanini kontrol et |
| Uygulama standardi gecmiyor | Zorunlu ozellik kimliklerini birebir kontrol et |
| Ozellik var ama eksik sayiliyor | Bagli hizmet kimligi/surumu `hizmetler` listesinde mi? |
| Piyasaya ac butonu hata veriyor | Sunucu bagli mi, manifest guncel mi? |
| Hizmet tekrar aktif oluyor | Sunucu tanitimi ve 8090 ezme degerini kontrol et |
| Uygulama otomatik durdu | Zorunlu hizmet veya bagimlilik kaybolmus olabilir |
| Kredi reddediliyor | Kredi notu, paket siniri ve toplam borc limiti |
| Taksitler kasayi eritiyor | Tick araligini ve aktif kredileri kontrol et |
| Abone artmiyor | Fiyat, kapasite, baglanti, kalite, performans, guvenlik |
| 8090 acilmiyor | `sirketYonetimAktif`, port 8090 ve firewall |

---

## 20. Teslim kontrol listesi

- [ ] Sunucu hizmetleri koddan ilan ediyor.
- [ ] Uygulama ozellikleri gercek hizmetlere bagli.
- [ ] Zorunlu ozellik adlari standartla birebir ayni.
- [ ] Uygulama kimligi kalici ve benzersiz.
- [ ] Ozel protokol yetkinlikleri gercek hizmetlere bagli.
- [ ] 8090'dan yalniz ticari karar veriliyor.
- [ ] Hizmet fiyatlari kontrol edildi.
- [ ] Gereksiz hizmetler pasife alindi.
- [ ] Uygulama fiyatlandirmasi zarar ettirmiyor.
- [ ] Kapasite aktif kullanicidan yuksek.
- [ ] Kredilerin taksit tarihleri biliniyor.
- [ ] SLA kabul edilmeden kapasite ve puanlar kontrol edildi.
- [ ] Gecici parola degistirildi.
- [ ] 8090 yalniz yerel agda erisilebilir.
