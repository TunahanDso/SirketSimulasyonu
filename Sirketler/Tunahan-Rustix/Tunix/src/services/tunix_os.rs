use super::HizmetHatasi;
use serde_json::{json, Map, Value};
use std::collections::{HashMap, HashSet};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Mutex, OnceLock};
use std::time::{SystemTime, UNIX_EPOCH};

static DURUM: OnceLock<Mutex<OsDurumu>> = OnceLock::new();
static SAYAC: AtomicU64 = AtomicU64::new(1);

#[derive(Default)]
struct OsDurumu {
    surecler: HashMap<String, Value>,
    kaynaklar: HashMap<String, Value>,
    dosyalar: HashMap<String, String>,
    paketler: HashSet<String>,
    kullanicilar: HashSet<String>,
    ag_profilleri: HashMap<String, Value>,
    loglar: Vec<Value>,
}

pub fn calistir(kimlik: &str, ham: &str) -> Result<String, HizmetHatasi> {
    let veri = nesne(ham)?;
    let sonuc = match kimlik {
        "isletim.surec-baslat" => surec_baslat(&veri)?,
        "isletim.surec-durdur" => surec_durdur(&veri)?,
        "isletim.surec-listele" => surec_listele()?,
        "isletim.kaynak-ata" => kaynak_ata(&veri)?,
        "isletim.kaynak-birak" => kaynak_birak(&veri)?,
        "isletim.dosya-sistemi" => dosya_sistemi(&veri)?,
        "isletim.paket-kur" => paket_kur(&veri)?,
        "isletim.paket-kaldir" => paket_kaldir(&veri)?,
        "isletim.ag-yapilandir" => ag_yapilandir(&veri)?,
        "isletim.guncelleme-kontrol" => guncelleme_kontrol(),
        "isletim.guncelleme-kur" => guncelleme_kur(&veri)?,
        "isletim.log-topla" => log_topla()?,
        "isletim.uygulama-calistir" => uygulama_calistir(&veri)?,
        "kimlik.kullanici-dogrula" => kullanici_dogrula(&veri)?,
        "guvenlik.istek-dogrula" => istek_dogrula(&veri)?,
        _ => return Err(HizmetHatasi::DesteklenmeyenHizmet),
    };
    serde_json::to_string(&json!({"sonuc": sonuc}))
        .map_err(|h| HizmetHatasi::SonucSerilestirme(h.to_string()))
}

fn durum() -> &'static Mutex<OsDurumu> {
    DURUM.get_or_init(|| Mutex::new(OsDurumu::default()))
}

fn kilit_hatasi<T>(_: T) -> HizmetHatasi {
    HizmetHatasi::GecersizIstek("Tunix OS durumu kullanılamıyor.".to_string())
}

fn nesne(ham: &str) -> Result<Map<String, Value>, HizmetHatasi> {
    if ham.len() > 65_536 {
        return Err(HizmetHatasi::GecersizIstek("İstek boyut sınırını aşıyor.".to_string()));
    }
    let value: Value = serde_json::from_str(ham)
        .map_err(|h| HizmetHatasi::GecersizIstek(format!("JSON geçersiz: {h}")))?;
    value.as_object().cloned()
        .ok_or_else(|| HizmetHatasi::GecersizIstek("JSON nesnesi bekleniyor.".to_string()))
}

fn metin(v: &Map<String, Value>, alan: &str, max: usize) -> Result<String, HizmetHatasi> {
    let sonuc = v.get(alan).and_then(Value::as_str).map(str::trim)
        .filter(|x| !x.is_empty())
        .ok_or_else(|| HizmetHatasi::GecersizIstek(format!("{alan} zorunludur.")))?;
    if sonuc.chars().count() > max {
        return Err(HizmetHatasi::GecersizIstek(format!("{alan} çok uzun.")));
    }
    Ok(sonuc.to_string())
}

fn sayi(v: &Map<String, Value>, alan: &str, default: u64, max: u64) -> Result<u64, HizmetHatasi> {
    let n = v.get(alan).and_then(Value::as_u64).unwrap_or(default);
    if n > max {
        return Err(HizmetHatasi::GecersizIstek(format!("{alan} sınırı aşıyor.")));
    }
    Ok(n)
}

