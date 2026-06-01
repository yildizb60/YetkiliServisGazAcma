namespace YetkiliServisGazAcma.Business.Services.Online
{
    public class OnlineServiceOptions
    {
        public bool Enabled { get; set; } = true;
        public string Endpoint { get; set; } = "http://onlinesvc.marmaragaz.com.tr/Test/Online.svc";
        public string Firma { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 20;
    }
}
