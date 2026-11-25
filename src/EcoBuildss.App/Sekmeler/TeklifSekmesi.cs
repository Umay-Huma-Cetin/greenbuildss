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

    public sealed class TeklifSekmesi : UserControl
    {
        readonly Durum d;
        // ---- girdiler ----
        readonly TextBox musteri = new() { Width = 250 };
        readonly ComboBox musteriTipi = Liste(Semalar.MusteriTipleri), il = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 },
            projeTuru = Liste(Semalar.ProjeTurleri), dograma = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 },
            yakit = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 }, oncelik = Liste(Semalar.Oncelikler);
        readonly NumericUpDown en = Sayi(4, 200, 12, 1), boy = Sayi(4, 300, 18, 1), kat = Sayi(1, 40, 4, 0), katH = Sayi(2.4m, 6, 3, 2),
            camOrani = Sayi(0, 90, 20, 0), mevcutU = Sayi(0.3m, 3.5m, 1.78m, 2), yil = Sayi(5, 50, 20, 0),
            kar = Sayi(0, 100, 18, 0), indirim = Sayi(0, 60, 0, 1), yanit = Sayi(0, 365, 2, 0);
        readonly CheckBox klima = new() { Text = "Klima ile soğutma var", AutoSize = true }, rakip = new() { Text = "Müşteri başka firmadan da teklif aldı", AutoSize = true };
        readonly Label ilBilgi = Ui.Aciklama("", 250), mevcutEtiket = Ui.Etiket("Mevcut duvar U (W/m²K)");
        // ---- sonuçlar ----
        readonly FlowLayoutPanel rozetler = new() { Dock = DockStyle.Top, Height = 36, Padding = new Padding(6, 2, 0, 0) };
        readonly TableLayoutPanel kartlar = new() { Dock = DockStyle.Top, Height = 236, ColumnCount = 3 };
        readonly DataGridView kesif = Ui.Izgara(), tum = Ui.Izgara();
        readonly Label gerekce = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10.5f), Padding = new Padding(10), ForeColor = Ui.Metin };
        readonly List<PaketKarti> paketKartlari = new();

        public Girdi? SonGirdi { get; private set; }
        public Sonuc? SonSonuc { get; private set; }
        List<PaketTeklifi> paketler = new();
        PaketTeklifi? secili;
        bool yukleniyor;

        public TeklifSekmesi(Durum durum)
        {
            d = durum;
            BackColor = Ui.Zemin;
            foreach (var i in d.V.Iller.Where(i => i.Ts825Bolge.HasValue).OrderBy(i => i.Ad, StringComparer.Create(Ui.TR, false))) il.Items.Add(i);
            il.SelectedItem = d.V.VarsayilanIl;
            foreach (var x in d.V.Dogramalar) dograma.Items.Add(x);
            dograma.SelectedItem = Girdi.VarsayilanDograma;
            if (dograma.SelectedIndex < 0 && dograma.Items.Count > 0) dograma.SelectedIndex = 0;
            yakit.Items.Add("DG · Doğal gaz (yoğuşmalı kombi)");
            foreach (var y in d.V.Yakitlar) yakit.Items.Add($"{y.Id} · {y}");
            yakit.SelectedIndex = 0; oncelik.SelectedItem = "Dengeli"; musteriTipi.SelectedIndex = 0; projeTuru.SelectedIndex = 0;

            // ---- sol: girdi paneli ----
            var sol = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(14, 8, 8, 8), BackColor = Color.White };
            void Grup(string ad) => sol.Controls.Add(new Label { Text = ad.ToUpper(Ui.TR), AutoSize = true, Font = Ui.Kalin, ForeColor = Ui.Yesil, Margin = new Padding(0, 14, 0, 2) });
            void Alan(string ad, Control c) { sol.Controls.Add(Ui.Etiket(ad)); sol.Controls.Add(c); }
            Grup("Müşteri");
            Alan("Müşteri / proje adı", musteri); Alan("Müşteri tipi", musteriTipi);
            Grup("Konum ve proje");
            Alan("İl", il); sol.Controls.Add(ilBilgi); Alan("Proje türü", projeTuru);
            sol.Controls.Add(mevcutEtiket); sol.Controls.Add(mevcutU);
            sol.Controls.Add(Ui.Aciklama("Yenilemede mevcut duvar korunur, yalıtım üstüne eklenir. U değeri enerji kimlik belgesinden ya da projeden alınır.", 250));
            Grup("Bina ölçüleri (dikdörtgen plan)");
            var olcu = new TableLayoutPanel { ColumnCount = 2, RowCount = 4, AutoSize = true, Margin = new Padding(0) };
            olcu.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125)); olcu.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            foreach (var c in new Control[] { en, boy, kat, katH }) { c.Width = 115; c.Margin = new Padding(0, 0, 6, 4); }
            olcu.Controls.Add(Ui.Etiket("En (m)"), 0, 0); olcu.Controls.Add(Ui.Etiket("Boy (m)"), 1, 0);
            olcu.Controls.Add(en, 0, 1); olcu.Controls.Add(boy, 1, 1);
            olcu.Controls.Add(Ui.Etiket("Kat sayısı"), 0, 2); olcu.Controls.Add(Ui.Etiket("Kat yüksekliği (m)"), 1, 2);
            olcu.Controls.Add(kat, 0, 3); olcu.Controls.Add(katH, 1, 3);
            sol.Controls.Add(olcu);
            Alan("Pencere / dış duvar oranı (%)", camOrani); Alan("Pencere doğraması", dograma);
            Grup("Enerji");
            Alan("Isıtma", yakit); sol.Controls.Add(klima); Alan("Analiz süresi (yıl)", yil);
            Grup("Müşteri önceliği (AHP)");
            sol.Controls.Add(oncelik);
            Grup("Ticari koşullar");
            Alan("Firma payı: işçilik + kâr (%)", kar); Alan("İndirim (%)", indirim); Alan("Talepten teklife geçen süre (gün)", yanit); sol.Controls.Add(rakip);
            sol.Controls.Add(Ui.Dugme("Hesapla", (_, __) => Hesapla(), true));

            // ---- sağ: sonuçlar ----
            for (int i = 0; i < 3; i++) { kartlar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f)); var k = new PaketKarti { Dock = DockStyle.Fill, Margin = new Padding(6) }; k.Click += (s, __) => Sec(((PaketKarti)s!).Paket); paketKartlari.Add(k); kartlar.Controls.Add(k, i, 0); }
            var detay = new TabControl { Dock = DockStyle.Fill, Font = Ui.Yazi };
            var t1 = new TabPage("Keşif ve maliyet"); t1.Controls.Add(kesif);
            var t2 = new TabPage("Neden bu paket?"); t2.Controls.Add(gerekce); t2.BackColor = Color.White;
            var t3 = new TabPage("Tüm seçenekler (çift tıkla: teklife al)"); t3.Controls.Add(tum);
            detay.TabPages.AddRange(new[] { t1, t2, t3 });
            tum.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && tum.Rows[e.RowIndex].Tag is Secenek s) Sec(PaketHesapla("Özel", s)); };
            var dugmeler = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(6, 2, 0, 0) };
            dugmeler.Controls.Add(Ui.Dugme("Teklifi kaydet (Ctrl+S)", (_, __) => Kaydet(), true));
            dugmeler.Controls.Add(Ui.Dugme("Excel'e aktar", (_, __) => Excel()));
            dugmeler.Controls.Add(Ui.Dugme("Yazdır / PDF", (_, __) => Yazdir()));
            var sag = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
            sag.Controls.Add(detay); sag.Controls.Add(dugmeler); sag.Controls.Add(kartlar); sag.Controls.Add(rozetler);

            var duzen = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            duzen.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300)); duzen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            duzen.Controls.Add(sol, 0, 0); duzen.Controls.Add(sag, 1, 0);
            Controls.Add(duzen);

            foreach (Control c in new Control[] { musteriTipi, il, projeTuru, dograma, yakit, oncelik, en, boy, kat, katH, camOrani, mevcutU, yil, kar, indirim, yanit, klima, rakip })
                switch (c)
                {
                    case ComboBox cb: cb.SelectedIndexChanged += (_, __) => Degisti(); break;
                    case NumericUpDown n: n.ValueChanged += (_, __) => Degisti(); break;
                    case CheckBox ch: ch.CheckedChanged += (_, __) => Degisti(); break;
                }
            d.ModellerDegisti += () => { if (SonSonuc != null) Hesapla(); };
        }

        static ComboBox Liste(string[] s) { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 }; c.Items.AddRange(s); return c; }
        static NumericUpDown Sayi(decimal min, decimal max, decimal deger, int ondalik) =>
            new() { Minimum = min, Maximum = max, Value = deger, DecimalPlaces = ondalik, Increment = ondalik == 0 ? 1 : ondalik == 1 ? 0.5m : 0.05m, Width = 250 };

        void Degisti() { if (!yukleniyor && SonSonuc != null) Hesapla(); }

        Il SeciliIl => (Il)il.SelectedItem!;
        string YakitKodu => ((string)yakit.SelectedItem!).Split(' ')[0];

        Girdi GirdiKur()
        {
            bool yen = (string)projeTuru.SelectedItem! == "Yenileme";
            var g = new Girdi
            {
                Bina = Bina.Dikdortgen((double)en.Value, (double)boy.Value, (int)kat.Value, (double)katH.Value), Il = SeciliIl, Bolge = SeciliIl.Ts825Bolge ?? 4,
                CamOrani = (double)camOrani.Value / 100, Dograma = (string)dograma.SelectedItem!, Yakit = YakitKodu, Sogutma = klima.Checked, Mevcut = yen,
                MevcutDuvarU = yen ? (double)mevcutU.Value : null, Yil = (int)yil.Value, SadeceTs825 = true,
                Kalibrasyon = d.Kalibrasyon?.Kullanilabilir == true ? d.Kalibrasyon.IsitmaKatsayisi : 1,
            };
            Oncelikler.Uygula(g, (string)oncelik.SelectedItem!);
            return g;
        }

        public void Hesapla()
        {
            try
            {
                mevcutU.Enabled = mevcutEtiket.Enabled = (string)projeTuru.SelectedItem! == "Yenileme";
                ilBilgi.Text = $"Isıtma {Ui.Sayi(SeciliIl.IsitmaDG)} · soğutma {Ui.Sayi(SeciliIl.SogutmaDG)} °C·gün (MGM) · TS 825 {SeciliIl.Ts825Bolge}. bölge";
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
                Rozetler(uygunYok);
                TumListe(r);
                Sec(secili != null && r.Tum.Any(s => s.Kod == secili.S.Kod) && secili.Ad == "Özel" ? PaketHesapla("Özel", r.Tum.First(s => s.Kod == secili.S.Kod)) : paketler[1]);
            }
            catch (Exception e) { d.Bildir("Hesap yapılamadı: " + e.Message); }
        }

        Secenek? ekonomikSecenek;

        PaketTeklifi PaketHesapla(string ad, Secenek s)
        {
            var r = SonSonuc!;
            double carpan = (1 + (double)kar.Value / 100) * (1 - (double)indirim.Value / 100);
            var p = new PaketTeklifi { Ad = ad, S = s, Maliyet = s.IlkYatirim, TeklifTL = s.IlkYatirim * carpan };
            p.TeklifTLm2 = p.TeklifTL / r.KullanimAlani;
            if (r.Mevcut) p.GeriOdeme = Kpi.IskontoluGeriOdeme(p.TeklifTL, r.MevcutGiderTL - s.YillikGiderTL, r.EnerjiIskonto);
            else if (ekonomikSecenek != null) p.GeriOdeme = Kpi.IskontoluGeriOdeme((s.IlkYatirim - ekonomikSecenek.IlkYatirim) * carpan, ekonomikSecenek.YillikGiderTL - s.YillikGiderTL, r.EnerjiIskonto);
            if (d.Kabul?.Guvenilir == true) p.KabulOlasiligi = d.Kabul.Olasilik(Ozellik(p), d.V);
            return p;
        }

        TeklifOzellik Ozellik(PaketTeklifi p) => new()
        {
            Il = SeciliIl.Ad, MusteriTipi = (string)musteriTipi.SelectedItem!, ProjeTuru = (string)projeTuru.SelectedItem!, Paket = p.Ad, Oncelik = (string)oncelik.SelectedItem!,
            KullanimAlani = SonSonuc!.KullanimAlani, TeklifTLm2 = p.TeklifTLm2, IndirimOrani = (double)indirim.Value / 100, YanitSuresiGun = (double)yanit.Value,
            GeriOdemeYil = p.GeriOdeme, RakipTeklif = rakip.Checked,
        };

        void Rozetler(bool uygunYok)
        {
            rozetler.Controls.Clear();
            var k = d.Kalibrasyon;
            rozetler.Controls.Add(k?.Kullanilabilir == true ? Ui.Rozet($"Enerji gerçek tüketimle kalibre: ×{Ui.Sayi(k.IsitmaKatsayisi, "0.00")}", 1) : Ui.Rozet("Enerji: kalibre edilmemiş TS 825 modeli", 2));
            var m = d.Kabul;
            rozetler.Controls.Add(m?.Guvenilir == true ? Ui.Rozet($"Kabul tahmini açık · test F1 {Ui.Sayi(m.Test.F1, "0.000")}", 1)
                : Ui.Rozet(m == null ? "Kabul tahmini kapalı: teklif geçmişi yok" : "Kabul tahmini kapalı: model güvenilirlik eşiğini geçmedi", 2));
            if (uygunYok) rozetler.Controls.Add(Ui.Rozet("Hiçbir paket TS 825 sınırlarını sağlamıyor", 3));
        }

        void TumListe(Sonuc r)
        {
            tum.Columns.Clear(); tum.Rows.Clear();
            foreach (var c in new[] { "Sıra", "Mantolama", "Cam", "Duvar", "TS 825", "İlk yatırım", "Yıllık ısıtma (kWh)", "Yıllık enerji (TL)", "Ömür boyu maliyet", "CO₂ (t)", "TOPSIS puanı" }) tum.Columns.Add(c, c);
            var puanli = r.Siralama.Select((s, i) => (s, i + 1)).ToDictionary(t => t.s.Kod, t => t.Item2);
            foreach (var s in r.Tum.OrderBy(s => puanli.TryGetValue(s.Kod, out int n) ? n : 999).ThenBy(s => s.ToplamMaliyet))
            {
                bool p = puanli.TryGetValue(s.Kod, out int sira);
                int i = tum.Rows.Add(p ? sira.ToString() : "—", s.Yalitim.KisaAd, s.Cam.KisaAd, s.DuvarAdi, s.Ts825Uygun ? "Uygun" : "Değil", Ui.Tl(s.IlkYatirim),
                                     Ui.Sayi(s.YillikIsitmaKWh), Ui.Tl(s.YillikGiderTL), Ui.Tl(s.ToplamMaliyet), Ui.Sayi(s.ToplamCO2Kg / 1000, "N1"), p ? Ui.Sayi(100 * s.Skor, "0.0") : "—");
                tum.Rows[i].Tag = s;
                if (!s.Ts825Uygun) tum.Rows[i].DefaultCellStyle.ForeColor = Ui.Soluk;
            }
        }

        void Sec(PaketTeklifi? p)
        {
            if (p == null || SonSonuc == null) return;
            secili = p;
            foreach (var k in paketKartlari) { k.Secili = k.Paket?.S.Kod == p.S.Kod && k.Paket?.Ad == p.Ad; k.Invalidate(); }
            var r = SonSonuc; var s = p.S; double kdv = d.V.P("KDV");
            kesif.Columns.Clear(); kesif.Rows.Clear();
            kesif.Columns.Add("k", "Kalem"); kesif.Columns.Add("m", "Miktar"); kesif.Columns.Add("b", "Birim fiyat"); kesif.Columns.Add("t", "Tutar");
            kesif.Columns[0].FillWeight = 160;
            void S(string a, string m = "", string b = "", string t = "", bool kalin = false) { int i = kesif.Rows.Add(a, m, b, t); if (kalin) kesif.Rows[i].DefaultCellStyle.Font = Ui.Kalin; }
            double carpan = p.TeklifTL / Math.Max(1, p.Maliyet);
            S($"Paket: {p.Ad}", TeklifRaporu.PaketAdi(s), "", "", true);
            S("Mantolama · " + s.Yalitim.KisaAd, Ui.Sayi(r.OpakDuvar, "N1") + " m²", Ui.Tl(s.Yalitim.FiyatM2) + "/m²", Ui.Tl(r.OpakDuvar * s.Yalitim.FiyatM2));
            S("Pencere camı · " + s.Cam.KisaAd, Ui.Sayi(r.CamAlani, "N1") + " m²", Ui.Tl(s.Cam.FiyatM2) + "/m²", Ui.Tl(r.CamAlani * s.Cam.FiyatM2));
            if (!r.Mevcut) S("Duvar · " + s.Duvar.KisaAd, Ui.Sayi(r.OpakDuvar, "N1") + " m²", Ui.Tl(s.Duvar.FiyatM2) + "/m²", Ui.Tl(r.OpakDuvar * s.Duvar.FiyatM2));
            S($"Çatı ({r.Cati.KisaAd}) ve zemin ({r.Zemin.KisaAd}) yalıtımı", Ui.Sayi(r.CatiAlani + r.ZeminAlani, "N1") + " m²", "", Ui.Tl(r.SabitYatirim));
            if (s.YanginEkMaliyet > 0) S("Yangın şeridi farkı (hiç yanmaz yalıtım, Md. 27)", Ui.Sayi(r.YanginSeridiM2, "N1") + " m²", "", Ui.Tl(s.YanginEkMaliyet));
            S("Malzeme ve uygulama maliyeti", "", "", Ui.Tl(p.Maliyet), true);
            S($"Firma payı %{kar.Value} · indirim %{indirim.Value}", "", "", Ui.Tl(p.TeklifTL - p.Maliyet));
            S("TEKLİF (KDV hariç)", Ui.Tl(p.TeklifTLm2) + "/m²", "", Ui.Tl(p.TeklifTL), true);
            S($"KDV %{100 * kdv:0}", "", "", Ui.Tl(p.TeklifTL * kdv));
            S("TEKLİF (KDV dahil)", "", "", Ui.Tl(p.TeklifTL * (1 + kdv)), true);
            S("");
            S("Yıllık ısıtma ihtiyacı", Ui.Sayi(s.YillikIsitmaKWh) + " kWh", Ui.Sayi(s.YillikIsitmaKWh / r.KullanimAlani, "N0") + " kWh/m²", Ui.Tl(s.YillikGiderTL) + "/yıl");
            S($"Ömür boyu maliyet ({SonGirdi!.Yil} yıl, bugünkü değer)", "", "", Ui.Tl(s.ToplamMaliyet));
            S(r.Mevcut ? "İskontolu geri ödeme (mevcut binaya göre)" : "İskontolu geri ödeme (ekonomik pakete göre)", p.GeriOdeme.HasValue ? Ui.Sayi(p.GeriOdeme.Value, "0.0") + " yıl" : "süre içinde dönmüyor");
            S($"Duvar U / TS 825 sınırı", $"{Ui.Sayi(s.DuvarU, "0.00")} / {Ui.Sayi(r.SinirDuvar, "0.00")} W/m²K", "", s.Ts825Uygun ? "TS 825 uygun" : "TS 825 değil");
            S("CO₂ (malzeme + işletme)", Ui.Sayi(s.ToplamCO2Kg / 1000, "N1") + " t");
            if (p.KabulOlasiligi.HasValue) S("Tahmini kabul olasılığı", Ui.Yuzde(p.KabulOlasiligi.Value, "0"), d.Kabul!.SecilenModel, p.KabulOlasiligi.Value >= d.Kabul.Esik ? "olumlu" : "riskli", true);

            var liste = SonSonuc.Siralama;
            gerekce.Text = TeklifRaporu.Gerekce(s, liste, r, liste[0], paketler[0].S, paketler[2].S, (string)oncelik.SelectedItem!)
                + $"\n\nÖncelik ağırlıkları (AHP): maliyet %{100 * r.Agirlik[0]:0}, enerji %{100 * r.Agirlik[1]:0}, karbon %{100 * r.Agirlik[2]:0} · tutarlılık oranı CR = {Ui.Sayi(r.CR, "0.00")}."
                + (s.YanginNotu.Length > 0 ? "\n\n" + s.YanginNotu : "")
                + (r.Kalibrasyon != 1 ? $"\n\nEnerji hesabı, içe aktarılan gerçek tüketim kayıtlarıyla bulunan ×{Ui.Sayi(r.Kalibrasyon, "0.00")} katsayısıyla düzeltildi." : "")
                + (p.KabulOlasiligi.HasValue ? $"\n\nKabul olasılığı, firmanın teklif geçmişinden öğrenen {d.Kabul!.SecilenModel} modelinden (test F1 {Ui.Sayi(d.Kabul.Test.F1, "0.000")}). " +
                   "Olasılığı en çok fiyat endeksi, yanıt süresi ve önceliğe uygun paket etkiler." : "");
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
                KullanimAlani = SonSonuc.KullanimAlani, Paket = secili.Ad, Oncelik = (string)oncelik.SelectedItem!, YalitimId = secili.S.Yalitim.Id, CamId = secili.S.Cam.Id, DuvarId = secili.S.Duvar.Id,
                Dograma = g.Dograma, Yakit = g.Yakit, En = b.En ?? 0, Boy = b.Boy ?? 0, KatSayisi = b.KatSayisi, KatYuksekligi = (double)katH.Value, CamOrani = g.CamOrani, Sogutma = g.Sogutma,
                TeklifTL = secili.TeklifTL, TeklifTLm2 = secili.TeklifTLm2, GeriOdemeYil = secili.GeriOdeme, IndirimOrani = (double)indirim.Value / 100, YanitSuresiGun = (double)yanit.Value,
                RakipTeklif = rakip.Checked, KabulOlasiligi = secili.KabulOlasiligi,
            };
            try { d.Db.TeklifKaydet(t); d.TekliflerDegistiDuyur(); d.Bildir($"{t.TeklifNo} kaydedildi. Sonucu belli olunca Kayıtlı teklifler sekmesinden Kabul/Red işaretleyin; model bu sonuçlardan öğrenir."); }
            catch (Exception e) { Ui.Hata(this, "Kaydedilemedi: " + e.Message); }
        }

        public void Yukle(KayitliTeklif t)
        {
            yukleniyor = true;
            try
            {
                musteri.Text = t.Musteri; musteriTipi.SelectedItem = t.MusteriTipi; projeTuru.SelectedItem = t.ProjeTuru; oncelik.SelectedItem = t.Oncelik;
                var i = il.Items.Cast<Il>().FirstOrDefault(x => x.Ad == t.Il); if (i != null) il.SelectedItem = i;
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
            d.Bildir($"{t.TeklifNo} güncel fiyat ve iklim verisiyle yeniden hesaplandı (kayıttaki tutar: {Ui.Tl(t.TeklifTL)}).");
        }

        static decimal Sinirla(NumericUpDown n, double v) => Math.Max(n.Minimum, Math.Min(n.Maximum, (decimal)v));

        void Excel()
        {
            if (secili == null || SonSonuc == null) return;
            using var f = new SaveFileDialog { Filter = "Excel çalışma kitabı|*.xlsx", FileName = $"EcoBuildss_teklif_{SeciliIl.Ad}_{DateTime.Today:yyyyMMdd}.xlsx" };
            if (f.ShowDialog(this) != DialogResult.OK) return;
            var r = SonSonuc;
            var bilgi = new TeklifRaporu.Bilgi
            {
                Proje = $"{musteri.Text} · {SeciliIl.Ad} · {SonGirdi!.Bina}", Paket = secili.Ad, Oncelik = (string)oncelik.SelectedItem!, Yil = SonGirdi.Yil,
                Tarife = SonGirdi.Yakit, Gerekce = gerekce.Text.Split('\n')[0], Ts825 = secili.S.Ts825Uygun ? "Uygun" : "Uygun değil",
            };
            var tumListe = r.Tum.Select(s => (s, r.Siralama.Any(x => x.Kod == s.Kod) ? (double?)(100 * s.Skor) : null)).ToList();
            try { XlsxYazici.Yaz(f.FileName, TeklifRaporu.Excel(d.V, secili.S, r, paketler[0].S, bilgi, tumListe)); d.Bildir("Excel dosyası yazıldı: " + f.FileName); }
            catch (Exception e) { Ui.Hata(this, "Excel dosyası yazılamadı: " + e.Message); }
        }

        void Yazdir()
        {
            if (secili == null) return;
            var satirlar = new List<(string a, string b, bool k)>
            {
                ("EcoBuildss · Malzeme teklifi", DateTime.Today.ToString("d MMMM yyyy", Ui.TR), true),
                ("Müşteri", musteri.Text, false), ("Proje", $"{SeciliIl.Ad} · {(string)projeTuru.SelectedItem!} · {SonGirdi!.Bina}", false), ("", "", false),
            };
            foreach (DataGridViewRow r in kesif.Rows)
                satirlar.Add(($"{r.Cells[0].Value} {r.Cells[1].Value}".Trim(), $"{r.Cells[3].Value}", r.DefaultCellStyle.Font == Ui.Kalin));
            satirlar.Add(("", "", false)); satirlar.Add(("Gerekçe", gerekce.Text.Split('\n')[0], false));
            var belge = new PrintDocument { DocumentName = "EcoBuildss teklif" };
            belge.PrintPage += (_, e) =>
            {
                float y = e.MarginBounds.Top, x = e.MarginBounds.Left, w = e.MarginBounds.Width;
                using var f = new Font("Segoe UI", 9.5f); using var fk = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                foreach (var (a, b, k) in satirlar)
                {
                    var ff = k ? fk : f;
                    var boyut = e.Graphics!.MeasureString(a, ff, (int)(w * 0.62));
                    e.Graphics.DrawString(a, ff, Brushes.Black, new RectangleF(x, y, w * 0.62f, boyut.Height));
                    e.Graphics.DrawString(b, ff, Brushes.Black, new RectangleF(x + w * 0.64f, y, w * 0.36f, boyut.Height));
                    y += Math.Max(boyut.Height, ff.Height) + 4;
                }
            };
            using var o = new PrintPreviewDialog { Document = belge, Width = 900, Height = 1000 };
            o.ShowDialog(this);
        }
    }

    /// <summary>Paket kartı: ad, malzemeler, teklif tutarı, enerji, geri ödeme, kabul olasılığı.</summary>
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
            void Yaz(string t, Font f, Color c, float ek = 4) { var b = g.MeasureString(t, f, (int)w); using var br = new SolidBrush(c); g.DrawString(t, f, br, new RectangleF(x, y, w, b.Height)); y += b.Height + ek; }
            Yaz(Paket.Ad.ToUpper(Ui.TR), Ui.Kalin, Paket.Ad == "Önerilen" ? Ui.Yesil : Ui.Soluk, 2);
            Yaz(TeklifRaporu.PaketAdi(s), Ui.Yazi, Ui.Metin, 6);
            Yaz(Ui.Tl(Paket.TeklifTL), Ui.BuyukSayi, Ui.Koyu, 0);
            Yaz($"teklif, KDV hariç · {Ui.Tl(Paket.TeklifTLm2)}/m²", Ui.Kucuk, Ui.Soluk, 8);
            Yaz($"Yıllık ısıtma {Ui.Sayi(s.YillikIsitmaKWh)} kWh · {Ui.Tl(s.YillikGiderTL)}", Ui.Yazi, Ui.Metin, 2);
            Yaz($"Geri ödeme: {(Paket.GeriOdeme.HasValue ? Ui.Sayi(Paket.GeriOdeme.Value, "0.0") + " yıl" : "süre içinde dönmüyor")}", Ui.Yazi, Ui.Metin, 2);
            Yaz(s.Ts825Uygun ? "✓ TS 825 uygun" : "✗ TS 825 sınırını aşıyor", Ui.Kalin, s.Ts825Uygun ? Ui.Yesil : Ui.Kirmizi, 2);
            if (Paket.KabulOlasiligi is double p) Yaz($"Kabul olasılığı %{100 * p:0}", Ui.Kalin, p >= 0.5 ? Ui.Yesil : Ui.Kirmizi, 0);
        }
    }
}
