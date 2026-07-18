const net = require("net");

const PORT = 7003;
const HOST = "0.0.0.0";

const SIRKET_KIMLIGI = "ugur-ugax";
const SIRKET_ADI = "Ugax";
const PROTOKOL_SURUMU = "0.1";
const SUNUCU_SURUMU = "0.1";

const sunucu = net.createServer((baglanti) => {
    console.log("Motor Ugur Link sunucusuna baglandi.");

    baglanti.setEncoding("utf8");

    let mesajTamponu = "";

    baglanti.on("data", (veri) => {
        console.log("Motordan ham veri geldi:");
        console.log(veri);

        mesajTamponu += veri;

        let satirSonu;

        while ((satirSonu = mesajTamponu.indexOf("\n")) !== -1) {
            const mesajSatiri = mesajTamponu
                .slice(0, satirSonu)
                .trim();

            mesajTamponu = mesajTamponu.slice(satirSonu + 1);

            if (mesajSatiri.length === 0) {
                continue;
            }

            mesajiIsle(mesajSatiri, baglanti);
        }
    });

    baglanti.on("end", () => {
        console.log("Motor baglantiyi kapatti.");
    });

    baglanti.on("close", () => {
        console.log("Motor baglantisi sonlandi.");
    });

    baglanti.on("error", (hata) => {
        console.log("Baglanti hatasi:", hata.message);
    });
});

function mesajiIsle(mesajSatiri, baglanti) {
    let mesaj;

    try {
        mesaj = JSON.parse(mesajSatiri);
    } catch (hata) {
        console.log("Gecersiz JSON geldi:", hata.message);
        console.log("Okunamayan mesaj:", mesajSatiri);
        return;
    }

    console.log("Motordan mesaj geldi:", mesaj.mesajTuru);

    switch (mesaj.mesajTuru) {
        case "merhaba":
            merhabaMesajiniIsle(mesaj, baglanti);
            break;

        case "kayitSonucu":
            kayitSonucunuIsle(mesaj);
            break;

        case "saglikKontrolu":
            saglikKontrolunuIsle(mesaj, baglanti);
            break;

        default:
            console.log("Bilinmeyen mesaj turu:", mesaj.mesajTuru);
            break;
    }
}

function merhabaMesajiniIsle(mesaj, baglanti) {
    console.log("Motor kimligi:", mesaj.motorKimligi);
    console.log("Motor protokol surumu:", mesaj.protokolSurumu);

    const tanitimMesaji = {
        mesajTuru: "sirketTanitim",
        mesajKimligi: yeniMesajKimligi(),
        protokolSurumu: PROTOKOL_SURUMU,
        sirketKimligi: SIRKET_KIMLIGI,
        sirketAdi: SIRKET_ADI,
        sunucuSurumu: SUNUCU_SURUMU
    };

    mesajGonder(baglanti, tanitimMesaji);

    console.log("Ugur Link tanitim mesaji motora gonderildi.");
}

function kayitSonucunuIsle(mesaj) {
    if (mesaj.basarili) {
        console.log("Ugur Link motor tarafindan kaydedildi.");
        console.log("Aciklama:", mesaj.aciklama);
    } else {
        console.log("Ugur Link kaydi reddedildi.");
        console.log("Sebep:", mesaj.aciklama);
    }
}

function saglikKontrolunuIsle(mesaj, baglanti) {
    const cevap = {
        mesajTuru: "saglikSonucu",
        mesajKimligi: yeniMesajKimligi(),
        protokolSurumu: PROTOKOL_SURUMU,
        istekKimligi: mesaj.istekKimligi,
        durum: "calisiyor",
        aktifBaglanti: 1,
        kuyrukUzunlugu: 0
    };

    mesajGonder(baglanti, cevap);

    console.log(
        `Tick ${mesaj.tickNumarasi} saglik kontrolune cevap verildi.`
    );
}

function mesajGonder(baglanti, mesaj) {
    const jsonMesaji = JSON.stringify(mesaj);

    console.log("Motora gonderilen mesaj:");
    console.log(jsonMesaji);

    baglanti.write(jsonMesaji + "\n");
}

function yeniMesajKimligi() {
    return `ugur-link-${Date.now()}-${Math.floor(Math.random() * 1000000)}`;
}

sunucu.listen(PORT, HOST, () => {
    console.log("Ugur Link sunucusu baslatildi.");
    console.log(`Ugur Link ${PORT} portunda motoru bekliyor...`);
});

sunucu.on("error", (hata) => {
    console.log("Sunucu hatasi:", hata.message);
});