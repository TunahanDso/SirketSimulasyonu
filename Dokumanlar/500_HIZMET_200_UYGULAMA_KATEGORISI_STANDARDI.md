# 500 Hizmet ve 200 Uygulama Kategorisi Standardı

Bu belge, motor V6'da kullanılan standart hizmet kataloğunu ve uygulama kategorisi sözleşmelerini açıklar.

Amaç, şirketlerin rastgele isimlendirilmiş uygulamalar yayımlamasını engellemek değil; uygulamaların gerçekten hangi teknik hizmetlerden oluştuğunu karşılaştırılabilir ve doğrulanabilir hâle getirmektir.

---

## 1. Neden standart hizmet kataloğu var?

Motor bir hizmetin yalnızca adını değil, sözleşmesini bilmelidir. Böylece:

- bütün şirketler aynı işi aynı hizmet kimliğiyle ilan eder,
- motor doğru test verisini üretir,
- sonuçlar şirketler arasında karşılaştırılabilir,
- uygulama kategorileri zorunlu özellikleri denetleyebilir,
- yatırımlar belirli teknik hizmetleri ön koşul yapabilir,
- uygulamalar arası bağlantılar standartlaşır.

Şirketler hizmetin kodunu kendileri yazar. Katalog yalnız sözleşmenin kimliğini ve genel sınırlarını belirler.

---

## 2. Katalog üretim modeli

V6 tam olarak 500 hizmet üretir:

```text
25 hizmet ailesi × 20 işlev = 500 standart hizmet
```

Üretim deterministiktir. Aynı motor sürümü her açılışta aynı kimlikleri üretir.

Hizmet kimliği biçimi:

```text
<aile>.<islem>
```

Örnekler:

```text
kimlik.oturum-dogrula
sosyal.gonderi-olustur
eposta.spam-kontrol
isletim.kaynak-ata
guvenlik.saldiri-tespit
veritabani.yedek-al
```

Standart sürüm başlangıçta `1.0` olarak kullanılır.

---

## 3. Hizmet aileleri

Aşağıdaki 25 aile V6 kataloğunun temelini oluşturur.

| No | Aile | Örnek görevler |
|---:|---|---|
| 1 | `kimlik` | kullanıcı, token, oturum, rol, izin |
| 2 | `profil` | profil getir, güncelle, tercih, gizlilik |
| 3 | `sosyal` | gönderi, akış, yorum, etkileşim, takip |
| 4 | `eposta` | gönder, gelen kutusu, spam, ek, klasör |
| 5 | `mesaj` | kanal, özel mesaj, grup, okundu bilgisi |
| 6 | `dosya` | yükle, indir, sil, sıkıştır, paylaş |
| 7 | `medya` | görsel, video, ses, dönüştürme, yayın |
| 8 | `arama` | indeksleme, sorgu, öneri, filtre, sıralama |
| 9 | `analitik` | olay, dönüşüm, kullanıcı yolu, rapor |
| 10 | `bildirim` | anlık bildirim, e-posta, SMS, tercih |
| 11 | `odeme` | tahsilat, iade, fatura, cüzdan |
| 12 | `ticaret` | ürün, sepet, sipariş, stok, kargo |
| 13 | `veritabani` | kayıt, sorgu, yedek, geri yükleme |
| 14 | `guvenlik` | saldırı tespiti, istek doğrulama, denetim |
| 15 | `yapay-zeka` | sınıflandırma, öneri, özet, tahmin |
| 16 | `konum` | koordinat, rota, yakınlık, bölge |
| 17 | `takvim` | etkinlik, davet, uygunluk, hatırlatma |
| 18 | `belge` | oluşturma, düzenleme, sürüm, imza |
| 19 | `is-akisi` | görev, onay, durum, otomasyon |
| 20 | `gelistirme` | derleme, test, paket, dağıtım, log |
| 21 | `isletim` | süreç, kaynak, dosya sistemi, paket, ağ |
| 22 | `iot` | cihaz, telemetri, komut, alarm |
| 23 | `egitim` | ders, sınav, ilerleme, sertifika |
| 24 | `saglik` | randevu, kayıt, ölçüm, uyarı |
| 25 | `oyun` | oturum, eşleştirme, skor, envanter |

Her ailede 20 işlev bulunur. `StandartKatalogV6`, toplamın 500 olmaması hâlinde motoru başlatmaz.

---

## 4. Şirket hizmet ilanı

Şirket sunucusu yalnız gerçekten çalıştırabildiği hizmetleri ilan etmelidir.

