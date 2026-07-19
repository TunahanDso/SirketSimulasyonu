use super::HizmetHatasi;
use serde_json::{json, Map, Value};
use std::collections::HashMap;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Mutex, OnceLock};
use std::time::{SystemTime, UNIX_EPOCH};

const AZAMI_ISTEK_BYTE: usize = 65_536;
const AZAMI_METIN_KARAKTER: usize = 50_000;
static KIMLIK_SAYACI: AtomicU64 = AtomicU64::new(1);
static DURUM: OnceLock<Mutex<PlatformDurumu>> = OnceLock::new();

#[derive(Default)]
struct PlatformDurumu {
    profiller: HashMap<String, Value>,
    gonderiler: Vec<Value>,
    etkilesimler: HashMap<String, HashMap<String, String>>,
    mailler: Vec<Value>,
    ekler: Vec<Value>,
}

pub fn calistir(
    hizmet_kimligi: &str,
    istek_verisi_json: &str,
) -> Result<String, HizmetHatasi> {
    let veri = json_nesnesi(istek_verisi_json)?;
    let sonuc = match hizmet_kimligi {
        "tunix.kimlik.dogrula" => kimlik_dogrula(&veri)?,
        "tunix.tingram.profil.getir" => profil_getir(&veri)?,
        "tunix.tingram.gonderi.olustur" => gonderi_olustur(&veri)?,
        "tunix.tingram.akisi.getir" => akis_getir(&veri)?,
        "tunix.tingram.etkilesim" => etkilesim_yap(&veri)?,
        "tunix.tingram.yorum" => yorum_olustur(&veri)?,
        "tunix.tingram.arama" => tingram_ara(&veri)?,
        "tunix.tmail.gonder" => mail_gonder(&veri)?,
        "tunix.tmail.gelen-kutusu" => gelen_kutusu(&veri)?,
        "tunix.tmail.ara" => mail_ara(&veri)?,
        "tunix.tmail.spam-kontrol" => spam_kontrol(&veri)?,
        "tunix.tmail.ek-yukle" => ek_yukle(&veri)?,
        "tunix.tmail.klasor" => klasor_getir(&veri)?,
        "tunix.tlink.kimlik" => tlink_kimlik(&veri)?,
        "tunix.tlink.paketle" => tlink_paketle(&veri)?,
        "tunix.tlink.dogrula" => tlink_dogrula(&veri)?,
        _ => return Err(HizmetHatasi::DesteklenmeyenHizmet),
    };

    serde_json::to_string(&json!({ "sonuc": sonuc }))
        .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))
}

fn durum() -> &'static Mutex<PlatformDurumu> {
    DURUM.get_or_init(|| Mutex::new(PlatformDurumu::default()))
}

fn json_nesnesi(metin: &str) -> Result<Map<String, Value>, HizmetHatasi> {
    if metin.len() > AZAMI_ISTEK_BYTE {
        return Err(HizmetHatasi::GecersizIstek(
            "İstek boyut sınırını aşıyor.".to_string(),
        ));
    }
    let deger: Value = serde_json::from_str(metin).map_err(|hata| {
        HizmetHatasi::GecersizIstek(format!("JSON ayrıştırılamadı: {hata}"))
    })?;
    deger.as_object().cloned().ok_or_else(|| {
        HizmetHatasi::GecersizIstek("İstek JSON nesnesi olmalıdır.".to_string())
    })
}

fn zorunlu_metin(
    veri: &Map<String, Value>,
    alan: &str,
    azami: usize,
) -> Result<String, HizmetHatasi> {
    let metin = veri
        .get(alan)
        .and_then(Value::as_str)
        .map(str::trim)
        .filter(|deger| !deger.is_empty())
        .ok_or_else(|| {
            HizmetHatasi::GecersizIstek(format!("{alan} dolu metin olmalıdır."))
        })?;
    if metin.chars().count() > azami || metin.chars().count() > AZAMI_METIN_KARAKTER {
        return Err(HizmetHatasi::GecersizIstek(format!(
            "{alan} azami uzunluğu aşıyor."
        )));
    }
    Ok(metin.to_string())
}

