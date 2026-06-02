namespace YetkiliServisGazAcma.Business.Services
{
    public class ApiIntegrationException : Exception
    {
        public ApiIntegrationException(string message)
            : base(message)
        {
        }
    }

    internal static class ApiClientFallback
    {
        public static void EnsureAllowed(ApiIntegrationOptions options, string operation)
        {
            if (options.AllowDatabaseFallback)
                return;

            throw new ApiIntegrationException(
                $"{operation} icin API yaniti alinamadi. Canli ayrik mimaride veritabani fallback kapali.");
        }
    }
}
