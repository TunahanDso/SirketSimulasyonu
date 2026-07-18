use serde::{Deserialize, Serialize};

use super::HizmetHatasi;

#[derive(Debug, Deserialize)]
struct KelimeSayIstegi {
    metin: String,
}

#[derive(Debug, Serialize)]
struct KelimeSaySonucu {
    sonuc: usize,
}

pub fn calistir(istek_verisi_json: &str) -> Result<String, HizmetHatasi> {
    let istek: KelimeSayIstegi = serde_json::from_str(istek_verisi_json)
        .map_err(|hata| HizmetHatasi::GecersizIstek(hata.to_string()))?;

    let sonuc = istek
        .metin
        .split([' ', '\t', '\r', '\n'])
        .filter(|parca| !parca.is_empty())
        .count();

    serde_json::to_string(&KelimeSaySonucu { sonuc })
        .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::Value;

    #[test]
    fn karisik_ayiricilarla_kelimeleri_sayar() {
        let sonuc = calistir(r#"{"metin":"Tunix  motor\nyazılım\tşirket"}"#)
            .expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert_eq!(json["sonuc"], 4);
    }

    #[test]
    fn bos_ve_ayiricilardan_olusan_metni_sifir_sayar() {
        for istek in [r#"{"metin":""}"#, r#"{"metin":" \t\r\n "}"#] {
            let sonuc = calistir(istek).expect("işlem başarılı olmalı");
            let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
            assert_eq!(json["sonuc"], 0);
        }
    }

    #[test]
    fn noktalama_isaretlerini_kelimeyi_bolmez() {
        let sonuc = calistir(r#"{"metin":"merhaba,dünya"}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert_eq!(json["sonuc"], 1);
    }

    #[test]
    fn gecersiz_istegi_reddeder() {
        let hata = calistir(r#"{"metin":123}"#).expect_err("istek reddedilmeli");
        assert!(matches!(hata, HizmetHatasi::GecersizIstek(_)));
    }
}
