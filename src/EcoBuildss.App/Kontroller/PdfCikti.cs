using System.Drawing.Imaging;
using EcoBuildss.Core.Depo;

namespace EcoBuildss.UI
{
    /// <summary>Yazıcıya giden sayfa çiziminin aynısını A4 PDF olarak kaydeder (yazıcı sürücüsü gerekmez).</summary>
    public static class PdfCikti
    {
        // A4 = 8,27 × 11,69 inç; yazıcıdaki gibi 1/100 inç birimle, varsayılan 1 inç kenar boşluğu
        static readonly Rectangle Kenar = new(100, 100, 627, 969);
        const float Olcek = 1.5f;   // 150 dpi

        public static PdfSayfa Ciz(Action<Graphics, Rectangle> ciz)
        {
            using var bm = new Bitmap((int)(827 * Olcek), (int)(1169 * Olcek));
            bm.SetResolution(100, 100);   // yazı tipleri yazıcıdaki 1/100 inç birimle aynı boyda
            using (var g = Graphics.FromImage(bm))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.ScaleTransform(Olcek, Olcek);
                ciz(g, Kenar);
            }
            using var ms = new MemoryStream();
            var kodlayici = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using var p = new EncoderParameters(1);
            p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
            bm.Save(ms, kodlayici, p);
            return new PdfSayfa { Jpeg = ms.ToArray(), Genislik = bm.Width, Yukseklik = bm.Height };
        }

        public static void Kaydet(IWin32Window sahip, Durum d, string dosyaAdi, string baslik, Action<Graphics, Rectangle> ciz, string gunlukIslem, string gunlukAyrinti)
        {
            using var f = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = dosyaAdi };
            if (f.ShowDialog(sahip) != DialogResult.OK) return;
            try
            {
                PdfYazici.Yaz(f.FileName, new[] { Ciz(ciz) }, baslik);
                d.Bildir("Kaydedildi: " + f.FileName);
                d.Db?.Gunluk(gunlukIslem, gunlukAyrinti + " · " + Path.GetFileName(f.FileName));
            }
            catch (Exception e) { d.Hata("PDF yazılamadı: " + e.Message, e); Ui.Hata(sahip, "PDF yazılamadı: " + e.Message); }
        }
    }
}