fn secimli_metin(
    veri: &Map<String, Value>,
    alan: &str,
    azami: usize,
) -> Result<String, HizmetHatasi> {
    match veri.get(alan) {
        None | Some(Value::Null) => Ok(String::new()),
        Some(Value::String(metin)) => {
            let metin = metin.trim();
            if metin.chars().count() > azami {
                Err(HizmetHatasi::GecersizIstek(format!(
                    "{alan} azami uzunluğu aşıyor."
                )))
            } else {
                Ok(metin.to_string())
            }
        }
        _ => Err(HizmetHatasi::GecersizIstek(format!(
            "{alan} metin olmalıdır."
        ))),
    }
}

fn limit(veri: &Map<String, Value>, varsayilan: u64) -> Result<usize, HizmetHatasi> {
    let deger = veri
        .get("limit")
        .and_then(Value::as_u64)
        .unwrap_or(varsayilan);
    if !(1..=100).contains(&deger) {
        return Err(HizmetHatasi::GecersizIstek(
            "limit 1-100 arasında olmalıdır.".to_string(),
        ));
    }
    Ok(deger as usize)
}

fn simdi_ms() -> u128 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis()
}

fn yeni_kimlik(on_ek: &str) -> String {
    let sayac = KIMLIK_SAYACI.fetch_add(1, Ordering::Relaxed);
    format!("{on_ek}-{}-{sayac}", simdi_ms())
}

fn kimlik_dogrula(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let kullanici = zorunlu_metin(veri, "kullaniciKimligi", 120)?;
    let gorunen_ad = secimli_metin(veri, "gorunenAd", 120)?;
    let profil_kullanici = kullanici.clone();
    let profil_ad = if gorunen_ad.is_empty() {
        kullanici.clone()
    } else {
        gorunen_ad
    };
    let mut kilit = durum().lock().map_err(kilit_hatasi)?;
    let profil = kilit
        .profiller
        .entry(kullanici)
        .or_insert_with(|| {
            json!({
                "kullaniciKimligi": profil_kullanici,
                "gorunenAd": profil_ad,
                "biyografi": "",
                "olusturulmaZamani": simdi_ms()
            })
        })
        .clone();
    Ok(json!({ "gecerli": true, "kullanici": profil }))
}

fn profil_getir(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let kullanici = zorunlu_metin(veri, "kullaniciKimligi", 120)?;
    let yeni_kullanici = kullanici.clone();
    let yeni_ad = kullanici.clone();
    let mut kilit = durum().lock().map_err(kilit_hatasi)?;
    let profil = kilit
        .profiller
        .entry(kullanici.clone())
        .or_insert_with(|| {
            json!({
                "kullaniciKimligi": yeni_kullanici,
                "gorunenAd": yeni_ad,
                "biyografi": "",
                "olusturulmaZamani": simdi_ms()
            })
        })
        .clone();
    let gonderi_sayisi = kilit
        .gonderiler
        .iter()
        .filter(|gonderi| {
            gonderi
                .get("kullaniciKimligi")
                .and_then(Value::as_str)
                == Some(kullanici.as_str())
        })
        .count();
    let mut sonuc = profil;
    sonuc["gonderiSayisi"] = json!(gonderi_sayisi);
    Ok(sonuc)
}

fn gonderi_olustur(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let kullanici = zorunlu_metin(veri, "kullaniciKimligi", 120)?;
    let metin = zorunlu_metin(veri, "metin", 2_000)?;
    let gonderi = json!({
        "gonderiKimligi": yeni_kimlik("tingram-post"),
        "kullaniciKimligi": kullanici,
        "metin": metin,
        "olusturulmaZamani": simdi_ms(),
        "etkilesimler": {}
    });
    durum()
        .lock()
        .map_err(kilit_hatasi)?
        .gonderiler
        .push(gonderi.clone());
    Ok(gonderi)
}

