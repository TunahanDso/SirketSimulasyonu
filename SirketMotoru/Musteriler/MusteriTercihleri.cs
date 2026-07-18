namespace SirketMotoru.Musteriler;

public sealed class MusteriTercihleri
{
    public double FiyatAgirligi { get; set; }

    public double ItibarAgirligi { get; set; }

    public double HizAgirligi { get; set; }

    public double GuvenilirlikAgirligi { get; set; }

    public void NormalizeEt()
    {
        double toplam =
            FiyatAgirligi +
            ItibarAgirligi +
            HizAgirligi +
            GuvenilirlikAgirligi;

        if (toplam <= 0)
        {
            FiyatAgirligi = 0.25;
            ItibarAgirligi = 0.25;
            HizAgirligi = 0.25;
            GuvenilirlikAgirligi = 0.25;

            return;
        }

        FiyatAgirligi /= toplam;
        ItibarAgirligi /= toplam;
        HizAgirligi /= toplam;
        GuvenilirlikAgirligi /= toplam;
    }
}