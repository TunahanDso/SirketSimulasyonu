use serde::{Deserialize, Serialize};

use super::HizmetHatasi;

#[derive(Debug, Deserialize)]
struct AsalCarpanIstegi {
    sayi: u64,
}

#[derive(Debug, Serialize)]
struct AsalCarpanSonucu {
    sonuc: Vec<u64>,
}

pub fn calistir(istek_verisi_json: &str) -> Result<String, HizmetHatasi> {
    let istek: AsalCarpanIstegi = serde_json::from_str(istek_verisi_json)
        .map_err(|hata| HizmetHatasi::GecersizIstek(hata.to_string()))?;

    if istek.sayi < 2 {
        return Err(HizmetHatasi::GecersizIstek(
            "Asal çarpanlara ayrılacak sayı en az 2 olmalıdır.".to_string(),
        ));
    }

    let mut kalan = istek.sayi;
    let mut sonuc = Vec::new();
    let mut bolen = 2_u64;

    while bolen <= kalan / bolen {
        while kalan % bolen == 0 {
            sonuc.push(bolen);
            kalan /= bolen;
        }

        bolen = if bolen == 2 { 3 } else { bolen + 2 };
    }

    if kalan > 1 {
        sonuc.push(kalan);
    }

    serde_json::to_string(&AsalCarpanSonucu { sonuc })
        .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::Value;

    #[test]
    fn tekrarlanan_asal_carpanlari_bulur() {
        let sonuc = calistir(r#"{"sayi":360}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
        assert_eq!(json["sonuc"], serde_json::json!([2, 2, 2, 3, 3, 5]));
    }

    #[test]
    fn asal_sayi_kendisini_dondurur() {
        let sonuc = calistir(r#"{"sayi":97}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
        assert_eq!(json["sonuc"], serde_json::json!([97]));
    }

    #[test]
    fn biri_reddeder() {
        let hata = calistir(r#"{"sayi":1}"#).expect_err("1 reddedilmeli");
        assert!(matches!(hata, HizmetHatasi::GecersizIstek(_)));
    }
}
