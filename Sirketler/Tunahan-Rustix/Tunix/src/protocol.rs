use rust_decimal::Decimal;
use serde::{Deserialize, Serialize};

pub const PROTOKOL_SURUMU: &str = "0.1";
pub const SIRKET_KIMLIGI: &str = "tunahan-tunix";
pub const SIRKET_ADI: &str = "Tunix";
pub const SUNUCU_SURUMU: &str = "0.8.0";

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
pub const VERI_MEDYAN_HESAPLA: &str = "veri.medyan-hesapla";
pub const VERI_MEDYAN_HESAPLA_SURUMU: &str = "1.0";
pub const VERI_STANDART_SAPMA: &str = "veri.standart-sapma";
pub const VERI_STANDART_SAPMA_SURUMU: &str = "1.0";
pub const DIZI_SIRALA: &str = "dizi.sirala";
pub const DIZI_SIRALA_SURUMU: &str = "1.0";
pub const MATEMATIK_ASAL_CARPANLAR: &str = "matematik.asal-carpanlar";
pub const MATEMATIK_ASAL_CARPANLAR_SURUMU: &str = "1.0";
pub const METIN_FREKANS_ANALIZI: &str = "metin.frekans-analizi";
pub const METIN_FREKANS_ANALIZI_SURUMU: &str = "1.0";

pub const PLATFORM_SURUMU: &str = "1.0";
pub const TUNIX_KIMLIK_DOGRULA: &str = "tunix.kimlik.dogrula";
pub const TINGRAM_PROFIL_GETIR: &str = "tunix.tingram.profil.getir";
pub const TINGRAM_GONDERI_OLUSTUR: &str = "tunix.tingram.gonderi.olustur";
pub const TINGRAM_AKISI_GETIR: &str = "tunix.tingram.akisi.getir";
pub const TINGRAM_ETKILESIM: &str = "tunix.tingram.etkilesim";
pub const TINGRAM_YORUM: &str = "tunix.tingram.yorum";
pub const TINGRAM_ARAMA: &str = "tunix.tingram.arama";
pub const TMAIL_GONDER: &str = "tunix.tmail.gonder";
pub const TMAIL_GELEN_KUTUSU: &str = "tunix.tmail.gelen-kutusu";
pub const TMAIL_ARA: &str = "tunix.tmail.ara";
pub const TMAIL_SPAM_KONTROL: &str = "tunix.tmail.spam-kontrol";
pub const TMAIL_EK_YUKLE: &str = "tunix.tmail.ek-yukle";
pub const TMAIL_KLASOR: &str = "tunix.tmail.klasor";
pub const TLINK_KIMLIK: &str = "tunix.tlink.kimlik";
pub const TLINK_PAKETLE: &str = "tunix.tlink.paketle";
pub const TLINK_DOGRULA: &str = "tunix.tlink.dogrula";

// V6 standart hizmet kimlikleri.
pub const STD_KIMLIK_OTURUM: &str = "kimlik.oturum-dogrula";
pub const STD_KIMLIK_KULLANICI: &str = "kimlik.kullanici-dogrula";
pub const STD_PROFIL_GETIR: &str = "profil.profil-getir";
pub const STD_SOSYAL_GONDERI: &str = "sosyal.gonderi-olustur";
pub const STD_SOSYAL_AKIS: &str = "sosyal.akisi-getir";
pub const STD_SOSYAL_ETKILESIM: &str = "sosyal.etkilesim-kaydet";
pub const STD_SOSYAL_YORUM: &str = "sosyal.yorum-ekle";
pub const STD_SOSYAL_ARAMA: &str = "sosyal.icerik-ara";
pub const STD_EPOSTA_GONDER: &str = "eposta.gonder";
pub const STD_EPOSTA_GELEN: &str = "eposta.gelen-kutusu";
pub const STD_EPOSTA_ARA: &str = "eposta.ara";
pub const STD_EPOSTA_SPAM: &str = "eposta.spam-kontrol";
pub const STD_EPOSTA_EK: &str = "eposta.ek-yukle";
pub const STD_EPOSTA_KLASOR: &str = "eposta.klasor-olustur";
pub const STD_OS_SUREC: &str = "isletim.surec-baslat";
pub const STD_OS_SUREC_DURDUR: &str = "isletim.surec-durdur";
pub const STD_OS_SUREC_LISTE: &str = "isletim.surec-listele";
pub const STD_OS_KAYNAK: &str = "isletim.kaynak-ata";
pub const STD_OS_KAYNAK_BIRAK: &str = "isletim.kaynak-birak";
pub const STD_OS_DOSYA: &str = "isletim.dosya-sistemi";
pub const STD_OS_PAKET: &str = "isletim.paket-kur";
pub const STD_OS_PAKET_KALDIR: &str = "isletim.paket-kaldir";
pub const STD_OS_AG: &str = "isletim.ag-yapilandir";
pub const STD_OS_GUNCELLEME_KONTROL: &str = "isletim.guncelleme-kontrol";
pub const STD_OS_GUNCELLEME_KUR: &str = "isletim.guncelleme-kur";
pub const STD_OS_LOG: &str = "isletim.log-topla";
pub const STD_OS_UYGULAMA: &str = "isletim.uygulama-calistir";
pub const STD_GUVENLIK_ISTEK: &str = "guvenlik.istek-dogrula";

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

