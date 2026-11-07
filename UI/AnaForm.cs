using System.Drawing;
using System.Windows.Forms;
using EcoBuildss.Core;
using EcoBuildss.Depo;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Ana pencere. Akış bir yapı-enerji danışmanının teklif sürecini izler:
    /// 1) projeyi tanımla  2) önceliği seç  3) üç paketi karşılaştır  4) gerekirse malzemeyi özelleştir
    /// 5) optimizasyonla kalınlığı ve bütçeyi sına  6) teklifi veritabanına kaydet.
    /// Tüm kontroller kodla oluşturulur (Designer dosyası yok).
    /// </summary>
    public sealed partial class AnaForm : Form
    {
        static readonly Color Yesil = Color.FromArgb(31, 111, 74);
        static readonly Color YesilAcik = Color.FromArgb(226, 239, 231);
        static readonly Color Turuncu = Color.FromArgb(180, 84, 26);
        static readonly Color TuruncuAcik = Color.FromArgb(248, 232, 220);
        static readonly Color Zemin = Color.FromArgb(243, 244, 241);
        static readonly Color Gri = Color.FromArgb(93, 103, 99);

        readonly VeriDeposu? veri;
        readonly Veritabani? db;
        bool hazir, onayarYukleniyor;
        Malzeme? secY, secP, secD;          // binada gösterilen seçim
        (Secenek sec, Sonuc r, Secenek ekonomik, string paket, int yil)? sonTeklif;   // yazdırma için
        string projeMetni = "";

        // ---- Sol panel: proje ----
        readonly ComboBox cmbForm = Combo(), cmbYon = Combo(), cmbCam = Combo(), cmbDograma = Combo(), cmbProjeTuru = Combo(), cmbYakit = Combo();
        readonly CheckBox chkSogutma = new() { Text = "Klima ile soğutma var", AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        readonly NumericUpDown numArtis = new() { Minimum = -5, Maximum = 10, DecimalPlaces = 1, Increment = 0.5m, Value = 0, Width = 80 };
        readonly ComboBox cmbIl = Combo(), cmbBolge = Combo();
        string dbHatasi = "";
        readonly Label lblIlBilgi = Ipucu(), lblSinirlar = Ipucu();
        bool ilYukleniyor;
        const string OzelOlcuMetni = "Özel ölçü (en × boy × kat)";
        readonly FlowLayoutPanel ozelPanel = new() { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Visible = false, Margin = new Padding(0, 0, 0, 6) };
        readonly NumericUpDown numEn = Olcu(4, 80, 12), numBoy = Olcu(4, 80, 15), numKat = Olcu(1, 10, 2, 0);
        readonly NumericUpDown numYil = new() { Minimum = 1, Maximum = 50, Value = 20, Width = 80 };
        readonly ComboBox cmbTarife = Combo();
        readonly Label lblFormBilgi = Ipucu();

        // ---- Sol panel: öncelik (AHP ön ayarları) ----
        readonly Button[] oncelikDugme = new Button[3];
        int oncelik = 1;
        static readonly (string ad, string aciklama, int me, int mk, int ek)[] OnAyar =
        {
            ("Bütçe", "En düşük toplam maliyet öne çıkar.", -4, -4, 0),
            ("Dengeli", "Maliyet, enerji ve karbon birlikte gözetilir.", -1, -1, 0),
            ("Enerji ve karbon", "Düşük tüketim ve düşük karbon öne çıkar.", 2, 2, 0),
        };
        readonly Label lblOncelik = Ipucu();

        // ---- Gelişmiş ayarlar ----
        readonly Button btnGelismis = new() { Text = "Gelişmiş ayarlar  ▸", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(31, 111, 74), Cursor = Cursors.Hand, Margin = new Padding(0, 14, 0, 0) };
        readonly FlowLayoutPanel gelismis = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Visible = false, Margin = new Padding(0, 6, 0, 0) };
        readonly CheckBox chkTs825 = new() { Text = "Yalnızca TS 825'e uygun seçenekleri öner", AutoSize = true, Checked = true, Margin = new Padding(0, 8, 0, 0) };
        readonly TrackBar tb1 = Kaydirici(-1), tb2 = Kaydirici(-1), tb3 = Kaydirici(0);
        readonly Label lbl1 = Ipucu(), lbl2 = Ipucu(), lbl3 = Ipucu();
        readonly Label lblAgirlik = new() { AutoSize = true, MaximumSize = new Size(250, 0), Margin = new Padding(0, 8, 0, 2), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        readonly Label lblCR = new() { AutoSize = true, MaximumSize = new Size(250, 0), Font = new Font("Segoe UI", 8.5f) };

        // ---- Üst çubuk ve durum çubuğu ----
        readonly UstCubuk ust = new();
        readonly ToolStripStatusLabel durum = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        readonly StatusStrip durumCubugu = new() { SizingGrip = false, BackColor = Color.FromArgb(226, 239, 231), Font = new Font("Segoe UI", 9.5f) };

        // ---- Teklif sekmesi ----
        readonly TabControl sekmeler = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f) };
        readonly PaketKarti[] paket = { new() { Baslik = "Ekonomik" }, new() { Baslik = "Önerilen", Vurgulu = true }, new() { Baslik = "Yüksek performans" } };
        readonly BinaPanel bina = new() { Dock = DockStyle.Fill };
        readonly Label lblOzet = new() { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, Height = 22, ForeColor = Gri, Margin = new Padding(0, 8, 0, 4) };
        readonly List<MalzemeKarti> kartlar = new();
        readonly FlowLayoutPanel kartY = KartSirasi(), kartP = KartSirasi(), kartD = KartSirasi();
        readonly TabControl kartAlani = new() { Dock = DockStyle.Fill, Height = 172, Visible = false, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 6, 0, 0) };
        readonly Button btnOzellestir = new() { Text = "Malzemeleri özelleştir  ▾", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(31, 111, 74), BackColor = Color.White, Cursor = Cursors.Hand, Margin = new Padding(0, 8, 0, 0) };

        // ---- Keşif özeti (sağ panel) ----
        readonly Label lblDetayBaslik = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 12f), ForeColor = Color.FromArgb(29, 35, 33), MaximumSize = new Size(300, 0) };
        readonly Label lblDetayAlt = new() { AutoSize = true, ForeColor = Gri, Font = new Font("Segoe UI", 9f), MaximumSize = new Size(300, 0), Margin = new Padding(0, 2, 0, 10) };
        readonly TableLayoutPanel kesif = new() { AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 6) };
        readonly Label lblTs = Not();
        readonly Label lblFark = Not();
        readonly Label lblYangin = Not();
        readonly Label lblKapsam = new() { AutoSize = true, MaximumSize = new Size(280, 0), ForeColor = Color.FromArgb(93, 103, 99), Font = new Font("Segoe UI", 8.5f), Margin = new Padding(0, 6, 0, 0) };
        readonly Button btnKaydet = new() { Text = "Teklifi kaydet", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(31, 111, 74), BackColor = Color.White, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 8, 0, 0), Cursor = Cursors.Hand };
        readonly Button btnYazdir = new() { Text = "Teklifi yazdır / PDF", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(31, 111, 74), BackColor = Color.White, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 12, 0, 0), Cursor = Cursors.Hand };
        readonly Button btnUygula = new() { Text = "Önerilen paketi göster", AutoSize = true, BackColor = Color.FromArgb(31, 111, 74), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Padding = new Padding(8, 4, 8, 4), Margin = new Padding(0, 8, 0, 0), Cursor = Cursors.Hand };

        // ---- Tüm seçenekler ----
        readonly DataGridView grid = new();

        // Tekrar kullanılan yazı tipleri (her güncellemede yeni Font üretmek GDI kaynaklarını tüketir)
        static readonly Font FontKesif = new("Segoe UI", 9f), FontKesifKalin = new("Segoe UI", 9f, FontStyle.Bold), FontTabloKalin = new("Segoe UI", 9.5f, FontStyle.Bold);

        public AnaForm()
        {
            Text = Marka.Ad;
            Icon = Marka.Simge();
            Font = new Font("Segoe UI", 10f);
            BackColor = Zemin;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1440, 920);
            MinimumSize = new Size(1200, 780);
            WindowState = FormWindowState.Maximized;

            string veriKlasoru = Path.Combine(AppContext.BaseDirectory, "Veri");
            // Veri SQLite veritabanından okunur; veritabanı ilk çalıştırmada CSV dosyalarından oluşturulur.
            try
            {
                db = new Veritabani(VeritabaniYolu(veriKlasoru));
                veri = db.HazirlaVeYukle(veriKlasoru);
                Gunluk.Db = db;
                Gunluk.Bilgi("Uygulama açıldı", $"Sürüm {Application.ProductVersion}; veri: {veri.Kaynak}; {Environment.OSVersion}");
                FormClosed += (_, _) => Gunluk.Bilgi("Uygulama kapatıldı");
            }
            catch (Exception exDb)
            {
                db = null;
                dbHatasi = exDb.Message;
                try { veri = VeriDeposu.Yukle(veriKlasoru); }
                catch (Exception ex)
                {
                    MessageBox.Show("Veri okunamadı. 'Veri' klasöründe bina_tipleri.csv, malzemeler.csv ve parametreler.csv olmalı.\n\n" + ex.Message + "\n\nVeritabanı hatası: " + exDb.Message,
                        Marka.Ad, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            ArayuzKur();
            Shown += (_, _) => { if (veri != null) IlkAcilisRehberi(); if (db == null && veri != null) Bildir($"Veritabanı açılamadı ({dbHatasi}); teklif kaydı bu oturumda kullanılamıyor."); };
            if (veri != null) { KumelemeYap(); Doldur(); hazir = true; OncelikSec(1); KayitlariYenile(); }
        }

        // ================= Arayüz =================
        void ArayuzKur()
        {
            var kok = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 0, 12, 0) };
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(kok);
            // Durum çubuğu yalnız bir işlem bildirimi (kaydedildi, aktarıldı…) varken görünür
            durumCubugu.Items.Add(durum);
            durumCubugu.Visible = false;
            Controls.Add(durumCubugu);
            Controls.Add(ust);
            // Üst çubuk: komutlar sağdaki ayarlar (dişli) düğmesinin menüsünde
            var menu = ust.MenuKur();
            Aciklayici.SetToolTip(ust.Ayarlar, "Menü: teklif, malzeme, kaynaklar ve yardım");
            menu.Ekle("Teklifi kaydet", "Ctrl+S").Click += (_, _) => TeklifiKaydet();
            menu.Ekle("Teklifi yazdır / PDF", "Ctrl+P").Click += (_, _) => TeklifiYazdir();
            menu.Ekle("Excel'e aktar", "Ctrl+E").Click += (_, _) => ExcelAktar();
            menu.Ayrac();
            menu.Ekle("Malzeme yönetimi").Click += (_, _) =>
            {
                using var f = new MalzemeYonetimi(Path.Combine(AppContext.BaseDirectory, "Veri"));
                f.ShowDialog(this);
                if (f.Kaydedildi && MessageBox.Show("Değişikliklerin uygulanması için uygulama yeniden başlatılsın mı?", Marka.Ad, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Application.Restart();
            };
            menu.Ayrac();
            menu.Ekle("Veri kaynakları").Click += (_, _) => new BilgiFormu("Veri kaynakları", KaynakMetni + FiyatYasiMetni()).Show(this);
            menu.Ekle("Hesaplama yöntemi").Click += (_, _) => new BilgiFormu("Hesaplama yöntemi", YontemMetni).Show(this);
            menu.Ekle("Nasıl kullanılır?", "F1").Click += (_, _) => RehberGoster();

            kok.Controls.Add(SolSutun(), 0, 0);
            sekmeler.TabPages.Add(TeklifSekmesi());
            var sT = new TabPage("Tüm seçenekler") { BackColor = Color.White, Padding = new Padding(8) };
            GridKur();
            sT.Controls.Add(grid);
            sT.Controls.Add(new Label { Text = "Çift tıklayınca teklife alınır.", Dock = DockStyle.Top, Height = 26, ForeColor = Gri });
            AnalizSekmeleriniKur();                       // Gösterge paneli, Duyarlılık
            sekmeler.TabPages.Add(sGosterge);
            sekmeler.TabPages.Add(OptimizasyonSekmesi());
            sekmeler.TabPages.Add(BolgeselSekmesi());
            sekmeler.TabPages.Add(SiniflandirmaSekmesi());
            sekmeler.TabPages.Add(sDuyarlilik!);
            sekmeler.TabPages.Add(HaritaSekmesi());
            sekmeler.TabPages.Add(KarsilastirSekmesi());
            sekmeler.TabPages.Add(sT);
            sekmeler.TabPages.Add(KayitSekmesi());
            kok.Controls.Add(sekmeler, 1, 0);
            SekmeleriBicimle();

            cmbIl.SelectedIndexChanged += (_, _) =>
            {
                if (!hazir) return;
                // İl merkezinin TS 825 bölgesi kaynakta varsa otomatik seçilir; yoksa kullanıcı İZODER il föyünden seçer
                if (cmbIl.SelectedItem is Il il && il.Ts825Bolge is int b) { ilYukleniyor = true; SecimYap(cmbBolge, b); ilYukleniyor = false; }
                ProjeDegisti();
            };
            cmbBolge.SelectedIndexChanged += (_, _) => { if (hazir && !ilYukleniyor) ProjeDegisti(); };
            foreach (var cmb in new[] { cmbForm, cmbYon, cmbCam, cmbDograma, cmbProjeTuru, cmbYakit }) cmb.SelectedIndexChanged += (_, _) => { if (!hazir) return; ozelPanel.Visible = cmbForm.SelectedItem is string; ProjeDegisti(); };
            foreach (var n in new[] { numEn, numBoy, numKat }) n.ValueChanged += (_, _) => { if (hazir && cmbForm.SelectedItem is string) ProjeDegisti(); };
            numYil.ValueChanged += (_, _) => Hesapla();
            cmbTarife.SelectedIndexChanged += (_, _) => Hesapla();
            chkSogutma.CheckedChanged += (_, _) => { if (hazir) ProjeDegisti(); };
            numArtis.ValueChanged += (_, _) => Hesapla();
            btnYazdir.FlatAppearance.BorderColor = Color.FromArgb(31, 111, 74);
            btnYazdir.Click += (_, _) => TeklifiYazdir();
            btnKaydet.FlatAppearance.BorderColor = Color.FromArgb(31, 111, 74);
            btnKaydet.Click += (_, _) => TeklifiKaydet();
            chkTs825.CheckedChanged += (_, _) => { SecimiSifirla(); Hesapla(); };
            foreach (var tb in new[] { tb1, tb2, tb3 }) tb.ValueChanged += (_, _) => { if (!onayarYukleniyor) { OncelikVurgula(-1); Hesapla(); } };
            btnUygula.FlatAppearance.BorderSize = 0;
            btnUygula.Click += (_, _) => { SecimiSifirla(); Hesapla(); };
            btnGelismis.FlatAppearance.BorderSize = 0;
            btnGelismis.Click += (_, _) => { gelismis.Visible = !gelismis.Visible; btnGelismis.Text = gelismis.Visible ? "Gelişmiş ayarlar  ▾" : "Gelişmiş ayarlar  ▸"; };
            btnOzellestir.FlatAppearance.BorderColor = Color.FromArgb(200, 210, 204);
            btnOzellestir.Click += (_, _) => { kartAlani.Visible = !kartAlani.Visible; btnOzellestir.Text = kartAlani.Visible ? "Malzeme seçimini kapat  ▴" : "Malzemeleri özelleştir  ▾"; };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                paket[i].Secildi += (_, _) =>
                {
                    var s = paket[k].Secenek; if (s == null) return;
                    secY = s.Yalitim; secP = s.Cam; secD = s.Duvar; Hesapla();
                };
            }
            grid.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count || grid.Rows[e.RowIndex].Tag is not Secenek s) return;
                secY = s.Yalitim; secP = s.Cam; secD = s.Duvar;
                sekmeler.SelectedIndex = 0;
                Hesapla();
            };
        }

        Control SolSutun()
        {
            var sol = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.White, Padding = new Padding(20, 18, 8, 16), Margin = new Padding(0, 0, 12, 0) };

            sol.Controls.Add(Bolum("PROJE"));
            sol.Controls.Add(Alan("Proje türü", cmbProjeTuru, "ProjeTuru"));
            sol.Controls.Add(Alan("İl", cmbIl, "DG"));
            sol.Controls.Add(Alan("TS 825 iklim bölgesi", cmbBolge, "TS825"));
            lblIlBilgi.Margin = new Padding(0, 0, 0, 8);
            sol.Controls.Add(lblIlBilgi);
            sol.Controls.Add(Alan("Bina", cmbForm));
            foreach (var (ad, n) in new[] { ("En (m)", numEn), ("Boy (m)", numBoy), ("Kat", numKat) })
            {
                var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 10, 0) };
                p.Controls.Add(new Label { Text = ad, AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = Gri, Margin = new Padding(0, 0, 0, 2) });
                n.Margin = new Padding(0);
                p.Controls.Add(n);
                ozelPanel.Controls.Add(p);
            }
            sol.Controls.Add(ozelPanel);
            sol.Controls.Add(Alan("Ön cephe yönü", cmbYon));
            sol.Controls.Add(Alan("Pencere oranı", cmbCam, "Pencere"));
            sol.Controls.Add(Alan("Pencere doğraması", cmbDograma, "Dograma"));
            sol.Controls.Add(Alan("Isıtma", cmbYakit, "Yakit"));
            sol.Controls.Add(chkSogutma);

            sol.Controls.Add(Ayrac());
            var bOnc = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
            var bOncL = Bolum("ÖNCELİĞİNİZ"); bOncL.Margin = new Padding(0);
            bOnc.Controls.Add(bOncL); bOnc.Controls.Add(Soru("Oncelik"));
            sol.Controls.Add(bOnc);
            var grup = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var b = new Button { Text = OnAyar[i].ad, Width = 250, Height = 34, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), Cursor = Cursors.Hand, Margin = new Padding(0, 0, 0, 6), Font = new Font("Segoe UI", 10f) };
                b.Click += (_, _) => OncelikSec(k);
                oncelikDugme[i] = b;
                grup.Controls.Add(b);
            }
            sol.Controls.Add(grup);

            sol.Controls.Add(btnGelismis);
            gelismis.Controls.Add(Alan("Analiz süresi (yıl)", numYil, "Sure"));
            gelismis.Controls.Add(Alan("Tarife kademesi (doğalgaz ve elektrik)", cmbTarife, "Tarife"));
            gelismis.Controls.Add(Alan("Enerji fiyatı reel artışı (%/yıl)", numArtis, "Artis"));
            gelismis.Controls.Add(chkTs825);
            gelismis.Controls.Add(lblSinirlar);
            var bas = Ipucu("Öncelik ağırlıkları (AHP ikili karşılaştırma)");
            bas.Font = new Font("Segoe UI", 9f, FontStyle.Bold); bas.ForeColor = Color.FromArgb(29, 35, 33); bas.Margin = new Padding(0, 12, 0, 0);
            gelismis.Controls.Add(bas);
            var tAhp = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = new Padding(0) };
            KarsilastirmaEkle(tAhp, "Maliyet", "Enerji", tb1, lbl1);
            KarsilastirmaEkle(tAhp, "Maliyet", "Karbon", tb2, lbl2);
            KarsilastirmaEkle(tAhp, "Enerji", "Karbon", tb3, lbl3);
            gelismis.Controls.Add(tAhp);
            gelismis.Controls.Add(lblAgirlik);
            gelismis.Controls.Add(lblCR);
            sol.Controls.Add(gelismis);
            return sol;
        }

        TabPage TeklifSekmesi()
        {
            var s = new TabPage("Teklif") { BackColor = Zemin, Padding = new Padding(10) };
            var ana = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            ana.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ana.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 336));

            var orta = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = new Padding(0) };
            orta.RowStyles.Add(new RowStyle(SizeType.Absolute, 182));   // paketler
            orta.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // özet satırı
            orta.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // bina
            orta.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // özelleştir düğmesi
            orta.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // malzeme kartları (sekmeli şerit)

            var paketler = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0) };
            for (int i = 0; i < 3; i++) { paketler.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f)); paket[i].Dock = DockStyle.Fill; paketler.Controls.Add(paket[i], i, 0); }
            paket[2].Margin = new Padding(0);
            foreach (var pk in paket)
                pk.YukseklikDegisti += (_, _) =>
                {
                    float h = Math.Max(182, paket.Max(x => x.IstenenYukseklik) + 4);
                    if (Math.Abs(orta.RowStyles[0].Height - h) > 0.5f) orta.RowStyles[0].Height = h;
                };
            orta.Controls.Add(paketler, 0, 0);
            orta.Controls.Add(lblOzet, 0, 1);
            orta.Controls.Add(bina, 0, 2);
            orta.Controls.Add(btnOzellestir, 0, 3);
            foreach (var (ad, sira) in new[] { ("Dış cephe yalıtımı", kartY), ("Pencere camı", kartP), ("Duvar malzemesi", kartD) })
            {
                var tp = new TabPage(ad) { BackColor = Color.White, Padding = new Padding(8, 8, 8, 0) };
                tp.Controls.Add(sira);
                kartAlani.TabPages.Add(tp);
            }
            orta.Controls.Add(kartAlani, 0, 4);
            ana.Controls.Add(orta, 0, 0);

            var sag = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.White, Padding = new Padding(16, 14, 10, 14), Margin = new Padding(12, 0, 0, 0) };
            sag.Controls.Add(Baslik("KEŞİF ÖZETİ"));
            sag.Controls.Add(lblDetayBaslik);
            sag.Controls.Add(lblDetayAlt);
            sag.Controls.Add(lblNeden);
            kesif.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            kesif.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            kesif.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            sag.Controls.Add(kesif);
            sag.Controls.Add(lblTs);
            sag.Controls.Add(lblYangin);
            sag.Controls.Add(lblFark);
            sag.Controls.Add(btnUygula);
            var dugmeler = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
            btnKaydet.Margin = new Padding(0, 8, 8, 0);
            dugmeler.Controls.Add(btnKaydet);
            dugmeler.Controls.Add(btnYazdir);
            sag.Controls.Add(dugmeler);
            var btnExcel = new Button { Text = "Excel'e aktar", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(31, 111, 74), BackColor = Color.White, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 8, 0, 0), Cursor = Cursors.Hand };
            btnExcel.FlatAppearance.BorderColor = Color.FromArgb(31, 111, 74);
            btnExcel.Click += (_, _) => ExcelAktar();
            sag.Controls.Add(btnExcel);
            ana.Controls.Add(sag, 1, 0);
            s.Controls.Add(ana);
            return s;
        }

        void GridKur()
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Zemin;
            grid.DefaultCellStyle.SelectionBackColor = YesilAcik;
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;

            (string ad, string baslik, bool sayi, float agirlik)[] kolonlar =
            {
                ("sira", "#", true, 4), ("yal", "Yalıtım", false, 10), ("cam", "Cam", false, 12), ("duv", "Duvar", false, 8),
                ("u", "Duvar U", true, 7), ("ts", "TS 825", false, 9), ("yat", "İlk yatırım", true, 11),
                ("isi", "Yıllık ısıtma", true, 10), ("gid", "Yıllık enerji gideri", true, 10), ("agr", "Duvar ağırlığı", true, 8), ("top", "Ömür boyu maliyet", true, 11), ("co2", "Toplam CO₂", true, 9), ("skor", "Puan", true, 6)
            };
            foreach (var k in kolonlar)
            {
                var col = new DataGridViewTextBoxColumn { Name = k.ad, HeaderText = k.baslik, FillWeight = k.agirlik, SortMode = DataGridViewColumnSortMode.NotSortable, MinimumWidth = k.ad switch { "sira" => 34, "yal" or "cam" => 112, "duv" => 74, "skor" => 46, "u" => 58, "ts" => 84, _ => 92 } };
                if (k.sayi) col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                grid.Columns.Add(col);
            }
        }

        void Doldur()
        {
            // Teklif yalnızca hem iklim verisi hem il merkezi TS 825 bölgesi kaynakla belli olan illerde verilir
            foreach (var il in veri!.Iller.Where(i => i.Ts825Bolge.HasValue).OrderBy(i => i.Ad, StringComparer.Create(Ortak.TR, false))) cmbIl.Items.Add(il);
            cmbIl.SelectedItem = cmbIl.Items.Cast<Il>().FirstOrDefault(i => i.Ad == "Malatya") ?? cmbIl.Items[0];
            foreach (int b in veri.Bolgeler.Select(b => b.No)) cmbBolge.Items.Add(new Secim($"{b}. bölge", b));
            SecimYap(cmbBolge, ((Il)cmbIl.SelectedItem!).Ts825Bolge!.Value);
            foreach (var f in veri.Formlar) cmbForm.Items.Add(f);
            cmbForm.Items.Add(OzelOlcuMetni);
            cmbYon.Items.AddRange(new object[] { new Secim("Kuzey", 2), new Secim("Doğu", 3), new Secim("Güney", 4), new Secim("Batı", 5) });
            cmbCam.Items.AddRange(new object[] { new Secim("%0 (penceresiz)", 0), new Secim("%10", 0.10), new Secim("%25", 0.25), new Secim("%40", 0.40) });
            cmbForm.SelectedIndex = 0;
            cmbYon.SelectedIndex = 2;
            cmbCam.SelectedIndex = 2;
            cmbProjeTuru.Items.AddRange(new object[] { "Yeni bina", "Mevcut bina (yenileme)" });
            cmbProjeTuru.SelectedIndex = 0;
            cmbYakit.Items.Add(new Secim("Doğalgaz · kombi", 0) { Kod = "DG" });
            foreach (var y in veri.Yakitlar) cmbYakit.Items.Add(new Secim($"{y.Ad.Split('(')[0].Trim()} · {y.Isitici.ToLower(Ortak.TR)}", 0) { Kod = y.Id });
            cmbYakit.SelectedIndex = 0;
            // Doğrama tipleri TS 825:2024 Ek A.3 çizelgesinden (ts825_pencere.csv) gelir
            foreach (var d in veri.Dogramalar) cmbDograma.Items.Add(d);
            cmbDograma.SelectedItem = veri.Dogramalar.Contains(Girdi.VarsayilanDograma) ? Girdi.VarsayilanDograma : veri.Dogramalar[0];
            // Tarifeler parametreler.csv'den okunur (E1: Kademe-1, E2: Kademe-2 / aylık limit aşımı)
            cmbTarife.Items.AddRange(new object[]
            {
                new Secim($"Kademe-1 · gaz {veri.P("E1").ToString("N3", Ortak.TR)} TL/Sm³ · elektrik {veri.P("EL1").ToString("N2", Ortak.TR)} TL/kWh", veri.P("E1")),
                new Secim($"Kademe-2 · gaz {veri.P("E2").ToString("N3", Ortak.TR)} TL/Sm³ · elektrik {veri.P("EL2").ToString("N2", Ortak.TR)} TL/kWh", veri.P("E2")),
            });
            cmbTarife.SelectedIndex = 0;

            // Kartlar türe, sonra malzeme adına ve kalınlığa göre sıralanır (EPS 5, EPS 8, ... Taş yünü 5, 8, 10)
            foreach (var m in veri.Malzemeler.Where(m => m.Tur is 'Y' or 'P' or 'D').OrderBy(m => m.Tur).ThenBy(m => m.Aile).ThenBy(m => m.Kalinlik))
            {
                var kart = new MalzemeKarti(m);
                kart.Secildi += (_, _) =>
                {
                    if (m.Tur == 'Y') secY = m; else if (m.Tur == 'P') secP = m; else secD = m;
                    Hesapla();
                };
                new ToolTip().SetToolTip(kart, $"{m.Ad}\n{m.FiyatHesabi}\nFiyat kaynağı: {m.FiyatKaynagi} ({m.FiyatTarihi})");
                kartlar.Add(kart);
                (m.Tur == 'Y' ? kartY : m.Tur == 'P' ? kartP : kartD).Controls.Add(kart);
            }
        }

        // ================= Öncelik =================
        void OncelikSec(int i)
        {
            onayarYukleniyor = true;
            tb1.Value = OnAyar[i].me; tb2.Value = OnAyar[i].mk; tb3.Value = OnAyar[i].ek;
            onayarYukleniyor = false;
            OncelikVurgula(i);
            SecimiSifirla();
            Hesapla();
        }

        void OncelikVurgula(int i)
        {
            oncelik = i;
            for (int k = 0; k < 3; k++)
            {
                bool on = k == i;
                oncelikDugme[k].BackColor = on ? YesilAcik : Color.White;
                oncelikDugme[k].ForeColor = on ? Yesil : Color.FromArgb(29, 35, 33);
                oncelikDugme[k].FlatAppearance.BorderColor = on ? Yesil : Color.FromArgb(214, 219, 214);
                oncelikDugme[k].FlatAppearance.BorderSize = on ? 2 : 1;
                oncelikDugme[k].Text = (on ? "●  " : "○  ") + OnAyar[k].ad;
            }
            lblOncelik.Text = i >= 0 ? OnAyar[i].aciklama : "Özel ağırlıklar kullanılıyor (gelişmiş ayarlar).";
        }

        void SecimiSifirla() { secY = secP = secD = null; }

        // ================= Hesap =================
        Girdi GirdiOku(bool filtre)
        {
            return new Girdi
            {
                Form = SeciliForm(),
                Yon = (int)((Secim)cmbYon.SelectedItem!).Deger,
                Il = cmbIl.SelectedItem as Il,
                Bolge = (int)((Secim)cmbBolge.SelectedItem!).Deger,
                CamOrani = ((Secim)cmbCam.SelectedItem!).Deger,
                Dograma = (string)cmbDograma.SelectedItem!,
                Mevcut = cmbProjeTuru.SelectedIndex == 1,
                Yakit = ((Secim)cmbYakit.SelectedItem!).Kod,
                Kademe = cmbTarife.SelectedIndex == 1 ? 2 : 1,
                Sogutma = chkSogutma.Checked,
                EnerjiArtisi = (double)numArtis.Value / 100,
                Yil = (int)numYil.Value,
                SadeceTs825 = filtre,
                MaliyetEnerji = KararMotoru.Saaty(tb1.Value),
                MaliyetKarbon = KararMotoru.Saaty(tb2.Value),
                EnerjiKarbon = KararMotoru.Saaty(tb3.Value),
                // TL/Sm³ ÷ kWh/Sm³ (E3 × 0,001163; EPDK üst ısıl değeri) = TL/kWh
                GazTLkWh = veri!.GazTLkWh(((Secim)cmbTarife.SelectedItem!).Deger),
            };
        }

        /// <summary>Seçili bina tipi ya da kullanıcının girdiği en × boy × kat ölçüleri.</summary>
        BinaFormu SeciliForm() => cmbForm.SelectedItem is BinaFormu f ? f
            : BinaFormu.OzelOlcu((double)numEn.Value, (double)numBoy.Value, (int)numKat.Value, veri!.P("M7"));

        /// <summary>Bina, yön ya da pencere oranı değişti: seçim ve bütçe sıfırlanır.</summary>
        void ProjeDegisti() { SecimiSifirla(); butceSifirla = true; Hesapla(); }

        void Hesapla()
        {
            if (!hazir || veri == null) return;
            var tr = Ortak.TR;
            var gF = GirdiOku(chkTs825.Checked);
            var r = KararMotoru.Hesapla(veri, gF);
            var rT = chkTs825.Checked ? KararMotoru.Hesapla(veri, GirdiOku(false)) : r;   // filtresiz tüm seçenekler
            // Seçilen doğrama için TS 825 Ek A.3'te değeri olmayan cam (ör. alüminyum doğramada üçlü cam) seçenek olamaz
            foreach (var k in kartlar.Where(k => k.Malzeme.Tur == 'P')) k.Visible = r.PencereU.ContainsKey(k.Malzeme.Id);
            if (secP != null && !r.PencereU.ContainsKey(secP.Id)) { secY = secP = secD = null; }
            // Yenilemede duvar seçilmez; yangın yönetmeliğince çıkarılan yalıtım kartları da gizlenir
            foreach (var k in kartlar.Where(k => k.Malzeme.Tur == 'D')) k.Visible = !gF.Mevcut;
            foreach (var k in kartlar.Where(k => k.Malzeme.Tur == 'Y')) k.Visible = KararMotoru.YanginaUygun(veri, r, k.Malzeme);
            if (secY != null && !KararMotoru.YanginaUygun(veri, r, secY)) { secY = secP = secD = null; }
            if (gF.Mevcut && secD != null && rT.Tum.All(x => x.Duvar != secD)) secD = null;
            var liste = r.Siralama.Count > 0 ? r.Siralama : rT.Siralama;
            var form = gF.Form;
            double gazTL = r.GazTL, kdv = veri.P("KDV");
            int katSayisi = form.KatSayisi(veri.P("M7"));

            // Sol panel metinleri
            var secIl = cmbIl.SelectedItem as Il;
            string kumeAdi = bolgeselSonuc != null && secIl != null && bolgeselSonuc.Kumeler.Any(k => k.Uyeler.Contains(secIl)) ? " · " + bolgeselSonuc.KumesiNe(secIl).Ad : "";
            bool bolgeKaynakli = secIl?.Ts825Bolge == r.BolgeNo;
            lblIlBilgi.Text = $"Isıtma {r.DG.ToString("N0", tr)} · soğutma {(secIl?.SogutmaDG ?? 0).ToString("N0", tr)} derece-gün{kumeAdi}\n" +
                (bolgeKaynakli ? $"İl merkezi {r.BolgeNo}. bölge ({secIl!.BolgeKaynagi})"
                 : $"⚠ İl merkezi {secIl?.Ts825Bolge}. bölgede; {r.BolgeNo}. bölge seçili. Yalnızca proje ilçesi bu bölgedeyse doğrudur (İZODER il föyü).");
            lblIlBilgi.ForeColor = Turuncu;
            lblIlBilgi.Visible = !bolgeKaynakli;   // yalnız il merkezinin bölgesinden farklı bölge seçilince uyarı
            lblSinirlar.Text = $"TS 825:2024 · {r.BolgeNo}. bölge: duvar U ≤ {r.SinirDuvar.ToString("0.00", tr)}, pencere U ≤ {r.SinirPencere.ToString("0.0", tr)}, çatı U ≤ {r.SinirCati.ToString("0.00", tr)}, zemin U ≤ {r.SinirZemin.ToString("0.00", tr)} W/m²K";
            lblFormBilgi.Text = $"Çatı ve zemin {form.CatiAlani.ToString("N0", tr)} m² · {katSayisi} kat · kullanım {r.KullanimAlani.ToString("N0", tr)} m²\n" +
                                $"Dış duvar {form.DuvarAlani.ToString("N0", tr)} m² · yükseklik {form.Yukseklik.ToString("0.#", tr)} m · hacim {r.Hacim.ToString("N0", tr)} m³";
            lbl1.Text = KarsilastirmaMetni(tb1.Value, "Maliyet", "Enerji");
            lbl2.Text = KarsilastirmaMetni(tb2.Value, "Maliyet", "Karbon");
            lbl3.Text = KarsilastirmaMetni(tb3.Value, "Enerji", "Karbon");
            lblAgirlik.Text = $"Maliyet %{r.Agirlik[0] * 100:0} · Enerji %{r.Agirlik[1] * 100:0} · Karbon %{r.Agirlik[2] * 100:0}";
            bool tutarli = r.CR <= 0.10;
            lblCR.Text = tutarli ? $"✓ Tutarlı (tutarlılık oranı {r.CR.ToString("0.00", tr)})" : $"Karşılaştırmalar çelişiyor (tutarlılık oranı {r.CR.ToString("0.00", tr)}).";
            lblCR.ForeColor = tutarli ? Gri : Turuncu;

            // --- Üç paket: iyi / daha iyi / en iyi ---
            var oneri = liste[0];
            var ekonomik = liste.OrderBy(s => s.IlkYatirim).ThenByDescending(s => s.Skor).First();
            var premium = liste.OrderBy(s => s.YillikIsitmaKWh).ThenBy(s => s.IlkYatirim).First();
            var paketSecenek = new[] { ekonomik, oneri, premium };
            for (int i = 0; i < 3; i++)
            {
                var s = paketSecenek[i];
                paket[i].Secenek = s;
                paket[i].YillikGider = s.YillikGiderTL;
                var kp = Kpi.Hesapla(s, r, ekonomik, r.EnerjiIskonto, gF.Yil);
                paket[i].EkYatirim = kp.EkYatirim; paket[i].Tasarruf = kp.Tasarruf;
                paket[i].GeriOdemeVar = kp.GeriOdemeVar;
                paket[i].GeriOdeme = kp.GeriOdemeYil;
                paket[i].AnalizSuresi = gF.Yil;
                paket[i].Rozet = i == 1 ? "EN YÜKSEK PUAN" : (Ayni(s, oneri) ? "ÖNERİLENLE AYNI" : "");
                paket[i].Invalidate();
            }

            // --- Seçim (varsayılan: önerilen paket) ---
            if (secY == null || secP == null || secD == null) { secY = oneri.Yalitim; secP = oneri.Cam; secD = oneri.Duvar; }
            var sec = rT.Tum.FirstOrDefault(s => s.Yalitim == secY && s.Cam == secP && s.Duvar == secD) ?? oneri;
            for (int i = 0; i < 3; i++) paket[i].Secili = Ayni(paketSecenek[i], sec);

            foreach (var k in kartlar)
            {
                k.Secili = k.Malzeme == secY || k.Malzeme == secP || k.Malzeme == secD;
                k.Onerilen = k.Malzeme == oneri.Yalitim || k.Malzeme == oneri.Cam || k.Malzeme == oneri.Duvar;
                if (k.Secili && k.Parent is FlowLayoutPanel fp) fp.ScrollControlIntoView(k);
            }

            bina.Form = form; bina.Yon = gF.Yon; bina.CamOrani = gF.CamOrani;
            bina.Yalitim = secY; bina.Cam = secP; bina.Duvar = secD;
            bina.DuvarU = sec.DuvarU; bina.HBaz = r.HBaz; bina.HYeni = sec.H;
            bina.SinirU = r.SinirDuvar; bina.KatYuksekligi = veri.P("M7"); bina.CatiU = r.CatiU;
            bina.Invalidate();

            lblOzet.Text = $"Opak duvar {r.OpakDuvar.ToString("N1", tr)} m²  ·  pencere {r.CamAlani.ToString("N1", tr)} m²  ·  çatı ve zemin {r.CatiAlani.ToString("N1", tr)} m²  ·  " +
                           $"{(chkTs825.Checked ? $"TS 825'e uygun: {r.Siralama.Count} / {r.ToplamKombinasyon} seçenek" : $"{rT.Siralama.Count} seçenek")}";
            bool uygunYok = chkTs825.Checked && r.Siralama.Count == 0;
            if (uygunYok)
            {
                // Hangi eleman hiçbir seçenekte sınırı sağlamıyor: kullanıcıya kesin nedeni söyle
                var neden = new List<string>();
                double minDuvar = rT.Tum.Min(x => x.DuvarU), minPencere = rT.Tum.Min(x => x.PencereU);
                if (minDuvar > r.SinirDuvar + 1e-12) neden.Add($"en iyi duvar U {minDuvar.ToString("0.00", tr)} > {r.SinirDuvar.ToString("0.00", tr)}");
                if (r.CamAlani > 0 && minPencere > r.SinirPencere + 1e-12) neden.Add($"en iyi pencere U {minPencere.ToString("0.00", tr)} > {r.SinirPencere.ToString("0.00", tr)}");
                if (r.CatiU > r.SinirCati + 1e-12) neden.Add($"çatı U {r.CatiU.ToString("0.00", tr)} > {r.SinirCati.ToString("0.00", tr)}");
                if (r.ZeminU > r.SinirZemin + 1e-12) neden.Add($"zemin U {r.ZeminU.ToString("0.00", tr)} > {r.SinirZemin.ToString("0.00", tr)}");
                lblOzet.Text = $"⚠ {r.BolgeNo}. bölgede listedeki malzemelerle TS 825'e uyan seçenek yok ({string.Join("; ", neden)}). Tüm seçenekler gösteriliyor.";
            }
            else if (r.CamDuvariAsiyor)
                lblOzet.Text = "⚠ Seçilen pencere oranı bu binanın dış duvar alanından büyük; pencere alanı duvar alanıyla sınırlandı. Daha düşük bir pencere oranı seçin.";
            lblOzet.ForeColor = Turuncu;
            lblOzet.Visible = uygunYok || r.CamDuvariAsiyor;   // yalnız uyarı varsa

            // --- Keşif özeti ---
            string paketAdi = Ayni(sec, oneri) ? "Önerilen paket" : Ayni(sec, ekonomik) ? "Ekonomik paket" : Ayni(sec, premium) ? "Yüksek performans paketi" : "Özel seçim";
            int sira = liste.FindIndex(x => Ayni(x, sec)) + 1;
            lblDetayBaslik.Text = paketAdi;
            lblDetayAlt.Text = sira > 0 ? $"{liste.Count} seçenek içinde {sira}. sırada · puan {(liste[sira - 1].Skor * 100).ToString("0", tr)}/100" : "TS 825'e uygun seçenekler listesinde değil";
            GerekceYaz(sec, liste, r, oneri, ekonomik, premium);
            KesifDoldur(sec, r, gazTL, gF.Yil, ekonomik, kdv);
            lblKapsam.Text = "Kapsam: duvar malzemesi, mantolama (uygulanmış sistem fiyatı: levha, yapıştırıcı, dübel, file, sıva ve işçilik dahil), pencere camı, çatı ve zemin yalıtım malzemesi. " +
                             $"Duvar örgüsü, cam, çatı ve zemin kalemleri malzeme fiyatıdır; bunların işçiliği ve nakliye dahil değildir. Karşılaştırma KDV hariç tutarlarla yapılır, KDV (%{(kdv * 100).ToString("0", tr)}) ayrıca gösterilir." +
                             " Pencere doğraması her seçenekte aynı olduğundan fiyata katılmaz." +
                             (sec.Yalitim.FiyatHesaplanan ? $" {sec.Yalitim.KisaAd} mantolama fiyatı hesaplanmıştır: aynı ailenin 10 cm uygulanmış fiyatı + aynı ürünün levha fiyat farkı." : "") +
                             (r.Cati.FiyatHesaplanan ? $" {r.BolgeNo}. bölgenin çatı sınırı (U ≤ {r.SinirCati.ToString("0.00", tr)}) için çatıya çift kat şilte konur (fiyat: 10 cm şilte fiyatının iki katı)." : "");
            lblTs.Text = Ts825Metni(sec, r, out bool tsUygun);
            if (tsUygun) lblTs.Text = $"✓ TS 825:2024 U sınırlarına uygun ({r.BolgeNo}. bölge)";
            NotRenk(lblTs, tsUygun);
            lblYangin.Text = sec.YanginNotu + (r.YanginElenen > 0 ? $"{(sec.YanginNotu.Length > 0 ? "\n" : "")}Bina {r.BinaYuksekligi.ToString("0.#", tr)} m (> {veri.P("B1").ToString("0.0#", tr)} m): cephe sistemi en az A2-s1,d0 olmalı; EPS seçenekleri çıkarıldı (Md. 27/1)." : "");
            lblYangin.Visible = lblYangin.Text.Length > 0;
            NotRenk(lblYangin, false);

            btnUygula.Visible = !Ayni(oneri, sec);
            if (Ayni(oneri, sec))
            {
                lblFark.Visible = false;
            }
            else
            {
                double dY = sec.IlkYatirim - oneri.IlkYatirim, dE = sec.YillikGiderTL - oneri.YillikGiderTL, dL = sec.ToplamMaliyet - oneri.ToplamMaliyet, dC = (sec.ToplamCO2Kg - oneri.ToplamCO2Kg) / 1000;
                lblFark.Visible = true;
                lblFark.Text = $"Önerilen pakete göre:\nilk yatırım {Isaret(dY)}{TL(Math.Abs(dY))}\nyıllık enerji gideri {Isaret(dE)}{TL(Math.Abs(dE))}\nömür boyu maliyet {Isaret(dL)}{TL(Math.Abs(dL))}\n{gF.Yil} yıllık CO₂ {Isaret(dC)}{Math.Abs(dC).ToString("N1", tr)} t";
                NotRenk(lblFark, dY <= 0 && dE <= 0);
            }

            projeMetni = $"{(form.Ozel ? $"{form.En!.Value.ToString("0.#", tr)} × {form.Boy!.Value.ToString("0.#", tr)} m, " : $"Tip {form.No}, ")}{katSayisi} katlı bina, {r.KullanimAlani.ToString("N0", tr)} m² kullanım alanı  ·  {r.IlAdi}, TS 825 {r.BolgeNo}. bölge  ·  ön cephe {((Secim)cmbYon.SelectedItem!).Ad.ToLower(tr)}  ·  pencere oranı {((Secim)cmbCam.SelectedItem!).Ad}, {r.Dograma.ToLower(tr)}";
            sonTeklif = (sec, r, ekonomik, paketAdi, gF.Yil);

            AnalizleriGuncelle(r, rT, sec, oneri, gF);
            OptimizasyonGuncelle(r, rT, sec, oneri, gF);
            if (sekmeler.SelectedTab == sBolgesel) BolgeselGuncelle();
            if (sekmeler.SelectedTab == sSinif) SiniflandirmaGuncelle();
            if (sekmeler.SelectedTab == sHarita) HaritaGuncelle();

            // --- Tüm seçenekler (uygun olanlar önce, puana göre) ---
            // Puan, önerinin hesaplandığı listeden (filtre açıksa yalnızca uygun seçenekler arasındaki TOPSIS) alınır;
            // o listede olmayan seçenekler puanlanmamıştır ve sona yazılır.
            var puan = liste.Select((x, i) => (k: x.Kod, x.Skor, i)).ToDictionary(t => t.k, t => (t.Skor, t.i));
            (double Skor, int i)? Puan(Secenek x) => puan.TryGetValue(x.Kod, out var p) ? p : null;
            var tum = rT.Siralama.OrderBy(s => Puan(s)?.i ?? int.MaxValue).ThenByDescending(s => s.Skor).ToList();
            sonTum = tum.Select(x => (x, Puan(x) is { } pp ? (double?)pp.Skor : null)).ToList();
            grid.Rows.Clear();
            for (int i = 0; i < tum.Count; i++)
            {
                var s = tum[i];
                int idx = grid.Rows.Add(i + 1, s.Yalitim.KisaAd, s.Cam.KisaAd, s.DuvarAdi,
                    s.DuvarU.ToString("0.00", tr), s.Ts825Uygun ? "Uygun" : "Uygun değil",
                    TL(s.IlkYatirim), s.YillikIsitmaKWh.ToString("N0", tr) + " kWh", TL(s.YillikGiderTL), s.DuvarAgirlikTon.ToString("N0", tr) + " t", TL(s.ToplamMaliyet),
                    (s.ToplamCO2Kg / 1000).ToString("N1", tr) + " t", Puan(s) is { } ps ? (ps.Skor * 100).ToString("0", tr) : "Puanlanmaz");
                var row = grid.Rows[idx];
                row.Tag = s;
                if (!s.Ts825Uygun && chkTs825.Checked) row.DefaultCellStyle.ForeColor = Color.FromArgb(160, 166, 162);
                row.Cells["ts"].Style.ForeColor = s.Ts825Uygun ? Yesil : Turuncu;
                if (Ayni(s, oneri)) row.DefaultCellStyle.BackColor = YesilAcik;
                if (Ayni(s, sec)) row.DefaultCellStyle.Font = FontTabloKalin;
            }
            grid.ClearSelection();
        }

        /// <summary>Teklif föyü gibi kalem kalem keşif: metraj ve tutar.</summary>
        void KesifDoldur(Secenek s, Sonuc r, double gazTL, int yil, Secenek ekonomik, double kdv)
        {
            var tr = Ortak.TR;
            kesif.SuspendLayout();
            KontrolleriTemizle(kesif);
            void Satir(string a, string b, string c, bool kalin = false, bool ayrac = false, string? terim = null)
            {
                var f = kalin ? FontKesifKalin : FontKesif;
                var pad = new Padding(0, ayrac ? 8 : 2, 0, 2);
                if (terim == null) kesif.Controls.Add(new Label { Text = a, AutoSize = true, Font = f, Margin = pad, MaximumSize = new Size(116, 0) });
                else
                {
                    var fl = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = pad, Padding = new Padding(0) };
                    fl.Controls.Add(new Label { Text = a, AutoSize = true, Font = f, Margin = new Padding(0), MaximumSize = new Size(96, 0) });
                    var q = Soru(terim); q.Margin = new Padding(2, 1, 0, 0); fl.Controls.Add(q);
                    kesif.Controls.Add(fl);
                }
                kesif.Controls.Add(new Label { Text = b, AutoSize = false, Width = 72, Height = 18, Font = f, Margin = pad, TextAlign = ContentAlignment.TopRight, ForeColor = Gri });
                kesif.Controls.Add(new Label { Text = c, AutoSize = false, Width = 82, Height = 18, Font = f, Margin = pad, TextAlign = ContentAlignment.TopRight });
            }
            string M2(double a) => a.ToString("N0", tr) + " m²";
            Satir("Kalem", "Metraj", "Tutar", true);
            if (!r.Mevcut) Satir(s.DuvarAdi + " duvar", M2(r.OpakDuvar), TL(r.OpakDuvar * s.Duvar.FiyatM2));
            Satir("Mantolama " + s.Yalitim.KisaAd, M2(r.OpakDuvar), TL(r.OpakDuvar * s.Yalitim.FiyatM2));
            if (s.YanginEkMaliyet > 0) Satir("Yangın şeridi (A1 farkı)", M2(r.YanginSeridiM2), TL(s.YanginEkMaliyet));
            if (r.CamAlani > 0) Satir(s.Cam.KisaAd, M2(r.CamAlani), TL(r.CamAlani * s.Cam.FiyatM2));
            Satir("Çatı: " + r.Cati.KisaAd, M2(r.CatiAlani), TL(r.CatiAlani * r.Cati.FiyatM2));
            Satir("Zemin: " + r.Zemin.KisaAd, M2(r.ZeminAlani), TL(r.ZeminAlani * r.Zemin.FiyatM2));
            Satir("Ara toplam", "", TL(s.IlkYatirim), true, true);
            Satir($"KDV %{(kdv * 100).ToString("0", tr)}", "", TL(s.IlkYatirim * kdv));
            Satir("Genel toplam", "KDV dahil", TL(s.IlkYatirim * (1 + kdv)), true);
            Satir("m² başına", "KDV hariç", (s.IlkYatirim / r.KullanimAlani).ToString("N0", tr) + " TL");
            Satir("Yıllık ısıtma", (s.YillikIsitmaKWh / r.KullanimAlani).ToString("N0", tr) + " kWh/m²", TL(s.YillikIsitmaTL), false, true, "kWh");
            if (r.Sogutma) Satir("Yıllık soğutma", (s.YillikSogutmaKWh / r.KullanimAlani).ToString("N0", tr) + " kWh/m²", TL(s.YillikSogutmaTL), false, false, "Sogutma");
            if (r.Mevcut)
            {
                var m = Kpi.MevcutaGore(s, r);
                Satir("Mevcuda göre tasarruf", "yıllık", TL(m.tasarrufTL), true, true, "ProjeTuru");
                Satir("Geri dönüş", "iskontolu", m.geriDonusYil is double gy ? (gy <= yil ? $"{gy.ToString("N1", tr)} yıl" : $"{gy.ToString("N0", tr)} yıl (> {yil})") : "Dönmüyor");
                Satir("Net bugünkü değer", $"{yil} yıl", TL(m.netBugunkuDeger));
            }
            Satir("Ömür boyu maliyet", $"{yil} yıl, BD", TL(s.ToplamMaliyet), true, false, "LCC");
            var k = Kpi.Hesapla(s, r, ekonomik, r.EnerjiIskonto, yil);
            if (!r.Mevcut) Satir("Geri dönüş", "iskontolu",
                !k.EkYatirim ? "Ek yatırım yok" : !k.Tasarruf ? "Tasarruf yok" : k.GeriOdemeVar && k.GeriOdemeYil <= yil ? $"{k.GeriOdemeYil.ToString("N1", tr)} yıl" : $"> {yil} yıl", false, false, "GeriDonus");
            Satir($"{yil} yıllık CO₂", "", (s.ToplamCO2Kg / 1000).ToString("N1", tr) + " t");
            Satir("Duvar U değeri", "", s.DuvarU.ToString("0.00", tr) + " W/m²K", false, false, "U");
            if (r.CamAlani > 0) Satir("Pencere U", "", s.PencereU.ToString("0.0", tr) + " W/m²K", false, false, "Dograma");
            Satir("Duvar ağırlığı", "", s.DuvarAgirlikTon.ToString("N0", tr) + " t", false, false, "Agirlik");
            kesif.ResumeLayout();
        }

        /// <summary>TS 825:2024 kontrolü: dış duvar, pencere, çatı ve zemin (seçili iklim bölgesi).</summary>
        string Ts825Metni(Secenek s, Sonuc r, out bool uygun)
        {
            var tr = Ortak.TR;
            var v = veri!;
            var kalem = new List<(string ad, double u, double sinir)> { ("duvar", s.DuvarU, r.SinirDuvar) };
            if (r.CamAlani > 0) kalem.Add(("pencere", s.PencereU, r.SinirPencere));
            kalem.Add(("çatı", r.CatiU, r.SinirCati));
            kalem.Add(("zemin", r.ZeminU, r.SinirZemin));
            uygun = s.Ts825Uygun;
            if (uygun)
                return $"✓ TS 825:2024 ({r.IlAdi}, {r.BolgeNo}. bölge) sınırlarına uygun\n" + string.Join(" · ", kalem.Select(x => $"{x.ad} U {x.u.ToString("0.00", tr)} ≤ {x.sinir.ToString("0.00", tr)}"));
            var neden = kalem.Where(x => x.u > x.sinir + 1e-12).Select(x => $"{x.ad} U {x.u.ToString("0.00", tr)} (en fazla {x.sinir.ToString("0.00", tr)})" +
                (x.ad == "duvar" ? " – daha kalın yalıtım gerekir" : x.ad == "pencere" ? " – daha iyi cam gerekir" : ""));
            return "TS 825'e uygun değil: " + string.Join("; ", neden) + ". Bu haliyle ruhsat alınamaz.";
        }

        // ================= Yardımcılar =================
        /// <summary>Kontrolleri kaldırıp serbest bırakır (Controls.Clear tek başına Windows tanıtıcılarını bırakmaz).</summary>
        static void KontrolleriTemizle(Control kap)
        {
            var eski = kap.Controls.Cast<Control>().ToList();
            kap.Controls.Clear();
            foreach (var c in eski) c.Dispose();
        }

        string TarifeAdi => cmbTarife.SelectedIndex == 1 ? "Kademe-2" : "Kademe-1";

        static bool Ayni(Secenek? a, Secenek? b) => a != null && b != null && a.Yalitim == b.Yalitim && a.Cam == b.Cam && a.Duvar == b.Duvar;
        static string TL(double v) => v.ToString("N0", Ortak.TR) + " TL";
        static string Isaret(double v) => v > 0 ? "+" : v < 0 ? "−" : "±";

        static string KarsilastirmaMetni(int v, string sol, string sag) =>
            v == 0 ? "Eşit önemde" : $"{(v < 0 ? sol : sag)} {Math.Abs(v) + 1} kat daha önemli";

        static void KarsilastirmaEkle(TableLayoutPanel t, string sol, string sag, TrackBar tb, Label durum)
        {
            var ust = new TableLayoutPanel { ColumnCount = 2, Width = 250, Height = 22, Margin = new Padding(0, 6, 0, 0) };
            ust.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            ust.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            ust.Controls.Add(new Label { Text = sol, AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }, 0, 0);
            ust.Controls.Add(new Label { Text = sag, AutoSize = true, Anchor = AnchorStyles.Right, Font = new Font("Segoe UI", 9f, FontStyle.Bold) }, 1, 0);
            t.Controls.Add(ust);
            tb.Width = 250;
            t.Controls.Add(tb);
            durum.AutoSize = false; durum.Width = 250; durum.Height = 18; durum.TextAlign = ContentAlignment.MiddleCenter;
            t.Controls.Add(durum);
        }

        static Control Alan(string etiket, Control c)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            p.Controls.Add(new Label { Text = etiket, AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = Gri, Margin = new Padding(0, 0, 0, 2) });
            c.Margin = new Padding(0);
            if (c is ComboBox) c.Width = 250;
            p.Controls.Add(c);
            return p;
        }

        static Label Bolum(string s) => new() { Text = s, AutoSize = true, ForeColor = Color.FromArgb(31, 111, 74), Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10) };
        static Label Ayrac() => new() { AutoSize = false, Height = 1, Width = 250, BackColor = Color.FromArgb(225, 228, 224), Margin = new Padding(0, 12, 0, 14) };
        static FlowLayoutPanel KartSirasi() => new() { AutoSize = false, WrapContents = false, AutoScroll = true, Height = 128, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
        static Label Baslik(string s) => new() { Text = s, AutoSize = true, ForeColor = Gri, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Margin = new Padding(0, 4, 0, 4) };
        static TrackBar Kaydirici(int deger) => new() { Minimum = -8, Maximum = 8, Value = deger, TickFrequency = 1, SmallChange = 1, LargeChange = 2 };
        static ComboBox Combo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
        static NumericUpDown Olcu(int min, int max, int deger, int ondalik = 1) => new() { Minimum = min, Maximum = max, Value = deger, DecimalPlaces = ondalik, Increment = ondalik == 0 ? 1 : 0.5m, Width = 70 };
        static Label Ipucu(string metin = "") => new() { Text = metin, AutoSize = true, ForeColor = Gri, Font = new Font("Segoe UI", 8.5f), MaximumSize = new Size(250, 0) };
        static Label Not() => new() { AutoSize = true, MaximumSize = new Size(280, 0), Padding = new Padding(8, 6, 8, 6), Margin = new Padding(0, 8, 0, 0), Font = new Font("Segoe UI", 9f) };

        static void NotRenk(Label l, bool iyi)
        {
            l.BackColor = iyi ? YesilAcik : TuruncuAcik;
            l.ForeColor = iyi ? Color.Black : Turuncu;
        }

        sealed class Secim
        {
            public string Ad { get; }
            public double Deger { get; }
            public string Kod { get; init; } = "";
            public Secim(string ad, double deger) { Ad = ad; Deger = deger; }
            public override string ToString() => Ad;
        }

        const string KaynakMetni =
            "İklim ve yönetmelik\n" +
            "• 81 ilin yıllık ısıtma ve soğutma derece-günü: 78 il MGM (Şensoy, Sağır, Eken ve Ulupınar), 1975–2005 rasat ortalaması; Çankırı, Karabük ve Şırnak MGM gün-derece sayfası, 1991–2005 yıllık değerlerinin ortalaması (yöntem farkı: ısıtmada ortanca +%1,5). Malatya 2.425.\n" +
            "• TS 825:2024 bölge U sınırları: İZODER (T. Diz, Yeni TS 825:2024 Standardı); il merkezi bölgeleri: İZODER ve EPSDER il föyleri, tesisat.org ve akademik çalışmalar (81 il).\n\n" +
            "Bina\n" +
            "• 12 bina formu: Tsanas ve Xifara (2012), UCI Energy Efficiency veri seti; ya da kullanıcının girdiği en × boy × kat.\n\n" +
            "Fiyatlar (KDV hariç)\n" +
            "• Mantolama 5/8/10 cm: karekod.org 2026 uygulanmış fiyat tablosu (levha, yapıştırıcı, dübel, file, sıva ve işçilik dahil).\n" +
            "• Levha fiyat serileri, 4–15 cm: taş yünü Bonus Premium F 150 Pro (Kasım 2026), EPS Expert EPS Beyaz (Kasım 2026); tasyunufiyatlari.com.\n" +
            "• Cam, duvar, çatı ve zemin: 2026 tarihli tedarikçi fiyatları (yalnız malzeme).\n" +
            "• Isıtma ve soğutma enerjisi: doğalgaz BOTAŞ (Nisan 2026); elektrik EPDK mesken tarifesi (Kurul Kararı 14461, 4 Nisan 2026); LPG tüp (Ankara, 3 Kasım 2026);\n" +
            "  kömür (Soma, 5000 kcal sınıfı, 3 Kasım 2026). Isıl değer ve emisyon: IPCC 2006; elektrik emisyonu: ETKB ulusal şebeke faktörü (2022).\n" +
            "• Cihaz verimleri (asgari): kombi %86 (AB 813/2013), kapalı soba %65 (AB 2015/1185), klima SEER 4,6 ve SCOP 3,8 (AB 206/2012).\n"+
            "• Yangın: Binaların Yangından Korunması Hakkında Yönetmelik Md. 27; EPS sistem sınıfı B-s1,d0 (Filli Boya teknik föyü), taş yünü A1 (Dalmaçyalı).\n"+
            "• Harita: il sınırları Natural Earth 1:10m (kamu malı), sadeleştirilmiş.\n" +
            "• Mantolama 12 ve 15 cm (hesaplanan): aynı ailenin 10 cm uygulanmış fiyatı + aynı ürünün levha fiyat farkı. ÇŞB 2026 resmî birim fiyatlarında\n" +
            "  mantolama fiyatı kalınlıkla yalnız levha kadar artar (işçilik ve aksesuar sabit); hesap bununla tutarlıdır.\n" +
            "• Çift kat çatı (hesaplanan): yayımlanmış 10 cm şilte fiyatının iki katı (iki kat şilte serilir).\n\n" +
            "Teknik değerler ve ekonomi\n" +
            "• Pencere U (cam + doğrama): TS 825:2024 Ek A.3 çizelgesi (İZODER il föyleri), seçilen doğrama ve cam için.\n" +
            "• Isıl değerler: TS 825 hesap değerleri ve üretici teknik föyleri. Havalandırma ve düzeltme katsayıları: TS 825 hesap yöntemi.\n" +
            "• Kazan verimi: AB 813/2013 Ekotasarım Tüzüğü (%86). Doğalgaz: BOTAŞ tarifesi; üst ısıl değer: EPDK. Emisyon: IPCC 2006. Malzeme karbonu: ICE v4.1.\n" +
            "• TCMB politika faizi %36 (22 Ekim 2026), TÜİK TÜFE yıllık %29,77 (Ekim 2026); KDV %20 (CBK 7346).\n\n" +
            "Her değerin kaynağı ve tarihi Veri klasöründeki CSV dosyalarında ve ecobuildss.db veritabanındadır.";

        const string YontemMetni =
            "Teklif ekranı\n" +
            "• Ekonomik: TS 825'e uygun seçenekler içinde ilk yatırımı en düşük olan.\n" +
            "• Önerilen: seçilen önceliğe göre TOPSIS puanı en yüksek olan.\n" +
            "• Yüksek performans: TS 825'e uygun seçenekler içinde ısıtma ihtiyacı en düşük olan.\n" +
            "• Geri dönüş: Ekonomik pakete göre ek yatırımın, yıllık yakıt tasarrufunun bugünkü değeriyle karşılandığı süre (iskontolu).\n\n" +
            "Enerji hesabı (TS 825 derece-gün yöntemi)\n" +
            "• H = ΣU·A (duvar, pencere, 0,8 × çatı, 0,5 × zemin) + 0,33 · nh · 0,8 · hacim.\n" +
            "• Pencere U, camın değil pencerenin (cam + doğrama) U değeridir: TS 825:2024 Ek A.3'ten seçilen doğrama ve cam tipiyle okunur. Cam adında ara boşluk yazmıyorsa\n" +
            "  çizelgedeki 12 ve 16 mm değerlerinin büyüğü alınır (en elverişsiz durum; TS 825 uygunluğu bu durumda da kesindir). Çizelgede değeri olmayan cam o doğramayla seçenek olmaz.\n" +
            "• Yıllık ısıtma (kWh) = 0,024 × derece-gün × H;  yakıt = ısıtma ÷ kazan verimi (0,86).\n" +
            "• Duvar U = 1 ÷ (0,17 + duvar kalınlığı ÷ λ + yalıtım kalınlığı ÷ λ). Çatı ve zemin: mevcut katmana yalıtım eklenir, U = 1 ÷ (1/U0 + d/λ).\n" +
            "• Çatı katmanı bölgeye göre seçilir: sınırı sağlayanların en ucuzu (1–4. bölgede tek kat, 5–6. bölgede çift kat şilte). Zemin: 2 × 5 cm XPS.\n" +
            "• TS 825 kontrolü dört elemanın U değeri ve seçilen iklim bölgesinin sınırlarıyla yapılır. Camların g değeri kaynakta yayımlanmadığından g sınırı denetlenmez.\n" +
            "• Soğutma (klima varsa): 0,024 × soğutma derece-günü × H ÷ SEER = elektrik. Isıtma yakıtı: yakıt = ısıtma ÷ cihaz verimi; fiyat ve emisyon yakıta göre.\n" +
            "• Güneş kazancı ve iç kazançlar hesaba katılmaz (soğutma bu yüzden olduğundan düşük çıkar); ön cephe yönü çizim ve teklif bilgisidir.\n" +
            "• Mevcut bina (yenileme): duvar yerinde kalır (referans U 1,78); yalıtım U = 1 ÷ (1/1,78 + d/λ). Tasarruf ve geri dönüş yalıtımsız mevcut binaya göre.\n" +
            "• Yangın yönetmeliği (Md. 27): bina yüksekliği 28,50 m'den fazlaysa cephe sistemi en az A2-s1,d0 olmalı → EPS seçenekleri çıkarılır.\n" +
            "  28,50 m'ye kadar zor alevlenici (EPS) sistemde zeminden 1,5 m hiç yanmaz şerit gerekir: bu şeritte taş yünü kullanılır, fiyat farkı\n" +
            "  (şerit alanı = 1,5 m × bina çevresi) ilk yatırıma eklenir; 6,50 m'den yüksek binada pencere çevresi bariyeri yazılır (pencere ölçüsü yok, bedele eklenmez).\n" +
            "• Duvar ağırlığı (bilgi): birim ağırlık × alan; deprem açısından hafif duvarı görmek içindir, karar puanına girmez.\n\n" +
            "Maliyet ve çok kriterli karar\n" +
            "• Ömür boyu maliyet (bugünkü değer) = ilk yatırım + PWF × yıllık enerji gideri; PWF = [1 − (1 + r)^−N] ÷ r.\n" +
            "  Enerji fiyatı enflasyondan e kadar hızlı artarsa r yerine (1 + r) ÷ (1 + e) − 1 kullanılır (Gelişmiş ayarlar; varsayılan e = 0).\n" +
            "• Reel iskonto r = (faiz − enflasyon) ÷ (1 + enflasyon) = (0,36 − 0,2977) ÷ 1,2977 ≈ %4,8.\n" +
            "• Toplam CO₂ = malzeme karbonu (ICE v4.1) + ısıtma karbonu (IPCC 2006).\n" +
            "• 10 mantolama × 2 cam × 3 duvar = 60 seçenek. TOPSIS: ömür boyu maliyet, yıllık ısıtma ve toplam CO₂ (üçü de küçük iyi); AHP ağırlıkları geometrik ortalama ile, CR ≤ 0,10.\n\n" +
            "Regresyon\n" +
            "• Fiyat = a + b · kalınlık (en küçük kareler). b her ailede aynı ürünün 4–15 cm levha fiyat serisinden gelir (taş yünü 8, EPS 12 kalınlık);\n" +
            "  doğru, yayımlanmış uygulanmış fiyatların ağırlık merkezinden geçer. Hesaplanan 12/15 cm fiyatları regresyona gözlem olarak girmez.\n\n" +
            "Optimizasyon\n" +
            "• Optimum yalıtım kalınlığı: ömür boyu maliyeti en küçükleyen kalınlık (altın oran araması), kısıt duvar U ≤ bölge sınırı; kapalı form çözümle (Bolattürk 2006) doğrulanır.\n" +
            "• Bütçe kısıtlı seçim: bütçeyi aşmayan ve TS 825'e uyan seçenekler içinde seçilen amacı en küçükleyen birleşim (0-1 model, tam sayım); Pareto cephesi.\n\n" +
            "Kümeleme (Bölgesel analiz)\n" +
            "• 81 il, ısıtma ve soğutma derece-gününe göre k-ortalamalar (z-skoru, k-means++, 200 başlangıç).\n" +
            "• k, 2–8 arasında Davies–Bouldin endeksi en düşük olan (81 ilde k = 2; siluet de k = 2'yi seçer). k ≥ 3 arasında iki ölçüt de k = 6'yı seçer; k elle değiştirilebilir.\n" +
            "• Her küme için optimum yalıtım kalınlığı ve önerilen paket hesaplanır: aynı kümedeki illerde aynı standart paket kullanılabilir.\n\n" +
            "Sınıflandırma (karar ağacı)\n" +
            "• Etiket: her il için karar motorunun ilin kendi derece-günü ve TS 825 bölgesiyle, ekrandaki bina ve öncelikle önerdiği paket (uydurma etiket yok).\n" +
            "• Özellikler: ısıtma derece-günü, soğutma derece-günü, TS 825 bölgesi. CART, Gini safsızlığı; eşikler komşu değerlerin orta noktası.\n" +
            "• Derinlik 1–6 arasında birini dışarıda bırak çapraz doğrulama (LOOCV) doğruluğu en yüksek olan (eşitlikte en sığ). Taban: hep en sık paketi söylemek.\n" +
            "• Amaç: karar motorunu sahada kullanılabilecek birkaç açık kurala indirmek; teklif her zaman karar motoruyla hesaplanır.\n\n" +
            "Fiyat belirsizliği (Monte Carlo)\n" +
            "• Kaynağı fiyatı aralık veren malzemelerde (mantolama, cam) fiyat aralık içinde düzgün dağılımla 1.000 kez çekilir (sabit tohum 42);\n" +
            "  hesaplanan 12/15 cm aynı ailenin 10 cm çekilişine levha farkı eklenerek izler. Her denemede karar yeniden verilir; önerinin birinci kalma oranı verilir.\n\n" +
            "Duyarlılık\n" +
            "• Enerji fiyatı (−%50 … +%200), reel enerji fiyatı artışı (−%5 … +%10/yıl), malzeme fiyatları (−%30 … +%50) ve analiz süresi (1–50 yıl) adım adım değiştirilir.\n\n" +
            "Kapsam: Mantolama dışındaki kalemlerde işçilik ve nakliye dahil değildir; pencere doğraması her seçenekte aynı olduğundan fiyata katılmaz. Fiyatlar KDV hariçtir. Sonuçlar seçenekleri karşılaştırmak içindir.";
    }
}
