using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EcoBuildss.Core;

namespace EcoBuildss.UI
{
    /// <summary>Tıklanabilir malzeme kartı: doku örneği, ad, fiyat, teknik değer, karbon.</summary>
    public sealed class MalzemeKarti : Control
    {
        static readonly Color Yesil = Color.FromArgb(31, 111, 74);
        public Malzeme Malzeme { get; }
        bool secili, onerilen, uzerinde;

        public event EventHandler? Secildi;

        public bool Secili { get => secili; set { secili = value; Invalidate(); } }
        public bool Onerilen { get => onerilen; set { onerilen = value; Invalidate(); } }

        public MalzemeKarti(Malzeme m)
        {
            Malzeme = m;
            Size = new Size(140, 104);
            Margin = new Padding(0, 0, 8, 8);
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = Color.White;
        }

        protected override void OnMouseEnter(EventArgs e) { uzerinde = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { uzerinde = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Focus(); Secildi?.Invoke(this, EventArgs.Empty); base.OnClick(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space) { Secildi?.Invoke(this, EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var tr = Ortak.TR;
            var m = Malzeme;

            var ornek = new RectangleF(1, 1, Width - 2, 36);
            Doku.Ciz(g, ornek, m.Id);

            using var baslik = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var kucuk = new Font("Segoe UI", 8f);
            using var gri = new SolidBrush(Color.FromArgb(93, 103, 99));
            using var siyah = new SolidBrush(Color.FromArgb(29, 35, 33));
            g.DrawString(m.KisaAd, baslik, siyah, 6, 39);
            g.DrawString(m.FiyatM2.ToString("N0", tr) + " TL/m²", kucuk, siyah, 6, 57);
            string teknik = m.Tur == 'P' ? $"cam Ug {m.U.ToString("0.00", tr)}" : $"λ {m.Lambda.ToString("0.000", tr)} · {(m.Kalinlik * 100).ToString("0.#", tr)} cm";
            g.DrawString(teknik, kucuk, gri, 6, 71);
            g.DrawString(m.KarbonM2.ToString("0.0", tr) + " kgCO₂e/m²", kucuk, gri, 6, 85);

            if (onerilen)
            {
                var etiket = new RectangleF(Width - 66, 4, 62, 16);
                using var eb = new SolidBrush(Color.FromArgb(230, 31, 111, 74));
                g.FillRectangle(eb, etiket);
                using var ef = new Font("Segoe UI", 7f, FontStyle.Bold);
                Doku.TextOrta(g, "ÖNERİLEN", ef, Color.White, etiket);
            }

            var cerceve = new Rectangle(0, 0, Width - 1, Height - 1);
            if (secili)
            {
                using var p = new Pen(Yesil, 3f) { Alignment = PenAlignment.Inset };
                g.DrawRectangle(p, cerceve);
                using var ib = new SolidBrush(Yesil);
                g.FillEllipse(ib, Width - 24, Height - 24, 18, 18); // onay işareti
                using var tik = new Pen(Color.White, 2f);
                g.DrawLines(tik, new[] { new PointF(Width - 20, Height - 15), new PointF(Width - 16.5f, Height - 11), new PointF(Width - 10, Height - 19) });
            }
            else
            {
                using var p = new Pen(uzerinde ? Color.FromArgb(120, 160, 140) : Color.FromArgb(217, 221, 216), uzerinde ? 2f : 1f);
                g.DrawRectangle(p, cerceve);
            }
            if (Focused) ControlPaint.DrawFocusRectangle(g, new Rectangle(3, 3, Width - 7, Height - 7));
        }
    }
}
