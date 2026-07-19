# Kod Tabanlı Uygulama ve Özel Protokol Manifesti v1

**Motor protokolü:** `0.1`  
**Yönetim kapısı:** `8090`  
**Temel ilke:** Kod, uygulama özellikleri ve protokoller şirket sunucusunda yazılır. `8090` hiçbir kod, hizmet veya uygulama özelliği üretmez; yalnız teknik olarak ilan edilen yayınların ticari yönetimini yapar.

---

## 1. Kesin iş akışı

1. Oyuncu uygulamayı veya hizmeti kendi şirket sunucusunda kodlar.
2. Uygulamanın her özelliği şirket sunucusunda gerçek bir hizmet kimliğine bağlanır.
3. Sunucu bağlantı kurulurken `sirketTanitim` mesajında:
   - hizmetleri,
   - uygulama manifestlerini,
   - özel protokol manifestlerini
   motora gönderir.
4. Motor uygulama kategorisinin zorunlu özelliklerini denetler.
5. Motor her özellikte belirtilen hizmetin gerçekten sunucu tarafından ilan edildiğini kontrol eder.
6. Zorunlu başka uygulama/protokol bağlantıları varsa bunlar doğrulanır.
7. Teknik doğrulamayı geçen uygulama `8090` ekranında **Sunucuda kodlandı** durumuyla görünür.
8. Oyuncu `8090` üzerinden yalnız ticari kararları verir:
   - piyasaya açma,
   - abonelik/kullanım/lisans fiyatı,
   - aktif veya pasif durum,
   - kapasite satın alma,
   - altyapı yatırımı,
   - kredi,
   - SLA sözleşmesi.
9. Uygulamanın kodu veya zorunlu hizmetlerinden biri sunucudan kaldırılırsa motor uygulamayı otomatik durdurur.

---

## 2. `sirketTanitim` mesajının yeni yapısı

Eski sunucular `uygulamalar` ve `ozelProtokoller` alanlarını göndermeden çalışmaya devam eder. Yeni uygulama veya protokol yayınlamak isteyen sunucular bu alanları eklemelidir.

```json
{
  "mesajTuru": "sirketTanitim",
  "mesajKimligi": "tanitim-001",
  "protokolSurumu": "0.1",
  "sirketKimligi": "mustafa-mudaf",
  "sirketAdi": "Mudaf",
  "sunucuSurumu": "0.8.0",
  "hizmetler": [],
  "uygulamalar": [],
  "ozelProtokoller": []
}
```

---

## 3. Uygulama manifesti

Bir uygulama yalnız isimden oluşmaz. Uygulama, birden fazla gerçek hizmetin standart özellik kimlikleri altında bir araya gelmesidir.

```json
{
  "uygulamaKimligi": "mudaf-mail",
  "uygulamaAdi": "Mudaf Mail",
  "surum": "1.0",
  "kategori": "eposta",
  "aciklama": "Kurumsal ve bireysel e-posta platformu.",
  "ozellikler": [
    {
      "ozellikKimligi": "kimlik.dogrula",
      "hizmetKimligi": "mudaf.kimlik.dogrula",
      "hizmetSurumu": "1.0",
      "aciklama": "Kullanıcı oturumu ve hesap doğrulama.",
      "zorunlu": true
    },
    {
      "ozellikKimligi": "eposta.gonder",
      "hizmetKimligi": "mudaf.eposta.gonder",
      "hizmetSurumu": "1.0",
      "aciklama": "E-posta gönderme işlemi.",
      "zorunlu": true
    },
    {
      "ozellikKimligi": "eposta.gelen-kutusu",
      "hizmetKimligi": "mudaf.eposta.gelen-kutusu",
      "hizmetSurumu": "1.0",
      "zorunlu": true
    },
    {
      "ozellikKimligi": "eposta.ara",
      "hizmetKimligi": "mudaf.eposta.ara",
      "hizmetSurumu": "1.0",
      "zorunlu": true
    },
    {
      "ozellikKimligi": "eposta.spam-kontrol",
      "hizmetKimligi": "mudaf.eposta.spam-kontrol",
      "hizmetSurumu": "1.0",
      "zorunlu": true
    },
    {
      "ozellikKimligi": "eposta.ek-yukle",
      "hizmetKimligi": "mudaf.eposta.ek-yukle",
      "hizmetSurumu": "1.0",
      "zorunlu": false
    }
  ],
  "bagimliliklar": [],
  "desteklenenProtokoller": [
    "mudaf-mail-protocol"
  ]
}
```