fn akis_getir(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let adet = limit(veri, 20)?;
    let kilit = durum().lock().map_err(kilit_hatasi)?;
    let akis = kilit
        .gonderiler
        .iter()
        .rev()
        .take(adet)
        .map(|gonderi| {
            let mut gonderi = gonderi.clone();
            if let Some(kimlik) = gonderi
                .get("gonderiKimligi")
                .and_then(Value::as_str)
            {
                gonderi["etkilesimler"] =
                    json!(etkilesim_ozeti(kilit.etkilesimler.get(kimlik)));
            }
            gonderi
        })
        .collect::<Vec<_>>();
    Ok(json!(akis))
}

fn etkilesim_yap(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let kullanici = zorunlu_metin(veri, "kullaniciKimligi", 120)?;
    let gonderi_kimligi = zorunlu_metin(veri, "gonderiKimligi", 160)?;
    let tur = zorunlu_metin(veri, "tur", 32)?.to_lowercase();
    if !matches!(tur.as_str(), "begen" | "alkis" | "destek" | "geri-al") {
        return Err(HizmetHatasi::GecersizIstek(
            "Desteklenmeyen etkileşim türü.".to_string(),
        ));
    }

    let mut kilit = durum().lock().map_err(kilit_hatasi)?;
    let bulundu = kilit.gonderiler.iter().any(|gonderi| {
        gonderi
            .get("gonderiKimligi")
            .and_then(Value::as_str)
            == Some(gonderi_kimligi.as_str())
    });
    if !bulundu {
        return Err(HizmetHatasi::GecersizIstek(
            "Gönderi bulunamadı.".to_string(),
        ));
    }

    let kayitlar = kilit
        .etkilesimler
        .entry(gonderi_kimligi.clone())
        .or_default();
    if tur == "geri-al" {
        kayitlar.remove(&kullanici);
    } else {
        kayitlar.insert(kullanici, tur);
    }
    let ozet = etkilesim_ozeti(Some(kayitlar));
    Ok(json!({
        "gonderiKimligi": gonderi_kimligi,
        "etkilesimler": ozet
    }))
}

fn etkilesim_ozeti(
    kayitlar: Option<&HashMap<String, String>>,
) -> HashMap<String, usize> {
    let mut ozet = HashMap::new();
    if let Some(kayitlar) = kayitlar {
        for tur in kayitlar.values() {
            *ozet.entry(tur.clone()).or_insert(0) += 1;
        }
    }
    ozet
}

fn yorum_olustur(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    Ok(json!({
        "yorumKimligi": yeni_kimlik("tingram-comment"),
        "gonderiKimligi": zorunlu_metin(veri, "gonderiKimligi", 160)?,
        "kullaniciKimligi": zorunlu_metin(veri, "kullaniciKimligi", 120)?,
        "metin": zorunlu_metin(veri, "metin", 800)?,
        "olusturulmaZamani": simdi_ms()
    }))
}

fn tingram_ara(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let sorgu = zorunlu_metin(veri, "sorgu", 160)?.to_lowercase();
    let kilit = durum().lock().map_err(kilit_hatasi)?;
    let sonuc = kilit
        .gonderiler
        .iter()
        .rev()
        .filter(|gonderi| {
            gonderi
                .get("metin")
                .and_then(Value::as_str)
                .is_some_and(|metin| metin.to_lowercase().contains(&sorgu))
        })
        .take(50)
        .cloned()
        .collect::<Vec<_>>();
    Ok(json!(sonuc))
}

fn mail_gonder(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let gonderen = zorunlu_metin(veri, "gonderen", 160)?;
    let alici = zorunlu_metin(veri, "alici", 160)?;
    let konu = zorunlu_metin(veri, "konu", 240)?;
    let govde = secimli_metin(veri, "govde", 20_000)?;
    let (spam_puani, nedenler) = spam_degerlendir(&konu, &govde);
    let mail_kimligi = yeni_kimlik("tmail");
    let mail = json!({
        "epostaKimligi": mail_kimligi,
        "gonderen": gonderen,
        "alici": alici,
        "konu": konu,
        "govde": govde,
        "spamPuani": spam_puani,
        "spam": spam_puani >= 40,
        "spamNedenleri": nedenler,
        "gonderilmeZamani": simdi_ms()
    });
    durum()
        .lock()
        .map_err(kilit_hatasi)?
        .mailler
        .push(mail);
    Ok(json!({
        "epostaKimligi": mail_kimligi,
        "teslimDurumu": "teslim-edildi",
        "spam": spam_puani >= 40
    }))
}

