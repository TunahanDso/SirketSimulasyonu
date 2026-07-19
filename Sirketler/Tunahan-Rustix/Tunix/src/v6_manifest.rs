use crate::protocol::*;
use rust_decimal::Decimal;

pub fn standart_hizmetler() -> Vec<SunulanHizmet> {
    vec![
        h(STD_KIMLIK_OTURUM, 3, 18),
        h(STD_KIMLIK_KULLANICI, 3, 18),
        h(STD_PROFIL_GETIR, 5, 14),
        h(STD_SOSYAL_GONDERI, 7, 12),
        h(STD_SOSYAL_AKIS, 6, 14),
        h(STD_SOSYAL_ETKILESIM, 4, 18),
        h(STD_SOSYAL_YORUM, 4, 14),
        h(STD_SOSYAL_ARAMA, 6, 10),
        h(STD_EPOSTA_GONDER, 7, 14),
        h(STD_EPOSTA_GELEN, 5, 16),
        h(STD_EPOSTA_ARA, 6, 12),
        h(STD_EPOSTA_SPAM, 4, 20),
        h(STD_EPOSTA_EK, 6, 10),
        h(STD_EPOSTA_KLASOR, 3, 16),
        h(STD_OS_SUREC, 12, 12),
        h(STD_OS_SUREC_DURDUR, 8, 14),
        h(STD_OS_SUREC_LISTE, 5, 20),
        h(STD_OS_KAYNAK, 14, 12),
        h(STD_OS_KAYNAK_BIRAK, 6, 16),
        h(STD_OS_DOSYA, 12, 14),
        h(STD_OS_PAKET, 15, 10),
        h(STD_OS_PAKET_KALDIR, 8, 12),
        h(STD_OS_AG, 13, 10),
        h(STD_OS_GUNCELLEME_KONTROL, 4, 20),
        h(STD_OS_GUNCELLEME_KUR, 18, 8),
        h(STD_OS_LOG, 5, 18),
        h(STD_OS_UYGULAMA, 14, 12),
        h(STD_GUVENLIK_ISTEK, 5, 24),

        // Tunix 0.9 geniş hizmet paketi: metin, dizi, veri, arama ve bildirim.
        h("metin.buyuk-harf", 5, 24),
        h("metin.kucuk-harf", 5, 24),
        h("metin.birlestir", 7, 20),
        h("metin.parcala", 7, 20),
        h("metin.ozetle", 12, 14),
        h("metin.anahtar-kelime", 14, 12),
        h("dizi.filtrele", 9, 18),
        h("dizi.birlestir", 8, 20),
        h("dizi.kesisim", 11, 16),
        h("dizi.birlesim", 11, 16),
        h("dizi.parcala", 8, 20),
        h("dizi.dogrula", 6, 24),
        h("veri.filtrele", 12, 16),
        h("veri.temizle", 13, 16),
        h("veri.normalize-et", 15, 14),
        h("veri.gruplandir", 16, 12),
        h("veri.birlestir", 14, 14),
        h("veri.korelasyon", 20, 10),
        h("arama.ara", 10, 20),
        h("arama.sirala", 9, 20),
        h("arama.otomatik-tamamla", 11, 18),
        h("arama.yazim-duzelt", 10, 18),
        h("bildirim.push-gonder", 8, 24),
        h("bildirim.zamanla", 9, 20),
    ]
}

fn h(kimlik: &'static str, fiyat: i64, kapasite: u32) -> SunulanHizmet {
    SunulanHizmet {
        hizmet_kimligi: kimlik,
        hizmet_surumu: PLATFORM_SURUMU,
        birim_fiyat: Decimal::new(fiyat, 0),
        azami_eszamanli_is: kapasite,
        aktif: true,
    }
}

pub fn uygulamalar() -> Vec<SunulanUygulama> {
    vec![tunix_os(), tingram(), tmail()]
}

fn tunix_os() -> SunulanUygulama {
    SunulanUygulama {
        uygulama_kimligi: "tunix-os",
        uygulama_adi: "Tunix OS",
        surum: "1.0.0",
        kategori: "isletim-sistemi",
        urun_turu: "isletim-sistemi",
        dagitim_modeli: "sunucu-ve-istemci",
        aciklama: "Rust ile geliştirilen süreç, kaynak, dosya sistemi, paket, ağ ve güvenlik katmanlarına sahip Tunix işletim sistemi.",
        ozellikler: vec![
            oz("os.surec.baslat", STD_OS_SUREC, true),
            oz("os.kaynak.ata", STD_OS_KAYNAK, true),
            oz("os.dosya.sistemi", STD_OS_DOSYA, true),
            oz("os.paket.kur", STD_OS_PAKET, true),
            oz("os.ag.yapilandir", STD_OS_AG, true),
            oz("os.kullanici.dogrula", STD_KIMLIK_KULLANICI, true),
            oz("os.istek.dogrula", STD_GUVENLIK_ISTEK, true),
            oz("os.surec.listele", STD_OS_SUREC_LISTE, false),
            oz("os.guncelleme.kontrol", STD_OS_GUNCELLEME_KONTROL, false),
            oz("os.guncelleme.kur", STD_OS_GUNCELLEME_KUR, false),
            oz("os.log.topla", STD_OS_LOG, false),
            oz("os.uygulama.calistir", STD_OS_UYGULAMA, false),
        ],
        bagimliliklar: vec![],
        desteklenen_protokoller: vec!["tunix-tlink"],
        desteklenen_platformlar: vec!["tunix-os"],
        gerekli_platformlar: vec![],
        mimariler: vec!["x86_64", "aarch64"],
        etiketler: vec!["rust", "isletim-sistemi", "tunix-ekosistemi"],
    }
}

