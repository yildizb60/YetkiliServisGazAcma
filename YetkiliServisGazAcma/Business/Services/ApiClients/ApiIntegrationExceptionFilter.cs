using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace YetkiliServisGazAcma.Business.Services
{
    public class ApiIntegrationExceptionFilter : IExceptionFilter
    {
        private readonly ITempDataDictionaryFactory _tempDataFactory;
        private readonly ILogger<ApiIntegrationExceptionFilter> _logger;

        public ApiIntegrationExceptionFilter(
            ITempDataDictionaryFactory tempDataFactory,
            ILogger<ApiIntegrationExceptionFilter> logger)
        {
            _tempDataFactory = tempDataFactory;
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            var exception = UnwrapApiIntegrationException(context.Exception);
            if (exception == null)
                return;

            _logger.LogWarning(exception, "API entegrasyon hatasi yakalandi.");

            var tempData = _tempDataFactory.GetTempData(context.HttpContext);
            tempData["Hata"] = exception.Message;

            context.Result = new RedirectResult(GetSafeRedirectPath(context.HttpContext.Request.Path.Value));
            context.ExceptionHandled = true;
        }

        private static ApiIntegrationException? UnwrapApiIntegrationException(Exception exception)
        {
            if (exception is ApiIntegrationException apiException)
                return apiException;

            if (exception is AggregateException aggregateException)
                return aggregateException
                    .Flatten()
                    .InnerExceptions
                    .OfType<ApiIntegrationException>()
                    .FirstOrDefault();

            return exception.InnerException == null
                ? null
                : UnwrapApiIntegrationException(exception.InnerException);
        }

        private static string GetSafeRedirectPath(string? currentPath)
        {
            var path = currentPath ?? string.Empty;

            var target = path.StartsWith("/AdminPanel", StringComparison.OrdinalIgnoreCase)
                ? "/AdminPanel"
                : path.StartsWith("/personel-panel", StringComparison.OrdinalIgnoreCase)
                    ? "/personel-panel"
                    : path.StartsWith("/ys-", StringComparison.OrdinalIgnoreCase)
                        ? "/ys-panel"
                        : "/giris";

            return string.Equals(path.TrimEnd('/'), target.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                ? "/giris"
                : target;
        }
    }
}
