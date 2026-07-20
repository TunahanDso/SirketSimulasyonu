use std::collections::BTreeMap;

use serde::{Deserialize, Serialize};

use super::HizmetHatasi;

#[derive(Debug, Deserialize)]
struct FrekansIstegi {
    metin: String,
}

#[derive(Debug, Serialize)]
struct FrekansSonucu {
    sonuc: BTreeMap<String, u64>,
}

pub fn calistir(istek_verisi_json: &str) -> Result<String, HizmetHatasi> {
    let istek: FrekansIstegi = serde_json::from_str(istek_verisi_json)
        .map_err(|hata| HizmetHatasi::GecersizIstek(hata.to_string()))?;

    let mut frekanslar = BTreeMap::new();

    for kelime in istek
        .metin
        .split([' ', '\t', '\r', '\n'])
        .filter(|kelime| !kelime.is_empty())
    {
        let anahtar = kelime.to_lowercase();
        let sayac = frekanslar.entry(anahtar).or_insert(0_u64);
        *sayac = sayac
            .checked_add(1)
            .ok_or(HizmetHatasi::HesaplamaTasmasi)?;
    }

    serde_json::to_string(&FrekansSonucu { sonuc: frekanslar })
        .map_err(|hata| HizmetHatasi::SonucSerilestirme(hata.to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::Value;

    #[test]
    fn kelimeleri_kucultup_sayar() {
        let sonuc = calistir(r#"{"metin":"Tunix tunix MOTOR motor motor"}"#)
            .expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
        assert_eq!(json["sonuc"]["tunix"], 2);
        assert_eq!(json["sonuc"]["motor"], 3);
    }

    #[test]
    fn bos_metinde_bos_harita_dondurur() {
        let sonuc = calistir(r#"{"metin":""}"#).expect("işlem başarılı olmalı");
        let json: Value = serde_json::from_str(&sonuc).expect("sonuç JSON olmalı");
        assert_eq!(json["sonuc"], serde_json::json!({}));
    }
}
