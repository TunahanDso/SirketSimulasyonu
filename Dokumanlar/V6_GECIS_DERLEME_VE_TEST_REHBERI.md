# V6 Geçiş, Derleme ve Test Rehberi

Bu belge mevcut oyun kayıtlarını koruyarak V6 dalına geçmek, motoru derlemek, Tunix OS'yi test etmek ve ilk açılış sorunlarını teşhis etmek için kullanılır.

---

## 1. Geçiş öncesi yedek

Motor kapalıyken çalışma JSON dosyalarını yedekleyin.

Örnek PowerShell:

```powershell
$zaman = Get-Date -Format "yyyyMMdd-HHmmss"
$hedef = ".\Yedekler\v6-oncesi-$zaman"
New-Item -ItemType Directory -Force -Path $hedef | Out-Null

$dosyalar = @(
  ".\MotorVerileri\musteriler.json",
  ".\MotorVerileri\sirket-isletim.json",
  ".\MotorVerileri\kod-tabanli-yayinlar.json",
  ".\MotorVerileri\ekosistem.json",
  ".\MotorVerileri\pazar-fiyat.json",
  ".\MotorVerileri\ekonomi-v6.json"
)

foreach ($dosya in $dosyalar) {
  if (Test-Path $dosya) {
    Copy-Item $dosya $hedef -Force
  }
}
```

Dosyaların gerçek konumu çalışma dizimine göre farklıysa motor başlangıç konsolunda yazan `Motor veri klasörü` yolunu kullanın.

---

## 2. Dalı çekme

```powershell
git status
git pull --ff-only origin agent/tunix-matematik-topla
```

Yerel değişiklik varsa önce kaydedin veya ayrı dala alın. `--ff-only`, beklenmeyen otomatik merge oluşmasını engeller.

---

## 3. C# temiz derleme

```powershell
dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj
```

Beklenen sonuç:

```text
0 hata ile başarılı
```

Build başarısızsa `dotnet run` çalıştırmayın. İlk hatadan başlayarak düzeltin; sonraki hataların bir bölümü ilk hatanın zincirleme sonucu olabilir.

---

## 4. Tunix Rust testleri

```powershell
cd .\Sirketler\Tunahan-Rustix\Tunix
cargo test
```

Kontrol edilmesi gereken test grupları:

- 10 çekirdek hizmet testi,
- Tingram testleri,
- Tmail testleri,
- Tlink testleri,
- Tunix OS süreç/kaynak testleri,
- V6 manifest testleri.

Testler geçince:

```powershell
cargo run
```

Beklenen başlık:

```text
TUNIX OS · TINGRAM · TMAIL · TLINK
Sunucu sürümü: 0.8.0
```

---

## 5. Diğer şirket testleri

### İlos Tech

```powershell
cd .\Sirketler\Ilayda-Python\IlosTech
python -m unittest -v
python .\server.py
```

İlos Tech uygulama manifestleri V6 kategori, işletim sistemi ve protokol kurallarına henüz uymuyorsa motor bunları pasife alabilir. Bu kayıt silme değildir; manifest güncellemesi gerekir.

### Mudaf ve Ugax

Kendi sunucularında:

- standart V6 hizmet kimlikleri,
- 200 kategoriden geçerli kategori,
- zorunlu hizmet kombinasyonu,
- işletim sistemi,
- protokol,
- platform desteği ilan edilmelidir.

---

## 6. Motoru çalıştırma

Şirket sunucuları açıkken:

```powershell
cd <repo-koku>
dotnet run --project .\SirketMotoru\SirketMotoru.csproj
```

Beklenen başlangıç logları:

- motor ayarları yüklendi,
- V6 hizmet kataloğu yüklendi,
- aktif hizmet sayısı 500,
- uygulama kategorisi 200,
- müşteri sayısı 20.000,
- fiyat pazarı öz testi geçti,
- V6 ekonomi hazır,
- 8080 yayında,
- 8090 yayında.

---

## 7. İlk açılışta eski uygulamalar neden pasif olabilir?

V6 şu koşulları zorunlu hâle getirir:

- geçerli kategori,
- kategori zorunlu hizmetleri,
- hizmetlerin şirket tarafından gerçekten ilan edilmesi,
- işletim sistemi/platform,
- bağlantı protokolü,
- işletim sistemi ile protokol uyumu.

Eski uygulama bu alanlardan birini taşımıyorsa pasife alınır. Çözüm:

1. şirket sunucusu manifestini güncelleyin,
2. zorunlu hizmetleri kodlayın ve ilan edin,
3. sunucuyu yeniden başlatın,
4. motorun manifesti almasını bekleyin,
5. 8090'dan işletim sistemi/protokol dağıtım ayarını seçin,
6. uygulamayı yeniden aktif edin.

---

## 8. Kalıcı verilerin korunması

Motor yeniden bağlanan varlık için:

- sunucu fiyatını yalnız ilk varsayılan olarak alır,
- kayıtlı 8090 fiyatını korur,
- aktiflik yönetimini korur,
- kapasite tahsisini korur,
- kullanıcı ve gelir geçmişini korur.

Yeni varlık ilk kez geliyorsa sunucu varsayılanı kaydedilir.

