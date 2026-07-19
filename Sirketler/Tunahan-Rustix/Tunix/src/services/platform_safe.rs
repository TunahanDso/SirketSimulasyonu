use super::HizmetHatasi;
use serde_json::{json, Map, Value};
use std::collections::HashMap;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Mutex, OnceLock};
use std::time::{SystemTime, UNIX_EPOCH};

const AZAMI_ISTEK_BYTE: usize = 65_536;
static SAYAC: AtomicU64 = AtomicU64::new(1);
static DURUM: OnceLock<Mutex<Durum>> = OnceLock::new();

#[derive(Default)]
struct Durum {
    profiller: HashMap<String, Value>,
    gonderiler: Vec<Value>,
    etkilesimler: HashMap<String, HashMap<String, String>>,
    mailler: Vec<Value>,
    ekler: Vec<Value>,
}

pub fn calistir(kimlik: &str, ham: &str) -> Result<String, HizmetHatasi> {
    let veri = nesne(ham)?;
    let sonuc = match kimlik {
        "tunix.kimlik.dogrula" => kimlik_dogrula(&veri)?,
        "tunix.tingram.profil.getir" => profil_getir(&veri)?,
        "tunix.tingram.gonderi.olustur" => gonderi_olustur(&veri)?,
        "tunix.tingram.akisi.getir" => akis_getir(&veri)?,
        "tunix.tingram.etkilesim" => etkilesim(&veri)?,
        "tunix.tingram.yorum" => yorum(&veri)?,
        "tunix.tingram.arama" => sosyal_ara(&veri)?,
        "tunix.tmail.gonder" => mail_gonder(&veri)?,
        "tunix.tmail.gelen-kutusu" => gelen_kutusu(&veri)?,
        "tunix.tmail.ara" => mail_ara(&veri)?,
        "tunix.tmail.spam-kontrol" => spam_kontrol(&veri)?,
        "tunix.tmail.ek-yukle" => ek_yukle(&veri)?,
        "tunix.tmail.klasor" => klasor(&veri)?,
        "tunix.tlink.kimlik" => tlink_kimlik(&veri)?,
        "tunix.tlink.paketle" => tlink_paketle(&veri)?,
        "tunix.tlink.dogrula" => tlink_dogrula(&veri)?,
        _ => return Err(HizmetHatasi::DesteklenmeyenHizmet),
    };
    serde_json::to_string(&json!({"sonuc": sonuc}))
        .map_err(|h| HizmetHatasi::SonucSerilestirme(h.to_string()))
}

fn state() -> &'static Mutex<Durum> {
    DURUM.get_or_init(|| Mutex::new(Durum::default()))
}

fn lock_error<T>(_: T) -> HizmetHatasi {
    HizmetHatasi::GecersizIstek("Platform durumu kullanılamıyor.".to_string())
}

fn nesne(ham: &str) -> Result<Map<String, Value>, HizmetHatasi> {
    if ham.len() > AZAMI_ISTEK_BYTE {
        return Err(HizmetHatasi::GecersizIstek(
            "İstek boyut sınırını aşıyor.".to_string(),
        ));
    }
    let value: Value = serde_json::from_str(ham)
        .map_err(|h| HizmetHatasi::GecersizIstek(format!("JSON geçersiz: {h}")))?;
    value
        .as_object()
        .cloned()
        .ok_or_else(|| HizmetHatasi::GecersizIstek("JSON nesnesi bekleniyor.".to_string()))
}

fn metin(v: &Map<String, Value>, alan: &str, max: usize) -> Result<String, HizmetHatasi> {
    let sonuc = v
        .get(alan)
        .and_then(Value::as_str)
        .map(str::trim)
        .filter(|x| !x.is_empty())
        .ok_or_else(|| HizmetHatasi::GecersizIstek(format!("{alan} zorunludur.")))?;
    if sonuc.chars().count() > max {
        return Err(HizmetHatasi::GecersizIstek(format!("{alan} çok uzun.")));
    }
    Ok(sonuc.to_string())
}

