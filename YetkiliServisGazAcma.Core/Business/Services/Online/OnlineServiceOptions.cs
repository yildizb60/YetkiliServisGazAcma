namespace YetkiliServisGazAcma.Business.Services.Online
{
    public class OnlineServiceOptions
    {
        public bool Enabled { get; set; }
        public string Endpoint { get; set; } = "";
        public string Firma { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 20;
    }
}