fn zaman() -> u128 {
    SystemTime::now().duration_since(UNIX_EPOCH).unwrap_or_default().as_millis()
}

fn kimlik(prefix: &str) -> String {
    format!("{prefix}-{}-{}", zaman(), SAYAC.fetch_add(1, Ordering::Relaxed))
}

fn log_ekle(d: &mut OsDurumu, tur: &str, aciklama: &str) {
    d.loglar.push(json!({"tur": tur, "aciklama": aciklama, "zaman": zaman()}));
    if d.loglar.len() > 500 {
        let fazla = d.loglar.len() - 500;
        d.loglar.drain(0..fazla);
    }
}

fn surec_baslat(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let uygulama = metin(v, "uygulamaKimligi", 160)?;
    let cpu = sayi(v, "cpuBirimi", 1, 128)?;
    let ram = sayi(v, "ramMb", 128, 65_536)?;
    let id = kimlik("tunix-process");
    let kayit = json!({
        "surecKimligi": id,
        "uygulamaKimligi": uygulama,
        "cpuBirimi": cpu,
        "ramMb": ram,
        "durum": "calisiyor",
        "baslangic": zaman()
    });
    let mut d = durum().lock().map_err(kilit_hatasi)?;
    d.surecler.insert(id.clone(), kayit.clone());
    log_ekle(&mut d, "surec", "Yeni süreç başlatıldı.");
    Ok(kayit)
}

fn surec_durdur(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let id = metin(v, "surecKimligi", 180)?;
    let mut d = durum().lock().map_err(kilit_hatasi)?;
    let mut kayit = d.surecler.remove(&id)
        .ok_or_else(|| HizmetHatasi::GecersizIstek("Süreç bulunamadı.".to_string()))?;
    kayit["durum"] = json!("durduruldu");
    log_ekle(&mut d, "surec", "Süreç durduruldu.");
    Ok(kayit)
}

fn surec_listele() -> Result<Value, HizmetHatasi> {
    let d = durum().lock().map_err(kilit_hatasi)?;
    Ok(json!(d.surecler.values().cloned().collect::<Vec<_>>()))
}

fn kaynak_ata(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let surec = metin(v, "surecKimligi", 180)?;
    let cpu = sayi(v, "cpuBirimi", 1, 128)?;
    let ram = sayi(v, "ramMb", 128, 65_536)?;
    let mut d = durum().lock().map_err(kilit_hatasi)?;
    if !d.surecler.contains_key(&surec) {
        return Err(HizmetHatasi::GecersizIstek("Kaynak atanacak süreç bulunamadı.".to_string()));
    }
    let kaynak = json!({"surecKimligi": surec, "cpuBirimi": cpu, "ramMb": ram, "atanmaZamani": zaman()});
    d.kaynaklar.insert(surec.clone(), kaynak.clone());
    log_ekle(&mut d, "kaynak", "Sürece kaynak atandı.");
    Ok(kaynak)
}

fn kaynak_birak(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let surec = metin(v, "surecKimligi", 180)?;
    let mut d = durum().lock().map_err(kilit_hatasi)?;
    let birakildi = d.kaynaklar.remove(&surec).is_some();
    log_ekle(&mut d, "kaynak", "Süreç kaynağı bırakıldı.");
    Ok(json!({"surecKimligi": surec, "birakildi": birakildi}))
}

fn dosya_sistemi(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let islem = metin(v, "islem", 32)?.to_lowercase();
    let yol = metin(v, "yol", 240)?;
    let mut d = durum().lock().map_err(kilit_hatasi)?;
    match islem.as_str() {
        "yaz" => {
            let icerik = v.get("icerik").and_then(Value::as_str).unwrap_or_default();
            if icerik.len() > 32_768 {
                return Err(HizmetHatasi::GecersizIstek("Dosya içeriği çok büyük.".to_string()));
            }
            d.dosyalar.insert(yol.clone(), icerik.to_string());
            log_ekle(&mut d, "dosya", "Dosya yazıldı.");
            Ok(json!({"yol": yol, "yazildi": true, "byte": icerik.len()}))
        }
        "oku" => Ok(json!({"yol": yol, "icerik": d.dosyalar.get(&yol).cloned()})),
        "sil" => Ok(json!({"yol": yol, "silindi": d.dosyalar.remove(&yol).is_some()})),
        "listele" => Ok(json!(d.dosyalar.keys().cloned().collect::<Vec<_>>())),
        _ => Err(HizmetHatasi::GecersizIstek("Dosya sistemi işlemi geçersiz.".to_string())),
    }
}

