using System.Text;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public static class DevreyeAlmaExcelService
    {
        public static byte[] Olustur(IEnumerable<Ys_DevreyeAlma> islemler)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tesisat No;Yetkili Servis;Firma Kodu;M\u00fc\u015fteri;Telefon;TC;Adres;Cihaz Tipi;Marka;Model;Seri No;Kapasite;Teknisyen;Teknisyen Yetki Belgesi No;Durum;Tarih;Notlar");

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
            return durum == 1 ? "Tamamland\u0131" : durum == 2 ? "\u0130ptal" : "Bekliyor";
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
                    '\u00c7' => 0xC7,
                    '\u00e7' => 0xE7,
                    '\u011e' => 0xD0,
                    '\u011f' => 0xF0,
                    '\u0130' => 0xDD,
                    '\u0131' => 0xFD,
                    '\u00d6' => 0xD6,
                    '\u00f6' => 0xF6,
                    '\u015e' => 0xDE,
                    '\u015f' => 0xFE,
                    '\u00dc' => 0xDC,
                    '\u00fc' => 0xFC,
                    '\u2019' => 0x92,
                    '\u201c' => 0x93,
                    '\u201d' => 0x94,
                    '\u2013' => 0x96,
                    '\u2014' => 0x97,
                    '\u20ac' => 0x80,
                    _ => (byte)'?'
                });
            }

            return bytes.ToArray();
        }
    }
}
