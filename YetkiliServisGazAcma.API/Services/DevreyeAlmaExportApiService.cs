using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public class DevreyeAlmaExportApiService
    {
        private readonly AppDbContext _context;

        public DevreyeAlmaExportApiService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DevreyeAlmaExportDosya?> AdminPdfAsync(int id, int? sirketId)
        {
            var kayit = await AdminDevreyeAlmaQuery(sirketId).FirstOrDefaultAsync(x => x.Id == id);
            return kayit == null ? null : PdfDosyasi(kayit);
        }

        public async Task<DevreyeAlmaExportDosya?> AdminExcelAsync(int id, int? sirketId)
        {
            var kayit = await AdminDevreyeAlmaQuery(sirketId).FirstOrDefaultAsync(x => x.Id == id);
            return kayit == null ? null : ExcelDosyasi(kayit);
        }

        public async Task<DevreyeAlmaExportDosya?> YetkiliServisPdfAsync(int id, int firmaId)
        {
            var kayit = await YetkiliServisDevreyeAlmaQuery(firmaId).FirstOrDefaultAsync(x => x.Id == id);
            return kayit == null ? null : PdfDosyasi(kayit);
        }

        public async Task<DevreyeAlmaExportDosya?> YetkiliServisExcelAsync(int id, int firmaId)
        {
            var kayit = await YetkiliServisDevreyeAlmaQuery(firmaId).FirstOrDefaultAsync(x => x.Id == id);
            return kayit == null ? null : ExcelDosyasi(kayit);
        }

        public async Task<DevreyeAlmaExportDosya> AdminRaporPdfAsync(int? sirketId, DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var (islemler, basTarih, bitTarih) = await AdminRaporIslemleriAsync(sirketId, bas, bit, ids, ids?.Count > 0 ? null : 20);
            return new DevreyeAlmaExportDosya
            {
                Bytes = DevreyeAlmaRaporPdfService.AdminRaporuOlustur(islemler, basTarih, bitTarih),
                ContentType = "application/pdf",
                DosyaAdi = RaporDosyaAdi("raporlar", basTarih, bitTarih, "pdf")
            };
        }

        public async Task<DevreyeAlmaExportDosya> AdminRaporExcelAsync(int? sirketId, DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var (islemler, basTarih, bitTarih) = await AdminRaporIslemleriAsync(sirketId, bas, bit, ids, take: null);
            return new DevreyeAlmaExportDosya
            {
                Bytes = DevreyeAlmaExcelService.Olustur(islemler),
                ContentType = "text/csv; charset=windows-1254",
                DosyaAdi = RaporDosyaAdi("raporlar", basTarih, bitTarih, "csv")
            };
        }

        public async Task<DevreyeAlmaExportDosya> YetkiliServisRaporPdfAsync(int firmaId, DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var (islemler, basTarih, bitTarih) = await YetkiliServisRaporIslemleriAsync(firmaId, bas, bit, ids, ids?.Count > 0 ? null : 10);
            return new DevreyeAlmaExportDosya
            {
                Bytes = DevreyeAlmaRaporPdfService.YetkiliServisRaporuOlustur(islemler, basTarih, bitTarih),
                ContentType = "application/pdf",
                DosyaAdi = RaporDosyaAdi("raporlar", basTarih, bitTarih, "pdf")
            };
        }

        public async Task<DevreyeAlmaExportDosya> YetkiliServisRaporExcelAsync(int firmaId, DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var (islemler, basTarih, bitTarih) = await YetkiliServisRaporIslemleriAsync(firmaId, bas, bit, ids, take: null);
            return new DevreyeAlmaExportDosya
            {
                Bytes = DevreyeAlmaExcelService.Olustur(islemler),
                ContentType = "text/csv; charset=windows-1254",
                DosyaAdi = RaporDosyaAdi("raporlar", basTarih, bitTarih, "csv")
            };
        }

        private IQueryable<Ys_DevreyeAlma> AdminDevreyeAlmaQuery(int? sirketId)
        {
            return TemelQuery()
                .Where(x => x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId.Value));
        }

        private IQueryable<Ys_DevreyeAlma> YetkiliServisDevreyeAlmaQuery(int firmaId)
        {
            return TemelQuery().Where(x => x.FirmaId == firmaId);
        }

        private async Task<(List<Ys_DevreyeAlma> Islemler, DateTime BasTarih, DateTime BitTarih)> AdminRaporIslemleriAsync(
            int? sirketId,
            DateTime? bas,
            DateTime? bit,
            List<int>? ids,
            int? take)
        {
            var basTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30);
            var bitTarih = bit?.Date ?? DateTime.Now.Date;
            var query = AdminDevreyeAlmaQuery(sirketId);

            if (ids?.Count > 0)
            {
                var idListesi = ids.Distinct().ToList();
                var secilenler = await query
                    .Where(x => idListesi.Contains(x.Id))
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .ToListAsync();

                if (secilenler.Count > 0)
                {
                    basTarih = secilenler.Min(x => x.OlusturmaTarihi).Date;
                    bitTarih = secilenler.Max(x => x.OlusturmaTarihi).Date;
                }

                return (secilenler, basTarih, bitTarih);
            }

            var bitSonrasi = bitTarih.AddDays(1);
            query = query.Where(x => x.OlusturmaTarihi >= basTarih && x.OlusturmaTarihi < bitSonrasi)
                .OrderByDescending(x => x.OlusturmaTarihi);

            var islemler = take.HasValue
                ? await query.Take(take.Value).ToListAsync()
                : await query.ToListAsync();

            return (islemler, basTarih, bitTarih);
        }

        private async Task<(List<Ys_DevreyeAlma> Islemler, DateTime BasTarih, DateTime BitTarih)> YetkiliServisRaporIslemleriAsync(
            int firmaId,
            DateTime? bas,
            DateTime? bit,
            List<int>? ids,
            int? take)
        {
            var query = YetkiliServisDevreyeAlmaQuery(firmaId);
            DateTime basTarih;
            DateTime bitTarih;

            if (ids?.Count > 0)
            {
                var idListesi = ids.Distinct().ToList();
                var secilenler = await query
                    .Where(x => idListesi.Contains(x.Id))
                    .OrderByDescending(x => x.DevreyeAlmaTarihi)
                    .ToListAsync();

                basTarih = secilenler.Count > 0 ? secilenler.Min(x => x.DevreyeAlmaTarihi).Date : DateTime.Now.Date;
                bitTarih = secilenler.Count > 0 ? secilenler.Max(x => x.DevreyeAlmaTarihi).Date : DateTime.Now.Date;
                return (secilenler, basTarih, bitTarih);
            }

            if (!bas.HasValue && !bit.HasValue)
            {
                var mevcutAralik = await query
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Bas = g.Min(x => x.DevreyeAlmaTarihi),
                        Bit = g.Max(x => x.DevreyeAlmaTarihi)
                    })
                    .FirstOrDefaultAsync();

                if (mevcutAralik != null)
                {
                    basTarih = mevcutAralik.Bas.Date;
                    bitTarih = mevcutAralik.Bit.Date;
                }
                else
                {
                    bitTarih = DateTime.Now.Date;
                    basTarih = bitTarih.AddDays(-30);
                }
            }
            else
            {
                bitTarih = bit?.Date ?? DateTime.Now.Date;
                basTarih = bas?.Date ?? bitTarih.AddDays(-30);
            }

            var bitSonrasi = bitTarih.AddDays(1);
            query = query.Where(x => x.DevreyeAlmaTarihi >= basTarih && x.DevreyeAlmaTarihi < bitSonrasi)
                .OrderByDescending(x => x.DevreyeAlmaTarihi);

            var islemler = take.HasValue
                ? await query.Take(take.Value).ToListAsync()
                : await query.ToListAsync();

            return (islemler, basTarih, bitTarih);
        }

        private IQueryable<Ys_DevreyeAlma> TemelQuery()
        {
            return _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                .Include(x => x.Marka)
                .Where(x => !x.SilindiMi);
        }

        private static DevreyeAlmaExportDosya PdfDosyasi(Ys_DevreyeAlma kayit)
        {
            return new DevreyeAlmaExportDosya
            {
                Bytes = DevreyeAlmaPdfService.Olustur(kayit),
                ContentType = "application/pdf",
                DosyaAdi = $"DevreyeAlma_{kayit.TesistatNo ?? kayit.Id.ToString()}_{kayit.Id}.pdf"
            };
        }

        private static DevreyeAlmaExportDosya ExcelDosyasi(Ys_DevreyeAlma kayit)
        {
            return new DevreyeAlmaExportDosya
            {
                Bytes = DevreyeAlmaExcelService.Olustur(new[] { kayit }),
                ContentType = "text/csv; charset=windows-1254",
                DosyaAdi = $"DevreyeAlma_{kayit.TesistatNo ?? kayit.Id.ToString()}_{kayit.Id}.csv"
            };
        }

        private static string RaporDosyaAdi(string onEk, DateTime basTarih, DateTime bitTarih, string uzanti)
        {
            return $"{onEk}_{basTarih:yyyyMMdd}_{bitTarih:yyyyMMdd}.{uzanti}";
        }
    }

    public class DevreyeAlmaExportDosya
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
        public string DosyaAdi { get; set; } = "devreye-alma";
    }
}
