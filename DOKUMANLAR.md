# Proje Dokumanlari

Bu dosya, Uc Kardes Yazilim Sirketi Simulasyonu icin okunmasi gereken teknik belgelerin ana indeksidir.

---

## Hizli okuma sirasi

### Oyuna yeni katilan sirket gelistiricisi

1. [SUNUCU_GUNCELLEME_V2_REHBERI.md](SUNUCU_GUNCELLEME_V2_REHBERI.md)
2. [HIZMET_SOZLESMELERI_V2.md](HIZMET_SOZLESMELERI_V2.md)
3. [SIBER_SALDIRI_SAVUNMA_REHBERI.md](SIBER_SALDIRI_SAVUNMA_REHBERI.md)
4. [UYGULAMA_MANIFESTI_V1.md](UYGULAMA_MANIFESTI_V1.md)
5. [SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)

### Motoru calistiran kisi

1. [YAZILIM_BORSASI_V3_GUNCELLEME_REHBERI.md](YAZILIM_BORSASI_V3_GUNCELLEME_REHBERI.md)
2. [SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)
3. [SIBER_SALDIRI_SAVUNMA_REHBERI.md](SIBER_SALDIRI_SAVUNMA_REHBERI.md)
4. [UYGULAMA_MANIFESTI_V1.md](UYGULAMA_MANIFESTI_V1.md)

---

## Ana belgeler

### [YAZILIM_BORSASI_V3_GUNCELLEME_REHBERI.md](YAZILIM_BORSASI_V3_GUNCELLEME_REHBERI.md)

Butun yeni oyun sistemlerinin tek kaynak ozeti:

- 5.000 musteri
- 10 temel hizmet
- Kalite/performans/guvenlik puanlari
- Saldiri ekonomisi
- 8080 ve 8090
- Kod tabanli uygulamalar
- Ozel protokoller
- Abonelik urunleri
- Yatirim ve kredi
- SLA sozlesmeleri
- Kalici veri mimarisi
- Gecis ve test plani
- Mevcut sinirlar

### [SIBER_SALDIRI_SAVUNMA_REHBERI.md](SIBER_SALDIRI_SAVUNMA_REHBERI.md)

Sirketlerin saldiri yemesini onlemek icin uygulama rehberi:

- Motor saldiri imzasi
- Saldiri dalgalari ve olasiliklar
- Bes kontrollu saldiri turu
- Kabul edilen guvenlik hata kodlari
- Finansal ve puan cezalari
- Rust, Go ve Node.js ornekleri
- Merkezi guvenlik katmani
- Boyut, tekrar ve zaman limitleri
- Test ve teshis tablolari

### [SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md](SIRKET_ISLETIM_MERKEZI_8090_REHBERI.md)

8090 sirket panelinin tam kullanim ve mimari rehberi:

- Kodlama ile yonetim arasindaki kesin sinir
- Giris ve oturum
- Hizmet fiyat/aktiflik yonetimi
- Kodlanmis uygulama yayini
- Uygulama kapasitesi
- Ozel protokol piyasasi
- Altyapi yatirimlari
- Banka ve kredi
- SLA sozlesmeleri
- HTTP uclari
- Kalici veri dosyalari

### [UYGULAMA_MANIFESTI_V1.md](UYGULAMA_MANIFESTI_V1.md)

Sirket sunucularinin yeni uygulama ve protokol ilan standardi:

- `sirketTanitim` yeni alanlari
- Uygulama manifesti
- Uygulama ozelligi-hizmet baglantisi
- Sosyal medya, e-posta, mesajlasma ve diger standartlar
- Uygulamalar arasi bagimliliklar
- Ozel protokol manifesti
- Tam JSON ornekleri

### [SUNUCU_GUNCELLEME_V2_REHBERI.md](SUNUCU_GUNCELLEME_V2_REHBERI.md)

Rust, Go ve Node.js sirket sunucularinin motor v2 ile uyum rehberi:

- Ham TCP protokolu
- Baglanti akisi
- Mesaj semalari
- 10 hizmet kimligi
- Sonuc formati
- Mudaf ve Ugax icin dikkat noktalar
- Son teslim kontrol listesi

### [HIZMET_SOZLESMELERI_V2.md](HIZMET_SOZLESMELERI_V2.md)

Motorun bagimsiz dogruladigi 10 temel hizmetin kesin teknik sozlesmeleri:

- Girdi JSON'u
- Cikti JSON'u
- Bos veri davranisi
- Ondalik hassasiyeti
- Siralama yonu
- Asal carpan dogrulamasi
- Metin ve karakter kurallari

---

## Portlar

| Port | Sistem | Amac |
|---:|---|---|
| 7001 | Tunix | Rust sirket sunucusu |
| 7002 | Mudaf | Go sirket sunucusu |
| 7003 | Ugax | Node.js sirket sunucusu |
| 8080 | Canli borsa | Piyasa ve sirket durumunu izleme |
| 8090 | Sirket Isletim Merkezi | Finansal ve ticari yonetim |

---

## Temel kural

```text
Kod ve iscilik sirket sunucusunda yapilir.
Motor teknik sonucu ve piyasa davranisini dogrular.
8080 izleme ekranidir.
8090 ticari ve finansal yonetim ekranidir.
8090 hizmet, uygulama ozelligi veya protokol kodu uretmez.
```

---

## Guncelleme sonrasi asgari kontrol

```powershell
git pull --ff-only origin agent/tunix-matematik-topla
dotnet build .\SirketMotoru\SirketMotoru.csproj
dotnet run --project .\SirketMotoru\SirketMotoru.csproj
```

Her sirket kendi klasorunde derleme ve testini ayrica yapmalidir.

---

## Belge bakim kurali

Motor protokolu, hizmet semasi, uygulama standardi, saldiri davranisi veya 8090 uclari degistiginde ilgili belge ayni commit serisinde guncellenmelidir. Kod ile dokuman birbiriyle celismemelidir.
