# V7 Finans, Banka, SLA, Panolar ve Tunix Hizmet Paketi

Bu belge, V6 ekosisteminin üzerine kurulan V7 ekonomi ve yönetim paketinin güncel teknik kaynağıdır.

## 1. Güncellemenin amacı

Önceki sürümde şirketlerin tarihsel `netGelir` değeri son tick kazancı gibi gösteriliyor, gelir ve gider kalemleri ayrı takip edilemiyor, hizmet sayısı arttıkça eski sabit maliyet formülü şirketleri çok hızlı biçimde sıfıra indiriyordu. V7 şu ayrımları kesinleştirir:

- tarihsel toplam gelir,
- son tamamlanan tick geliri,
- son tamamlanan tick gideri,
- son tamamlanan tick net sonucu,
- nakit değişimi,
- kredi girişi,
- kapanış kasası,
- ödenemeyen yükümlülük,
- toplam aktif borç.

Kredi girişi işletme geliri değildir. Kurtarma kredisi net kazancı şişirmez.

## 2. Kalıcı tick muhasebesi

V7 muhasebesi `MotorVerileri/finans-v7.json` dosyasında saklanır. Her şirket için son 360 tick korunur.

Her tick kaydında şu alanlar bulunur:

- hizmet, SLA ve olay geliri,
- ürün geliri,
- abonelik geliri bilgi alanı,
- protokol lisans geliri,
- toplam gelir,
- operasyon ve altyapı gideri,
- finansman ve taksit gideri,
- yatırım gideri,
- iade ve chargeback gideri,
- ceza gideri,
- yeni ödenemeyen gider,
- toplam ödenmiş gider,
- tick net kazancı,
- kurtarma kredisi girişi,
- nakit değişimi,
- kapanış kasası,
- aktif borç,
- kredi notu.

`ToplamGider`, o tick içinde ödenmiş giderleri ifade eder. Ödenemeyen yeni yükümlülük ayrıca gösterilir; banka ve kurtarma sistemi bu alanı da dikkate alır.

## 3. Sürdürülebilir gider dengesi

Eski ekonomi formülü her hizmet ve kapasite birimine çok yüksek sabit gider yüklüyordu. 500 hizmetlik katalogda bu durum, henüz müşteri kazanamayan şirketlerin birkaç tick içinde kasasının sıfırlanmasına neden oluyordu.

V7 gider hedefi aşağıdaki gerçek ölçek bileşenlerine göre hesaplanır:

- aktif hizmet sayısı,
- etkin hizmet kapasitesi,
- aktif ürün sayısı,
- aktif kullanıcı sayısı,
- yatırım seviyeleri,
- teknik borç,
- bakım baskısı.

Eski formülün hedefin çok üstünde oluşturduğu yapay maliyet:

1. önce ödenemeyen yükümlülükten silinir,
2. daha önce kasadan çıktıysa şirkete iade edilir,
3. muhasebe işlem kaydına gerekçesiyle yazılır.

Şunlar normalizasyonla silinmez:

- gerçek yatırım harcaması,
- kredi faizi ve taksit,
- SLA cezası,
- güvenlik veya operasyon olayı,
- fiyat pazarı iadesi,
- aktif kullanıcı ve gerçek altyapı maliyeti.

## 4. Otomatik kurtarma kredisi

Şirketin kasası tükendiğinde veya yeni ödenemeyen gider oluştuğunda motor, şirket faaliyetinin tamamen kilitlenmesini önlemek için otomatik kurtarma kredisi açabilir.

Kurallar:

- kredi işletme geliri değildir,
- asgari ana para 25.000 TL,
- azami ana para 500.000 TL,
- 12 taksit,
- iki tickte bir ödeme,
- ilk kurtarmada yaklaşık yüzde 3,5 tick faizi,
- her sonraki kurtarmada faiz yükselir,
- faiz yüzde 7,5/tick üst sınırına sahiptir,
- kredi notu 90 puan düşer,
- temerrüt sayısı artar,
- 8080 ve 8090 haber bültenine kritik haber düşer,
- aynı şirkete üç tickten daha sık kurtarma kredisi verilmez.

Kurtarma kredisi ucuz veya avantajlı finansman değildir. Şirketi hayatta tutan fakat uzun vadede şirket değerini ve kredi kapasitesini bozan son çaredir.

## 5. Banka sistemi

8090 Banka paneli artık şunları gösterir:

- kredi notu,
- kredi notu sınıfı (`A+`–`E`),
- risk durumu,
- tahmini toplam kredi limiti,
- kullanılabilir kredi limiti,
- aktif borç,
- aktif kredi sayısı,
- sonraki taksit,
- sonraki ödeme ticki,
- temerrüt sayısı,
- kurtarma kredisi sayısı,
- şirket notuna göre piyasa faiz çarpanı.

Kredi limiti şirket değeri ve likiditeden etkilenir. Mevcut borç kullanılabilir limitten düşülür.

## 6. 8090 ayrıntılı gelir–gider paneli

`Gelir & Gider` sekmesinde:

- son tickin brüt geliri,
- son tickin toplam gideri,
- son tick net sonucu,
- gelir kalemleri,
- gider kalemleri,
- 60 ticklik gelir/gider/net grafiği,
- tick muhasebe defteri,
- kapanış kasası,
- borç ve kredi notu