### Alanlar

| Alan | Açıklama |
|---|---|
| `uygulamaKimligi` | Şirket içinde ve motor piyasasında kalıcı teknik uygulama kimliği |
| `uygulamaAdi` | Kullanıcıya gösterilen ürün adı |
| `surum` | Uygulamanın teknik sürümü |
| `kategori` | Motorun uyguladığı standart kategorisi |
| `ozellikler` | Uygulamanın gerçek hizmetlere bağlı yetenekleri |
| `bagimliliklar` | Başka şirket veya uygulamalara teknik bağlantılar |
| `desteklenenProtokoller` | Uygulamanın kullandığı özel protokol kimlikleri |

`uygulamaKimligi` aynı uygulamanın yeni sürümlerinde değişmemelidir.

---

## 4. Özellik–hizmet bağlantısı

Her uygulama özelliği gerçek bir hizmete bağlanmalıdır:

```json
{
  "ozellikKimligi": "sosyal.gonderi.olustur",
  "hizmetKimligi": "tunix.social.post.create",
  "hizmetSurumu": "1.0",
  "aciklama": "Yeni gönderi oluşturur.",
  "zorunlu": true
}
```

Aynı hizmet şu `hizmetler` listesinde de bulunmalıdır:

```json
{
  "hizmetKimligi": "tunix.social.post.create",
  "hizmetSurumu": "1.0",
  "birimFiyat": 3.5,
  "azamiEszamanliIs": 4,
  "aktif": true
}
```

Hizmet listesinde bulunmayan bir özellik kodlanmış kabul edilmez.

---

## 5. Sosyal medya uygulaması standardı

### Zorunlu özellikler

- `kimlik.dogrula`
- `sosyal.profil.getir`
- `sosyal.gonderi.olustur`
- `sosyal.akisi.getir`
- `sosyal.etkilesim`

### Opsiyonel özellikler

- `sosyal.yorum`
- `sosyal.mesajlasma`
- `sosyal.moderasyon`
- `sosyal.bildirim`
- `sosyal.arama`

Yalnız `sosyal.gonderi.olustur` yazıp tam sosyal medya uygulaması ilan edilemez. Zorunlu özelliklerin tamamı ayrı ayrı gerçek hizmet kodlarına bağlı olmalıdır.

---

## 6. E-posta uygulaması standardı

### Zorunlu özellikler

- `kimlik.dogrula`
- `eposta.gonder`
- `eposta.gelen-kutusu`
- `eposta.ara`
- `eposta.spam-kontrol`

### Opsiyonel özellikler

- `eposta.ek-yukle`
- `eposta.klasor`
- `eposta.filtre`
- `eposta.takvim-baglantisi`

---

## 7. Mesajlaşma uygulaması standardı

### Zorunlu özellikler

- `kimlik.dogrula`
- `mesaj.gonder`
- `mesaj.sohbet-getir`
- `mesaj.teslim-durumu`
- `mesaj.kullanici-durumu`

### Opsiyonel özellikler

- `mesaj.grup`
- `mesaj.dosya`
- `mesaj.arama`
- `mesaj.sifreleme`

---

## 8. Bulut depolama standardı

### Zorunlu özellikler

- `kimlik.dogrula`
- `dosya.yukle`
- `dosya.indir`
- `dosya.listele`
- `dosya.sil`

### Opsiyonel özellikler

- `dosya.paylas`
- `dosya.surumle`
- `dosya.ara`
- `dosya.yedekle`

