using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using EcoBuildss.Core;

namespace EcoBuildss.UI
{
    /// <summary>
    /// İl sınırları (Natural Earth 1:10m yönetim birimleri, kamu malı; Veri/il_sinirlari.csv, sadeleştirilmiş) üzerinde
    /// her ili bir değere göre renklendiren harita. Fare ilin üstüne gelince ad ve değer gösterilir.
    /// </summary>
    public sealed class HaritaPanel : Control
    {
        public sealed class Bolum { public string Il = ""; public List<PointF[]> Parcalar = new(); }
        public List<Bolum> Iller { get; } = new();
        /// <summary>İl → (renk, açıklama). Sözlükte olmayan il "veri yok" (gri) çizilir.</summary>
        public Dictionary<string, (Color renk, string metin)> Degerler { get; } = new();
        public List<(Color renk, string ad)> Lejant { get; } = new();
        public string Baslik = "";
        public string? Vurgu;                    // seçili il (kalın çerçeve)
        public event EventHandler<string>? IlTiklandi;
        readonly ToolTip ipucu = new() { InitialDelay = 100, ReshowDelay = 50 };
        string? sonIl;
        RectangleF kutu; float olcek; float x0, y0; const float CosEnlem = 0.77f;   // 39,5° enleminde boylam daralması

        public HaritaPanel()
        {
            DoubleBuffered = true; ResizeRedraw = true; BackColor = Color.White;
            MouseMove += (_, e) => { var il = IlBul(e.Location); if (il == sonIl) return; sonIl = il; ipucu.SetToolTip(this, il == null ? "" : $"{il}: {(Degerler.TryGetValue(il, out var d) ? d.metin : "veri yok")}"); };
            MouseClick += (_, e) => { if (IlBul(e.Location) is string il) IlTiklandi?.Invoke(this, il); };
        }

        public static List<Bolum> Oku(string yol)
        {
            var l = new Dictionary<string, Bolum>();
            if (!File.Exists(yol)) return new List<Bolum>();
            var rows = VeriDeposu.CsvOku(yol);
            foreach (var r in rows.Skip(1))
            {
                if (!l.TryGetValue(r[0], out var b)) l[r[0]] = b = new Bolum { Il = r[0] };
                b.Parcalar.Add(r[2].Split(';').Select(p => { var a = p.Split(' '); return new PointF(float.Parse(a[0], CultureInfo.InvariantCulture), float.Parse(a[1], CultureInfo.InvariantCulture)); }).ToArray());
            }
            return l.Values.ToList();
        }

        PointF Izdus(PointF p) => new(x0 + (p.X - kutu.Left) * CosEnlem * olcek, y0 + (kutu.Bottom - p.Y) * olcek);

        void OlcekKur()
        {
            var tum = Iller.SelectMany(b => b.Parcalar.SelectMany(x => x)).ToList();
            if (tum.Count == 0) return;
            float minX = tum.Min(p => p.X), maxX = tum.Max(p => p.X), minY = tum.Min(p => p.Y), maxY = tum.Max(p => p.Y);
            kutu = RectangleF.FromLTRB(minX, minY, maxX, maxY);
            float w = ClientSize.Width - 24, h = ClientSize.Height - 80;
            olcek = Math.Min(w / (kutu.Width * CosEnlem), h / kutu.Height);
            x0 = 12 + (w - kutu.Width * CosEnlem * olcek) / 2; y0 = 34;
        }

        string? IlBul(Point p)
        {
            OlcekKur();
            foreach (var b in Iller)
                foreach (var pr in b.Parcalar)
                {
                    using var gp = new GraphicsPath(); gp.AddPolygon(pr.Select(Izdus).ToArray());
                    if (gp.IsVisible(p)) return b.Il;
                }
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Cizim.Hazirla(g);
            using var fb = new Font("Segoe UI Semibold", 11f); using var f = new Font("Segoe UI", 8.5f);
            using var metin = new SolidBrush(Cizim.Metin); using var soluk = new SolidBrush(Cizim.Soluk);
            g.DrawString(Baslik, fb, metin, 12, 6);
            if (Iller.Count == 0) { g.DrawString("İl sınırları dosyası (Veri/il_sinirlari.csv) bulunamadı.", f, soluk, 12, 40); return; }
            OlcekKur();
            using var kenar = new Pen(Color.White, 1f); using var vurgu = new Pen(Cizim.Metin, 2.2f);
            using var bos = new HatchBrush(HatchStyle.ForwardDiagonal, Color.FromArgb(200, 204, 200), Color.FromArgb(240, 242, 239));
            foreach (var b in Iller)
            {
                var renk = Degerler.TryGetValue(b.Il, out var d) ? d.renk : (Color?)null;
                using var fr = renk is Color c ? new SolidBrush(c) : null;
                foreach (var pr in b.Parcalar)
                {
                    var pts = pr.Select(Izdus).ToArray();
                    g.FillPolygon(fr ?? (Brush)bos, pts); g.DrawPolygon(kenar, pts);
                }
            }
            if (Vurgu != null && Iller.FirstOrDefault(b => b.Il == Vurgu) is Bolum vb)
                foreach (var pr in vb.Parcalar) g.DrawPolygon(vurgu, pr.Select(Izdus).ToArray());
            // lejant
            float lx = 12, ly = ClientSize.Height - 40;
            bool eksik = Iller.Any(b => !Degerler.ContainsKey(b.Il));      // gri il yoksa "Veri yok" gösterilmez
            foreach (var (renk, ad) in eksik ? Lejant.Concat(new[] { (Color.FromArgb(220, 222, 218), "Veri yok") }) : Lejant)
            {
                var sz = g.MeasureString(ad, f);
                if (lx + 18 + sz.Width > ClientSize.Width - 8) { lx = 12; ly += 18; }
                using var br = new SolidBrush(renk); g.FillRectangle(br, lx, ly + 3, 12, 12);
                g.DrawString(ad, f, metin, lx + 16, ly + 1); lx += 16 + sz.Width + 14;
            }
            g.DrawString("İl sınırları: Natural Earth (kamu malı)", f, soluk, ClientSize.Width - 230, 10);
        }
    }
}
