mod protocol;
mod services;

use std::collections::{HashSet, VecDeque};
use std::io::{self, BufRead, BufReader, BufWriter, ErrorKind, Write};
use std::net::{TcpListener, TcpStream};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Mutex, OnceLock};
use std::thread;
use std::time::{Instant, SystemTime, UNIX_EPOCH};

use protocol::{
    DIZI_SIRALA, DIZI_SIRALA_SURUMU, FinansDurumuMesaji, IsIstegiMesaji,
    IsSonucuMesaji, KayitSonucuMesaji, MATEMATIK_ASAL_CARPANLAR,
    MATEMATIK_ASAL_CARPANLAR_SURUMU, MATEMATIK_CARP, MATEMATIK_CARP_SURUMU,
    MATEMATIK_TOPLA, MATEMATIK_TOPLA_SURUMU, METIN_FREKANS_ANALIZI,
    METIN_FREKANS_ANALIZI_SURUMU, METIN_KARAKTER_SAY, METIN_KARAKTER_SAY_SURUMU,
    METIN_KELIME_SAY, METIN_KELIME_SAY_SURUMU, MerhabaMesaji, MesajBasligi,
    PLATFORM_SURUMU, PROTOKOL_SURUMU, ProtokolYetkinligi, SIRKET_ADI,
    SIRKET_KIMLIGI, SUNUCU_SURUMU, SaglikKontroluMesaji, SaglikSonucuMesaji,
    SirketTanitimMesaji, SunulanHizmet, SunulanOzelProtokol, SunulanUygulama,
    TINGRAM_AKISI_GETIR, TINGRAM_ARAMA, TINGRAM_ETKILESIM,
    TINGRAM_GONDERI_OLUSTUR, TINGRAM_PROFIL_GETIR, TINGRAM_YORUM,
    TLINK_DOGRULA, TLINK_KIMLIK, TLINK_PAKETLE, TMAIL_ARA, TMAIL_EK_YUKLE,
    TMAIL_GELEN_KUTUSU, TMAIL_GONDER, TMAIL_KLASOR, TMAIL_SPAM_KONTROL,
    TUNIX_KIMLIK_DOGRULA, UygulamaBagimliligi, UygulamaOzelligi,
    VERI_MEDYAN_HESAPLA, VERI_MEDYAN_HESAPLA_SURUMU, VERI_ORTALAMA_HESAPLA,
    VERI_ORTALAMA_HESAPLA_SURUMU, VERI_STANDART_SAPMA,
    VERI_STANDART_SAPMA_SURUMU,
};
use rust_decimal::Decimal;
use serde::Serialize;

const SUNUCU_ADRESI: &str = "0.0.0.0:7001";
const AZAMI_MESAJ_BOYUTU_BYTE: usize = 65_536;
const GUVENLIK_REDDI_KODU: &str = "GUVENLIK_REDDI";
const TEKRAR_REDDI_KODU: &str = "KOTU_NIYETLI_ISTEK";
const TEKRAR_CACHE_KAPASITESI: usize = 5_000;

static MESAJ_SAYACI: AtomicU64 = AtomicU64::new(1);
static SON_FINANS_DURUMU: OnceLock<Mutex<Option<FinansDurumuMesaji>>> = OnceLock::new();
static TEKRAR_CACHE: OnceLock<Mutex<TekrarCache>> = OnceLock::new();

#[derive(Default)]
struct TekrarCache {
    sira: VecDeque<String>,
    kimlikler: HashSet<String>,
}

fn main() -> io::Result<()> {
    println!("========================================================");
    println!("TUNIX SERVER · TINGRAM · TMAIL · TLINK");
    println!("========================================================");

    let dinleyici = TcpListener::bind(SUNUCU_ADRESI)?;
    println!("Tunix sunucusu başlatıldı.");
    println!("Sunucu sürümü: {SUNUCU_SURUMU}");
    println!("Tunix 7001 portunda motoru bekliyor...");
    println!(
        "Yayın: {} hizmet · {} uygulama · {} özel protokol",
        sunulan_hizmetler().len(),
        sunulan_uygulamalar().len(),
        sunulan_ozel_protokoller().len()
    );

    for gelen_baglanti in dinleyici.incoming() {
        match gelen_baglanti {
            Ok(baglanti) => {
                println!("Motor Tunix sunucusuna bağlandı.");
                thread::spawn(move || {
                    if let Err(hata) = motor_baglantisini_yonet(baglanti) {
                        eprintln!("Motor bağlantısı hatası: {hata}");
                    }
                });
            }
            Err(hata) => eprintln!("Bağlantı kabul edilemedi: {hata}"),
        }
    }

    Ok(())
}