fn gelen_kutusu(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let kullanici = zorunlu_metin(veri, "kullaniciKimligi", 160)?.to_lowercase();
    let adet = limit(veri, 30)?;
    let kilit = durum().lock().map_err(kilit_hatasi)?;
    let sonuc = kilit
        .mailler
        .iter()
        .rev()
        .filter(|mail| {
            mail.get("alici")
                .and_then(Value::as_str)
                .is_some_and(|alici| alici.to_lowercase() == kullanici)
        })
        .take(adet)
        .cloned()
        .collect::<Vec<_>>();
    Ok(json!(sonuc))
}

fn mail_ara(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let kullanici = zorunlu_metin(veri, "kullaniciKimligi", 160)?.to_lowercase();
    let sorgu = zorunlu_metin(veri, "sorgu", 240)?.to_lowercase();
    let kilit = durum().lock().map_err(kilit_hatasi)?;
    let sonuc = kilit
        .mailler
        .iter()
        .rev()
        .filter(|mail| {
            let alici_uygun = mail
                .get("alici")
                .and_then(Value::as_str)
                .is_some_and(|alici| alici.to_lowercase() == kullanici);
            let aranacak = format!(
                "{} {} {}",
                mail.get("gonderen").and_then(Value::as_str).unwrap_or_default(),
                mail.get("konu").and_then(Value::as_str).unwrap_or_default(),
                mail.get("govde").and_then(Value::as_str).unwrap_or_default()
            )
            .to_lowercase();
            alici_uygun && aranacak.contains(&sorgu)
        })
        .take(100)
        .cloned()
        .collect::<Vec<_>>();
    Ok(json!(sonuc))
}

fn spam_kontrol(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let konu = secimli_metin(veri, "konu", 240)?;
    let govde = secimli_metin(veri, "govde", 20_000)?;
    let (puan, nedenler) = spam_degerlendir(&konu, &govde);
    Ok(json!({
        "spamPuani": puan,
        "spam": puan >= 40,
        "nedenler": nedenler
    }))
}

fn spam_degerlendir(konu: &str, govde: &str) -> (u32, Vec<String>) {
    let metin = format!("{konu} {govde}").to_lowercase();
    let isaretler = [
        "bedava",
        "hemen kazan",
        "şifre",
        "kredi kartı",
        "acil tıkla",
        "ödül",
        "kripto fırsat",
    ];
    let nedenler = isaretler
        .iter()
        .filter(|isaret| metin.contains(*isaret))
        .map(|isaret| (*isaret).to_string())
        .collect::<Vec<_>>();
    let mut puan = nedenler.len() as u32 * 20;
    if metin.matches('!').count() >= 5 {
        puan += 15;
    }
    (puan.min(100), nedenler)
}

fn ek_yukle(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let eposta_kimligi = zorunlu_metin(veri, "epostaKimligi", 160)?;
    let dosya_adi = zorunlu_metin(veri, "dosyaAdi", 240)?;
    let boyut = veri
        .get("boyutByte")
        .and_then(Value::as_u64)
        .ok_or_else(|| {
            HizmetHatasi::GecersizIstek(
                "boyutByte pozitif tam sayı olmalıdır.".to_string(),
            )
        })?;
    if boyut > 25_000_000 {
        return Err(HizmetHatasi::GecersizIstek(
            "Dosya eki 25 MB sınırını aşıyor.".to_string(),
        ));
    }
    let ek = json!({
        "ekKimligi": yeni_kimlik("tmail-attachment"),
        "epostaKimligi": eposta_kimligi,
        "dosyaAdi": dosya_adi,
        "boyutByte": boyut
    });
    durum().lock().map_err(kilit_hatasi)?.ekler.push(ek.clone());
    Ok(ek)
}

