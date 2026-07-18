mod matematik_topla;

use crate::protocol::{MATEMATIK_TOPLA, MATEMATIK_TOPLA_SURUMU};

#[derive(Debug)]
pub enum HizmetHatasi {
    DesteklenmeyenHizmet,
    GecersizIstek(String),
    HesaplamaTasmasi,
    SonucSerilestirme(String),
}

impl HizmetHatasi {
    pub fn kodu(&self) -> &'static str {
        match self {
            Self::DesteklenmeyenHizmet => "HIZMET_DESTEKLENMIYOR",
            Self::GecersizIstek(_) => "ISTEK_VERISI_GECERSIZ",
            Self::HesaplamaTasmasi => "HESAPLAMA_TASMASI",
            Self::SonucSerilestirme(_) => "SONUC_URETILEMEDI",
        }
    }

    pub fn mesaji(&self) -> String {
        match self {
            Self::DesteklenmeyenHizmet => {
                "İstenen hizmet veya hizmet sürümü Tunix tarafından sunulmuyor.".to_string()
            }
            Self::GecersizIstek(aciklama) => {
                format!("Toplama isteği geçersiz: {aciklama}")
            }
            Self::HesaplamaTasmasi => {
                "Toplama işlemi desteklenen sayısal sınırı aştı.".to_string()
            }
            Self::SonucSerilestirme(aciklama) => {
                format!("Toplama sonucu JSON biçimine dönüştürülemedi: {aciklama}")
            }
        }
    }
}

pub fn hizmeti_calistir(
    hizmet_kimligi: &str,
    hizmet_surumu: &str,
    istek_verisi_json: &str,
) -> Result<String, HizmetHatasi> {
    match (hizmet_kimligi, hizmet_surumu) {
        (MATEMATIK_TOPLA, MATEMATIK_TOPLA_SURUMU) => {
            matematik_topla::calistir(istek_verisi_json)
        }
        _ => Err(HizmetHatasi::DesteklenmeyenHizmet),
    }
}
