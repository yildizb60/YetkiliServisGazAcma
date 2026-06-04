using System.Text;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public static class DevreyeAlmaExcelService
    {
        public static byte[] Olustur(IEnumerable<Ys_DevreyeAlma> islemler)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tesisat No;Yetkili Servis;Firma Kodu;Müşteri;Telefon;TC;Adres;Cihaz Tipi;Marka;Model;Seri No;Kapasite;Teknisyen;Teknisyen Yetki Belgesi No;Durum;Tarih;Notlar");

            foreach (var i in islemler)
            {
                sb.AppendLine(string.Join(";", new[]
                {
                    Csv(i.TesistatNo),
                    Csv(i.Firma?.FirmaAdi),
                    Csv(i.Firma?.Sirket?.SirketAdi),
                    Csv(i.MusteriAdi),
                    Csv(i.MusteriTelefon),
                    Csv(i.MusteriTcNo),
                    Csv(i.Adres),
                    Csv(i.CihazTipi),
                    Csv(i.Marka?.MarkaAdi ?? i.CihazMarka),
                    Csv(i.CihazModeli),
                    Csv(i.SeriNo),
                    Csv(i.CihazKapasite),
                    Csv(i.TeknisyenAdi),
                    Csv(i.TeknisyenYetkiBelgesiNo),
                    Csv(DurumText(i.Durum)),
                    Csv(i.DevreyeAlmaTarihi.ToString("dd.MM.yyyy HH:mm")),
                    Csv(i.Notlar)
                }));
            }

            return Windows1254Bytes("sep=;\r\n" + sb);
        }

        private static string DurumText(int durum)
        {
            return durum == 1 ? "Tamamlandı" : durum == 2 ? "İptal" : "Bekliyor";
        }

        private static string Csv(string? value)
        {
            var temiz = value ?? string.Empty;
            temiz = temiz.Replace("\"", "\"\"");

            if (temiz.Contains(';') || temiz.Contains('"') || temiz.Contains('\n') || temiz.Contains('\r'))
                return $"\"{temiz}\"";

            return temiz;
        }

        private static byte[] Windows1254Bytes(string text)
        {
            var bytes = new List<byte>(text.Length);

            foreach (var ch in text)
            {
                if (ch <= 0x7F)
                {
                    bytes.Add((byte)ch);
                    continue;
                }

                bytes.Add(ch switch
                {
                    'Ç' => 0xC7,
                    'ç' => 0xE7,
                    'Ğ' => 0xD0,
                    'ğ' => 0xF0,
                    'İ' => 0xDD,
                    'ı' => 0xFD,
                    'Ö' => 0xD6,
                    'ö' => 0xF6,
                    'Ş' => 0xDE,
                    'ş' => 0xFE,
                    'Ü' => 0xDC,
                    'ü' => 0xFC,
                    '’' => 0x92,
                    '“' => 0x93,
                    '”' => 0x94,
                    '–' => 0x96,
                    '—' => 0x97,
                    '€' => 0x80,
                    _ => (byte)'?'
                });
            }

            return bytes.ToArray();
        }
    }
}