fn sunulan_hizmetler() -> Vec<SunulanHizmet> {
    vec![
        hizmet(MATEMATIK_TOPLA, MATEMATIK_TOPLA_SURUMU, 5, 2),
        hizmet(MATEMATIK_CARP, MATEMATIK_CARP_SURUMU, 8, 2),
        hizmet(VERI_ORTALAMA_HESAPLA, VERI_ORTALAMA_HESAPLA_SURUMU, 15, 2),
        hizmet(METIN_KELIME_SAY, METIN_KELIME_SAY_SURUMU, 7, 2),
        hizmet(METIN_KARAKTER_SAY, METIN_KARAKTER_SAY_SURUMU, 4, 2),
        hizmet(VERI_MEDYAN_HESAPLA, VERI_MEDYAN_HESAPLA_SURUMU, 22, 2),
        hizmet(VERI_STANDART_SAPMA, VERI_STANDART_SAPMA_SURUMU, 35, 2),
        hizmet(DIZI_SIRALA, DIZI_SIRALA_SURUMU, 28, 2),
        hizmet(MATEMATIK_ASAL_CARPANLAR, MATEMATIK_ASAL_CARPANLAR_SURUMU, 45, 2),
        hizmet(METIN_FREKANS_ANALIZI, METIN_FREKANS_ANALIZI_SURUMU, 32, 2),
        hizmet(TUNIX_KIMLIK_DOGRULA, PLATFORM_SURUMU, 3, 12),
        hizmet(TINGRAM_PROFIL_GETIR, PLATFORM_SURUMU, 5, 10),
        hizmet(TINGRAM_GONDERI_OLUSTUR, PLATFORM_SURUMU, 7, 8),
        hizmet(TINGRAM_AKISI_GETIR, PLATFORM_SURUMU, 6, 10),
        hizmet(TINGRAM_ETKILESIM, PLATFORM_SURUMU, 4, 12),
        hizmet(TINGRAM_YORUM, PLATFORM_SURUMU, 4, 10),
        hizmet(TINGRAM_ARAMA, PLATFORM_SURUMU, 6, 8),
        hizmet(TMAIL_GONDER, PLATFORM_SURUMU, 7, 10),
        hizmet(TMAIL_GELEN_KUTUSU, PLATFORM_SURUMU, 5, 12),
        hizmet(TMAIL_ARA, PLATFORM_SURUMU, 6, 10),
        hizmet(TMAIL_SPAM_KONTROL, PLATFORM_SURUMU, 4, 14),
        hizmet(TMAIL_EK_YUKLE, PLATFORM_SURUMU, 6, 8),
        hizmet(TMAIL_KLASOR, PLATFORM_SURUMU, 3, 14),
        hizmet(TLINK_KIMLIK, PLATFORM_SURUMU, 3, 16),
        hizmet(TLINK_PAKETLE, PLATFORM_SURUMU, 4, 16),
        hizmet(TLINK_DOGRULA, PLATFORM_SURUMU, 3, 16),
    ]
}

fn hizmet(hizmet_kimligi: &'static str, hizmet_surumu: &'static str, fiyat: i64, kapasite: u32) -> SunulanHizmet {
    SunulanHizmet {
        hizmet_kimligi,
        hizmet_surumu,
        birim_fiyat: Decimal::new(fiyat, 0),
        azami_eszamanli_is: kapasite,
        aktif: true,
    }
}

