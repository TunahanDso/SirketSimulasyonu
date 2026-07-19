mod dizi_sirala;
mod matematik_asal_carpanlar;
mod matematik_carp;
mod matematik_topla;
mod metin_frekans_analizi;
mod metin_karakter_say;
mod metin_kelime_say;
mod platform_release;
mod tunix_os;
mod veri_medyan_hesapla;
mod veri_ortalama_hesapla;
mod veri_standart_sapma;

use crate::protocol::{
    DIZI_SIRALA, DIZI_SIRALA_SURUMU, MATEMATIK_ASAL_CARPANLAR,
    MATEMATIK_ASAL_CARPANLAR_SURUMU, MATEMATIK_CARP, MATEMATIK_CARP_SURUMU,
    MATEMATIK_TOPLA, MATEMATIK_TOPLA_SURUMU, METIN_FREKANS_ANALIZI,
    METIN_FREKANS_ANALIZI_SURUMU, METIN_KARAKTER_SAY, METIN_KARAKTER_SAY_SURUMU,
    METIN_KELIME_SAY, METIN_KELIME_SAY_SURUMU, VERI_MEDYAN_HESAPLA,
    VERI_MEDYAN_HESAPLA_SURUMU, VERI_ORTALAMA_HESAPLA,
    VERI_ORTALAMA_HESAPLA_SURUMU, VERI_STANDART_SAPMA,
    VERI_STANDART_SAPMA_SURUMU,
};

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
            Self::DesteklenmeyenHizmet =>
                "İstenen hizmet veya hizmet sürümü Tunix tarafından sunulmuyor.".to_string(),
            Self::GecersizIstek(aciklama) => format!("Hizmet isteği geçersiz: {aciklama}"),
            Self::HesaplamaTasmasi => "Hesaplama desteklenen sayısal sınırı aştı.".to_string(),
            Self::SonucSerilestirme(aciklama) =>
                format!("Hizmet sonucu JSON biçimine dönüştürülemedi: {aciklama}"),
        }
    }
}

pub fn hizmeti_calistir(
    hizmet_kimligi: &str,
    hizmet_surumu: &str,
    istek_verisi_json: &str,
) -> Result<String, HizmetHatasi> {
    match (hizmet_kimligi, hizmet_surumu) {
        (MATEMATIK_TOPLA, MATEMATIK_TOPLA_SURUMU) => matematik_topla::calistir(istek_verisi_json),
        (MATEMATIK_CARP, MATEMATIK_CARP_SURUMU) => matematik_carp::calistir(istek_verisi_json),
        (VERI_ORTALAMA_HESAPLA, VERI_ORTALAMA_HESAPLA_SURUMU) => veri_ortalama_hesapla::calistir(istek_verisi_json),
        (METIN_KELIME_SAY, METIN_KELIME_SAY_SURUMU) => metin_kelime_say::calistir(istek_verisi_json),
        (METIN_KARAKTER_SAY, METIN_KARAKTER_SAY_SURUMU) => metin_karakter_say::calistir(istek_verisi_json),
        (VERI_MEDYAN_HESAPLA, VERI_MEDYAN_HESAPLA_SURUMU) => veri_medyan_hesapla::calistir(istek_verisi_json),
        (VERI_STANDART_SAPMA, VERI_STANDART_SAPMA_SURUMU) => veri_standart_sapma::calistir(istek_verisi_json),
        (DIZI_SIRALA, DIZI_SIRALA_SURUMU) => dizi_sirala::calistir(istek_verisi_json),
        (MATEMATIK_ASAL_CARPANLAR, MATEMATIK_ASAL_CARPANLAR_SURUMU) => matematik_asal_carpanlar::calistir(istek_verisi_json),
        (METIN_FREKANS_ANALIZI, METIN_FREKANS_ANALIZI_SURUMU) => metin_frekans_analizi::calistir(istek_verisi_json),

        // Motorun V6 standart hizmet kimlikleri, Tunix'in gerçekten çalışan koduna bağlanır.
        ("kimlik.oturum-dogrula", "1.0") => platform_release::calistir("tunix.kimlik.dogrula", istek_verisi_json),
        ("profil.profil-getir", "1.0") => platform_release::calistir("tunix.tingram.profil.getir", istek_verisi_json),
        ("sosyal.gonderi-olustur", "1.0") => platform_release::calistir("tunix.tingram.gonderi.olustur", istek_verisi_json),
        ("sosyal.akisi-getir", "1.0") => platform_release::calistir("tunix.tingram.akisi.getir", istek_verisi_json),
        ("sosyal.etkilesim-kaydet", "1.0") => platform_release::calistir("tunix.tingram.etkilesim", istek_verisi_json),
        ("sosyal.yorum-ekle", "1.0") => platform_release::calistir("tunix.tingram.yorum", istek_verisi_json),
        ("sosyal.icerik-ara", "1.0") => platform_release::calistir("tunix.tingram.arama", istek_verisi_json),
        ("eposta.gonder", "1.0") => platform_release::calistir("tunix.tmail.gonder", istek_verisi_json),
        ("eposta.gelen-kutusu", "1.0") => platform_release::calistir("tunix.tmail.gelen-kutusu", istek_verisi_json),
        ("eposta.ara", "1.0") => platform_release::calistir("tunix.tmail.ara", istek_verisi_json),
        ("eposta.spam-kontrol", "1.0") => platform_release::calistir("tunix.tmail.spam-kontrol", istek_verisi_json),
        ("eposta.ek-yukle", "1.0") => platform_release::calistir("tunix.tmail.ek-yukle", istek_verisi_json),
        ("eposta.klasor-olustur", "1.0") => platform_release::calistir("tunix.tmail.klasor", istek_verisi_json),

        (kimlik, "1.0") if kimlik.starts_with("isletim.")
            || kimlik == "kimlik.kullanici-dogrula"
            || kimlik == "guvenlik.istek-dogrula" =>
            tunix_os::calistir(kimlik, istek_verisi_json),

        (kimlik, "1.0") if kimlik.starts_with("tunix.") =>
            platform_release::calistir(kimlik, istek_verisi_json),
        _ => Err(HizmetHatasi::DesteklenmeyenHizmet),
    }
}
