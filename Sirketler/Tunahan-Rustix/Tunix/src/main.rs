mod protocol;
mod services;

use std::io::{self, BufRead, BufReader, BufWriter, ErrorKind, Write};
use std::net::{TcpListener, TcpStream};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Mutex, OnceLock};
use std::thread;
use std::time::{Instant, SystemTime, UNIX_EPOCH};

use protocol::{
    FinansDurumuMesaji, IsIstegiMesaji, IsSonucuMesaji, KayitSonucuMesaji,
    MATEMATIK_CARP, MATEMATIK_CARP_SURUMU, MATEMATIK_TOPLA, MATEMATIK_TOPLA_SURUMU,
    METIN_KARAKTER_SAY, METIN_KARAKTER_SAY_SURUMU, METIN_KELIME_SAY,
    METIN_KELIME_SAY_SURUMU, MerhabaMesaji, MesajBasligi, PROTOKOL_SURUMU, SIRKET_ADI,
    SIRKET_KIMLIGI, SUNUCU_SURUMU, SaglikKontroluMesaji, SaglikSonucuMesaji,
    SirketTanitimMesaji, SunulanHizmet, VERI_ORTALAMA_HESAPLA,
    VERI_ORTALAMA_HESAPLA_SURUMU,
};
use rust_decimal::Decimal;
use serde::Serialize;

const SUNUCU_ADRESI: &str = "0.0.0.0:7001";
const AZAMI_MESAJ_BOYUTU_BYTE: usize = 65_536;
static MESAJ_SAYACI: AtomicU64 = AtomicU64::new(1);
static SON_FINANS_DURUMU: OnceLock<Mutex<Option<FinansDurumuMesaji>>> = OnceLock::new();

