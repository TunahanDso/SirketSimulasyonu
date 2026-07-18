use std::io::{self, BufRead, BufReader, BufWriter, Write};
use std::net::{TcpListener, TcpStream};
use std::sync::atomic::{AtomicU64, Ordering};
use std::thread;
use std::time::{SystemTime, UNIX_EPOCH};

// Tunix sunucusunun dinleyeceği adres.
//
// 0.0.0.0:
// Hem aynı bilgisayardan hem de yerel ağdaki diğer
// bilgisayarlardan bağlantı kabul eder.
//
// 7001:
// Tunix şirketinin portudur.
const SUNUCU_ADRESI: &str = "0.0.0.0:7001";

// Motorun şirketi tanıyacağı bilgiler.
const SIRKET_KIMLIGI: &str = "tunahan-tunix";
const SIRKET_ADI: &str = "Tunix";
const PROTOKOL_SURUMU: &str = "0.1";
const SUNUCU_SURUMU: &str = "0.1";

// Aynı milisaniyede birden fazla mesaj üretilirse
// mesaj kimliklerinin çakışmaması için sayaç kullanıyoruz.
static MESAJ_SAYACI: AtomicU64 = AtomicU64::new(1);

fn main() -> io::Result<()> {
    println!("================================");
    println!("          TUNIX SERVER");
    println!("================================");

    // İşletim sisteminden 7001 portunu istiyoruz.
    let dinleyici = TcpListener::bind(SUNUCU_ADRESI)?;

    println!("Tunix sunucusu baslatildi.");
    println!("Tunix 7001 portunda motoru bekliyor...");
    println!();

    // Sunucu kapanmadığı sürece yeni bağlantıları bekler.
    for gelen_baglanti in dinleyici.incoming() {
        match gelen_baglanti {
            Ok(baglanti) => {
                println!("Motor Tunix sunucusuna baglandi.");

                // Her motor bağlantısını ayrı iş parçacığında
                // çalıştırıyoruz.
                thread::spawn(move || {
                    if let Err(hata) = motor_baglantisini_yonet(baglanti) {
                        eprintln!("Motor baglantisi hatasi: {hata}");
                    }
                });
            }

            Err(hata) => {
                eprintln!("Baglanti kabul edilemedi: {hata}");
            }
        }
    }

    Ok(())
}

// Bir motor bağlantısının bütün yaşam döngüsünü yönetir.
fn motor_baglantisini_yonet(baglanti: TcpStream) -> io::Result<()> {
    // Aynı TCP bağlantısını hem okumak hem yazmak istiyoruz.
    //
    // Bu yüzden bağlantının bir kopyasını okuma tarafına,
    // aslını da yazma tarafına veriyoruz.
    let okuma_baglantisi = baglanti.try_clone()?;

    let okuyucu = BufReader::new(okuma_baglantisi);
    let mut yazici = BufWriter::new(baglanti);

    // TCP bir mesaj sistemi değil, veri akışıdır.
    //
    // Motor her JSON mesajının sonuna "\n" koyduğu için
    // BufReader satır satır okuyabilir.
    for gelen_satir in okuyucu.lines() {
        let gelen_mesaj = match gelen_satir {
            Ok(mesaj) => mesaj,

            Err(hata) => {
                eprintln!("Motordan mesaj okunamadi: {hata}");
                break;
            }
        };

        // Boş satır geldiyse işlemiyoruz.
        if gelen_mesaj.trim().is_empty() {
            continue;
        }

        println!();
        println!("Motordan ham mesaj geldi:");
        println!("{gelen_mesaj}");

        // Mesajın hangi türde olduğunu JSON içinden buluyoruz.
        let mesaj_turu = match json_metin_al(&gelen_mesaj, "mesajTuru") {
            Some(deger) => deger,

            None => {
                eprintln!("Mesajda mesajTuru bulunamadi.");
                continue;
            }
        };

        println!("Motordan mesaj geldi: {mesaj_turu}");

        match mesaj_turu.as_str() {
            "merhaba" => {
                merhaba_mesajini_isle(
                    &gelen_mesaj,
                    &mut yazici,
                )?;
            }

            "kayitSonucu" => {
                kayit_sonucunu_isle(&gelen_mesaj);
            }

            "saglikKontrolu" => {
                saglik_kontrolunu_isle(
                    &gelen_mesaj,
                    &mut yazici,
                )?;
            }

            bilinmeyen_mesaj => {
                println!(
                    "Bilinmeyen mesaj turu: {bilinmeyen_mesaj}"
                );
            }
        }
    }

    println!("Motor Tunix baglantisini kapatti.");

    Ok(())
}

