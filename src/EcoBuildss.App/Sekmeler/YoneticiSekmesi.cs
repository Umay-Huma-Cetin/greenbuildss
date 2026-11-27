using EcoBuildss.Core.Depo;

namespace EcoBuildss.UI
{
    /// <summary>Yönetici ekranı: firma, kullanıcılar, işlem günlüğü, yedek, veri, modeller.</summary>
    public sealed class YoneticiSekmesi : UserControl
    {
        readonly Durum d;

        public YoneticiSekmesi(Durum durum)
        {
            d = durum;
            var sekme = new TabControl { Dock = DockStyle.Fill, Font = Ui.Yazi };
            void Ekle(string ad, Control c) { var t = new TabPage(ad) { BackColor = Ui.Zemin }; c.Dock = DockStyle.Fill; t.Controls.Add(c); sekme.TabPages.Add(t); }
            Ekle("Firma", Firma());
            Ekle("Kullanıcılar", Kullanicilar());
            Ekle("Fiyatlar", Fiyatlar());
            Ekle("Günlük", Gunluk());
            Ekle("Yedek", Yedek());
            Ekle("Veri", new VeriSekmesi(d));
            Ekle("Modeller", new ModellerSekmesi(d));
            Controls.Add(sekme);
            Controls.Add(new Label { Text = "Yönetim", Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI Semibold", 14f), ForeColor = Ui.Koyu, Padding = new Padding(12, 8, 0, 0) });
        }

        Control Firma()
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20), BackColor = Color.White };
            var ad = new TextBox { Width = 360, Text = d.Db?.Ayar("firma_adi") ?? "" };
            var tel = new TextBox { Width = 360, Text = d.Db?.Ayar("firma_telefon") ?? "" };
            p.Controls.Add(Ui.Etiket("Firma adı")); p.Controls.Add(ad);
            p.Controls.Add(Ui.Etiket("Telefon / e-posta")); p.Controls.Add(tel);
            p.Controls.Add(Ui.Dugme("Kaydet", (_, __) =>
            {
                try { d.Db!.AyarYaz("firma_adi", ad.Text.Trim()); d.Db.AyarYaz("firma_telefon", tel.Text.Trim()); d.Bildir("Kaydedildi."); }
                catch (Exception e) { Ui.Hata(p, e.Message); }
            }, true));
            return p;
        }

