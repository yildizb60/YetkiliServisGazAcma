namespace YetkiliServisGazAcma.Business.Services
{
    public record SmsGonderimSonucu(bool Basarili, string? MesajId = null, string? Hata = null);

    public interface ISmsProvider
    {
        string ProviderName { get; }
        Task<SmsGonderimSonucu> GonderAsync(string telefon, string mesaj);
    }
}