fn sunulan_uygulamalar() -> Vec<SunulanUygulama> {
    vec![
        SunulanUygulama {
            uygulama_kimligi: "tunix-tingram",
            uygulama_adi: "Tingram",
            surum: "1.0",
            kategori: "sosyal-medya",
            aciklama: "Tunix tarafından Rust ile geliştirilen hızlı sosyal medya platformu.",
            ozellikler: vec![
                ozellik("kimlik.dogrula", TUNIX_KIMLIK_DOGRULA, true),
                ozellik("sosyal.profil.getir", TINGRAM_PROFIL_GETIR, true),
                ozellik("sosyal.gonderi.olustur", TINGRAM_GONDERI_OLUSTUR, true),
                ozellik("sosyal.akisi.getir", TINGRAM_AKISI_GETIR, true),
                ozellik("sosyal.etkilesim", TINGRAM_ETKILESIM, true),
                ozellik("sosyal.yorum", TINGRAM_YORUM, false),
                ozellik("sosyal.arama", TINGRAM_ARAMA, false),
            ],
            bagimliliklar: vec![UygulamaBagimliligi {
                sirket_kimligi: SIRKET_KIMLIGI,
                uygulama_kimligi: "tunix-tmail",
                asgari_surum: "1.0",
                protokol_kimligi: "tunix-tlink",
                zorunlu: false,
            }],
            desteklenen_protokoller: vec!["tunix-tlink"],
        },
        SunulanUygulama {
            uygulama_kimligi: "tunix-tmail",
            uygulama_adi: "Tmail",
            surum: "1.0",
            kategori: "eposta",
            aciklama: "Tunix tarafından Rust ile geliştirilen spam korumalı e-posta platformu.",
            ozellikler: vec![
                ozellik("kimlik.dogrula", TUNIX_KIMLIK_DOGRULA, true),
                ozellik("eposta.gonder", TMAIL_GONDER, true),
                ozellik("eposta.gelen-kutusu", TMAIL_GELEN_KUTUSU, true),
                ozellik("eposta.ara", TMAIL_ARA, true),
                ozellik("eposta.spam-kontrol", TMAIL_SPAM_KONTROL, true),
                ozellik("eposta.ek-yukle", TMAIL_EK_YUKLE, false),
                ozellik("eposta.klasor", TMAIL_KLASOR, false),
            ],
            bagimliliklar: vec![],
            desteklenen_protokoller: vec!["tunix-tlink"],
        },
    ]
}

fn ozellik(ozellik_kimligi: &'static str, hizmet_kimligi: &'static str, zorunlu: bool) -> UygulamaOzelligi {
    UygulamaOzelligi {
        ozellik_kimligi,
        hizmet_kimligi,
        hizmet_surumu: PLATFORM_SURUMU,
        aciklama: "Gerçek Tunix sunucu hizmetine bağlı uygulama özelliği.",
        zorunlu,
    }
}

fn sunulan_ozel_protokoller() -> Vec<SunulanOzelProtokol> {
    vec![SunulanOzelProtokol {
        protokol_kimligi: "tunix-tlink",
        protokol_adi: "Tlink",
        surum: "1.0",
        aciklama: "Tunix uygulamaları ve şirketler arası sürümlü veri zarfı protokolü.",
        sema_kimligi: "tlink-json-envelope-v1",
        sema_ozeti: "protokol, sürüm, kaynak, hedef, veri ve bütünlük alanlarından oluşan JSON zarfı",
        yetkinlikler: vec![
            protokol_yetkinligi("kimlik", TLINK_KIMLIK),
            protokol_yetkinligi("paketle", TLINK_PAKETLE),
            protokol_yetkinligi("dogrula", TLINK_DOGRULA),
        ],
        uyumlu_protokoller: vec!["ilos-ilink"],
    }]
}

fn protokol_yetkinligi(yetkinlik_kimligi: &'static str, hizmet_kimligi: &'static str) -> ProtokolYetkinligi {
    ProtokolYetkinligi {
        yetkinlik_kimligi,
        hizmet_kimligi,
        hizmet_surumu: PLATFORM_SURUMU,
        aciklama: "Tlink protokolünün çalışan Tunix sunucu yetkinliği.",
    }
}

