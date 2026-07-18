use serde::{Deserialize, Serialize};

use super::HizmetHatasi;

#[derive(Debug, Deserialize)]
struct KarakterSayIstegi {
    metin: String,
}

#[derive(Debug, Serialize)]
struct KarakterSaySonucu {
    sonuc: usize,
}

pub fn calistir(istek_verisi_json: &str) -> Result<String, HizmetHatasi> {
    let istek: KarakterSayIstegi = serde_json::from_str(istek_verisi_json)
        .map_err(|hata| HizmetHatasi::GecersizIstek(hata.to_string()))?;

    // C# string.Length UTF-16 kod birimi sayar. Rust'ta encode_utf16 aynı davranışı verir.
    let sonuc = istek.metin.encode_utf16().count();

    serde_json::to_string(&KarakterSaySonucu { sonuc })
        .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::Value;

    #[test]
    fn turkce_metni_utf16_uyumlu_sayar() {
        let sonuc = calistir(r#"{"metin":"İnsan için teknoloji."}"#)
            .expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert_eq!(json["sonuc"], "İnsan için teknoloji.".encode_utf16().count());
    }

    #[test]
    fn emojiyi_iki_utf16_birimi_sayar() {
        let sonuc = calistir(r#"{"metin":"A😀B"}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert_eq!(json["sonuc"], 4);
    }

    #[test]
    fn bos_metni_sifir_sayar() {
        let sonuc = calistir(r#"{"metin":""}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");

        assert_eq!(json["sonuc"], 0);
    }

    #[test]
    fn gecersiz_istegi_reddeder() {
        let hata = calistir(r#"{"metin":123}"#).expect_err("istek reddedilmeli");
        assert!(matches!(hata, HizmetHatasi::GecersizIstek(_)));
    }
}
