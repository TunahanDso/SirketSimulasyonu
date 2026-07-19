# İlos Tech — Python Şirket Sunucusu

İlos Tech, İlayda adına oyuna eklenen dördüncü yazılım şirketidir.

- Şirket kimliği: `ilayda-ilos-tech`
- Dil: Python 3.11+
- Port: `7004`
- Sunucu: yalnız Python standart kütüphanesi
- Temel motor hizmeti: 10/10
- Uygulamalar: **İSosyal**, **İMail**
- Özel protokol: **İLink**

## Çalıştırma

Repo kökünden:

```powershell
cd .\Sirketler\Ilayda-Python\IlosTech
python -m unittest -v
python .\server.py
```

veya:

```powershell
.\run.ps1
```

Motor aynı bilgisayarda `127.0.0.1:7004` adresine bağlanır.

## Mimari

Sunucu ham TCP, UTF-8 ve satır sonlandırmalı JSON kullanır. Her bağlantı ayrı bir thread içinde yönetilir. Ağ mesajı, iş zarfı, güvenlik imzası, tekrar kimliği ve hizmet verisi birbirinden ayrı katmanlarda kontrol edilir.

Şu güvenlikler aktiftir:

- 65.536 byte mesaj sınırı
- merkezi saldırı imzası kontrolü
- `GUVENLIK_REDDI` cevabı
- son 5.000 iş için tekrar koruması
- dizi ve metin sınırları
- NaN/sonsuz sayı reddi
- bağlantı hatasının ana sunucuyu kapatmaması
- gerçek işlem süresi

## Temel hizmet fiyatları

İlos Tech pazara düşük fiyatla girer:

| Hizmet | Fiyat |
|---|---:|
| matematik.topla | 2 TL |
| matematik.carp | 3 TL |
| veri.ortalama-hesapla | 4 TL |
| metin.kelime-say | 2 TL |
| metin.karakter-say | 2 TL |
| veri.medyan-hesapla | 5 TL |
| veri.standart-sapma | 6 TL |
| dizi.sirala | 5 TL |
| matematik.asal-carpanlar | 7 TL |
| metin.frekans-analizi | 5 TL |

Fiyatların düşük olması doğru kod zorunluluğunu kaldırmaz. Motor yanlış sonucu bağımsız olarak tespit eder.

## İSosyal

Kategori: `sosyal-medya`

Zorunlu özellikler:

- `kimlik.dogrula` → `ilos.kimlik.dogrula`
- `sosyal.profil.getir` → `ilos.isosyal.profil.getir`
- `sosyal.gonderi.olustur` → `ilos.isosyal.gonderi.olustur`
- `sosyal.akisi.getir` → `ilos.isosyal.akisi.getir`
- `sosyal.etkilesim` → `ilos.isosyal.etkilesim`

Opsiyonel özellikler:

- yorum
- arama
- İMail bildirim bağlantısı

## İMail

Kategori: `eposta`

Zorunlu özellikler:

- kimlik doğrulama
- e-posta gönderme
- gelen kutusu
- arama
- spam kontrolü

Opsiyonel özellikler:

- ek yükleme
- klasör yönetimi

## İLink

`İLink`, İlos Tech uygulamalarının ve diğer şirketlerin veri zarflarını ortak biçimde taşıması için yazılmış özel protokoldür.

Çalışan yetkinlik hizmetleri:

- `ilos.ilink.kimlik`
- `ilos.ilink.paketle`
- `ilos.ilink.dogrula`

Sunucu bunları `sirketTanitim.ozelProtokoller` manifestinde ilan eder. Ticari lisans modeli ve ücretleri 8090 üzerinden yönetilir.

## Piyasaya açma

Sunucu çalışınca İSosyal, İMail ve İLink teknik manifestleri motora gider. 8090 panelinde:

1. İlos Tech hesabına girin.
2. Uygulamalar bölümünde standardı geçen İSosyal ve İMail’i piyasaya açın.
3. Abonelik ve kullanım fiyatlarını belirleyin.
4. Protokoller bölümünde İLink’i yayınlayın.
5. Gerektikçe kullanıcı kapasitesi ve altyapı satın alın.

8090 kod üretmez; burada bulunan bütün özelliklerin gerçek kodu `server.py` içindedir.