fn motor_baglantisini_yonet(baglanti: TcpStream) -> io::Result<()> {
    baglanti.set_nodelay(true)?;
    let mut okuyucu = BufReader::new(baglanti.try_clone()?);
    let mut yazici = BufWriter::new(baglanti);

    loop {
        let mut ham = Vec::with_capacity(1_024);
        let okunan = okuyucu.read_until(b'\n', &mut ham)?;
        if okunan == 0 { break; }
        if ham.len() > AZAMI_MESAJ_BOYUTU_BYTE + 1 {
            return Err(io::Error::new(ErrorKind::InvalidData, "Motorun gönderdiği mesaj azami boyutu aşıyor."));
        }
        while matches!(ham.last(), Some(b'\n' | b'\r')) { ham.pop(); }
        if ham.is_empty() { continue; }

        let gelen_mesaj = String::from_utf8(ham)
            .map_err(|hata| io::Error::new(ErrorKind::InvalidData, format!("Mesaj UTF-8 değil: {hata}")))?;
        let baslik: MesajBasligi = json_ayristir(&gelen_mesaj, "mesaj başlığı")?;
        match baslik.mesaj_turu.as_str() {
            "merhaba" => merhaba_mesajini_isle(&gelen_mesaj, &mut yazici)?,
            "kayitSonucu" => kayit_sonucunu_isle(&gelen_mesaj)?,
            "saglikKontrolu" => saglik_kontrolunu_isle(&gelen_mesaj, &mut yazici)?,
            "isIstegi" => is_istegini_isle(&gelen_mesaj, &mut yazici)?,
            "finansDurumu" => finans_durumunu_isle(&gelen_mesaj)?,
            bilinmeyen => eprintln!("Bilinmeyen mesaj türü yok sayıldı: {bilinmeyen}"),
        }
    }
    println!("Motor Tunix bağlantısını kapattı.");
    Ok(())
}

fn merhaba_mesajini_isle(gelen_mesaj: &str, yazici: &mut BufWriter<TcpStream>) -> io::Result<()> {
    let merhaba: MerhabaMesaji = json_ayristir(gelen_mesaj, "merhaba")?;
    if merhaba.protokol_surumu != PROTOKOL_SURUMU {
        return Err(io::Error::new(ErrorKind::InvalidData, format!("Desteklenmeyen protokol sürümü. Beklenen: {PROTOKOL_SURUMU}, gelen: {}", merhaba.protokol_surumu)));
    }
    println!("Motor kimliği: {}", merhaba.motor_kimligi);
    let tanitim_mesaji = SirketTanitimMesaji {
        mesaj_turu: "sirketTanitim",
        mesaj_kimligi: yeni_mesaj_kimligi(),
        protokol_surumu: PROTOKOL_SURUMU,
        sirket_kimligi: SIRKET_KIMLIGI,
        sirket_adi: SIRKET_ADI,
        sunucu_surumu: SUNUCU_SURUMU,
        hizmetler: sunulan_hizmetler(),
        uygulamalar: sunulan_uygulamalar(),
        ozel_protokoller: sunulan_ozel_protokoller(),
    };
    mesaj_gonder(yazici, &tanitim_mesaji)?;
    println!("Tunix hizmet, Tingram, Tmail ve Tlink manifestlerini motora gönderdi.");
    Ok(())
}

fn kayit_sonucunu_isle(gelen_mesaj: &str) -> io::Result<()> {
    let sonuc: KayitSonucuMesaji = json_ayristir(gelen_mesaj, "kayıt sonucu")?;
    if sonuc.basarili {
        println!("Tunix motor tarafından kaydedildi: {}", sonuc.aciklama);
        Ok(())
    } else {
        Err(io::Error::new(ErrorKind::PermissionDenied, format!("Tunix kaydı reddedildi: {}", sonuc.aciklama)))
    }
}

