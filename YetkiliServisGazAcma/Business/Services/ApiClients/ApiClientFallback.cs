namespace YetkiliServisGazAcma.Business.Services
{
    public class ApiIntegrationException : Exception
    {
        public ApiIntegrationException(string operation, string message, int statusCode = 503)
            : base(message)
        {
            Operation = operation;
            StatusCode = statusCode;
        }

        public string Operation { get; }
        public int StatusCode { get; }
    }

    internal static class ApiClientFallback
    {
        public static void EnsureAllowed(ApiIntegrationOptions options, string operation)
        {
            if (options.AllowDatabaseFallback)
                return;

            throw new ApiIntegrationException(
                operation,
                "Veri servisine şu anda ulaşılamıyor. Lütfen kısa bir süre sonra yeniden deneyin.");
        }
    }
}
