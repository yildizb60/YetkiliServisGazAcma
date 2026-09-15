using Microsoft.AspNetCore.Mvc;

namespace YetkiliServisGazAcma.API.Controllers;

internal static class HassasDosyaYaniti
{
    public static FileContentResult HassasDosya(
        this ControllerBase controller,
        byte[] bytes,
        string contentType,
        string dosyaAdi)
    {
        controller.Response.Headers.CacheControl = "private, no-store";
        controller.Response.Headers.Pragma = "no-cache";
        controller.Response.Headers.Expires = "0";
        return new FileContentResult(bytes, contentType) { FileDownloadName = dosyaAdi };
    }
}
