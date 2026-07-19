namespace SirketMotoru.Protokol.Mesajlar;

public sealed class FinansDurumuMesaji
{
    public string MesajTuru { get; init; } =
        MesajTurleri.FinansDurumu;

    public string MesajKimligi { get; init; } =
        Guid.NewGuid().ToString("N");

    public string ProtokolSurumu { get; init; } =
        string.Empty;

    public string SirketKimligi { get; init; } =
        string.Empty;

    public long TickNumarasi { get; init; }

    public decimal Kasa { get; init; }

    public decimal ToplamGelir { get; init; }

    public decimal ToplamIade { get; init; }

    public decimal ToplamCeza { get; init; }

    public decimal BekleyenOdeme { get; init; }

    public decimal NetGelir { get; init; }

    public int TamamlananIsSayisi { get; init; }

    public int BasarisizIsSayisi { get; init; }

    public int ZamanAsiminaUgrayanIsSayisi { get; init; }

    public int IptalEdilenIsSayisi { get; init; }

    public double ItibarPuani { get; init; }

    public double GuvenilirlikPuani { get; init; }

    public double OrtalamaMusteriMemnuniyeti { get; init; }

    public DateTimeOffset GuncellenmeZamani { get; init; } =
        DateTimeOffset.UtcNow;

    public void Dogrula()
    {
        if (!string.Equals(
                MesajTuru,
                MesajTurleri.FinansDurumu,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Beklenmeyen finans mesaj türü: {MesajTuru}");
        }

        if (string.IsNullOrWhiteSpace(
                MesajKimligi))
        {
            throw new InvalidOperationException(
                "Finans mesajı kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(
                ProtokolSurumu))
        {
            throw new InvalidOperationException(
                "Finans mesajı protokol sürümü boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(
                SirketKimligi))
        {
            throw new InvalidOperationException(
                "Finans mesajındaki şirket kimliği boş olamaz.");
        }

        if (Kasa < 0 ||
            ToplamGelir < 0 ||
            ToplamIade < 0 ||
            ToplamCeza < 0 ||
            BekleyenOdeme < 0)
        {
            throw new InvalidOperationException(
                "Finans mesajındaki parasal değerler negatif olamaz.");
        }
    }
}
