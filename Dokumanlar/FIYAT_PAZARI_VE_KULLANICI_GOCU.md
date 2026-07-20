# Fiyat Pazarı ve Kullanıcı Göçü

Bu belge, ürün ve uygulama fiyatlarının şirket tarafından serbestçe belirlenmesine rağmen müşterilerin fiyattan bağımsız davranmasını engelleyen pazar sistemini açıklar.

## Temel kural

Şirketler ürün fiyatını 8090 yönetim panelinden belirler. Motor fiyatı keyfî bir üst sınırla otomatik olarak değiştirmez. Buna karşılık fiyatın pazar sonucu tamamen gerçektir:

- Aşırı pahalı ürün yeni kullanıcı kazanamaz.
- Mevcut kullanıcılar aynı kategoride daha iyi fiyat/kalite sunan rakibe geçebilir.
- Rakip yoksa kullanıcı kategoriyi kullanmayı bırakır.
- Tekrarlanan fiyat şoku ürün memnuniyetini, şirket itibarını ve dolaylı olarak şirket değerini düşürür.
- Çok ucuz veya ücretsiz ürün talep kazanabilir; fakat kullanıcı, kapasite, sunucu ve bakım giderleri devam ettiği için şirket zarar edebilir.

## Pazarların ayrılması

Her ürün yalnız kendi pazarında karşılaştırılır. Pazar anahtarı şu iki bilgiden oluşur:

```text
ürünTürü | kategori
```

Örnekler:

```text
uygulama|sosyal-medya
uygulama|eposta
uygulama|mesajlasma
isletim-sistemi|sistem
platform|gelistirici-araci
oyun|oyun
```

Sosyal medya fiyatı e-posta veya işletim sistemiyle karşılaştırılmaz. Her kategorinin kendi taban tüketici bütçesi ve kendi rakipleri vardır.

## Referans fiyatın oluşması

Motor her tickte kategori için şunları hesaplar:

- Aktif ürün sayısı
- Toplam kullanıcı
- En ucuz ve en pahalı etkin fiyat
- Dayanıklı yüzde 40 pazar fiyatı
- Kategori bütçe çıpası
- Pazar referans fiyatı
- Erişilebilir alt ve üst fiyat aralığı

Tek bir aşırı fiyatın pazarı yukarı çekmemesi için fiyatlar referans hesabında kırpılır. İki ürünlü pazarda bir ürün 200 TL, diğeri 1.000.000 TL ise milyonluk fiyat medyanı bozup kendisini normalleştiremez.

## Etkin fiyat

Farklı fiyatlandırma modelleri aynı pazarda karşılaştırılabilsin diye etkin fiyat hesaplanır:

- `abonelik`: aylık abonelik fiyatı
- `freemium`: abonelik fiyatının bir bölümü + beklenen kullanım bedeli
- `kullanim`: beklenen aylık kullanım bedeli
- `lisans` ve `tek-seferlik`: lisans bedelinin dönemlere yayılmış karşılığı

Bu nedenle kullanım başına küçük görünen fakat toplamda çok pahalı olan ürün de pazar tarafından pahalı sayılabilir.

## Adil fiyat

Kategori referans fiyatı her ürün için doğrudan aynı değildir. Ürünün kabul edilebilir premium seviyesi şunlardan etkilenir:

- Ürün kalitesi
- Kod kalitesi
- Performans
- Güvenlik
- Güvenilirlik
- Ürün memnuniyeti
- Şirket itibarı

Kaliteli ve itibarlı bir ürün pazar ortalamasının üstünde fiyat isteyebilir. Ancak kalite, 100 veya 1.000 kat fiyat farkını meşrulaştırmaz.

## Fiyat oranları ve sonuçları

`fiyat oranı = etkin fiyat / adil fiyat`

| Fiyat oranı | Pazar davranışı |
|---:|---|
| 0–0,35 | Çok ucuz; yüksek talep, yüksek işletme yükü |
| 0,35–0,65 | Ucuz; güçlü talep |
| 0,65–1,10 | Rekabetçi ve normal |
| 1,10–1,30 | Pahalı; sınırlı kayıp |
| 1,30–1,55 | Belirgin biçimde pahalı |
| 1,55–1,85 | Çok pahalı; ciddi büyüme kaybı |
| 1,85–2,25 | Yeni müşteri neredeyse durur, kullanıcı kaçar |
| 2,25–3,00 | Fiyat şoku |
| 3,00 üzeri | Pazar dışı; yeni talep sıfıra yaklaşır |
| 20 üzeri | Mevcut kullanıcıların yaklaşık %97'sine kadar olan kısmı tek tickte kaçabilir |

Oranlar kesin kullanıcı sayısı değildir. Operasyon riski, fiyat değişiminin büyüklüğü, rakip sayısı, kalite, kapasite ve rastgele pazar oynaklığı sonucu değiştirir.