---

## 9. Uygulamalar arası bağlantılar

Bir uygulama başka bir şirketteki uygulamaya bağlanabilir:

```json
{
  "sirketKimligi": "ugur-ugax",
  "uygulamaKimligi": "ugax-messaging",
  "asgariSurum": "1.2",
  "protokolKimligi": "ugax-realtime-protocol",
  "zorunlu": true
}
```

Örnek kullanım:

- Sosyal medya uygulamasının Ugax mesajlaşma uygulamasını özel mesaj sistemi olarak kullanması
- E-ticaret uygulamasının başka şirketin ödeme veya e-posta hizmetine bağlanması
- Analitik ürününün sosyal medya uygulamasından veri alması

Zorunlu bağlantı yoksa uygulama standardı geçmez veya mevcut yayın motor tarafından durdurulur. Opsiyonel bağlantı yoksa uygulama çalışabilir fakat ilgili özellik kullanılamaz.

---

## 10. Özel protokol manifesti

Özel protokol de `8090` ekranında isim yazılarak oluşturulmaz. Protokol şirket sunucusunda uygulanmalı ve gerçek hizmet yetkinliklerine bağlanmalıdır.

```json
{
  "protokolKimligi": "tunix-social-protocol",
  "protokolAdi": "Tunix Social Protocol",
  "surum": "1.0",
  "aciklama": "Sosyal uygulamalar arası gönderi ve profil veri değişimi.",
  "semaKimligi": "tsp-v1-json",
  "semaOzeti": "sha256:ORNEK-SEMA-OZETI",
  "yetkinlikler": [
    {
      "yetkinlikKimligi": "profil-getir",
      "hizmetKimligi": "tunix.social.profile.get",
      "hizmetSurumu": "1.0"
    },
    {
      "yetkinlikKimligi": "gonderi-yayinla",
      "hizmetKimligi": "tunix.social.post.create",
      "hizmetSurumu": "1.0"
    }
  ],
  "uyumluProtokoller": [
    "ugax-realtime-protocol"
  ]
}
```

Motor bütün `yetkinlikler` içindeki hizmetlerin gerçekten şirket sunucusunda ilan edilmesini zorunlu tutar.

---

## 11. `8090` ekranının yetkileri

### Yapabilir

- Sunucunun ilan ettiği hizmetleri görüntülemek
- Hizmet fiyatını değiştirmek
- Hizmeti ticari olarak aktif/pasif yapmak
- Kodlanmış ve standardı geçen uygulamayı piyasaya açmak
- Uygulamanın abonelik/kullanım/lisans ücretini belirlemek
- Uygulamayı durdurmak veya yeniden yayına almak
- Uygulama kullanıcı kapasitesi satın almak
- Kodlanmış özel protokolü piyasaya açmak
- Başka protokolü benimsemek
- CPU, RAM, ağ, depolama, yedekleme ve departman yatırımları yapmak
- Bankadan kredi çekmek
- SLA sözleşmesi kabul etmek
- Finans, abone, şirket değeri ve işlem geçmişini izlemek

### Yapamaz

- Yeni hizmet kodu üretmek
- Yeni uygulama özelliği eklemek
- Sunucuda olmayan hizmeti ilan etmek
- Uygulama kategorisinin zorunlu özelliklerini otomatik tamamlamak
- Özel protokolün teknik yetkinliklerini üretmek
- Yanlış algoritmayı doğru hâle getirmek
- Sunucu kodunu değiştirmek

---

## 12. Pasife alma davranışı

Bir hizmet `8090` ekranından pasife alınırsa:

- hizmet yeni standart iş alamaz,
- bu hizmeti zorunlu özellik olarak kullanan uygulama teknik olarak eksik duruma düşer,
- aktif uygulama motor tarafından otomatik durdurulur,
- abonelik ve kullanıcı büyümesi kesilir,
- sözleşme ihlali oluşabilir.