---

## 9. 8080 kontrol listesi

Tarayıcı:

```text
http://localhost:8080/
```

Kontrol:

- [ ] Ana Sayfa bütün panoları alt alta gösteriyor.
- [ ] Diğer menüler yalnız kendi panelini gösteriyor.
- [ ] Şirket kartında bir önceki tick net geliri var.
- [ ] Şirket değeri yalnız kasaya eşit değil.
- [ ] Gerçek/tahsis/kullanılan kapasite görünüyor.
- [ ] İşletim sistemi pazarı görünüyor.
- [ ] Uygulama kategorisi pazar payları geliyor.
- [ ] Haber bülteni olayları gösteriyor.
- [ ] Müşteri CV özetleri geliyor.
- [ ] Veri sık ve takılmadan yenileniyor.

Eski HTML önbelleği görünürse `Ctrl+F5` kullanın.

---

## 10. 8090 kontrol listesi

Tarayıcı:

```text
http://localhost:8090/
```

Kontrol:

- [ ] Giriş çalışıyor.
- [ ] Sekmeler doğru paneli açıyor.
- [ ] Input alanı yazarken sıfırlanmıyor.
- [ ] Hizmet fiyatı değişiyor ve reconnect sonrası korunuyor.
- [ ] Hizmet aktif/pasif durumu korunuyor.
- [ ] Hizmet kapasite tahsisi gerçek havuzla sınırlandırılıyor.
- [ ] Uygulama kapasite tahsisi gerçek havuzdan tüketiyor.
- [ ] İşletim sistemi ve protokol seçimi görünüyor.
- [ ] Yatırım ön koşulu ve bitiş ticki görünüyor.
- [ ] Bloke yatırım nedeni gösteriliyor.
- [ ] Kredi, SLA ve protokol işlemleri sonuç mesajı veriyor.

---

## 11. API sağlık kontrolleri

```powershell
Invoke-RestMethod http://localhost:8080/api/saglik
Invoke-RestMethod http://localhost:8080/api/durum
Invoke-RestMethod http://localhost:8080/api/musteri-cv
```

8080 sağlık cevabında V6 borsa sürümü görünmelidir.

8090 oturum gerektiren uçlar doğrudan anonim çağrıda reddedilebilir; bu normaldir.

---

## 12. Katalog doğrulaması

Motor başlangıcında:

```text
Aktif: 500
Uygulama kategorisi: 200
```

sayıları görünmelidir.

Farklı sayı görülürse:

- `StandartKatalogV6` verisi bozuk olabilir,
- sıkıştırılmış katalog metni okunamıyor olabilir,
- aile veya kategori üretiminde kimlik çakışması olabilir.

Motor bu durumda başlamamalı ve açık hata vermelidir.

---

## 13. Kapasite testi

1. Bir şirkette düşük kapasiteyle uygulama açın.
2. Kullanıcı kapasitesini fiziksel havuzun çok üzerine tahsis edin.
3. Birkaç tick çalıştırın.
4. 8080 ve 8090'da doluluğu izleyin.

Beklenen:

- etkin tahsis fiziksel havuzla sınırlanır,
- kullanılan kapasite havuzu aşarsa performans/güvenilirlik düşer,
- haber bültenine kapasite olayı gelir,
- ağır durumda uygulama pasife alınabilir.

---

## 14. Fiyat testi

Aynı kategoride iki uygulama oluşturun:

```text
Uygulama A: 200 TL
Uygulama B: 1.000.000 TL
```

Beklenen:

- B yeni kullanıcı kazanamaz,
- B kullanıcı kaybeder,
- kullanıcılar A'ya veya diğer uygun rakiplere göç eder,
- B'nin pazar dışı geliri geri alınır,
- haber ve fiyat pazarı kayıtları güncellenir.

---

## 15. Yatırım testi

1. CPU yatırımı satın alın.
2. Şirkette `isletim.kaynak-ata` hizmeti bulunmasın.
3. Yatırımın bloke olduğunu doğrulayın.
4. Hizmeti gerçek kodla ekleyip sunucuyu yeniden bağlayın.
5. Yatırımın inşaat sürecine geçtiğini doğrulayın.
6. Bitiş tickinden sonra gerçek kapasite artışını kontrol edin.

---

## 16. Çalışma kayıtlarını sıfırlama

Yalnız test ortamında yapılmalıdır.

Önce yedek alın. Sonra yalnız sıfırlamak istediğiniz çalışma dosyasını silin. Kaynak katalog veya ayar dosyalarını silmeyin.

Örnek:

```powershell
Remove-Item .\MotorVerileri\ekonomi-v6.json
```

Bu işlem V6 kapasite/yatırım/grafik geçmişini sıfırlar; şirket kodunu silmez.

---

## 17. Sorun bildirirken gönderilecek bilgiler

- `dotnet build` tam çıktısı,
- `cargo test` tam çıktısı,
- motor başlangıç logunun ilk 100 satırı,
- hata olan tick logu,
- ilgili şirket sunucusunun konsol çıktısı,
- mümkünse `8080/api/durum` içindeki ilgili bölüm,
- hangi commitin çekildiği:

```powershell
git rev-parse HEAD
```