        // ======================= Kullanıcılar =======================
        Control Kullanicilar()
        {
            var liste = Ui.Izgara();
            void Doldur()
            {
                liste.Columns.Clear(); liste.Rows.Clear();
                if (d.Db == null) return;
                foreach (var c in new[] { "Ad soyad", "Kullanıcı adı", "Rol", "Durum", "Son giriş" }) liste.Columns.Add(c, c);
                foreach (var k in d.Db.Kullanicilar())
                {
                    int i = liste.Rows.Add(k.AdSoyad, k.KullaniciAdi, k.RolAdi, k.Aktif ? "Etkin" : "Pasif", k.SonGiris?.ToString("dd.MM.yyyy HH:mm") ?? "—");
                    liste.Rows[i].Tag = k;
                    if (!k.Aktif) liste.Rows[i].DefaultCellStyle.ForeColor = Ui.Soluk;
                }
            }
            Kullanici? Secili() => liste.CurrentRow?.Tag as Kullanici;
            void Dene(Action a) { try { a(); Doldur(); } catch (Exception e) { Ui.Hata(this, e.Message); } }

            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 6, 8, 0) };
            ust.Controls.Add(Ui.Dugme("Ekle", (_, __) => Dene(Ekle), true));
            ust.Controls.Add(Ui.Dugme("Parola sıfırla", (_, __) => { if (Secili() is Kullanici k) Dene(() => Sifirla(k)); }));
            ust.Controls.Add(Ui.Dugme("Rol değiştir", (_, __) => { if (Secili() is Kullanici k) Dene(() => d.Db!.KullaniciGuncelle(k.Id, k.Yonetici ? Rol.Satis : Rol.Yonetici, k.Aktif)); }));
            ust.Controls.Add(Ui.Dugme("Etkin / pasif", (_, __) => { if (Secili() is Kullanici k) Dene(() => d.Db!.KullaniciGuncelle(k.Id, k.Rol, !k.Aktif)); }));
            var p = new Panel();
            p.Controls.Add(Ui.Kart("Kullanıcılar", liste)); p.Controls.Add(ust);
            VisibleChanged += (_, __) => { if (Visible) Doldur(); };
            return p;
        }

        void Ekle()
        {
            using var f = Pencere("Kullanıcı ekle", 380);
            var p = (FlowLayoutPanel)f.Controls[0];
            var ad = new TextBox { Width = 300 }; var kad = new TextBox { Width = 300 }; var par = new TextBox { Width = 300, UseSystemPasswordChar = true };
            var rol = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 }; rol.Items.AddRange(new object[] { "Satış", "Yönetici" }); rol.SelectedIndex = 0;
            var mesaj = new Label { AutoSize = true, MaximumSize = new Size(300, 0), ForeColor = Ui.Kirmizi };
            p.Controls.Add(Ui.Etiket("Ad soyad")); p.Controls.Add(ad); p.Controls.Add(Ui.Etiket("Kullanıcı adı")); p.Controls.Add(kad);
            p.Controls.Add(Ui.Etiket("Geçici parola")); p.Controls.Add(par); p.Controls.Add(Ui.Etiket("Rol")); p.Controls.Add(rol); p.Controls.Add(mesaj);
            var tamam = Ui.Dugme("Ekle", (_, __) =>
            {
                try { d.Db!.KullaniciEkle(kad.Text, ad.Text, rol.SelectedIndex == 1 ? Rol.Yonetici : Rol.Satis, par.Text); f.DialogResult = DialogResult.OK; }
                catch (Exception e) { mesaj.Text = e.Message; }
            }, true);
            p.Controls.Add(tamam); f.AcceptButton = tamam;
            if (f.ShowDialog(this) == DialogResult.OK) d.Bildir($"{kad.Text} eklendi.");
        }

        void Sifirla(Kullanici k)
        {
            using var f = Pencere($"Parola sıfırla · {k.KullaniciAdi}", 220);
            var p = (FlowLayoutPanel)f.Controls[0];
            var par = new TextBox { Width = 300, UseSystemPasswordChar = true };
            var mesaj = new Label { AutoSize = true, MaximumSize = new Size(300, 0), ForeColor = Ui.Kirmizi };
            p.Controls.Add(Ui.Etiket("Geçici parola")); p.Controls.Add(par); p.Controls.Add(mesaj);
            var tamam = Ui.Dugme("Sıfırla", (_, __) => { try { d.Db!.ParolaSifirla(k.Id, par.Text); f.DialogResult = DialogResult.OK; } catch (Exception e) { mesaj.Text = e.Message; } }, true);
            p.Controls.Add(tamam); f.AcceptButton = tamam;
            if (f.ShowDialog(this) == DialogResult.OK) d.Bildir($"{k.KullaniciAdi}: parola sıfırlandı.");
        }

        static Form Pencere(string baslik, int yukseklik)
        {
            var f = new Form { Text = baslik, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(360, yukseklik), BackColor = Color.White, Font = Ui.Yazi };
            f.Controls.Add(new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(30, 16, 20, 10) });
            return f;
        }

        // ======================= Fiyatlar =======================
        Control Fiyatlar()
        {
            var liste = Ui.Izgara();
            void Doldur()
            {
                liste.Columns.Clear(); liste.Rows.Clear();
                foreach (var c in new[] { "Kod", "Kalem", "Tür", "Fiyat", "Birim", "Kaynak", "Tarih" }) liste.Columns.Add(c, c);
                liste.Columns[1].FillWeight = 220; liste.Columns[5].FillWeight = 220;
                liste.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                foreach (var k in d.V.FiyatKalemleri())
                {
                    int i = liste.Rows.Add(k.Id, k.Ad, k.Tur, k.Fiyat.ToString("#,0.00##", Ui.TR), k.Birim, k.Kaynak, k.Tarih);
                    liste.Rows[i].Tag = k;
                    if (k.Guncellendi) liste.Rows[i].DefaultCellStyle.BackColor = Ui.AcikYesil;
                }
            }
            FiyatKalemi? Secili() => liste.CurrentRow?.Tag as FiyatKalemi;
            void Guncelle(FiyatKalemi k)
            {
                using var f = Pencere($"{k.Id} · fiyat", 250);
                var p = (FlowLayoutPanel)f.Controls[0];
                var fiyat = new TextBox { Width = 300, Text = k.Fiyat.ToString("0.####", Ui.TR) };
                var kaynak = new TextBox { Width = 300 };
                var mesaj = new Label { AutoSize = true, MaximumSize = new Size(300, 0), ForeColor = Ui.Kirmizi };
                p.Controls.Add(Ui.Etiket($"Fiyat ({k.Birim}, KDV hariç)")); p.Controls.Add(fiyat); p.Controls.Add(Ui.Etiket("Kaynak")); p.Controls.Add(kaynak); p.Controls.Add(mesaj);
                var tamam = Ui.Dugme("Kaydet", (_, __) =>
                {
                    if (!double.TryParse(fiyat.Text.Trim(), System.Globalization.NumberStyles.Number, Ui.TR, out var x) || x <= 0) { mesaj.Text = "Geçerli bir fiyat girin."; return; }
                    if (kaynak.Text.Trim().Length < 3) { mesaj.Text = "Kaynağı yazın (tedarikçi, fatura, liste)."; return; }
                    try
                    {
                        string tarih = DateTime.Today.ToString("dd.MM.yyyy");
                        d.V.FiyatUygula(k.Id, x, kaynak.Text.Trim(), tarih);
                        d.Db!.FiyatKaydet(k.Id, x, kaynak.Text.Trim(), k.Fiyat);
                        f.DialogResult = DialogResult.OK;
                    }
                    catch (Exception e) { mesaj.Text = e.Message; }
                }, true);
                p.Controls.Add(tamam); f.AcceptButton = tamam;
                if (f.ShowDialog(this) == DialogResult.OK) { Doldur(); d.VeriDegistiDuyur(); d.Bildir($"{k.Id} fiyatı güncellendi."); }
            }
            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 6, 8, 0) };
            ust.Controls.Add(Ui.Dugme("Güncelle", (_, __) => { if (Secili() is FiyatKalemi k) Guncelle(k); }, true));
            ust.Controls.Add(Ui.Dugme("Referansa dön", (_, __) =>
            {
                if (Secili() is not FiyatKalemi k || !k.Guncellendi) return;
                try { d.Db!.FiyatSil(k.Id); d.V.FiyatReferansaDon(k.Id); Doldur(); d.VeriDegistiDuyur(); }
                catch (Exception e) { Ui.Hata(this, e.Message); }
            }));
            liste.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && Secili() is FiyatKalemi k) Guncelle(k); };
            var p = new Panel();
            p.Controls.Add(Ui.Kart("Fiyatlar", liste)); p.Controls.Add(ust);
            p.VisibleChanged += (_, __) => { if (p.Visible) Doldur(); };
            return p;
        }

        // ======================= Günlük =======================
        Control Gunluk()
        {
            var liste = Ui.Izgara();
            var seviye = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
            seviye.Items.AddRange(new object[] { "Tümü", "Bilgi", "Uyarı", "Hata", "Güvenlik" }); seviye.SelectedIndex = 0;
            var ara = new TextBox { Width = 220 };
            var durumu = new Label { AutoSize = true, Font = Ui.Kalin, Margin = new Padding(12, 9, 0, 0) };
            void Doldur()
            {
                liste.Columns.Clear(); liste.Rows.Clear();
                if (d.Db == null || !d.Yonetici) return;
                foreach (var c in new[] { "Tarih", "Kullanıcı", "Seviye", "İşlem", "Ayrıntı" }) liste.Columns.Add(c, c);
                liste.Columns[4].FillWeight = 260;
                Seviye? s = seviye.SelectedIndex switch { 1 => Seviye.Bilgi, 2 => Seviye.Uyari, 3 => Seviye.Hata, 4 => Seviye.Guvenlik, _ => null };
                string q = ara.Text.Trim();
                foreach (var g in d.Db.GunlukOku(2000, s))
                {
                    if (q.Length > 0 && !(g.Islem + " " + g.Ayrinti + " " + g.Kullanici).ToLower(Ui.TR).Contains(q.ToLower(Ui.TR))) continue;
                    int i = liste.Rows.Add(g.Tarih.ToString("dd.MM.yyyy HH:mm:ss"), g.Kullanici, g.Seviye, g.Islem, g.Ayrinti);
                    if (g.Seviye == "Hata") liste.Rows[i].DefaultCellStyle.ForeColor = Ui.Kirmizi;
                    else if (g.Seviye == "Güvenlik") liste.Rows[i].DefaultCellStyle.ForeColor = Color.FromArgb(140, 90, 20);
                }
            }
            void Denetle(bool kaydet)
            {
                var (n, sorun) = d.Db!.GunlukDogrula();
                durumu.Text = sorun == null ? $"✓ Sağlam ({n:N0} kayıt)" : "✗ " + sorun;
                durumu.ForeColor = sorun == null ? Ui.Yesil : Ui.Kirmizi;
                if (kaydet || sorun != null) d.Db.Gunluk("Günlük denetlendi", sorun ?? $"{n} kayıt sağlam", sorun == null ? Seviye.Bilgi : Seviye.Guvenlik);
            }
            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 6, 8, 0) };
            ust.Controls.Add(Ui.Etiket("Seviye")); ust.Controls.Add(seviye); ust.Controls.Add(Ui.Etiket("Ara")); ust.Controls.Add(ara);
            ust.Controls.Add(Ui.Dugme("Yenile", (_, __) => Doldur()));
            ust.Controls.Add(Ui.Dugme("Denetle", (_, __) => Denetle(true), true));
            ust.Controls.Add(durumu);
            seviye.SelectedIndexChanged += (_, __) => Doldur();
            ara.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Doldur(); e.SuppressKeyPress = true; } };
            var p = new Panel();
            p.Controls.Add(Ui.Kart("İşlem günlüğü", liste)); p.Controls.Add(ust);
            p.VisibleChanged += (_, __) => { if (p.Visible) { Doldur(); Denetle(false); } };
            return p;
        }

        // ======================= Yedek =======================
        Control Yedek()
        {
            var yedekler = Ui.Izgara();
            void Doldur()
            {
                yedekler.Columns.Clear(); yedekler.Rows.Clear();
                if (d.Db == null) return;
                foreach (var c in new[] { "Yedek", "Tarih", "Boyut" }) yedekler.Columns.Add(c, c);
                foreach (var f in d.Db.Yedekler()) { int i = yedekler.Rows.Add(f.Name, f.LastWriteTime.ToString("dd.MM.yyyy HH:mm"), $"{f.Length / 1024:N0} KB"); yedekler.Rows[i].Tag = f.FullName; }
            }
            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 6, 8, 0) };
            ust.Controls.Add(Ui.Dugme("Yedekle", (_, __) => { try { var y = d.Db!.Yedekle(); Doldur(); d.Bildir("Yedek alındı."); } catch (Exception e) { d.Hata("Yedek alınamadı: " + e.Message, e); } }, true));
            ust.Controls.Add(Ui.Dugme("Geri yükle", (_, __) =>
            {
                if (yedekler.CurrentRow?.Tag is not string yol) return;
                if (MessageBox.Show(this, $"{Path.GetFileName(yol)} geri yüklensin mi?", "Geri yükle", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try { d.Db!.GeriYukle(yol); GeriYuklendi?.Invoke(); }
                catch (Exception e) { d.Hata("Geri yüklenemedi: " + e.Message, e); Ui.Hata(this, "Geri yüklenemedi: " + e.Message); }
            }));
            ust.Controls.Add(Ui.Dugme("Kurtarma anahtarı", (_, __) => KurtarmaYenile()));
            ust.Controls.Add(Ui.Dugme("Klasör", (_, __) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(d.Db!.YedekKlasoru) { UseShellExecute = true }); } catch { } }));
            var p = new Panel();
            p.Controls.Add(Ui.Kart("Yedekler", yedekler)); p.Controls.Add(ust);
            p.VisibleChanged += (_, __) => { if (p.Visible) Doldur(); };
            return p;
        }

        void KurtarmaYenile()
        {
            using var f = Pencere("Yeni kurtarma anahtarı", 200);
            var p = (FlowLayoutPanel)f.Controls[0];
            var par = new TextBox { Width = 300, UseSystemPasswordChar = true };
            var mesaj = new Label { AutoSize = true, MaximumSize = new Size(300, 0), ForeColor = Ui.Kirmizi };
            p.Controls.Add(Ui.Etiket("Parolanız")); p.Controls.Add(par); p.Controls.Add(mesaj);
            var tamam = Ui.Dugme("Oluştur", (_, __) =>
            {
                f.Cursor = Cursors.WaitCursor;
                try { d.Db!.KurtarmaAnahtariYenile(par.Text); f.DialogResult = DialogResult.OK; }
                catch (Exception e) { mesaj.Text = e.Message; }
                finally { f.Cursor = Cursors.Default; }
            }, true);
            p.Controls.Add(tamam); f.AcceptButton = tamam;
            if (f.ShowDialog(this) == DialogResult.OK) GirisFormu.KurtarmaAnahtariniGoster(this, d.Db!);
        }

        /// <summary>Yedekten geri yüklenince yeniden oturum açılmalı (yedekteki kullanıcılar ve anahtarlar geçerli olur).</summary>
        public event Action? GeriYuklendi;
    }
}