fn tingram() -> SunulanUygulama {
    SunulanUygulama {
        uygulama_kimligi: "tunix-tingram",
        uygulama_adi: "Tingram",
        surum: "1.1.0",
        kategori: "sosyal-medya",
        urun_turu: "uygulama",
        dagitim_modeli: "tunix-os",
        aciklama: "Tunix OS üzerinde Tlink ile çalışan Rust sosyal medya uygulaması.",
        ozellikler: vec![
            oz("kimlik.oturum", STD_KIMLIK_OTURUM, true),
            oz("profil.getir", STD_PROFIL_GETIR, true),
            oz("sosyal.gonderi", STD_SOSYAL_GONDERI, true),
            oz("sosyal.akis", STD_SOSYAL_AKIS, true),
            oz("sosyal.etkilesim", STD_SOSYAL_ETKILESIM, true),
            oz("sosyal.yorum", STD_SOSYAL_YORUM, false),
            oz("sosyal.arama", STD_SOSYAL_ARAMA, false),
        ],
        bagimliliklar: vec![UygulamaBagimliligi {
            sirket_kimligi: SIRKET_KIMLIGI,
            uygulama_kimligi: "tunix-tmail",
            asgari_surum: "1.0",
            protokol_kimligi: "tunix-tlink",
            bagimlilik_turu: "uygulama",
            zorunlu: false,
        }],
        desteklenen_protokoller: vec!["tunix-tlink"],
        desteklenen_platformlar: vec!["tunix-os"],
        gerekli_platformlar: vec!["tunix-os"],
        mimariler: vec!["x86_64", "aarch64"],
        etiketler: vec!["sosyal", "rust", "tunix-os"],
    }
}

fn tmail() -> SunulanUygulama {
    SunulanUygulama {
        uygulama_kimligi: "tunix-tmail",
        uygulama_adi: "Tmail",
        surum: "1.1.0",
        kategori: "eposta",
        urun_turu: "uygulama",
        dagitim_modeli: "tunix-os",
        aciklama: "Tunix OS üzerinde Tlink ile çalışan spam korumalı Rust e-posta uygulaması.",
        ozellikler: vec![
            oz("kimlik.oturum", STD_KIMLIK_OTURUM, true),
            oz("eposta.gonder", STD_EPOSTA_GONDER, true),
            oz("eposta.gelen", STD_EPOSTA_GELEN, true),
            oz("eposta.ara", STD_EPOSTA_ARA, true),
            oz("eposta.spam", STD_EPOSTA_SPAM, true),
            oz("eposta.ek", STD_EPOSTA_EK, false),
            oz("eposta.klasor", STD_EPOSTA_KLASOR, false),
        ],
        bagimliliklar: vec![],
        desteklenen_protokoller: vec!["tunix-tlink"],
        desteklenen_platformlar: vec!["tunix-os"],
        gerekli_platformlar: vec!["tunix-os"],
        mimariler: vec!["x86_64", "aarch64"],
        etiketler: vec!["eposta", "rust", "tunix-os"],
    }
}

fn oz(
    ozellik_kimligi: &'static str,
    hizmet_kimligi: &'static str,
    zorunlu: bool,
) -> UygulamaOzelligi {
    UygulamaOzelligi {
        ozellik_kimligi,
        hizmet_kimligi,
        hizmet_surumu: PLATFORM_SURUMU,
        aciklama: "Motor V6 standart hizmetine bağlı çalışan özellik.",
        zorunlu,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn tunix_os_zorunlu_hizmetleri_tasiyor() {
        let os = uygulamalar().into_iter().find(|x| x.uygulama_kimligi == "tunix-os").unwrap();
        assert_eq!(os.kategori, "isletim-sistemi");
        assert!(os.ozellikler.iter().filter(|x| x.zorunlu).count() >= 7);
    }

    #[test]
    fn tingram_standart_sosyal_hizmetlerini_kullaniyor() {
        let app = uygulamalar().into_iter().find(|x| x.uygulama_kimligi == "tunix-tingram").unwrap();
        assert!(app.ozellikler.iter().any(|x| x.hizmet_kimligi == STD_SOSYAL_GONDERI));
        assert_eq!(app.gerekli_platformlar, vec!["tunix-os"]);
    }

    #[test]
    fn genis_paket_yirmi_dort_hizmet_tasiyor() {
        let ids = standart_hizmetler().into_iter().filter(|x| {
            x.hizmet_kimligi.starts_with("metin.") ||
            x.hizmet_kimligi.starts_with("dizi.") ||
            x.hizmet_kimligi.starts_with("arama.") ||
            x.hizmet_kimligi.starts_with("bildirim.") ||
            matches!(x.hizmet_kimligi, "veri.filtrele"|"veri.temizle"|"veri.normalize-et"|"veri.gruplandir"|"veri.birlestir"|"veri.korelasyon")
        }).count();
        assert!(ids >= 24);
    }
}
