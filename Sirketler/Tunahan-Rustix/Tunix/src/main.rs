mod protocol;
mod services;
mod v6_manifest;

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
    VERI_MEDYAN_HESAPLA, VERI_MEDYAN_HESAPLA_SURUMU, VERI_ORTALAMA_HESAPLA,
    VERI_ORTALAMA_HESAPLA_SURUMU, VERI_STANDART_SAPMA,
    VERI_STANDART_SAPMA_SURUMU,
};
use rust_decimal::Decimal;
use serde::Serialize;

const SUNUCU_ADRESI: &str = "0.0.0.0:7001";
const AZAMI_MESAJ_BOYUTU_BYTE: usize = 65_536;
const TEKRAR_CACHE_KAPASITESI: usize = 5_000;
const GUVENLIK_REDDI: &str = "GUVENLIK_REDDI";
const TEKRAR_REDDI: &str = "KOTU_NIYETLI_ISTEK";

static MESAJ_SAYACI: AtomicU64 = AtomicU64::new(1);
static SON_FINANS: OnceLock<Mutex<Option<FinansDurumuMesaji>>> = OnceLock::new();
static TEKRAR_CACHE: OnceLock<Mutex<TekrarCache>> = OnceLock::new();

#[derive(Default)]
struct TekrarCache {
    sira: VecDeque<String>,
    kimlikler: HashSet<String>,
}

fn main() -> io::Result<()> {
    println!("========================================================");
    println!("TUNIX OS · TINGRAM · TMAIL · TLINK");
    println!("========================================================");
    println!("Sunucu sürümü: {SUNUCU_SURUMU}");
    println!("Adres: {SUNUCU_ADRESI}");
    println!(
        "Yayın: {} hizmet · {} uygulama · {} protokol",
        sunulan_hizmetler().len(),
        sunulan_uygulamalar().len(),
        sunulan_protokoller().len()
    );

    let dinleyici = TcpListener::bind(SUNUCU_ADRESI)?;
    for gelen in dinleyici.incoming() {
        match gelen {
            Ok(baglanti) => {
                thread::spawn(move || {
                    if let Err(hata) = baglantiyi_yonet(baglanti) {
                        eprintln!("Motor bağlantısı kapandı: {hata}");
                    }
                });
            }
            Err(hata) => eprintln!("Bağlantı kabul edilemedi: {hata}"),
        }
    }
    Ok(())
}

fn sunulan_hizmetler() -> Vec<SunulanHizmet> {
    let mut hizmetler = vec![
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
    ];
    hizmetler.extend(v6_manifest::standart_hizmetler());
    // Tlink yetkinlikleri de motorun standart hizmet kataloğundadır.
    hizmetler.push(hizmet("kimlik.token-dogrula", PLATFORM_SURUMU, 3, 20));
    hizmetler.push(hizmet("dosya.sikistir", PLATFORM_SURUMU, 4, 18));
    hizmetler.push(hizmet("guvenlik.baglanti-dogrula", PLATFORM_SURUMU, 3, 20));

    let mut gorulen = HashSet::new();
    hizmetler.retain(|h| gorulen.insert((h.hizmet_kimligi, h.hizmet_surumu)));
    hizmetler
}

fn hizmet(
    kimlik: &'static str,
    surum: &'static str,
    fiyat: i64,
    kapasite: u32,
) -> SunulanHizmet {
    SunulanHizmet {
        hizmet_kimligi: kimlik,
        hizmet_surumu: surum,
        birim_fiyat: Decimal::new(fiyat, 0),
        azami_eszamanli_is: kapasite,
        aktif: true,
    }
}

fn sunulan_uygulamalar() -> Vec<SunulanUygulama> {
    v6_manifest::uygulamalar()
}

fn sunulan_protokoller() -> Vec<SunulanOzelProtokol> {
    vec![SunulanOzelProtokol {
        protokol_kimligi: "tunix-tlink",
        protokol_adi: "Tlink",
        surum: "1.0",
        aciklama: "Tunix OS, uygulamalar ve şirketler arası sürümlü veri zarfı protokolü.",
        sema_kimligi: "tlink-json-envelope-v1",
        sema_ozeti: "protokol, sürüm, kaynak, hedef, veri ve bütünlük alanları",
        yetkinlikler: vec![
            protokol_yetkinligi("kimlik", "kimlik.token-dogrula"),
            protokol_yetkinligi("paketle", "dosya.sikistir"),
            protokol_yetkinligi("dogrula", "guvenlik.baglanti-dogrula"),
        ],
        uyumlu_protokoller: vec!["ilos-ilink"],
    }]
}

fn protokol_yetkinligi(
    kimlik: &'static str,
    hizmet: &'static str,
) -> ProtokolYetkinligi {
    ProtokolYetkinligi {
        yetkinlik_kimligi: kimlik,
        hizmet_kimligi: hizmet,
        hizmet_surumu: PLATFORM_SURUMU,
        aciklama: "Motor V6 standart hizmetine bağlı çalışan Tlink yetkinliği.",
    }
}

