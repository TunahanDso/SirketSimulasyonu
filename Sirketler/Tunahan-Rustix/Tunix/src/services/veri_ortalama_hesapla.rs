use serde::{Deserialize, Serialize};

use super::HizmetHatasi;

#[derive(Debug, Deserialize)]
struct OrtalamaIstegi {
    sayilar: Vec<f64>,
}

#[derive(Debug, Serialize)]
struct OrtalamaSonucu {
    sonuc: f64,
}

pub fn calistir(istek_verisi_json: &str) -> Result<String, HizmetHatasi> {
    let istek: OrtalamaIstegi = serde_json::from_str(istek_verisi_json)
        .map_err(|hata| HizmetHatasi::GecersizIstek(hata.to_string()))?;

    if istek.sayilar.is_empty() {
        return Err(HizmetHatasi::GecersizIstek(
            "Ortalama hesaplamak için en az bir sayı gereklidir.".to_string(),
        ));
    }

    if istek.sayilar.iter().any(|sayi| !sayi.is_finite()) {
        return Err(HizmetHatasi::GecersizIstek(
            "Sayı dizisi yalnızca sonlu sayılar içermelidir.".to_string(),
        ));
    }

    let toplam = istek.sayilar.iter().copied().sum::<f64>();
    if !toplam.is_finite() {
        return Err(HizmetHatasi::HesaplamaTasmasi);
    }

    let ortalama = toplam / istek.sayilar.len() as f64;
    if !ortalama.is_finite() {
        return Err(HizmetHatasi::HesaplamaTasmasi);
    }

    serde_json::to_string(&OrtalamaSonucu { sonuc: ortalama })
        .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::Value;

    #[test]
    fn tam_sayilarin_ortalamasini_hesaplar() {
        let sonuc = calistir(r#"{"sayilar":[10,20,30]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert!(json["sonuc"].is_number());
        assert_eq!(json["sonuc"], 20);
    }

    #[test]
    fn ondalikli_sayilari_yuvarlamadan_hesaplar() {
        let sonuc = calistir(r#"{"sayilar":[0.1,0.2,1.25]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
        let deger = json["sonuc"].as_f64().expect("sonuç sayı olmalı");

        assert!((deger - 0.516_666_666_666_666_7).abs() < 1e-12);
    }

    #[test]
    fn tek_sayinin_ortalamasi_kendisidir() {
        let sonuc = calistir(r#"{"sayilar":[42.5]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert_eq!(json["sonuc"], 42.5);
    }

    #[test]
    fn bos_diziyi_reddeder() {
        let hata = calistir(r#"{"sayilar":[]}"#).expect_err("boş dizi reddedilmeli");

        assert!(matches!(hata, HizmetHatasi::GecersizIstek(_)));
    }

    #[test]
    fn gecersiz_alan_turunu_reddeder() {
        let hata = calistir(r#"{"sayilar":"dizi-degil"}"#).expect_err("istek reddedilmeli");

        assert!(matches!(hata, HizmetHatasi::GecersizIstek(_)));
    }
}
