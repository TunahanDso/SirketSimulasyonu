# Kalıcı Kayıt, Yönetim Hesapları ve Tek Seferlik Destekler

Bu belge 8090 Şirket Yönetim Merkezi ile motorun kalıcı çalışma verilerini açıklar.

## 1. Yatırımlar sıfırlanır mı?

Hayır. Başarılı bir 8090 yatırım işleminde:

1. Şirket kasasından yatırım maliyeti düşülür.
2. `yatirimSeviyeleri` içindeki ilgili seviye artırılır.
3. Kapasite bonusları canlı şirket hizmetlerine uygulanır.
4. İşletim verisi ve şirket bilançosu atomik olarak diske kaydedilir.

Motor yeniden açıldığında aynı dosya yüklenir ve yatırım seviyeleri tekrar uygulanır.

## 2. Kalıcı dosyalar

| Dosya | İçerik |
|---|---|
| `MotorVerileri/sirket-bilancolari.json` | Kasa, gelir, ceza, kalite, performans, güvenlik ve şirket geçmişi |
| `MotorVerileri/sirket-isletim.json` | Yatırımlar, krediler, ürünler, aboneler, SLA'lar, fiyat ezmeleri ve yönetim hesapları |
| `MotorVerileri/kod-tabanli-yayinlar.json` | Hizmet aktiflikleri, uygulama-ürün eşlemeleri ve protokol yayın eşlemeleri |
| `MotorVerileri/musteriler.json` | 10.000 kalıcı müşteri ve işlem geçmişleri |
| `MotorVerileri/tek-seferlik-destekler.json` | Bir kez uygulanan sermaye desteklerinin tekrar uygulanmasını engelleyen kayıt |

Bu dosyalar `.gitignore` kapsamındadır. `git pull` ve gelecekteki `git stash -u` işlemleri oyun durumunu taşımamalıdır.

## 3. Eski stash nedeniyle durum kaybolmuş görünüyorsa

Daha önce `git stash push -u` çalıştırıldıysa, o anda takip edilmeyen çalışma dosyaları stash'in üçüncü ebeveyninde bulunabilir.

Önce yalnız listele:

```powershell
git stash list
git stash show --include-untracked --name-only 'stash@{0}'
```

Listede `MotorVerileri/sirket-isletim.json` görülüyorsa yalnız o dosyayı geri getir:

```powershell
git checkout 'stash@{0}^3' -- MotorVerileri/sirket-isletim.json
```

Kod tabanlı yayınlar da stashten geri alınacaksa:

```powershell
git checkout 'stash@{0}^3' -- MotorVerileri/kod-tabanli-yayinlar.json
```

Bütün stash'i körlemesine `pop` etmek yerine yalnız gerekli çalışma dosyalarını geri almak daha güvenlidir.

## 4. İlos Tech ilk giriş bilgileri

İlos Tech hesabı henüz kendi parolasını belirlememişse başlangıçta şu bilgiler kullanılır:

```text
Kullanıcı adı: ilos
Geçici parola: ilos123
```

İlk girişten sonra parola 8090 içindeki **Hesap** sekmesinden değiştirilmelidir.

Şirket daha önce parolasını değiştirmişse başlangıç migrasyonu o parolaya dokunmaz.

## 5. Ugax finansman desteği

Ugax'a tek seferlik doğrudan sermaye desteği uygulanır:

```text
Şirket: Ugax
Şirket kimliği: ugur-ugax
Destek: 500.000 TL
Borç: Yok
Faiz: Yok
Tekrar: Yalnız bir kez
```

Motor desteği uygularken önce şirket bilançosunu kaydeder, sonra `tek-seferlik-destekler.json` içine destek kimliğini yazar.

Sonraki motor açılışlarında aynı destek kimliği bulunduğunda para tekrar eklenmez.

Bu tutar işletme geliri olarak değil, doğrudan sermaye/likidite desteği olarak kasaya eklenir.

## 6. Açılışta beklenen kayıtlar

İlk güncel açılışta:

```text
İlos Tech yönetim hesabı hazır | Kullanıcı: ilos | Geçici parola: ilos123
TEK SEFERLİK FİNANSMAN | Ugax kasasına 500.000,00 TL aktarıldı
```

Daha sonraki açılışlarda:

```text
Ugax 500.000 TL finansman desteği daha önce uygulanmış; yeniden eklenmedi.
```

mesajı görülmelidir.

## 7. Hızlı doğrulama

```powershell
git pull --ff-only origin agent/tunix-matematik-topla
dotnet build .\SirketMotoru\SirketMotoru.csproj
dotnet run --project .\SirketMotoru\SirketMotoru.csproj
```

Motor çalışırken bir yatırım satın al, motoru `Ctrl+C` ile düzgün kapat ve yeniden aç. Aynı yatırım seviyesi 8090'da korunmalıdır.
