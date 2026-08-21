using System.Security.Cryptography;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.API.Services
{
    public sealed partial class DemoYkcImzaProvider : IYkcImzaProvider
    {
        public string ProviderAdi => "Demo İmza Simülasyonu";
        public bool KullanilabilirMi => true;
        public bool DemoModuMu => true;

        public Task<YkcImzaGonderSonuc> GonderAsync(
            YkcImzaGonderIstek istek,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (istek.TalepId <= 0 || istek.BelgeBytes.Length == 0)
                return Task.FromResult(YkcImzaGonderSonuc.Basarisiz("DEMO_BELGE_GECERSIZ", "Demo imza için geçerli bir FR265 belgesi gerekir."));

            var gercekHash = Convert.ToHexString(SHA256.HashData(istek.BelgeBytes));
            if (!string.IsNullOrWhiteSpace(istek.BelgeHash)
                && !string.Equals(gercekHash, istek.BelgeHash.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(YkcImzaGonderSonuc.Basarisiz("DEMO_HASH_UYUSMAZLIGI", "FR265 belge özeti doğrulanamadı."));
            }

            var belgeNo = $"DEMO-YKC-{istek.TalepId}-V{Math.Max(istek.BelgeVersiyonu, 1)}-{gercekHash[..12]}";
            return Task.FromResult(new YkcImzaGonderSonuc
            {
                Basarili = true,
                ProviderDocumentId = belgeNo
            });
        }

        public Task<YkcImzaDurumSonuc> DurumSorgulaAsync(
            string providerDocumentId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var temizBelgeNo = providerDocumentId?.Trim() ?? string.Empty;
            var eslesme = DemoBelgeNoRegex().Match(temizBelgeNo);
            if (!eslesme.Success
                || !int.TryParse(eslesme.Groups["talepId"].Value, out var talepId)
                || !int.TryParse(eslesme.Groups["versiyon"].Value, out var versiyon))
            {
                return Task.FromResult(YkcImzaDurumSonuc.Basarisiz("DEMO_BELGE_BULUNAMADI", "Demo imza belge numarası geçersiz."));
            }

            var tamamlanmaTarihi = DateTime.Now;
            var pdf = DemoSonucBelgesiOlustur(
                talepId,
                versiyon,
                temizBelgeNo,
                eslesme.Groups["hash"].Value,
                tamamlanmaTarihi);

            return Task.FromResult(new YkcImzaDurumSonuc
            {
                Basarili = true,
                Durum = YkcImzaDurumDegerleri.Tamamlandi,
                Imzacilar = Enumerable.Range(1, 3)
                    .Select(siraNo => new YkcImzaProviderImzaciDurumu
                    {
                        SiraNo = siraNo,
                        Durum = YkcImzaciDurumDegerleri.Imzaladi,
                        ImzaTarihi = tamamlanmaTarihi
                    })
                    .ToList(),
                NihaiBelgeBytes = pdf,
                NihaiBelgeAdi = $"FR265_Demo_Imza_Sonucu_{talepId}.pdf",
                NihaiBelgeIcerikTipi = "application/pdf"
            });
        }

        private static byte[] DemoSonucBelgesiOlustur(
            int talepId,
            int versiyon,
            string belgeNo,
            string hashOzeti,
            DateTime tamamlanmaTarihi)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(38);
                    page.DefaultTextStyle(style => style.FontSize(10).FontColor(Colors.Grey.Darken3));

                    page.Header().Column(column =>
                    {
                        column.Item().Text("FR265 DİJİTAL İMZA SÜREÇ SİMÜLASYONU")
                            .FontSize(17)
                            .Bold()
                            .FontColor(Colors.Blue.Darken3);
                        column.Item().PaddingTop(4).Text($"Cihaz değişim talebi #{talepId} · Belge sürümü {versiyon}")
                            .FontSize(11)
                            .SemiBold();
                    });

                    page.Content().PaddingVertical(24).Column(column =>
                    {
                        column.Spacing(16);
                        column.Item()
                            .Border(1)
                            .BorderColor(Colors.Orange.Darken1)
                            .Background(Colors.Orange.Lighten5)
                            .Padding(14)
                            .Column(uyari =>
                            {
                                uyari.Item().Text("DEMO / TEST BELGESİ").Bold().FontColor(Colors.Orange.Darken3);
                                uyari.Item().PaddingTop(4).Text("Bu belge gerçek bir elektronik imza içermez ve hukuki imzalı belge yerine kullanılamaz. Yalnızca dış imza entegrasyonunun uygulama içindeki işleyişini göstermek için üretilmiştir.");
                            });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(150);
                                columns.RelativeColumn();
                            });

                            BilgiSatiri(table, "Demo belge numarası", belgeNo);
                            BilgiSatiri(table, "Talep numarası", talepId.ToString());
                            BilgiSatiri(table, "Belge sürümü", versiyon.ToString());
                            BilgiSatiri(table, "Kaynak belge hash özeti", hashOzeti);
                            BilgiSatiri(table, "Simülasyon tarihi", tamamlanmaTarihi.ToString("dd.MM.yyyy HH:mm:ss"));
                        });

                        column.Item().Text("Simüle Edilen İmza Adımları").FontSize(12).Bold();
                        foreach (var (sira, rol) in new[]
                        {
                            (1, "Sertifikalı Firma Yetkilisi"),
                            (2, "Dağıtım Şirketi Yetkilisi"),
                            (3, "Abone / Kullanıcı")
                        })
                        {
                            column.Item()
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .PaddingVertical(9)
                                .Row(row =>
                                {
                                    row.ConstantItem(26).Text($"{sira}.").Bold();
                                    row.RelativeItem().Text(rol).SemiBold();
                                    row.ConstantItem(150).AlignRight().Text("Demo olarak tamamlandı").FontColor(Colors.Green.Darken2);
                                });
                        }
                    });

                    page.Footer().AlignCenter().Text("YKC geliştirme ortamı · Gerçek imza sağlayıcısı bağlandığında bu adaptör kullanılmayacaktır.")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                });
            }).GeneratePdf();
        }

        private static void BilgiSatiri(TableDescriptor table, string etiket, string deger)
        {
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(7).Text(etiket).SemiBold();
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(7).Text(deger);
        }

        [GeneratedRegex("^DEMO-YKC-(?<talepId>\\d+)-V(?<versiyon>\\d+)-(?<hash>[A-F0-9]{12})$", RegexOptions.CultureInvariant)]
        private static partial Regex DemoBelgeNoRegex();
    }
}
