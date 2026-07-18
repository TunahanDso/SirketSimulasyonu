namespace SirketMotoru.Protokol.Mesajlar;

public sealed class IsSonucuMesaji
{
    public string MesajTuru { get; init; } =
        "is_sonucu";

    public string IstekKimligi { get; init; } =
        string.Empty;

    public string IsKimligi { get; init; } =
        string.Empty;

    public string SirketKimligi { get; init; } =
        string.Empty;

    public bool Basarili { get; init; }

    public string SonucVerisiJson { get; init; } =
        "{}";

    public string? HataKodu { get; init; }

    public string? HataMesaji { get; init; }

    public double IslemSuresiMs { get; init; }

    public DateTimeOffset TamamlanmaZamani { get; init; } =
        DateTimeOffset.UtcNow;

    public void Dogrula()
    {
        if (string.IsNullOrWhiteSpace(
                IstekKimligi))
        {
            throw new InvalidOperationException(
                "Sonuç mesajındaki istek kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(
                IsKimligi))
        {
            throw new InvalidOperationException(
                "Sonuç mesajındaki iş kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(
                SirketKimligi))
        {
            throw new InvalidOperationException(
                "Sonuç mesajındaki şirket kimliği boş olamaz.");
        }

        if (IslemSuresiMs < 0)
        {
            throw new InvalidOperationException(
                "İşlem süresi negatif olamaz.");
        }

        if (Basarili &&
            string.IsNullOrWhiteSpace(
                SonucVerisiJson))
        {
            throw new InvalidOperationException(
                "Başarılı iş sonucunun veri alanı boş olamaz.");
        }

        if (!Basarili &&
            string.IsNullOrWhiteSpace(
                HataMesaji))
        {
            throw new InvalidOperationException(
                "Başarısız iş sonucunda hata mesajı bulunmalıdır.");
        }
    }
}