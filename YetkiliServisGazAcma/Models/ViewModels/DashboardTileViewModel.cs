namespace YetkiliServisGazAcma.Models.ViewModels
{
    public sealed class DashboardTileViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Tone { get; set; } = "blue";
        public int Done { get; set; }
        public int Total { get; set; }
        public bool ShowProgress { get; set; } = true;
        public string MetricValue { get; set; } = string.Empty;
        public string MetricLabel { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Stat1Label { get; set; } = string.Empty;
        public string Stat1Value { get; set; } = string.Empty;
        public string Stat1Class { get; set; } = "df-tile-stat-val";
        public string Stat2Label { get; set; } = string.Empty;
        public string Stat2Value { get; set; } = string.Empty;
        public string Stat2Class { get; set; } = "df-tile-stat-val";
        public string Stat3Label { get; set; } = string.Empty;
        public string Stat3Value { get; set; } = string.Empty;
        public string Stat3Class { get; set; } = "df-tile-stat-val";
        public bool Featured { get; set; }

        public int RingPercentage => Total <= 0
            ? 0
            : Math.Clamp((int)Math.Round(Done * 100.0 / Total), 0, 100);

        public string RingOffset
        {
            get
            {
                const double circumference = 175.9292;
                return (circumference - (circumference * RingPercentage / 100.0))
                    .ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