```json
{
  "hizmetKimligi": "sosyal.gonderi-olustur",
  "hizmetSurumu": "1.0",
  "birimFiyat": 7,
  "azamiEszamanliIs": 12,
  "aktif": true
}
```

Motor şu kontrolleri yapar:

- hizmet kimliği katalogda var mı,
- sürüm destekleniyor mu,
- aynı hizmet iki kez ilan edilmiş mi,
- kapasite ve fiyat geçerli mi,
- hizmet çağrıldığında sonuç sözleşmeye uyuyor mu.

Katalogda bulunmayan özel hizmet, standart iş talebi alamaz. Şirketin özel işlevi bir uygulama içi ayrıntı olarak kalabilir; piyasada motor tarafından ölçülecekse standart katalog hizmetine bağlanmalıdır.

---

## 5. Uygulama kategori modeli

V6 tam olarak 200 kategori üretir:

```text
20 sektör × 10 uygulama arketipi = 200 kategori
```

Temel arketipler:

1. sosyal medya,
2. e-posta,
3. mesajlaşma,
4. bulut depolama,
5. e-ticaret,
6. analitik,
7. güvenlik,
8. geliştirici aracı,
9. işletim sistemi,
10. oyun.

Sektör örnekleri:

- genel,
- finans,
- sağlık,
- eğitim,
- kamu,
- üretim,
- perakende,
- lojistik,
- medya,
- turizm,
- otomotiv,
- enerji,
- tarım,
- hukuk,
- insan kaynakları,
- gayrimenkul,
- telekom,
- araştırma,
- savunma dışı endüstriyel teknoloji,
- profesyonel hizmetler.

Kimlik örnekleri:

```text
sosyal-medya
finans.sosyal-medya
saglik.eposta
uretim.analitik
egitim.oyun
isletim-sistemi
```

---

## 6. Her kategori ne tanımlar?

Bir kategori kaydı aşağıdaki kuralları taşır:

- `kategoriKimligi`
- `kategoriAdi`
- `sektor`
- `urunTuru`
- `zorunluHizmetler`
- `izinliEkHizmetAileleri`
- `asgariHizmetSayisi`
- `onerilenHizmetSayisi`
- `azamiHizmetSayisi`
- `ekHizmetKaliteKatkisi`
- `tabanKapasiteTuketimi`
- `kullaniciBasinaKapasiteTuketimi`

Şirket uygulama kategorisini kendisi seçer fakat katalog dışı yeni bir kategori adı uyduramaz.

---

## 7. Sosyal medya örneği

Sosyal medya kategorisinin zorunlu çekirdeği örnek olarak şunları içerir:

- `kimlik.oturum-dogrula`
- `profil.profil-getir`
- `sosyal.gonderi-olustur`
- `sosyal.akisi-getir`
- `sosyal.etkilesim-kaydet`

Uygun opsiyonel hizmetler:

- `sosyal.yorum-ekle`
- `sosyal.icerik-ara`
- takip hizmetleri,
- bildirim hizmetleri,
- medya yükleme,
- içerik önerisi,
- moderasyon,
- analitik.

Bir sosyal medya uygulamasına alakasız bir ödeme veya sağlık hizmeti eklemek doğrudan kalite kazandırmaz. Kategori izin vermiyorsa uygunsuz hizmet olarak işaretlenebilir.

---

## 8. E-posta örneği

Zorunlu çekirdek:

- `kimlik.oturum-dogrula`
- `eposta.gonder`
- `eposta.gelen-kutusu`
- `eposta.ara`
- `eposta.spam-kontrol`

Opsiyonel kalite hizmetleri:

- `eposta.ek-yukle`
- `eposta.klasor-olustur`
- bildirim,
- takvim bağlantısı,
- belge önizleme,
- güvenlik denetimi,
- arşivleme.

---

## 9. İşletim sistemi örneği

İşletim sistemi yalnız üç isimden oluşamaz. Süreç, kaynak, dosya sistemi, paket, ağ, kimlik ve güvenlik gibi çekirdek hizmetleri taşımalıdır.

Örnek zorunlu çekirdek:

- `isletim.surec-baslat`
- `isletim.kaynak-ata`
- `isletim.dosya-sistemi`
- `isletim.paket-kur`
- `isletim.ag-yapilandir`
- `kimlik.kullanici-dogrula`
- `guvenlik.istek-dogrula`

Opsiyonel kalite hizmetleri:

- süreç listeleme,
- kaynak bırakma,
- paket kaldırma,
- güncelleme kontrolü,
- güncelleme kurma,
- log toplama,
- uygulama çalıştırma,
- yedekleme,
- cihaz yönetimi.

