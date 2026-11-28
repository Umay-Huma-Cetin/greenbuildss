using EcoBuildss.Core.Depo;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Oturum açma. Veritabanında hiç kullanıcı yoksa ilk yönetici hesabı oluşturulur (ilk kurulum).
    /// 5 hatalı denemede hesap 15 dakika kilitlenir; her deneme güvenlik günlüğüne yazılır.
    /// </summary>
    public sealed class GirisFormu : Form
    {
        readonly Veritabani db;
        readonly bool kurulum;
        readonly TextBox adSoyad = Kutu(), kullanici = Kutu(), parola = Kutu(true), parola2 = Kutu(true);
        readonly Label mesaj = new() { AutoSize = true, MaximumSize = new Size(300, 0), ForeColor = Ui.Kirmizi, Font = Ui.Yazi, Margin = new Padding(0, 8, 0, 0) };

        static TextBox Kutu(bool gizli = false) => new() { Width = 300, Font = new Font("Segoe UI", 11f), UseSystemPasswordChar = gizli };

        public GirisFormu(Veritabani db, string? bilgi = null)
        {
            this.db = db;
            kurulum = db.KullaniciSayisi == 0;
            Text = kurulum ? "EcoBuildss · İlk kurulum" : "EcoBuildss · Oturum aç";
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White; Font = Ui.Yazi; ClientSize = new Size(380, kurulum ? 450 : 330);

            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(40, 26, 30, 10) };
            p.Controls.Add(new Label { Text = "EcoBuildss", AutoSize = true, Font = new Font("Segoe UI Semibold", 18f), ForeColor = Ui.Koyu });
            p.Controls.Add(new Label
            {
                Text = kurulum ? "Yönetici hesabı oluşturun." : (bilgi ?? ""),
                AutoSize = true, MaximumSize = new Size(300, 0), ForeColor = Ui.Soluk, Margin = new Padding(0, 2, 0, 10),
            });
            void Alan(string a, Control c) { p.Controls.Add(Ui.Etiket(a)); p.Controls.Add(c); }
            if (kurulum) Alan("Ad soyad", adSoyad);
            Alan("Kullanıcı adı", kullanici);
            Alan("Parola", parola);
            if (kurulum) { Alan("Parola (tekrar)", parola2); p.Controls.Add(Ui.Aciklama("En az 8 karakter, harf ve rakam.", 300)); }
            p.Controls.Add(mesaj);
            var tamam = Ui.Dugme(kurulum ? "Oluştur" : "Giriş", (_, __) => Tamam(), true);
            tamam.AutoSize = false; tamam.Width = 300; tamam.Height = 38; tamam.Margin = new Padding(0, 10, 0, 0);
            p.Controls.Add(tamam);
            if (!kurulum)
            {
                var unuttum = new LinkLabel { Text = "Parolamı unuttum", AutoSize = true, Margin = new Padding(0, 10, 0, 0), LinkColor = Ui.Yesil };
                unuttum.LinkClicked += (_, __) => Kurtar();
                p.Controls.Add(unuttum);
            }
            Controls.Add(p);
            AcceptButton = tamam;
            Shown += (_, __) => (kurulum ? adSoyad : kullanici).Focus();
        }

        /// <summary>Yöneticinin verdiği geçici parolayla ilk girişte yeni parola belirlenir.</summary>
        bool YeniParola()
        {
            string eski = parola.Text;
            using var f = new Form { Text = "Yeni parola belirleyin", FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(360, 250), BackColor = Color.White, Font = Ui.Yazi };
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(30, 16, 20, 10) };
            var y1 = new TextBox { Width = 290, UseSystemPasswordChar = true }; var y2 = new TextBox { Width = 290, UseSystemPasswordChar = true };
            var m = new Label { AutoSize = true, MaximumSize = new Size(290, 0), ForeColor = Ui.Kirmizi };
            p.Controls.Add(Ui.Aciklama("Kendi parolanızı belirleyin.", 290));
            p.Controls.Add(Ui.Etiket("Yeni parola")); p.Controls.Add(y1); p.Controls.Add(Ui.Etiket("Yeni parola (tekrar)")); p.Controls.Add(y2); p.Controls.Add(m);
            var t = Ui.Dugme("Kaydet", (_, __) =>
            {
                if (y1.Text != y2.Text) { m.Text = "Parolalar aynı değil."; return; }
                if (y1.Text == eski) { m.Text = "Yeni parola geçici paroladan farklı olmalı."; return; }
                try { db.ParolaDegistir(eski, y1.Text); f.DialogResult = DialogResult.OK; } catch (Exception e) { m.Text = e.Message; }
            }, true);
            p.Controls.Add(t); f.Controls.Add(p); f.AcceptButton = t;
            return f.ShowDialog(this) == DialogResult.OK;
        }

        static Form Pencere(string baslik, int y) => new() { Text = baslik, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(380, y), BackColor = Color.White, Font = Ui.Yazi };

        /// <summary>Kurtarma anahtarı yalnız bir kez gösterilir; parola unutulursa şifreli veriye erişimin tek yoludur.</summary>
        void KurtarmaAnahtariniGoster() => KurtarmaAnahtariniGoster(this, db);

        internal static void KurtarmaAnahtariniGoster(IWin32Window sahip, Veritabani db)
        {
            if (db.YeniKurtarmaAnahtari is not string anahtar) return;
            using var f = Pencere("Kurtarma anahtarı", 270);
            f.ControlBox = false;
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(30, 18, 20, 10) };
            p.Controls.Add(Ui.Aciklama("Bu anahtarı yazıp güvenli bir yerde saklayın. Bir daha gösterilmez.", 320));
            var kutu = new TextBox { Text = anahtar, ReadOnly = true, Width = 320, Font = new Font("Consolas", 13f), BackColor = Ui.AcikYesil, Margin = new Padding(0, 12, 0, 8) };
            p.Controls.Add(kutu);
            var onay = new CheckBox { Text = "Kaydettim", AutoSize = true };
            var t = Ui.Dugme("Tamam", (_, __) => f.DialogResult = DialogResult.OK, true); t.Enabled = false;
            onay.CheckedChanged += (_, __) => t.Enabled = onay.Checked;
            p.Controls.Add(Ui.Dugme("Kopyala", (_, __) =>
            {
                try
                {
                    Clipboard.SetText(anahtar);
                    // Anahtar panoda unutulmasın: 60 sn sonra, hâlâ oradaysa silinir
                    var t = new System.Windows.Forms.Timer { Interval = 60_000 };
                    t.Tick += (s1, e1) => { t.Stop(); t.Dispose(); try { if (Clipboard.ContainsText() && Clipboard.GetText() == anahtar) Clipboard.Clear(); } catch { } };
                    t.Start();
                }
                catch { }
            }));
            p.Controls.Add(onay); p.Controls.Add(t);
            f.Controls.Add(p);
            f.FormClosing += (_, e) => { if (!onay.Checked) e.Cancel = true; };   // anahtar kaydedilmeden kapanmaz
            f.ShowDialog(sahip);
            db.KurtarmaAnahtariGosterildi();
            db.Gunluk("Kurtarma anahtarı gösterildi", db.Aktif?.KullaniciAdi ?? "", Seviye.Guvenlik);
        }

        /// <summary>Yönetici parolasını kurtarma anahtarıyla yeniler.</summary>
        void Kurtar()
        {
            using var f = Pencere("Parola yenile", 380);
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(30, 16, 20, 10) };
            var ad = new TextBox { Width = 320, Text = kullanici.Text }; var anahtar = new TextBox { Width = 320, Font = new Font("Consolas", 11f), CharacterCasing = CharacterCasing.Upper };
            var y1 = new TextBox { Width = 320, UseSystemPasswordChar = true }; var y2 = new TextBox { Width = 320, UseSystemPasswordChar = true };
            var m = new Label { AutoSize = true, MaximumSize = new Size(320, 0), ForeColor = Ui.Kirmizi };
            void A(string e, Control c) { p.Controls.Add(Ui.Etiket(e)); p.Controls.Add(c); }
            A("Kullanıcı adı", ad); A("Kurtarma anahtarı", anahtar); A("Yeni parola", y1); A("Yeni parola (tekrar)", y2);
            p.Controls.Add(m);
            var t = Ui.Dugme("Yenile", (_, __) =>
            {
                if (y1.Text != y2.Text) { m.Text = "Parolalar aynı değil."; return; }
                f.Cursor = Cursors.WaitCursor;
                try { db.KurtarmaIleSifirla(ad.Text, anahtar.Text, y1.Text); f.DialogResult = DialogResult.OK; }
                catch (Exception e) { m.Text = e.Message; }
                finally { f.Cursor = Cursors.Default; }
            }, true);
            p.Controls.Add(t); f.Controls.Add(p); f.AcceptButton = t;
            if (f.ShowDialog(this) == DialogResult.OK) { kullanici.Text = ad.Text; parola.Clear(); parola.Focus(); mesaj.ForeColor = Ui.Yesil; mesaj.Text = "Parola yenilendi. Giriş yapın."; }
        }

        void Tamam()
        {
            mesaj.Text = ""; mesaj.ForeColor = Ui.Kirmizi;
            Cursor = Cursors.WaitCursor;
            try
            {
                if (kurulum)
                {
                    if (parola.Text != parola2.Text) { mesaj.Text = "Parolalar aynı değil."; return; }
                    db.IlkYonetici(kullanici.Text, adSoyad.Text, parola.Text);
                    KurtarmaAnahtariniGoster();
                    DialogResult = DialogResult.OK; return;
                }
                switch (db.GirisYap(kullanici.Text, parola.Text))
                {
                    case GirisSonucu.Basarili:
                        if (db.Aktif!.ParolaDegismeli && !YeniParola()) { db.OturumKapat("geçici parola değiştirilmedi"); mesaj.Text = "Geçici parolayı değiştirmeden devam edilemez."; break; }
                        KurtarmaAnahtariniGoster();
                        DialogResult = DialogResult.OK; return;
                    case GirisSonucu.Kilitli: mesaj.Text = $"Çok sayıda hatalı deneme: hesap {Veritabani.KilitSuresi.TotalMinutes:0} dakika kilitli. Yöneticiniz parolayı sıfırlayabilir."; break;
                    case GirisSonucu.Pasif: mesaj.Text = "Bu hesap devre dışı. Yöneticinize başvurun."; break;
                    case GirisSonucu.AnahtarYok: mesaj.Text = "Yöneticiniz parolanızı sıfırlamalı."; break;
                    default: mesaj.Text = "Kullanıcı adı ya da parola hatalı."; break;
                }
                parola.Clear(); parola.Focus();
            }
            catch (ArgumentException e) { mesaj.Text = e.Message; }
            catch (Exception e) { mesaj.Text = "Oturum açılamadı: " + e.Message; }
            finally { Cursor = Cursors.Default; }
        }
    }
}