fn opsiyonel(v: &Map<String, Value>, alan: &str, max: usize) -> Result<String, HizmetHatasi> {
    match v.get(alan) {
        None | Some(Value::Null) => Ok(String::new()),
        Some(Value::String(x)) if x.chars().count() <= max => Ok(x.trim().to_string()),
        Some(Value::String(_)) => Err(HizmetHatasi::GecersizIstek(format!("{alan} çok uzun."))),
        _ => Err(HizmetHatasi::GecersizIstek(format!("{alan} metin olmalıdır."))),
    }
}

fn limit(v: &Map<String, Value>, default: u64) -> Result<usize, HizmetHatasi> {
    let n = v.get("limit").and_then(Value::as_u64).unwrap_or(default);
    if !(1..=100).contains(&n) {
        return Err(HizmetHatasi::GecersizIstek("limit 1-100 arasında olmalıdır.".to_string()));
    }
    Ok(n as usize)
}

fn now() -> u128 {
    SystemTime::now().duration_since(UNIX_EPOCH).unwrap_or_default().as_millis()
}

fn id(prefix: &str) -> String {
    format!("{prefix}-{}-{}", now(), SAYAC.fetch_add(1, Ordering::Relaxed))
}

fn kimlik_dogrula(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let user = metin(v, "kullaniciKimligi", 120)?;
    let display = opsiyonel(v, "gorunenAd", 120)?;
    let profile_user = user.clone();
    let profile_display = if display.is_empty() { user.clone() } else { display };
    let mut s = state().lock().map_err(lock_error)?;
    let profile = s.profiller.entry(user).or_insert_with(|| json!({
        "kullaniciKimligi": profile_user,
        "gorunenAd": profile_display,
        "biyografi": "",
        "olusturulmaZamani": now()
    })).clone();
    Ok(json!({"gecerli": true, "kullanici": profile}))
}

fn profil_getir(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let user = metin(v, "kullaniciKimligi", 120)?;
    let new_user = user.clone();
    let new_display = user.clone();
    let mut s = state().lock().map_err(lock_error)?;
    let mut profile = s.profiller.entry(user.clone()).or_insert_with(|| json!({
        "kullaniciKimligi": new_user,
        "gorunenAd": new_display,
        "biyografi": "",
        "olusturulmaZamani": now()
    })).clone();
    let count = s.gonderiler.iter().filter(|p| {
        p.get("kullaniciKimligi").and_then(Value::as_str) == Some(user.as_str())
    }).count();
    profile["gonderiSayisi"] = json!(count);
    Ok(profile)
}

fn gonderi_olustur(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let post = json!({
        "gonderiKimligi": id("tingram-post"),
        "kullaniciKimligi": metin(v, "kullaniciKimligi", 120)?,
        "metin": metin(v, "metin", 2000)?,
        "olusturulmaZamani": now(),
        "etkilesimler": {}
    });
    state().lock().map_err(lock_error)?.gonderiler.push(post.clone());
    Ok(post)
}

fn akis_getir(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let count = limit(v, 20)?;
    let s = state().lock().map_err(lock_error)?;
    let result = s.gonderiler.iter().rev().take(count).map(|p| {
        let mut post = p.clone();
        if let Some(post_id) = post.get("gonderiKimligi").and_then(Value::as_str) {
            post["etkilesimler"] = json!(etkilesim_ozeti(s.etkilesimler.get(post_id)));
        }
        post
    }).collect::<Vec<_>>();
    Ok(json!(result))
}

fn etkilesim(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let user = metin(v, "kullaniciKimligi", 120)?;
    let post_id = metin(v, "gonderiKimligi", 160)?;
    let kind = metin(v, "tur", 32)?.to_lowercase();
    if !matches!(kind.as_str(), "begen" | "alkis" | "destek" | "geri-al") {
        return Err(HizmetHatasi::GecersizIstek("Etkileşim türü geçersiz.".to_string()));
    }
    let mut s = state().lock().map_err(lock_error)?;
    if !s.gonderiler.iter().any(|p| p.get("gonderiKimligi").and_then(Value::as_str) == Some(post_id.as_str())) {
        return Err(HizmetHatasi::GecersizIstek("Gönderi bulunamadı.".to_string()));
    }
    let entries = s.etkilesimler.entry(post_id.clone()).or_default();
    if kind == "geri-al" { entries.remove(&user); } else { entries.insert(user, kind); }
    let summary = etkilesim_ozeti(Some(entries));
    Ok(json!({"gonderiKimligi": post_id, "etkilesimler": summary}))
}