fn finans_durumunu_isle(gelen_mesaj: &str) -> io::Result<()> {
    let finans: FinansDurumuMesaji = json_ayristir(gelen_mesaj, "finans durumu")?;
    if finans.protokol_surumu != PROTOKOL_SURUMU {
        return Err(io::Error::new(ErrorKind::InvalidData, "Finans mesajı protokol sürümü uyuşmuyor."));
    }
    if finans.sirket_kimligi != SIRKET_KIMLIGI {
        return Err(io::Error::new(ErrorKind::PermissionDenied, "Finans mesajı başka şirkete ait."));
    }
    println!("Tunix finans · Tick: {} · Kasa: {} · Net: {} · Gelir: {} · Ceza: {}", finans.tick_numarasi, finans.kasa, finans.net_gelir, finans.toplam_gelir, finans.toplam_ceza);
    let kilit = SON_FINANS_DURUMU.get_or_init(|| Mutex::new(None));
    let mut son_durum = kilit.lock().map_err(|_| io::Error::other("Finans durumu kilidi zehirlendi."))?;
    *son_durum = Some(finans);
    Ok(())
}

fn saglik_kontrolunu_isle(gelen_mesaj: &str, yazici: &mut BufWriter<TcpStream>) -> io::Result<()> {
    let kontrol: SaglikKontroluMesaji = json_ayristir(gelen_mesaj, "sağlık kontrolü")?;
    mesaj_gonder(yazici, &SaglikSonucuMesaji {
        mesaj_turu: "saglikSonucu",
        mesaj_kimligi: yeni_mesaj_kimligi(),
        protokol_surumu: PROTOKOL_SURUMU,
        istek_kimligi: kontrol.istek_kimligi,
        durum: "calisiyor",
        aktif_baglanti: 1,
        kuyruk_uzunlugu: 0,
    })
}

fn is_istegini_isle(gelen_mesaj: &str, yazici: &mut BufWriter<TcpStream>) -> io::Result<()> {
    let istek: IsIstegiMesaji = json_ayristir(gelen_mesaj, "iş isteği")?;
    let baslangic = Instant::now();

    if kotu_niyetli_istek_mi(&istek.istek_verisi_json) {
        let sure = baslangic.elapsed().as_secs_f64() * 1_000.0;
        eprintln!("GÜVENLİK REDDİ | İş: {} | Hizmet: {}@{} | Süre: {:.3} ms", istek.is_kimligi, istek.hizmet_kimligi, istek.hizmet_surumu, sure);
        return mesaj_gonder(yazici, &IsSonucuMesaji {
            mesaj_turu: "isSonucu",
            istek_kimligi: istek.istek_kimligi,
            is_kimligi: istek.is_kimligi,
            sirket_kimligi: SIRKET_KIMLIGI,
            basarili: false,
            sonuc_verisi_json: "{}".to_string(),
            hata_kodu: Some(GUVENLIK_REDDI_KODU),
            hata_mesaji: Some("Motor imzalı kötü niyetli istek Tunix güvenlik katmanı tarafından engellendi.".to_string()),
            islem_suresi_ms: sure,
        });
    }

    if !is_kimligini_kaydet(&istek.is_kimligi)? {
        let sure = baslangic.elapsed().as_secs_f64() * 1_000.0;
        return mesaj_gonder(yazici, &IsSonucuMesaji {
            mesaj_turu: "isSonucu",
            istek_kimligi: istek.istek_kimligi,
            is_kimligi: istek.is_kimligi,
            sirket_kimligi: SIRKET_KIMLIGI,
            basarili: false,
            sonuc_verisi_json: "{}".to_string(),
            hata_kodu: Some(TEKRAR_REDDI_KODU),
            hata_mesaji: Some("Tekrarlanan iş kimliği güvenli biçimde reddedildi.".to_string()),
            islem_suresi_ms: sure,
        });
    }

    let hizmet_sonucu = services::hizmeti_calistir(&istek.hizmet_kimligi, &istek.hizmet_surumu, &istek.istek_verisi_json);
    let islem_suresi_ms = baslangic.elapsed().as_secs_f64() * 1_000.0;
    let sonuc_mesaji = match hizmet_sonucu {
        Ok(sonuc_verisi_json) => {
            println!("İş tamamlandı | İş: {} | Hizmet: {}@{} | Süre: {:.3} ms", istek.is_kimligi, istek.hizmet_kimligi, istek.hizmet_surumu, islem_suresi_ms);
            IsSonucuMesaji {
                mesaj_turu: "isSonucu",
                istek_kimligi: istek.istek_kimligi,
                is_kimligi: istek.is_kimligi,
                sirket_kimligi: SIRKET_KIMLIGI,
                basarili: true,
                sonuc_verisi_json,
                hata_kodu: None,
                hata_mesaji: None,
                islem_suresi_ms,
            }
        }
        Err(hata) => {
            let hata_kodu = hata.kodu();
            let hata_mesaji = hata.mesaji();
            eprintln!("İş başarısız | İş: {} | Kod: {} | Sebep: {}", istek.is_kimligi, hata_kodu, hata_mesaji);
            IsSonucuMesaji {
                mesaj_turu: "isSonucu",
                istek_kimligi: istek.istek_kimligi,
                is_kimligi: istek.is_kimligi,
                sirket_kimligi: SIRKET_KIMLIGI,
                basarili: false,
                sonuc_verisi_json: "{}".to_string(),
                hata_kodu: Some(hata_kodu),
                hata_mesaji: Some(hata_mesaji),
                islem_suresi_ms,
            }
        }
    };
    mesaj_gonder(yazici, &sonuc_mesaji)
}

