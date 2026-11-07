using System.Drawing;
using System.Drawing.Drawing2D;

namespace EcoBuildss.UI
{
    /// <summary>Malzemelerin görsel dokuları (GDI+ ile çizilir, resim dosyası gerekmez).</summary>
    public static class Doku
    {
        /// <summary>Aynı malzemenin farklı kalınlıkları aynı dokuyu kullanır.</summary>
        /// <summary>Doku anahtarı: Y1 = EPS, Y2 = XPS, Y3 = taş yünü, Y4 = cam yünü (malzemeler.csv kimliklerinden).</summary>
        static string DokuId(string id) => id switch
        {
            "Y1" or "Y2" or "Y3" => "Y1",   // EPS mantolama 5 / 8 / 10 cm
            "Y4" or "Y5" or "Y6" => "Y3",   // taş yünü mantolama 5 / 8 / 10 cm
            "C1" => "Y4",                   // cam yünü çatı şiltesi
            "Z1" => "Y2",                   // XPS zemin levhası
            _ => id
        };

        public static Color AnaRenk(string id) => DokuId(id) switch
        {
            "Y1" => Color.FromArgb(244, 244, 238),   // EPS: beyaz boncuklu köpük
            "Y2" => Color.FromArgb(160, 200, 230),   // XPS: düz renkli sert köpük
            "Y3" => Color.FromArgb(188, 160, 104),   // taş yünü: kahverengimsi lif
            "Y4" => Color.FromArgb(238, 208, 92),    // cam yünü: sarı lif
            "P1" => Color.FromArgb(196, 224, 238),
            "P2" => Color.FromArgb(188, 226, 214),
            "P3" => Color.FromArgb(178, 216, 230),
            "D1" => Color.FromArgb(176, 78, 50),     // tuğla
            "D2" => Color.FromArgb(230, 230, 222),   // gazbeton
            "D3" => Color.FromArgb(150, 150, 144),   // bims
            _ => Color.LightGray
        };

