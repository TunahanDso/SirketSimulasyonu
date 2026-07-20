use rust_decimal::Decimal;
use serde::{Deserialize, Serialize};

use super::HizmetHatasi;

#[derive(Debug, Deserialize)]
struct MatematikToplaIstegi {
    sayilar: Vec<Decimal>,
}

#[derive(Debug, Serialize)]
struct MatematikToplaSonucu {
    #[serde(with = "rust_decimal::serde::arbitrary_precision")]
    sonuc: Decimal,
}

pub fn calistir(istek_verisi_json: &str) -> Result<String, HizmetHatasi> {
    let istek: MatematikToplaIstegi = serde_json::from_str(istek_verisi_json)
        .map_err(|hata| HizmetHatasi::GecersizIstek(hata.to_string()))?;

    let toplam = istek
        .sayilar
        .into_iter()
        .try_fold(Decimal::ZERO, |biriken, sayi| {
            biriken
                .checked_add(sayi)
                .ok_or(HizmetHatasi::HesaplamaTasmasi)
        })?;

    serde_json::to_string(&MatematikToplaSonucu { sonuc: toplam })
        .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::Value;

    #[test]
    fn pozitif_ve_negatif_sayilari_toplar() {
        let sonuc = calistir(r#"{"sayilar":[10,-3,5]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert!(json["sonuc"].is_number());
        assert_eq!(json["sonuc"], 12);
    }

    #[test]
    fn ondalik_sayilari_kayipsiz_toplar() {
        let sonuc = calistir(r#"{"sayilar":[0.1,0.2,1.25]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert!(json["sonuc"].is_number());
        assert_eq!(json["sonuc"].to_string(), "1.55");
    }

    #[test]
    fn bos_dizinin_toplami_sifirdir() {
        let sonuc = calistir(r#"{"sayilar":[]}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert!(json["sonuc"].is_number());
        assert_eq!(json["sonuc"], 0);
    }

    #[test]
    fn gecersiz_istegi_reddeder() {
        let hata = calistir(r#"{"sayilar":"dizi-degil"}"#).expect_err("istek reddedilmeli");

        assert!(matches!(hata, HizmetHatasi::GecersizIstek(_)));
        assert_eq!(hata.kodu(), "ISTEK_VERISI_GECERSIZ");
    }
}
