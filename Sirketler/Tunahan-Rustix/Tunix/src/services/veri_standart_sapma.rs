use serde::{Deserialize, Serialize};

use super::HizmetHatasi;

#[derive(Debug, Deserialize)]
struct StandartSapmaIstegi {
    sayilar: Vec<f64>,
}

#[derive(Debug, Serialize)]
struct StandartSapmaSonucu {
    sonuc: f64,
}

pub fn calistir(istek_verisi_json: &str) -> Result<String, HizmetHatasi> {
    let istek: StandartSapmaIstegi = serde_json::from_str(istek_verisi_json)
        .map_err(|hata| HizmetHatasi::GecersizIstek(hata.to_string()))?;

    if istek.sayilar.is_empty() {
        return Err(HizmetHatasi::GecersizIstek(
            "Standart sapma hesaplamak için en az bir sayı gereklidir.".to_string(),
        ));
    }

    if istek.sayilar.iter().any(|sayi| !sayi.is_finite()) {
        return Err(HizmetHatasi::GecersizIstek(
            "Sayı dizisi yalnızca sonlu sayılar içermelidir.".to_string(),
        ));
    }

    let adet = istek.sayilar.len() as f64;
    let ortalama = istek.sayilar.iter().copied().sum::<f64>() / adet;
    let varyans = istek
        .sayilar
        .iter()
        .map(|sayi| {
            let fark = *sayi - ortalama;
            fark * fark
        })
        .sum::<f64>()
        / adet;
    let sonuc = varyans.sqrt();

    if !sonuc.is_finite() {
        return Err(HizmetHatasi::HesaplamaTasmasi);
    }

    serde_json::to_string(&StandartSapmaSonucu { sonuc })
        .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::Value;

    #[test]
    fn populasyon_standart_sapmasini_hesaplar() {
        let sonuc = calistir(r#"{"sayilar":[2,4,4,4,5,5,7,9]}"#)
            .expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
        let deger = json["sonuc"].as_f64().expect("sonuç sayı olmalı");
        assert!((deger - 2.0).abs() < 1e-12);
    }

    #[test]
    fn tek_sayinin_sapmasi_sifirdir() {
        let sonuc = calistir(r#"{"sayilar":[42]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
        assert_eq!(json["sonuc"], 0.0);
    }
}
