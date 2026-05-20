using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public static class DevreyeAlmaPdfService
    {
        public static byte[] Olustur(Ys_DevreyeAlma i)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Element(Header);
                    page.Content().Element(c => Icerik(c, i));
                    page.Footer().Element(Footer);
                });
            }).GeneratePdf();
        }

        static void Header(IContainer c)
        {
            c.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().Text("CİHAZ DEVREYE ALMA BELGESİ")
                            .FontSize(16).Bold().FontColor("#1B9FD4");
                        inner.Item().Text("Doğalgaz Yetkili Servis Yönetim Sistemi")
                            .FontSize(10).FontColor("#888888");
                    });
                    row.ConstantItem(120).AlignRight().Column(inner =>
                    {
                        inner.Item().Text(DateTime.Now.ToString("dd.MM.yyyy"))
                            .FontSize(10).FontColor("#888888");
                    });
                });
                col.Item().PaddingTop(6).BorderBottom(2).BorderColor("#4CAF7D").Text("");
                col.Item().Height(10);
            });
        }

        static void Icerik(IContainer c, Ys_DevreyeAlma i)
        {
            c.Column(col =>
            {
                col.Spacing(10);

                col.Item().Element(x => Bolum(x, "TESİSAT BİLGİLERİ", "#1B9FD4"));
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(); cd.RelativeColumn();
                        cd.RelativeColumn(); cd.RelativeColumn();
                    });
                    Satir(t, "Tesisat No", i.TesistatNo);
                    Satir(t, "Abone No", i.AboneNo);
                    Satir(t, "Uygunluk Belge No", i.UygunlukBelgeNo);
                    Satir(t, "Uygunluk Tarihi", i.UygunlukTarihi?.ToString("dd.MM.yyyy"));
                });

                col.Item().Element(x => Bolum(x, "MÜŞTERİ BİLGİLERİ", "#1B9FD4"));
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(); cd.RelativeColumn();
                        cd.RelativeColumn(); cd.RelativeColumn();
                    });
                    Satir(t, "Müşteri Adı", i.MusteriAdi);
                    Satir(t, "TC Kimlik No", i.MusteriTcNo);
                    Satir(t, "Telefon", i.MusteriTelefon);
                    SatirGenis(t, "Adres", i.Adres);
                });

                col.Item().Element(x => Bolum(x, "CİHAZ BİLGİLERİ", "#1B9FD4"));
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(); cd.RelativeColumn();
                        cd.RelativeColumn(); cd.RelativeColumn();
                    });
                    Satir(t, "Cihaz Tipi", i.CihazTipi);
                    Satir(t, "Marka", i.CihazMarka);
                    Satir(t, "Model", i.CihazModeli);
                    Satir(t, "Seri No", i.SeriNo);
                    Satir(t, "Kapasite", i.CihazKapasite);
                    Satir(t, "Devreye Alma Tarihi", i.DevreyeAlmaTarihi.ToString("dd.MM.yyyy"));
                });

                col.Item().Element(x => Bolum(x, "TEKNİSYEN BİLGİLERİ", "#1B9FD4"));
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(); cd.RelativeColumn();
                        cd.RelativeColumn(); cd.RelativeColumn();
                    });
                    Satir(t, "Teknisyen Adı", i.TeknisyenAdi);
                    Satir(t, "Yetki Belgesi No", i.TeknisyenSertifikaNo);
                    Satir(t, "Yetkili Servis", i.Firma?.FirmaAdi);
                    Satir(t, "Marka Yetkisi", i.Marka?.MarkaAdi);
                });

                if (!string.IsNullOrEmpty(i.Notlar))
                {
                    col.Item().Element(x => Bolum(x, "NOTLAR", "#888888"));
                    col.Item().Padding(8).Background("#f8f8f8")
                        .Text(i.Notlar ?? "").FontSize(10);
                }

                col.Item().Height(30);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().BorderBottom(1).BorderColor("#ccc").Height(40).Text("");
                        inner.Item().PaddingTop(4).Text("Teknisyen İmzası").FontSize(9).FontColor("#888");
                    });
                    row.ConstantItem(30);
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().BorderBottom(1).BorderColor("#ccc").Height(40).Text("");
                        inner.Item().PaddingTop(4).Text("Müşteri İmzası").FontSize(9).FontColor("#888");
                    });
                    row.ConstantItem(30);
                    row.RelativeItem().Column(inner =>
                    {
                        inner.Item().BorderBottom(1).BorderColor("#ccc").Height(40).Text("");
                        inner.Item().PaddingTop(4).Text("Yetkili Servis Kaşe/İmza").FontSize(9).FontColor("#888");
                    });
                });
            });
        }

        static void Bolum(IContainer c, string baslik, string renk)
        {
            c.Background(renk).Padding(6).PaddingLeft(10)
                .Text(baslik).FontSize(10).Bold().FontColor("#ffffff");
        }

        static void Satir(TableDescriptor t, string label, string? deger)
        {
            t.Cell().Background("#f4f4f4").Padding(5).PaddingLeft(8)
                .Text(label).FontSize(9).FontColor("#555").Bold();
            t.Cell().Padding(5).PaddingLeft(8)
                .Text(deger ?? "—").FontSize(10);
        }

        // Adres gibi geniş alanlar için 4 sütunun tamamını kullanır
        static void SatirGenis(TableDescriptor t, string label, string? deger)
        {
            t.Cell().Background("#f4f4f4").Padding(5).PaddingLeft(8)
                .Text(label).FontSize(9).FontColor("#555").Bold();
            t.Cell().ColumnSpan(3).Padding(5).PaddingLeft(8)
                .Text(deger ?? "—").FontSize(10);
        }

        static void Footer(IContainer c)
        {
            c.Row(row =>
            {
                row.RelativeItem().Text(x =>
                {
                    x.Span("Bu belge yetkili servis yönetim sistemi tarafından otomatik oluşturulmuştur.")
                        .FontSize(8).FontColor("#aaa");
                });
                row.ConstantItem(100).AlignRight().Text(x =>
                {
                    x.Span("Sayfa ").FontSize(8).FontColor("#aaa");
                    x.CurrentPageNumber().FontSize(8).FontColor("#aaa");
                    x.Span(" / ").FontSize(8).FontColor("#aaa");
                    x.TotalPages().FontSize(8).FontColor("#aaa");
                });
            });
        }
    }
}
