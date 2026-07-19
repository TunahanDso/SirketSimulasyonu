using System.Reflection;
using SirketMotoru.Kayit;

namespace SirketMotoru.Isletim;

internal static class PazarFiyatOzTesti
{
    public static void Dogrula()
    {
        MethodInfo talepMetodu = typeof(PazarFiyatYoneticisi).GetMethod(
            "TalepCarpani",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Fiyat pazarı talep eğrisi bulunamadı.");
        MethodInfo kayipMetodu = typeof(PazarFiyatYoneticisi).GetMethod(
            "FiyatKayipOrani",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Fiyat pazarı kayıp eğrisi bulunamadı.");
        MethodInfo gelirMetodu = typeof(PazarGelirDuzeltmeYoneticisi).GetMethod(
            "GelirKabulCarpani",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Fiyat pazarı gelir kabul eğrisi bulunamadı.");

        double[] oranlar =
        [
            0.30, 0.65, 1.00, 1.20, 1.50, 2.00,
            2.50, 3.00, 5.00, 20.00, 100.00, 5_000.00
        ];
        double oncekiTalep = double.MaxValue;
        double oncekiKayip = 0;
        double oncekiGelir = double.MaxValue;
        foreach (double oran in oranlar)
        {
            double talep = Cagir(talepMetodu, oran);
            double kayip = Cagir(kayipMetodu, oran);
            double gelir = Cagir(gelirMetodu, oran);

            if (talep > oncekiTalep + 0.000001)
                throw new InvalidOperationException(
                    $"Fiyat talep eğrisi monoton değil. Oran: {oran:N2}");
            if (kayip + 0.000001 < oncekiKayip)
                throw new InvalidOperationException(
                    $"Fiyat kayıp eğrisi monoton değil. Oran: {oran:N2}");
            if (gelir > oncekiGelir + 0.000001)
                throw new InvalidOperationException(
                    $"Fiyat gelir eğrisi monoton değil. Oran: {oran:N2}");
            if (talep is < 0 or > 1.5 ||
                kayip is < 0 or > 1 ||
                gelir is < 0 or > 1)
            {
                throw new InvalidOperationException(
                    $"Fiyat eğrisi sınır dışı. Oran: {oran:N2}");
            }

            oncekiTalep = talep;
            oncekiKayip = kayip;
            oncekiGelir = gelir;
        }

        double milyonlukSosyalOrani = 1_000_000d / 200d;
        double milyonTalebi = Cagir(talepMetodu, milyonlukSosyalOrani);
        double milyonKaybi = Cagir(kayipMetodu, milyonlukSosyalOrani);
        double milyonGeliri = Cagir(gelirMetodu, milyonlukSosyalOrani);
        if (milyonTalebi > 0.000001 ||
            milyonKaybi < 0.95 ||
            milyonGeliri > 0.000001)
        {
            throw new InvalidOperationException(
                "1.000.000 TL sosyal medya fiyatı pazar dışı davranmıyor.");
        }

        KonsolKayitcisi.Basari(
            $"Fiyat pazarı öz testi geçti | " +
            $"1.000.000/200 oranı: ×{milyonlukSosyalOrani:N0} | " +
            $"Talep: {milyonTalebi:N3} | " +
            $"Kayıp: %{milyonKaybi * 100:N0} | " +
            $"Kabul edilen gelir: %{milyonGeliri * 100:N0}");
    }

    private static double Cagir(MethodInfo metot, double oran)
    {
        object? sonuc = metot.Invoke(null, [oran]);
        return sonuc is double deger
            ? deger
            : throw new InvalidOperationException(
                "Fiyat eğrisi geçerli sayı döndürmedi.");
    }
}
