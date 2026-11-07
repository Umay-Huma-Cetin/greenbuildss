using System.Drawing;
using System.Windows.Forms;
using EcoBuildss.Core;
using EcoBuildss.Depo;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Kullanım kolaylığı: terim açıklamaları ("?" işaretleri), "Neden bu paket?" gerekçesi, ilk açılış rehberi ve Excel'e aktarma.
    /// </summary>
    public sealed partial class AnaForm
    {
        static readonly ToolTip Aciklayici = new() { AutoPopDelay = 30000, InitialDelay = 250, ReshowDelay = 100, ShowAlways = true };

        /// <summary>Uygulamada geçen teknik terimlerin sade açıklamaları.</summary>
        static readonly Dictionary<string, (string baslik, string metin)> Terimler = new()
        {
            ["U"] = ("U değeri (W/m²K)", "Isı geçirgenlik katsayısı: içerisi ile dışarısı arasında 1 °C fark varken 1 m² yüzeyden geçen ısı (watt). " +
                                         "Küçük olması daha iyi yalıtım demektir. TS 825:2024, her iklim bölgesi için duvar, pencere, çatı ve zemine en yüksek U değerini koyar."),
            ["DG"] = ("Derece-gün", "Bir yıl boyunca dış sıcaklığın ısıtma sınırının altında kaldığı günlerdeki sıcaklık farklarının toplamı (MGM, 18/15 °C). " +
                                    "Büyük olması daha soğuk iklim ve daha çok ısıtma ihtiyacı demektir. Soğutma derece-günü yazın serinletme ihtiyacını gösterir (22 °C)."),
            ["TS825"] = ("TS 825:2024 iklim bölgesi", "Binalarda ısı yalıtımı standardı Türkiye'yi 6 iklim bölgesine ayırır: 1. bölge en ılık, 6. bölge en soğuk. " +
                                                      "Bölge, il merkezine göre İZODER il föyünden alınır; aynı ilin ilçeleri farklı bölgede olabilir. Sınırları sağlamayan proje ruhsat alamaz."),
            ["LCC"] = ("Ömür boyu maliyet", "İlk yatırım + analiz süresi boyunca ödenecek doğalgaz giderinin bugünkü değeri. Paketleri yalnız ilk fiyatına göre değil, " +
                                            "yıllar içindeki toplam maliyete göre karşılaştırır. Gelecekteki giderler reel iskonto oranıyla (faiz ve enflasyondan) bugüne indirgenir."),
            ["GeriDonus"] = ("Ek yatırımın geri dönüşü", "Ekonomik pakete göre fazladan ödenen yatırımın, yıllık yakıt tasarrufuyla (bugünkü değerle) kaç yılda geri kazanıldığı."),
            ["kWh"] = ("Yıllık ısıtma ihtiyacı", "Binanın bir yılda ısıtma için ihtiyaç duyduğu enerji (kWh), TS 825 derece-gün yöntemiyle. kWh/m² değeri kullanım alanı başınadır; " +
                                                 "farklı büyüklükteki binaları karşılaştırmayı sağlar."),
            ["Oncelik"] = ("Öncelik ve puan", "Maliyet, enerji ve karbon ölçütlerinin ağırlıkları AHP (ikili karşılaştırma) ile belirlenir; 60 seçenek TOPSIS ile 0–100 arası puanlanır. " +
                                              "Puan, seçeneğin en iyi ve en kötü hayali seçeneğe göre konumudur. Gelişmiş ayarlardan ağırlıklar değiştirilebilir."),
            ["MonteCarlo"] = ("Fiyat belirsizliği (Monte Carlo)", "Kaynaklar fiyatı çoğu zaman bir aralık olarak verir (ör. 650–800 TL/m²); hesapta aralığın ortası kullanılır. " +
                                                  "Burada her fiyat kendi aralığında rastgele çekilir (aralığın her noktası eşit olasılıklı) ve karar 1.000 kez yeniden verilir. Önerinin birinci kalma oranı yüksekse öneri fiyat belirsizliğine karşı sağlamdır."),
            ["Sogutma"] = ("Soğutma", "Klima varsa yaz soğutması da hesaplanır: soğutma enerjisi = 0,024 × soğutma derece-günü (MGM, 22 °C) × H; elektrik = soğutma ÷ SEER 4,6 (AB 206/2012 asgari). " +
                                      "Güneş kazancı hesaba katılmadığından gerçek soğutma yükünün altında kalır; seçenekleri karşılaştırmak içindir."),
            ["Agirlik"] = ("Duvar ağırlığı", "Dış duvar ve yalıtımın toplam ağırlığı (birim ağırlık × alan). Deprem bölgesinde hafif duvar binanın deprem yükünü azaltır. " +
                                         "Bilgi amaçlıdır; taşıyıcı sistem hesabı yerine geçmez ve karar puanına girmez."),
            ["ProjeTuru"] = ("Proje türü", "Yeni bina: duvar malzemesi de seçilir. Mevcut bina (yenileme): duvar yerinde kalır (yalıtımsız referans duvar, U 1,78), yalnız mantolama, pencere camı, çatı ve zemin yalıtımı yapılır; " +
                                              "sonuç mevcut duruma göre yıllık tasarruf, geri dönüş süresi ve net bugünkü değer olarak verilir."),
            ["Yakit"] = ("Isıtma", "Binayı ısıtan yakıt ve cihaz. Yakıtın fiyatı, ısıl değeri, cihazın verimi ve emisyonu kaynaklı değerlerdir (Veri kaynakları). " +
                                   "Pahalı yakıtta yalıtımın kendini ödeme süresi kısalır; öneri değişebilir."),
            ["Artis"] = ("Enerji fiyatı reel artışı", "Enerji fiyatlarının enflasyonun üstünde yıllık ne kadar artacağı. 0 = enflasyonla aynı hızda. " +
                                                     "Pozitif değerde gelecekteki enerji giderleri daha yüksek, kalın yalıtım daha kârlı olur."),
            ["Dograma"] = ("Pencere doğraması", "Pencerenin çerçevesi. TS 825:2024'teki pencere U sınırı cam ile doğramanın birlikte U değeri içindir; bu değer seçilen doğrama ve cam için TS 825:2024 Ek A.3 çizelgesinden alınır. Doğramanın fiyatı her seçenekte aynı olduğundan karşılaştırmaya katılmaz."),
            ["Pencere"] = ("Pencere alanı oranı", "Pencere alanının kullanım alanına oranı. Pencere duvardan daha çok ısı kaybettirdiği için oran büyüdükçe ısıtma ihtiyacı artar."),
            ["Sure"] = ("Analiz süresi", "Ömür boyu maliyet ve karbonun kaç yıl için hesaplanacağı. Mantolama ve doğramada 20–30 yıl yaygın bir ufuktur."),
            ["Tarife"] = ("Tarife kademesi", "BOTAŞ doğalgaz ve EPDK elektrik konut tarifesi (Nisan 2026). Kademe-2, tüketimi kademe sınırını aşan aboneler için uygulanan yüksek fiyattır."),
            ["Agac"] = ("Karar ağacı (sınıflandırma)", "Her il için karar motorunun önerdiği paket, ilin ısıtma ve soğutma derece-günü ve TS 825 bölgesinden tahmin edilir. " +
                                                     "Ağaç \"ısıtma derece-günü ≤ 2.054 mü?\" gibi evet/hayır sorularıyla ilerler; her yaprak bir paket önerir. Derinlik, her il sırayla dışarıda bırakılıp " +
                                                     "geri kalanlarla kurulan ağacın o ili doğru bilme oranıyla (LOOCV) seçilir. Teklif yine karar motoruyla hesaplanır; ağaç hızlı ön tahmin ve açıklama içindir."),
            ["Kume"] = ("Küme sayısı ve ölçütler", "k-ortalamalar, illeri ısıtma ve soğutma derece-gününe göre k gruba ayırır. Davies–Bouldin endeksi kümelerin ne kadar sıkı ve birbirinden " +
                                                   "ayrık olduğunu ölçer (düşük daha iyi). Siluet katsayısı her ilin kendi kümesine diğer kümelerden ne kadar yakın olduğunu ölçer (−1…1, yüksek daha iyi)."),
            ["Optimum"] = ("Optimum kalınlık", "Yalıtım kalınlaştıkça ilk yatırım artar, yakıt gideri azalır. Ömür boyu maliyetin en düşük olduğu kalınlık ekonomik optimumdur. " +
                                               "TS 825 sınırı bundan kalın yalıtım istiyorsa yönetmelik bağlayıcıdır."),
            ["Regresyon"] = ("Fiyat–kalınlık regresyonu", "Piyasadaki levha fiyatlarından (4–15 cm) her ek santimetrenin maliyeti en küçük kareler yöntemiyle bulunur. " +
                                                         "R² 1'e yakınsa fiyat kalınlıkla neredeyse tam doğrusal artıyor demektir. Optimizasyon bu eğimi kullanır."),
            ["Butce"] = ("Bütçe ve Pareto", "Pareto cephesi: hiçbir başka seçeneğin hem daha ucuz hem daha iyi olmadığı seçenekler. Bütçe sınırında amaçta en iyi sonucu veren seçenek seçilir."),
        };

        /// <summary>Yeşil "?" işareti: üstüne gelince açıklama, tıklayınca ayrıntı penceresi.</summary>
        static Label Soru(string terim)
        {
            var (baslik, metin) = Terimler[terim];
            var l = new Label
            {
                Text = "?", AutoSize = false, Size = new Size(16, 16), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Help,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.FromArgb(31, 111, 74), Margin = new Padding(4, 1, 0, 0)
            };
            Aciklayici.SetToolTip(l, baslik + "\n" + metin);
            l.Click += (_, _) => MessageBox.Show(metin, baslik, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return l;
        }

        /// <summary>Etiket + "?" + denetim.</summary>
        static Control Alan(string etiket, Control c, string terim)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
            var ust = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 2) };
            ust.Controls.Add(new Label { Text = etiket, AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = Gri, Margin = new Padding(0, 1, 0, 0), MaximumSize = new Size(228, 0) });
            ust.Controls.Add(Soru(terim));
            p.Controls.Add(ust);
            c.Margin = new Padding(0);
            p.Controls.Add(c);
            return p;
        }

        // ---------------- Neden bu paket? ----------------
        readonly Label lblNeden = new()
        {
            AutoSize = true, MaximumSize = new Size(272, 0), Padding = new Padding(8, 6, 8, 6), Margin = new Padding(0, 0, 0, 8),
            Font = new Font("Segoe UI", 9f), BackColor = Color.FromArgb(236, 244, 239), ForeColor = Color.FromArgb(29, 35, 33)
        };
        string sonGerekce = "";
        List<(Secenek s, double? puan)> sonTum = new();

        void GerekceYaz(Secenek sec, List<Secenek> liste, Sonuc r, Secenek oneri, Secenek ekonomik, Secenek premium)
        {
            string onc = oncelik >= 0 ? OnAyar[oncelik].ad : "Özel ağırlıklar";
            sonGerekce = TeklifRaporu.Gerekce(sec, liste, r, oneri, ekonomik, premium, onc);
            lblNeden.Text = "Neden bu paket?  " + sonGerekce;
        }

        // ---------------- İşlem bildirimi (durum çubuğu birkaç saniye görünür) ----------------
        readonly System.Windows.Forms.Timer bildirimSaati = new() { Interval = 7000 };
        void Bildir(string mesaj)
        {
            durum.Text = "  " + mesaj;
            durumCubugu.Visible = true;
            bildirimSaati.Stop();
            bildirimSaati.Tick -= BildirimKapat; bildirimSaati.Tick += BildirimKapat;
            bildirimSaati.Start();
        }
        void BildirimKapat(object? s, EventArgs e) { bildirimSaati.Stop(); durumCubugu.Visible = false; }

        // ---------------- Üst çubuk komutları ve kısayollar ----------------
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Action? is_ = keyData switch
            {
                Keys.Control | Keys.S => TeklifiKaydet,
                Keys.Control | Keys.P => TeklifiYazdir,
                Keys.Control | Keys.E => ExcelAktar,
                Keys.F1 => RehberGoster,
                _ => null,
            };
            if (is_ != null && veri != null) { is_(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------------- Sekmeler: düz, yeşil alt çizgili başlıklar ----------------
        void SekmeleriBicimle()
        {
            sekmeler.DrawMode = TabDrawMode.OwnerDrawFixed;
            sekmeler.SizeMode = TabSizeMode.Fixed;
            sekmeler.ItemSize = new Size(110, 36);
            sekmeler.DrawItem += (_, e) =>
            {
                var g = e.Graphics; var r = sekmeler.GetTabRect(e.Index);
                bool sec = e.Index == sekmeler.SelectedIndex;
                using (var zemin = new SolidBrush(sec ? Color.White : Zemin)) g.FillRectangle(zemin, r);
                if (sec) using (var cubuk = new SolidBrush(Yesil)) g.FillRectangle(cubuk, r.Left, r.Bottom - 3, r.Width, 3);
                using var f = new Font("Segoe UI", 9.5f, sec ? FontStyle.Bold : FontStyle.Regular);
                TextRenderer.DrawText(g, sekmeler.TabPages[e.Index].Text, f, r, sec ? Yesil : Gri,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
        }

        // ---------------- Excel'e aktar ----------------
        /// <summary>Fiyatların bugüne göre yaşı: 6 aydan eski fiyatlar için uyarı (fiyatlar teklif öncesi satıcıdan teyit edilmeli).</summary>
        string FiyatYasiMetni()
        {
            if (veri == null) return "";
            var bugun = DateTime.Today;
            var l = veri.Malzemeler.Select(m => (ad: m.Ad, tarih: m.FiyatTarihi, yas: Ortak.FiyatYasiAy(m.FiyatTarihi, bugun)))
                .Concat(veri.Yakitlar.Select(y => (ad: $"{y.Ad} ({y.Isitici})", tarih: y.FiyatTarihi, yas: Ortak.FiyatYasiAy(y.FiyatTarihi, bugun))))
                .Where(x => x.yas is int a && a > 6).OrderByDescending(x => x.yas).ToList();
            if (l.Count == 0) return "\n\nFiyat yaşı\n• Bütün fiyatlar son 6 ay içinde.";
            return "\n\nFiyat yaşı (6 aydan eski fiyatlar; teklif öncesi satıcıdan teyit edin)\n" +
                   string.Join("\n", l.GroupBy(x => (x.tarih, x.yas)).Select(gr => $"• {gr.Key.yas} ay ({gr.Key.tarih}): {string.Join(", ", gr.Select(x => x.ad.Split('(')[0].Trim()).Distinct())}"));
        }

        void ExcelAktar()
        {
            if (sonTeklif == null || veri == null) return;
            var (s, r, ekonomik, paketAdi, yil) = sonTeklif.Value;
            using var dlg = new SaveFileDialog
            {
                Filter = "Excel çalışma kitabı (*.xlsx)|*.xlsx", DefaultExt = "xlsx", AddExtension = true, OverwritePrompt = true,
                FileName = $"Teklif_{r.IlAdi}_{DateTime.Today:yyyy-MM-dd}.xlsx"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                ExcelYaz(dlg.FileName);
                Bildir("Teklif Excel'e aktarıldı: " + Path.GetFileName(dlg.FileName));
                Gunluk.Bilgi("Excel'e aktarıldı", $"{s.Kod} · {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                Gunluk.Hata("Excel dosyası yazılamadı", ex);
                MessageBox.Show("Excel dosyası yazılamadı (dosya açık olabilir).\n\n" + ex.Message, Marka.Ad, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Son hesaplanan teklifi .xlsx olarak yazar (test için de kullanılır).</summary>
        internal void ExcelYaz(string yol)
        {
            if (sonTeklif == null || veri == null) throw new InvalidOperationException("Hesaplanmış teklif yok.");
            var (s, r, ekonomik, paketAdi, yil) = sonTeklif.Value;
            var bilgi = new TeklifRaporu.Bilgi
            {
                Proje = projeMetni, Paket = paketAdi, Oncelik = oncelik >= 0 ? OnAyar[oncelik].ad : "Özel ağırlıklar",
                Tarife = r.YakitId == "DG" ? $"BOTAŞ, Nisan 2026, {TarifeAdi}" : veri.Yakitlar.FirstOrDefault(y => y.Id == r.YakitId) is Yakit yk ? (yk.Elektrikli ? $"EPDK, Nisan 2026, {TarifeAdi}" : $"{yk.FiyatKaynagi}, {yk.FiyatTarihi}") : "", Gerekce = sonGerekce, Ts825 = Ts825Metni(s, r, out _), Yil = yil
            };
            XlsxYazici.Yaz(yol, TeklifRaporu.Excel(veri, s, r, ekonomik, bilgi, sonTum));
        }

        // ---------------- İlk açılış rehberi ----------------
        static string RehberIsareti => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Marka.Ad, "rehber_goruldu");

        void IlkAcilisRehberi()
        {
            bool goruldu;
            try { goruldu = File.Exists(RehberIsareti); } catch { goruldu = true; }
            if (goruldu) return;
            RehberGoster();
            try { Directory.CreateDirectory(Path.GetDirectoryName(RehberIsareti)!); File.WriteAllText(RehberIsareti, DateTime.Now.ToString("O")); } catch { }
        }

        void RehberGoster()
        {
            var r = new RehberFormu();
            r.Show(this);
        }
    }

    /// <summary>Beş adımlık kısa kullanım rehberi (ilk açılışta ve sağ üstteki ayarlar menüsünden "Nasıl kullanılır?" ile açılır).</summary>
    public sealed class RehberFormu : Form
    {
        static readonly (string baslik, string metin)[] Adimlar =
        {
            ("1. Projeyi tanımlayın",
             "Sol panelden ili seçin: iklim (derece-gün) ve TS 825 bölgesi otomatik gelir. Ardından bina tipini ya da özel ölçüyü (en × boy × kat), ön cephe yönünü, pencere oranını ve doğramasını, ısıtma yakıtını ve klima olup olmadığını girin. Mevcut bir binaya yalıtım yapılacaksa proje türünü \"Mevcut bina (yenileme)\" seçin."),
            ("2. Önceliğinizi seçin",
             "Bütçe, Dengeli ya da Enerji ve karbon önceliklerinden birini seçin. Uygulama 10 mantolama × 2 cam × 3 duvar = 60 seçeneği hesaplar, TS 825'i sağlamayanları eler, kalanları puanlar."),
            ("3. Paketleri karşılaştırın",
             "Teklif sekmesinde üç paket görürsünüz: en ucuz, en yüksek puanlı ve en az enerji harcayan. Birine tıklayınca bina çizimi, keşif özeti ve \"Neden bu paket?\" açıklaması güncellenir. " +
             "Terimlerin yanındaki yeşil ? işaretinin üstüne gelerek açıklamayı okuyabilirsiniz."),
            ("4. Analizleri inceleyin",
             "Optimizasyon sekmesi en uygun yalıtım kalınlığını ve bütçeyle en iyi paketi bulur. Bölgesel analiz illeri iklimine göre kümeler ve her küme için standart paket önerir. Sınıflandırma, bir yerin iklim verisinden hangi paketin önerileceğini karar ağacıyla tahmin eder. " +
             "Duyarlılık sekmesi fiyat belirsizliğinde önerinin kaç denemede birinci kaldığını ve enerji fiyatı, malzeme fiyatı ya da analiz süresi değişirse önerinin aynı kalıp kalmadığını gösterir. " +
             "Harita sekmesi her ilin önerisini ve optimum kalınlığını haritada gösterir; Karşılaştır sekmesi iki senaryoyu yan yana koyar."),
            ("5. Teklifi kaydedin ve paylaşın",
             "Teklifi kaydet ile veritabanına kaydedin, Kayıtlı teklifler sekmesinden geri açın. Teklifi yazdır / PDF ile müşteriye verilecek belgeyi, Excel'e aktar ile keşif ve tüm seçenekleri içeren çalışma kitabını alın."),
        };

        int adim;
        readonly Label lblBaslik = new() { AutoSize = false, Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI Semibold", 13f), ForeColor = Color.FromArgb(31, 111, 74) };
        readonly Label lblMetin = new() { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10.5f), ForeColor = Color.FromArgb(29, 35, 33) };
        readonly Label lblSayac = new() { AutoSize = true, ForeColor = Color.FromArgb(93, 103, 99), Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 8, 16, 0) };
        readonly Button btnGeri = Dugme("‹ Geri", false), btnIleri = Dugme("İleri ›", true);

        static Button Dugme(string t, bool ana)
        {
            var b = new Button
            {
                Text = t, AutoSize = true, FlatStyle = FlatStyle.Flat, Padding = new Padding(10, 3, 10, 3), Margin = new Padding(8, 0, 0, 0), Cursor = Cursors.Hand,
                BackColor = ana ? Color.FromArgb(31, 111, 74) : Color.White, ForeColor = ana ? Color.White : Color.FromArgb(31, 111, 74), Font = new Font("Segoe UI", 9.5f)
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(31, 111, 74);
            return b;
        }

        public RehberFormu()
        {
            Text = $"{Marka.Ad} · Nasıl kullanılır?";
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 270); BackColor = Color.White;
            try { Icon = Marka.Simge(); } catch { }
            var ic = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 8) };
            ic.Controls.Add(lblMetin); ic.Controls.Add(lblBaslik);
            var alt = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 10, 16, 10), BackColor = Color.FromArgb(243, 244, 241) };
            alt.Controls.Add(btnIleri); alt.Controls.Add(btnGeri); alt.Controls.Add(lblSayac);
            Controls.Add(ic); Controls.Add(alt);
            btnGeri.Click += (_, _) => { if (adim > 0) { adim--; Goster(); } };
            btnIleri.Click += (_, _) => { if (adim < Adimlar.Length - 1) { adim++; Goster(); } else Close(); };
            AcceptButton = btnIleri;
            Goster();
        }

        void Goster()
        {
            lblBaslik.Text = Adimlar[adim].baslik;
            lblMetin.Text = Adimlar[adim].metin;
            lblSayac.Text = $"{adim + 1} / {Adimlar.Length}";
            btnGeri.Enabled = adim > 0;
            btnIleri.Text = adim == Adimlar.Length - 1 ? "Başlayalım" : "İleri ›";
        }

        /// <summary>Test için: istenen adımı göster.</summary>
        public void AdimaGit(int i) { adim = Math.Max(0, Math.Min(i, Adimlar.Length - 1)); Goster(); }
        public static int AdimSayisi => Adimlar.Length;
    }

    /// <summary>Uzun bilgi metinleri için okunaklı pencere: başlıklar yeşil ve kalın, maddeler aralıklı, kaydırılabilir.</summary>
    public sealed class BilgiFormu : Form
    {
        public BilgiFormu(string baslik, string metin)
        {
            Text = $"{Marka.Ad} · {baslik}";
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(760, 600); MinimumSize = new Size(520, 360);
            BackColor = Color.White; ShowInTaskbar = false; MinimizeBox = false;
            try { Icon = Marka.Simge(); } catch { }
            var akis = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(24, 16, 16, 16) };
            int gen = ClientSize.Width - 70;
            akis.Controls.Add(new Label { Text = baslik, AutoSize = true, Font = new Font("Segoe UI Semibold", 15f), ForeColor = Color.FromArgb(31, 111, 74), Margin = new Padding(0, 0, 0, 10) });
            foreach (var blok in metin.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var satirlar = blok.Split('\n');
                int bas = 0;
                if (!satirlar[0].StartsWith("•") && satirlar.Length > 1)
                {
                    akis.Controls.Add(new Label { Text = satirlar[0], AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Color.FromArgb(29, 35, 33), Margin = new Padding(0, 10, 0, 4) });
                    bas = 1;
                }
                // devam satırları (iki boşlukla başlayan) bir önceki maddeye eklenir
                var maddeler = new List<string>();
                foreach (var sat in satirlar.Skip(bas))
                    if (sat.StartsWith("  ") && maddeler.Count > 0) maddeler[maddeler.Count - 1] += " " + sat.Trim(); else maddeler.Add(sat);
                foreach (var m in maddeler)
                    akis.Controls.Add(new Label
                    {
                        Text = m, AutoSize = true, MaximumSize = new Size(gen, 0), Font = new Font("Segoe UI", 9.75f), ForeColor = Color.FromArgb(45, 52, 49),
                        Margin = new Padding(m.StartsWith("•") ? 6 : 0, 2, 0, 2)
                    });
            }
            var alt = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 10, 16, 10), BackColor = Color.FromArgb(243, 244, 241) };
            var kapat = new Button { Text = "Kapat", AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(31, 111, 74), ForeColor = Color.White, Padding = new Padding(12, 3, 12, 3), Cursor = Cursors.Hand };
            kapat.FlatAppearance.BorderSize = 0; kapat.Click += (_, _) => Close();
            alt.Controls.Add(kapat);
            Controls.Add(akis); Controls.Add(alt);
            AcceptButton = kapat; CancelButton = kapat;
            Resize += (_, _) => { int w = ClientSize.Width - 70; foreach (Control c in akis.Controls) if (c is Label l && l.MaximumSize.Width > 0) l.MaximumSize = new Size(w, 0); };
        }
    }
}
