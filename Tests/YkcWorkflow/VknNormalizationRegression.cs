using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace Tests.YkcWorkflow;

internal static class VknNormalizationRegression
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + label);
            passed++;
            Console.WriteLine("PASS: " + label);
        }

        foreach (var (input, expected) in new (string?, string)[]
        {
            (null, ""), (" \t", ""), ("1234567890", "1234567890"),
            (" 123-456-7890 ", "1234567890"), ("(012) 345.678/90", "01234567890"),
            ("\u0661\u0662\u0663\u0664\u0665\u0666\u0667\u0668\u0669\u0660", "")
        })
        {
            var normalized = VergiNoKurali.Normalize(input);
            Check(normalized == expected && VergiNoKurali.Normalize(normalized) == normalized,
                "VKN normalization preserves ASCII digits and leading zeros, and is idempotent: " + input);
        }

        // No provider is configured: unexpected database access fails the regression.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var canonical = new AppKullanici { UserName = "1234567890", KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis };
        var nationalId = new AppKullanici { UserName = "01234567890", KullaniciTipi = KullaniciTipiDegerleri.YetkiliServis };
        var email = new AppKullanici { UserName = "email-owner", Email = "1234567890@example.test" };
        var exact = new AppKullanici { UserName = "123-456-7890" };
        using var users = new ProbeUsers(db, [canonical, nationalId, email]);
        var flow = new OturumAkisApiService(users, null!, null!, null!,
            Options.Create(new SertifikaliFirmaKimlikOptions()), Options.Create(new SmsOptions()),
            new EphemeralDataProtectionProvider(), null!, db);

        foreach (var login in new[] { "1234567890", " 123-456-7890 ", "123 456 7890", "123.456/7890" })
            Check(await flow.FindAsync(login) == canonical, "Formatted VKN resolves the canonical account: " + login);
        Check(await flow.FindAsync("(012) 345.678/90") == nationalId,
            "Formatted eleven-digit identifier preserves its leading zero");
        Check(await flow.FindAsync(" 1234567890@example.test ") == email,
            "Exact email lookup is not rewritten as a VKN");
        users.Accounts.Add(exact);
        Check(await flow.FindAsync("123-456-7890") == exact,
            "Existing exact usernames take precedence over normalized VKN fallback");
        foreach (var login in new[] { "unknown1234567890", "1234567890@missing.test", "123-45", "---", "\u06611234567890" })
            Check(await flow.FindAsync(login) == null, "Non-VKN or malformed login is not aliased: " + login);

        var registration = new YetkiliServisService(null!, null!);
        var administration = new AdminYetkiliServisYonetimApiService(null!, null!);
        foreach (var invalid in new[] { "123-45", "---", "\u0661\u0662\u0663\u0664\u0665\u0666\u0667\u0668\u0669\u0660" })
        {
            var firm = new Ys_Firma { VergiNo = invalid };
            var result = await registration.Kayit(firm, "Fixture123!", [], []);
            Check(!result.basarili && firm.VergiNo == VergiNoKurali.Normalize(invalid),
                "Registration validates normalized VKN before persistence: " + invalid);
            var adminResult = await administration.EkleAsync(new() { FirmaAdi = "Fixture", VergiNo = invalid }, new(), 1);
            Check(!adminResult.Basarili && adminResult.Mesaj == "VKN/TCKN 10 veya 11 haneli olmalidir",
                "Admin registration rejects malformed supplied VKN before persistence: " + invalid);
        }
        Check(await registration.VknIleGetir("---") == null,
            "Empty normalized VKN cannot match an optional blank firm identifier");
        Console.WriteLine($"{passed} VKN normalization checks passed without a database or server.");
    }

    private sealed class ProbeUsers(AppDbContext db, List<AppKullanici> accounts) : UserManager<AppKullanici>(
        new UserStore<AppKullanici>(db), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<AppKullanici>(),
        [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<AppKullanici>>.Instance)
    {
        public List<AppKullanici> Accounts { get; } = accounts;
        public override Task<AppKullanici?> FindByEmailAsync(string email)
            => Task.FromResult(Accounts.SingleOrDefault(x => x.Email == email));
        public override Task<AppKullanici?> FindByNameAsync(string userName)
            => Task.FromResult(Accounts.SingleOrDefault(x => x.UserName == userName));
    }
}
