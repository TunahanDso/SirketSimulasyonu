namespace SirketMotoru.Protokol.Mesajlar;

public sealed class IsIstegiMesaji
{
    public string MesajTuru { get; init; } =
        "is_istegi";

    public string IstekKimligi { get; init; } =
        Guid.NewGuid().ToString("N");

    public string IsKimligi { get; init; } =
        string.Empty;

    public long TickNumarasi { get; init; }

    public string MusteriKimligi { get; init; } =
        string.Empty;

    public string HizmetKimligi { get; init; } =
        string.Empty;

    public string HizmetSurumu { get; init; } =
        string.Empty;

    public decimal TeklifEdilenTutar { get; init; }

    public int ZamanAsimiMs { get; init; } =
        5_000;

    public string IstekVerisiJson { get; init; } =
        "{}";

    public DateTimeOffset OlusturulmaZamani { get; init; } =
        DateTimeOffset.UtcNow;

    public void Dogrula()
    {
        if (string.IsNullOrWhiteSpace(
                IstekKimligi))
        {
            throw new InvalidOperationException(
                "İstek kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(
                IsKimligi))
        {
            throw new InvalidOperationException(
                "İş kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(
                MusteriKimligi))
        {
            throw new InvalidOperationException(
                "Müşteri kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(
                HizmetKimligi))
        {
            throw new InvalidOperationException(
                "Hizmet kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(
                HizmetSurumu))
        {
            throw new InvalidOperationException(
                "Hizmet sürümü boş olamaz.");
        }

        if (TeklifEdilenTutar <= 0)
        {
            throw new InvalidOperationException(
                "Teklif edilen tutar sıfırdan büyük olmalıdır.");
        }

        if (ZamanAsimiMs <= 0)
        {
            throw new InvalidOperationException(
                "Zaman aşımı süresi sıfırdan büyük olmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(
                IstekVerisiJson))
        {
            throw new InvalidOperationException(
                "İstek verisi boş olamaz.");
        }
    }
}