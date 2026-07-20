# Hizmet Sözleşmeleri v2

Motor ile şirket sunucuları UTF-8, satır sonlandırmalı ham TCP JSON protokolünü kullanır. Alan adları camelCase biçimindedir. Şirketler yalnız ilan ettikleri hizmetlere iş alır.

## Yeni hizmetler

### `veri.medyan-hesapla@1.0`

İstek:

```json
{"sayilar":[9,1,5,3]}
```

Başarılı sonuç:

```json
{"sonuc":4}
```

Dizi boş olamaz. Çift elemanlı dizide ortadaki iki değerin aritmetik ortalaması alınır.

### `veri.standart-sapma@1.0`

İstek:

```json
{"sayilar":[2,4,4,4,5,5,7,9]}
```

Başarılı sonuç:

```json
{"sonuc":2}
```

Motor örneklem değil, popülasyon standart sapmasını doğrular. Varyans böleni `N` değeridir.

### `dizi.sirala@1.0`

İstek:

```json
{"sayilar":[3,1,2],"yon":"artan"}
```

Başarılı sonuç:

```json
{"sonuc":[1,2,3]}
```

`yon` yalnız `artan` veya `azalan` olabilir. Sonuç dizisinin uzunluğu ve bütün değerleri motor tarafından doğrulanır.

### `matematik.asal-carpanlar@1.0`

İstek:

```json
{"sayi":360}
```

Başarılı sonuç:

```json
{"sonuc":[2,2,2,3,3,5]}
```

Sayı en az 2 olmalıdır. Çarpanlar tekrarlı ve küçükten büyüğe sıralı gönderilir.

### `metin.frekans-analizi@1.0`

İstek:

```json
{"metin":"Tunix tunix motor"}
```

Başarılı sonuç:

```json
{"sonuc":{"motor":1,"tunix":2}}
```

Ayırıcılar yalnız boşluk, sekme, CR ve LF karakterleridir. Kelimeler küçük harfe dönüştürülür. Noktalama işaretleri kelimenin parçasıdır.

## Motorun kalite değerlendirmesi

- Motor sonucu şirketin beyanına güvenmeden kendisi hesaplar.
- Doğru sonuç ve zor iş başarısı kod kalitesini yavaşça yükseltir.
- Yanlış veya biçimsiz sonuç kod kalitesi, itibar ve güvenilirliği hızlı düşürür.
- Gerçek işlem süresi zaman aşımı sınırına oranlanarak performans puanına işlenir.
- Başarı puanları yavaş artar; başarısızlık cezaları daha büyüktür.
- Puanlar her tick 50 merkezine doğru çok hafif aşınır.

## Kötü niyetli iş sözleşmesi

Bazı talepler normal hizmet alanlarının yanında motor tarafından üretilen şu alanı içerir:

```json
{
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

Bu veri gerçek zararlı kod değildir; simülasyon içi saldırı imzasıdır. Güvenli şirket, hizmet algoritmasını çalıştırmadan aşağıdaki başarısız sonucu döndürmelidir:

```json
{
  "mesajTuru":"isSonucu",
  "istekKimligi":"istekten-gelen-kimlik",
  "isKimligi":"isten-gelen-kimlik",
  "sirketKimligi":"sirket-kimligi",
  "basarili":false,
  "sonucVerisiJson":"{}",
  "hataKodu":"GUVENLIK_REDDI",
  "hataMesaji":"Kötü niyetli istek engellendi.",
  "islemSuresiMs":0.2
}
```

Motor yalnız `GUVENLIK_REDDI`, `ISTEK_GUVENLI_DEGIL` veya `KOTU_NIYETLI_ISTEK` kodlarından birini açıkça gönderen şirketi savunma yapmış sayar. İsteği normal iş gibi çalıştırmak, zaman aşımına düşmek veya generic hata vermek saldırı başarısı ve finansal kayıp doğurur.
