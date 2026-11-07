using System.Drawing;
using System.Windows.Forms;
using EcoBuildss.Core;
using EcoBuildss.Depo;

namespace EcoBuildss.UI
{
    /// <summary>Kayıtlı teklifler sekmesi: teklifler SQLite veritabanına kaydedilir, listelenir, yeniden açılır ya da silinir.</summary>
    public sealed partial class AnaForm
    {
        readonly DataGridView gridKayit = new();
        readonly Label lblDb = new() { AutoSize = true, Dock = DockStyle.Top, ForeColor = Color.FromArgb(93, 103, 99), Font = new Font("Segoe UI", 9f), Padding = new Padding(2, 0, 0, 8) };
        readonly Button btnKayitAc = YesilDugme("Seçili teklifi aç"), btnKayitSil = new() { Text = "Sil", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(180, 84, 26), BackColor = Color.White, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(8, 4, 0, 0), Cursor = Cursors.Hand };

        /// <summary>Veritabanı Veri klasöründe durur; klasöre yazılamıyorsa kullanıcının yerel uygulama verisi klasörüne alınır.</summary>
        static string VeritabaniYolu(string veriKlasoru)
        {
            string klasor;
            try
            {
                string test = Path.Combine(veriKlasoru, ".yazma_testi");
                File.WriteAllText(test, ""); File.Delete(test);
                klasor = veriKlasoru;
            }
            catch
            {
                klasor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Marka.Ad);
                Directory.CreateDirectory(klasor);
            }
            string yol = Path.Combine(klasor, "ecobuildss.db");
            return yol;
        }