Hizmet yeniden aktifleştirildiğinde uygulama otomatik açılmaz. Oyuncu teknik durumu kontrol ettikten sonra uygulamayı `8090` ekranından yeniden yayına almalıdır.

---

## 13. Örnek tam `sirketTanitim`

```json
{
  "mesajTuru": "sirketTanitim",
  "mesajKimligi": "mudaf-tanitim-101",
  "protokolSurumu": "0.1",
  "sirketKimligi": "mustafa-mudaf",
  "sirketAdi": "Mudaf",
  "sunucuSurumu": "0.8.0",
  "hizmetler": [
    {
      "hizmetKimligi": "mudaf.kimlik.dogrula",
      "hizmetSurumu": "1.0",
      "birimFiyat": 2,
      "azamiEszamanliIs": 3,
      "aktif": true
    },
    {
      "hizmetKimligi": "mudaf.eposta.gonder",
      "hizmetSurumu": "1.0",
      "birimFiyat": 4,
      "azamiEszamanliIs": 3,
      "aktif": true
    },
    {
      "hizmetKimligi": "mudaf.eposta.gelen-kutusu",
      "hizmetSurumu": "1.0",
      "birimFiyat": 3,
      "azamiEszamanliIs": 3,
      "aktif": true
    },
    {
      "hizmetKimligi": "mudaf.eposta.ara",
      "hizmetSurumu": "1.0",
      "birimFiyat": 5,
      "azamiEszamanliIs": 2,
      "aktif": true
    },
    {
      "hizmetKimligi": "mudaf.eposta.spam-kontrol",
      "hizmetSurumu": "1.0",
      "birimFiyat": 6,
      "azamiEszamanliIs": 2,
      "aktif": true
    }
  ],
  "uygulamalar": [
    {
      "uygulamaKimligi": "mudaf-mail",
      "uygulamaAdi": "Mudaf Mail",
      "surum": "1.0",
      "kategori": "eposta",
      "aciklama": "Mudaf tarafından Go ile yazılmış e-posta platformu.",
      "ozellikler": [
        {"ozellikKimligi":"kimlik.dogrula","hizmetKimligi":"mudaf.kimlik.dogrula","hizmetSurumu":"1.0","zorunlu":true},
        {"ozellikKimligi":"eposta.gonder","hizmetKimligi":"mudaf.eposta.gonder","hizmetSurumu":"1.0","zorunlu":true},
        {"ozellikKimligi":"eposta.gelen-kutusu","hizmetKimligi":"mudaf.eposta.gelen-kutusu","hizmetSurumu":"1.0","zorunlu":true},
        {"ozellikKimligi":"eposta.ara","hizmetKimligi":"mudaf.eposta.ara","hizmetSurumu":"1.0","zorunlu":true},
        {"ozellikKimligi":"eposta.spam-kontrol","hizmetKimligi":"mudaf.eposta.spam-kontrol","hizmetSurumu":"1.0","zorunlu":true}
      ],
      "bagimliliklar": [],
      "desteklenenProtokoller": ["mudaf-mail-protocol"]
    }
  ],
  "ozelProtokoller": [
    {
      "protokolKimligi": "mudaf-mail-protocol",
      "protokolAdi": "Mudaf Mail Protocol",
      "surum": "1.0",
      "aciklama": "E-posta istemci ve servis entegrasyonu.",
      "semaKimligi": "mmp-json-v1",
      "semaOzeti": "sha256:ORNEK",
      "yetkinlikler": [
        {"yetkinlikKimligi":"mail-send","hizmetKimligi":"mudaf.eposta.gonder","hizmetSurumu":"1.0"},
        {"yetkinlikKimligi":"mail-search","hizmetKimligi":"mudaf.eposta.ara","hizmetSurumu":"1.0"}
      ],
      "uyumluProtokoller": []
    }
  ]
}
```

Bu manifest yalnız teknik ilanı yapar. Mudaf Mail'in fiyatlandırılması, kapasitesi, krediyle büyütülmesi ve aktif/pasif yönetimi `8090` ekranından yapılır.