fn baglantiyi_yonet(baglanti: TcpStream) -> io::Result<()> {
    baglanti.set_nodelay(true)?;
    let mut okuyucu = BufReader::new(baglanti.try_clone()?);
    let mut yazici = BufWriter::new(baglanti);

    loop {
        let mut ham = Vec::with_capacity(1024);
        let okunan = okuyucu.read_until(b'\n', &mut ham)?;
        if okunan == 0 {
            break;
        }
        if ham.len() > AZAMI_MESAJ_BOYUTU_BYTE + 1 {
            return Err(io::Error::new(
                ErrorKind::InvalidData,
                "Gelen mesaj boyut sınırını aşıyor.",
            ));
        }
        while ham
            .last()
            .is_some_and(|byte| *byte == b'\n' || *byte == b'\r')
        {
            ham.pop();
        }
        if ham.is_empty() {
            continue;
        }

        let satir = String::from_utf8(ham).map_err(|hata| {
            io::Error::new(ErrorKind::InvalidData, format!("Mesaj UTF-8 değil: {hata}"))
        })?;
        let baslik: MesajBasligi = json_ayristir(&satir, "mesaj başlığı")?;
        match baslik.mesaj_turu.as_str() {
            "merhaba" => merhaba_isle(&satir, &mut yazici)?,
            "kayitSonucu" => kayit_sonucu_isle(&satir)?,
            "saglikKontrolu" => saglik_isle(&satir, &mut yazici)?,
            "isIstegi" => is_istegi_isle(&satir, &mut yazici)?,
            "finansDurumu" => finans_isle(&satir)?,
            bilinmeyen => eprintln!("Bilinmeyen mesaj türü: {bilinmeyen}"),
        }
    }
    Ok(())
}

fn merhaba_isle(
    satir: &str,
    yazici: &mut BufWriter<TcpStream>,
) -> io::Result<()> {
    let merhaba: MerhabaMesaji = json_ayristir(satir, "merhaba")?;
    if merhaba.protokol_surumu != PROTOKOL_SURUMU {
        return Err(io::Error::new(
            ErrorKind::InvalidData,
            "Motor protokol sürümü uyumsuz.",
        ));
    }

    let mesaj = SirketTanitimMesaji {
        mesaj_turu: "sirketTanitim",
        mesaj_kimligi: yeni_mesaj_kimligi(),
        protokol_surumu: PROTOKOL_SURUMU,
        sirket_kimligi: SIRKET_KIMLIGI,
        sirket_adi: SIRKET_ADI,
        sunucu_surumu: SUNUCU_SURUMU,
        hizmetler: sunulan_hizmetler(),
        uygulamalar: sunulan_uygulamalar(),
        ozel_protokoller: sunulan_protokoller(),
    };
    mesaj_gonder(yazici, &mesaj)
}

fn kayit_sonucu_isle(satir: &str) -> io::Result<()> {
    let sonuc: KayitSonucuMesaji = json_ayristir(satir, "kayıt sonucu")?;
    if sonuc.basarili {
        println!("Motor kaydı başarılı: {}", sonuc.aciklama);
        Ok(())
    } else {
        Err(io::Error::new(
            ErrorKind::PermissionDenied,
            sonuc.aciklama,
        ))
    }
}

fn saglik_isle(
    satir: &str,
    yazici: &mut BufWriter<TcpStream>,
) -> io::Result<()> {
    let kontrol: SaglikKontroluMesaji = json_ayristir(satir, "sağlık kontrolü")?;
    mesaj_gonder(
        yazici,
        &SaglikSonucuMesaji {
            mesaj_turu: "saglikSonucu",
            mesaj_kimligi: yeni_mesaj_kimligi(),
            protokol_surumu: PROTOKOL_SURUMU,
            istek_kimligi: kontrol.istek_kimligi,
            durum: "calisiyor",
            aktif_baglanti: 1,
            kuyruk_uzunlugu: 0,
        },
    )
}

fn is_istegi_isle(
    satir: &str,
    yazici: &mut BufWriter<TcpStream>,
) -> io::Result<()> {
    let istek: IsIstegiMesaji = json_ayristir(satir, "iş isteği")?;
    let baslangic = Instant::now();
    let istek_kimligi = istek.istek_kimligi.clone();
    let is_kimligi = istek.is_kimligi.clone();

    if saldiri_imzasi_var_mi(&istek.istek_verisi_json) {
        return sonuc_gonder(
            yazici,
            istek_kimligi,
            is_kimligi,
            false,
            "{}".to_string(),
            Some(GUVENLIK_REDDI),
            Some("Motor imzalı saldırı Tunix güvenlik kapısında engellendi.".to_string()),
            baslangic,
        );
    }

    if !is_kimligini_kaydet(&istek.is_kimligi)? {
        return sonuc_gonder(
            yazici,
            istek_kimligi,
            is_kimligi,
            false,
            "{}".to_string(),
            Some(TEKRAR_REDDI),
            Some("Tekrarlanan iş kimliği reddedildi.".to_string()),
            baslangic,
        );
    }

    match services::hizmeti_calistir(
        &istek.hizmet_kimligi,
        &istek.hizmet_surumu,
        &istek.istek_verisi_json,
    ) {
        Ok(sonuc) => sonuc_gonder(
            yazici,
            istek_kimligi,
            is_kimligi,
            true,
            sonuc,
            None,
            None,
            baslangic,
        ),
        Err(hata) => sonuc_gonder(
            yazici,
            istek_kimligi,
            is_kimligi,
            false,
            "{}".to_string(),
            Some(hata.kodu()),
            Some(hata.mesaji()),
            baslangic,
        ),
    }
}