## Yeni kullanıcı ediniminin iptali

Eski ürün ekonomisi fiyat çok yüksek olsa bile en az `0,12` talep çarpanı bırakıyordu. Bu nedenle milyonluk abonelik fiyatı olan ürün bile kullanıcı kazanabiliyordu.

Yeni pazar sistemi temel ürün ekonomisinden sonra çalışır:

1. O tickte kazanılan kullanıcı sayısını tespit eder.
2. Fiyat oranına göre geçersiz edinimleri iptal eder.
3. Mevcut kullanıcılar için fiyat kaynaklı kayıp hesaplar.
4. Kaçan kullanıcıların uygun kısmını rakiplere dağıtır.
5. Son sayıları kalıcı işletim dosyasına kaydeder.

Bu sebeple eski formülün eklediği kullanıcılar aynı tickte pazar tarafından geri alınabilir.

## Rakibe göç

Kaçan kullanıcıların tamamı kaybolmaz. Aynı kategorideki rakipler şu koşullarla kullanıcı alabilir:

- Ürün aktif olmalı.
- Boş kullanıcı kapasitesi bulunmalı.
- Fiyat oranı aşırı yüksek olmamalı.
- Ürünün fiyat/kalite değeri kabul edilebilir olmalı.

Göç ağırlığı kalite, fiyat ve boş kapasiteye göre hesaplanır. En ucuz ürün otomatik olarak bütün kullanıcıları almaz; kötü kalite ve yetersiz kapasite göçü sınırlar.

## Fiyat şoku geçmişi

Aşırı fiyat bir ticklik unutulan olay değildir. Motor ürün başına şunları kalıcı kaydeder:

- Son etkin fiyat
- Son aktif kullanıcı sayısı
- Fiyat şoku serisi
- Bu tick engellenen yeni kullanıcı
- Bu tick fiyat nedeniyle kaçan kullanıcı
- Bu tick rakipten gelen kullanıcı
- Adil fiyat
- Fiyat oranı
- Talep çarpanı
- Fiyat kayıp oranı

Fiyat kısa sürede %50 veya daha fazla artırılırsa ayrıca fiyat değişim şoku uygulanır. Tekrarlanan pahalı fiyatlandırma kullanıcı kaybını büyütür.

## Kalıcı dosya

Pazar durumu çalışma sırasında şu dosyada tutulur:

```text
MotorVerileri/pazar-fiyat.json
```

Bu dosya Git tarafından izlenmez. Motor kapanıp açıldığında fiyat geçmişi ve fiyat şoku serileri korunur.

## 8090 verisi

`GET /api/durum` cevabında `fiyatPazari` alanı bulunur.

```json
{
  "fiyatPazari": {
    "sonTick": 42,
    "urunler": [
      {
        "urunAdi": "Örnek Sosyal",
        "etkinFiyat": 1000000,
        "adilFiyat": 210,
        "fiyatOrani": 4761.9,
        "talepCarpani": 0,
        "buTickEngellenenYeniKullanici": 2,
        "buTickFiyatKaybi": 1840,
        "buTickRakiptenGelenKullanici": 0,
        "pazarDurumu": "pazar-dışı"
      }
    ],
    "kategoriler": [
      {
        "pazarKimligi": "uygulama|sosyal-medya",
        "referansFiyat": 200,
        "erisilebilirAltFiyat": 110,
        "erisilebilirUstFiyat": 290
      }
    ]
  }
}
```

## Örnek: 200 TL ve 1.000.000 TL sosyal medya

Aynı kalite düzeyinde iki ürün olduğunu düşünelim:

- Ürün A: 200 TL
- Ürün B: 1.000.000 TL

Motor milyonluk fiyatı pazar referansına katarken kırpar; ürün B kendisini normal fiyat gibi gösteremez. Sonuçta:

- Ürün A rekabetçi fiyat aralığında kalır.
- Ürün B'nin talep çarpanı sıfır olur.
- Ürün B'nin o tickte kazandığı kullanıcılar iptal edilir.
- Mevcut kullanıcıların çok büyük kısmı ayrılır.
- Kapasitesi varsa kullanıcıların önemli bölümü Ürün A'ya geçer.
- Ürün B'nin memnuniyet ve şirket itibar puanı düşer.
- Kullanıcı ve itibar kaybı şirket değerlemesine yansır.

## Stratejik sonuç

Pazar sistemi şirketi tek bir doğru fiyata zorlamaz. Oyuncu:

- Ucuz büyüme,
- Premium kalite,
- Freemium,
- Kullanım bazlı gelir,
- Niş ve pahalı kurumsal ürün,
- Zararına pazar payı kazanma

stratejilerini deneyebilir. Ancak her stratejinin gerçek müşteri, kapasite, gider, itibar ve değerleme sonucu vardır.
