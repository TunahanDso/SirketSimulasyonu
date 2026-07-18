use rust_decimal::Decimal;
use serde::{Deserialize, Serialize};

pub const PROTOKOL_SURUMU: &str = "0.1";
pub const SIRKET_KIMLIGI: &str = "tunahan-tunix";
pub const SIRKET_ADI: &str = "Tunix";
pub const SUNUCU_SURUMU: &str = "0.2.0";
pub const MATEMATIK_TOPLA: &str = "matematik.topla";
pub const MATEMATIK_TOPLA_SURUMU: &str = "1.0";

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
    fn tanitim_mesaji_camel_case_ve_hizmet_bilgisi_icerir() {
        let mesaj = SirketTanitimMesaji {
            mesaj_turu: "sirketTanitim",
            mesaj_kimligi: "tunix-test-1".to_string(),
            protokol_surumu: PROTOKOL_SURUMU,
            sirket_kimligi: SIRKET_KIMLIGI,
            sirket_adi: SIRKET_ADI,
            sunucu_surumu: SUNUCU_SURUMU,
            hizmetler: vec![SunulanHizmet {
                hizmet_kimligi: MATEMATIK_TOPLA,
                hizmet_surumu: MATEMATIK_TOPLA_SURUMU,
                birim_fiyat: Decimal::ONE,
                azami_eszamanli_is: 1,
                aktif: true,
            }],
        };

        let json = serde_json::to_value(mesaj).expect("mesaj serialize edilmeli");

        assert_eq!(json["mesajTuru"], "sirketTanitim");
        assert_eq!(json["sirketKimligi"], SIRKET_KIMLIGI);
        assert_eq!(json["hizmetler"][0]["hizmetKimligi"], MATEMATIK_TOPLA);
        assert_eq!(json["hizmetler"][0]["azamiEszamanliIs"], 1);
    }
}
