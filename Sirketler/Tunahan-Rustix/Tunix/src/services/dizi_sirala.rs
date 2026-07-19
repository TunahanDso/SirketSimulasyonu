use serde::{Deserialize, Serialize};

use super::HizmetHatasi;

#[derive(Debug, Deserialize)]
struct SiralamaIstegi {
    sayilar: Vec<f64>,
    yon: String,
}

#[derive(Debug, Serialize)]
struct SiralamaSonucu {
    sonuc: Vec<f64>,
}

pub fn calistir(istek_verisi_json: &str) -> Result<String, HizmetHatasi> {
    let mut istek: SiralamaIstegi = serde_json::from_str(istek_verisi_json)
        .map_err(|hata| HizmetHatasi::GecersizIstek(hata.to_string()))?;

    if istek.sayilar.iter().any(|sayi| !sayi.is_finite()) {
        return Err(HizmetHatasi::GecersizIstek(
            "Sayı dizisi yalnızca sonlu sayılar içermelidir.".to_string(),
        ));
    }

    istek.sayilar.sort_by(f64::total_cmp);

    match istek.yon.trim().to_lowercase().as_str() {
        "artan" => {}
        "azalan" => istek.sayilar.reverse(),
        _ => {
            return Err(HizmetHatasi::GecersizIstek(
                "Sıralama yönü 'artan' veya 'azalan' olmalıdır.".to_string(),
            ));
        }
    }

    serde_json::to_string(&SiralamaSonucu {
        sonuc: istek.sayilar,
    })
    .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::Value;

    #[test]
    fn artan_siralar() {
        let sonuc = calistir(r#"{"sayilar":[3,1,2],"yon":"artan"}"#)
            .expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
        assert_eq!(json["sonuc"], serde_json::json!([1.0, 2.0, 3.0]));
    }

    #[test]
    fn azalan_siralar() {
        let sonuc = calistir(r#"{"sayilar":[3,1,2],"yon":"azalan"}"#)
            .expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
        assert_eq!(json["sonuc"], serde_json::json!([3.0, 2.0, 1.0]));
    }

    #[test]
    fn gecersiz_yonu_reddeder() {
        let hata = calistir(r#"{"sayilar":[1],"yon":"rastgele"}"#)
            .expect_err("geçersiz yön reddedilmeli");
        assert!(matches!(hata, HizmetHatasi::GecersizIstek(_)));
    }
}
