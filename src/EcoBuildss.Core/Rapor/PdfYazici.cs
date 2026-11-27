using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace EcoBuildss.Core
{
    /// <summary>PDF'e konacak bir sayfa: JPEG olarak çizilmiş sayfa görüntüsü.</summary>
    public sealed class PdfSayfa
    {
        public byte[] Jpeg = Array.Empty<byte>();
        public int Genislik, Yukseklik;   // piksel
    }

    /// <summary>
    /// Dış kütüphanesiz PDF yazıcı. Her sayfa, ekranda/yazıcıda kullanılan çizimin A4 boyutunda JPEG görüntüsüdür (DCTDecode).
    /// Böylece PDF, yazdırılan sayfayla birebir aynı olur ve hiçbir yazıcı sürücüsü gerekmez.
    /// </summary>
    public static class PdfYazici
    {
        public const double A4Gen = 595.28, A4Yuk = 841.89;   // pt (1/72 inç)

        public static void Yaz(string dosya, IReadOnlyList<PdfSayfa> sayfalar, string baslik = "")
        {
            // Önce bellekte üretilir: hata olursa var olan dosya bozulmaz, yarım dosya kalmaz
            using var ms = new MemoryStream();
            Yaz(ms, sayfalar, baslik);
            File.WriteAllBytes(dosya, ms.ToArray());
        }

        public static void Yaz(Stream cikti, IReadOnlyList<PdfSayfa> sayfalar, string baslik = "")
        {
            if (sayfalar.Count == 0) throw new ArgumentException("En az bir sayfa gerekir.");
            foreach (var s in sayfalar)
                if (s.Jpeg.Length < 4 || s.Jpeg[0] != 0xFF || s.Jpeg[1] != 0xD8 || s.Genislik <= 0 || s.Yukseklik <= 0) throw new ArgumentException("Sayfa görüntüsü geçerli bir JPEG değil.");

            var ms = new MemoryStream();
            var ofs = new List<long>();
            void Ascii(string t) { var b = Encoding.ASCII.GetBytes(t); ms.Write(b, 0, b.Length); }
            void Nesne(int no) { while (ofs.Count < no) ofs.Add(0); ofs[no - 1] = ms.Position; Ascii($"{no} 0 obj\n"); }
            string N(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);

            Ascii("%PDF-1.4\n"); ms.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' }, 0, 6);   // ikili dosya işareti
            int n = sayfalar.Count;
            // 1: katalog, 2: sayfa ağacı, 3: bilgi; her sayfa için 3 nesne: sayfa, içerik, görüntü
            int Sayfa(int i) => 4 + 3 * i;
            Nesne(1); Ascii("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            var kids = new StringBuilder();
            for (int i = 0; i < n; i++) kids.Append($"{Sayfa(i)} 0 R ");
            Nesne(2); Ascii($"<< /Type /Pages /Count {n} /Kids [ {kids}] >>\nendobj\n");
            Nesne(3); Ascii($"<< /Title {PdfMetin(baslik)} /Producer (EcoBuildss) /CreationDate (D:{DateTime.Now:yyyyMMddHHmmss}) >>\nendobj\n");
            for (int i = 0; i < n; i++)
            {
                var s = sayfalar[i];
                // görüntü sayfaya oranı korunarak sığdırılır
                double olcek = Math.Min(A4Gen / s.Genislik, A4Yuk / s.Yukseklik), w = s.Genislik * olcek, h = s.Yukseklik * olcek;
                string icerik = $"q {N(w)} 0 0 {N(h)} {N((A4Gen - w) / 2)} {N(A4Yuk - h)} cm /Im0 Do Q\n";
                Nesne(Sayfa(i));
                Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(A4Gen)} {N(A4Yuk)}] /Resources << /XObject << /Im0 {Sayfa(i) + 2} 0 R >> >> /Contents {Sayfa(i) + 1} 0 R >>\nendobj\n");
                Nesne(Sayfa(i) + 1); Ascii($"<< /Length {icerik.Length} >>\nstream\n{icerik}endstream\nendobj\n");
                Nesne(Sayfa(i) + 2);
                Ascii($"<< /Type /XObject /Subtype /Image /Width {s.Genislik} /Height {s.Yukseklik} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {s.Jpeg.Length} >>\nstream\n");
                ms.Write(s.Jpeg, 0, s.Jpeg.Length);
                Ascii("\nendstream\nendobj\n");
            }
            long xref = ms.Position;
            Ascii($"xref\n0 {ofs.Count + 1}\n0000000000 65535 f \n");
            foreach (var o in ofs) Ascii($"{o:0000000000} 00000 n \n");
            Ascii($"trailer\n<< /Size {ofs.Count + 1} /Root 1 0 R /Info 3 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            ms.Position = 0; ms.CopyTo(cikti);
        }

        /// <summary>PDF metin dizesi: ASCII dışı karakterler UTF-16BE (BOM'lu) onaltılı olarak yazılır.</summary>
        static string PdfMetin(string s)
        {
            bool ascii = true; foreach (var c in s) if (c > 126 || c < 32 || c == '(' || c == ')' || c == '\\') { ascii = false; break; }
            if (ascii) return "(" + s + ")";
            return "<FEFF" + BitConverter.ToString(Encoding.BigEndianUnicode.GetBytes(s)).Replace("-", "") + ">";
        }
    }
}
