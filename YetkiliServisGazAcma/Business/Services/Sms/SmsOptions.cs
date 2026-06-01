namespace YetkiliServisGazAcma.Business.Services
{
    public class SmsOptions
    {
        public bool Enabled { get; set; }
        public string Provider { get; set; } = "Pending";
        public string Sender { get; set; } = "";
        public int CodeLength { get; set; } = 6;
        public int CodeExpireMinutes { get; set; } = 5;
        public int MaxAttempts { get; set; } = 5;
        public string BaseUrl { get; set; } = "https://smsnviapi.ahlatci.com.tr";
        public string SendPath { get; set; } = "/api/AhlSmsApi/Send_SMS_BULK";
        public string TokenPath { get; set; } = "/api/Token/oauth/get_token";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string GrantType { get; set; } = "password";
        public string Scope { get; set; } = "SMSApiLocal";
        public string BearerToken { get; set; } = "";
        public string DefaultHeader { get; set; } = "CORUMGAZ";
        public string CountryCode { get; set; } = "90";
        public string InfoCompany { get; set; } = "SCADA";
        public string InfoUserName { get; set; } = "ACEX";
        public string InfoAccessIP { get; set; } = "127.0.0.1";
        public int TimeoutSeconds { get; set; } = 20;
        public Dictionary<string, string> CompanyHeaders { get; set; } =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["CORUMGAZ"] = "CORUMGAZ",
                ["KARGAZ"] = "KARGAZ",
                ["SURMELIGAZ"] = "SURMELIGAZ",
                ["MARMARAGAZ_YALOVA"] = "YALOVAGAZ",
                ["MARMARAGAZ_CORLU"] = "CORLUGAZ",
                ["YALOVAGAZ"] = "YALOVAGAZ",
                ["CORLUGAZ"] = "CORLUGAZ"
            };
    }
}
