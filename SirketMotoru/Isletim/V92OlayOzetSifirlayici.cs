namespace SirketMotoru.Isletim;

public static class V92OlayOzetSifirlayici
{
    public static void Sifirla()
    {
        V9PazarDosyasi pazar = V9PazarDeposu.Getir();
        foreach (V9SirketPazarOzeti ozet in pazar.SirketOzetleri)
        {
            ozet.OlayGeliri = 0;
            ozet.OlayGideri = 0;
        }
        V9PazarDeposu.Guncelle(pazar);
    }
}