fn kotu_niyetli_istek_mi(istek_verisi_json: &str) -> bool {
    let Ok(deger) = serde_json::from_str::<serde_json::Value>(istek_verisi_json) else { return false; };
    deger.as_object()
        .and_then(|kok| kok.get("_guvenlikSinamasi"))
        .and_then(serde_json::Value::as_object)
        .and_then(|sinama| sinama.get("etiket"))
        .and_then(serde_json::Value::as_str)
        .is_some_and(|etiket| etiket == "motor-saldiri-v1")
}

fn is_kimligini_kaydet(is_kimligi: &str) -> io::Result<bool> {
    let kilit = TEKRAR_CACHE.get_or_init(|| Mutex::new(TekrarCache::default()));
    let mut cache = kilit.lock().map_err(|_| io::Error::other("Tekrar cache kilidi zehirlendi."))?;
    if cache.kimlikler.contains(is_kimligi) { return Ok(false); }
    let kimlik = is_kimligi.to_string();
    cache.kimlikler.insert(kimlik.clone());
    cache.sira.push_back(kimlik);
    while cache.sira.len() > TEKRAR_CACHE_KAPASITESI {
        if let Some(eski) = cache.sira.pop_front() { cache.kimlikler.remove(&eski); }
    }
    Ok(true)
}

fn json_ayristir<T>(json: &str, mesaj_adi: &str) -> io::Result<T>
where T: serde::de::DeserializeOwned {
    serde_json::from_str(json).map_err(|hata| io::Error::new(ErrorKind::InvalidData, format!("{mesaj_adi} ayrıştırılamadı: {hata}")))
}

fn mesaj_gonder<T>(yazici: &mut BufWriter<TcpStream>, mesaj: &T) -> io::Result<()>
where T: Serialize {
    let json = serde_json::to_string(mesaj).map_err(|hata| io::Error::new(ErrorKind::InvalidData, format!("Gönderilecek mesaj JSON'a dönüştürülemedi: {hata}")))?;
    if json.len() > AZAMI_MESAJ_BOYUTU_BYTE {
        return Err(io::Error::new(ErrorKind::InvalidData, "Gönderilecek mesaj azami boyutu aşıyor."));
    }
    if json.len() <= 2_048 { println!("Motora gönderilen mesaj: {json}"); }
    else { println!("Motora büyük mesaj gönderildi | Boyut: {} byte", json.len()); }
    yazici.write_all(json.as_bytes())?;
    yazici.write_all(b"\n")?;
    yazici.flush()
}

fn yeni_mesaj_kimligi() -> String {
    let sayac = MESAJ_SAYACI.fetch_add(1, Ordering::Relaxed);
    let zaman = SystemTime::now().duration_since(UNIX_EPOCH).unwrap_or_default().as_nanos();
    format!("tunix-{zaman}-{sayac}")
}