---

## 10. Uygulama manifesti

```json
{
  "uygulamaKimligi": "ornek-sosyal",
  "uygulamaAdi": "Örnek Sosyal",
  "surum": "1.0.0",
  "kategori": "sosyal-medya",
  "urunTuru": "uygulama",
  "dagitimModeli": "sunucu",
  "ozellikler": [
    {
      "ozellikKimligi": "oturum",
      "hizmetKimligi": "kimlik.oturum-dogrula",
      "hizmetSurumu": "1.0",
      "zorunlu": true
    },
    {
      "ozellikKimligi": "gonderi",
      "hizmetKimligi": "sosyal.gonderi-olustur",
      "hizmetSurumu": "1.0",
      "zorunlu": true
    }
  ],
  "desteklenenProtokoller": ["ornek-protokol"],
  "desteklenenPlatformlar": ["ornek-os"],
  "gerekliPlatformlar": ["ornek-os"]
}
```

Manifestte yazan her özellik şirketin `hizmetler` ilanında da bulunmalıdır.

---

## 11. Doğrulama sonucu

Motor her uygulama için aşağıdaki bilgileri üretir:

- kategori geçerli mi,
- eksik zorunlu hizmetler,
- uygun ek hizmetler,
- uygunsuz hizmetler,
- hizmet kalite puanı,
- kapasite tüketim puanı.

Uygulama şu durumlarda piyasaya çıkamaz veya pasife alınır:

- kategori katalogda yoksa,
- zorunlu hizmet eksikse,
- şirket hizmeti ilan etmiyorsa,
- hizmet pasifse,
- asgari hizmet sayısı sağlanmıyorsa,
- azami hizmet sayısı aşılmışsa,
- işletim sistemi/protokol bağlantısı geçersizse.

---

## 12. Ek hizmetlerin kalite etkisi

Daha çok hizmet otomatik olarak daha iyi uygulama anlamına gelmez.

Kalite hesabı:

- zorunlu hizmetlerin eksiksiz olması temel şarttır,
- kategoriye uygun her ek hizmet sınırlı kalite katkısı verir,
- alakasız hizmetler kalite cezası doğurabilir,
- çok fazla hizmet kapasite ve bakım giderini artırır,
- çalışan hizmet sayısı arttıkça hata yüzeyi ve güvenlik riski de artar.

Örnek kavramsal formül:

```text
Hizmet kalite puanı =
  taban kalite
  + uygun ek hizmet katkısı
  - eksik zorunlu hizmet cezası
  - uygunsuz hizmet cezası
  - kesinti ve hata cezası
```

---

## 13. Kapasite etkisi

Her kategori iki kapasite katsayısı taşır:

- uygulama açıldığında tüketilen taban kapasite,
- aktif kullanıcı başına tüketilen kapasite.

Daha fazla özellik:

- kaliteyi artırabilir,
- daha fazla CPU/RAM/ağ/depolama tahsisi gerektirir,
- yoğunlukta aşırı yük riskini büyütür,
- bakım giderini yükseltir.

Bu nedenle oyuncu yalnız “en çok hizmeti ekle” stratejisiyle kazanamaz.

---

## 14. Eski uygulamaların geçişi

Motor eski uygulama kaydını silmez.

Yeni V6 standardına uymayan uygulama:

1. geçmiş kullanıcı ve gelirini korur,
2. `kategori/servis standardı eksik` gerekçesiyle pasif olur,
3. şirket sunucusu manifestini düzeltir,
4. şirket yeniden bağlanır,
5. motor yeni manifesti doğrular,
6. 8090'dan tekrar aktif edilir.

---

## 15. Geliştirici kontrol listesi

Yeni uygulama yayımlamadan önce:

- [ ] 200 kategoriden doğru kategori seçildi.
- [ ] Bütün zorunlu hizmetler kodlandı.
- [ ] Hizmetler şirket tanıtımında ilan edildi.
- [ ] Manifest özellikleri ilan edilen hizmetlerle eşleşiyor.
- [ ] Opsiyonel hizmetler kategoriyle ilgili.
- [ ] Desteklenen işletim sistemi belirtildi.
- [ ] Bağlantı protokolü belirtildi.
- [ ] Protokol işletim sistemi tarafından da destekleniyor.
- [ ] Hizmet testleri geçiyor.
- [ ] Gerçek kapasite havuzu yeterli.
- [ ] Fiyat kategori pazarına uygun.
