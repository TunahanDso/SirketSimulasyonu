# Proje Dokümanları

Bu dosya, Üç Kardeş Yazılım Şirketi Simülasyonu için güncel teknik belgelerin ana indeksidir.

> **Güncel ana sürüm: V9.2 kontrollü geliştirme sürümü**  
> V9.2, çalışan V9.1 motorunu koruyarak ayrıntılı şirketler borsası, çoklu protokol, yeniden ürün düzenleme, toplu hizmet kapasitesi ve gelişmiş haber/olay sistemini ekler.

> **Reset kararı:** V9.2 ilk açılışında kullanıcı talebiyle son bir tam çalışma verisi reseti uygulanır. Bundan sonraki sürümlerde ilerleme korunur ve mevcut veri migrasyonla güncellenir.

---

## 1. Önce okunacak belgeler

1. [V9.2 Borsa, Çoklu Protokol, Toplu Kapasite ve Son Reset](Dokumanlar/V9_2_BORSA_COKLU_PROTOKOL_TOPLU_KAPASITE_VE_SON_RESET.md)
2. [V9.1 Temiz Ekonomi, Kapasite, Talep ve Panel Rehberi](Dokumanlar/V9_1_TEMIZ_EKONOMI_KAPASITE_TALEP_VE_PANOLAR.md)
3. [500 Hizmet ve 200 Uygulama Kategorisi Standardı](Dokumanlar/500_HIZMET_200_UYGULAMA_KATEGORISI_STANDARDI.md)
4. [Tunix OS Teknik Rehberi](Dokumanlar/TUNIX_OS_TEKNIK_REHBERI.md)
5. [Uygulama Manifesti V1](UYGULAMA_MANIFESTI_V1.md)
6. [Sunucu Güncelleme V2 Rehberi](SUNUCU_GUNCELLEME_V2_REHBERI.md)

V9.2 belgesi borsa, kapasite tahsisi, ürün düzenleme, çoklu protokol, olay sistemi ve reset davranışında önceki belgelerden üstündür. V9.1 belgesi tek ekonomi, hizmet fiyatları, müşteri pazarı ve temel kredi/gider davranışında geçerliliğini korur.

---

## 2. V9.2 temel kuralları

```text
Kod ve gerçek hizmet işçiliği şirket sunucusunda yapılır.
Motor teknik sonucu, kapasiteyi, muhasebeyi ve piyasayı doğrular.
Manifestte ilan edilen hizmet gerçek yönlendiricide çalışmalıdır.
500 standart hizmetin fiyatı motor tarafından sabittir.

Her şirketin tek bir fiziksel kapasite havuzu vardır.
Başlangıç fiziksel kapasitesi 2.500'dür.
Yeni uygulama veya işletim sistemi yeni toplam kapasite üretmez.
Hizmetler tek ortak kapasite havuzundan pay alır.
Her uygulama ve işletim sistemi aynı toplam havuzdan ayrı pay alır.
Toplam kapasite yalnız altyapı yatırımıyla büyür.

Uygulama ve OS fiyatları kategori min–max aralığındadır.
Yayımlanmış ürünler 8090'dan yeniden düzenlenebilir.
İşletim sistemleri birden fazla aktif protokol kabul edebilir.
Uygulamanın seçtiği bütün protokoller seçilen OS tarafından desteklenmelidir.

Bir şirketin kapalı olması global ticki durdurmaz.
Kapalı şirket iş alamaz ve ekonomik olarak donar.
Otomatik kurtarma kredisi yoktur.
Negatif rastgele olay şirket kasasından fazla kesinti yapamaz.

8080 salt okunur piyasa ve borsa ekranıdır.
8090 şirkete özel ticari, ürün ve altyapı yönetim ekranıdır.
```

---

## 3. V9.2 panoları

| Port | Sistem | Kapsam |
|---:|---|---|
| 8080 | Yazılım Borsası V9.2 | Şirket değer sıralaması, tahmini hisse, finans, kapasite, kurumsal puanlar, uygulama/OS/hizmet pazarı, müşteri CV ve şaşaalı haber merkezi |
| 8090 | Şirket Yönetim Merkezi V9.2 | Toplu hizmet kapasitesi, ürün/OS tahsisi, yeniden ürün düzenleme, çoklu protokol, ürün fiyatı, 500 hizmet, 200 kategori, SLA, yatırım, finans ve şirket haberleri |

Her iki panel tek görünüm kökü kullanır. Canlı yenileme sırasında aktif sekme ve iç tablo scroll konumları korunur.

---

## 4. Son temiz sezon

V9.2 ilk çalıştırmada çalışma verilerini arşivler ve temizler:

```text
MotorVerileri/Arsiv/V9.2-son-reset-oncesi-YYYYMMDD-HHMMSS/
MotorVerileri/v9.2-son-temiz-sezon-3.json
```

Korunanlar:

- şirket sunucu kodları,
- manifestler,
- `motor-ayarlari.json`,
- `hizmet-katalogu.json`.

Sıfırlananlar:

- bilançolar ve kasalar,
- yatırımlar ve krediler,
- ürün/protokol piyasa kayıtları,
- kapasite tahsisleri,
- müşteri OS/uygulama geçmişleri,
- haber, olay, talep ve tick geçmişi.

Bu resetten sonra yeni sürümler mevcut veriyi koruyarak ilerlemelidir.

---

## 5. Şirketler ve portlar

| Şirket | Dil | Port | Başlıca ürünler |
|---|---|---:|---|
| Tunix | Rust | 7001 | Tunix OS, Tingram, Tmail, Tlink ve standart hizmet paketi |
| Mudaf | Go | 7002 | Oyuncunun kodladığı hizmetler, uygulamalar ve platformlar |
| Ugax | Node.js | 7003 | Oyuncunun kodladığı hizmetler, uygulamalar ve platformlar |
| İlos Tech | Python | 7004 | İSosyal, İMail, İLink ve standart hizmetler |

---

## 6. Destekleyici belgeler

- [Siber Saldırı Savunma Rehberi](SIBER_SALDIRI_SAVUNMA_REHBERI.md)
- [Şirket İşletim Merkezi 8090 Rehberi](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)
- [İlk 10 Hizmet Sözleşmesi](HIZMET_SOZLESMELERI_V2.md)
- [V8 Ceza, Uyum ve Panel Geçiş Rehberi](Dokumanlar/V8_CEZA_UYUM_KATALOG_VE_PANEL_GECIS_REHBERI.md)
- [V7 Finans ve Banka Rehberi](Dokumanlar/V7_FINANS_BANKA_SLA_PANOLAR_VE_TUNIX_HIZMETLERI.md)

Eski V6–V8 belgeleri tarihsel tasarım kararları için tutulur. Güncel davranışta önce V9.2, ardından V9.1 belgeleri esas alınır.

---

## 7. Güncelleme sonrası test

CMD:

```cmd
git pull --ff-only origin agent/tunix-matematik-topla
dotnet clean .\SirketMotoru\SirketMotoru.csproj
dotnet build .\SirketMotoru\SirketMotoru.csproj

cd /d Sirketler\Tunahan-Rustix\Tunix
cargo test
cd /d ..\..\..
```

İlk açılışta beklenen ana loglar:

```text
V9.2 SON TAM RESET tamamlandı
V9.2 ekosistem hazır
V9.2 olay merkezi hazır
Yazılım borsası V9.2 yayında
Şirket yönetim merkezi V9.2 yayında
V9.2 tick sistemi başladı
```

Motor derlemesi ve şirket testleri geçmeden V9.2 davranışı doğrulanmış kabul edilmez.