#[allow(clippy::too_many_arguments)]
fn sonuc_gonder(
    yazici: &mut BufWriter<TcpStream>,
    istek_kimligi: String,
    is_kimligi: String,
    basarili: bool,
    sonuc_verisi_json: String,
    hata_kodu: Option<&'static str>,
    hata_mesaji: Option<String>,
    baslangic: Instant,
) -> io::Result<()> {
    mesaj_gonder(
        yazici,
        &IsSonucuMesaji {
            mesaj_turu: "isSonucu",
            istek_kimligi,
            is_kimligi,
            sirket_kimligi: SIRKET_KIMLIGI,
            basarili,
            sonuc_verisi_json,
            hata_kodu,
            hata_mesaji,
            islem_suresi_ms: baslangic.elapsed().as_secs_f64() * 1000.0,
        },
    )
}

fn finans_isle(satir: &str) -> io::Result<()> {
    let finans: FinansDurumuMesaji = json_ayristir(satir, "finans durumu")?;
    if finans.protokol_surumu != PROTOKOL_SURUMU
        || finans.sirket_kimligi != SIRKET_KIMLIGI
    {
        return Err(io::Error::new(
            ErrorKind::PermissionDenied,
            "Finans mesajı doğrulanamadı.",
        ));
    }
    println!(
        "Finans · Tick {} · Kasa {} · Net {} · Ceza {}",
        finans.tick_numarasi,
        finans.kasa,
        finans.net_gelir,
        finans.toplam_ceza
    );
    *SON_FINANS
        .get_or_init(|| Mutex::new(None))
        .lock()
        .map_err(|_| io::Error::other("Finans kilidi kullanılamıyor."))? = Some(finans);
    Ok(())
}

fn saldiri_imzasi_var_mi(json_metni: &str) -> bool {
    let Ok(deger) = serde_json::from_str::<serde_json::Value>(json_metni) else {
        return false;
    };
    deger
        .as_object()
        .and_then(|kok| kok.get("_guvenlikSinamasi"))
        .and_then(serde_json::Value::as_object)
        .and_then(|sinama| sinama.get("etiket"))
        .and_then(serde_json::Value::as_str)
        .is_some_and(|etiket| etiket == "motor-saldiri-v1")
}

fn is_kimligini_kaydet(kimlik: &str) -> io::Result<bool> {
    let mut cache = TEKRAR_CACHE
        .get_or_init(|| Mutex::new(TekrarCache::default()))
        .lock()
        .map_err(|_| io::Error::other("Tekrar cache kilidi kullanılamıyor."))?;
    if cache.kimlikler.contains(kimlik) {
        return Ok(false);
    }
    let yeni = kimlik.to_string();
    cache.kimlikler.insert(yeni.clone());
    cache.sira.push_back(yeni);
    while cache.sira.len() > TEKRAR_CACHE_KAPASITESI {
        if let Some(eski) = cache.sira.pop_front() {
            cache.kimlikler.remove(&eski);
        }
    }
    Ok(true)
}

fn json_ayristir<T>(json: &str, ad: &str) -> io::Result<T>
where
    T: serde::de::DeserializeOwned,
{
    serde_json::from_str(json).map_err(|hata| {
        io::Error::new(
            ErrorKind::InvalidData,
            format!("{ad} ayrıştırılamadı: {hata}"),
        )
    })
}

fn mesaj_gonder<T>(
    yazici: &mut BufWriter<TcpStream>,
    mesaj: &T,
) -> io::Result<()>
where
    T: Serialize,
{
    let json = serde_json::to_string(mesaj).map_err(|hata| {
        io::Error::new(
            ErrorKind::InvalidData,
            format!("Mesaj serileştirilemedi: {hata}"),
        )
    })?;
    if json.len() > AZAMI_MESAJ_BOYUTU_BYTE {
        return Err(io::Error::new(
            ErrorKind::InvalidData,
            "Gönderilecek mesaj boyut sınırını aşıyor.",
        ));
    }
    if json.len() > 2048 {
        println!("Motora büyük mesaj gönderildi · {} byte", json.len());
    }
    yazici.write_all(json.as_bytes())?;
    yazici.write_all(b"\n")?;
    yazici.flush()
}

fn yeni_mesaj_kimligi() -> String {
    let sayac = MESAJ_SAYACI.fetch_add(1, Ordering::Relaxed);
    let zaman = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_nanos();
    format!("tunix-{zaman}-{sayac}")
}
