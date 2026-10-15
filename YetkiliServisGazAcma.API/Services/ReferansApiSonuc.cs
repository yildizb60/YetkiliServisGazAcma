namespace YetkiliServisGazAcma.API.Services;

public enum ReferansApiDurum
{
    Basarili,
    KimlikGerekli,
    Yasak,
    Bulunamadi,
    Gecersiz
}

public sealed record ReferansApiSonuc<T>(ReferansApiDurum Durum, T? Veri = default, string? Mesaj = null)
    where T : class;
