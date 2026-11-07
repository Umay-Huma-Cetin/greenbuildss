using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EcoBuildss.UI
{
    /// <summary>Marka öğeleri: logo işareti, pencere simgesi ve üst uygulama çubuğu.</summary>
    public static class Marka
    {
        public const string Ad = "EcoBuildss";
        public static readonly Color Koyu = Color.FromArgb(22, 52, 40);      // üst çubuk
        public static readonly Color Vurgu = Color.FromArgb(31, 111, 74);    // ana yeşil
        public static readonly Color Acik = Color.FromArgb(120, 196, 150);   // logo yaprağı

        /// <summary>Logo: yaprak çatılı bina siluetini verilen kareye çizer.</summary>
        public static void LogoCiz(Graphics g, RectangleF r, Color govde, Color yaprak)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = r.Width, h = r.Height, x = r.X, y = r.Y;
            // bina gövdesi
            using (var b = new SolidBrush(govde))
                g.FillRectangle(b, x + w * 0.18f, y + h * 0.48f, w * 0.64f, h * 0.44f);
            // kapı boşluğu
            using (var kapi = new SolidBrush(yaprak))
                g.FillRectangle(kapi, x + w * 0.44f, y + h * 0.68f, w * 0.12f, h * 0.24f);
            // yaprak biçimli çatı
            using var yol = new GraphicsPath();
            yol.AddBezier(x + w * 0.08f, y + h * 0.50f, x + w * 0.20f, y + h * 0.06f, x + w * 0.80f, y + h * 0.02f, x + w * 0.94f, y + h * 0.16f);
            yol.AddBezier(x + w * 0.94f, y + h * 0.16f, x + w * 0.86f, y + h * 0.40f, x + w * 0.50f, y + h * 0.52f, x + w * 0.08f, y + h * 0.50f);
            yol.CloseFigure();
            using (var b = new SolidBrush(yaprak)) g.FillPath(b, yol);
            // yaprak damarı
            using var damar = new Pen(govde, Math.Max(1f, w * 0.04f));
            g.DrawBezier(damar, x + w * 0.16f, y + h * 0.46f, x + w * 0.40f, y + h * 0.34f, x + w * 0.64f, y + h * 0.22f, x + w * 0.88f, y + h * 0.17f);
        }

        /// <summary>Pencere ve görev çubuğu simgesi, kodla üretilir (ayrı .ico dosyası gerekmez).</summary>
        public static Icon Simge()
        {
            using var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var zemin = new SolidBrush(Vurgu)) g.FillEllipse(zemin, 1, 1, 62, 62);
                LogoCiz(g, new RectangleF(12, 12, 40, 40), Color.White, Acik);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    /// <summary>Uygulamanın üst çubuğu: logo, ürün adı ve sağda ayarlar (dişli) menüsü.</summary>
    public sealed class UstCubuk : Panel
    {
        readonly FlowLayoutPanel sag = new() { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 10, 12, 0) };

        public UstCubuk()
        {
            Dock = DockStyle.Top;
            Height = 52;
            BackColor = Marka.Koyu;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Controls.Add(sag);
        }

        /// <summary>Sağdaki ayarlar (dişli) düğmesi; komutlar tıklayınca açılan menüdedir.</summary>
        public readonly AyarDugmesi Ayarlar = new();
        public readonly ContextMenuStrip Menu = new() { Font = new Font("Segoe UI", 10f), ShowImageMargin = false, Padding = new Padding(4) };

        public UstCubukMenu MenuKur()
        {
            Ayarlar.Click += (_, _) => Menu.Show(Ayarlar, new Point(Ayarlar.Width, Ayarlar.Height + 4), ToolStripDropDownDirection.BelowLeft);
            Menu.Renderer = new MenuCizici();
            sag.Controls.Add(Ayarlar);
            return new UstCubukMenu(Menu);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Marka.LogoCiz(g, new RectangleF(16, 10, 32, 32), Color.White, Marka.Acik);
            using var f = new Font("Segoe UI Semibold", 15f);
            using var b = new SolidBrush(Color.White);
            g.DrawString(Marka.Ad, f, b, 54, 12);
        }
    }

    public sealed class UstCubukMenu
    {
        readonly ContextMenuStrip m;
        public UstCubukMenu(ContextMenuStrip m) { this.m = m; }
        public ToolStripMenuItem Ekle(string metin, string? kisayol = null)
        {
            var o = new ToolStripMenuItem(metin) { ShortcutKeyDisplayString = kisayol ?? "", Padding = new Padding(6, 6, 6, 6) };
            m.Items.Add(o); return o;
        }
        public void Ayrac() => m.Items.Add(new ToolStripSeparator());
    }

    /// <summary>Dişli simgeli yuvarlak düğme (simge kodla çizilir, yazı tipine bağlı değil).</summary>
    public sealed class AyarDugmesi : Control
    {
        bool uzerinde;
        public AyarDugmesi()
        {
            Size = new Size(38, 32); Cursor = Cursors.Hand; Margin = new Padding(8, 0, 0, 0); TabStop = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AccessibleName = "Ayarlar ve komutlar";
        }
        protected override void OnMouseEnter(EventArgs e) { uzerinde = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { uzerinde = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var z = new SolidBrush(uzerinde ? Color.FromArgb(52, 96, 76) : Color.FromArgb(38, 74, 58)))
            using (var yol = Yuvarlak(new RectangleF(0, 0, Width - 1, Height - 1), 6)) g.FillPath(z, yol);
            float cx = Width / 2f, cy = Height / 2f, rDis = 9.5f, rIc = 7f, rDelik = 3.2f; int dis = 8;
            using var p = new GraphicsPath();
            var nk = new List<PointF>();
            for (int i = 0; i < dis * 4; i++)
            {
                double a = Math.PI * 2 * i / (dis * 4) - Math.PI / (dis * 4);
                float r = (i % 4 == 0 || i % 4 == 1) ? rDis : rIc;
                nk.Add(new PointF(cx + (float)(r * Math.Cos(a)), cy + (float)(r * Math.Sin(a))));
            }
            p.AddPolygon(nk.ToArray());
            p.AddEllipse(cx - rDelik, cy - rDelik, rDelik * 2, rDelik * 2);
            p.FillMode = FillMode.Alternate;
            using var b = new SolidBrush(Color.White); g.FillPath(b, p);
        }
        static GraphicsPath Yuvarlak(RectangleF r, float y)
        {
            var p = new GraphicsPath(); float d = y * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
            return p;
        }
    }

    /// <summary>Menü: beyaz zemin, açık yeşil vurgu, ince kenar.</summary>
    sealed class MenuCizici : ToolStripProfessionalRenderer
    {
        public MenuCizici() : base(new Renkler()) { RoundedEdges = false; }
        sealed class Renkler : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(226, 239, 231);
            public override Color MenuItemBorder => Color.FromArgb(226, 239, 231);
            public override Color MenuBorder => Color.FromArgb(200, 206, 202);
            public override Color ToolStripDropDownBackground => Color.White;
            public override Color ImageMarginGradientBegin => Color.White;
            public override Color ImageMarginGradientMiddle => Color.White;
            public override Color ImageMarginGradientEnd => Color.White;
            public override Color SeparatorDark => Color.FromArgb(225, 228, 224);
        }
    }
}
