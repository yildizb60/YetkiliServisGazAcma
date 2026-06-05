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
    }

    public class DevreyeAlmaExportDosya
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
        public string DosyaAdi { get; set; } = "devreye-alma";
    }
}
