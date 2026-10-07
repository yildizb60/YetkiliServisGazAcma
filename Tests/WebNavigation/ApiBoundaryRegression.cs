using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;

internal static class ApiBoundaryRegression
{
    public static void Run()
    {
        var assembly = typeof(MarkaApiClient).Assembly;
        var passed = 0;
        foreach (var client in assembly.GetTypes().Where(t => t.IsPublic && t.Name.EndsWith("ApiClient", StringComparison.Ordinal)))
        {
            foreach (var method in client.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                CheckContract(method.ReturnType, new HashSet<Type>(), client.Name + "." + method.Name);
            Console.WriteLine("PASS: " + client.Name + " returns contracts without persistence entity graphs");
            passed++;
        }

        foreach (var controller in assembly.GetTypes().Where(t => !t.IsAbstract && typeof(Controller).IsAssignableFrom(t)))
        {
            if (controller.GetConstructors().SelectMany(c => c.GetParameters()).Any(p => typeof(DbContext).IsAssignableFrom(p.ParameterType)))
                throw new InvalidOperationException("MVC controller depends on persistence: " + controller.Name);
            passed++;
        }

        foreach (var type in new[] { typeof(MarkaApiDto), typeof(MarkaKaydetDto), typeof(DagitimSirketApiDto),
                     typeof(PanelSirketDto), typeof(PanelKimlikApiSonuc), typeof(UrunKategoriApiDto), typeof(HomeOzetCevap) })
        {
            if (type.Assembly != typeof(ApiIslemSonuc).Assembly)
                throw new InvalidOperationException("Contract must be shared with API: " + type.Name);
            passed++;
        }
        Console.WriteLine($"{passed} API boundary checks passed.");
    }

    private static void CheckContract(Type type, HashSet<Type> visited, string path)
    {
        if (type == typeof(void) || type.IsPrimitive || type.IsEnum || !visited.Add(type)) return;
        if (type.IsArray) { CheckContract(type.GetElementType()!, visited, path); return; }
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments()) CheckContract(argument, visited, path);
        if (type.Namespace == "YetkiliServisGazAcma.Entities")
            throw new InvalidOperationException("API client exposes persistence entity " + type.Name + " at " + path);
        if (type.Namespace?.StartsWith("YetkiliServisGazAcma", StringComparison.Ordinal) != true) return;
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            CheckContract(property.PropertyType, visited, path + "." + property.Name);
    }
}