fn paket_kur(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let paket = metin(v, "paketKimligi", 160)?;
    let mut d = durum().lock().map_err(kilit_hatasi)?;
    let yeni = d.paketler.insert(paket.clone());
    log_ekle(&mut d, "paket", "Paket kuruldu.");
    Ok(json!({"paketKimligi": paket, "kuruldu": yeni, "toplamPaket": d.paketler.len()}))
}

fn paket_kaldir(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let paket = metin(v, "paketKimligi", 160)?;
    let mut d = durum().lock().map_err(kilit_hatasi)?;
    let silindi = d.paketler.remove(&paket);
    log_ekle(&mut d, "paket", "Paket kaldırıldı.");
    Ok(json!({"paketKimligi": paket, "kaldirildi": silindi}))
}

fn ag_yapilandir(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let profil = metin(v, "profilKimligi", 120)?;
    let adres = metin(v, "adres", 180)?;
    let mut d = durum().lock().map_err(kilit_hatasi)?;
    let kayit = json!({"profilKimligi": profil, "adres": adres, "durum": "aktif", "guncellenme": zaman()});
    d.ag_profilleri.insert(profil.clone(), kayit.clone());
    log_ekle(&mut d, "ag", "Ağ profili yapılandırıldı.");
    Ok(kayit)
}

fn guncelleme_kontrol() -> Value {
    json!({"mevcutSurum": "1.0.0", "sonSurum": "1.0.0", "guncellemeVar": false})
}

fn guncelleme_kur(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let surum = metin(v, "surum", 40)?;
    Ok(json!({"surum": surum, "durum": "kuruldu", "yenidenBaslatmaGerekli": false}))
}

fn log_topla() -> Result<Value, HizmetHatasi> {
    let d = durum().lock().map_err(kilit_hatasi)?;
    Ok(json!(d.loglar.iter().rev().take(100).cloned().collect::<Vec<_>>()))
}

fn uygulama_calistir(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let uygulama = metin(v, "uygulamaKimligi", 160)?;
    let paket = metin(v, "paketKimligi", 160)?;
    let d = durum().lock().map_err(kilit_hatasi)?;
    if !d.paketler.contains(&paket) {
        return Err(HizmetHatasi::GecersizIstek("Uygulama paketi kurulu değil.".to_string()));
    }
    Ok(json!({"uygulamaKimligi": uygulama, "paketKimligi": paket, "durum": "calisiyor"}))
}

fn kullanici_dogrula(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let kullanici = metin(v, "kullaniciKimligi", 160)?;
    let mut d = durum().lock().map_err(kilit_hatasi)?;
    d.kullanicilar.insert(kullanici.clone());
    Ok(json!({"kullaniciKimligi": kullanici, "gecerli": true, "oturumYetkisi": "standart"}))
}

fn istek_dogrula(v: &Map<String, Value>) -> Result<Value, HizmetHatasi> {
    let kaynak = metin(v, "kaynak", 160)?;
    let boyut = sayi(v, "boyutByte", 0, 65_536)?;
    let risk = if kaynak.contains("../") || kaynak.contains("<script") { 95 } else if boyut > 49_152 { 65 } else { 8 };
    Ok(json!({"gecerli": risk < 70, "riskPuani": risk, "kaynak": kaynak}))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn surec_ve_kaynak_akisi_calisir() {
        let sonuc = calistir("isletim.surec-baslat", r#"{"uygulamaKimligi":"tingram","cpuBirimi":2,"ramMb":512}"#).unwrap();
        assert!(sonuc.contains("calisiyor"));
    }

    #[test]
    fn paket_kurmadan_uygulama_calistirilamaz() {
        let sonuc = calistir("isletim.uygulama-calistir", r#"{"uygulamaKimligi":"x","paketKimligi":"y"}"#);
        assert!(sonuc.is_err());
    }
}