        TabPage KayitSekmesi()
        {
            var s = new TabPage("Kayıtlı teklifler") { BackColor = Color.White, Padding = new Padding(8) };
            gridKayit.Dock = DockStyle.Fill;
            gridKayit.ReadOnly = true;
            gridKayit.AllowUserToAddRows = false; gridKayit.AllowUserToDeleteRows = false; gridKayit.AllowUserToResizeRows = false;
            gridKayit.RowHeadersVisible = false; gridKayit.MultiSelect = false;
            gridKayit.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridKayit.BackgroundColor = Color.White; gridKayit.BorderStyle = BorderStyle.None;
            gridKayit.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridKayit.EnableHeadersVisualStyles = false;
            gridKayit.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            gridKayit.ColumnHeadersDefaultCellStyle.BackColor = Zemin;
            gridKayit.DefaultCellStyle.SelectionBackColor = YesilAcik;
            gridKayit.DefaultCellStyle.SelectionForeColor = Color.Black;
            (string ad, string baslik, bool sayi, float agirlik)[] kolonlar =
            {
                ("no", "No", true, 4), ("tarih", "Tarih", false, 10), ("proje", "Proje", false, 14), ("bina", "Bina", false, 14), ("paket", "Paket", false, 9),
                ("malzeme", "Malzemeler", false, 20), ("yat", "İlk yatırım", true, 9), ("kdv", "KDV dahil", true, 9), ("lcc", "Ömür boyu maliyet", true, 10), ("co2", "CO₂", true, 6), ("ts", "TS 825", false, 6),
            };
            foreach (var k in kolonlar)
            {
                var col = new DataGridViewTextBoxColumn { Name = k.ad, HeaderText = k.baslik, FillWeight = k.agirlik, SortMode = DataGridViewColumnSortMode.NotSortable, MinimumWidth = 40 };
                if (k.sayi) col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                gridKayit.Columns.Add(col);
            }
            var alt = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 0) };
            btnKayitSil.FlatAppearance.BorderColor = Color.FromArgb(214, 180, 160);
            // pasif düğmeler soluk görünsün (düz düğmede Windows yazıyı gri yapar, zemin değişmez)
            btnKayitAc.EnabledChanged += (_, _) => btnKayitAc.BackColor = btnKayitAc.Enabled ? Yesil : Color.FromArgb(205, 211, 207);
            btnKayitSil.EnabledChanged += (_, _) => btnKayitSil.FlatAppearance.BorderColor = btnKayitSil.Enabled ? Color.FromArgb(214, 180, 160) : Color.FromArgb(220, 222, 220);
            alt.Controls.Add(btnKayitAc); alt.Controls.Add(btnKayitSil);
            s.Controls.Add(gridKayit);
            s.Controls.Add(lblDb);
            s.Controls.Add(alt);
            gridKayit.Paint += (_, e) =>
            {
                if (gridKayit.Rows.Count > 0) return;
                var alan = gridKayit.ClientRectangle; alan.Y += gridKayit.ColumnHeadersHeight; alan.Height -= gridKayit.ColumnHeadersHeight;
                using var f1 = new Font("Segoe UI Semibold", 12f); using var f2 = new Font("Segoe UI", 9.5f);
                var ust = new Rectangle(alan.X, alan.Y + alan.Height / 2 - 34, alan.Width, 28);
                var alt2 = new Rectangle(alan.X, alan.Y + alan.Height / 2 - 4, alan.Width, 40);
                TextRenderer.DrawText(e.Graphics, "Henüz kayıtlı teklif yok", f1, ust, Color.FromArgb(29, 35, 33), TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(e.Graphics, "Teklif sekmesinde bir paket seçip \"Teklifi kaydet\" (Ctrl+S) ile kaydedin; burada listelenir ve sonra yeniden açılabilir.", f2, alt2, Gri,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
            };
            gridKayit.Resize += (_, _) => gridKayit.Invalidate();
            btnKayitAc.Click += (_, _) => { if (SeciliKayit() is { } k) TeklifiAc(k); };
            gridKayit.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && gridKayit.Rows[e.RowIndex].Tag is KayitliTeklif k) TeklifiAc(k); };
            btnKayitSil.Click += (_, _) =>
            {
                if (db == null || SeciliKayit() is not { } k) return;
                if (MessageBox.Show($"{k.Id} numaralı teklif ({k.ProjeAdi}) silinsin mi?", Marka.Ad, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                db.TeklifSil(k.Id); KayitlariYenile();
                Gunluk.Bilgi("Teklif silindi", $"{k.Id} · {k.ProjeAdi}");
            };
            return s;
        }

        KayitliTeklif? SeciliKayit() => gridKayit.SelectedRows.Count > 0 ? gridKayit.SelectedRows[0].Tag as KayitliTeklif : null;

        void KayitlariYenile()
        {
            var tr = Ortak.TR;
            gridKayit.Rows.Clear();
            if (db == null)
            {
                lblDb.Text = "Veritabanı açılamadığı için teklif kaydı kullanılamıyor (veri CSV dosyalarından okundu).";
                btnKayitAc.Enabled = btnKayitSil.Enabled = btnKaydet.Enabled = false;
                return;
            }
            try
            {
                var say = db.SatirSayilari();
                lblDb.Text = "Çift tıklayınca teklif açılır.";
                foreach (var k in db.Teklifler())
                {
                    var malz = new[] { k.YalitimId, k.CamId, k.DuvarId }.Select(id => veri!.Malzemeler.FirstOrDefault(m => m.Id == id)?.KisaAd ?? id + " (listede yok)");
                    int i = gridKayit.Rows.Add(k.Id, k.Tarih.ToString("d MMM yyyy HH:mm", tr), k.ProjeAdi, k.BinaOzeti, k.Paket, string.Join(" · ", malz),
                        TL(k.IlkYatirim), TL(k.KdvDahil), TL(k.OmurBoyuMaliyet), k.Co2Ton.ToString("N1", tr) + " t", k.Ts825Uygun ? "Uygun" : "Uygun değil");
                    gridKayit.Rows[i].Tag = k;
                }
                gridKayit.ClearSelection();
                if (gridKayit.Rows.Count > 0) gridKayit.Rows[0].Selected = true;
                btnKayitAc.Enabled = btnKayitSil.Enabled = gridKayit.Rows.Count > 0;
            }
            catch (Exception ex) { lblDb.Text = "Kayıtlar okunamadı: " + ex.Message; Gunluk.Hata("Kayıtlar okunamadı", ex); }
        }

        void TeklifiKaydet()
        {
            if (db == null || sonTeklif == null || sonGirdi == null || veri == null) return;
            var (s, r, _, paketAdi, yil) = sonTeklif.Value;
            var g = sonGirdi;
            string? ad = MetinSor("Teklifi kaydet", "Proje / müşteri adı:", $"Teklif {DateTime.Now.ToString("d MMM yyyy", Ortak.TR)}");
            if (ad == null) return;
            var k = new KayitliTeklif
            {
                Tarih = DateTime.Now, ProjeAdi = ad.Trim(), Paket = paketAdi, BinaOzeti = $"{r.IlAdi} · {g.Form}",
                FormNo = g.Form.No, En = g.Form.En ?? 0, Boy = g.Form.Boy ?? 0, Kat = g.Form.KatSayisi(veri.P("M7")),
                Yon = g.Yon, CamOrani = g.CamOrani, Yil = yil, Tarife = cmbTarife.SelectedIndex, Ts825Filtre = chkTs825.Checked,
                Oncelik = oncelik, Tb1 = tb1.Value, Tb2 = tb2.Value, Tb3 = tb3.Value, Il = r.IlAdi, Bolge = r.BolgeNo, Dograma = r.Dograma, Mevcut = g.Mevcut, Yakit = g.Yakit, Sogutma = g.Sogutma, EnerjiArtisi = g.EnerjiArtisi,
                YalitimId = s.Yalitim.Id, CamId = s.Cam.Id, DuvarId = s.Duvar.Id,
                IlkYatirim = s.IlkYatirim, KdvDahil = s.IlkYatirim * (1 + veri.P("KDV")), YillikIsitmaKWh = s.YillikIsitmaKWh,
                YillikGiderTL = s.YillikGiderTL, OmurBoyuMaliyet = s.ToplamMaliyet, Co2Ton = s.ToplamCO2Kg / 1000, Ts825Uygun = s.Ts825Uygun,
            };
            try
            {
                long id = db.TeklifKaydet(k);
                KayitlariYenile();
                Gunluk.Bilgi("Teklif kaydedildi", $"{id} · {k.ProjeAdi} · {k.Il} {k.Bolge}. bölge · {s.Kod} · {k.Dograma} · ilk yatırım {k.IlkYatirim:F2} TL");
                Bildir($"Teklif {id} numarasıyla kaydedildi.");
            }
            catch (Exception ex) { Gunluk.Hata("Teklif kaydedilemedi", ex); MessageBox.Show("Teklif kaydedilemedi:\n" + ex.Message, Marka.Ad, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        /// <summary>Kayıtlı teklifin girdilerini ekrana geri yükler ve güncel verilerle yeniden hesaplar.</summary>
        void TeklifiAc(KayitliTeklif k)
        {
            if (veri == null) return;
            var y = veri.Malzemeler.FirstOrDefault(m => m.Id == k.YalitimId);
            var p = veri.Malzemeler.FirstOrDefault(m => m.Id == k.CamId);
            var d = veri.Malzemeler.FirstOrDefault(m => m.Id == k.DuvarId);
            hazir = false;
            try
            {
                if (k.FormNo > 0 && k.FormNo <= veri.Formlar.Count) cmbForm.SelectedItem = veri.Formlar[k.FormNo - 1];
                else
                {
                    cmbForm.SelectedItem = OzelOlcuMetni;
                    numEn.Value = Math.Max(numEn.Minimum, Math.Min(numEn.Maximum, (decimal)k.En));
                    numBoy.Value = Math.Max(numBoy.Minimum, Math.Min(numBoy.Maximum, (decimal)k.Boy));
                    numKat.Value = Math.Max(numKat.Minimum, Math.Min(numKat.Maximum, k.Kat));
                }
                ozelPanel.Visible = cmbForm.SelectedItem is string;
                if (k.Il.Length > 0 && cmbIl.Items.Cast<Il>().FirstOrDefault(i => i.Ad == k.Il) is Il kil) cmbIl.SelectedItem = kil;
                if (k.Bolge > 0) SecimYap(cmbBolge, k.Bolge);
                SecimYap(cmbYon, k.Yon); SecimYap(cmbCam, k.CamOrani);
                if (cmbDograma.Items.Contains(k.Dograma)) cmbDograma.SelectedItem = k.Dograma;
                cmbProjeTuru.SelectedIndex = k.Mevcut ? 1 : 0;
                if (cmbYakit.Items.Cast<Secim>().FirstOrDefault(x => x.Kod == k.Yakit) is Secim ys) cmbYakit.SelectedItem = ys;
                chkSogutma.Checked = k.Sogutma;
                numArtis.Value = Math.Max(numArtis.Minimum, Math.Min(numArtis.Maximum, (decimal)(k.EnerjiArtisi * 100)));
                numYil.Value = Math.Max(numYil.Minimum, Math.Min(numYil.Maximum, k.Yil));
                if (k.Tarife >= 0 && k.Tarife < cmbTarife.Items.Count) cmbTarife.SelectedIndex = k.Tarife;
                chkTs825.Checked = k.Ts825Filtre;
                onayarYukleniyor = true;
                tb1.Value = Math.Max(tb1.Minimum, Math.Min(tb1.Maximum, k.Tb1));
                tb2.Value = Math.Max(tb2.Minimum, Math.Min(tb2.Maximum, k.Tb2));
                tb3.Value = Math.Max(tb3.Minimum, Math.Min(tb3.Maximum, k.Tb3));
                onayarYukleniyor = false;
                OncelikVurgula(k.Oncelik >= 0 && k.Oncelik < 3 ? k.Oncelik : -1);
            }
            finally { hazir = true; }
            butceSifirla = true;
            SecimiSifirla();
            if (y != null && p != null && d != null) { secY = y; secP = p; secD = d; }
            sekmeler.SelectedIndex = 0;
            Hesapla();
            if (y == null || p == null || d == null)
                MessageBox.Show("Bu teklifteki malzemelerden biri artık malzeme listesinde yok; önerilen paket gösteriliyor.", Marka.Ad, MessageBoxButtons.OK, MessageBoxIcon.Information);
            else if (sonTeklif is { } t && Math.Abs(t.sec.IlkYatirim - k.IlkYatirim) > 1)
                Bildir($"Teklif {k.Id} açıldı. Fiyatlar kayıttan bu yana değiştiği için ilk yatırım {TL(k.IlkYatirim)} yerine {TL(t.sec.IlkYatirim)}.");
            else Bildir($"Teklif {k.Id} açıldı.");
            Gunluk.Bilgi("Teklif açıldı", $"{k.Id} · {k.ProjeAdi}" + (y == null || p == null || d == null ? " · malzemelerden biri listede yok" : ""));
        }

        static void SecimYap(ComboBox c, double deger)
        {
            for (int i = 0; i < c.Items.Count; i++)
                if (c.Items[i] is Secim s && Math.Abs(s.Deger - deger) < 1e-9) { c.SelectedIndex = i; return; }
        }

        /// <summary>Basit metin giriş penceresi.</summary>
        string? MetinSor(string baslik, string etiket, string varsayilan)
        {
            using var f = new Form { Text = baslik, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, ClientSize = new Size(420, 132), Font = new Font("Segoe UI", 10f) };
            var lbl = new Label { Text = etiket, Location = new Point(16, 16), AutoSize = true };
            var txt = new TextBox { Text = varsayilan, Location = new Point(16, 42), Width = 388 };
            var tamam = new Button { Text = "Kaydet", DialogResult = DialogResult.OK, Location = new Point(228, 86), Width = 84, Height = 30 };
            var iptal = new Button { Text = "Vazgeç", DialogResult = DialogResult.Cancel, Location = new Point(320, 86), Width = 84, Height = 30 };
            f.Controls.AddRange(new Control[] { lbl, txt, tamam, iptal });
            f.AcceptButton = tamam; f.CancelButton = iptal;
            return f.ShowDialog(this) == DialogResult.OK && txt.Text.Trim().Length > 0 ? txt.Text.Trim() : null;
        }
    }
}