fn etkilesim_ozeti(entries: Option<&HashMap<String, String>>) -> HashMap<String, usize> {
    let mut result = HashMap::new();
    if let Some(entries) = entries {
        for kind in entries.values() { *result.entry(kind.clone()).or_insert(0) += 1; }
    }
    result
}

fn yorum(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    Ok(json!({
        "yorumKimligi": id("tingram-comment"),
        "gonderiKimligi": metin(v, "gonderiKimligi", 160)?,
        "kullaniciKimligi": metin(v, "kullaniciKimligi", 120)?,
        "metin": metin(v, "metin", 800)?,
        "olusturulmaZamani": now()
    }))
}

fn sosyal_ara(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let q = metin(v, "sorgu", 160)?.to_lowercase();
    let s = state().lock().map_err(lock_error)?;
    Ok(json!(s.gonderiler.iter().rev().filter(|p| {
        p.get("metin").and_then(Value::as_str).is_some_and(|x| x.to_lowercase().contains(&q))
    }).take(50).cloned().collect::<Vec<_>>()))
}

fn mail_gonder(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let sender = metin(v, "gonderen", 160)?;
    let receiver = metin(v, "alici", 160)?;
    let subject = metin(v, "konu", 240)?;
    let body = opsiyonel(v, "govde", 20_000)?;
    let (score, reasons) = spam_degerlendir(&subject, &body);
    let mail_id = id("tmail");
    let mail = json!({
        "epostaKimligi": mail_id.clone(),
        "gonderen": sender,
        "alici": receiver,
        "konu": subject,
        "govde": body,
        "spamPuani": score,
        "spam": score >= 40,
        "spamNedenleri": reasons,
        "gonderilmeZamani": now()
    });
    state().lock().map_err(lock_error)?.mailler.push(mail);
    Ok(json!({"epostaKimligi": mail_id, "teslimDurumu": "teslim-edildi", "spam": score >= 40}))
}

fn gelen_kutusu(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let user = metin(v, "kullaniciKimligi", 160)?.to_lowercase();
    let count = limit(v, 30)?;
    let s = state().lock().map_err(lock_error)?;
    Ok(json!(s.mailler.iter().rev().filter(|mail| {
        mail.get("alici").and_then(Value::as_str).is_some_and(|x| x.to_lowercase() == user)
    }).take(count).cloned().collect::<Vec<_>>()))
}

fn mail_ara(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let user = metin(v, "kullaniciKimligi", 160)?.to_lowercase();
    let q = metin(v, "sorgu", 240)?.to_lowercase();
    let s = state().lock().map_err(lock_error)?;
    Ok(json!(s.mailler.iter().rev().filter(|mail| {
        let owner = mail.get("alici").and_then(Value::as_str).is_some_and(|x| x.to_lowercase() == user);
        let haystack = format!("{} {} {}",
            mail.get("gonderen").and_then(Value::as_str).unwrap_or_default(),
            mail.get("konu").and_then(Value::as_str).unwrap_or_default(),
            mail.get("govde").and_then(Value::as_str).unwrap_or_default()).to_lowercase();
        owner && haystack.contains(&q)
    }).take(100).cloned().collect::<Vec<_>>()))
}

fn spam_kontrol(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let subject = opsiyonel(v, "konu", 240)?;
    let body = opsiyonel(v, "govde", 20_000)?;
    let (score, reasons) = spam_degerlendir(&subject, &body);
    Ok(json!({"spamPuani": score, "spam": score >= 40, "nedenler": reasons}))
}

fn spam_degerlendir(subject: &str, body: &str) -> (u32, Vec<String>) {
    let haystack = format!("{subject} {body}").to_lowercase();
    let signals = ["bedava", "hemen kazan", "şifre", "kredi kartı", "acil tıkla", "ödül", "kripto fırsat"];
    let reasons = signals.iter().filter(|x| haystack.contains(*x)).map(|x| (*x).to_string()).collect::<Vec<_>>();
    let mut score = reasons.len() as u32 * 20;
    if haystack.matches('!').count() >= 5 { score += 15; }
    (score.min(100), reasons)
}