fn klasor_getir(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    Ok(json!({
        "kullaniciKimligi": zorunlu_metin(veri, "kullaniciKimligi", 160)?,
        "klasorler": ["gelen", "gonderilen", "taslak", "spam", "arsiv"]
    }))
}

fn tlink_kimlik(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let surum = secimli_metin(veri, "protokolSurumu", 30)?;
    let surum = if surum.is_empty() {
        "1.0".to_string()
    } else {
        surum
    };
    Ok(json!({
        "uygulamaKimligi": zorunlu_metin(veri, "uygulamaKimligi", 160)?,
        "protokol": "tunix-tlink",
        "surum": surum,
        "uyumlu": surum.starts_with("1.")
    }))
}

fn tlink_paketle(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let tasinan = veri.get("veri").cloned().unwrap_or(Value::Null);
    let butunluk = butunluk_hesapla(&tasinan)?;
    Ok(json!({
        "protokol": "tunix-tlink",
        "surum": "1.0",
        "kaynak": zorunlu_metin(veri, "kaynak", 160)?,
        "hedef": zorunlu_metin(veri, "hedef", 160)?,
        "veri": tasinan,
        "butunluk": butunluk
    }))
}

fn tlink_dogrula(veri: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let paket = veri
        .get("paket")
        .and_then(Value::as_object)
        .ok_or_else(|| {
            HizmetHatasi::GecersizIstek("paket nesnesi zorunludur.".to_string())
        })?;
    let tasinan = paket.get("veri").cloned().unwrap_or(Value::Null);
    let beklenen = butunluk_hesapla(&tasinan)?;
    let gelen = paket
        .get("butunluk")
        .and_then(Value::as_u64)
        .unwrap_or_default();
    let protokol = paket
        .get("protokol")
        .and_then(Value::as_str)
        .unwrap_or_default();
    Ok(json!({
        "gecerli": protokol == "tunix-tlink" && gelen == beklenen,
        "beklenenButunluk": beklenen
    }))
}

fn butunluk_hesapla(deger: &Value) -> Result<u64, HizmetHatasi> {
    let metin = serde_json::to_string(deger)
        .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))?;
    Ok(metin
        .as_bytes()
        .iter()
        .fold(0_u64, |toplam, byte| {
            (toplam + u64::from(*byte)) % 1_000_000_007
        }))
}

fn kilit_hatasi<T>(_: T) -> HizmetHatasi {
    HizmetHatasi::GecersizIstek(
        "Platform durumu geçici olarak kullanılamıyor.".to_string(),
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn tingram_gonderisi_akista_gorunur() {
        let sonuc = calistir(
            "tunix.tingram.gonderi.olustur",
            r#"{"kullaniciKimligi":"tuna","metin":"Tingram yayında"}"#,
        )
        .expect("gönderi oluşturulmalı");
        assert!(sonuc.contains("tingram-post"));
        let akis = calistir(
            "tunix.tingram.akisi.getir",
            r#"{"limit":10}"#,
        )
        .expect("akış dönmeli");
        assert!(akis.contains("Tingram yayında"));
    }

    #[test]
    fn tmail_spam_kontrolu_calisir() {
        let sonuc = calistir(
            "tunix.tmail.spam-kontrol",
            r#"{"konu":"BEDAVA ödül","govde":"Acil tıkla!!!!!"}"#,
        )
        .expect("spam sonucu dönmeli");
        assert!(sonuc.contains("spamPuani"));
        assert!(sonuc.contains("true"));
    }

    #[test]
    fn tlink_paketi_dogrulanir() {
        let paket = calistir(
            "tunix.tlink.paketle",
            r#"{"kaynak":"tingram","hedef":"tmail","veri":{"olay":"bildirim"}}"#,
        )
        .expect("paket dönmeli");
        let paket_json: Value =
            serde_json::from_str(&paket).expect("sonuç JSON olmalı");
        let istek = json!({ "paket": paket_json["sonuc"].clone() });
        let sonuc = calistir(
            "tunix.tlink.dogrula",
            &istek.to_string(),
        )
        .expect("doğrulama dönmeli");
        assert!(sonuc.contains("true"));
    }
}
