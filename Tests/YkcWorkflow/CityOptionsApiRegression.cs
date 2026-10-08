using System.Collections;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

internal static class CityOptionsApiRegression
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

        // Empty async sets exercise aggregate assembly without a database provider or connection.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options)
        {
            Ys_Firmalar = new EmptySet<Ys_Firma>(), Ys_DevreyeAlmalar = new EmptySet<Ys_DevreyeAlma>(),
            Ys_Subeler = new EmptySet<Ys_Sube>(), Ys_Markalar = new EmptySet<Ys_Marka>(),
            UrunKategoriler = new EmptySet<UrunKategori>()
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SehirFirmaKodlari:API Z"] = "API_Z", ["SehirFirmaKodlari:API A"] = "API_A",
            ["SehirFirmaKodlari:Ignored city"] = " "
        }).Build();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddSingleton(db)
            .AddScoped<SehirFirmaKoduService>().AddScoped<AdminYetkiliServisListeService>()
            .AddScoped<AdminYetkiliServisYonetimApiService>().AddScoped<AdminRaporApiService>()
            .BuildServiceProvider();
        using var scope = services.CreateScope();
        var listService = scope.ServiceProvider.GetRequiredService<AdminYetkiliServisListeService>();
        var reportService = scope.ServiceProvider.GetRequiredService<AdminRaporApiService>();
        foreach (var company in new int?[] { null, 7 })
        {
            var list = await listService.ListeleAsync(new() { SirketId = company, Q = "no matching rows", Il = "API Z" });
            Check(list.Servisler.Count == 0 && list.Sehirler.SequenceEqual(["API A", "API Z"]),
                "Service-list options use API configuration independently of company scope and matching rows");
            var report = await reportService.DevreyeAlmalarAsync(new() { Il = "API Z" }, company);
            Check(report.Islemler.Count == 0 && report.Sehirler.SequenceEqual(list.Sehirler),
                "Commissioning options use the same API configuration even when there are no results");
            var editor = await scope.ServiceProvider.GetRequiredService<AdminYetkiliServisYonetimApiService>().EditorAsync(0, company);
            Check(editor != null && editor.Sehirler.SequenceEqual(list.Sehirler)
                && editor.SehirFirmaKodlari.Count == 2 && editor.SehirFirmaKodlari["API Z"] == "API_Z"
                && editor.SehirFirmaKodlari["API A"] == "API_A",
                "Editor includes canonical city options and company codes using the existing registered service");
            var roundTrip = JsonSerializer.Deserialize<AdminYetkiliServisEditorDto>(JsonSerializer.Serialize(editor));
            Check(roundTrip != null && roundTrip.Sehirler.SequenceEqual(list.Sehirler)
                && roundTrip.SehirFirmaKodlari["API Z"] == "API_Z",
                "City options and code-map contracts survive JSON round-trip");
        }
        Check((await new AdminYetkiliServisListeService(db).ListeleAsync(new())).Sehirler.Count == 0
            && (await new AdminRaporApiService(db).DevreyeAlmalarAsync(null, null)).Sehirler.Count == 0,
            "Existing one-argument service fixtures remain valid without manufacturing local defaults");
        Check(new AdminYetkiliServisListeDto().Sehirler.Count == 0
            && new AdminDevreyeAlmaListeDto().Sehirler.Count == 0
            && new AdminYetkiliServisEditorDto().SehirFirmaKodlari.Count == 0,
            "Missing option fields retain safe collection defaults");
        Console.WriteLine($"{passed} API city option checks passed without a server or database.");
    }

    private sealed class EmptySet<T> : DbSet<T>, IQueryable<T>, IAsyncEnumerable<T> where T : class
    {
        private readonly EmptyQuery<T> query = new();
        public override IEntityType EntityType => throw new InvalidOperationException("No database model is used in this fixture.");
        Type IQueryable.ElementType => typeof(T);
        Expression IQueryable.Expression => ((IQueryable<T>)query).Expression;
        IQueryProvider IQueryable.Provider => ((IQueryable<T>)query).Provider;
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();
        public override IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => query.GetAsyncEnumerator(cancellationToken);
    }

    private sealed class EmptyQuery<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public EmptyQuery() : base(Array.Empty<T>()) { }
        IQueryProvider IQueryable.Provider => new EmptyProvider();
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new EmptyEnumerator<T>();
    }

    private sealed class EmptyProvider : IQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new EmptyQuery<TElement>();
        public object? Execute(Expression expression) => throw new NotSupportedException();
        public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException();
    }

    private sealed class EmptyEnumerator<T> : IAsyncEnumerator<T>
    {
        public T Current => throw new InvalidOperationException("The fixture has no rows.");
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(false);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