// Motorun ilk gönderdiği "merhaba" mesajını işler.
fn merhaba_mesajini_isle(
    gelen_mesaj: &str,
    yazici: &mut BufWriter<TcpStream>,
) -> io::Result<()> {
    if let Some(motor_kimligi) =
        json_metin_al(gelen_mesaj, "motorKimligi")
    {
        println!("Motor kimligi: {motor_kimligi}");
    }

    if let Some(protokol_surumu) =
        json_metin_al(gelen_mesaj, "protokolSurumu")
    {
        println!(
            "Motor protokol surumu: {protokol_surumu}"
        );
    }

    // Tunix kendisini motora tanıtıyor.
    let tanitim_mesaji = format!(
        concat!(
            "{{",
            "\"mesajTuru\":\"sirketTanitim\",",
            "\"mesajKimligi\":\"{}\",",
            "\"protokolSurumu\":\"{}\",",
            "\"sirketKimligi\":\"{}\",",
            "\"sirketAdi\":\"{}\",",
            "\"sunucuSurumu\":\"{}\"",
            "}}"
        ),
        yeni_mesaj_kimligi(),
        json_kacis(PROTOKOL_SURUMU),
        json_kacis(SIRKET_KIMLIGI),
        json_kacis(SIRKET_ADI),
        json_kacis(SUNUCU_SURUMU),
    );

    mesaj_gonder(yazici, &tanitim_mesaji)?;

    println!("Tunix tanitim mesaji motora gonderildi.");

    Ok(())
}

// Motorun şirket kaydını kabul edip etmediğini işler.
fn kayit_sonucunu_isle(gelen_mesaj: &str) {
    let basarili =
        json_bool_al(gelen_mesaj, "basarili").unwrap_or(false);

    let aciklama =
        json_metin_al(gelen_mesaj, "aciklama")
            .unwrap_or_else(|| {
                "Motor aciklama gondermedi.".to_string()
            });

    if basarili {
        println!("Tunix motor tarafindan kaydedildi.");
        println!("Aciklama: {aciklama}");
    } else {
        println!("Tunix kaydi reddedildi.");
        println!("Sebep: {aciklama}");
    }
}

// Motorun gönderdiği sağlık kontrolüne cevap verir.
fn saglik_kontrolunu_isle(
    gelen_mesaj: &str,
    yazici: &mut BufWriter<TcpStream>,
) -> io::Result<()> {
    // Motor, isteği gönderirken bir istek kimliği oluşturur.
    // Tunix cevabında aynı kimliği geri göndermelidir.
    let istek_kimligi =
        match json_metin_al(gelen_mesaj, "istekKimligi") {
            Some(deger) => deger,

            None => {
                eprintln!(
                    "Saglik kontrolunde istekKimligi bulunamadi."
                );

                return Ok(());
            }
        };

    let tick_numarasi =
        json_tamsayi_al(gelen_mesaj, "tickNumarasi")
            .unwrap_or(0);

    let saglik_mesaji = format!(
        concat!(
            "{{",
            "\"mesajTuru\":\"saglikSonucu\",",
            "\"mesajKimligi\":\"{}\",",
            "\"protokolSurumu\":\"{}\",",
            "\"istekKimligi\":\"{}\",",
            "\"durum\":\"calisiyor\",",
            "\"aktifBaglanti\":1,",
            "\"kuyrukUzunlugu\":0",
            "}}"
        ),
        yeni_mesaj_kimligi(),
        json_kacis(PROTOKOL_SURUMU),
        json_kacis(&istek_kimligi),
    );

    mesaj_gonder(yazici, &saglik_mesaji)?;

    println!(
        "Tick {tick_numarasi} saglik kontrolune cevap verildi."
    );

    Ok(())
}

