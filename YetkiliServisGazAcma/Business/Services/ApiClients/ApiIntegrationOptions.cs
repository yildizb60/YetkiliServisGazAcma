namespace YetkiliServisGazAcma.Business.Services
{
    public class ApiIntegrationOptions
    {
        public bool Enabled { get; set; }
        public string BaseUrl { get; set; } = "http://localhost:5057";
        public int TimeoutSeconds { get; set; } = 5;
        public bool AllowDatabaseFallback { get; set; }
    }
}
