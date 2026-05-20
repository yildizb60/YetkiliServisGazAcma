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
    }
}
