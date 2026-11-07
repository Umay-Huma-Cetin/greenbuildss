using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EcoBuildss.Core;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Karar ağacının çizimi: iç düğümde koşul, yaprakta tahmin edilen paket ve il sayısı. Yapraklar soldan sağa sıralanır,
    /// iç düğüm çocuklarının ortasına yerleşir. Sol dal "evet" (koşul sağlanıyor), sağ dal "hayır". Dış kütüphane kullanılmaz.
    /// </summary>
    public sealed class AgacCizimi : Control
    {
        public KararAgaci? Agac;
        public Func<string, string> SinifAdi = s => s;
        public Func<string, int> SinifRengi = _ => 0;
        public Func<int, double, string> EsikBicim = (_, e) => e.ToString("N0", Ortak.TR);
        public AgacDugumu? Vurgu;          // seçili ilin düştüğü yaprak
        public HashSet<AgacDugumu> Yol = new();

        public AgacCizimi() { DoubleBuffered = true; BackColor = Color.White; ResizeRedraw = true; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Cizim.Hazirla(g);
            if (Agac == null) return;
            var yap = new List<AgacDugumu>(); Topla(Agac.Kok, yap);
            int derin = Math.Max(1, Agac.Derinlik);
            float W = ClientSize.Width, H = ClientSize.Height;
            float kutuW = Math.Min(190, (W - 16) / Math.Max(1, yap.Count) - 10), kutuH = 50, yaprakH = 62;
            float dy = Math.Max(kutuH + 26, (H - yaprakH - 8) / derin);
            var konum = new Dictionary<AgacDugumu, PointF>();
            int sira = 0;
            float Yerlestir(AgacDugumu d, int dz)
            {
                float x;
                if (d.Yaprak) { x = (W / yap.Count) * (sira++ + 0.5f); }
                else { float a = Yerlestir(d.Sol!, dz + 1), b = Yerlestir(d.Sag!, dz + 1); x = (a + b) / 2; }
                konum[d] = new PointF(x, 4 + dz * dy);
                return x;
            }
            Yerlestir(Agac.Kok, 0);

            using var f = new Font("Segoe UI", 8.5f); using var fk = new Font("Segoe UI", 8.5f, FontStyle.Bold); using var fs = new Font("Segoe UI", 8f);
            using var kalem = new Pen(Cizim.Eksen, 1.4f); using var yolKalem = new Pen(Color.FromArgb(31, 111, 74), 2.6f);
            using var metin = new SolidBrush(Cizim.Metin); using var soluk = new SolidBrush(Cizim.Soluk);
            // kenarlar
            foreach (var kv in konum)
            {
                var d = kv.Key; var p = kv.Value;
                if (d.Yaprak) continue;
                foreach (var (c, ad) in new[] { (d.Sol!, "evet"), (d.Sag!, "hayır") })
                {
                    var q = konum[c];
                    var a = new PointF(p.X, p.Y + kutuH); var b = new PointF(q.X, q.Y);
                    g.DrawLine(Yol.Contains(d) && Yol.Contains(c) ? yolKalem : kalem, a, b);
                    var orta = new PointF((a.X + b.X) / 2, (a.Y + b.Y) / 2 - 8);
                    var sz = g.MeasureString(ad, fs);
                    using var zemin = new SolidBrush(Color.White);
                    g.FillRectangle(zemin, orta.X - sz.Width / 2, orta.Y, sz.Width, sz.Height);
                    g.DrawString(ad, fs, soluk, orta.X - sz.Width / 2, orta.Y);
                }
            }
            // düğümler
            var orta2 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            foreach (var kv in konum)
            {
                var d = kv.Key; var p = kv.Value;
                var r = new RectangleF(p.X - kutuW / 2, p.Y, kutuW, d.Yaprak ? yaprakH : kutuH);
                using var yol = Yuvarlak(r, 6);
                if (d.Yaprak)
                {
                    var renk = Cizim.Seri[SinifRengi(d.Sinif) % Cizim.Seri.Length];
                    using var b = new SolidBrush(Color.FromArgb(d == Vurgu ? 255 : 40, renk));
                    g.FillPath(b, yol);
                    using var c = new Pen(renk, d == Vurgu ? 2.6f : 1.4f); g.DrawPath(c, yol);
                    using var yazi = new SolidBrush(d == Vurgu ? Color.White : Cizim.Metin);
                    g.DrawString(SinifAdi(d.Sinif), fk, yazi, new RectangleF(r.X + 4, r.Y + 3, r.Width - 8, 42), orta2);
                    g.DrawString($"{d.N} il", f, yazi, new RectangleF(r.X + 4, r.Y + 43, r.Width - 8, 16), orta2);
                }
                else
                {
                    using var b = new SolidBrush(Yol.Contains(d) ? Color.FromArgb(226, 239, 231) : Color.FromArgb(246, 247, 245));
                    g.FillPath(b, yol);
                    using var c = new Pen(Yol.Contains(d) ? Color.FromArgb(31, 111, 74) : Cizim.Eksen, 1.2f); g.DrawPath(c, yol);
                    g.DrawString($"{Agac.OzellikAdlari[d.Ozellik]} ≤ {EsikBicim(d.Ozellik, d.Esik)} ?", fk, metin, new RectangleF(r.X + 4, r.Y + 4, r.Width - 8, 24), orta2);
                    g.DrawString($"{d.N} il · Gini {d.Gini.ToString("0.00", Ortak.TR)}", f, soluk, new RectangleF(r.X + 4, r.Y + 27, r.Width - 8, 18), orta2);
                }
            }
        }

        static void Topla(AgacDugumu d, List<AgacDugumu> l) { if (d.Yaprak) l.Add(d); else { Topla(d.Sol!, l); Topla(d.Sag!, l); } }

        static GraphicsPath Yuvarlak(RectangleF r, float y)
        {
            var p = new GraphicsPath(); float d = y * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
            return p;
        }
    }
}
