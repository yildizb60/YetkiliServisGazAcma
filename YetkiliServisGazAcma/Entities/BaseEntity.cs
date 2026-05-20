using System;

namespace YetkiliServisGazAcma.Entities
{
    public abstract class BaseEntity
    {
        public int Id { get; set; }

        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
        public string? OlusturanKullanici { get; set; }

        public DateTime? GuncellemeTarihi { get; set; }
        public string? GuncelleyenKullanici { get; set; }

        public bool SilindiMi { get; set; } = false;
        public DateTime? SilinmeTarihi { get; set; }
        public string? SilenKullanici { get; set; }
    }
}