fn ek_yukle(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let size = v.get("boyutByte").and_then(Value::as_u64).ok_or_else(|| HizmetHatasi::GecersizIstek("boyutByte zorunludur.".to_string()))?;
    if size > 25_000_000 { return Err(HizmetHatasi::GecersizIstek("Ek 25 MB sınırını aşıyor.".to_string())); }
    let attachment = json!({
        "ekKimligi": id("tmail-attachment"),
        "epostaKimligi": metin(v, "epostaKimligi", 160)?,
        "dosyaAdi": metin(v, "dosyaAdi", 240)?,
        "boyutByte": size
    });
    state().lock().map_err(lock_error)?.ekler.push(attachment.clone());
    Ok(attachment)
}

fn klasor(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    Ok(json!({"kullaniciKimligi": metin(v, "kullaniciKimligi", 160)?, "klasorler": ["gelen", "gonderilen", "taslak", "spam", "arsiv"]}))
}

fn tlink_kimlik(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let requested = opsiyonel(v, "protokolSurumu", 30)?;
    let version = if requested.is_empty() { "1.0".to_string() } else { requested };
    let compatible = version.starts_with("1.");
    Ok(json!({"uygulamaKimligi": metin(v, "uygulamaKimligi", 160)?, "protokol": "tunix-tlink", "surum": version, "uyumlu": compatible}))
}

fn tlink_paketle(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let payload = v.get("veri").cloned().unwrap_or(Value::Null);
    let checksum = checksum(&payload)?;
    Ok(json!({"protokol": "tunix-tlink", "surum": "1.0", "kaynak": metin(v, "kaynak", 160)?, "hedef": metin(v, "hedef", 160)?, "veri": payload, "butunluk": checksum}))
}

fn tlink_dogrula(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let packet = v.get("paket").and_then(Value::as_object).ok_or_else(|| HizmetHatasi::GecersizIstek("paket nesnesi zorunludur.".to_string()))?;
    let payload = packet.get("veri").cloned().unwrap_or(Value::Null);
    let expected = checksum(&payload)?;
    let incoming = packet.get("butunluk").and_then(Value::as_u64).unwrap_or_default();
    let protocol = packet.get("protokol").and_then(Value::as_str).unwrap_or_default();
    Ok(json!({"gecerli": protocol == "tunix-tlink" && incoming == expected, "beklenenButunluk": expected}))
}

fn checksum(v: &Value) -> Result<u64, HizmetHatasi> {
    let text = serde_json::to_string(v).map_err(|h| HizmetHatasi::SonucSerilestirme(h.to_string()))?;
    Ok(text.as_bytes().iter().fold(0_u64, |sum, b| (sum + u64::from(*b)) % 1_000_000_007))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn tingram_post_and_feed() {
        let post = calistir("tunix.tingram.gonderi.olustur", r#"{"kullaniciKimligi":"tuna","metin":"Tingram yayında"}"#).unwrap();
        assert!(post.contains("tingram-post"));
        assert!(calistir("tunix.tingram.akisi.getir", r#"{"limit":10}"#).unwrap().contains("Tingram yayında"));
    }

    #[test]
    fn tmail_spam() {
        let result = calistir("tunix.tmail.spam-kontrol", r#"{"konu":"BEDAVA ödül","govde":"Acil tıkla!!!!!"}"#).unwrap();
        assert!(result.contains("spamPuani"));
        assert!(result.contains("true"));
    }

    #[test]
    fn tlink_roundtrip() {
        let packed = calistir("tunix.tlink.paketle", r#"{"kaynak":"tingram","hedef":"tmail","veri":{"olay":"bildirim"}}"#).unwrap();
        let value: Value = serde_json::from_str(&packed).unwrap();
        let request = json!({"paket": value["sonuc"].clone()});
        assert!(calistir("tunix.tlink.dogrula", &request.to_string()).unwrap().contains("true"));
    }
}
