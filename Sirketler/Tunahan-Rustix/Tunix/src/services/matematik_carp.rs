use rust_decimal::Decimal;
use serde::{Deserialize, Serialize};

use super::HizmetHatasi;

#[derive(Debug, Deserialize)]
struct MatematikCarpIstegi {
    sayilar: Vec<Decimal>,
}

#[derive(Debug, Serialize)]
struct MatematikCarpSonucu {
    #[serde(with = "rust_decimal::serde::arbitrary_precision")]
    sonuc: Decimal,
}

pub fn calistir(istek_verisi_json: &str) -> Result<String, HizmetHatasi> {
    let istek: MatematikCarpIstegi = serde_json::from_str(istek_verisi_json)
        .map_err(|hata| HizmetHatasi::GecersizIstek(hata.to_string()))?;

    if istek.sayilar.is_empty() {
        return Err(HizmetHatasi::GecersizIstek(
            "'sayilar' dizisi boş olamaz.".to_string(),
        ));
    }

    let carpim = istek
        .sayilar
        .into_iter()
        .try_fold(Decimal::ONE, |biriken, sayi| {
            biriken
                .checked_mul(sayi)
                .ok_or(HizmetHatasi::HesaplamaTasmasi)
        })?;

    serde_json::to_string(&MatematikCarpSonucu { sonuc: carpim })
        .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::Value;

    #[test]
    fn tamsayilari_carpar() {
        let sonuc = calistir(r#"{"sayilar":[2,3,4]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert!(json["sonuc"].is_number());
        assert_eq!(json["sonuc"], 24);
    }

    #[test]
    fn sifir_iceriyorsa_sifir_dondurur() {
        let sonuc = calistir(r#"{"sayilar":[9,0,5]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert_eq!(json["sonuc"], 0);
    }

    #[test]
    fn ondalik_sayilari_kayipsiz_carpar() {
        let sonuc = calistir(r#"{"sayilar":[0.5,1.2,10]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert!(json["sonuc"].is_number());
        assert_eq!(json["sonuc"].to_string(), "6.00");
    }

    #[test]
    fn bos_diziyi_reddeder() {
        let hata = calistir(r#"{"sayilar":[]}"#).expect_err("boş dizi reddedilmeli");

        assert!(matches!(hata, HizmetHatasi::GecersizIstek(_)));
        assert_eq!(hata.kodu(), "ISTEK_VERISI_GECERSIZ");
    }
}
