using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EcoBuildss.Core;

namespace EcoBuildss.UI
{
    /// <summary>Ortak çizim yardımcıları ve renkler (dış kütüphane kullanılmaz).</summary>
    public static class Cizim
    {
        public static readonly Color Metin = Color.FromArgb(29, 35, 33);
        public static readonly Color Soluk = Color.FromArgb(108, 118, 114);
        public static readonly Color Izgara = Color.FromArgb(232, 235, 231);
        public static readonly Color Eksen = Color.FromArgb(180, 186, 182);
        // Küme / seri renkleri: yeşil, turuncu, mavi (renk körlüğüne dayanıklı ayrışma)
        public static readonly Color[] Seri = { Color.FromArgb(31, 138, 92), Color.FromArgb(214, 118, 38), Color.FromArgb(52, 106, 178), Color.FromArgb(150, 90, 160),
                                                Color.FromArgb(196, 160, 20), Color.FromArgb(40, 165, 190), Color.FromArgb(190, 60, 100), Color.FromArgb(95, 100, 98) };

        public static void Hazirla(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        }

        /// <summary>Okunur eksen adımları (1, 2, 2.5, 5 × 10ⁿ).</summary>
        public static double[] Adimlar(double min, double max, int hedef = 5)
        {
            if (max <= min) max = min + 1;
            double ham = (max - min) / hedef, us = Math.Pow(10, Math.Floor(Math.Log10(ham)));
            double adim = new[] { 1, 2, 2.5, 5, 10 }.Select(k => k * us).First(k => k >= ham);
            double bas = Math.Floor(min / adim) * adim, son = Math.Ceiling(max / adim) * adim;
            var l = new List<double>(); for (double v = bas; v <= son + adim / 2; v += adim) l.Add(Math.Round(v, 10));
            return l.ToArray();
        }

        public static string Kisa(double v)
        {
            var tr = Ortak.TR;
            double a = Math.Abs(v);
            return a >= 1e6 ? (v / 1e6).ToString("0.#", tr) + " mn" : a >= 1e4 ? (v / 1e3).ToString("0", tr) + " bin" : v.ToString(a < 10 ? "0.##" : "N0", tr);
        }
    }

    /// <summary>
    /// X–Y grafiği: noktalar (saçılım), çizgi serileri (eğri, regresyon doğrusu, Pareto cephesi) ve dikey işaret çizgileri
    /// (optimum, yönetmelik alt sınırı, bütçe). Dış kütüphane kullanılmaz.
    /// </summary>
    public sealed class SacilimGrafik : Control
    {
        public sealed class Nokta { public double X, Y; public int Seri; public bool Vurgu; public string? Etiket; public bool Soluk; }
        public sealed class Cizgi { public List<(double x, double y)> Noktalar = new(); public int Seri; public bool Kesikli; public float Kalinlik = 2f; }
        public List<Nokta> Noktalar { get; } = new();
        public List<Cizgi> Cizgiler { get; } = new();
        public List<(double x, string etiket, int seri)> Dikeyler { get; } = new();
        public string[] SeriAd = Array.Empty<string>();
        public int[]? SeriRenk;   // açıklamadaki her ad için renk sırası (verilmezse 0, 1, 2…)
        public string XBaslik = "", YBaslik = "";
        public Func<double, string> XBicim = Cizim.Kisa, YBicim = Cizim.Kisa;
        public double? XMin, YMin;   // eksen alt sınırı sabitlenmek istenirse (ör. 0)

        public SacilimGrafik()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
        }

