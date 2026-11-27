using System.Drawing.Printing;
using EcoBuildss.Core.Depo;
using EcoBuildss.Core.Uygulamalar;
using EcoBuildss.Core.Veri;

namespace EcoBuildss.UI
{
    /// <summary>Bir paketin teklif değerleri (maliyet + firma payı + indirim, geri ödeme, kabul olasılığı).</summary>
    public sealed class PaketTeklifi
    {
        public string Ad = "";
        public Secenek S = null!;
        public double Maliyet, TeklifTL, TeklifTLm2;
        public double? GeriOdeme, KabulOlasiligi;
    }

    /// <summary>
    /// Teklif ekranı. Solda yalnız temel girdiler (il, proje, ölçüler, öncelik); diğerleri "Diğer ayarlar" altında.
    /// Ortada üç paket ve binanın çizimi, altta seçilen paketin özeti. Keşif ve tüm seçenekler "Ayrıntılar" penceresinde.
    /// </summary>
    public sealed class TeklifSekmesi : UserControl
    {
        readonly Durum d;
        // ---- temel girdiler ----
        readonly ComboBox il = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 }, projeTuru = Liste(Semalar.ProjeTurleri);
        readonly NumericUpDown en = Sayi(4, 200, 12, 1), boy = Sayi(4, 300, 18, 1), kat = Sayi(1, 40, 4, 0);
        readonly RadioButton[] oncelik = Semalar.Oncelikler.Select(o => new RadioButton
        {
            Text = o == "Enerji ve karbon" ? "Enerji" : o, Tag = o, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter,
            Width = 78, Height = 30, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 2, 0), Font = Ui.Yazi, Cursor = Cursors.Hand,
        }).ToArray();
        // ---- diğer ayarlar ----
        readonly TextBox musteri = new() { Width = 240 };
        readonly ComboBox musteriTipi = Liste(Semalar.MusteriTipleri), dograma = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 },
            yakit = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        readonly NumericUpDown katH = Sayi(2.4m, 6, 3, 2), camOrani = Sayi(0, 90, 20, 0), mevcutU = Sayi(0.3m, 3.5m, 1.78m, 2), yil = Sayi(5, 50, 20, 0),
            kar = Sayi(0, 100, 18, 0), indirim = Sayi(0, 60, 0, 1), yanit = Sayi(0, 365, 2, 0);
        readonly CheckBox klima = new() { Text = "Klima var", AutoSize = true }, rakip = new() { Text = "Rakip teklif var", AutoSize = true };
        readonly NumericUpDown cevre = Sayi(0, 2000, 0, 1), taban = Sayi(0, 50000, 0, 0), penEn = Sayi(30, 400, 120, 0), penBoy = Sayi(30, 400, 150, 0);
        readonly Panel digerPanel = new() { AutoSize = true, Visible = false };
        readonly LinkLabel indirimLink = new() { Text = "İndirim", AutoSize = true, LinkColor = Ui.Yesil, Visible = false };
        readonly Label mevcutEtiket = Ui.Etiket("Mevcut duvar U (W/m²K)"), ilBilgi = Ui.Aciklama("", 240);
        // ---- sonuçlar ----
        readonly List<PaketKarti> paketKartlari = new();
        readonly BinaPanel bina = new() { Dock = DockStyle.Fill };
        // Malzemeleri özelleştir: mantolama, cam ve duvar kartları (mühendis paketi tek tek değiştirebilir)
        readonly TabControl kartAlani = new() { Dock = DockStyle.Bottom, Height = 172, Visible = false, Font = Ui.Yazi };
        readonly Button btnOzellestir = new() { Text = "Malzemeleri özelleştir  ▾", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Ui.Yesil, BackColor = Color.White, Cursor = Cursors.Hand, Font = Ui.Kalin };
        readonly List<MalzemeKarti> malzemeKartlari = new();
        TabPage? duvarSayfasi;
        readonly Label ozTutar = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 20f), ForeColor = Ui.Koyu },
            ozAlt = new() { AutoSize = true, Font = Ui.Yazi, ForeColor = Ui.Soluk }, ozSatir = new() { AutoSize = true, Font = new Font("Segoe UI", 10.5f), ForeColor = Ui.Metin },
            ozKabul = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 11f) }, ozNot = new() { AutoSize = true, Font = Ui.Kucuk, ForeColor = Ui.Soluk };

        public Girdi? SonGirdi { get; private set; }
        public Sonuc? SonSonuc { get; private set; }
        List<PaketTeklifi> paketler = new();
        PaketTeklifi? secili;
        Secenek? ekonomikSecenek;
        bool yukleniyor;

        public TeklifSekmesi(Durum durum)
        {
            d = durum;
            mevcutU.Value = Math.Min(mevcutU.Maximum, Math.Max(mevcutU.Minimum, (decimal)d.V.P("M1")));   // Türkiye ölçümü: Mıhlayanlar & Meral (2023)
            BackColor = Ui.Zemin;
            foreach (var i in d.V.Iller.Where(i => i.Ts825Bolge.HasValue).OrderBy(i => i.Ad, StringComparer.Create(Ui.TR, false))) il.Items.Add(i);
            il.SelectedItem = d.V.VarsayilanIl;
            foreach (var x in d.V.Dogramalar) dograma.Items.Add(x);
            dograma.SelectedItem = Girdi.VarsayilanDograma;
            if (dograma.SelectedIndex < 0 && dograma.Items.Count > 0) dograma.SelectedIndex = 0;
            yakit.Items.Add("DG · Doğal gaz (kombi)");
            foreach (var y in d.V.Yakitlar) yakit.Items.Add($"{y.Id} · {y}");
            yakit.SelectedIndex = 0; musteriTipi.SelectedIndex = 0; projeTuru.SelectedIndex = 0;
            oncelik[1].Checked = true;

            // ================= sol: girdiler =================
            var sol = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(18, 14, 8, 8), BackColor = Color.White };
            void Alan(FlowLayoutPanel p, string ad, Control c) { p.Controls.Add(Ui.Etiket(ad)); p.Controls.Add(c); }
            sol.Controls.Add(new Label { Text = "Yeni bina", AutoSize = true, Font = new Font("Segoe UI Semibold", 14f), ForeColor = Ui.Koyu, Margin = new Padding(0, 0, 0, 6) });
            Alan(sol, "İl", il); sol.Controls.Add(ilBilgi);
            projeTuru.SelectedItem = "Yeni bina";   // bu ekran yeni bina içindir; mevcut bina Apartman mantolaması ekranında
            var olcu = new TableLayoutPanel { ColumnCount = 3, RowCount = 2, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            for (int i = 0; i < 3; i++) olcu.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            foreach (var c in new Control[] { en, boy, kat }) { c.Width = 74; c.Margin = new Padding(0, 0, 8, 0); }
            olcu.Controls.Add(Ui.Etiket("En (m)"), 0, 0); olcu.Controls.Add(Ui.Etiket("Boy (m)"), 1, 0); olcu.Controls.Add(Ui.Etiket("Kat"), 2, 0);
            olcu.Controls.Add(en, 0, 1); olcu.Controls.Add(boy, 1, 1); olcu.Controls.Add(kat, 2, 1);
            sol.Controls.Add(olcu);
            sol.Controls.Add(Ui.Etiket("Müşterinin önceliği"));
            var onc = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            foreach (var r in oncelik) { r.FlatAppearance.CheckedBackColor = Ui.Yesil; r.FlatAppearance.BorderColor = Ui.Kenar; onc.Controls.Add(r); }
            sol.Controls.Add(onc);
            OncelikRenk();

            // "Diğer ayarlar" — gerektiğinde açılır
            var ac = new LinkLabel { Text = "Ayarlar ▾", AutoSize = true, Font = Ui.Kalin, LinkColor = Ui.Yesil, Margin = new Padding(0, 18, 0, 4) };
            ac.LinkClicked += (_, __) => { digerPanel.Visible = !digerPanel.Visible; ac.Text = digerPanel.Visible ? "Ayarlar ▴" : "Ayarlar ▾"; };
            sol.Controls.Add(ac);
            var dp = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
            Alan(dp, "Müşteri / proje adı", musteri); Alan(dp, "Müşteri tipi", musteriTipi);
            Alan(dp, "Kat yüksekliği (m)", katH); Alan(dp, "Plan çevresi (m)", cevre); Alan(dp, "Taban alanı (m²)", taban); Alan(dp, "Pencere eni (cm)", penEn); Alan(dp, "Pencere boyu (cm)", penBoy); Alan(dp, "Pencere / dış duvar oranı (%)", camOrani); Alan(dp, "Pencere doğraması", dograma);
            Alan(dp, "Isıtma", yakit); dp.Controls.Add(klima);
            dp.Controls.Add(mevcutEtiket); dp.Controls.Add(mevcutU);
            Alan(dp, "Analiz süresi (yıl)", yil); Alan(dp, "Firma payı: işçilik + kâr (%)", kar); Alan(dp, "İndirim (%)", indirim);
            Alan(dp, "Talepten teklife geçen gün", yanit); dp.Controls.Add(rakip);
            foreach (var c in new Control[] { cevre, taban, penEn, penBoy, katH, camOrani, mevcutU, yil, kar, indirim, yanit }) c.Width = 120;
            digerPanel.Controls.Add(dp);
            sol.Controls.Add(digerPanel);

            // ================= orta: paketler, bina, özet =================
            var kartlar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 132, ColumnCount = 3, Padding = new Padding(0, 0, 0, 4) };
            for (int i = 0; i < 3; i++)
            {
                kartlar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
                var k = new PaketKarti { Dock = DockStyle.Fill, Margin = new Padding(6) };
                k.Click += (s, __) => Sec(((PaketKarti)s!).Paket);
                paketKartlari.Add(k); kartlar.Controls.Add(k, i, 0);
            }
            var binaKart = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
            var ozellestirSatiri = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(0, 4, 0, 0) };
            btnOzellestir.FlatAppearance.BorderColor = Ui.Kenar;
            btnOzellestir.Click += (_, __) => { kartAlani.Visible = !kartAlani.Visible; btnOzellestir.Text = kartAlani.Visible ? "Malzeme seçimini kapat  ▴" : "Malzemeleri özelleştir  ▾"; };
            ozellestirSatiri.Controls.Add(btnOzellestir);
            foreach (var (ad, tur) in new[] { ("Dış cephe yalıtımı", 'Y'), ("Pencere camı", 'P'), ("Duvar malzemesi", 'D') })
            {
                var sira = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, BackColor = Color.White };
                // Kartlar aileye ve kalınlığa göre sıralı (EPS 5, 8, 10 … Taş yünü 5, 8, 10 …)
                foreach (var m in d.V.Malzemeler.Where(m => m.Tur == tur).OrderBy(m => m.Aile).ThenBy(m => m.Kalinlik))
                {
                    var kart = new MalzemeKarti(m);
                    kart.Secildi += (_, __) => MalzemeSec(m);
                    new ToolTip().SetToolTip(kart, $"{m.Ad}\n{m.FiyatHesabi}\nFiyat kaynağı: {m.FiyatKaynagi} ({m.FiyatTarihi})");
                    malzemeKartlari.Add(kart); sira.Controls.Add(kart);
                }
                var tp = new TabPage(ad) { BackColor = Color.White, Padding = new Padding(8, 8, 8, 0) };
                tp.Controls.Add(sira); kartAlani.TabPages.Add(tp);
                if (tur == 'D') duvarSayfasi = tp;
            }
            binaKart.Controls.Add(bina); binaKart.Controls.Add(ozellestirSatiri); binaKart.Controls.Add(kartAlani);

            var ozet = new Panel { Dock = DockStyle.Bottom, Height = 140, BackColor = Color.White, Padding = new Padding(18, 10, 14, 8) };
            ozet.Paint += (_, e) => { using var p = new Pen(Ui.Kenar); e.Graphics.DrawLine(p, 0, 0, ozet.Width, 0); };
            var ozSol = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            var tutarSatiri = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            tutarSatiri.Controls.Add(ozTutar); ozAlt.Margin = new Padding(6, 14, 18, 0); tutarSatiri.Controls.Add(ozAlt); ozKabul.Margin = new Padding(0, 12, 0, 0); tutarSatiri.Controls.Add(ozKabul);
            indirimLink.Margin = new Padding(8, 15, 0, 0); indirimLink.LinkClicked += (_, __) => IndirimAnalizi(); tutarSatiri.Controls.Add(indirimLink);
            ozSol.Controls.Add(tutarSatiri); ozSol.Controls.Add(ozSatir); ozSol.Controls.Add(ozNot);
            var ozSag = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
            var kaydet = Ui.Dugme("Kaydet", (_, __) => Kaydet(), true); kaydet.Width = 170; kaydet.AutoSize = false; kaydet.Height = 36;
            var ayrinti = Ui.Dugme("Ayrıntılar", (_, __) => Ayrintilar()); ayrinti.Width = 170; ayrinti.AutoSize = false; ayrinti.Height = 32;
            var analiz = Ui.Dugme("Analiz", (_, __) => Analiz()); analiz.Width = 170; analiz.AutoSize = false; analiz.Height = 32;
            ozSag.Controls.Add(kaydet); ozSag.Controls.Add(ayrinti); ozSag.Controls.Add(analiz);
            ozet.Controls.Add(ozSol); ozet.Controls.Add(ozSag);

            var orta = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 8, 8, 0) };
            orta.Controls.Add(binaKart); orta.Controls.Add(kartlar); orta.Controls.Add(ozet);

            var duzen = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            duzen.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290)); duzen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            duzen.Controls.Add(sol, 0, 0); duzen.Controls.Add(orta, 1, 0);
            Controls.Add(duzen);

            foreach (Control c in new Control[] { cevre, taban, penEn, penBoy, musteriTipi, il, projeTuru, dograma, yakit, en, boy, kat, katH, camOrani, mevcutU, yil, kar, indirim, yanit, klima, rakip })
                switch (c)
                {
                    case ComboBox cb: cb.SelectedIndexChanged += (_, __) => Degisti(); break;
                    case NumericUpDown n: n.ValueChanged += (_, __) => Degisti(); break;
                    case CheckBox ch: ch.CheckedChanged += (_, __) => Degisti(); break;
                }
            foreach (var r in oncelik) r.CheckedChanged += (_, __) => { OncelikRenk(); if (r.Checked) Degisti(); };
            d.ModellerDegisti += () => { if (SonSonuc != null) Hesapla(); };
            d.VeriDegisti += () => { if (SonSonuc != null) Hesapla(); };
        }

        static ComboBox Liste(string[] s) { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 }; c.Items.AddRange(s); return c; }
        static NumericUpDown Sayi(decimal min, decimal max, decimal deger, int ondalik) =>
            new() { Minimum = min, Maximum = max, Value = deger, DecimalPlaces = ondalik, Increment = ondalik == 0 ? 1 : ondalik == 1 ? 0.5m : 0.05m, Width = 240 };

        void OncelikRenk() { foreach (var r in oncelik) { r.BackColor = r.Checked ? Ui.Yesil : Color.White; r.ForeColor = r.Checked ? Color.White : Ui.Metin; } }
        void Degisti() { if (!yukleniyor && SonSonuc != null) Hesapla(); }

        Il SeciliIl => (Il)il.SelectedItem!;
        string SeciliOncelik => (string)oncelik.First(r => r.Checked).Tag!;
        string YakitKodu => ((string)yakit.SelectedItem!).Split(' ')[0];

        Girdi GirdiKur()
        {
            bool yen = (string)projeTuru.SelectedItem! == "Yenileme";
            var g = new Girdi
            {
                Bina = BinaKur(), PencereEn = (double)penEn.Value / 100, PencereBoy = (double)penBoy.Value / 100, Il = SeciliIl, Bolge = SeciliIl.Ts825Bolge ?? 4,
                CamOrani = (double)camOrani.Value / 100, Dograma = (string)dograma.SelectedItem!, Yakit = YakitKodu, Sogutma = klima.Checked, Mevcut = yen,
                MevcutDuvarU = yen ? (double)mevcutU.Value : null, Yil = (int)yil.Value, SadeceTs825 = true,
                Kalibrasyon = d.Kalibrasyon?.Kullanilabilir == true ? d.Kalibrasyon.IsitmaKatsayisi : 1,
            };
            Oncelikler.Uygula(g, SeciliOncelik);
            return g;
        }

        /// <summary>Plan çevresi ve taban alanı girildiyse dikdörtgen olmayan plan, yoksa en × boy.</summary>
        Bina BinaKur() => cevre.Value > 0 && taban.Value > 0
            ? Bina.Plan((double)cevre.Value, (double)taban.Value, (int)kat.Value, (double)katH.Value)
            : Bina.Dikdortgen((double)en.Value, (double)boy.Value, (int)kat.Value, (double)katH.Value);

        public void Hesapla()
        {
            try
            {
                bool yen = (string)projeTuru.SelectedItem! == "Yenileme";
                mevcutU.Enabled = mevcutEtiket.Enabled = yen;
                ilBilgi.Text = $"{SeciliIl.Ts825Bolge}. iklim bölgesi · ısıtma {Ui.Sayi(SeciliIl.IsitmaDG)} °C·gün";
                var g = GirdiKur();
                var r = KararMotoru.Hesapla(d.V, g);
                bool uygunYok = r.Siralama.Count == 0;
                if (uygunYok) { g.SadeceTs825 = false; r = KararMotoru.Hesapla(d.V, g); }
                SonGirdi = g; SonSonuc = r;
                var eko = r.Siralama.OrderBy(s => s.IlkYatirim).First();
                var yuksek = r.Siralama.OrderBy(s => s.YillikIsitmaKWh).ThenBy(s => s.ToplamMaliyet).First();
                ekonomikSecenek = eko;
                paketler = new List<PaketTeklifi> { PaketHesapla("Ekonomik", eko), PaketHesapla("Önerilen", r.Siralama[0]), PaketHesapla("Yüksek performans", yuksek) };
                for (int i = 0; i < 3; i++) paketKartlari[i].Paket = paketler[i];
                var onceki = secili;
                Sec(onceki != null && onceki.Ad == "Özel" && r.Tum.Any(s => s.Kod == onceki.S.Kod) ? PaketHesapla("Özel", r.Tum.First(s => s.Kod == onceki.S.Kod))
                    : paketler.FirstOrDefault(p => p.Ad == onceki?.Ad) ?? paketler[1]);
                if (uygunYok) d.Bildir("TS 825'e uyan paket yok; en yakınlar gösteriliyor.");
            }
            catch (Exception e) { d.Hata("Hesap yapılamadı: " + e.Message, e); }
        }

        PaketTeklifi PaketHesapla(string ad, Secenek s, double? indirimOrani = null)
        {
            var r = SonSonuc!;
            double ind = indirimOrani ?? (double)indirim.Value / 100;
            double carpan = (1 + (double)kar.Value / 100) * (1 - ind);
            var p = new PaketTeklifi { Ad = ad, S = s, Maliyet = s.IlkYatirim, TeklifTL = s.IlkYatirim * carpan };
            p.TeklifTLm2 = p.TeklifTL / r.KullanimAlani;
            if (r.Mevcut) p.GeriOdeme = Kpi.IskontoluGeriOdeme(p.TeklifTL, r.MevcutGiderTL - s.YillikGiderTL, r.EnerjiIskonto);
            else if (ekonomikSecenek != null) p.GeriOdeme = Kpi.IskontoluGeriOdeme((s.IlkYatirim - ekonomikSecenek.IlkYatirim) * carpan, ekonomikSecenek.YillikGiderTL - s.YillikGiderTL, r.EnerjiIskonto);
            if (d.Kabul?.Guvenilir == true) p.KabulOlasiligi = d.Kabul.Olasilik(Ozellik(p, ind), d.V);
            return p;
        }

        /// <summary>İndirim kararı: her indirim oranında kabul olasılığı ve beklenen kâr; en iyisi önerilir.</summary>
        void IndirimAnalizi()
        {
            if (secili == null || SonSonuc == null || d.Kabul?.Guvenilir != true) return;
            var sec = secili;
            double mevcut = (double)indirim.Value / 100, k = (double)kar.Value / 100;
            // Kârın tamamen eridiği noktanın biraz ötesine kadar (en çok %30)
            var egri = EcoBuildss.Core.Uygulamalar.IndirimAnalizi.Egri(i => { var p = PaketHesapla(sec.Ad, sec.S, i); return (p.KabulOlasiligi ?? 0, p.TeklifTL); }, sec.Maliyet,
                                                                         Math.Min(0.30, k / (1 + k) + 0.02));
            var en = EcoBuildss.Core.Uygulamalar.IndirimAnalizi.EnIyi(egri);
            var simdi = egri.OrderBy(n => Math.Abs(n.Indirim - mevcut)).First();

            using var f = new Form { Text = "İndirim", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(760, 520), BackColor = Color.White, Font = Ui.Yazi, MinimizeBox = false };
            var grafik = new SacilimGrafik { Dock = DockStyle.Fill, XBaslik = "İndirim (%)", YBaslik = "Beklenen kâr (TL)", XBicim = x => (x * 100).ToString("0"), SeriAd = new[] { "Beklenen kâr" } };
            grafik.Cizgiler.Add(new SacilimGrafik.Cizgi { Noktalar = egri.Select(n => (n.Indirim, n.BeklenenKar)).ToList(), Seri = 0 });
            if (en != null) grafik.Dikeyler.Add((en.Indirim, "en iyi", 1));
            grafik.Dikeyler.Add((simdi.Indirim, "şimdi", 2));
            var tablo = Ui.Izgara(); tablo.Dock = DockStyle.Fill;
            foreach (var c in new[] { "İndirim", "Teklif (KDV hariç)", "Kabul olasılığı", "Kâr", "Beklenen kâr" }) tablo.Columns.Add(c, c);
            foreach (var n in egri.Where((n, i) => i % 2 == 0 || n == en))
            {
                int i = tablo.Rows.Add("%" + (n.Indirim * 100).ToString("0"), Ui.Tl(n.TeklifTL), Ui.Olasilik(n.Olasilik), Ui.Tl(n.Kar), Ui.Tl(n.BeklenenKar));
                if (n == en) tablo.Rows[i].DefaultCellStyle.BackColor = Ui.AcikYesil;
            }
            var ozetEt = new Label { Dock = DockStyle.Top, Height = 34, Font = Ui.Kalin, Padding = new Padding(10, 8, 0, 0),
                Text = en == null ? "Hiçbir indirimde kâr kalmıyor." :
                    $"En iyi: %{en.Indirim * 100:0} indirim · olasılık {Ui.Olasilik(en.Olasilik)} · beklenen kâr {Ui.Tl(en.BeklenenKar)}   (şimdi: {Ui.Tl(simdi.BeklenenKar)})" };
            var alt = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var uygula = Ui.Dugme("Uygula", (_, __) => { if (en != null) { indirim.Value = (decimal)Math.Round(en.Indirim * 100, 1); d.Db?.Gunluk("İndirim önerisi uygulandı", $"%{en.Indirim * 100:0}"); } f.Close(); }, true);
            uygula.Enabled = en != null;
            alt.Controls.Add(uygula);
            var bolme = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            bolme.Panel1.Controls.Add(grafik); bolme.Panel2.Controls.Add(tablo);
            f.Controls.Add(bolme); f.Controls.Add(ozetEt); f.Controls.Add(alt);
            f.Shown += (_, __) => { try { bolme.SplitterDistance = bolme.Height / 2; } catch { } };
            f.ShowDialog(this);
        }

        TeklifOzellik Ozellik(PaketTeklifi p, double? indirimOrani = null) => new()
        {
            Il = SeciliIl.Ad, MusteriTipi = (string)musteriTipi.SelectedItem!, ProjeTuru = (string)projeTuru.SelectedItem!, Paket = p.Ad, Oncelik = SeciliOncelik,
            KullanimAlani = SonSonuc!.KullanimAlani, TeklifTLm2 = p.TeklifTLm2, IndirimOrani = indirimOrani ?? (double)indirim.Value / 100, YanitSuresiGun = (double)yanit.Value,
            GeriOdemeYil = p.GeriOdeme, RakipTeklif = rakip.Checked,
        };

        void Sec(PaketTeklifi? p)
        {
            if (p == null || SonSonuc == null) return;
            secili = p;
            foreach (var k in paketKartlari) { k.Secili = k.Paket?.S.Kod == p.S.Kod; k.Invalidate(); }
            var oneri = SonSonuc.Siralama[0];
            foreach (var mk in malzemeKartlari)
            {
                string id = mk.Malzeme.Id;
                mk.Secili = id == p.S.Yalitim.Id || id == p.S.Cam.Id || (!SonSonuc.Mevcut && id == p.S.Duvar.Id);
                mk.Onerilen = id == oneri.Yalitim.Id || id == oneri.Cam.Id || (!SonSonuc.Mevcut && id == oneri.Duvar.Id);
                // Yangın yönetmeliği nedeniyle bu binada kullanılamayan yalıtım soluk görünür
                mk.Enabled = mk.Malzeme.Tur != 'Y' || SonSonuc.Tum.Any(x => x.Yalitim.Id == id);
            }
            if (duvarSayfasi != null)
            {
                bool gerek = !SonSonuc.Mevcut;
                if (gerek && !kartAlani.TabPages.Contains(duvarSayfasi)) kartAlani.TabPages.Add(duvarSayfasi);
                else if (!gerek && kartAlani.TabPages.Contains(duvarSayfasi)) kartAlani.TabPages.Remove(duvarSayfasi);
            }
            var r = SonSonuc; var s = p.S;
            // Bina çizimi ve ölçekli duvar kesiti
            bina.Form = SonGirdi!.Bina; bina.CamOrani = SonGirdi.CamOrani; bina.KatYuksekligi = (double)katH.Value;
            bina.Yalitim = s.Yalitim; bina.Cam = s.Cam; bina.Duvar = s.Duvar; bina.DuvarU = s.DuvarU; bina.SinirU = r.SinirDuvar;
            bina.HBaz = r.HBaz; bina.HYeni = s.H; bina.CatiU = r.CatiU;
            bina.Invalidate();
            // Özet
            double kdv = d.V.P("KDV");
            ozTutar.Text = Ui.Tl(p.TeklifTL * (1 + kdv));
            ozAlt.Text = $"KDV dahil · {p.Ad.ToLower(Ui.TR)} paket · {Ui.Tl(p.TeklifTLm2)}/m²";
            string tasarruf = r.Mevcut ? $"Yıllık tasarruf {Ui.Tl(Math.Max(0, r.MevcutGiderTL - s.YillikGiderTL))}" : $"Yıllık enerji gideri {Ui.Tl(s.YillikGiderTL)}";
            string geri = p.GeriOdeme is double y ? (y < 0.05 ? "ek yatırım yok" : $"kendini {Ui.Sayi(y, "0.0")} yılda öder") : "enerji tasarrufu ek yatırımı karşılamıyor";
            ozSatir.Text = $"{TeklifRaporu.PaketAdi(s)}   ·   {tasarruf}   ·   {geri}   ·   {(s.Ts825Uygun ? "TS 825'e uygun ✓" : "TS 825'e uygun değil")}";
            ozKabul.Text = p.KabulOlasiligi is double k2 ? $"Kazanma olasılığı {Ui.Olasilik(k2)}" : "";
            ozKabul.ForeColor = (p.KabulOlasiligi ?? 1) >= 0.5 ? Ui.Yesil : Ui.Kirmizi;
            indirimLink.Visible = p.KabulOlasiligi.HasValue;
            ozNot.Text = s.YanginEkMaliyet > 0 ? s.YanginNotu : "";
        }

        /// <summary>Mühendis bir malzeme kartına tıklayınca seçili paketin yalnız o bileşeni değişir; teklif anında yeniden hesaplanır.</summary>
        void MalzemeSec(Malzeme m)
        {
            if (secili == null || SonSonuc == null) return;
            var s = secili.S;
            string y = m.Tur == 'Y' ? m.Id : s.Yalitim.Id, c = m.Tur == 'P' ? m.Id : s.Cam.Id, du = m.Tur == 'D' ? m.Id : s.Duvar.Id;
            var yeni = SonSonuc.Tum.FirstOrDefault(x => x.Yalitim.Id == y && x.Cam.Id == c && x.Duvar.Id == du);
            if (yeni == null) { d.Bildir($"{m.KisaAd} bu binada kullanılamaz."); return; }
            Sec(paketler.FirstOrDefault(p => p.S.Kod == yeni.Kod) ?? PaketHesapla("Özel", yeni));
            if (!yeni.Ts825Uygun) d.Bildir($"{TeklifRaporu.PaketAdi(yeni)}: TS 825'e uygun değil.");
        }

        /// <summary>Ekonomik kalınlık, bütçe, duyarlılık ve Monte Carlo: ayrı pencerede.</summary>
        void Analiz()
        {
            if (SonSonuc == null) return;
            using var f = new Form { Text = "Ayrıntılı analiz", Size = new Size(1320, 860), StartPosition = FormStartPosition.CenterParent, Font = Ui.Yazi, BackColor = Ui.Zemin };
            var a = new AnalizSekmesi(d, () => SonGirdi, () => SonSonuc) { Dock = DockStyle.Fill };
            f.Controls.Add(a);
            f.ShowDialog(this);
        }

        // ---------------- ayrıntılar penceresi ----------------
        void Ayrintilar()
        {
            if (secili == null || SonSonuc == null) return;
            var r = SonSonuc; var s = secili.S; var p = secili; double kdv = d.V.P("KDV");
            using var f = new Form { Text = $"Ayrıntılar · {p.Ad} paket", Size = new Size(1000, 680), StartPosition = FormStartPosition.CenterParent, Font = Ui.Yazi, BackColor = Ui.Zemin, MinimizeBox = false };
            var kesif = Ui.Izgara(); var tum = Ui.Izgara();
            var gerekce = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10.5f), Padding = new Padding(12), BackColor = Color.White };

            kesif.Columns.Add("k", "Kalem"); kesif.Columns.Add("m", "Miktar"); kesif.Columns.Add("b", "Birim fiyat"); kesif.Columns.Add("t", "Tutar");
            kesif.Columns[0].FillWeight = 170;
            void S(string a, string m = "", string b = "", string t = "", bool kalin = false) { int i = kesif.Rows.Add(a, m, b, t); if (kalin) kesif.Rows[i].DefaultCellStyle.Font = Ui.Kalin; }
            S("Mantolama · " + s.Yalitim.KisaAd, Ui.Sayi(r.OpakDuvar, "N1") + " m²", Ui.Tl(s.Yalitim.FiyatM2) + "/m²", Ui.Tl(r.OpakDuvar * s.Yalitim.FiyatM2));
            S("Pencere camı · " + s.Cam.KisaAd, Ui.Sayi(r.CamAlani, "N1") + " m²", Ui.Tl(s.Cam.FiyatM2) + "/m²", Ui.Tl(r.CamAlani * s.Cam.FiyatM2));
            if (!r.Mevcut) S("Duvar · " + s.Duvar.KisaAd, Ui.Sayi(r.OpakDuvar, "N1") + " m²", Ui.Tl(s.Duvar.FiyatM2) + "/m²", Ui.Tl(r.OpakDuvar * s.Duvar.FiyatM2));
            S($"Çatı ({r.Cati.KisaAd}) ve zemin ({r.Zemin.KisaAd}) yalıtımı", Ui.Sayi(r.CatiAlani + r.ZeminAlani, "N1") + " m²", "", Ui.Tl(r.SabitYatirim));
            if (s.YanginEkMaliyet > 0) S("Yangın şeridi farkı (hiç yanmaz yalıtım, Md. 27)", Ui.Sayi(r.YanginSeridiM2, "N1") + " m²", "", Ui.Tl(s.YanginEkMaliyet));
            S("Malzeme ve uygulama maliyeti", "", "", Ui.Tl(p.Maliyet), true);
            S($"Firma payı %{kar.Value} · indirim %{indirim.Value}", "", "", Ui.Tl(p.TeklifTL - p.Maliyet));
            S("Teklif (KDV hariç)", Ui.Tl(p.TeklifTLm2) + "/m²", "", Ui.Tl(p.TeklifTL), true);
            S($"KDV %{100 * kdv:0}", "", "", Ui.Tl(p.TeklifTL * kdv));
            S("Teklif (KDV dahil)", "", "", Ui.Tl(p.TeklifTL * (1 + kdv)), true);
            S("");
            S("Yıllık ısıtma ihtiyacı", Ui.Sayi(s.YillikIsitmaKWh) + " kWh", Ui.Sayi(s.YillikIsitmaKWh / r.KullanimAlani) + " kWh/m²", Ui.Tl(s.YillikGiderTL) + "/yıl");
            S($"Ömür boyu maliyet ({SonGirdi!.Yil} yıl, bugünkü değer)", "", "", Ui.Tl(s.ToplamMaliyet));
            S("Duvar U / TS 825 sınırı", $"{Ui.Sayi(s.DuvarU, "0.00")} / {Ui.Sayi(r.SinirDuvar, "0.00")} W/m²K", "", s.Ts825Uygun ? "uygun" : "uygun değil");
            S("CO₂ (malzeme + işletme)", Ui.Sayi(s.ToplamCO2Kg / 1000, "N1") + " t");

            var liste = r.Siralama;
            gerekce.Text = TeklifRaporu.Gerekce(s, liste, r, liste[0], paketler[0].S, paketler[2].S, SeciliOncelik)
                + $"\n\nÖncelik ağırlıkları (AHP): maliyet %{100 * r.Agirlik[0]:0}, enerji %{100 * r.Agirlik[1]:0}, karbon %{100 * r.Agirlik[2]:0}."
                + (s.YanginNotu.Length > 0 ? "\n\n" + s.YanginNotu : "");

            foreach (var c in new[] { "Sıra", "Mantolama", "Cam", "Duvar", "TS 825", "İlk yatırım", "Yıllık enerji", "Ömür boyu maliyet", "Puan" }) tum.Columns.Add(c, c);
            var puanli = liste.Select((x, i) => (x, i + 1)).ToDictionary(t => t.x.Kod, t => t.Item2);
            foreach (var x in r.Tum.OrderBy(x => puanli.TryGetValue(x.Kod, out int n) ? n : 999).ThenBy(x => x.ToplamMaliyet))
            {
                bool pu = puanli.TryGetValue(x.Kod, out int sira);
                int i = tum.Rows.Add(pu ? sira.ToString() : "—", x.Yalitim.KisaAd, x.Cam.KisaAd, x.DuvarAdi, x.Ts825Uygun ? "uygun" : "değil", Ui.Tl(x.IlkYatirim),
                                     Ui.Tl(x.YillikGiderTL), Ui.Tl(x.ToplamMaliyet), pu ? Ui.Sayi(100 * x.Skor, "0") : "—");
                tum.Rows[i].Tag = x;
                if (!x.Ts825Uygun) tum.Rows[i].DefaultCellStyle.ForeColor = Ui.Soluk;
            }
            tum.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && tum.Rows[e.RowIndex].Tag is Secenek x) { Sec(PaketHesapla("Özel", x)); f.Close(); } };

            var sekme = new TabControl { Dock = DockStyle.Fill };
            var t1 = new TabPage("Keşif ve maliyet"); t1.Controls.Add(kesif);
            var t2 = new TabPage("Neden bu paket?"); t2.Controls.Add(gerekce);
            var t3 = new TabPage("Tüm seçenekler"); t3.Controls.Add(tum);
            sekme.TabPages.AddRange(new[] { t1, t2, t3 });
            var alt = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8, 4, 0, 0) };
            alt.Controls.Add(Ui.Dugme("Excel", (_, __) => Excel(f)));
            alt.Controls.Add(Ui.Dugme("Yazdır", (_, __) => Yazdir(kesif, gerekce.Text, f)));
            alt.Controls.Add(Ui.Dugme("PDF", (_, __) =>
            {
                var satirlar = YaziSatirlari(kesif, gerekce.Text);
                PdfCikti.Kaydet(f, d, $"EcoBuildss_teklif_{SeciliIl.Ad}_{DateTime.Today:yyyyMMdd}.pdf", "EcoBuildss malzeme teklifi",
                                (g, m) => TeklifSayfaCiz(g, m, satirlar), "Teklif PDF", $"{SeciliIl.Ad} · {secili?.Ad}");
            }));
            f.Controls.Add(sekme); f.Controls.Add(alt);
            f.ShowDialog(this);
        }

        // ---------------- kayıt ve çıktı ----------------
        public void Kaydet()
        {
            if (secili == null || SonSonuc == null) return;
            if (d.Db == null) { Ui.Hata(this, "Veritabanı açılamadı: " + d.DbHatasi); return; }
            var g = SonGirdi!; var b = g.Bina;
            var t = new KayitliTeklif
            {
                TeklifNo = d.Db.YeniTeklifNo(), Musteri = musteri.Text.Trim(), MusteriTipi = (string)musteriTipi.SelectedItem!, Il = SeciliIl.Ad, ProjeTuru = (string)projeTuru.SelectedItem!,
                KullanimAlani = SonSonuc.KullanimAlani, Paket = secili.Ad, Oncelik = SeciliOncelik, YalitimId = secili.S.Yalitim.Id, CamId = secili.S.Cam.Id, DuvarId = secili.S.Duvar.Id,
                Dograma = g.Dograma, Yakit = g.Yakit, En = (double)en.Value, Boy = (double)boy.Value, Cevre = b.En.HasValue ? 0 : (double)cevre.Value, Taban = b.En.HasValue ? 0 : (double)taban.Value, PencereEn = (double)penEn.Value / 100, PencereBoy = (double)penBoy.Value / 100, KatSayisi = b.KatSayisi, KatYuksekligi = (double)katH.Value, CamOrani = g.CamOrani, Sogutma = g.Sogutma,
                TeklifTL = secili.TeklifTL, TeklifTLm2 = secili.TeklifTLm2, GeriOdemeYil = secili.GeriOdeme, IndirimOrani = (double)indirim.Value / 100, YanitSuresiGun = (double)yanit.Value,
                RakipTeklif = rakip.Checked, KabulOlasiligi = secili.KabulOlasiligi,
            };
            try { d.Db.TeklifKaydet(t); d.TekliflerDegistiDuyur(); d.Bildir($"{t.TeklifNo} kaydedildi."); }
            catch (Exception e) { d.Hata("Kaydedilemedi: " + e.Message, e); Ui.Hata(this, "Kaydedilemedi: " + e.Message); }
        }

        public void Yukle(KayitliTeklif t)
        {
            yukleniyor = true;
            try
            {
                musteri.Text = t.Musteri; musteriTipi.SelectedItem = t.MusteriTipi; projeTuru.SelectedItem = t.ProjeTuru;
                foreach (var r in oncelik) r.Checked = (string)r.Tag! == t.Oncelik;
                if (!oncelik.Any(r => r.Checked)) oncelik[1].Checked = true;
                var i = il.Items.Cast<Il>().FirstOrDefault(x => x.Ad == t.Il); if (i != null) il.SelectedItem = i;
                cevre.Value = Sinirla(cevre, t.Cevre); taban.Value = Sinirla(taban, t.Taban);
                if (t.PencereEn > 0) { penEn.Value = Sinirla(penEn, t.PencereEn * 100); penBoy.Value = Sinirla(penBoy, t.PencereBoy * 100); }
                if (t.En > 0) { en.Value = Sinirla(en, t.En); boy.Value = Sinirla(boy, t.Boy); kat.Value = Sinirla(kat, t.KatSayisi); katH.Value = Sinirla(katH, t.KatYuksekligi); }
                camOrani.Value = Sinirla(camOrani, 100 * t.CamOrani); klima.Checked = t.Sogutma; rakip.Checked = t.RakipTeklif;
                indirim.Value = Sinirla(indirim, 100 * t.IndirimOrani); yanit.Value = Sinirla(yanit, t.YanitSuresiGun);
                if (dograma.Items.Contains(t.Dograma)) dograma.SelectedItem = t.Dograma;
                for (int k = 0; k < yakit.Items.Count; k++) if (((string)yakit.Items[k]!).Split(' ')[0] == t.Yakit) yakit.SelectedIndex = k;
            }
            finally { yukleniyor = false; }
            Hesapla();
            var s = SonSonuc?.Tum.FirstOrDefault(x => x.Yalitim.Id == t.YalitimId && x.Cam.Id == t.CamId && x.Duvar.Id == t.DuvarId);
            if (s != null) Sec(paketler.FirstOrDefault(p => p.S.Kod == s.Kod) ?? PaketHesapla("Özel", s));
            d.Bildir($"{t.TeklifNo} güncel fiyatlarla açıldı (kayıtlı: {Ui.Tl(t.TeklifTL)}).");
        }

        static decimal Sinirla(NumericUpDown n, double v) => Math.Max(n.Minimum, Math.Min(n.Maximum, (decimal)v));

        void Excel(IWin32Window sahip)
        {
            if (secili == null || SonSonuc == null) return;
            using var f = new SaveFileDialog { Filter = "Excel çalışma kitabı|*.xlsx", FileName = $"EcoBuildss_teklif_{SeciliIl.Ad}_{DateTime.Today:yyyyMMdd}.xlsx" };
            if (f.ShowDialog(sahip) != DialogResult.OK) return;
            var r = SonSonuc;
            var bilgi = new TeklifRaporu.Bilgi
            {
                Proje = $"{musteri.Text} · {SeciliIl.Ad} · {SonGirdi!.Bina}", Paket = secili.Ad, Oncelik = SeciliOncelik, Yil = SonGirdi.Yil, Tarife = SonGirdi.Yakit,
                Gerekce = TeklifRaporu.Gerekce(secili.S, r.Siralama, r, r.Siralama[0], paketler[0].S, paketler[2].S, SeciliOncelik), Ts825 = secili.S.Ts825Uygun ? "Uygun" : "Uygun değil",
            };
            var tumListe = r.Tum.Select(s => (s, r.Siralama.Any(x => x.Kod == s.Kod) ? (double?)(100 * s.Skor) : null)).ToList();
            try { XlsxYazici.Yaz(f.FileName, TeklifRaporu.Excel(d.V, secili.S, r, paketler[0].S, bilgi, tumListe)); d.Bildir("Kaydedildi: " + f.FileName); d.Db?.Gunluk("Teklif Excel'e aktarıldı", $"{secili.Ad} · {SeciliIl.Ad} · {Path.GetFileName(f.FileName)}"); }
            catch (Exception e) { d.Hata("Excel dosyası yazılamadı: " + e.Message, e); Ui.Hata(sahip, "Excel dosyası yazılamadı: " + e.Message); }
        }

        List<(string a, string b, bool k)> YaziSatirlari(DataGridView kesif, string gerekce)
        {
            var satirlar = new List<(string a, string b, bool k)>
            {
                ("EcoBuildss · Malzeme teklifi", DateTime.Today.ToString("d MMMM yyyy", Ui.TR), true),
                ("Müşteri", musteri.Text, false), ("Proje", $"{SeciliIl.Ad} · {(string)projeTuru.SelectedItem!} · {SonGirdi!.Bina}", false), ("", "", false),
            };
            foreach (DataGridViewRow r in kesif.Rows) satirlar.Add(($"{r.Cells[0].Value} {r.Cells[1].Value}".Trim(), $"{r.Cells[3].Value}", r.DefaultCellStyle.Font == Ui.Kalin));
            satirlar.Add(("", "", false)); satirlar.Add(("Gerekçe", gerekce.Split('\n')[0], false));
            return satirlar;
        }

        static void TeklifSayfaCiz(Graphics g, Rectangle m, List<(string a, string b, bool k)> satirlar)
        {
            float y = m.Top, x = m.Left, w = m.Width;
            using var f = new Font("Segoe UI", 9.5f); using var fk = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            foreach (var (a, b, k) in satirlar)
            {
                var ff = k ? fk : f;
                var boyut = g.MeasureString(a, ff, (int)(w * 0.62));
                g.DrawString(a, ff, Brushes.Black, new RectangleF(x, y, w * 0.62f, boyut.Height));
                g.DrawString(b, ff, Brushes.Black, new RectangleF(x + w * 0.64f, y, w * 0.36f, boyut.Height));
                y += Math.Max(boyut.Height, ff.Height) + 4;
            }
        }

        void Yazdir(DataGridView kesif, string gerekce, IWin32Window sahip)
        {
            var satirlar = YaziSatirlari(kesif, gerekce);
            var belge = new PrintDocument { DocumentName = "EcoBuildss teklif" };
            belge.EndPrint += (_, e) => { if (e.PrintAction == PrintAction.PrintToPrinter) d.Db?.Gunluk("Teklif yazdırıldı", $"{SeciliIl.Ad} · {secili?.Ad}"); };
            belge.PrintPage += (_, e) => TeklifSayfaCiz(e.Graphics!, e.MarginBounds, satirlar);
            using var o = new PrintPreviewDialog { Document = belge, Width = 900, Height = 1000 };
            o.ShowDialog(sahip);
        }
    }

    /// <summary>Sade paket kartı: ad, malzemeler, teklif tutarı, yıllık enerji gideri, TS 825.</summary>
    public sealed class PaketKarti : Control
    {
        public PaketTeklifi? Paket;
        public bool Secili;
        public PaketKarti() { DoubleBuffered = true; Cursor = Cursors.Hand; BackColor = Color.White; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Secili ? Ui.AcikYesil : Color.White);
            using (var k = new Pen(Secili ? Ui.Yesil : Ui.Kenar, Secili ? 2.5f : 1f)) g.DrawRectangle(k, 1, 1, Width - 3, Height - 3);
            if (Paket == null) return;
            var s = Paket.S; float y = 10, x = 14, w = Width - 28;
            void Yaz(string t, Font f, Color c, float ek = 3) { var b = g.MeasureString(t, f, (int)w); using var br = new SolidBrush(c); g.DrawString(t, f, br, new RectangleF(x, y, w, b.Height)); y += b.Height + ek; }
            Yaz(Paket.Ad.ToUpper(Ui.TR) + (Paket.Ad == "Önerilen" ? "  ★" : ""), Ui.Kalin, Paket.Ad == "Önerilen" ? Ui.Yesil : Ui.Soluk, 1);
            Yaz(TeklifRaporu.PaketAdi(s), Ui.Kucuk, Ui.Metin, 4);
            Yaz(Ui.Tl(Paket.TeklifTL), Ui.BuyukSayi, Ui.Koyu, 2);
            Yaz($"Yıllık enerji {Ui.Tl(s.YillikGiderTL)}" + (s.Ts825Uygun ? "  ·  TS 825 ✓" : "  ·  TS 825 ✗"), Ui.Kucuk, s.Ts825Uygun ? Ui.Soluk : Ui.Kirmizi, 0);
        }
    }
}
