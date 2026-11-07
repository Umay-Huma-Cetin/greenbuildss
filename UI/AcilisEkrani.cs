using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Açılış animasyonu (yaklaşık 2 saniye): logo kurulur — bina gövdesi yerden yükselir, yaprak çatı büyüyerek açılır,
    /// kapı yanar —, ürün adı ve alt başlık belirir, alt çizgi dolar. Çizgi dolunca ana pencere hazırlanır (Hazirla),
    /// sonra ekran solarak kapanır. Tıklama ya da bir tuş animasyonu atlar. Çizim kodla yapılır, dosya gerekmez.
    /// </summary>
    public sealed class AcilisEkrani : Form
    {
        const double Sure = 1.9, Solma = 0.3;          // saniye: animasyon, kapanırken solma
        readonly Stopwatch saat = Stopwatch.StartNew();
        readonly System.Windows.Forms.Timer zaman = new() { Interval = 15 };
        bool hazirlandi, kapaniyor; double kapanisBasi;
        /// <summary>Çizgi dolunca bir kez çağrılır (ana pencereyi kurmak için); bu sırada ekran son karede bekler.</summary>
        public Action? Hazirla;

        static readonly Color Zemin = Marka.Koyu, ZeminAcik = Color.FromArgb(31, 70, 54);

        public AcilisEkrani()
        {
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(560, 340); ShowInTaskbar = true; TopMost = true; Text = Marka.Ad; BackColor = Zemin;
            DoubleBuffered = true; Opacity = 0; KeyPreview = true;
            try { Icon = Marka.Simge(); } catch { }
            using (var p = Yuvarlak(new RectangleF(0, 0, Width, Height), 14)) Region = new Region(p);
            zaman.Tick += (_, _) => Ilerle();
            Shown += (_, _) => zaman.Start();
            Click += (_, _) => Atla(); KeyDown += (_, _) => Atla();
        }

        double T => saat.Elapsed.TotalSeconds;
        static double Ara(double t, double a, double b) => Math.Max(0, Math.Min(1, (t - a) / (b - a)));
        static double Yumusak(double x) => 1 - Math.Pow(1 - x, 3);                       // ease-out
        static double Yaylan(double x) { double c = 1.70158; return 1 + (c + 1) * Math.Pow(x - 1, 3) + c * Math.Pow(x - 1, 2); }  // hafif aşma

        double ofset;                                    // atlanınca animasyon sonuna sıçrar
        void Atla() { if (!kapaniyor) ofset = Math.Max(ofset, Sure - T); }

        void Ilerle()
        {
            double t = T + ofset;
            if (!kapaniyor)
            {
                Opacity = Math.Min(1, t / 0.25);
                if (t >= Sure)
                {
                    Opacity = 1; Invalidate(); Update();
                    if (!hazirlandi) { hazirlandi = true; try { Hazirla?.Invoke(); } catch (Exception ex) { Depo.Gunluk.Hata("Ana pencere hazırlanamadı", ex); } }
                    kapaniyor = true; kapanisBasi = T;
                }
            }
            else
            {
                double s = Ara(T, kapanisBasi, kapanisBasi + Solma);
                Opacity = 1 - s;
                if (s >= 1) { zaman.Stop(); Close(); return; }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            double t = kapaniyor ? Sure : T + ofset;
            using (var z = new LinearGradientBrush(ClientRectangle, ZeminAcik, Zemin, 60f)) g.FillRectangle(z, ClientRectangle);

            // arka plandaki yumuşak halka
            float cx = Width / 2f, cy = 128;
            double h = Yumusak(Ara(t, 0.0, 0.9));
            using (var hb = new SolidBrush(Color.FromArgb((int)(28 * h), Marka.Acik)))
            { float r = (float)(58 + 22 * h); g.FillEllipse(hb, cx - r, cy - r, 2 * r, 2 * r); }
            using (var hk = new Pen(Color.FromArgb(150, Marka.Acik), 2f))
            { float r = 74; float aci = (float)(360 * Yumusak(Ara(t, 0.1, 1.2))); if (aci > 0.5f) g.DrawArc(hk, cx - r, cy - r, 2 * r, 2 * r, -90, aci); }

            // logo: 96 × 96 kare, merkez (cx, cy)
            float w = 96, x = cx - w / 2, y = cy - w / 2;
            // 1) gövde yerden yükselir
            double gv = Yumusak(Ara(t, 0.15, 0.6));
            if (gv > 0)
            {
                float gy = y + w * 0.48f, gh = w * 0.44f;
                var st = g.Save(); g.SetClip(new RectangleF(x, y, w, gy + gh - y));
                using var b = new SolidBrush(Color.White);
                g.FillRectangle(b, x + w * 0.18f, gy + (float)((1 - gv) * gh), w * 0.64f, gh);
                g.Restore(st);
            }
            // 2) yaprak çatı sol uçtan büyüyerek açılır
            double ct = Ara(t, 0.5, 1.05);
            if (ct > 0)
            {
                float s = (float)Yaylan(ct);
                var st = g.Save();
                float px = x + w * 0.08f, py = y + w * 0.50f;
                g.TranslateTransform(px, py); g.RotateTransform((float)(-25 * (1 - Yumusak(ct)))); g.ScaleTransform(s, s); g.TranslateTransform(-px, -py);
                using var yol = new GraphicsPath();
                yol.AddBezier(x + w * 0.08f, y + w * 0.50f, x + w * 0.20f, y + w * 0.06f, x + w * 0.80f, y + w * 0.02f, x + w * 0.94f, y + w * 0.16f);
                yol.AddBezier(x + w * 0.94f, y + w * 0.16f, x + w * 0.86f, y + w * 0.40f, x + w * 0.50f, y + w * 0.52f, x + w * 0.08f, y + w * 0.50f);
                yol.CloseFigure();
                using (var b = new SolidBrush(Marka.Acik)) g.FillPath(b, yol);
                using var damar = new Pen(Color.White, w * 0.04f);
                g.DrawBezier(damar, x + w * 0.16f, y + w * 0.46f, x + w * 0.40f, y + w * 0.34f, x + w * 0.64f, y + w * 0.22f, x + w * 0.88f, y + w * 0.17f);
                g.Restore(st);
            }
            // 3) kapı yanar
            double kp = Ara(t, 0.95, 1.2);
            if (kp > 0)
                using (var b = new SolidBrush(Color.FromArgb((int)(255 * kp), Marka.Acik)))
                    g.FillRectangle(b, x + w * 0.44f, y + w * 0.68f, w * 0.12f, w * 0.24f);

            // ad ve alt başlık
            double ad = Yumusak(Ara(t, 0.9, 1.4));
            using (var f = new Font("Segoe UI Semibold", 26f))
            using (var b = new SolidBrush(Color.FromArgb((int)(255 * ad), Color.White)))
            {
                var sz = g.MeasureString(Marka.Ad, f);
                g.DrawString(Marka.Ad, f, b, cx - sz.Width / 2, 212 + (float)(14 * (1 - ad)));
            }
            double alt = Yumusak(Ara(t, 1.15, 1.6));
            using (var f = new Font("Segoe UI", 10.5f))
            using (var b = new SolidBrush(Color.FromArgb((int)(210 * alt), Color.FromArgb(205, 228, 214))))
            {
                const string s = "Yeşil bina malzeme seçimi · Karar destek sistemi";
                var sz = g.MeasureString(s, f);
                g.DrawString(s, f, b, cx - sz.Width / 2, 258 + (float)(8 * (1 - alt)));
            }

            // alt çizgi
            float cw = 220, cxl = cx - cw / 2, cyl = 300;
            using (var z = new SolidBrush(Color.FromArgb(50, Color.White))) g.FillRectangle(z, cxl, cyl, cw, 3);
            double dol = Yumusak(Ara(t, 0.2, Sure));
            using (var d = new SolidBrush(Marka.Acik)) g.FillRectangle(d, cxl, cyl, (float)(cw * dol), 3);
        }

        static GraphicsPath Yuvarlak(RectangleF r, float y)
        {
            var p = new GraphicsPath(); float d = y * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
            return p;
        }

        protected override void Dispose(bool disposing) { if (disposing) zaman.Dispose(); base.Dispose(disposing); }
    }
}