        public void Temizle() { Noktalar.Clear(); Cizgiler.Clear(); Dikeyler.Clear(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Cizim.Hazirla(g);
            var xsT = Noktalar.Select(n => n.X).Concat(Cizgiler.SelectMany(c => c.Noktalar.Select(p => p.x))).Concat(Dikeyler.Select(d => d.x)).ToList();
            var ysT = Noktalar.Select(n => n.Y).Concat(Cizgiler.SelectMany(c => c.Noktalar.Select(p => p.y))).ToList();
            if (xsT.Count == 0 || ysT.Count == 0 || Width < 120 || Height < 100) return;
            using var f = new Font("Segoe UI", 8.5f);
            using var fb = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var soluk = new SolidBrush(Cizim.Soluk);
            using var mb = new SolidBrush(Cizim.Metin);
            // açıklama (lejant) dar alana sığmazsa alt satıra geçer; çizim alanı satır sayısı kadar aşağı kayar
            var lejant = new List<(float x, int satir)>(); float lejX = 6; int lSatir = 0;
            for (int i = 0; i < SeriAd.Length; i++)
            {
                float lw = g.MeasureString(SeriAd[i], f).Width + 15;
                if (lejX > 6 && lejX + lw > Width - 6) { lSatir++; lejX = 6; }
                lejant.Add((lejX, lSatir)); lejX += lw + 15;
            }
            float ek0 = lSatir * 16;
            var alan = new RectangleF(70, 46 + ek0, Width - 90, Height - 94 - ek0);   // üstte açıklama satırı ve eksen başlığı

            double x0 = XMin ?? xsT.Min(), x1 = xsT.Max(), y0 = YMin ?? ysT.Min(), y1 = ysT.Max();
            var xs = Cizim.Adimlar(x0, x1); var ys = Cizim.Adimlar(y0, y1);
            double xa = xs[0], xb = xs[xs.Length - 1], ya = ys[0], yb = ys[ys.Length - 1];
            float PX(double v) => alan.Left + (float)((v - xa) / (xb - xa)) * alan.Width;
            float PY(double v) => alan.Bottom - (float)((v - ya) / (yb - ya)) * alan.Height;

            using (var iz = new Pen(Cizim.Izgara))
            {
                foreach (var v in ys) { g.DrawLine(iz, alan.Left, PY(v), alan.Right, PY(v)); var t = YBicim(v); var sz = g.MeasureString(t, f); g.DrawString(t, f, soluk, alan.Left - sz.Width - 4, PY(v) - sz.Height / 2); }
                foreach (var v in xs) { g.DrawLine(iz, PX(v), alan.Top, PX(v), alan.Bottom); var t = XBicim(v); var sz = g.MeasureString(t, f); g.DrawString(t, f, soluk, PX(v) - sz.Width / 2, alan.Bottom + 4); }
            }
            using (var ek = new Pen(Cizim.Eksen)) { g.DrawLine(ek, alan.Left, alan.Bottom, alan.Right, alan.Bottom); g.DrawLine(ek, alan.Left, alan.Top, alan.Left, alan.Bottom); }

            // dikey işaretler (etiketler üst üste binmesin diye sırayla aşağı kaydırılır)
            float etiketY = alan.Top + 2;
            foreach (var (x, etiket, seri) in Dikeyler.OrderBy(d => d.x))
            {
                var c = Cizim.Seri[seri % Cizim.Seri.Length];
                using var p = new Pen(c, 1.6f) { DashStyle = DashStyle.Dash };
                float px = PX(x);
                g.DrawLine(p, px, alan.Top, px, alan.Bottom);
                if (etiket.Length > 0)
                {
                    var sz = g.MeasureString(etiket, fb);
                    float lx = px + 4 + sz.Width > alan.Right ? px - sz.Width - 4 : px + 4;
                    using var zb = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
                    g.FillRectangle(zb, lx - 1, etiketY, sz.Width + 2, sz.Height);
                    using var cb = new SolidBrush(c);
                    g.DrawString(etiket, fb, cb, lx, etiketY);
                    etiketY += sz.Height + 2;
                }
            }

            foreach (var cz in Cizgiler.Where(c => c.Noktalar.Count > 1))
            {
                using var p = new Pen(Cizim.Seri[cz.Seri % Cizim.Seri.Length], cz.Kalinlik) { DashStyle = cz.Kesikli ? DashStyle.Dash : DashStyle.Solid };
                g.DrawLines(p, cz.Noktalar.Select(q => new PointF(PX(q.x), PY(q.y))).ToArray());
            }

            foreach (var n in Noktalar.Where(n => !n.Vurgu))
            {
                var c = n.Soluk ? Color.FromArgb(185, 190, 187) : Cizim.Seri[n.Seri % Cizim.Seri.Length];
                using var b = new SolidBrush(Color.FromArgb(n.Soluk ? 170 : 220, c));
                g.FillEllipse(b, PX(n.X) - 4, PY(n.Y) - 4, 8, 8);
                if (n.Etiket != null) g.DrawString(n.Etiket, f, soluk, PX(n.X) + 6, PY(n.Y) - 16);
            }
            foreach (var n in Noktalar.Where(n => n.Vurgu))
            {
                var c = Cizim.Seri[n.Seri % Cizim.Seri.Length];
                using var b = new SolidBrush(c);
                using var p = new Pen(Cizim.Metin, 2f);
                g.FillEllipse(b, PX(n.X) - 7, PY(n.Y) - 7, 14, 14);
                g.DrawEllipse(p, PX(n.X) - 7, PY(n.Y) - 7, 14, 14);
                if (n.Etiket != null)
                {
                    var sz = g.MeasureString(n.Etiket, fb);
                    float lx = Math.Min(PX(n.X) + 10, alan.Right - sz.Width), ly = Math.Max(alan.Top, PY(n.Y) - sz.Height - 6);
                    using var zb = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
                    g.FillRectangle(zb, lx - 2, ly, sz.Width + 4, sz.Height);
                    g.DrawString(n.Etiket, fb, mb, lx, ly);
                }
            }

            var xsz = g.MeasureString(XBaslik, f); g.DrawString(XBaslik, f, soluk, alan.Left + (alan.Width - xsz.Width) / 2, alan.Bottom + 22);
            g.DrawString(YBaslik, f, soluk, 4, 24 + ek0);
            // açıklama (lejant) en üst satırda, soldan sağa
            for (int i = 0; i < SeriAd.Length; i++)
            {
                var (lxA, ls) = lejant[i];
                using var b = new SolidBrush(Cizim.Seri[(SeriRenk != null ? SeriRenk[i] : i) % Cizim.Seri.Length]);
                g.FillRectangle(b, lxA, 9 + ls * 16, 12, 4);
                g.DrawString(SeriAd[i], f, mb, lxA + 15, 3 + ls * 16);
            }
        }
    }