fn main() -> io::Result<()> {
    println!("================================");
    println!("          TUNIX SERVER");
    println!("================================");

    let dinleyici = TcpListener::bind(SUNUCU_ADRESI)?;
    println!("Tunix sunucusu başlatıldı.");
    println!("Sunucu sürümü: {SUNUCU_SURUMU}");
    println!("Tunix 7001 portunda motoru bekliyor...");
    for (kimlik, surum, fiyat) in [
        (
            MATEMATIK_TOPLA,
            MATEMATIK_TOPLA_SURUMU,
            Decimal::new(5, 0),
        ),
        (
            MATEMATIK_CARP,
            MATEMATIK_CARP_SURUMU,
            Decimal::new(8, 0),
        ),
        (
            VERI_ORTALAMA_HESAPLA,
            VERI_ORTALAMA_HESAPLA_SURUMU,
            Decimal::new(15, 0),
        ),
        (
            METIN_KELIME_SAY,
            METIN_KELIME_SAY_SURUMU,
            Decimal::new(7, 0),
        ),
        (
            METIN_KARAKTER_SAY,
            METIN_KARAKTER_SAY_SURUMU,
            Decimal::new(4, 0),
        ),
    ] {
        println!("Yayınlanan hizmet: {kimlik}@{surum} | Fiyat: {fiyat} TL");
    }
    println!();

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

fn motor_baglantisini_yonet(baglanti: TcpStream) -> io::Result<()> {
    baglanti.set_nodelay(true)?;
    let okuyucu = BufReader::new(baglanti.try_clone()?);
    let mut yazici = BufWriter::new(baglanti);

    for gelen_satir in okuyucu.lines() {
        let gelen_mesaj = gelen_satir?;
        if gelen_mesaj.trim().is_empty() {
            continue;
        }
        if gelen_mesaj.len() > AZAMI_MESAJ_BOYUTU_BYTE {
            return Err(io::Error::new(
                ErrorKind::InvalidData,
                "Motorun gönderdiği mesaj azami boyutu aşıyor.",
            ));
        }

        let baslik: MesajBasligi = json_ayristir(&gelen_mesaj, "mesaj başlığı")?;
        println!("Motordan mesaj geldi: {}", baslik.mesaj_turu);

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

fn merhaba_mesajini_isle(
    gelen_mesaj: &str,
    yazici: &mut BufWriter<TcpStream>,
) -> io::Result<()> {
    let merhaba: MerhabaMesaji = json_ayristir(gelen_mesaj, "merhaba")?;
    if merhaba.protokol_surumu != PROTOKOL_SURUMU {
        return Err(io::Error::new(
            ErrorKind::InvalidData,
            format!(
                "Desteklenmeyen protokol sürümü. Beklenen: {PROTOKOL_SURUMU}, gelen: {}",
                merhaba.protokol_surumu
            ),
        ));
    }

    println!("Motor kimliği: {}", merhaba.motor_kimligi);
    let tanitim_mesaji = SirketTanitimMesaji {
        mesaj_turu: "sirketTanitim",
        mesaj_kimligi: yeni_mesaj_kimligi(),
        protokol_surumu: PROTOKOL_SURUMU,
        sirket_kimligi: SIRKET_KIMLIGI,
        sirket_adi: SIRKET_ADI,
        sunucu_surumu: SUNUCU_SURUMU,
        hizmetler: vec![
            hizmet(
                MATEMATIK_TOPLA,
                MATEMATIK_TOPLA_SURUMU,
                Decimal::new(5, 0),
            ),
            hizmet(
                MATEMATIK_CARP,
                MATEMATIK_CARP_SURUMU,
                Decimal::new(8, 0),
            ),
            hizmet(
                VERI_ORTALAMA_HESAPLA,
                VERI_ORTALAMA_HESAPLA_SURUMU,
                Decimal::new(15, 0),
            ),
            hizmet(
                METIN_KELIME_SAY,
                METIN_KELIME_SAY_SURUMU,
                Decimal::new(7, 0),
            ),
            hizmet(
                METIN_KARAKTER_SAY,
                METIN_KARAKTER_SAY_SURUMU,
                Decimal::new(4, 0),
            ),
        ],
    };

    mesaj_gonder(yazici, &tanitim_mesaji)?;
    println!("Tunix tanıtım ve hizmet ilanı motora gönderildi.");
    Ok(())
}

fn hizmet(
    hizmet_kimligi: &'static str,
    hizmet_surumu: &'static str,
    birim_fiyat: Decimal,
) -> SunulanHizmet {
    SunulanHizmet {
        hizmet_kimligi,
        hizmet_surumu,
        birim_fiyat,
        azami_eszamanli_is: 1,
        aktif: true,
    }
}

fn kayit_sonucunu_isle(gelen_mesaj: &str) -> io::Result<()> {
    let sonuc: KayitSonucuMesaji = json_ayristir(gelen_mesaj, "kayıt sonucu")?;
    if sonuc.basarili {
        println!("Tunix motor tarafından kaydedildi: {}", sonuc.aciklama);
        Ok(())
    } else {
        Err(io::Error::new(
            ErrorKind::PermissionDenied,
            format!("Tunix kaydı reddedildi: {}", sonuc.aciklama),
        ))
    }
}

fn finans_durumunu_isle(gelen_mesaj: &str) -> io::Result<()> {
    let finans: FinansDurumuMesaji = json_ayristir(gelen_mesaj, "finans durumu")?;

    if finans.protokol_surumu != PROTOKOL_SURUMU {
        return Err(io::Error::new(
            ErrorKind::InvalidData,
            format!(
                "Finans mesajı protokol sürümü uyuşmuyor. Beklenen: {PROTOKOL_SURUMU}, gelen: {}",
                finans.protokol_surumu
            ),
        ));
    }

    if finans.sirket_kimligi != SIRKET_KIMLIGI {
        return Err(io::Error::new(
            ErrorKind::PermissionDenied,
            format!(
                "Finans mesajı başka şirkete ait. Beklenen: {SIRKET_KIMLIGI}, gelen: {}",
                finans.sirket_kimligi
            ),
        ));
    }

    let kasa = finans.kasa;
    let net_gelir = finans.net_gelir;
    let toplam_gelir = finans.toplam_gelir;
    let toplam_ceza = finans.toplam_ceza;
    let toplam_iade = finans.toplam_iade;
    let tamamlanan_is = finans.tamamlanan_is_sayisi;
    let basarisiz_is = finans.basarisiz_is_sayisi;
    let tick = finans.tick_numarasi;

    let finans_kilidi = SON_FINANS_DURUMU.get_or_init(|| Mutex::new(None));
    let mut son_durum = finans_kilidi
        .lock()
        .map_err(|_| io::Error::other("Finans durumu kilidi zehirlendi."))?;
    *son_durum = Some(finans);

    println!(
        "Tunix finans durumu güncellendi | Tick: {tick} | Kasa: {kasa} | Net gelir: {net_gelir} | Toplam gelir: {toplam_gelir} | İade: {toplam_iade} | Ceza: {toplam_ceza} | Başarılı iş: {tamamlanan_is} | Başarısız iş: {basarisiz_is}"
    );

    Ok(())
}

fn saglik_kontrolunu_isle(
    gelen_mesaj: &str,
    yazici: &mut BufWriter<TcpStream>,
) -> io::Result<()> {
    let kontrol: SaglikKontroluMesaji = json_ayristir(gelen_mesaj, "sağlık kontrolü")?;
    let saglik_mesaji = SaglikSonucuMesaji {
        mesaj_turu: "saglikSonucu",
        mesaj_kimligi: yeni_mesaj_kimligi(),
        protokol_surumu: PROTOKOL_SURUMU,
        istek_kimligi: kontrol.istek_kimligi,
        durum: "calisiyor",
        aktif_baglanti: 1,
        kuyruk_uzunlugu: 0,
    };
    mesaj_gonder(yazici, &saglik_mesaji)?;
    println!("Tick {} sağlık kontrolüne cevap verildi.", kontrol.tick_numarasi);
    Ok(())
}

fn is_istegini_isle(
    gelen_mesaj: &str,
    yazici: &mut BufWriter<TcpStream>,
) -> io::Result<()> {
    let istek: IsIstegiMesaji = json_ayristir(gelen_mesaj, "iş isteği")?;
    let baslangic = Instant::now();
    let hizmet_sonucu = services::hizmeti_calistir(
        &istek.hizmet_kimligi,
        &istek.hizmet_surumu,
        &istek.istek_verisi_json,
    );
    let islem_suresi_ms = baslangic.elapsed().as_secs_f64() * 1_000.0;

    let sonuc_mesaji = match hizmet_sonucu {
        Ok(sonuc_verisi_json) => {
            println!(
                "İş başarıyla işlendi | İş: {} | Hizmet: {}@{} | Süre: {:.3} ms",
                istek.is_kimligi, istek.hizmet_kimligi, istek.hizmet_surumu, islem_suresi_ms
            );
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
            eprintln!(
                "İş başarısız | İş: {} | Kod: {} | Sebep: {}",
                istek.is_kimligi, hata_kodu, hata_mesaji
            );
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

fn json_ayristir<T>(json: &str, mesaj_adi: &str) -> io::Result<T>
where
    T: serde::de::DeserializeOwned,
{
    serde_json::from_str(json).map_err(|hata| {
        io::Error::new(
            ErrorKind::InvalidData,
            format!("{mesaj_adi} ayrıştırılamadı: {hata}"),
        )
    })
}

fn mesaj_gonder<T>(yazici: &mut BufWriter<TcpStream>, mesaj: &T) -> io::Result<()>
where
    T: Serialize,
{
    let json = serde_json::to_string(mesaj).map_err(|hata| {
        io::Error::new(
            ErrorKind::InvalidData,
            format!("Gönderilecek mesaj JSON'a dönüştürülemedi: {hata}"),
        )
    })?;
    if json.len() > AZAMI_MESAJ_BOYUTU_BYTE {
        return Err(io::Error::new(
            ErrorKind::InvalidData,
            "Gönderilecek mesaj azami boyutu aşıyor.",
        ));
    }
    println!("Motora gönderilen mesaj: {json}");
    yazici.write_all(json.as_bytes())?;
    yazici.write_all(b"\n")?;
    yazici.flush()
}

fn yeni_mesaj_kimligi() -> String {
    let zaman = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis();
    let sayac = MESAJ_SAYACI.fetch_add(1, Ordering::Relaxed);
    format!("tunix-{zaman}-{sayac}")
}
