package main

import (
	"bufio"
	"encoding/json"
	"fmt"
	"net"
	"time"
)

const (
	sirketKimligi  = "mustafa-mudaf"
	sirketAdi      = "Mudaf"
	protokolSurumu = "0.1"
	sunucuSurumu   = "0.1"
	mudafPortu     = ":7002"
)

type TemelMesaj struct {
	MesajTuru      string `json:"mesajTuru"`
	MesajKimligi   string `json:"mesajKimligi"`
	ProtokolSurumu string `json:"protokolSurumu"`
}

type MerhabaMesaji struct {
	MesajTuru      string `json:"mesajTuru"`
	MesajKimligi   string `json:"mesajKimligi"`
	ProtokolSurumu string `json:"protokolSurumu"`
	MotorKimligi   string `json:"motorKimligi"`
}

type SirketTanitimMesaji struct {
	MesajTuru      string `json:"mesajTuru"`
	MesajKimligi   string `json:"mesajKimligi"`
	ProtokolSurumu string `json:"protokolSurumu"`
	SirketKimligi  string `json:"sirketKimligi"`
	SirketAdi      string `json:"sirketAdi"`
	SunucuSurumu   string `json:"sunucuSurumu"`
}

type KayitSonucuMesaji struct {
	MesajTuru      string `json:"mesajTuru"`
	MesajKimligi   string `json:"mesajKimligi"`
	ProtokolSurumu string `json:"protokolSurumu"`
	Basarili       bool   `json:"basarili"`
	SirketKimligi  string `json:"sirketKimligi"`
	Aciklama       string `json:"aciklama"`
}

type SaglikKontroluMesaji struct {
	MesajTuru      string `json:"mesajTuru"`
	MesajKimligi   string `json:"mesajKimligi"`
	ProtokolSurumu string `json:"protokolSurumu"`
	IstekKimligi   string `json:"istekKimligi"`
	TickNumarasi   int64  `json:"tickNumarasi"`
}

type SaglikSonucuMesaji struct {
	MesajTuru      string `json:"mesajTuru"`
	MesajKimligi   string `json:"mesajKimligi"`
	ProtokolSurumu string `json:"protokolSurumu"`
	IstekKimligi   string `json:"istekKimligi"`
	Durum          string `json:"durum"`
	AktifBaglanti  int    `json:"aktifBaglanti"`
	KuyrukUzunlugu int    `json:"kuyrukUzunlugu"`
}

func main() {
	fmt.Println("Mudaf sunucusu baslatiliyor...")

	dinleyici, hata := net.Listen("tcp", mudafPortu)
	if hata != nil {
		fmt.Println("Sunucu baslatilamadi:", hata)
		return
	}

	defer dinleyici.Close()

	fmt.Println("Mudaf 7002 portunda motoru bekliyor...")

	for {
		baglanti, hata := dinleyici.Accept()
		if hata != nil {
			fmt.Println("Baglanti kabul edilemedi:", hata)
			continue
		}

		fmt.Println("Motor Mudaf sunucusuna baglandi.")

		go motorBaglantisiniYonet(baglanti)
	}
}

func motorBaglantisiniYonet(baglanti net.Conn) {
	defer baglanti.Close()

	okuyucu := bufio.NewScanner(baglanti)
	yazici := json.NewEncoder(baglanti)

	for okuyucu.Scan() {
		gelenMesaj := okuyucu.Bytes()

		var temelMesaj TemelMesaj

		hata := json.Unmarshal(gelenMesaj, &temelMesaj)
		if hata != nil {
			fmt.Println("Gecersiz JSON geldi:", hata)
			continue
		}

		fmt.Println("Motordan mesaj geldi:", temelMesaj.MesajTuru)

		switch temelMesaj.MesajTuru {

		case "merhaba":
			merhabaMesajiniIsle(gelenMesaj, yazici)

		case "kayitSonucu":
			kayitSonucunuIsle(gelenMesaj)

		case "saglikKontrolu":
			saglikKontrolunuIsle(gelenMesaj, yazici)

		default:
			fmt.Println("Bilinmeyen mesaj turu:", temelMesaj.MesajTuru)
		}
	}

	if hata := okuyucu.Err(); hata != nil {
		fmt.Println("Motor baglantisi hatasi:", hata)
	} else {
		fmt.Println("Motor baglantiyi kapatti.")
	}
}

func merhabaMesajiniIsle(
	gelenMesaj []byte,
	yazici *json.Encoder,
) {
	var merhaba MerhabaMesaji

	hata := json.Unmarshal(gelenMesaj, &merhaba)
	if hata != nil {
		fmt.Println("Merhaba mesaji okunamadi:", hata)
		return
	}

	fmt.Println("Motor kimligi:", merhaba.MotorKimligi)
	fmt.Println("Motor protokol surumu:", merhaba.ProtokolSurumu)

	tanitim := SirketTanitimMesaji{
		MesajTuru:      "sirketTanitim",
		MesajKimligi:   yeniMesajKimligi(),
		ProtokolSurumu: protokolSurumu,
		SirketKimligi:  sirketKimligi,
		SirketAdi:      sirketAdi,
		SunucuSurumu:   sunucuSurumu,
	}

	hata = yazici.Encode(tanitim)
	if hata != nil {
		fmt.Println("Sirket tanitimi gonderilemedi:", hata)
		return
	}

	fmt.Println("Mudaf tanitim mesaji motora gonderildi.")
}

func kayitSonucunuIsle(gelenMesaj []byte) {
	var sonuc KayitSonucuMesaji

	hata := json.Unmarshal(gelenMesaj, &sonuc)
	if hata != nil {
		fmt.Println("Kayit sonucu okunamadi:", hata)
		return
	}

	if sonuc.Basarili {
		fmt.Println("Mudaf motor tarafindan kaydedildi.")
		fmt.Println("Aciklama:", sonuc.Aciklama)
	} else {
		fmt.Println("Mudaf kaydi reddedildi.")
		fmt.Println("Sebep:", sonuc.Aciklama)
	}
}

func saglikKontrolunuIsle(
	gelenMesaj []byte,
	yazici *json.Encoder,
) {
	var kontrol SaglikKontroluMesaji

	hata := json.Unmarshal(gelenMesaj, &kontrol)
	if hata != nil {
		fmt.Println("Saglik kontrolu okunamadi:", hata)
		return
	}

	cevap := SaglikSonucuMesaji{
		MesajTuru:      "saglikSonucu",
		MesajKimligi:   yeniMesajKimligi(),
		ProtokolSurumu: protokolSurumu,
		IstekKimligi:   kontrol.IstekKimligi,
		Durum:          "calisiyor",
		AktifBaglanti:  1,
		KuyrukUzunlugu: 0,
	}

	hata = yazici.Encode(cevap)
	if hata != nil {
		fmt.Println("Saglik cevabi gonderilemedi:", hata)
		return
	}

	fmt.Printf(
		"Tick %d saglik kontrolune cevap verildi.\n",
		kontrol.TickNumarasi,
	)
}

func yeniMesajKimligi() string {
	return fmt.Sprintf(
		"mudaf-%d",
		time.Now().UnixNano(),
	)
}
