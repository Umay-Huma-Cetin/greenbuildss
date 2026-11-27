namespace EcoBuildss.UI
{
    /// <summary>Açılış ekranı: üç iş, üç büyük düğme. Hangi işe ne zaman başvurulacağı tek cümleyle yazılı.</summary>
    public sealed class AnaSayfa : UserControl
    {
        public event Action<int>? Git;
        readonly Durum d;
        readonly Label ozet = new() { AutoSize = true, Font = Ui.Yazi, ForeColor = Ui.Soluk, Margin = new Padding(0, 4, 0, 18) };

        public AnaSayfa(Durum durum)
        {
            d = durum;
            BackColor = Ui.Zemin;
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(48, 36, 24, 24) };
            p.Controls.Add(new Label { Text = "Bugün ne yapıyoruz?", AutoSize = true, Font = new Font("Segoe UI Semibold", 22f), ForeColor = Ui.Koyu });
            p.Controls.Add(ozet);
            var satir = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            satir.Controls.Add(Kart("Apartman mantolaması", "Daire başı tutar, taksit, fatura düşüşü", 1));
            satir.Controls.Add(Kart("Yeni bina", "Duvar, yalıtım, cam ve teklif", 2));
            satir.Controls.Add(Kart("Tekliflerim", "Bekleyen ve sonuçlanan teklifler", 3));
            p.Controls.Add(satir);
            Controls.Add(p);
            VisibleChanged += (_, __) => { if (Visible) Guncelle(); };
            d.TekliflerDegisti += Guncelle;
        }

        Control Kart(string baslik, string aciklama, int hedef)
        {
            var k = new Panel { Width = 300, Height = 150, BackColor = Color.White, Margin = new Padding(0, 0, 18, 18), Cursor = Cursors.Hand, Padding = new Padding(20, 18, 20, 14) };
            var l1 = new Label { Text = baslik, Dock = DockStyle.Top, Height = 36, Font = new Font("Segoe UI Semibold", 15f), ForeColor = Ui.Koyu };
            var l3 = new Label { Text = aciklama, Dock = DockStyle.Fill, Font = Ui.Yazi, ForeColor = Ui.Soluk };
            var l4 = new Label { Text = "Aç  →", Dock = DockStyle.Bottom, Height = 24, Font = Ui.Kalin, ForeColor = Ui.Yesil };
            k.Controls.Add(l3); k.Controls.Add(l4); k.Controls.Add(l1);
            k.Paint += (_, e) => { using var pen = new Pen(Ui.Kenar); e.Graphics.DrawRectangle(pen, 0, 0, k.Width - 1, k.Height - 1); using var b = new SolidBrush(Ui.Yesil); e.Graphics.FillRectangle(b, 0, 0, 5, k.Height); };
            foreach (Control c in new Control[] { k, l1, l3, l4 }) c.Click += (_, __) => Git?.Invoke(hedef);
            return k;
        }

        void Guncelle()
        {
            if (d.Db == null) { ozet.Text = "Veritabanı açılamadı."; return; }
            var l = d.Db.Teklifler();
            int bek = l.Count(t => t.Sonuc == "Bekliyor"), kabul = l.Count(t => t.Sonuc == "Kabul"), red = l.Count(t => t.Sonuc == "Red");
            string firma = d.Db.Ayar("firma_adi");
            ozet.Text = (firma.Length > 0 ? firma + " · " : "") + (l.Count == 0 ? "Henüz teklif yok."
                : $"{bek} teklif cevap bekliyor" + (kabul + red > 0 ? $" · kazanma oranı %{100.0 * kabul / (kabul + red):0}" : ""));
        }
    }
}