#[allow(dead_code)]
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct FinansDurumuMesaji {
    pub mesaj_turu: String,
    pub mesaj_kimligi: String,
    pub protokol_surumu: String,
    pub sirket_kimligi: String,
    pub tick_numarasi: i64,
    pub kasa: Decimal,
    pub toplam_gelir: Decimal,
    pub toplam_iade: Decimal,
    pub toplam_ceza: Decimal,
    pub bekleyen_odeme: Decimal,
    pub net_gelir: Decimal,
    pub tamamlanan_is_sayisi: u64,
    pub basarisiz_is_sayisi: u64,
    pub zaman_asimina_ugrayan_is_sayisi: u64,
    pub iptal_edilen_is_sayisi: u64,
    pub itibar_puani: f64,
    pub guvenilirlik_puani: f64,
    pub ortalama_musteri_memnuniyeti: f64,
    pub guncellenme_zamani: String,
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
    pub uygulamalar: Vec<SunulanUygulama>,
    pub ozel_protokoller: Vec<SunulanOzelProtokol>,
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
pub struct SunulanUygulama {
    pub uygulama_kimligi: &'static str,
    pub uygulama_adi: &'static str,
    pub surum: &'static str,
    pub kategori: &'static str,
    pub urun_turu: &'static str,
    pub dagitim_modeli: &'static str,
    pub aciklama: &'static str,
    pub ozellikler: Vec<UygulamaOzelligi>,
    pub bagimliliklar: Vec<UygulamaBagimliligi>,
    pub desteklenen_protokoller: Vec<&'static str>,
    pub desteklenen_platformlar: Vec<&'static str>,
    pub gerekli_platformlar: Vec<&'static str>,
    pub mimariler: Vec<&'static str>,
    pub etiketler: Vec<&'static str>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct UygulamaOzelligi {
    pub ozellik_kimligi: &'static str,
    pub hizmet_kimligi: &'static str,
    pub hizmet_surumu: &'static str,
    pub aciklama: &'static str,
    pub zorunlu: bool,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct UygulamaBagimliligi {
    pub sirket_kimligi: &'static str,
    pub uygulama_kimligi: &'static str,
    pub asgari_surum: &'static str,
    pub protokol_kimligi: &'static str,
    pub bagimlilik_turu: &'static str,
    pub zorunlu: bool,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SunulanOzelProtokol {
    pub protokol_kimligi: &'static str,
    pub protokol_adi: &'static str,
    pub surum: &'static str,
    pub aciklama: &'static str,
    pub sema_kimligi: &'static str,
    pub sema_ozeti: &'static str,
    pub yetkinlikler: Vec<ProtokolYetkinligi>,
    pub uyumlu_protokoller: Vec<&'static str>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ProtokolYetkinligi {
    pub yetkinlik_kimligi: &'static str,
    pub hizmet_kimligi: &'static str,
    pub hizmet_surumu: &'static str,
    pub aciklama: &'static str,
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
    fn yeni_tanitim_mesaji_manifest_alanlarini_serilestirir() {
        let mesaj = SirketTanitimMesaji {
            mesaj_turu: "sirketTanitim",
            mesaj_kimligi: "test".to_string(),
            protokol_surumu: PROTOKOL_SURUMU,
            sirket_kimligi: SIRKET_KIMLIGI,
            sirket_adi: SIRKET_ADI,
            sunucu_surumu: SUNUCU_SURUMU,
            hizmetler: vec![],
            uygulamalar: vec![],
            ozel_protokoller: vec![],
        };
        let json = serde_json::to_value(mesaj).expect("tanıtım serileştirilmeli");
        assert!(json.get("uygulamalar").is_some());
        assert!(json.get("ozelProtokoller").is_some());
        assert_eq!(json["sunucuSurumu"], "0.8.0");
    }
}
