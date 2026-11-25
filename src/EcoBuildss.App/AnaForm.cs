using EcoBuildss.Core.Depo;

namespace EcoBuildss.UI
{
    /// <summary>Ana pencere: üst bant, beş sekme, alt durum çubuğu.</summary>
    public sealed class AnaForm : Form
    {
        readonly Durum d = new();
        readonly Label durumYazisi = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Ui.Kucuk, ForeColor = Ui.Soluk, Padding = new Padding(10, 0, 0, 0) };
        readonly Label ornekUyari = new()
        {
            AutoSize = true, Text = "Örnek (sentetik) veri kullanılıyor",
            Font = Ui.Kalin, BackColor = Ui.Amber, ForeColor = Ui.Koyu, Padding = new Padding(8, 4, 8, 4), Visible = false,
        };
        TeklifSekmesi? teklif;
        readonly TabControl sekmeler = new() { Dock = DockStyle.Fill, Font = Ui.Kalin, Padding = new Point(14, 6) };

        public AnaForm()
        {
            Text = "EcoBuildss · Yeşil bina malzeme karar destek sistemi";
            Font = Ui.Yazi; BackColor = Ui.Zemin; MinimumSize = new Size(1180, 760); Size = new Size(1440, 900); StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            d.Kok = Kok();
            d.V = VeriDeposu.Yukle(d.Referans);
            try { d.Db = new Veritabani(Path.Combine(VeriKlasoru(), "ecobuildss.db")); }
            catch (Exception e) { d.DbHatasi = e.Message; }
            d.Bildirim += m => durumYazisi.Text = $"{DateTime.Now:HH:mm}  ·  {m}";

            var ust = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Ui.Koyu };
            var ad = new Label { Text = "EcoBuildss", Font = new Font("Segoe UI Semibold", 17f), ForeColor = Color.White, AutoSize = true, Location = new Point(18, 12) };
            var alt = new Label { Text = "Teklif · analiz · gerçek veriyle kalibrasyon ve öğrenme", Font = Ui.Kucuk, ForeColor = Color.FromArgb(185, 206, 196), AutoSize = true, Location = new Point(160, 24) };
            ust.Controls.Add(ad); ust.Controls.Add(alt);
            var sag = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 14, 14, 0), BackColor = Ui.Koyu, WrapContents = false };
            sag.Controls.Add(ornekUyari);
            ust.Controls.Add(sag);

            var alttaki = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Color.White };
            alttaki.Controls.Add(durumYazisi);

            teklif = new TeklifSekmesi(d);
            Ekle("Teklif", teklif);
            Ekle("Analiz", new AnalizSekmesi(d, () => teklif.SonGirdi, () => teklif.SonSonuc));
            Ekle("Veri", new VeriSekmesi(d));
            Ekle("Modeller", new ModellerSekmesi(d));
            var kayit = new KayitlarSekmesi(d);
            kayit.TeklifeYukle += t => { teklif.Yukle(t); sekmeler.SelectedIndex = 0; };
            Ekle("Kayıtlı teklifler", kayit);

            Controls.Add(sekmeler); Controls.Add(alttaki); Controls.Add(ust);

            d.ModellerDegisti += () => ornekUyari.Visible = d.OrnekVeriKullaniliyor;
            Shown += (_, __) =>
            {
                d.ModelleriKur();
                d.Bildir(d.Db == null ? $"Veritabanı açılamadı ({d.DbHatasi}); kayıt ve veri yükleme bu oturumda kapalı." : $"Hazır · veritabanı: {d.Db.Dosya}");
                teklif.Hesapla();
            };
            KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.S) { teklif.Kaydet(); e.Handled = true; } };
            FormClosed += (_, __) => d.Db?.Dispose();
        }

        void Ekle(string ad, Control c) { var t = new TabPage(ad) { BackColor = Ui.Zemin, Padding = new Padding(4) }; c.Dock = DockStyle.Fill; t.Controls.Add(c); sekmeler.TabPages.Add(t); }

        /// <summary>veri/referans klasörünü programın yanında, yoksa üst klasörlerde (geliştirme ortamı) arar.</summary>
        static string Kok()
        {
            var k = new DirectoryInfo(AppContext.BaseDirectory);
            while (k != null && !Directory.Exists(Path.Combine(k.FullName, "veri", "referans"))) k = k.Parent;
            return k?.FullName ?? throw new DirectoryNotFoundException("veri/referans klasörü bulunamadı. Programı çözüm klasöründen ya da derleme çıktısından çalıştırın.");
        }

        /// <summary>Veritabanı kullanıcının belgelerinde tutulur (program klasörü yazmaya kapalı olabilir).</summary>
        static string VeriKlasoru()
        {
            string k = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EcoBuildss");
            Directory.CreateDirectory(k);
            return k;
        }
    }
}