// Hazırlanan JSON mesajını TCP bağlantısından gönderir.
fn mesaj_gonder(
    yazici: &mut BufWriter<TcpStream>,
    mesaj: &str,
) -> io::Result<()> {
    println!("Motora gonderilen mesaj:");
    println!("{mesaj}");

    // Protokol gereği her JSON mesajının sonuna
    // satır sonu ekliyoruz.
    yazici.write_all(mesaj.as_bytes())?;
    yazici.write_all(b"\n")?;

    // BufWriter veriyi bellekte bekletebilir.
    // flush ile hemen ağa gönderilmesini sağlıyoruz.
    yazici.flush()?;

    Ok(())
}

// Her giden mesaj için benzersiz kimlik üretir.
fn yeni_mesaj_kimligi() -> String {
    let zaman = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis();

    let sayac =
        MESAJ_SAYACI.fetch_add(1, Ordering::Relaxed);

    format!("tunix-{zaman}-{sayac}")
}

// JSON içinde metin türündeki bir alanı bulur.
//
// Örnek:
//
// {"mesajTuru":"merhaba"}
//
// json_metin_al(json, "mesajTuru")
// sonucu:
// Some("merhaba")
fn json_metin_al(json: &str, alan_adi: &str) -> Option<String> {
    let aranan = format!("\"{alan_adi}\"");

    let alan_baslangici = json.find(&aranan)?;

    let alan_sonrasi =
        &json[alan_baslangici + aranan.len()..];

    let iki_nokta = alan_sonrasi.find(':')?;

    let deger_bolumu =
        alan_sonrasi[iki_nokta + 1..].trim_start();

    if !deger_bolumu.starts_with('"') {
        return None;
    }

    let karakterler =
        deger_bolumu[1..].chars();

    let mut sonuc = String::new();
    let mut kacis_var = false;

    for karakter in karakterler {
        if kacis_var {
            match karakter {
                '"' => sonuc.push('"'),
                '\\' => sonuc.push('\\'),
                'n' => sonuc.push('\n'),
                'r' => sonuc.push('\r'),
                't' => sonuc.push('\t'),
                diger => sonuc.push(diger),
            }

            kacis_var = false;
            continue;
        }

        if karakter == '\\' {
            kacis_var = true;
            continue;
        }

        if karakter == '"' {
            return Some(sonuc);
        }

        sonuc.push(karakter);
    }

    None
}

// JSON içindeki true veya false değerini okur.
fn json_bool_al(json: &str, alan_adi: &str) -> Option<bool> {
    let ham_deger = json_ham_deger_al(json, alan_adi)?;

    match ham_deger.as_str() {
        "true" => Some(true),
        "false" => Some(false),
        _ => None,
    }
}

// JSON içindeki tam sayı değerini okur.
fn json_tamsayi_al(
    json: &str,
    alan_adi: &str,
) -> Option<i64> {
    let ham_deger = json_ham_deger_al(json, alan_adi)?;

    ham_deger.parse::<i64>().ok()
}

// JSON içindeki tırnaksız ham değeri bulur.
//
// Bu fonksiyon boolean ve sayı alanları için kullanılır.
fn json_ham_deger_al(
    json: &str,
    alan_adi: &str,
) -> Option<String> {
    let aranan = format!("\"{alan_adi}\"");

    let alan_baslangici = json.find(&aranan)?;

    let alan_sonrasi =
        &json[alan_baslangici + aranan.len()..];

    let iki_nokta = alan_sonrasi.find(':')?;

    let deger_bolumu =
        alan_sonrasi[iki_nokta + 1..].trim_start();

    let deger_sonu = deger_bolumu
        .find(|karakter: char| {
            karakter == ',' ||
            karakter == '}' ||
            karakter.is_whitespace()
        })
        .unwrap_or(deger_bolumu.len());

    let sonuc =
        deger_bolumu[..deger_sonu].trim();

    if sonuc.is_empty() {
        None
    } else {
        Some(sonuc.to_string())
    }
}

// JSON içine koyacağımız metinlerde özel karakterleri
// güvenli biçime dönüştürür.
fn json_kacis(metin: &str) -> String {
    let mut sonuc = String::new();

    for karakter in metin.chars() {
        match karakter {
            '"' => sonuc.push_str("\\\""),
            '\\' => sonuc.push_str("\\\\"),
            '\n' => sonuc.push_str("\\n"),
            '\r' => sonuc.push_str("\\r"),
            '\t' => sonuc.push_str("\\t"),
            diger => sonuc.push(diger),
        }
    }

    sonuc
}