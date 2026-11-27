using EcoBuildss.Core.Depo;

namespace EcoBuildss.UI
{
    /// <summary>Yönetici ekranı: firma bilgileri, kullanıcılar ve roller, güvenlik (yedek, günlük bütünlüğü), veri yükleme, modeller.</summary>
    public sealed class YoneticiSekmesi : UserControl
    {
        readonly Durum d;

        public YoneticiSekmesi(Durum durum)
        {
            d = durum;
            var sekme = new TabControl { Dock = DockStyle.Fill, Font = Ui.Yazi };
            void Ekle(string ad, Control c) { var t = new TabPage(ad) { BackColor = Ui.Zemin }; c.Dock = DockStyle.Fill; t.Controls.Add(c); sekme.TabPages.Add(t); }
            Ekle("Firma bilgileri", Firma());
            Ekle("Kullanıcılar", Kullanicilar());
            Ekle("Güvenlik ve yedek", Guvenlik());
            Ekle("Veri yükleme", new VeriSekmesi(d));
            Ekle("Modeller ve doğrulama", new ModellerSekmesi(d));
            Controls.Add(sekme);
            Controls.Add(new Label { Text = "Veri ve modeller", Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI Semibold", 14f), ForeColor = Ui.Koyu, Padding = new Padding(12, 8, 0, 0) });
        }

        Control Firma()
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20), BackColor = Color.White };
            var ad = new TextBox { Width = 360, Text = d.Db?.Ayar("firma_adi") ?? "" };
            var tel = new TextBox { Width = 360, Text = d.Db?.Ayar("firma_telefon") ?? "" };
            p.Controls.Add(Ui.Etiket("Firma adı")); p.Controls.Add(ad);
            p.Controls.Add(Ui.Etiket("Telefon / e-posta")); p.Controls.Add(tel);
            p.Controls.Add(Ui.Aciklama("Toplantı sayfasının ve tekliflerin başında görünür.", 360));
            p.Controls.Add(Ui.Dugme("Kaydet", (_, __) =>
            {
                try { d.Db!.AyarYaz("firma_adi", ad.Text.Trim()); d.Db.AyarYaz("firma_telefon", tel.Text.Trim()); d.Bildir("Firma bilgileri kaydedildi."); }
                catch (Exception e) { Ui.Hata(p, e.Message); }
            }, true));
            p.Controls.Add(new Label { Text = "VERİLER NEREDEN GELİYOR?", AutoSize = true, Font = Ui.Kalin, ForeColor = Ui.Yesil, Margin = new Padding(0, 24, 0, 4) });
            p.Controls.Add(Ui.Aciklama(
                "İklim: MGM il derece-günleri · Yönetmelik: TS 825:2024 U sınırları, Binaların Yangından Korunması Hakkında Yönetmelik md. 27 · " +
                "Malzeme fiyatları: uygulanmış sistem fiyatları (satıcı ve piyasa listeleri, her satırda kaynak ve tarih) · Doğal gaz: BOTAŞ tarifesi · Elektrik: EPDK · " +
                "Faiz ve enflasyon: TCMB, TÜİK · Karbon: ICE veri tabanı, IPCC. Kaynaklar veri/referans klasöründeki dosyalarda satır satır yazılıdır.", 760));
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
                    int i = liste.Rows.Add(k.AdSoyad, k.KullaniciAdi, k.RolAdi, k.Aktif ? "etkin" : "devre dışı", k.SonGiris?.ToString("dd.MM.yyyy HH:mm") ?? "—");
                    liste.Rows[i].Tag = k;
                    if (!k.Aktif) liste.Rows[i].DefaultCellStyle.ForeColor = Ui.Soluk;
                }
            }
            Kullanici? Secili() => liste.CurrentRow?.Tag as Kullanici;
            void Dene(Action a) { try { a(); Doldur(); } catch (Exception e) { Ui.Hata(this, e.Message); } }

            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 6, 8, 0) };
            ust.Controls.Add(Ui.Dugme("Kullanıcı ekle…", (_, __) => Dene(Ekle), true));
            ust.Controls.Add(Ui.Dugme("Parolayı sıfırla…", (_, __) => { if (Secili() is Kullanici k) Dene(() => Sifirla(k)); }));
            ust.Controls.Add(Ui.Dugme("Yönetici / satış yap", (_, __) => { if (Secili() is Kullanici k) Dene(() => d.Db!.KullaniciGuncelle(k.Id, k.Yonetici ? Rol.Satis : Rol.Yonetici, k.Aktif)); }));
            ust.Controls.Add(Ui.Dugme("Etkinleştir / devre dışı bırak", (_, __) => { if (Secili() is Kullanici k) Dene(() => d.Db!.KullaniciGuncelle(k.Id, k.Rol, !k.Aktif)); }));
            var p = new Panel();
            p.Controls.Add(Ui.Kart("Kullanıcılar", liste, "Yönetici: her şey · Satış: teklif hazırlar, kaydeder ve sonucunu işaretler; teklif silemez, veri yükleyemez, kullanıcı ve ayar değiştiremez"));
            p.Controls.Add(ust);
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
            if (f.ShowDialog(this) == DialogResult.OK) d.Bildir($"{kad.Text} eklendi. Geçici parolayı kişiye iletin; ilk girişte değiştirmesini isteyin.");
        }

        void Sifirla(Kullanici k)
        {
            using var f = Pencere($"Parolayı sıfırla · {k.KullaniciAdi}", 220);
            var p = (FlowLayoutPanel)f.Controls[0];
            var par = new TextBox { Width = 300, UseSystemPasswordChar = true };
            var mesaj = new Label { AutoSize = true, MaximumSize = new Size(300, 0), ForeColor = Ui.Kirmizi };
            p.Controls.Add(Ui.Etiket("Yeni geçici parola")); p.Controls.Add(par); p.Controls.Add(mesaj);
            var tamam = Ui.Dugme("Sıfırla", (_, __) => { try { d.Db!.ParolaSifirla(k.Id, par.Text); f.DialogResult = DialogResult.OK; } catch (Exception e) { mesaj.Text = e.Message; } }, true);
            p.Controls.Add(tamam); f.AcceptButton = tamam;
            if (f.ShowDialog(this) == DialogResult.OK) d.Bildir($"{k.KullaniciAdi} parolası sıfırlandı; hesap kilidi de açıldı.");
        }

        static Form Pencere(string baslik, int yukseklik)
        {
            var f = new Form { Text = baslik, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(360, yukseklik), BackColor = Color.White, Font = Ui.Yazi };
            f.Controls.Add(new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(30, 16, 20, 10) });
            return f;
        }

        // ======================= Güvenlik ve yedek =======================
        Control Guvenlik()
        {
            var yedekler = Ui.Izgara();
            var zincir = new Label { AutoSize = true, MaximumSize = new Size(560, 0), Font = Ui.Yazi, Margin = new Padding(0, 6, 0, 0) };
            void Doldur()
            {
                yedekler.Columns.Clear(); yedekler.Rows.Clear();
                if (d.Db == null) return;
                foreach (var c in new[] { "Yedek", "Tarih", "Boyut" }) yedekler.Columns.Add(c, c);
                foreach (var f in d.Db.Yedekler()) { int i = yedekler.Rows.Add(f.Name, f.LastWriteTime.ToString("dd.MM.yyyy HH:mm"), $"{f.Length / 1024:N0} KB"); yedekler.Rows[i].Tag = f.FullName; }
            }
            void Dogrula()
            {
                var (n, sorun) = d.Db!.GunlukDogrula();
                zincir.Text = sorun == null ? $"✓ İşlem günlüğü sağlam: {n:N0} kaydın hiçbiri değiştirilmemiş ya da silinmemiş." : $"✗ Günlük zinciri bozuk: {sorun} Veritabanı dosyasına program dışından müdahale edilmiş olabilir.";
                zincir.ForeColor = sorun == null ? Ui.Yesil : Ui.Kirmizi;
                d.Db.Gunluk("Günlük bütünlüğü denetlendi", sorun ?? $"{n} kayıt sağlam", sorun == null ? Seviye.Bilgi : Seviye.Guvenlik);
            }

            var sol = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(4), BackColor = Color.White };
            sol.Controls.Add(Ui.Aciklama("Her işlem (oturum açma ve kapama, başarısız giriş, teklif kaydı, silme, sonuç, veri yükleme, ayar ve kullanıcı değişikliği, yedek, hata) " +
                "veritabanındaki işlem günlüğüne kim yaptıysa onun adıyla yazılır. Her kayıt bir öncekinin SHA-256 özetini içerir: bir kayıt sonradan değiştirilir ya da silinirse " +
                "aşağıdaki denetim bunu bulur. Parolalar PBKDF2-SHA256 (600.000 tekrar, kullanıcıya özel tuz) ile saklanır; 5 hatalı denemede hesap 15 dakika kilitlenir; " +
                $"{AnaForm.ZamanAsimi.TotalMinutes:0} dakika işlem yapılmazsa oturum kilitlenir.", 560));
            sol.Controls.Add(Ui.Dugme("Günlük bütünlüğünü denetle", (_, __) => Dogrula(), true));
            sol.Controls.Add(zincir);

            var yedekPanel = new Panel();
            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            ust.Controls.Add(Ui.Dugme("Şimdi yedekle", (_, __) => { try { var y = d.Db!.Yedekle(); Doldur(); d.Bildir("Yedek alındı: " + Path.GetFileName(y)); } catch (Exception e) { d.Hata("Yedek alınamadı: " + e.Message, e); } }, true));
            ust.Controls.Add(Ui.Dugme("Seçili yedeği geri yükle…", (_, __) =>
            {
                if (yedekler.CurrentRow?.Tag is not string yol) return;
                if (MessageBox.Show(this, $"{Path.GetFileName(yol)} geri yüklensin mi?\n\nŞu anki veriler önce ayrıca yedeklenir; yine de bu yedekten sonra yapılan kayıtlar ekrandan kalkar.",
                        "Geri yükle", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try { d.Db!.GeriYukle(yol); Doldur(); d.TekliflerDegistiDuyur(); d.ModelleriKur(); d.Bildir("Yedekten geri yüklendi: " + Path.GetFileName(yol)); }
                catch (Exception e) { d.Hata("Geri yüklenemedi: " + e.Message, e); Ui.Hata(this, "Geri yüklenemedi: " + e.Message); }
            }));
            ust.Controls.Add(Ui.Dugme("Yedek klasörünü aç", (_, __) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(d.Db!.YedekKlasoru) { UseShellExecute = true }); } catch { } }));
            yedekPanel.Controls.Add(yedekler); yedekler.Dock = DockStyle.Fill; yedekPanel.Controls.Add(ust);

            var iz = Ui.Izgara2(2, 45, 55);
            iz.Controls.Add(Ui.Kart("İşlem günlüğü ve parola güvenliği", sol), 0, 0);
            iz.Controls.Add(Ui.Kart("Yedekler", yedekPanel, $"Program her gün ilk açılışta otomatik yedek alır; son {Veritabani.YedekSaklamaGun} günün yedeği saklanır"), 1, 0);
            VisibleChanged += (_, __) => { if (Visible) Doldur(); };
            return iz;
        }
    }
}
