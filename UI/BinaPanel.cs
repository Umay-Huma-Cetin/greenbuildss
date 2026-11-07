using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EcoBuildss.Core;

namespace EcoBuildss.UI
{
    /// <summary>Seçilen tasarım ve malzemelerle binayı, ısı kaybını ve ölçekli duvar kesitini çizer.</summary>
    public sealed class BinaPanel : Control
    {
        public BinaFormu? Form { get; set; }
        public int Yon { get; set; } = 4;
        public double CamOrani { get; set; } = 0.25;
        public Malzeme? Yalitim { get; set; }
        public Malzeme? Cam { get; set; }
        public Malzeme? Duvar { get; set; }
        public double DuvarU { get; set; }
        public double HBaz { get; set; }
        public double HYeni { get; set; }
        public double SinirU { get; set; } = 0.35;
        public double CatiU { get; set; } = 0.5;
        public double KatYuksekligi { get; set; } = 3.5;

        static readonly string[] YonAdi = { "", "", "Kuzey", "Doğu", "Güney", "Batı" };

        public BinaPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            MinimumSize = new Size(420, 250);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var tr = Ortak.TR;
            float W = Width, H = Height;

            // Gökyüzü ve zemin
            using (var gok = new LinearGradientBrush(new RectangleF(0, 0, W, H), Color.FromArgb(214, 232, 244), Color.FromArgb(246, 249, 250), 90f))
                g.FillRectangle(gok, 0, 0, W, H);
            float zeminY = H - 46;
            using (var zemin = new SolidBrush(Color.FromArgb(178, 196, 168))) g.FillRectangle(zemin, 0, zeminY, W, H - zeminY);
            using (var zc = new Pen(Color.FromArgb(140, 160, 130), 2)) g.DrawLine(zc, 0, zeminY, W, zeminY);

            if (Form == null || Yalitim == null || Cam == null || Duvar == null) return;

            // Ölçek: veri setindeki formlar için sabit (en büyük oturum ~14,85 m kenar, en yüksek 7 m) — formlar birbirine göre
            // doğru boyda görünür; özel ölçülü bina daha büyükse ölçek ona göre küçülür. Özel ölçüde ön cephe genişliği "en"dir.
            float kenarM = (float)(Form.En ?? Math.Sqrt(Form.CatiAlani));
            float yukM = (float)Form.Yukseklik;
            float s = Math.Min((W * 0.40f) / Math.Max(15f, kenarM), (H * 0.66f) / Math.Max(7.5f, yukM));
            float bw = kenarM * s, bh = yukM * s, derin = bw * 0.32f;
            float bx = W * 0.30f - bw / 2 + 20, by = zeminY - bh;
            int kat = Math.Max(1, (int)Math.Round(Form.Yukseklik / KatYuksekligi));
            // Ön cephe kuzeye bakıyorsa güneş binanın arkasında kalır: binadan önce çizilir
            if (Yon == 2) GunesCiz(g, new PointF(bx + bw * 0.5f + derin, by - derin * 0.5f - 10), true);

            // Yan cephe (derinlik)
            var yan = new[] { new PointF(bx + bw, by), new PointF(bx + bw + derin, by - derin * 0.5f), new PointF(bx + bw + derin, zeminY - derin * 0.5f), new PointF(bx + bw, zeminY) };
            using (var yb = new SolidBrush(Doku.Koyu(Doku.AnaRenk(Yalitim.Id), 0.78))) g.FillPolygon(yb, yan);
            // Çatı üst yüzü
            var cati = new[] { new PointF(bx, by), new PointF(bx + derin, by - derin * 0.5f), new PointF(bx + bw + derin, by - derin * 0.5f), new PointF(bx + bw, by) };
            using (var cb = new SolidBrush(Color.FromArgb(112, 118, 116))) g.FillPolygon(cb, cati);

