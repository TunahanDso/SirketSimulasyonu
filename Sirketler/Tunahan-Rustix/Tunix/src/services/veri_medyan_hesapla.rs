use serde::{Deserialize, Serialize};

use super::HizmetHatasi;

#[derive(Debug, Deserialize)]
struct MedyanIstegi {
    sayilar: Vec<f64>,
}

#[derive(Debug, Serialize)]
struct MedyanSonucu {
    sonuc: f64,
}

pub fn calistir(istek_verisi_json: &str) -> Result<String, HizmetHatasi> {
    let mut istek: MedyanIstegi = serde_json::from_str(istek_verisi_json)
        .map_err(|hata| HizmetHatasi::GecersizIstek(hata.to_string()))?;

    if istek.sayilar.is_empty() {
        return Err(HizmetHatasi::GecersizIstek(
            "Medyan hesaplamak için en az bir sayı gereklidir.".to_string(),
        ));
    }

    if istek.sayilar.iter().any(|sayi| !sayi.is_finite()) {
        return Err(HizmetHatasi::GecersizIstek(
            "Sayı dizisi yalnızca sonlu sayılar içermelidir.".to_string(),
        ));
    }

    istek.sayilar.sort_by(f64::total_cmp);
    let orta = istek.sayilar.len() / 2;
    let sonuc = if istek.sayilar.len() % 2 == 1 {
        istek.sayilar[orta]
    } else {
        (istek.sayilar[orta - 1] + istek.sayilar[orta]) / 2.0
    };

    if !sonuc.is_finite() {
        return Err(HizmetHatasi::HesaplamaTasmasi);
    }

    serde_json::to_string(&MedyanSonucu { sonuc })
        .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::Value;

    #[test]
    fn tek_sayili_dizinin_medyanini_hesaplar() {
        let sonuc = calistir(r#"{"sayilar":[9,1,5]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
        assert_eq!(json["sonuc"], 5.0);
    }

    #[test]
    fn cift_sayili_dizinin_medyanini_hesaplar() {
        let sonuc = calistir(r#"{"sayilar":[8,2,4,6]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
        assert_eq!(json["sonuc"], 5.0);
    }

    #[test]
    fn bos_diziyi_reddeder() {
        let hata = calistir(r#"{"sayilar":[]}"#).expect_err("boş dizi reddedilmeli");
        assert!(matches!(hata, HizmetHatasi::GecersizIstek(_)));
    }
}
