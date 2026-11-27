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
            BackColor = Color.White; Font = Ui.Yazi; ClientSize = new Size(380, kurulum ? 470 : 330);

            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(40, 26, 30, 10) };
            p.Controls.Add(new Label { Text = "EcoBuildss", AutoSize = true, Font = new Font("Segoe UI Semibold", 18f), ForeColor = Ui.Koyu });
            p.Controls.Add(new Label
            {
                Text = kurulum ? "İlk kurulum: firmanın yönetici hesabını oluşturun. Diğer kullanıcıları sonra yönetici ekler." : (bilgi ?? "Devam etmek için oturum açın."),
                AutoSize = true, MaximumSize = new Size(300, 0), ForeColor = Ui.Soluk, Margin = new Padding(0, 2, 0, 10),
            });
            void Alan(string a, Control c) { p.Controls.Add(Ui.Etiket(a)); p.Controls.Add(c); }
            if (kurulum) Alan("Ad soyad", adSoyad);
            Alan("Kullanıcı adı", kullanici);
            Alan("Parola", parola);
            if (kurulum) { Alan("Parola (tekrar)", parola2); p.Controls.Add(Ui.Aciklama("En az 8 karakter; en az bir harf ve bir rakam.", 300)); }
            p.Controls.Add(mesaj);
            var tamam = Ui.Dugme(kurulum ? "Hesabı oluştur ve başla" : "Oturum aç", (_, __) => Tamam(), true);
            tamam.AutoSize = false; tamam.Width = 300; tamam.Height = 38; tamam.Margin = new Padding(0, 10, 0, 0);
            p.Controls.Add(tamam);
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
            p.Controls.Add(Ui.Aciklama("Yöneticinizin verdiği geçici parolayla girdiniz. Devam etmek için kendi parolanızı belirleyin.", 290));
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

        void Tamam()
        {
            mesaj.Text = "";
            Cursor = Cursors.WaitCursor;
            try
            {
                if (kurulum)
                {
                    if (parola.Text != parola2.Text) { mesaj.Text = "Parolalar aynı değil."; return; }
                    db.IlkYonetici(kullanici.Text, adSoyad.Text, parola.Text);
                    DialogResult = DialogResult.OK; return;
                }
                switch (db.GirisYap(kullanici.Text, parola.Text))
                {
                    case GirisSonucu.Basarili:
                        if (db.Aktif!.ParolaDegismeli && !YeniParola()) { db.OturumKapat("geçici parola değiştirilmedi"); mesaj.Text = "Geçici parolayı değiştirmeden devam edilemez."; break; }
                        DialogResult = DialogResult.OK; return;
                    case GirisSonucu.Kilitli: mesaj.Text = $"Çok sayıda hatalı deneme: hesap {Veritabani.KilitSuresi.TotalMinutes:0} dakika kilitli. Yöneticiniz parolayı sıfırlayabilir."; break;
                    case GirisSonucu.Pasif: mesaj.Text = "Bu hesap devre dışı. Yöneticinize başvurun."; break;
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
