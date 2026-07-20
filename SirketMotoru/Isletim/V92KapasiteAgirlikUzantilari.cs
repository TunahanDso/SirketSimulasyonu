using SirketMotoru.Protokol;

namespace SirketMotoru.Isletim;

/// <summary>
/// V9.2 toplu hizmet kapasitesi dağıtımında int talep sayaçlarını
/// double ağırlıklara güvenli biçimde dönüştürür.
///
/// List&lt;SunulanHizmet&gt; için daha özel olan bu overload, standart LINQ
/// ToDictionary overload'ından önce seçilir ve mevcut çağrının dönüş tipini
/// Dictionary&lt;SunulanHizmet, double&gt; olarak üretir.
/// </summary>
internal static class V92KapasiteAgirlikUzantilari
{
    public static Dictionary<SunulanHizmet, double> ToDictionary(
        this List<SunulanHizmet> kaynak,
        Func<SunulanHizmet, SunulanHizmet> anahtarSecici,
        Func<SunulanHizmet, int> agirlikSecici)
    {
        ArgumentNullException.ThrowIfNull(kaynak);
        ArgumentNullException.ThrowIfNull(anahtarSecici);
        ArgumentNullException.ThrowIfNull(agirlikSecici);

        Dictionary<SunulanHizmet, double> sonuc = new();
        foreach (SunulanHizmet hizmet in kaynak)
            sonuc[anahtarSecici(hizmet)] = agirlikSecici(hizmet);

        return sonuc;
    }
}
