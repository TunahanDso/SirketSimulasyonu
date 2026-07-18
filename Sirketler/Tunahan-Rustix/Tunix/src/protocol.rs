use rust_decimal::Decimal;
use serde::{Deserialize, Serialize};

pub const PROTOKOL_SURUMU: &str = "0.1";
pub const SIRKET_KIMLIGI: &str = "tunahan-tunix";
pub const SIRKET_ADI: &str = "Tunix";
pub const SUNUCU_SURUMU: &str = "0.4.0";
pub const MATEMATIK_TOPLA: &str = "matematik.topla";
pub const MATEMATIK_TOPLA_SURUMU: &str = "1.0";
pub const MATEMATIK_CARP: &str = "matematik.carp";
pub const MATEMATIK_CARP_SURUMU: &str = "1.0";
pub const VERI_ORTALAMA_HESAPLA: &str = "veri.ortalama-hesapla";
pub const VERI_ORTALAMA_HESAPLA_SURUMU: &str = "1.0";
pub const METIN_KELIME_SAY: &str = "metin.kelime-say";
pub const METIN_KELIME_SAY_SURUMU: &str = "1.0";
pub const METIN_KARAKTER_SAY: &str = "metin.karakter-say";
pub const METIN_KARAKTER_SAY_SURUMU: &str = "1.0";

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MesajBasligi {
    pub mesaj_turu: String,
}

#[allow(dead_code)]
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MerhabaMesaji {
    pub mesaj_turu: String,
    pub mesaj_kimligi: String,
    pub protokol_surumu: String,
    pub motor_kimligi: String,
}

#[allow(dead_code)]
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct KayitSonucuMesaji {
    pub mesaj_turu: String,
    pub mesaj_kimligi: String,
    pub protokol_surumu: String,
    pub basarili: bool,
    pub sirket_kimligi: String,
    pub aciklama: String,
}

#[allow(dead_code)]
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SaglikKontroluMesaji {
    pub mesaj_turu: String,
    pub mesaj_kimligi: String,
    pub protokol_surumu: String,
    pub istek_kimligi: String,
    pub tick_numarasi: i64,
}

#[allow(dead_code)]
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IsIstegiMesaji {
    pub mesaj_turu: String,
    pub istek_kimligi: String,
    pub is_kimligi: String,
    pub tick_numarasi: i64,
    pub musteri_kimligi: String,
    pub hizmet_kimligi: String,
    pub hizmet_surumu: String,
    pub teklif_edilen_tutar: Decimal,
    pub zaman_asimi_ms: u64,
    pub istek_verisi_json: String,
    #[serde(default)]
    pub olusturulma_zamani: Option<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SirketTanitimMesaji {
    pub mesaj_turu: &'static str,
    pub mesaj_kimligi: String,
    pub protokol_surumu: &'static str,
    pub sirket_kimligi: &'static str,
    pub sirket_adi: &'static str,
    pub sunucu_surumu: &'static str,
    pub hizmetler: Vec<SunulanHizmet>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SunulanHizmet {
    pub hizmet_kimligi: &'static str,
    pub hizmet_surumu: &'static str,
    #[serde(with = "rust_decimal::serde::arbitrary_precision")]
    pub birim_fiyat: Decimal,
    pub azami_eszamanli_is: u32,
    pub aktif: bool,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SaglikSonucuMesaji {
    pub mesaj_turu: &'static str,
    pub mesaj_kimligi: String,
    pub protokol_surumu: &'static str,
    pub istek_kimligi: String,
    pub durum: &'static str,
    pub aktif_baglanti: u32,
    pub kuyruk_uzunlugu: u32,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct IsSonucuMesaji {
    pub mesaj_turu: &'static str,
    pub istek_kimligi: String,
    pub is_kimligi: String,
    pub sirket_kimligi: &'static str,
    pub basarili: bool,
    pub sonuc_verisi_json: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub hata_kodu: Option<&'static str>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub hata_mesaji: Option<String>,
    pub islem_suresi_ms: f64,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn tanitim_mesaji_bes_hizmeti_icerir() {
        let mesaj = SirketTanitimMesaji {
            mesaj_turu: "sirketTanitim",
            mesaj_kimligi: "tunix-test-1".to_string(),
            protokol_surumu: PROTOKOL_SURUMU,
            sirket_kimligi: SIRKET_KIMLIGI,
            sirket_adi: SIRKET_ADI,
            sunucu_surumu: SUNUCU_SURUMU,
            hizmetler: vec![
                SunulanHizmet { hizmet_kimligi: MATEMATIK_TOPLA, hizmet_surumu: MATEMATIK_TOPLA_SURUMU, birim_fiyat: Decimal::ONE, azami_eszamanli_is: 1, aktif: true },
                SunulanHizmet { hizmet_kimligi: MATEMATIK_CARP, hizmet_surumu: MATEMATIK_CARP_SURUMU, birim_fiyat: Decimal::new(2, 0), azami_eszamanli_is: 1, aktif: true },
                SunulanHizmet { hizmet_kimligi: VERI_ORTALAMA_HESAPLA, hizmet_surumu: VERI_ORTALAMA_HESAPLA_SURUMU, birim_fiyat: Decimal::new(3, 0), azami_eszamanli_is: 1, aktif: true },
                SunulanHizmet { hizmet_kimligi: METIN_KELIME_SAY, hizmet_surumu: METIN_KELIME_SAY_SURUMU, birim_fiyat: Decimal::new(2, 0), azami_eszamanli_is: 1, aktif: true },
                SunulanHizmet { hizmet_kimligi: METIN_KARAKTER_SAY, hizmet_surumu: METIN_KARAKTER_SAY_SURUMU, birim_fiyat: Decimal::ONE, azami_eszamanli_is: 1, aktif: true },
            ],
        };

        let json = serde_json::to_value(mesaj).expect("mesaj serialize edilmeli");
        let hizmetler = json["hizmetler"].as_array().expect("hizmetler dizi olmalı");

        assert_eq!(hizmetler.len(), 5);
        assert!(hizmetler.iter().all(|h| h["birimFiyat"].is_number()));
        assert_eq!(hizmetler[0]["hizmetKimligi"], MATEMATIK_TOPLA);
        assert_eq!(hizmetler[1]["hizmetKimligi"], MATEMATIK_CARP);
        assert_eq!(hizmetler[2]["hizmetKimligi"], VERI_ORTALAMA_HESAPLA);
        assert_eq!(hizmetler[3]["hizmetKimligi"], METIN_KELIME_SAY);
        assert_eq!(hizmetler[4]["hizmetKimligi"], METIN_KARAKTER_SAY);
    }
}
