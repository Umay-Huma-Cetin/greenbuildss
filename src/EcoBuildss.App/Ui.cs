using System.Globalization;

namespace EcoBuildss.UI
{
    /// <summary>Renkler, yazı tipleri ve sık kullanılan denetimlerin fabrikası (tutarlı görünüm için tek yer).</summary>
    public static class Ui
    {
        public static readonly Color Koyu = Color.FromArgb(19, 38, 31), Yesil = Color.FromArgb(35, 122, 80), AcikYesil = Color.FromArgb(227, 240, 232),
            Amber = Color.FromArgb(227, 161, 59), Zemin = Color.FromArgb(245, 246, 241), Kenar = Color.FromArgb(220, 227, 222), Metin = Color.FromArgb(29, 35, 33),
            Soluk = Color.FromArgb(108, 118, 114), Kirmizi = Color.FromArgb(178, 58, 48), AcikKirmizi = Color.FromArgb(250, 232, 229), AcikAmber = Color.FromArgb(252, 241, 222);
        public static readonly Font Yazi = new("Segoe UI", 9.5f), Kalin = new("Segoe UI", 9.5f, FontStyle.Bold), Kucuk = new("Segoe UI", 8.5f),
            Baslik = new("Segoe UI Semibold", 12f), BuyukSayi = new("Segoe UI Semibold", 16f);
        public static readonly CultureInfo TR = Ortak.TR;

        public static string Tl(double v) => v.ToString("N0", TR) + " TL";
        public static string Sayi(double v, string b = "N0") => v.ToString(b, TR);
        public static string Yuzde(double v, string b = "0.0") => "%" + (100 * v).ToString(b, TR);

        public static Label Etiket(string metin, Font? f = null, Color? renk = null) =>
            new() { Text = metin, AutoSize = true, Font = f ?? Yazi, ForeColor = renk ?? Metin, Margin = new Padding(0, 6, 0, 2) };

        public static Label Aciklama(string metin, int genislik) =>
            new() { Text = metin, AutoSize = true, MaximumSize = new Size(genislik, 0), Font = Kucuk, ForeColor = Soluk, Margin = new Padding(0, 2, 0, 6) };

        public static Button Dugme(string metin, EventHandler tikla, bool ana = false)
        {
            var b = new Button
            {
                Text = metin, AutoSize = true, FlatStyle = FlatStyle.Flat, Font = ana ? Kalin : Yazi, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 4, 8, 4),
                BackColor = ana ? Yesil : Color.White, ForeColor = ana ? Color.White : Metin, Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderColor = ana ? Yesil : Kenar;
            b.Click += tikla;
            return b;
        }

        public static DataGridView Izgara()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Font = Yazi, GridColor = Kenar,
                EnableHeadersVisualStyles = false, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            };
            g.ColumnHeadersDefaultCellStyle.BackColor = Zemin; g.ColumnHeadersDefaultCellStyle.Font = Kalin; g.ColumnHeadersDefaultCellStyle.ForeColor = Metin;
            g.DefaultCellStyle.SelectionBackColor = AcikYesil; g.DefaultCellStyle.SelectionForeColor = Metin;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 249);
            return g;
        }

        /// <summary>Başlıklı beyaz kart: içine Govde denetimi yerleştirilir.</summary>
        public static Panel Kart(string baslik, Control govde, string? alt = null)
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 8, 12, 10), Margin = new Padding(6) };
            govde.Dock = DockStyle.Fill;
            p.Controls.Add(govde);
            if (alt != null) p.Controls.Add(new Label { Text = alt, Dock = DockStyle.Top, Height = 22, Font = Kucuk, ForeColor = Soluk });
            p.Controls.Add(new Label { Text = baslik, Dock = DockStyle.Top, Height = 26, Font = Baslik, ForeColor = Koyu });
            p.Paint += (_, e) => { using var k = new Pen(Kenar); e.Graphics.DrawRectangle(k, 0, 0, p.Width - 1, p.Height - 1); };
            return p;
        }

        /// <summary>Durum rozeti: yeşil (uygun), amber (uyarı), kırmızı (güvenilmez).</summary>
        public static Label Rozet(string metin, int durum) => new()
        {
            Text = metin, AutoSize = true, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 4, 8, 4), Font = Kalin,
            BackColor = durum == 1 ? AcikYesil : durum == 2 ? AcikAmber : AcikKirmizi, ForeColor = durum == 1 ? Yesil : durum == 2 ? Color.FromArgb(140, 90, 20) : Kirmizi,
        };

        public static TableLayoutPanel Izgara2(int sutun, params float[] yuzde)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = sutun, BackColor = Zemin, Padding = new Padding(4) };
            for (int i = 0; i < sutun; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, i < yuzde.Length ? yuzde[i] : 100f / sutun));
            return t;
        }

        public static void Hata(IWin32Window? sahip, string mesaj) => MessageBox.Show(sahip, mesaj, "EcoBuildss", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