gösterilir.

Panel bir input veya select alanı düzenlenirken arka planda veri alır fakat DOM'u yeniden çizmez. Kullanıcı alanı bıraktığında bekleyen veri uygulanır.

## 7. SLA uygunluk sistemi

Her SLA kategorisinin gerekli hizmetleri vardır. 8090 paneli teklif satırında:

- zorunlu hizmetleri,
- şirkette bulunan hizmetleri,
- eksik hizmetleri,
- kalite şartını,
- performans şartını,
- güvenlik şartını,
- kapasite şartını

gösterir.

Şirket uygun değilse `Kabul et` düğmesi pasif görünür. API de aynı şartları tekrar kontrol eder; yalnız HTML düğmesine güvenilmez.

## 8. 8080 finans ve grafik ekranları

8080 şirket kartlarında artık tarihsel net gelir yerine yalnız son tamamlanan tick için:

- gelir,
- gider,
- net kazanç,
- kurtarma kredisi,
- kasa,
- borç,
- kredi notu,
- şirket değeri,
- gerçek kapasite ve kullanım

gösterilir.

Grafik panelleri:

- şirket başına gelir/gider/net çizgi grafiği,
- kategori başına uygulama pazar payı pasta grafiği,
- işletim sistemi pazarı,
- finans karşılaştırma tablosu,
- müşteri CV örnekleri,
- risk ve güvenlik sıralaması.

## 9. 8080 scroll koruması

8080 yaklaşık 450 ms aralıkla veri sorgulamaya devam eder, ancak:

- yalnız tick değiştiğinde yeniden render eder,
- kullanıcı sayfayı veya tabloyu kaydırırken render işlemini erteler,
- yeni veri bellekte bekler,
- scroll bittikten sonra pencere ve tablo scroll konumlarını geri yükler,
- servis, uygulama, müşteri ve log listeleri artık otomatik olarak başa atlamaz.

## 10. Haber bülteni

8080 ve 8090'da kayan manşet bandı ve ayrıntılı haber paneli bulunur.

Haber kaynakları:

- güçlü tick kârı,
- ciddi tick zararı,
- otomatik kurtarma kredisi,
- kapasite aşımı ve duraksatma,
- uygulama veya işletim sistemi olayı,
- yatırım tamamlanması,
- piyasa ve müşteri hareketleri,
- güvenlik olayları.

Haberler dekor değildir; finansal ve operasyonel motor kayıtlarından üretilir.

## 11. Tunix 24 yeni standart hizmet

Tunix'e gerçek Rust implementasyonlarıyla şu hizmetler eklendi:

### Metin

- `metin.buyuk-harf`
- `metin.kucuk-harf`
- `metin.birlestir`
- `metin.parcala`
- `metin.ozetle`
- `metin.anahtar-kelime`

### Dizi

- `dizi.filtrele`
- `dizi.birlestir`
- `dizi.kesisim`
- `dizi.birlesim`
- `dizi.parcala`
- `dizi.dogrula`

### Veri

- `veri.filtrele`
- `veri.temizle`
- `veri.normalize-et`
- `veri.gruplandir`
- `veri.birlestir`
- `veri.korelasyon`

### Arama

- `arama.ara`
- `arama.sirala`
- `arama.otomatik-tamamla`
- `arama.yazim-duzelt`

### Bildirim

- `bildirim.push-gonder`
- `bildirim.zamanla`

Bu hizmetler:

- manifestte ilan edilir,
- fiyat ve kapasite değerine sahiptir,
- Rust yönlendiricisinde gerçek kodla çalışır,
- birim testlerle temel davranışı doğrulanır.

## 12. V6 genel sonuç doğrulaması

İlk 10 çekirdek hizmetin matematiksel ve semantik doğrulaması aynen korunur.

500 hizmetlik katalogda özel sonuç doğrulayıcısı henüz bulunmayan hizmet için motor:

- şirketin işi başarılı bildirmesini,
- sonucun boş olmamasını,
- 262.144 byte sınırını aşmamasını,
- geçerli JSON olmasını,
- 64 derinlik sınırını aşmamasını

kontrol eder. Yalnız `özel doğrulayıcı yok` gerekçesiyle doğru V6 sonucu çöpe atılmaz.

Bu temel doğrulama yapısaldır; bütün 500 hizmet için tam semantik doğrulama anlamına gelmez. Kritik hizmetlerin özel doğrulayıcıları zaman içinde ayrıca yazılmalıdır.

## 13. Kalıcı dosyalar

V7 ile birlikte şu dosya Git tarafından korunur:

```text
MotorVerileri/finans-v7.json
```

V6 ekonomi, ekosistem, fiyat pazarı ve mevcut işletim kayıtları da kalıcıdır.

## 14. Derleme ve test

```powershell
git pull --ff-only origin agent/tunix-matematik-topla

dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj

Push-Location .\Sirketler\Tunahan-Rustix\Tunix
cargo test
Pop-Location
```

Yerel build tamamlanmadan V7 paketinin derlendiği varsayılmamalıdır. Derleyicinin verdiği ilk hata, sonraki düzeltmenin kaynak noktasıdır.
