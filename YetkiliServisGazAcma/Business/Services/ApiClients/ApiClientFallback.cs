namespace YetkiliServisGazAcma.Business.Services
{
    public class ApiIntegrationException : Exception
    {
        public ApiIntegrationException(string operation, string message)
            : base(message)
        {
            Operation = operation;
        }

        public string Operation { get; }
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
