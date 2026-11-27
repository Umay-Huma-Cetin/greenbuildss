using EcoBuildss.Core.Depo;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Ana pencere: solda gezinme çubuğu ve oturum bilgisi, sağda tek ekran.
    /// "Veri ve modeller" yalnız yöneticiye görünür. 15 dakika işlem yapılmazsa oturum kilitlenir.
    /// </summary>
    public sealed class AnaForm : Form, IMessageFilter
    {
        public static readonly TimeSpan ZamanAsimi = TimeSpan.FromMinutes(15);
        readonly Durum d;
        readonly Label durumYazisi = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Ui.Kucuk, ForeColor = Ui.Soluk, Padding = new Padding(10, 0, 0, 0) };
        readonly Label kullaniciEtiket = new() { Dock = DockStyle.Top, Height = 40, Font = Ui.Kalin, ForeColor = Color.White, Padding = new Padding(18, 4, 0, 0) };
        readonly Panel icerik = new() { Dock = DockStyle.Fill, BackColor = Ui.Zemin };
        readonly List<Button> menu = new();
        readonly List<Control> ekranlar = new();
        readonly TeklifSekmesi teklif;
        readonly ApartmanSekmesi apartman;
        readonly KayitlarSekmesi kayitlar;
        readonly System.Windows.Forms.Timer bekleme = new() { Interval = 30_000 };
        DateTime sonEtkinlik = DateTime.Now;
        int aktifEkran;

        public AnaForm(Durum durum)
        {
            d = durum;
            Text = "EcoBuildss";
            Font = Ui.Yazi; BackColor = Ui.Zemin; MinimumSize = new Size(1200, 760); Size = new Size(1440, 900); StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            d.Bildirim += m => durumYazisi.Text = $"{DateTime.Now:HH:mm}  ·  {m}";

            // ---- sol gezinme çubuğu ----
            var yan = new Panel { Dock = DockStyle.Left, Width = 214, BackColor = Ui.Koyu };
            var logo = new Label { Text = "EcoBuildss", Dock = DockStyle.Top, Height = 64, Font = new Font("Segoe UI Semibold", 17f), ForeColor = Color.White, Padding = new Padding(18, 16, 0, 0) };
            var liste = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Ui.Koyu, Padding = new Padding(0, 8, 0, 0) };
            string[] adlar = { "Ana sayfa", "Apartman mantolaması", "Yeni bina", "Tekliflerim", "Veri ve modeller" };
            for (int i = 0; i < adlar.Length; i++)
            {
                int j = i;
                var b = new Button
                {
                    Text = "   " + adlar[i], Width = 214, Height = 46, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10.5f),
                    ForeColor = Color.FromArgb(214, 228, 220), BackColor = Ui.Koyu, Margin = new Padding(0, i == 4 ? 28 : 0, 0, 0), Cursor = Cursors.Hand,
                };
                b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 58, 47);
                b.Click += (_, __) => Goster(j);
                menu.Add(b); liste.Controls.Add(b);
            }
            // Oturum bilgisi (sol alt)
            var oturum = new Panel { Dock = DockStyle.Bottom, Height = 104, BackColor = Color.FromArgb(14, 30, 24), Padding = new Padding(0, 6, 0, 0) };
            var linkler = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(16, 0, 0, 0) };
            LinkLabel Link(string t, Action a) { var l = new LinkLabel { Text = t, AutoSize = true, LinkColor = Color.FromArgb(185, 206, 196), ActiveLinkColor = Color.White, Font = Ui.Kucuk, Margin = new Padding(2, 2, 0, 2) }; l.LinkClicked += (_, __) => a(); return l; }
            linkler.Controls.Add(Link("Parola", ParolaDegistir));
            linkler.Controls.Add(Link("Çıkış", () => Kilitle("çıkış", "Oturum kapatıldı.")));
            oturum.Controls.Add(linkler); oturum.Controls.Add(kullaniciEtiket);
            yan.Controls.Add(liste); yan.Controls.Add(oturum); yan.Controls.Add(logo);

            var alt = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Color.White };
            alt.Controls.Add(durumYazisi);

            var ana = new AnaSayfa(d);
            ana.Git += Goster;
            apartman = new ApartmanSekmesi(d);
            teklif = new TeklifSekmesi(d);
            kayitlar = new KayitlarSekmesi(d);
            kayitlar.TeklifeYukle += t =>
            {
                if (t.ProjeTuru == "Yenileme") { Goster(1); apartman.Yukle(t); }
                else { Goster(2); teklif.Yukle(t); }
            };
            var yonetim = new YoneticiSekmesi(d);
            yonetim.GeriYuklendi += () => Kilitle("geri yükleme", "Yedek geri yüklendi. Yeniden giriş yapın.");
            ekranlar.AddRange(new Control[] { ana, apartman, teklif, kayitlar, yonetim });
            foreach (var e in ekranlar) { e.Dock = DockStyle.Fill; e.Visible = false; icerik.Controls.Add(e); }

            Controls.Add(icerik); Controls.Add(alt); Controls.Add(yan);

            Shown += (_, __) =>
            {
                d.ModelleriKur();
                d.Bildir($"Hoş geldiniz, {d.Kullanici?.AdSoyad}.");
                teklif.Hesapla();
                OturumuYansit();
                Goster(0);
                Application.AddMessageFilter(this);
                bekleme.Start();
            };
            bekleme.Tick += (_, __) => { if (DateTime.Now - sonEtkinlik > ZamanAsimi && d.Kullanici != null) Kilitle("zaman aşımı", "Oturum zaman aşımına uğradı."); };
            KeyDown += (_, e) =>
            {
                if (e.Control && e.KeyCode == Keys.S) { if (ekranlar[1].Visible) apartman.Kaydet(); else if (ekranlar[2].Visible) teklif.Kaydet(); e.Handled = true; }
            };
            FormClosed += (_, __) => { bekleme.Stop(); Application.RemoveMessageFilter(this); };
        }

        /// <summary>Klavye ve fare hareketleri oturum zaman aşımını sıfırlar.</summary>
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg is 0x0100 or 0x0200 or 0x0201 or 0x0204 or 0x020A) sonEtkinlik = DateTime.Now;
            return false;
        }

        /// <summary>Oturum açan kullanıcının adı ve rolü; menü yetkiye göre.</summary>
        void OturumuYansit()
        {
            var k = d.Kullanici;
            kullaniciEtiket.Text = k == null ? "" : $"{k.AdSoyad} · {k.RolAdi}";
            menu[4].Visible = d.Yonetici;
            kayitlar.YetkiyiYansit();
            Text = k == null ? "EcoBuildss" : $"EcoBuildss · {k.AdSoyad} ({k.RolAdi})";
            if (!d.Yonetici && aktifEkran == 4) Goster(0);
        }

        void Goster(int i)
        {
            if (i == 4 && !d.Yonetici) { d.Bildir("Yetkiniz yok."); return; }
            aktifEkran = i;
            for (int k = 0; k < ekranlar.Count; k++)
            {
                ekranlar[k].Visible = k == i;
                menu[k].BackColor = k == i ? Ui.Yesil : Ui.Koyu;
                menu[k].ForeColor = k == i ? Color.White : Color.FromArgb(214, 228, 220);
                menu[k].Font = new Font("Segoe UI", 10.5f, k == i ? FontStyle.Bold : FontStyle.Regular);
            }
        }

        /// <summary>Oturumu kapatır, ekranı gizler ve yeniden oturum açılmasını ister. Vazgeçilirse program kapanır.</summary>
        void Kilitle(string neden, string bilgi)
        {
            bekleme.Stop();
            d.Db?.OturumKapat(neden);
            icerik.Visible = false;
            using (var g = new GirisFormu(d.Db!, bilgi))
            {
                if (g.ShowDialog(this) != DialogResult.OK) { Close(); return; }
            }
            sonEtkinlik = DateTime.Now;
            icerik.Visible = true;
            OturumuYansit();
            d.TekliflerDegistiDuyur();
            d.Bildir($"Oturum açıldı: {d.Kullanici?.AdSoyad}.");
            bekleme.Start();
        }

        void ParolaDegistir()
        {
            using var f = new Form { Text = "Parola değiştir", FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(360, 330), BackColor = Color.White, Font = Ui.Yazi };
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(30, 20, 20, 10) };
            TextBox K() => new() { Width = 290, UseSystemPasswordChar = true };
            var eski = K(); var yeni = K(); var yeni2 = K();
            var mesaj = new Label { AutoSize = true, MaximumSize = new Size(290, 0), ForeColor = Ui.Kirmizi };
            p.Controls.Add(Ui.Etiket("Mevcut parola")); p.Controls.Add(eski);
            p.Controls.Add(Ui.Etiket("Yeni parola")); p.Controls.Add(yeni);
            p.Controls.Add(Ui.Etiket("Yeni parola (tekrar)")); p.Controls.Add(yeni2);
            p.Controls.Add(mesaj);
            var tamam = Ui.Dugme("Değiştir", (_, __) =>
            {
                if (yeni.Text != yeni2.Text) { mesaj.Text = "Yeni parolalar aynı değil."; return; }
                try { d.Db!.ParolaDegistir(eski.Text, yeni.Text); f.DialogResult = DialogResult.OK; }
                catch (Exception e) { mesaj.Text = e.Message; }
            }, true);
            p.Controls.Add(tamam); f.Controls.Add(p); f.AcceptButton = tamam;
            if (f.ShowDialog(this) == DialogResult.OK) d.Bildir("Parolanız değiştirildi.");
        }

        /// <summary>veri/referans klasörünü programın yanında, yoksa üst klasörlerde (geliştirme ortamı) arar.</summary>
        public static string Kok()
        {
            var k = new DirectoryInfo(AppContext.BaseDirectory);
            while (k != null && !Directory.Exists(Path.Combine(k.FullName, "veri", "referans"))) k = k.Parent;
            return k?.FullName ?? throw new DirectoryNotFoundException("veri/referans klasörü bulunamadı. Programı çözüm klasöründen ya da derleme çıktısından çalıştırın.");
        }

        /// <summary>Veritabanı ve yedekler kullanıcının uygulama verisi klasöründe tutulur.</summary>
        public static string VeriKlasoru()
        {
            string k = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EcoBuildss");
            Directory.CreateDirectory(k);
            return k;
        }
    }
}
