using System.Drawing.Printing;
using EcoBuildss.Core.Depo;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Apartman mantolaması: kat malikleri toplantısında sorulan üç sorunun cevabı tek ekranda —
    /// daire başına ne ödeyeceğim (peşin / taksit), faturam ne kadar düşecek, kaç yılda döner.
    /// Gerçek fatura girilirse tasarruf o faturadan hesaplanır.
    /// </summary>
    public sealed class ApartmanSekmesi : UserControl
    {
        readonly Durum d;
        readonly TextBox ad = new() { Width = 240 };
        readonly ComboBox il = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 }, taksit = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 },
            dograma = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        readonly NumericUpDown daire = Sayi(1, 500, 16, 0), kat = Sayi(1, 40, 5, 0), en = Sayi(4, 200, 14, 1), boy = Sayi(4, 300, 20, 1),
            fatura = Sayi(0, 1_000_000, 0, 0, 500), yaz = Sayi(0, 100_000, 0, 0, 50), vade = Sayi(0, 10, 0, 2, 0.25m),
            katH = Sayi(2.4m, 6, 2.9m, 2), camOrani = Sayi(0, 90, 18, 0), mevcutU = Sayi(0.3m, 3.5m, 1.78m, 2), kar = Sayi(0, 100, 18, 0), indirim = Sayi(0, 60, 0, 1);
        readonly CheckBox cati = new() { Text = "Çatı arası", Checked = true, AutoSize = true },
            pencere = new() { Text = "Pencereler", AutoSize = true }, zemin = new() { Text = "Bodrum tavanı", AutoSize = true };
        readonly NumericUpDown cevre = Sayi(0, 2000, 0, 1), taban = Sayi(0, 50000, 0, 0), penEn = Sayi(30, 400, 120, 0), penBoy = Sayi(30, 400, 150, 0);
        readonly Panel digerPanel = new() { AutoSize = true, Visible = false };
        readonly Label ilBilgi = Ui.Aciklama("", 240);

        readonly List<ApartmanKarti> kartlar = new();
        readonly BinaPanel bina = new() { Dock = DockStyle.Fill };
        readonly Label[] buyuk = Enumerable.Range(0, 4).Select(_ => new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 17f), ForeColor = Ui.Koyu }).ToArray();
        readonly Label[] kucuk = Enumerable.Range(0, 4).Select(_ => new Label { AutoSize = true, MaximumSize = new Size(230, 0), Font = Ui.Kucuk, ForeColor = Ui.Soluk }).ToArray();
        readonly Label not = new() { AutoSize = false, Font = Ui.Kucuk, ForeColor = Ui.Soluk, Margin = new Padding(0, 6, 0, 0) };

        public Girdi? SonGirdi { get; private set; }
        public Sonuc? SonSonuc { get; private set; }
        List<(string ad, Secenek s, ApartmanSonucu a)> paketler = new();
        int seciliIndeks = 1;
        bool yukleniyor;

        public ApartmanSekmesi(Durum durum)
        {
            d = durum;
            mevcutU.Value = Math.Min(mevcutU.Maximum, Math.Max(mevcutU.Minimum, (decimal)d.V.P("M1")));   // Türkiye ölçümü: Mıhlayanlar & Meral (2023)
            BackColor = Ui.Zemin;
            foreach (var i in d.V.Iller.Where(i => i.Ts825Bolge.HasValue).OrderBy(i => i.Ad, StringComparer.Create(Ui.TR, false))) il.Items.Add(i);
            il.SelectedItem = d.V.VarsayilanIl;
            taksit.Items.AddRange(new object[] { "Peşin", "6 ay", "10 ay", "12 ay", "18 ay", "24 ay" }); taksit.SelectedIndex = 3;
            foreach (var x in d.V.Dogramalar) dograma.Items.Add(x);
            dograma.SelectedItem = Girdi.VarsayilanDograma; if (dograma.SelectedIndex < 0 && dograma.Items.Count > 0) dograma.SelectedIndex = 0;

            // ================= sol: girdiler =================
            var sol = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(18, 14, 8, 8), BackColor = Color.White };
            void Alan(FlowLayoutPanel p, string a, Control c) { p.Controls.Add(Ui.Etiket(a)); p.Controls.Add(c); }
            void Baslik(string a) => sol.Controls.Add(new Label { Text = a.ToUpper(Ui.TR), AutoSize = true, Font = Ui.Kalin, ForeColor = Ui.Yesil, Margin = new Padding(0, 14, 0, 0) });
            sol.Controls.Add(new Label { Text = "Apartman mantolaması", AutoSize = true, Font = new Font("Segoe UI Semibold", 14f), ForeColor = Ui.Koyu, Margin = new Padding(0, 0, 0, 4) });
            Alan(sol, "Apartman adı", ad);
            Alan(sol, "İl", il); sol.Controls.Add(ilBilgi);
            sol.Controls.Add(Izgara(("Daire", daire), ("Kat", kat), ("", null)));
            sol.Controls.Add(Izgara(("En (m)", en), ("Boy (m)", boy), ("", null)));
            Baslik("Doğal gaz faturası (bir daire)");
            sol.Controls.Add(Izgara(("Yıllık (TL)", fatura), ("Yaz, aylık (TL)", yaz)));
            Baslik("İşin kapsamı");
            sol.Controls.Add(new Label { Text = "✓ Dış cephe", AutoSize = true, Font = Ui.Yazi, Margin = new Padding(0, 6, 0, 0) });
            sol.Controls.Add(cati); sol.Controls.Add(zemin); sol.Controls.Add(pencere);
            Baslik("Ödeme");
            sol.Controls.Add(Izgara(("Taksit", taksit), ("Vade farkı (%/ay)", vade)));
            var ac = new LinkLabel { Text = "Ayarlar ▾", AutoSize = true, Font = Ui.Kalin, LinkColor = Ui.Yesil, Margin = new Padding(0, 16, 0, 4) };
            ac.LinkClicked += (_, __) => { digerPanel.Visible = !digerPanel.Visible; ac.Text = digerPanel.Visible ? "Ayarlar ▴" : "Ayarlar ▾"; };
            sol.Controls.Add(ac);
            var dp = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
            Alan(dp, "Mevcut duvar U (W/m²K)", mevcutU);
            Alan(dp, "Kat yüksekliği (m)", katH); Alan(dp, "Plan çevresi (m)", cevre); Alan(dp, "Taban alanı (m²)", taban); Alan(dp, "Pencere eni (cm)", penEn); Alan(dp, "Pencere boyu (cm)", penBoy); Alan(dp, "Pencere / dış duvar oranı (%)", camOrani); Alan(dp, "Pencere doğraması", dograma);
            Alan(dp, "Firma payı: işçilik + kâr (%)", kar); Alan(dp, "İndirim (%)", indirim);
            foreach (var c in new Control[] { cevre, taban, penEn, penBoy, mevcutU, katH, camOrani, kar, indirim }) c.Width = 120;
            digerPanel.Controls.Add(dp); sol.Controls.Add(digerPanel);

            // ================= orta =================
            var kartSatiri = new TableLayoutPanel { Dock = DockStyle.Top, Height = 124, ColumnCount = 3 };
            for (int i = 0; i < 3; i++)
            {
                kartSatiri.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
                int j = i; var k = new ApartmanKarti { Dock = DockStyle.Fill, Margin = new Padding(6) };
                k.Click += (_, __) => { seciliIndeks = j; Goster(); };
                kartlar.Add(k); kartSatiri.Controls.Add(k, i, 0);
            }
            // Toplantı özeti: dört büyük sayı
            var ozet = new TableLayoutPanel { Dock = DockStyle.Top, Height = 118, ColumnCount = 4, BackColor = Color.White, Padding = new Padding(16, 10, 10, 6), Margin = new Padding(6) };
            string[] basliklar = { "DAİRE BAŞINA", "TAKSİTLE", "ISITMA FATURASI (YILLIK, BİR DAİRE)", "KENDİNİ ÖDEME SÜRESİ" };
            for (int i = 0; i < 4; i++)
            {
                ozet.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
                p.Controls.Add(new Label { Text = basliklar[i], AutoSize = true, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Ui.Yesil });
                p.Controls.Add(buyuk[i]); p.Controls.Add(kucuk[i]);
                ozet.Controls.Add(p, i, 0);
            }
            var notPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 0, 10, 0) }; notPanel.Controls.Add(not); not.Dock = DockStyle.Fill;
            var binaKart = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) }; binaKart.Controls.Add(bina);
            var dugmeler = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(6, 6, 0, 0), BackColor = Color.White };
            dugmeler.Controls.Add(Ui.Dugme("Yazdır", (_, __) => Yazdir(), true));
            dugmeler.Controls.Add(Ui.Dugme("Kaydet", (_, __) => Kaydet()));
            var orta = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 8, 8, 0) };
            orta.Controls.Add(binaKart); orta.Controls.Add(notPanel); orta.Controls.Add(ozet); orta.Controls.Add(kartSatiri); orta.Controls.Add(dugmeler);

            var duzen = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            duzen.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290)); duzen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            duzen.Controls.Add(sol, 0, 0); duzen.Controls.Add(orta, 1, 0);
            Controls.Add(duzen);

            foreach (Control c in new Control[] { cevre, taban, penEn, penBoy, il, taksit, dograma, daire, kat, en, boy, fatura, yaz, vade, katH, camOrani, mevcutU, kar, indirim, cati, pencere, zemin })
                switch (c)
                {
                    case ComboBox cb: cb.SelectedIndexChanged += (_, __) => Degisti(); break;
                    case NumericUpDown n: n.ValueChanged += (_, __) => Degisti(); break;
                    case CheckBox ch: ch.CheckedChanged += (_, __) => Degisti(); break;
                }
            VisibleChanged += (_, __) => { if (Visible && SonSonuc == null) Hesapla(); };
            d.VeriDegisti += () => { if (SonSonuc != null) Hesapla(); };
        }

        static NumericUpDown Sayi(decimal min, decimal max, decimal deger, int ondalik, decimal? artis = null) =>
            new() { Minimum = min, Maximum = max, Value = deger, DecimalPlaces = ondalik, Increment = artis ?? (ondalik == 0 ? 1 : 0.05m), Width = 110, ThousandsSeparator = max > 9999 };

        static TableLayoutPanel Izgara(params (string ad, Control? c)[] alanlar)
        {
            var t = new TableLayoutPanel { ColumnCount = alanlar.Length, RowCount = 2, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            for (int i = 0; i < alanlar.Length; i++)
            {
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, alanlar.Length == 2 ? 124 : 82));
                if (alanlar[i].c == null) continue;
                var c = alanlar[i].c!; c.Width = alanlar.Length == 2 ? 114 : 74; c.Margin = new Padding(0, 0, 8, 0);
                t.Controls.Add(Ui.Etiket(alanlar[i].ad), i, 0); t.Controls.Add(c, i, 1);
            }
            return t;
        }

        void Degisti() { if (!yukleniyor) Hesapla(); }
        Il SeciliIl => (Il)il.SelectedItem!;
        int TaksitSayisi => taksit.SelectedIndex switch { 1 => 6, 2 => 10, 3 => 12, 4 => 18, 5 => 24, _ => 0 };

        /// <summary>Plan çevresi ve taban alanı girildiyse dikdörtgen olmayan plan, yoksa en × boy.</summary>
        Bina BinaKur() => cevre.Value > 0 && taban.Value > 0
            ? Bina.Plan((double)cevre.Value, (double)taban.Value, (int)kat.Value, (double)katH.Value)
            : Bina.Dikdortgen((double)en.Value, (double)boy.Value, (int)kat.Value, (double)katH.Value);

        public void Hesapla()
        {
            try
            {
                ilBilgi.Text = $"{SeciliIl.Ts825Bolge}. iklim bölgesi · ısıtma {Ui.Sayi(SeciliIl.IsitmaDG)} °C·gün (MGM)";
                var g = new Girdi
                {
                    Bina = BinaKur(), PencereEn = (double)penEn.Value / 100, PencereBoy = (double)penBoy.Value / 100, Il = SeciliIl, Bolge = SeciliIl.Ts825Bolge ?? 4,
                    CamOrani = (double)camOrani.Value / 100, Dograma = (string)dograma.SelectedItem!, Mevcut = true, MevcutDuvarU = (double)mevcutU.Value,
                    PencereDegisir = pencere.Checked, CatiYalitimi = cati.Checked, ZeminYalitimi = zemin.Checked, SadeceTs825 = true,
                    Kalibrasyon = d.Kalibrasyon?.Kullanilabilir == true ? d.Kalibrasyon.IsitmaKatsayisi : 1,
                };
                Oncelikler.Uygula(g, "Dengeli");
                var r = KararMotoru.Hesapla(d.V, g);
                if (r.Siralama.Count == 0) { g.SadeceTs825 = false; r = KararMotoru.Hesapla(d.V, g); }
                SonGirdi = g; SonSonuc = r;
                double? f = fatura.Value > 0 ? (double)fatura.Value : null, y = yaz.Value > 0 ? (double)yaz.Value : null;
                ApartmanSonucu A(Secenek s) => Apartman.Hesapla(d.V, r, s, (int)daire.Value, (double)kar.Value / 100, (double)indirim.Value / 100, f, y, TaksitSayisi, (double)vade.Value / 100);
                var eko = r.Siralama.OrderBy(s => s.IlkYatirim).First();
                var yuk = r.Siralama.OrderBy(s => s.YillikIsitmaKWh).ThenBy(s => s.IlkYatirim).First();
                paketler = new() { ("Ekonomik", eko, A(eko)), ("Önerilen", r.Siralama[0], A(r.Siralama[0])), ("En yüksek tasarruf", yuk, A(yuk)) };
                Goster();
            }
            catch (Exception e) { d.Hata("Hesap yapılamadı: " + e.Message, e); }
        }

        void Goster()
        {
            if (paketler.Count == 0 || SonSonuc == null) return;
            for (int i = 0; i < 3; i++) { kartlar[i].Veri = paketler[i]; kartlar[i].Secili = i == seciliIndeks; kartlar[i].Invalidate(); }
            var (pad, s, a) = paketler[seciliIndeks]; var r = SonSonuc;
            buyuk[0].Text = Ui.Tl(a.DaireBasiTL);
            kucuk[0].Text = $"KDV dahil · {a.DaireSayisi} daire eşit pay · tüm iş {Ui.Tl(a.KdvDahilTL)}";
            buyuk[1].Text = a.Taksit > 1 ? $"{a.Taksit} × {Ui.Tl(a.AylikTaksit)}" : "Peşin";
            kucuk[1].Text = a.Taksit > 1 ? (vade.Value > 0 ? $"aylık %{vade.Value:0.##} vade farkıyla, toplam {Ui.Tl(a.ToplamOdeme)}" : "vade farksız") : "taksit seçilmedi";
            buyuk[2].Text = $"−%{100 * a.AzalmaOrani:0}";
            kucuk[2].Text = $"{Ui.Tl(a.FaturaOnce)} → {Ui.Tl(a.FaturaSonra)} / yıl" + (a.FaturadanHesaplandi ? "" : " · tahmini");
            buyuk[3].Text = a.GeriOdemeYil is double gy ? $"{Ui.Sayi(gy, "0.0")} yıl" : "40+ yıl";
            kucuk[3].Text = s.Ts825Uygun ? "TS 825'e uygun ✓" : "TS 825'e uygun değil";
            not.Text = "KMK md. 42: sayı ve arsa payı çoğunluğuyla karar; gider arsa payına göre." + (s.YanginNotu.Length > 0 ? " " + s.YanginNotu : "");
            bina.Form = SonGirdi!.Bina; bina.CamOrani = SonGirdi.CamOrani; bina.KatYuksekligi = (double)katH.Value;
            bina.Yalitim = s.Yalitim; bina.Cam = s.Cam; bina.Duvar = s.Duvar; bina.DuvarU = s.DuvarU; bina.SinirU = r.SinirDuvar;
            bina.HBaz = r.HBaz; bina.HYeni = s.H; bina.CatiU = r.CatiU;
            bina.Invalidate();
        }

        public void Kaydet()
        {
            if (paketler.Count == 0 || SonSonuc == null) return;
            if (d.Db == null) { Ui.Hata(this, "Veritabanı açılamadı: " + d.DbHatasi); return; }
            var (pad, s, a) = paketler[seciliIndeks]; var b = SonGirdi!.Bina;
            var t = new KayitliTeklif
            {
                TeklifNo = d.Db.YeniTeklifNo(), Musteri = ad.Text.Trim(), MusteriTipi = "Site yönetimi", Il = SeciliIl.Ad, ProjeTuru = "Yenileme",
                KullanimAlani = SonSonuc.KullanimAlani, Paket = pad == "En yüksek tasarruf" ? "Yüksek performans" : pad, Oncelik = "Dengeli",
                YalitimId = s.Yalitim.Id, CamId = s.Cam.Id, DuvarId = s.Duvar.Id, Dograma = SonGirdi.Dograma, Yakit = "DG",
                En = (double)en.Value, Boy = (double)boy.Value, Cevre = b.En.HasValue ? 0 : (double)cevre.Value, Taban = b.En.HasValue ? 0 : (double)taban.Value, PencereEn = (double)penEn.Value / 100, PencereBoy = (double)penBoy.Value / 100, KatSayisi = b.KatSayisi, KatYuksekligi = (double)katH.Value, CamOrani = SonGirdi.CamOrani,
                TeklifTL = a.TeklifTL, TeklifTLm2 = a.TeklifTL / SonSonuc.KullanimAlani, GeriOdemeYil = a.GeriOdemeYil, IndirimOrani = (double)indirim.Value / 100,
                DaireSayisi = a.DaireSayisi,
            };
            if (d.Kabul?.Guvenilir == true)
                t.KabulOlasiligi = d.Kabul.Olasilik(new Core.Uygulamalar.TeklifOzellik { Il = t.Il, MusteriTipi = t.MusteriTipi, ProjeTuru = t.ProjeTuru, Paket = t.Paket, Oncelik = t.Oncelik,
                    KullanimAlani = t.KullanimAlani, TeklifTLm2 = t.TeklifTLm2, IndirimOrani = t.IndirimOrani, GeriOdemeYil = t.GeriOdemeYil }, d.V);
            try { d.Db.TeklifKaydet(t); d.TekliflerDegistiDuyur(); d.Bildir($"{t.TeklifNo} kaydedildi."); }
            catch (Exception e) { d.Hata("Kaydedilemedi: " + e.Message, e); Ui.Hata(this, "Kaydedilemedi: " + e.Message); }
        }

        public void Yukle(KayitliTeklif t)
        {
            yukleniyor = true;
            try
            {
                ad.Text = t.Musteri;
                var i = il.Items.Cast<Il>().FirstOrDefault(x => x.Ad == t.Il); if (i != null) il.SelectedItem = i;
                cevre.Value = Sinirla(cevre, t.Cevre); taban.Value = Sinirla(taban, t.Taban);
                if (t.PencereEn > 0) { penEn.Value = Sinirla(penEn, t.PencereEn * 100); penBoy.Value = Sinirla(penBoy, t.PencereBoy * 100); }
                if (t.En > 0) { en.Value = Sinirla(en, t.En); boy.Value = Sinirla(boy, t.Boy); kat.Value = Sinirla(kat, t.KatSayisi); katH.Value = Sinirla(katH, t.KatYuksekligi); }
                if (t.DaireSayisi > 0) daire.Value = Sinirla(daire, t.DaireSayisi);
                camOrani.Value = Sinirla(camOrani, 100 * t.CamOrani); indirim.Value = Sinirla(indirim, 100 * t.IndirimOrani);
            }
            finally { yukleniyor = false; }
            Hesapla();
            int k = paketler.FindIndex(p => p.s.Yalitim.Id == t.YalitimId); if (k >= 0) { seciliIndeks = k; Goster(); }
            d.Bildir($"{t.TeklifNo} güncel fiyatlarla açıldı (kayıtlı: {Ui.Tl(t.TeklifTL)}).");
        }

        static decimal Sinirla(NumericUpDown n, double v) => Math.Max(n.Minimum, Math.Min(n.Maximum, (decimal)v));

        /// <summary>Kat malikleri toplantısında dağıtılacak tek sayfa.</summary>
        void Yazdir()
        {
            if (paketler.Count == 0 || SonSonuc == null) return;
            var (pad, s, a) = paketler[seciliIndeks]; var r = SonSonuc;
            string firma = d.Db?.Ayar("firma_adi", "") ?? "", tel = d.Db?.Ayar("firma_telefon", "") ?? "";
            var belge = new PrintDocument { DocumentName = "EcoBuildss toplantı sayfası" };
            belge.PrintPage += (_, e) => SayfaCiz(e.Graphics!, e.MarginBounds);
            belge.EndPrint += (_, e) => { if (e.PrintAction == PrintAction.PrintToPrinter) d.Db?.Gunluk("Toplantı sayfası yazdırıldı", $"{ad.Text} · {SeciliIl.Ad} · {Ui.Tl(a.DaireBasiTL)}/daire"); };
            using var o = new PrintPreviewDialog { Document = belge, Width = 900, Height = 1000 };
            o.ShowDialog(this);
        }

        /// <summary>Toplantı sayfasının çizimi (yazıcı ya da PDF için).</summary>
        void SayfaCiz(Graphics g, Rectangle m)
        {
            if (paketler.Count == 0 || SonSonuc == null) return;
            var (pad, s, a) = paketler[seciliIndeks]; var r = SonSonuc;
            string firma = d.Db?.Ayar("firma_adi", "") ?? "", tel = d.Db?.Ayar("firma_telefon", "") ?? "";
            {
                float y = m.Top, x = m.Left, w = m.Width;
                using var fBas = new Font("Segoe UI Semibold", 16f); using var fAra = new Font("Segoe UI Semibold", 11f); using var fYazi = new Font("Segoe UI", 10f);
                using var fBuyuk = new Font("Segoe UI Semibold", 17f); using var fKucuk = new Font("Segoe UI", 8.5f);
                using var yesil = new SolidBrush(Ui.Yesil); using var gri = new SolidBrush(Ui.Soluk);
                void Yaz(string t, Font f, Brush b, float ek = 4, float sol = 0, float gen = -1) { var gw = gen < 0 ? w - sol : gen; var o = g.MeasureString(t, f, (int)gw); g.DrawString(t, f, b, new RectangleF(x + sol, y, gw, o.Height)); y += o.Height + ek; }
                if (firma.Length > 0) Yaz(firma + (tel.Length > 0 ? " · " + tel : ""), fAra, yesil, 2);
                Yaz($"{(ad.Text.Trim().Length > 0 ? ad.Text.Trim() : "Apartman")} — dış cephe ısı yalıtımı (mantolama) teklifi", fBas, Brushes.Black, 2);
                Yaz($"{SeciliIl.Ad} · {a.DaireSayisi} daire · {SonGirdi!.Bina.KatSayisi} kat · {DateTime.Today.ToString("d MMMM yyyy", Ui.TR)}", fYazi, gri, 14);
                Yaz($"Önerilen iş: {TeklifRaporu.PaketAdi(s)}" + (r.CatiYalitimi ? " + çatı arası yalıtımı" : "") + (r.ZeminYalitimi ? " + bodrum tavanı yalıtımı" : "") + (r.PencereDegisir ? " + pencere camları" : ""), fAra, Brushes.Black, 12);
                string[,] kutu =
                {
                    { "Daire başına", Ui.Tl(a.DaireBasiTL), "KDV dahil, eşit pay" },
                    { "Taksitle", a.Taksit > 1 ? $"{a.Taksit} × {Ui.Tl(a.AylikTaksit)}" : "Peşin", a.Taksit > 1 ? $"toplam {Ui.Tl(a.ToplamOdeme)}" : "" },
                    { "Isıtma faturası", $"−%{100 * a.AzalmaOrani:0}", $"yılda {Ui.Tl(a.FaturaOnce)} → {Ui.Tl(a.FaturaSonra)}" },
                    { "Kendini öder", a.GeriOdemeYil is double gy ? $"{Ui.Sayi(gy, "0.0")} yıl" : "40+ yıl", "bugünkü değerle" },
                };
                float kw = w / 4, y0 = y;
                for (int i = 0; i < 4; i++)
                {
                    float kx = x + i * kw; float yy = y0;
                    g.DrawString(kutu[i, 0].ToUpper(Ui.TR), fKucuk, yesil, kx, yy); yy += 16;
                    g.DrawString(kutu[i, 1], fBuyuk, Brushes.Black, kx, yy); yy += 36;
                    g.DrawString(kutu[i, 2], fKucuk, gri, new RectangleF(kx, yy, kw - 8, 34));
                }
                y = y0 + 100;
                Yaz("Hesabın dayanağı", fAra, Brushes.Black, 4);
                Yaz($"• Isıtma ihtiyacı TS 825:2024 derece-gün yöntemiyle hesaplandı ({SeciliIl.Ad}: {Ui.Sayi(SeciliIl.IsitmaDG)} °C·gün, MGM). Duvar U değeri {Ui.Sayi(r.MevcutDuvarU, "0.00")} → {Ui.Sayi(s.DuvarU, "0.00")} W/m²K (sınır {Ui.Sayi(r.SinirDuvar, "0.00")}).", fYazi, Brushes.Black, 2);
                Yaz(a.FaturadanHesaplandi ? $"• Tasarruf, bir dairenin gerçek doğal gaz faturasından hesaplandı (yaz aylarındaki sıcak su ve ocak payı ayrıldı)." : "• Fatura bilgisi girilmedi; tasarruf BOTAŞ tarifesi ve KDV ile tahmin edildi.", fYazi, Brushes.Black, 2);
                Yaz($"• Mantolama alanı {Ui.Sayi(r.OpakDuvar, "N0")} m² (pencereler düşülmüş). Fiyatlar KDV hariç {Ui.Tl(a.TeklifTL)}; KDV %{100 * d.V.P("KDV"):0}.", fYazi, Brushes.Black, 2);
                if (s.YanginNotu.Length > 0) Yaz("• " + s.YanginNotu, fYazi, Brushes.Black, 2);
                y += 8;
                Yaz("Karar", fAra, Brushes.Black, 4);
                Yaz("Kat Mülkiyeti Kanunu md. 42: ısı yalıtımı kararı kat maliklerinin sayı ve arsa payı çoğunluğuyla alınır; giderler arsa payı oranında ödenir. Daire başına tutar eşit pay varsayımıyla gösterilmiştir.", fYazi, Brushes.Black, 30);
                g.DrawLine(Pens.Gray, x, y + 30, x + w * 0.4f, y + 30); g.DrawLine(Pens.Gray, x + w * 0.6f, y + 30, x + w, y + 30);
                g.DrawString("Yönetici", fKucuk, gri, x, y + 34); g.DrawString("Firma yetkilisi", fKucuk, gri, x + w * 0.6f, y + 34);
            }
        }
    }

    /// <summary>Apartman paket kartı: ad, iş, daire başına tutar, faturadaki azalma.</summary>
    public sealed class ApartmanKarti : Control
    {
        public (string ad, Secenek s, ApartmanSonucu a)? Veri;
        public bool Secili;
        public ApartmanKarti() { DoubleBuffered = true; Cursor = Cursors.Hand; BackColor = Color.White; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Secili ? Ui.AcikYesil : Color.White);
            using (var k = new Pen(Secili ? Ui.Yesil : Ui.Kenar, Secili ? 2.5f : 1f)) g.DrawRectangle(k, 1, 1, Width - 3, Height - 3);
            if (Veri is not var (ad, s, a)) return;
            float y = 10, x = 14, w = Width - 28;
            void Yaz(string t, Font f, Color c, float ek = 3) { var b = g.MeasureString(t, f, (int)w); using var br = new SolidBrush(c); g.DrawString(t, f, br, new RectangleF(x, y, w, b.Height)); y += b.Height + ek; }
            Yaz(ad.ToUpper(Ui.TR) + (ad == "Önerilen" ? "  ★" : ""), Ui.Kalin, ad == "Önerilen" ? Ui.Yesil : Ui.Soluk, 1);
            Yaz(TeklifRaporu.PaketAdi(s), Ui.Kucuk, Ui.Metin, 4);
            Yaz($"{Ui.Tl(a.DaireBasiTL)} / daire", Ui.BuyukSayi, Ui.Koyu, 2);
            Yaz($"Isıtma faturası −%{100 * a.AzalmaOrani:0} · yılda {Ui.Tl(a.YillikTasarruf)} az", Ui.Kucuk, Ui.Soluk, 0);
        }
    }
}
