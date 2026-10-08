using Microsoft.Extensions.Configuration;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;

internal static class PublicDirectoryApiRegression
{
    public static async Task RunAsync()
    {
        // An unconfigured city returns before any database or domain write can occur.
        var cities = new SehirFirmaKoduService(new ConfigurationBuilder().Build(), null!);
        var service = new YetkiliServisBasvuruApiService(null!, null!, cities);
        var passed = 0;
        async Task Reject(YetkiliServisBasvuruDto? application, string message)
        {
            var result = await service.KayitAsync(application);
            if (result.Basarili || result.FirmaId != null || result.Mesaj != message)
                throw new InvalidOperationException("FAIL: canonical registration validation: " + message);
            passed++;
            Console.WriteLine("PASS: canonical registration validation: " + message);
        }

        await Reject(null, "Kayit bilgileri zorunludur");
        await Reject(new(), "Firma adi zorunludur");
        foreach (var (change, message) in new (Action<YetkiliServisBasvuruDto>, string)[]
        {
            (x => x.VergiNo = " ", "VKN zorunludur"),
            (x => x.VergiNo = "123", "VKN/TCKN 10 veya 11 haneli olmalidir"),
            (x => x.Sifre = "", "Sifre zorunludur"),
            (x => x.Sifre = "12345", "Sifre en az 6 karakter olmalidir"),
            (x => x.FaaliyetIli = " ", "Il bilgisi zorunludur"),
            (x => x.Ilce = new string('a', 101), "Ilce en fazla 100 karakter olabilir"),
            (x => x.Email = "not-an-email", "E-posta formati gecersiz"),
            (x => x.Telefon = "123", "Telefon numarasi 05XXXXXXXXX veya 90XXXXXXXXXX formatinda olmalidir"),
            (x => x.TcKimlikNo = null, "TC kimlik no 11 haneli ve sayisal olmalidir"),
            (x => x.TcKimlikNo = "1000000000x", "TC kimlik no 11 haneli ve sayisal olmalidir")
        })
        {
            var application = Application();
            change(application);
            await Reject(application, message);
        }

        const string missingCompany = "Seçilen il için aktif dağıtım şirketi bulunamadı veya şirket eşleşmesi belirsiz. Lütfen sistem yöneticisiyle iletişime geçin.";
        await Reject(Application(), missingCompany);
        var optionalFields = Application();
        optionalFields.Email = null;
        optionalFields.Ilce = null;
        optionalFields.VergiNo = "10000000001";
        optionalFields.MarkaIdleri = null;
        optionalFields.KategoriIdleri = null;
        await Reject(optionalFields, missingCompany);
        Console.WriteLine($"{passed} public registration API checks passed without a database.");
    }

    private static YetkiliServisBasvuruDto Application() => new()
    {
        FirmaAdi = "Fixture", VergiNo = "1000000001", Sifre = "FixtureOnly123!",
        FaaliyetIli = "Unconfigured fixture city", Ilce = "District",
        Email = "fixture@example.invalid", Telefon = "05550000000", TcKimlikNo = "10000000000",
        MarkaIdleri = [], KategoriIdleri = []
    };
}