        public static Color Koyu(Color c, double k) =>
            Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));

        /// <summary>Malzemenin yüzey dokusunu verilen dikdörtgene çizer. olcek: 1 = kart, &lt;1 = bina cephesi.</summary>
        public static void Ciz(Graphics g, RectangleF r, string id, float olcek = 1f)
        {
            if (r.Width <= 1 || r.Height <= 1) return;
            var durum = g.Save();
            g.SetClip(r, CombineMode.Intersect); // mevcut kırpmayı koru (bina kesiti için)
            var ana = AnaRenk(id);
            using (var b = new SolidBrush(ana)) g.FillRectangle(b, r);
            var rnd = new Random(id.GetHashCode() & 0xFFFF);

            switch (DokuId(id))
            {
                case "Y1": // EPS boncukları
                {
                    using var p = new Pen(Color.FromArgb(205, 205, 196), 1f);
                    float s = 7f * olcek;
                    for (float y = r.Top; y < r.Bottom + s; y += s * 0.85f)
                    for (float x = r.Left - (((int)((y - r.Top) / s) % 2) * s / 2); x < r.Right + s; x += s)
                        g.DrawEllipse(p, x, y, s * 0.9f, s * 0.9f);
                    break;
                }
                case "Y2": // XPS ince yatay katmanlar
                {
                    using var p = new Pen(Koyu(ana, 0.9), 1f);
                    for (float y = r.Top; y < r.Bottom; y += 5f * olcek) g.DrawLine(p, r.Left, y, r.Right, y);
                    break;
                }
                case "Y3":
                case "Y4": // lifli dokular
                {
                    using var p = new Pen(Koyu(ana, DokuId(id) == "Y3" ? 0.72 : 0.82), 1f);
                    float adim = 4f * olcek;
                    for (float y = r.Top; y < r.Bottom + adim; y += adim)
                    {
                        var pts = new List<PointF>();
                        float faz = (float)rnd.NextDouble() * 6f;
                        for (float x = r.Left; x <= r.Right + 6; x += 6)
                            pts.Add(new PointF(x, y + (float)Math.Sin((x / 9f) + faz) * 1.6f * olcek));
                        if (pts.Count > 1) g.DrawCurve(p, pts.ToArray(), 0.4f);
                    }
                    break;
                }
                case "D1": // tuğla örgüsü + delikler
                {
                    float h = 12f * olcek, w = 26f * olcek;
                    using var harc = new Pen(Color.FromArgb(222, 205, 186), Math.Max(1f, 2f * olcek));
                    using var delik = new SolidBrush(Koyu(ana, 0.7));
                    int sira = 0;
                    for (float y = r.Top; y < r.Bottom; y += h, sira++)
                    {
                        g.DrawLine(harc, r.Left, y, r.Right, y);
                        float bas = r.Left - (sira % 2) * w / 2;
                        for (float x = bas; x < r.Right; x += w)
                        {
                            g.DrawLine(harc, x, y, x, y + h);
                            if (olcek >= 0.8f)
                                for (int k = 0; k < 3; k++)
                                    g.FillRectangle(delik, x + w * (0.2f + k * 0.25f), y + h * 0.35f, w * 0.12f, h * 0.3f);
                        }
                    }
                    break;
                }
                case "D2": // gazbeton büyük bloklar + gözenekler
                {
                    float h = 20f * olcek, w = 48f * olcek;
                    using var derz = new Pen(Color.FromArgb(205, 205, 196), 1f);
                    using var goz = new SolidBrush(Color.FromArgb(200, 200, 190));
                    int sira = 0;
                    for (float y = r.Top; y < r.Bottom; y += h, sira++)
                    {
                        g.DrawLine(derz, r.Left, y, r.Right, y);
                        for (float x = r.Left - (sira % 2) * w / 2; x < r.Right; x += w) g.DrawLine(derz, x, y, x, y + h);
                    }
                    int n = (int)(r.Width * r.Height / (60 / Math.Max(olcek, 0.3f)));
                    for (int i = 0; i < Math.Min(n, 4000); i++)
                        g.FillEllipse(goz, r.Left + (float)rnd.NextDouble() * r.Width, r.Top + (float)rnd.NextDouble() * r.Height, 1.6f, 1.6f);
                    break;
                }
                case "D3": // bims blok + benekler
                {
                    float h = 18f * olcek, w = 38f * olcek;
                    using var derz = new Pen(Color.FromArgb(185, 185, 178), Math.Max(1f, 1.5f * olcek));
                    int sira = 0;
                    for (float y = r.Top; y < r.Bottom; y += h, sira++)
                    {
                        g.DrawLine(derz, r.Left, y, r.Right, y);
                        for (float x = r.Left - (sira % 2) * w / 2; x < r.Right; x += w) g.DrawLine(derz, x, y, x, y + h);
                    }
                    using var ben1 = new SolidBrush(Color.FromArgb(118, 118, 112));
                    using var ben2 = new SolidBrush(Color.FromArgb(178, 176, 168));
                    int n = (int)(r.Width * r.Height / 25);
                    for (int i = 0; i < Math.Min(n, 5000); i++)
                        g.FillRectangle(i % 2 == 0 ? ben1 : ben2, r.Left + (float)rnd.NextDouble() * r.Width, r.Top + (float)rnd.NextDouble() * r.Height, 1.5f, 1.5f);
                    break;
                }
                case "P1":
                case "P2":
                case "P3": // cam ünitesi kesiti
                    CamKesiti(g, r, id);
                    break;
            }
            g.Restore(durum);
        }

        /// <summary>Cam ünitesini yandan kesit olarak çizer: cam levhalar, boşluk, Low-E kaplama, argon.</summary>
        static void CamKesiti(Graphics g, RectangleF r, string id)
        {
            using (var zemin = new LinearGradientBrush(r, Color.FromArgb(236, 244, 248), Color.FromArgb(210, 228, 238), 90f))
                g.FillRectangle(zemin, r);
            int levha = id == "P3" ? 3 : 2;
            bool lowE = id != "P1";
            float camW = Math.Max(4f, r.Width * 0.05f);
            float bosluk = Math.Min(r.Width * 0.18f, (r.Width - levha * camW) / (levha + 1));
            float toplam = levha * camW + (levha - 1) * bosluk;
            float x0 = r.Left + (r.Width - toplam) / 2;
            using var camB = new SolidBrush(Color.FromArgb(150, 196, 222));
            using var camKenar = new Pen(Color.FromArgb(90, 140, 170), 1f);
            using var argon = new SolidBrush(Color.FromArgb(70, 160, 120, 210));
            using var kaplama = new Pen(Color.FromArgb(40, 150, 90), 2f);
            using var yazi = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            for (int i = 0; i < levha; i++)
            {
                float x = x0 + i * (camW + bosluk);
                if (i < levha - 1)
                {
                    var bos = new RectangleF(x + camW, r.Top + 6, bosluk, r.Height - 12);
                    if (lowE) { g.FillRectangle(argon, bos); TextOrta(g, "Ar", yazi, Color.FromArgb(90, 70, 140), bos); }
                    else TextOrta(g, "hava", yazi, Color.FromArgb(110, 130, 140), bos);
                }
                g.FillRectangle(camB, x, r.Top + 4, camW, r.Height - 8);
                g.DrawRectangle(camKenar, x, r.Top + 4, camW, r.Height - 8);
                if (lowE && i == 0) g.DrawLine(kaplama, x + camW + 1, r.Top + 6, x + camW + 1, r.Bottom - 6);
            }
        }

        public static void TextOrta(Graphics g, string s, Font f, Color c, RectangleF r)
        {
            using var b = new SolidBrush(c);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(s, f, b, r, sf);
        }
    }
}
