using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace EcoBuildss.Core
{
    /// <summary>Excel çalışma sayfası: satırlar (metin, tam sayı ya da ondalık), kalın satırlar ve sütun genişlikleri.</summary>
    public sealed class XlsxSayfa
    {
        public string Ad = "Sayfa1";
        public List<object?[]> Satirlar = new();
        public HashSet<int> KalinSatirlar = new();     // 0 tabanlı satır numaraları
        public double[] Genislik = Array.Empty<double>();
        public XlsxSayfa Satir(params object?[] h) { Satirlar.Add(h); return this; }
        public XlsxSayfa Kalin(params object?[] h) { KalinSatirlar.Add(Satirlar.Count); Satirlar.Add(h); return this; }
        public XlsxSayfa Bos() { Satirlar.Add(Array.Empty<object?>()); return this; }
    }

    /// <summary>
    /// Ek paket gerektirmeden .xlsx (Office Open XML) dosyası yazar: her hücre değeri olduğu gibi saklanır,
    /// sayılar Excel'de sayı olarak (toplanabilir, biçimlenebilir) açılır.
    /// </summary>
    public static class XlsxYazici
    {
        const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string NsR = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static void Yaz(string yol, IReadOnlyList<XlsxSayfa> sayfalar)
        {
            if (sayfalar.Count == 0) throw new ArgumentException("En az bir sayfa gerekir.");
            if (File.Exists(yol)) File.Delete(yol);
            using var zip = ZipFile.Open(yol, ZipArchiveMode.Create);
            void Ekle(string ad, string icerik)
            {
                var e = zip.CreateEntry(ad, CompressionLevel.Optimal);
                using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
                w.Write(icerik);
            }
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                      "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                      "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                      "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (int i = 1; i <= sayfalar.Count; i++)
                sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            sb.Append("</Types>");
            Ekle("[Content_Types].xml", sb.ToString());
            Ekle("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");

            sb.Clear().Append($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"{Ns}\" xmlns:r=\"{NsR}\"><sheets>");
            var adlar = new HashSet<string>();
            for (int i = 0; i < sayfalar.Count; i++)
            {
                string ad = SayfaAdi(sayfalar[i].Ad, i, adlar);
                sb.Append($"<sheet name=\"{Kacis(ad)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            }
            sb.Append("</sheets></workbook>");
            Ekle("xl/workbook.xml", sb.ToString());

            sb.Clear().Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 1; i <= sayfalar.Count; i++)
                sb.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
            sb.Append($"<Relationship Id=\"rId{sayfalar.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            Ekle("xl/_rels/workbook.xml.rels", sb.ToString());

            // Biçimler: 0 normal, 1 kalın, 2 ondalık (#,##0.00), 3 tam sayı (#,##0), 4 kalın ondalık, 5 kalın tam sayı
            Ekle("xl/styles.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"{Ns}\">" +
                "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"#,##0.00\"/></numFmts>" +
                "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"6\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
                "<xf numFmtId=\"3\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
                "<xf numFmtId=\"164\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\"/>" +
                "<xf numFmtId=\"3\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\"/></cellXfs>" +
                "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");

            for (int i = 0; i < sayfalar.Count; i++) Ekle($"xl/worksheets/sheet{i + 1}.xml", SayfaXml(sayfalar[i]));
        }

        static string SayfaXml(XlsxSayfa s)
        {
            var sb = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"{Ns}\">");
            if (s.Genislik.Length > 0)
            {
                sb.Append("<cols>");
                for (int c = 0; c < s.Genislik.Length; c++)
                    sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{s.Genislik[c].ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                sb.Append("</cols>");
            }
            sb.Append("<sheetData>");
            for (int r = 0; r < s.Satirlar.Count; r++)
            {
                var satir = s.Satirlar[r];
                bool kalin = s.KalinSatirlar.Contains(r);
                sb.Append($"<row r=\"{r + 1}\">");
                for (int c = 0; c < satir.Length; c++)
                {
                    var v = satir[c];
                    if (v == null) continue;
                    string hucre = SutunAdi(c) + (r + 1);
                    switch (v)
                    {
                        case int or long:
                            sb.Append($"<c r=\"{hucre}\" s=\"{(kalin ? 5 : 3)}\"><v>{Convert.ToInt64(v).ToString(CultureInfo.InvariantCulture)}</v></c>"); break;
                        case double or float or decimal:
                            double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                            if (double.IsNaN(d) || double.IsInfinity(d)) throw new ArgumentException($"{s.Ad} {hucre}: sayı değil.");
                            sb.Append($"<c r=\"{hucre}\" s=\"{(kalin ? 4 : 2)}\"><v>{d.ToString("R", CultureInfo.InvariantCulture)}</v></c>"); break;
                        default:
                            string t = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
                            if (t.Length == 0) continue;
                            sb.Append($"<c r=\"{hucre}\" t=\"inlineStr\"{(kalin ? " s=\"1\"" : "")}><is><t xml:space=\"preserve\">{Kacis(t)}</t></is></c>"); break;
                    }
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        public static string SutunAdi(int c)
        {
            string s = "";
            for (c++; c > 0; c = (c - 1) / 26) s = (char)('A' + (c - 1) % 26) + s;
            return s;
        }

        static string SayfaAdi(string ad, int i, HashSet<string> kullanilan)
        {
            var temiz = new string(ad.Where(ch => "[]:*?/\\".IndexOf(ch) < 0).ToArray()).Trim();
            if (temiz.Length == 0) temiz = $"Sayfa{i + 1}";
            if (temiz.Length > 31) temiz = temiz.Substring(0, 31);
            string aday = temiz; int n = 2;
            while (!kullanilan.Add(aday.ToLowerInvariant())) aday = (temiz.Length > 28 ? temiz.Substring(0, 28) : temiz) + " " + n++;
            return aday;
        }

        // XML'de geçersiz denetim karakterleri atılır, özel karakterler kaçışlanır
        static string Kacis(string s) =>
            SecurityElement.Escape(new string(s.Where(ch => ch == '\t' || ch == '\n' || ch == '\r' || ch >= 0x20).ToArray())) ?? "";
    }
}
