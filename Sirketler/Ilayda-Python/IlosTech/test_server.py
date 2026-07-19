import json
import unittest

import server


class IlosTechTests(unittest.TestCase):
    def test_all_core_services_are_advertised(self) -> None:
        expected = {
            "matematik.topla",
            "matematik.carp",
            "veri.ortalama-hesapla",
            "metin.kelime-say",
            "metin.karakter-say",
            "veri.medyan-hesapla",
            "veri.standart-sapma",
            "dizi.sirala",
            "matematik.asal-carpanlar",
            "metin.frekans-analizi",
        }
        advertised = {item.service_id for item in server.SERVICE_LIST}
        self.assertTrue(expected.issubset(advertised))

    def test_core_contracts(self) -> None:
        self.assertEqual(server.core_sum({"sayilar": [1, 2, 3]}), {"sonuc": 6})
        self.assertEqual(server.core_product({"sayilar": [2, 3, 4]}), {"sonuc": 24})
        self.assertEqual(server.core_median({"sayilar": [9, 1, 5, 3]}), {"sonuc": 4.0})
        self.assertEqual(server.core_stddev({"sayilar": [2, 4, 4, 4, 5, 5, 7, 9]}), {"sonuc": 2.0})
        self.assertEqual(server.core_sort({"sayilar": [3, 1, 2], "yon": "artan"}), {"sonuc": [1, 2, 3]})
        self.assertEqual(server.core_prime_factors({"sayi": 360}), {"sonuc": [2, 2, 2, 3, 3, 5]})
        self.assertEqual(server.core_frequency({"metin": "Tunix tunix motor"}), {"sonuc": {"motor": 1, "tunix": 2}})

    def test_utf16_character_count(self) -> None:
        self.assertEqual(server.core_character_count({"metin": "A🚀"}), {"sonuc": 3})

    def test_security_probe_is_rejected(self) -> None:
        message = {
            "istekKimligi": "request-security-test",
            "isKimligi": "job-security-test",
            "hizmetKimligi": "matematik.topla",
            "hizmetSurumu": "1.0",
            "istekVerisiJson": json.dumps(
                {
                    "sayilar": [1, 2],
                    "_guvenlikSinamasi": {"etiket": "motor-saldiri-v1"},
                }
            ),
        }
        result = server.execute_job(message)
        self.assertFalse(result["basarili"])
        self.assertEqual(result["hataKodu"], "GUVENLIK_REDDI")

    def test_app_manifests_pass_required_shape(self) -> None:
        applications = {item["uygulamaKimligi"]: item for item in server.application_manifests()}
        self.assertIn("ilos-isosyal", applications)
        self.assertIn("ilos-imail", applications)
        self.assertGreaterEqual(len(applications["ilos-isosyal"]["ozellikler"]), 5)
        self.assertGreaterEqual(len(applications["ilos-imail"]["ozellikler"]), 5)


if __name__ == "__main__":
    unittest.main()
