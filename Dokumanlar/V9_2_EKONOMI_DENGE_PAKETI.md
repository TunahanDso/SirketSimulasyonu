# V9.2 Ekonomi Denge Paketi

Bu paket çalışan V9.2 motorunu yeniden yazmaz. Aşırı yüksek şirket kârlarını dengelemek, olay akışını canlandırmak ve şirketleri eşit başlangıca döndürmek için mevcut ekonomi katmanını günceller.

## 1. Yeni maliyet modeli

Şirket işletme gideri artık gelirden bağımsız küçük bir sabit rakam değildir.

Her tickte ek maliyet şu bileşenlerden oluşur:

```text
sabit operasyon gideri
+ aktif hizmet gideri
+ ayrılmış kapasite gideri
+ gerçekten kullanılan kapasite gideri
+ aktif kullanıcı gideri
+ büyütülmüş altyapı gideri
+ ciroya bağlı değişken gider
+ yüksek ciroda kademeli ölçek gideri
+ düşük teknik puanlarda verimsizlik gideri
+ aşırı kapasite kullanımında baskı gideri
```

Ciro dilimleri:

- temel değişken maliyet: cironun %34'ü,
- 2.000 TL üzerindeki bölüm için ek %5,
- 10.000 TL üzerindeki bölüm için ek %7,
- 50.000 TL üzerindeki bölüm için ek %6.

Bunlar marjinal dilimlerdir; bütün ciroya birden uygulanmaz.

Kalite, performans, güvenlik ve güvenilirlik ortalaması düşük olan şirket daha verimsiz çalışır ve daha yüksek maliyet öder. Çok yüksek puanlı şirket küçük bir operasyon indirimi kazanır.

Kullanılan kapasite toplam fiziksel kapasitenin %80'ini geçerse gecikme, acil altyapı ve operasyon baskısı nedeniyle ek maliyet oluşur.

## 2. Güvenlik sınırları

Yeni maliyet modeli şirketi tek tickte yok etmez:

- gider şirket kasasından fazla kesilemez,
- otomatik kredi oluşturulmaz,
- borç kendiliğinden büyümez,
- toplam ek maliyet ciro ve sabit giderle ilişkili güvenli bir tavana sahiptir.

Amaç zarar yazmayı imkânsız yapmak değil; başarılı şirketlerin sınırsız kâr marjıyla para basmasını engellemektir.

## 3. Daha yoğun olay sistemi

Her tick en az bir piyasa olayı oluşur. Çoğu tickte iki, zaman zaman üç veya dört olay görülebilir.

Olayların yaklaşık dağılımı:

- %48 olumlu,
- %52 olumsuz,
- kritik olay olasılığı yaklaşık %4,5.

Yeni ve güncellenen olay örnekleri:

- viral kullanıcı akışı,
- kurumsal anlaşma,
- kamu ihalesi,
- satın alma söylentisi,
- veri merkezi indirimi,
- ürün incelemesi,
- ödül,
- ödeme sağlayıcısı kesintisi,
- acil kapasite kiralaması,
- yanlış reklam kampanyası,
- tedarik zinciri gecikmesi,
- sözleşme iptali,
- nadir veri merkezi kazası.

Olay etkileri şirket ölçeğine göre büyüyebilir; ancak:

- olumlu olaylar cironun sınırlı bir bölümünü aşamaz,
- normal olumsuz olaylar kasanın yaklaşık %7,5'inden fazlasını alamaz,
- kritik olumsuz olaylar kasanın yaklaşık %14'üyle sınırlıdır,
- güvenlik olayları yeniden milyonluk otomatik zarar üretmez.

## 4. Operasyon raporları

Cirosu veya ölçek maliyeti belirli seviyeyi geçen şirketler için haber bülteninde `OPERASYON RAPORU` yayımlanır.

Raporda:

- tick cirosu,
- altyapı ve kullanıcı maliyeti,
- kapasite maliyeti,
- toplam operasyon gideri

gösterilir.

## 5. Tek seferlik reset

Yeni dengeyle herkesin eşit başlaması için çalışma verileri bir kez daha sıfırlanır.

İşaret dosyası:

```text
MotorVerileri/v9.2-ekonomi-dengesi-reset-4.json
```

Arşiv:

```text
MotorVerileri/Arsiv/V9.2-ekonomi-denge-reset-oncesi-YYYYMMDD-HHMMSS/
```

Korunanlar:

- şirket sunucu kodları,
- manifestler,
- motor ayarları,
- hizmet kataloğu.

Sıfırlananlar:

- şirket kasaları ve bilançoları,
- yatırımlar ve krediler,
- uygulama/OS/protokol piyasa kayıtları,
- kapasite tahsisleri,
- müşteri ürün tercihleri,
- haber, olay, talep ve tick geçmişi.

## 6. Test

```powershell
git pull --ff-only origin agent/tunix-matematik-topla
dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj
```

İlk çalıştırmada beklenen reset satırı:

```text
V9.2 EKONOMİ DENGE RESETİ tamamlandı
```

Olay merkezi başlangıç satırı:

```text
V9.2 ekonomi denge paketi hazır | Kademeli ciro maliyeti ve yoğun piyasa olayları aktif.
```