            // Ön cephe: alt katman duvar, üstünde yalıtım; sol altta kesit (cutaway)
            var on = new RectangleF(bx, by, bw, bh);
            Doku.Ciz(g, on, Duvar.Id, 0.7f);
            using (var yol = new GraphicsPath())
            {
                yol.AddRectangle(on);
                using var kesik = new GraphicsPath();
                kesik.AddPolygon(new[] { new PointF(bx, zeminY - bh * 0.42f), new PointF(bx + bw * 0.30f, zeminY), new PointF(bx, zeminY) });
                using var bolge = new Region(yol);
                bolge.Exclude(kesik);
                var d = g.Save();
                g.SetClip(bolge, CombineMode.Replace);
                Doku.Ciz(g, on, Yalitim.Id, 0.7f);
                g.Restore(d);
                using var kesikKenar = new Pen(Color.FromArgb(150, 60, 60, 60), 1.5f) { DashStyle = DashStyle.Dash };
                g.DrawLine(kesikKenar, bx, zeminY - bh * 0.42f, bx + bw * 0.30f, zeminY);
            }
            using (var kenar = new Pen(Color.FromArgb(70, 76, 74), 1.5f)) g.DrawRectangle(kenar, bx, by, bw, bh);
            using (var parapet = new SolidBrush(Color.FromArgb(90, 96, 94))) g.FillRectangle(parapet, bx - 2, by - 5, bw + 4, 6);

            // Kat çizgisi
            using (var katP = new Pen(Color.FromArgb(90, 70, 76, 74), 1f))
                for (int k = 1; k < kat; k++) { float yk = zeminY - (float)(k * KatYuksekligi * s); g.DrawLine(katP, bx, yk, bx + bw, yk); }

            // Pencereler
            int adet = CamOrani switch { <= 0 => 0, <= 0.1 => 2, <= 0.25 => 3, _ => 4 };
            float katH = (float)(KatYuksekligi * s);
            float pw = bw * (float)(0.07 + CamOrani * 0.42), ph = katH * 0.5f;
            for (int k = 0; k < kat && adet > 0; k++)
            {
                float y = zeminY - (k + 1) * katH + katH * 0.22f;
                for (int i = 0; i < adet; i++)
                {
                    float x = bx + bw * (i + 1) / (adet + 1) - pw / 2;
                    if (k == 0 && x < bx + bw * 0.30f) x = Math.Max(x, bx + bw * 0.30f); // kesitin üstüne denk gelmesin
                    PencereCiz(g, new RectangleF(x, y, pw, ph));
                }
            }

            // Isı kaybı okları: her yapı elemanından kendi U değerine göre (kalınlık ve renk koyuluğu U ile artar)
            //  · ön ve yan cephe duvarı → duvar U   · çatı → çatı yalıtımıyla birlikte çatı U
            //  · pencereler: camın U değeri arttıkça camın üzerindeki sıcak (turuncu) ton koyulaşır
            float yanX = bx + bw, katYuk = katH;
            for (int k = 0; k < kat; k++)
            {
                float y = zeminY - (k + 0.55f) * katYuk;
                IsiOku(g, new PointF(bx - 4, y), new PointF(bx - 60, y + 2), DuvarU);                                   // ön cephe, sol kenar
                IsiOku(g, new PointF(yanX + derin * 0.55f, y - derin * 0.28f), new PointF(yanX + derin * 0.55f + 56, y - derin * 0.28f - 4), DuvarU); // yan cephe
            }
            for (int i = 1; i <= 2; i++)
            {
                float x = bx + derin * 0.5f + (bw * i / 3f);
                IsiOku(g, new PointF(x, by - derin * 0.25f), new PointF(x + 6, by - derin * 0.25f - 34), CatiU);        // çatı (yalıtımlı çatı U)
            }
            using (var f = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.FromArgb(180, 84, 26)))
            {
                float ly = Math.Max(60, by - 52);
                g.DrawString("ısı kaybı", f, b, bx - 80, ly);
            }

