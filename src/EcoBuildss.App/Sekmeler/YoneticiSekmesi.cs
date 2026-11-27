namespace EcoBuildss.UI
{
    /// <summary>Firma yöneticisi için: firma bilgileri, gerçek veri yükleme ve öğrenen modellerin doğrulaması.</summary>
    public sealed class YoneticiSekmesi : UserControl
    {
        public YoneticiSekmesi(Durum d)
        {
            var sekme = new TabControl { Dock = DockStyle.Fill, Font = Ui.Yazi };
            void Ekle(string ad, Control c) { var t = new TabPage(ad) { BackColor = Ui.Zemin }; c.Dock = DockStyle.Fill; t.Controls.Add(c); sekme.TabPages.Add(t); }
            Ekle("Firma bilgileri", Firma(d));
            Ekle("Veri yükleme", new VeriSekmesi(d));
            Ekle("Modeller ve doğrulama", new ModellerSekmesi(d));
            Controls.Add(sekme);
            Controls.Add(new Label { Text = "Veri ve modeller", Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI Semibold", 14f), ForeColor = Ui.Koyu, Padding = new Padding(12, 8, 0, 0) });
        }

        static Control Firma(Durum d)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20), BackColor = Color.White };
            var ad = new TextBox { Width = 360, Text = d.Db?.Ayar("firma_adi") ?? "" };
            var tel = new TextBox { Width = 360, Text = d.Db?.Ayar("firma_telefon") ?? "" };
            p.Controls.Add(Ui.Etiket("Firma adı")); p.Controls.Add(ad);
            p.Controls.Add(Ui.Etiket("Telefon / e-posta")); p.Controls.Add(tel);
            p.Controls.Add(Ui.Aciklama("Toplantı sayfasının ve tekliflerin başında görünür.", 360));
            p.Controls.Add(Ui.Dugme("Kaydet", (_, __) =>
            {
                if (d.Db == null) { Ui.Hata(p, "Veritabanı açılamadı: " + d.DbHatasi); return; }
                d.Db.AyarYaz("firma_adi", ad.Text.Trim()); d.Db.AyarYaz("firma_telefon", tel.Text.Trim()); d.Bildir("Firma bilgileri kaydedildi.");
            }, true));
            p.Controls.Add(new Label { Text = "\nVERİLER NEREDEN GELİYOR?", AutoSize = true, Font = Ui.Kalin, ForeColor = Ui.Yesil, Margin = new Padding(0, 20, 0, 4) });
            p.Controls.Add(Ui.Aciklama(
                "İklim: MGM il derece-günleri · Yönetmelik: TS 825:2024 U sınırları, Binaların Yangından Korunması Hakkında Yönetmelik md. 27 · " +
                "Malzeme fiyatları: uygulanmış sistem fiyatları (satıcı ve piyasa listeleri, her satırda kaynak ve tarih) · Doğal gaz: BOTAŞ tarifesi · Elektrik: EPDK · " +
                "Faiz ve enflasyon: TCMB, TÜİK · Karbon: ICE veri tabanı, IPCC. Kaynaklar veri/referans klasöründeki dosyalarda satır satır yazılıdır.", 760));
            return p;
        }
    }
}
