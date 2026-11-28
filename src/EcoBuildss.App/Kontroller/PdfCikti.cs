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

        public const int NotSiniri = 600;

        /// <summary>PDF'e eklenecek not: indiren kişi müşteriye kendi açıklamasını yazar. Vazgeçilirse null.</summary>
        public static string? NotSor(IWin32Window sahip)
        {
            using var f = new Form { Text = "PDF", FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                                     StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(460, 270), BackColor = Color.White, Font = Ui.Yazi };
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20, 14, 20, 10) };
            var not = new TextBox { Multiline = true, Width = 420, Height = 140, MaxLength = NotSiniri, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
            var sayac = new Label { AutoSize = true, ForeColor = Ui.Soluk, Font = Ui.Kucuk, Text = $"0 / {NotSiniri}" };
            not.TextChanged += (_, __) => sayac.Text = $"{not.TextLength} / {NotSiniri}";
            p.Controls.Add(Ui.Etiket("Not (isteğe bağlı)")); p.Controls.Add(not); p.Controls.Add(sayac);
            var tamam = Ui.Dugme("Kaydet", (_, __) => f.DialogResult = DialogResult.OK, true);
            p.Controls.Add(tamam);
            f.Controls.Add(p);
            return f.ShowDialog(sahip) == DialogResult.OK ? not.Text.Trim() : null;
        }

        public static void Kaydet(IWin32Window sahip, Durum d, string dosyaAdi, string baslik, Action<Graphics, Rectangle, string> ciz, string gunlukIslem, string gunlukAyrinti)
        {
            if (NotSor(sahip) is not string not) return;
            using var f = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = dosyaAdi };
            if (f.ShowDialog(sahip) != DialogResult.OK) return;
            try
            {
                PdfYazici.Yaz(f.FileName, new[] { Ciz((g, m) => ciz(g, m, not)) }, baslik);
                d.Bildir("Kaydedildi: " + f.FileName);
                d.Db?.Gunluk(gunlukIslem, gunlukAyrinti + " · " + Path.GetFileName(f.FileName) + (not.Length > 0 ? " · notlu" : ""));
            }
            catch (Exception e) { d.Hata("PDF yazılamadı: " + e.Message, e); Ui.Hata(sahip, "PDF yazılamadı: " + e.Message); }
        }
    }
}