    /// <summary>Yatay çubuk grafiği; negatif değerleri sıfırın soluna çizer.</summary>
    public sealed class CubukGrafik : Control
    {
        public List<(string ad, double deger, int renk)> Cubuklar { get; } = new();
        public Func<double, string> Bicim = v => Cizim.Kisa(v);
        public int EtiketGenisligi = 150;

        public CubukGrafik()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Cizim.Hazirla(g);
            if (Cubuklar.Count == 0) return;
            using var f = new Font("Segoe UI", 9f);
            using var fb = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var mb = new SolidBrush(Cizim.Metin);
            double min = Math.Min(0, Cubuklar.Min(c => c.deger)), max = Math.Max(0, Cubuklar.Max(c => c.deger));
            if (max == min) max = min + 1;
            float sol = EtiketGenisligi, sag = Width - 80, satir = Math.Min(52, (Height - 8f) / Cubuklar.Count);
            float X(double v) => sol + (float)((v - min) / (max - min)) * (sag - sol);
            using (var ek = new Pen(Cizim.Eksen)) g.DrawLine(ek, X(0), 2, X(0), 4 + satir * Cubuklar.Count);
            for (int i = 0; i < Cubuklar.Count; i++)
            {
                var (ad, v, renk) = Cubuklar[i];
                float y = 4 + i * satir;
                var sz = g.MeasureString(ad, f);
                g.DrawString(ad, f, mb, sol - sz.Width - 8, y + (satir - sz.Height) / 2);
                float a = X(Math.Min(0, v)), b = X(Math.Max(0, v));
                using var br = new SolidBrush(Cizim.Seri[renk % Cizim.Seri.Length]);
                g.FillRectangle(br, a, y + satir * 0.2f, Math.Max(1, b - a), satir * 0.6f);
                string s = Bicim(v);
                var ss = g.MeasureString(s, fb);
                // negatif çubukta değer, sıfır çizgisinin sağına yazılır (etiketle çakışmasın)
                g.DrawString(s, fb, mb, v >= 0 ? b + 4 : X(0) + 4, y + (satir - ss.Height) / 2);
            }
        }
    }

    /// <summary>KPI kartı: başlık, büyük değer, karşılaştırma satırı ve durum şeridi.</summary>
    public sealed class KpiKarti : Control
    {
        public string Baslik = "", Deger = "", Alt = "";
        public int Durum;   // 0 nötr, 1 iyi, 2 dikkat

        public KpiKarti()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White; Size = new Size(232, 104); Margin = new Padding(0, 0, 12, 12);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Cizim.Hazirla(g);
            var serit = Durum == 1 ? Color.FromArgb(31, 138, 92) : Durum == 2 ? Color.FromArgb(214, 118, 38) : Color.FromArgb(200, 205, 201);
            using (var b = new SolidBrush(serit)) g.FillRectangle(b, 0, 0, 4, Height);
            using (var p = new Pen(Color.FromArgb(225, 228, 224))) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            using var f1 = new Font("Segoe UI", 9f);
            using var f2 = new Font("Segoe UI Semibold", 17f);
            using var f3 = new Font("Segoe UI", 8.5f);
            using var soluk = new SolidBrush(Cizim.Soluk);
            using var mb = new SolidBrush(Cizim.Metin);
            g.DrawString(Baslik, f1, soluk, 14, 10);
            g.DrawString(Deger, f2, mb, 12, 30);
            using var ab = new SolidBrush(Durum == 2 ? Color.FromArgb(180, 84, 26) : Cizim.Soluk);
            g.DrawString(Alt, f3, ab, new RectangleF(14, 70, Width - 20, 32));
        }
    }

    /// <summary>
    /// Duyarlılık şeridi: bir girdinin değer aralığını, önerinin aynı kaldığı dilimlere bölünmüş olarak gösterir.
    /// </summary>
    public sealed class DilimGrafik : Control
    {
        public double Bas, Son, Mevcut;
        public readonly List<(double bas, double son, int renk, string etiket)> Dilimler = new();
        public readonly List<(double x, string ad)> Isaretler = new();
        public Func<double, string> Bicim = v => v.ToString("0", Ortak.TR);

        public DilimGrafik()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White; Height = 96;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Cizim.Hazirla(g);
            if (Dilimler.Count == 0 || Son <= Bas) return;
            float sol = 14, sag = Width - 14, y0 = 30, h = 26;
            float X(double v) => sol + (float)((v - Bas) / (Son - Bas)) * (sag - sol);
            using var f = new Font("Segoe UI", 8.5f);
            using var fk = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var soluk = new SolidBrush(Cizim.Soluk);
            using var metin = new SolidBrush(Cizim.Metin);
            for (int i = 0; i < Dilimler.Count; i++)
            {
                var d = Dilimler[i];
                // dilim, kendi ilk ve son değeri arasındaki aralığın yanı sıra bir sonraki dilime kadar uzanır
                double bitis = i + 1 < Dilimler.Count ? (d.son + Dilimler[i + 1].bas) / 2 : d.son;
                double baslangic = i > 0 ? (Dilimler[i - 1].son + d.bas) / 2 : d.bas;
                float x1 = X(baslangic), x2 = X(bitis);
                using (var b = new SolidBrush(Cizim.Seri[d.renk % Cizim.Seri.Length])) g.FillRectangle(b, x1, y0, Math.Max(1, x2 - x1), h);
                if (x2 - x1 > 22) TextOrta(g, d.etiket, fk, Brushes.White, new RectangleF(x1, y0, x2 - x1, h));
                if (i > 0)
                {
                    g.DrawLine(Pens.White, x1, y0, x1, y0 + h);
                    var t = Bicim(baslangic); var sz = g.MeasureString(t, f);
                    g.DrawString(t, f, metin, Math.Min(Math.Max(x1 - sz.Width / 2, sol - 10), sag - sz.Width + 10), y0 + h + 4);
                }
            }
            // eksen uçları
            g.DrawString(Bicim(Bas), f, soluk, sol - 6, y0 + h + 20);
            var ts = Bicim(Son); g.DrawString(ts, f, soluk, sag - g.MeasureString(ts, f).Width + 6, y0 + h + 20);
            // işaretler (ör. Kademe-2 tarifesi)
            foreach (var (x, ad) in Isaretler)
            {
                if (x < Bas || x > Son) continue;
                float px = X(x);
                using var p = new Pen(Cizim.Metin, 1.2f) { DashStyle = DashStyle.Dash };
                g.DrawLine(p, px, y0 - 4, px, y0 + h + 4);
                var sz = g.MeasureString(ad, f);
                g.DrawString(ad, f, metin, Math.Min(Math.Max(px - sz.Width / 2, 0), Width - sz.Width), y0 + h + 20);
            }
            // mevcut değer
            float mx = X(Math.Max(Bas, Math.Min(Son, Mevcut)));
            using (var b = new SolidBrush(Cizim.Metin))
                g.FillPolygon(b, new[] { new PointF(mx - 6, y0 - 12), new PointF(mx + 6, y0 - 12), new PointF(mx, y0 - 3) });
            var ms = "şu an " + Bicim(Mevcut); var msz = g.MeasureString(ms, fk);
            g.DrawString(ms, fk, metin, Math.Min(Math.Max(mx - msz.Width / 2, 0), Width - msz.Width), 0);
        }

        static void TextOrta(Graphics g, string s, Font f, Brush b, RectangleF r)
        {
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(s, f, b, r, sf);
        }
    }
}

