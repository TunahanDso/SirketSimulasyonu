namespace SirketMotoru.Ayarlar;

public static class MotorAyarlariDogrulayicisi
{
    public static void Dogrula(MotorAyarlari ayarlar)
    {
        ArgumentNullException.ThrowIfNull(ayarlar);

        if (string.IsNullOrWhiteSpace(ayarlar.MotorKimligi))
        {
            throw new InvalidOperationException(
                "Motor kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(ayarlar.ProtokolSurumu))
        {
            throw new InvalidOperationException(
                "Motor protokol sürümü boş olamaz.");
        }

        if (ayarlar.TickSuresiSaniye < 1)
        {
            throw new InvalidOperationException(
                "Tick süresi en az 1 saniye olmalıdır.");
        }

        if (ayarlar.BaglantiZamanAsimiMs < 100)
        {
            throw new InvalidOperationException(
                "Bağlantı zaman aşımı en az 100 ms olmalıdır.");
        }

        if (ayarlar.MesajZamanAsimiMs < 100)
        {
            throw new InvalidOperationException(
                "Mesaj zaman aşımı en az 100 ms olmalıdır.");
        }

        if (ayarlar.AzamiMesajBoyutuByte < 256)
        {
            throw new InvalidOperationException(
                "Azami mesaj boyutu en az 256 byte olmalıdır.");
        }

        if (ayarlar.CanliPanoPortu is < 1 or > 65_535)
        {
            throw new InvalidOperationException(
                "Canlı pano portu 1-65535 arasında olmalıdır.");
        }

        if (ayarlar.CanliPanoYenilemeMs is < 250 or > 60_000)
        {
            throw new InvalidOperationException(
                "Canlı pano yenileme süresi 250-60000 ms arasında olmalıdır.");
        }

        if (ayarlar.SirketYonetimPortu is < 1 or > 65_535)
        {
            throw new InvalidOperationException(
                "Şirket yönetim portu 1-65535 arasında olmalıdır.");
        }

        if (ayarlar.SirketYonetimAktif &&
            ayarlar.SirketYonetimPortu == ayarlar.CanliPanoPortu)
        {
            throw new InvalidOperationException(
                "Şirket yönetim portu ile canlı pano portu aynı olamaz.");
        }

        if (ayarlar.SirketYonetimOturumDakika is < 10 or > 1_440)
        {
            throw new InvalidOperationException(
                "Şirket yönetim oturumu 10-1440 dakika arasında olmalıdır.");
        }

        if (ayarlar.Sirketler is null || ayarlar.Sirketler.Count == 0)
        {
            throw new InvalidOperationException(
                "Motor ayarlarında en az bir şirket bulunmalıdır.");
        }

        HashSet<string> kimlikler =
            new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> adresVePortlar =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (SirketBaglantiAyari sirket in ayarlar.Sirketler)
        {
            SirketAyariniDogrula(
                sirket,
                kimlikler,
                adresVePortlar);
        }
    }

    private static void SirketAyariniDogrula(
        SirketBaglantiAyari sirket,
        HashSet<string> kimlikler,
        HashSet<string> adresVePortlar)
    {
        ArgumentNullException.ThrowIfNull(sirket);

        sirket.SirketKimligi = sirket.SirketKimligi?.Trim()
                                ?? string.Empty;
        sirket.SirketAdi = sirket.SirketAdi?.Trim()
                           ?? string.Empty;
        sirket.Adres = sirket.Adres?.Trim()
                       ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sirket.SirketKimligi))
        {
            throw new InvalidOperationException(
                "Şirket kimliği boş olamaz.");
        }

        if (!kimlikler.Add(sirket.SirketKimligi))
        {
            throw new InvalidOperationException(
                $"Tekrarlanan şirket kimliği: {sirket.SirketKimligi}");
        }

        if (string.IsNullOrWhiteSpace(sirket.SirketAdi))
        {
            throw new InvalidOperationException(
                $"{sirket.SirketKimligi} için şirket adı boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(sirket.Adres))
        {
            throw new InvalidOperationException(
                $"{sirket.SirketAdi} için sunucu adresi boş olamaz.");
        }

        if (sirket.Port is < 1 or > 65_535)
        {
            throw new InvalidOperationException(
                $"{sirket.SirketAdi} için port geçersiz: {sirket.Port}");
        }

        string adresVePort = $"{sirket.Adres}:{sirket.Port}";
        if (!adresVePortlar.Add(adresVePort))
        {
            throw new InvalidOperationException(
                "Aynı adres ve port birden fazla şirket tarafından " +
                $"kullanılıyor: {adresVePort}");
        }
    }
}
