using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace YetkiliServisGazAcma.Business.Services;

public static class PrivateDocumentStorage
{
    public static string Root(IWebHostEnvironment environment, IConfiguration? configuration, string category)
    {
        var configured = configuration?["DocumentStorage:RootPath"];
        if (string.IsNullOrWhiteSpace(configured))
            return LegacyRoot(environment, category);

        if (!Path.IsPathFullyQualified(configured))
            throw new InvalidOperationException("DocumentStorage:RootPath mutlak bir yol olmalidir.");

        var root = Path.GetFullPath(configured);
        if (IsInRoot(root, environment.ContentRootPath))
            throw new InvalidOperationException("DocumentStorage:RootPath uygulamanin yayin dizini icinde olamaz.");

        var webRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath);
        if (IsInRoot(root, webRoot))
            throw new InvalidOperationException("DocumentStorage:RootPath wwwroot icinde olamaz.");

        return Path.Combine(root, category);
    }

    public static string LegacyRoot(IWebHostEnvironment environment, string category)
        => Path.Combine(environment.ContentRootPath, "App_Data", category);

    public static string? ExistingFile(IWebHostEnvironment environment, IConfiguration? configuration,
        string category, string relativePath)
    {
        foreach (var root in new[] { Root(environment, configuration, category), LegacyRoot(environment, category) }.Distinct())
        {
            var fullRoot = Path.GetFullPath(root);
            var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            if (IsInRoot(path, fullRoot) && File.Exists(path))
                return path;
        }

        return null;
    }

    public static bool IsInRoot(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
