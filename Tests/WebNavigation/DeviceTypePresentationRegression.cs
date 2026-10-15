using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using YetkiliServisGazAcma.Models;

namespace Tests.WebNavigation;

internal static class DeviceTypePresentationRegression
{
    public static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "YetkiliServisGazAcma.slnx")))
            root = root.Parent;
        if (root == null) throw new InvalidOperationException("Repository root not found.");
        var webRoot = Path.Combine(root.FullName, "YetkiliServisGazAcma", "wwwroot");
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }

        foreach (var type in new[] { "Kombi", "kombi", "KOMBI", "  Kombi  " })
            Check(DevreyeAlmaCihazTipiSunumu.IkonUrl(type) == "/images/icons/category-kombi.webp"
                && DevreyeAlmaCihazTipiSunumu.IkonSinifi(type) == "bi-thermometer-half"
                && DevreyeAlmaCihazTipiSunumu.RenkSinifi(type) == "is-kombi",
                type + ": kombi uses the original local image and keeps its fallback and badge color class");

        Check(DevreyeAlmaCihazTipiSunumu.Etiket(" Kombi ") == "Kombi",
            "Kombi label remains unchanged");

        var kombiImage = File.ReadAllBytes(Path.Combine(webRoot, "images", "icons", "category-kombi.webp"));
        Check(kombiImage.Length is >= 20 and <= 65536
            && Encoding.ASCII.GetString(kombiImage, 0, 4) == "RIFF"
            && Encoding.ASCII.GetString(kombiImage, 8, 4) == "WEBP"
            && BinaryPrimitives.ReadUInt32LittleEndian(kombiImage.AsSpan(4, 4)) + 8 == kombiImage.Length,
            "Local kombi asset has a valid WebP header and bounded size");
        // Original asset: https://ecaservis.com.tr/assets/images/dealers-icon-9.webp
        Check(Convert.ToHexString(SHA256.HashData(kombiImage))
            == "217FEC4D2D56243565E361D342599F94A301B16A0D8C69D2A7EBBC63AF2E7ED5",
            "Local kombi asset preserves the exact original image bytes");

        foreach (var type in new[] { "Ocak", "Gaz kullanici cihazlar", "Gaz kullan\u0131c\u0131 cihazlar" })
        {
            var url = DevreyeAlmaCihazTipiSunumu.IkonUrl(type);
            Check(url != null && url.StartsWith("/images/icons/", StringComparison.Ordinal)
                && XDocument.Load(Path.Combine(webRoot, url.TrimStart('/'))).Root?.Name
                    == XName.Get("svg", "http://www.w3.org/2000/svg"),
                type + ": existing local SVG remains available");
        }

        var fontDirectory = Path.Combine(webRoot, "lib", "bootstrap-icons", "font");
        var css = File.ReadAllText(Path.Combine(fontDirectory, "bootstrap-icons.min.css"));
        Check(Regex.IsMatch(css, @"\.bi-thermometer-half::before\{content:""\\[0-9a-f]+""\}", RegexOptions.None, TimeSpan.FromSeconds(1)),
            "Bundled stylesheet retains the image-error fallback glyph");
        foreach (var extension in new[] { "woff2", "woff" })
        {
            var relativePath = "fonts/bootstrap-icons." + extension;
            var bytes = File.ReadAllBytes(Path.Combine(fontDirectory, relativePath));
            Check(css.Contains(relativePath, StringComparison.Ordinal) && bytes.Length > 1024
                && Encoding.ASCII.GetString(bytes, 0, 4) == (extension == "woff2" ? "wOF2" : "wOFF"),
                "Bundled " + extension + " font exists with a valid file signature");
        }

        Check(DevreyeAlmaCihazTipiSunumu.IkonUrl(null) == null
            && DevreyeAlmaCihazTipiSunumu.IkonSinifi(null) == "bi-fire"
            && DevreyeAlmaCihazTipiSunumu.IkonSinifi("Sofben") == "bi-water",
            "Other fallback icons remain unchanged");
        Console.WriteLine($"{passed} device-type presentation checks passed.");
    }
}
