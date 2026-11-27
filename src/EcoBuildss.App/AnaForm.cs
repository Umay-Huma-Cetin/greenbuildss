using EcoBuildss.Core.Depo;

namespace EcoBuildss.UI
{
    /// <summary>Ana pencere: solda gezinme çubuğu (Ana sayfa, Apartman mantolaması, Yeni bina, Tekliflerim, Veri ve modeller), sağda tek ekran.</summary>
    public sealed class AnaForm : Form
    {
        readonly Durum d = new();
        readonly Label durumYazisi = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Ui.Kucuk, ForeColor = Ui.Soluk, Padding = new Padding(10, 0, 0, 0) };
        readonly Label ornekUyari = new() { Dock = DockStyle.Bottom, Height = 46, Text = "Örnek (sentetik) veri yüklü", Font = Ui.Kucuk, BackColor = Ui.Amber, ForeColor = Ui.Koyu, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
        readonly Panel icerik = new() { Dock = DockStyle.Fill, BackColor = Ui.Zemin };
        readonly List<Button> menu = new();
        readonly List<Control> ekranlar = new();
        TeklifSekmesi teklif = null!;
        ApartmanSekmesi apartman = null!;

        public AnaForm()
        {
            Text = "EcoBuildss";
            Font = Ui.Yazi; BackColor = Ui.Zemin; MinimumSize = new Size(1200, 760); Size = new Size(1440, 900); StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            d.Kok = Kok();
            d.V = VeriDeposu.Yukle(d.Referans);
            try { d.Db = new Veritabani(Path.Combine(VeriKlasoru(), "ecobuildss.db")); }
            catch (Exception e) { d.DbHatasi = e.Message; }
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
            yan.Controls.Add(liste); yan.Controls.Add(ornekUyari); yan.Controls.Add(logo);

            var alt = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Color.White };
            alt.Controls.Add(durumYazisi);

            var ana = new AnaSayfa(d);
            ana.Git += Goster;
            apartman = new ApartmanSekmesi(d);
            teklif = new TeklifSekmesi(d);
            var kayitlar = new KayitlarSekmesi(d);
            kayitlar.TeklifeYukle += t =>
            {
                if (t.ProjeTuru == "Yenileme") { Goster(1); apartman.Yukle(t); }
                else { Goster(2); teklif.Yukle(t); }
            };
            ekranlar.AddRange(new Control[] { ana, apartman, teklif, kayitlar, new YoneticiSekmesi(d) });
            foreach (var e in ekranlar) { e.Dock = DockStyle.Fill; e.Visible = false; icerik.Controls.Add(e); }

            Controls.Add(icerik); Controls.Add(alt); Controls.Add(yan);

            d.ModellerDegisti += () => ornekUyari.Visible = d.OrnekVeriKullaniliyor;
            Shown += (_, __) =>
            {
                d.ModelleriKur();
                d.Bildir(d.Db == null ? $"Veritabanı açılamadı ({d.DbHatasi}); kayıt bu oturumda kapalı." : "Hazır");
                teklif.Hesapla();
                Goster(0);
            };
            KeyDown += (_, e) =>
            {
                if (e.Control && e.KeyCode == Keys.S) { if (ekranlar[1].Visible) apartman.Kaydet(); else if (ekranlar[2].Visible) teklif.Kaydet(); e.Handled = true; }
            };
            FormClosed += (_, __) => d.Db?.Dispose();
        }

        void Goster(int i)
        {
            for (int k = 0; k < ekranlar.Count; k++)
            {
                ekranlar[k].Visible = k == i;
                menu[k].BackColor = k == i ? Ui.Yesil : Ui.Koyu;
                menu[k].ForeColor = k == i ? Color.White : Color.FromArgb(214, 228, 220);
                menu[k].Font = new Font("Segoe UI", 10.5f, k == i ? FontStyle.Bold : FontStyle.Regular);
            }
        }

        /// <summary>veri/referans klasörünü programın yanında, yoksa üst klasörlerde (geliştirme ortamı) arar.</summary>
        static string Kok()
        {
            var k = new DirectoryInfo(AppContext.BaseDirectory);
            while (k != null && !Directory.Exists(Path.Combine(k.FullName, "veri", "referans"))) k = k.Parent;
            return k?.FullName ?? throw new DirectoryNotFoundException("veri/referans klasörü bulunamadı. Programı çözüm klasöründen ya da derleme çıktısından çalıştırın.");
        }

        /// <summary>Veritabanı kullanıcının uygulama verisi klasöründe tutulur (program klasörü yazmaya kapalı olabilir).</summary>
        static string VeriKlasoru()
        {
            string k = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EcoBuildss");
            Directory.CreateDirectory(k);
            return k;
        }
    }
}