            // Pusula: plan görünümü, ön cephe izleyiciye (aşağıya) bakar; kuzey oku buna göre döner
            float donus = Yon switch { 2 => 180f, 3 => 90f, 5 => 270f, _ => 0f };
            PusulaCiz(g, new PointF(46, 50), donus);
            using (var f = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.FromArgb(29, 35, 33)))
                g.DrawString("Ön cephe: " + YonAdi[Math.Max(2, Math.Min(5, Yon))], f, b, 86, 36);
            // Güneş güneydedir: ön cephe güneyse önde, doğuysa solda, batıysa sağda, kuzeyse binanın arkasında
            float gy = Math.Max(30, by - derin - 34);
            if (Yon == 4) GunesCiz(g, new PointF(bx + bw * 0.5f, gy), false);
            else if (Yon == 3) GunesCiz(g, new PointF(Math.Max(30, bx - 70), gy + 10), false);
            else if (Yon == 5) GunesCiz(g, new PointF(Math.Min(W - 290, bx + bw + derin + 60), gy + 10), false);

            // Isı kaybı katsayısı rozeti
            double azalma = HBaz > 0 ? (1 - HYeni / HBaz) * 100 : 0;
            string rozetMetni = $"Isı kaybı, yalıtımsız binaya göre %{azalma.ToString("0", tr)} daha az";
            using (var f = new Font("Segoe UI", 9f))
            using (var b = new SolidBrush(Color.FromArgb(29, 35, 33)))
            {
                var rozet = new RectangleF(12, zeminY + 8, g.MeasureString(rozetMetni, f).Width + 14, 30);
                using (var rb = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) g.FillRectangle(rb, rozet);
                g.DrawString(rozetMetni, f, b, rozet.X + 6, rozet.Y + 6);
            }

            // Ölçekli duvar kesiti
            KesitCiz(g, new RectangleF(W - 262, 16, 246, 182));
        }

        void PencereCiz(Graphics g, RectangleF r)
        {
            using var cerceve = new SolidBrush(Color.FromArgb(246, 246, 244));
            g.FillRectangle(cerceve, r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6);
            var ic = Cam!.Id switch { "P2" => Color.FromArgb(150, 206, 192), "P3" => Color.FromArgb(140, 196, 214), _ => Color.FromArgb(168, 210, 232) };
            using (var cam = new LinearGradientBrush(r, Color.FromArgb(232, 244, 250), ic, 60f)) g.FillRectangle(cam, r);
            int levha = Cam.Id == "P3" ? 3 : 2;
            using var p = new Pen(Color.FromArgb(120, 255, 255, 255), 1f);
            for (int i = 1; i < levha; i++) g.DrawRectangle(p, r.X + i * 2.5f, r.Y + i * 2.5f, r.Width - i * 5, r.Height - i * 5);
            // Isı kaybı tonu: U = 2,8 (standart çift cam) belirgin, U = 0,7 (üçlü cam) çok hafif
            int ton = (int)Math.Min(150, Cam.U / 2.8 * 150);
            using (var sicak = new SolidBrush(Color.FromArgb(ton, 226, 110, 60))) g.FillRectangle(sicak, r);
            using var orta = new Pen(Color.FromArgb(246, 246, 244), 2f);
            g.DrawLine(orta, r.X + r.Width / 2, r.Y, r.X + r.Width / 2, r.Bottom);
        }

        void KesitCiz(Graphics g, RectangleF k)
        {
            var tr = Ortak.TR;
            using (var arka = new SolidBrush(Color.FromArgb(242, 255, 255, 255))) g.FillRectangle(arka, k);
            using (var kenar = new Pen(Color.FromArgb(200, 205, 200))) g.DrawRectangle(kenar, k.X, k.Y, k.Width, k.Height);
            using var bas = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var kucuk = new Font("Segoe UI", 8f);
            using var siyah = new SolidBrush(Color.FromArgb(29, 35, 33));
            using var gri = new SolidBrush(Color.FromArgb(93, 103, 99));
            g.DrawString("Duvar kesiti (ölçekli)", bas, siyah, k.X + 8, k.Y + 6);

            float pxCm = 3.6f;
            float dw = (float)(Duvar!.Kalinlik * 100) * pxCm, iw = (float)(Yalitim!.Kalinlik * 100) * pxCm, sv = 1.5f * pxCm;
            float x = k.X + (k.Width - (dw + iw + 2 * sv)) / 2, y = k.Y + 30, h = 92;
            using (var siva = new SolidBrush(Color.FromArgb(236, 232, 224))) { g.FillRectangle(siva, x, y, sv, h); g.FillRectangle(siva, x + sv + dw + iw, y, sv, h); }
            Doku.Ciz(g, new RectangleF(x + sv, y, dw, h), Duvar.Id, 0.6f);
            Doku.Ciz(g, new RectangleF(x + sv + dw, y, iw, h), Yalitim.Id, 0.6f);
            using (var cz = new Pen(Color.FromArgb(70, 76, 74))) { g.DrawRectangle(cz, x, y, dw + iw + 2 * sv, h); g.DrawLine(cz, x + sv + dw, y, x + sv + dw, y + h); }
            g.DrawString("İç", kucuk, gri, x - 20, y + h / 2 - 7);
            g.DrawString("Dış", kucuk, gri, x + dw + iw + 2 * sv + 4, y + h / 2 - 7);
            Doku.TextOrta(g, $"{(Duvar.Kalinlik * 100).ToString("0.#", tr)} cm", kucuk, Color.FromArgb(29, 35, 33), new RectangleF(x + sv, y + h + 1, dw, 16));
            g.DrawString($"+ {(Yalitim.Kalinlik * 100).ToString("0", tr)} cm", kucuk, siyah, x + sv + dw + iw + sv + 2, y + h + 1);

            bool uygun = DuvarU <= SinirU;
            var etiket = new RectangleF(k.X + 8, k.Bottom - 26, k.Width - 16, 20);
            using (var eb = new SolidBrush(uygun ? Color.FromArgb(226, 239, 231) : Color.FromArgb(248, 232, 220))) g.FillRectangle(eb, etiket);
            Doku.TextOrta(g, $"U = {DuvarU.ToString("0.00", tr)} · sınır {SinirU.ToString("0.00", tr)} · {(uygun ? "uygun" : "uygun değil")}",
                kucuk, uygun ? Color.FromArgb(31, 111, 74) : Color.FromArgb(180, 84, 26), etiket);
        }

        /// <summary>Pusula: "donus" derece saat yönünde döndürülmüş kuzey oku ve dört yön harfi; alttaki işaret ön cepheyi gösterir.</summary>
        static void PusulaCiz(Graphics g, PointF m, float donus)
        {
            using var dis = new Pen(Color.FromArgb(120, 70, 76, 74), 1.5f);
            using var ic = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
            g.FillEllipse(ic, m.X - 22, m.Y - 22, 44, 44);
            g.DrawEllipse(dis, m.X - 22, m.Y - 22, 44, 44);
            using var kirmizi = new SolidBrush(Color.FromArgb(198, 42, 42));
            using var gri = new SolidBrush(Color.FromArgb(150, 150, 150));
            using var f = new Font("Segoe UI", 7f, FontStyle.Bold);
            PointF D(float aci, float r) { double a = (aci - 90) * Math.PI / 180; return new PointF(m.X + (float)Math.Cos(a) * r, m.Y + (float)Math.Sin(a) * r); }
            g.FillPolygon(kirmizi, new[] { D(donus, 15), D(donus + 90, 4.5f), D(donus - 90, 4.5f) });
            g.FillPolygon(gri, new[] { D(donus + 180, 15), D(donus + 90, 4.5f), D(donus - 90, 4.5f) });
            string[] harf = { "K", "D", "G", "B" };
            for (int i = 0; i < 4; i++)
            {
                var p = D(donus + i * 90, 31);
                Doku.TextOrta(g, harf[i], f, i == 0 ? Color.FromArgb(198, 42, 42) : Color.FromArgb(93, 103, 99), new RectangleF(p.X - 8, p.Y - 7, 16, 14));
            }
            // ön cephe işareti (izleyici tarafı)
            using var on = new Pen(Color.FromArgb(31, 111, 74), 3f);
            g.DrawArc(on, m.X - 22, m.Y - 22, 44, 44, 60, 60);
        }

        /// <summary>Dalgalı ısı kaybı oku; U büyüdükçe kalınlaşır ve koyulaşır.</summary>
        static void IsiOku(Graphics g, PointF bas, PointF son, double u, bool kisa = false)
        {
            float guc = (float)Math.Min(1.0, u / 2.8);
            using var ok = new Pen(Color.FromArgb((int)(70 + 170 * guc), 214, 84, 42), (kisa ? 1.2f : 1.6f) + (kisa ? 5f : 8f) * guc);
            ok.CustomEndCap = new AdjustableArrowCap(2.2f, 2.2f);
            float dx = son.X - bas.X, dy = son.Y - bas.Y, nx = -dy * 0.12f, ny = dx * 0.12f;
            var dalga = new[] { bas, new PointF(bas.X + dx * 0.33f + nx, bas.Y + dy * 0.33f + ny), new PointF(bas.X + dx * 0.66f - nx, bas.Y + dy * 0.66f - ny), son };
            g.DrawCurve(ok, dalga, 0.5f);
        }

        static void GunesCiz(Graphics g, PointF m, bool arkada)
        {
            if (arkada) { using var hale = new SolidBrush(Color.FromArgb(70, 246, 196, 60)); g.FillEllipse(hale, m.X - 20, m.Y - 20, 40, 40); }
            using var isin = new Pen(Color.FromArgb(200, 242, 180, 40), 2f);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                g.DrawLine(isin, m.X + (float)Math.Cos(a) * 15, m.Y + (float)Math.Sin(a) * 15, m.X + (float)Math.Cos(a) * 23, m.Y + (float)Math.Sin(a) * 23);
            }
            using var b = new SolidBrush(Color.FromArgb(246, 196, 60));
            g.FillEllipse(b, m.X - 11, m.Y - 11, 22, 22);
        }
    }
}
