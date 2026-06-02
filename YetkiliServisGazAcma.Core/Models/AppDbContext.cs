using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Models
{
    public class AppDbContext : IdentityDbContext<AppKullanici>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Dag_Sirket> Dag_Sirketler { get; set; }
        public DbSet<Ys_Firma> Ys_Firmalar { get; set; }
        public DbSet<Ys_Marka> Ys_Markalar { get; set; }
        public DbSet<Ys_FirmaMarka> Ys_FirmaMarkalar { get; set; }
        public DbSet<UrunKategori> UrunKategoriler { get; set; }
        public DbSet<Ys_FirmaKategori> Ys_FirmaKategoriler { get; set; }
        public DbSet<Ys_Sertifika> Ys_Sertifikalar { get; set; }
        public DbSet<Ys_DevreyeAlma> Ys_DevreyeAlmalar { get; set; }
        public DbSet<Dag_PersonelYetki> Dag_PersonelYetkiler { get; set; }
        public DbSet<Ys_Sube> Ys_Subeler { get; set; }
        public DbSet<SmsDogrulamaKodu> SmsDogrulamaKodlari { get; set; }
        public DbSet<SmsGonderimLog> SmsGonderimLoglari { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema("dbo");

            modelBuilder.Entity<AppKullanici>().ToTable("Ys_AspNetUsers");
            modelBuilder.Entity<IdentityRole>().ToTable("Ys_AspNetRoles");
            modelBuilder.Entity<IdentityUserRole<string>>().ToTable("Ys_AspNetUserRoles");
            modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("Ys_AspNetUserClaims");
            modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("Ys_AspNetUserLogins");
            modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("Ys_AspNetRoleClaims");
            modelBuilder.Entity<IdentityUserToken<string>>().ToTable("Ys_AspNetUserTokens");

            modelBuilder.Entity<Dag_Sirket>().ToTable("Ys_Dag_Sirketler");
            modelBuilder.Entity<Dag_PersonelYetki>().ToTable("Ys_Dag_PersonelYetkiler");
            modelBuilder.Entity<UrunKategori>().ToTable("Ys_UrunKategoriler");
            modelBuilder.Entity<Ys_Firma>().ToTable("Ys_Firmalar");
            modelBuilder.Entity<Ys_Marka>().ToTable("Ys_Markalar");
            modelBuilder.Entity<Ys_FirmaMarka>().ToTable("Ys_FirmaMarkalar");
            modelBuilder.Entity<Ys_FirmaKategori>().ToTable("Ys_FirmaKategoriler");
            modelBuilder.Entity<Ys_Sertifika>().ToTable("Ys_Sertifikalar");
            modelBuilder.Entity<Ys_DevreyeAlma>().ToTable("Ys_DevreyeAlmalar");
            modelBuilder.Entity<Ys_Sube>().ToTable("Ys_Subeler");
            modelBuilder.Entity<SmsDogrulamaKodu>().ToTable("Ys_SmsDogrulamaKodlari");
            modelBuilder.Entity<SmsGonderimLog>().ToTable("Ys_SmsGonderimLoglari");

            modelBuilder.Entity<AppKullanici>()
                .HasIndex(x => new { x.KullaniciTipi, x.AktifMi, x.FirmaId });

            modelBuilder.Entity<AppKullanici>()
                .HasIndex(x => new { x.KullaniciTipi, x.AktifMi, x.SirketId });

            modelBuilder.Entity<Ys_Firma>()
                .HasIndex(x => new { x.SirketId, x.SilindiMi, x.AktifMi });

            modelBuilder.Entity<Ys_Firma>()
                .HasIndex(x => new { x.FaaliyetIli, x.SilindiMi });

            modelBuilder.Entity<Ys_Sube>()
                .HasIndex(x => new { x.FirmaId, x.SilindiMi, x.AktifMi });

            modelBuilder.Entity<Ys_Sertifika>()
                .HasIndex(x => new { x.FirmaId, x.Durum, x.SilindiMi });

            modelBuilder.Entity<Ys_Sertifika>()
                .HasIndex(x => new { x.SertifikaBitisTarihi, x.SilindiMi });

            modelBuilder.Entity<Ys_FirmaMarka>()
                .HasIndex(x => new { x.FirmaId, x.MarkaId, x.SilindiMi });

            modelBuilder.Entity<Ys_FirmaKategori>()
                .HasIndex(x => new { x.FirmaId, x.KategoriId, x.SilindiMi });

            modelBuilder.Entity<Ys_DevreyeAlma>()
                .HasIndex(x => new { x.FirmaId, x.DevreyeAlmaTarihi, x.SilindiMi });

            modelBuilder.Entity<Ys_DevreyeAlma>()
                .HasIndex(x => new { x.MarkaId, x.Durum, x.SilindiMi });

            modelBuilder.Entity<SmsDogrulamaKodu>()
                .HasIndex(x => new { x.KullaniciId, x.Amac, x.KullanildiMi, x.SilindiMi, x.GecerlilikTarihi });
        }
    }
}
