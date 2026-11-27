using EcoBuildss.Core.Uygulamalar;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Öğrenen modellerin sonuçları ve doğrulaması:
    ///  • Regresyon — enerji modelinin gerçek tüketimle kalibrasyonu
    ///  • Sınıflandırma — teklif kabul tahmini (aday modeller, test başarısı, kalite kapısı)
    ///  • Kümeleme — il iklim bölgeleri ve müşteri segmentleri
    /// </summary>
    public sealed class ModellerSekmesi : UserControl
    {
        readonly Durum d;
        // kalibrasyon
        readonly FlowLayoutPanel kalRozet = new() { Dock = DockStyle.Top, Height = 34 };
        readonly Label kalMetin = Ui.Aciklama("", 560);
        readonly DataGridView kalKatsayi = Ui.Izgara(), kalOlcu = Ui.Izgara();
        readonly SacilimGrafik kalGrafik = new() { XBaslik = "Tahmin (kayıt modele hiç gösterilmeden), kWh", YBaslik = "Gerçek tüketim, kWh" };
        // sınıflandırma
        readonly FlowLayoutPanel sinRozet = new() { Dock = DockStyle.Top, Height = 34 };
        readonly Label sinMetin = Ui.Aciklama("", 560);
        readonly DataGridView sinAday = Ui.Izgara(), sinTest = Ui.Izgara();
        readonly CubukGrafik sinOnem = new() { Dock = DockStyle.Fill, EtiketGenisligi = 260 };
        readonly ListBox sinKural = new() { Dock = DockStyle.Fill, Font = Ui.Kucuk, BorderStyle = BorderStyle.None, HorizontalScrollbar = true };
        // kümeleme
        readonly ComboBox kumeVeri = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 }, kumeK = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        readonly Label kumeMetin = Ui.Aciklama("", 900);
        readonly DataGridView kumeTablo = Ui.Izgara(), kumeSeg = Ui.Izgara();
        readonly SacilimGrafik kumeGrafik = new();
        readonly HaritaPanel harita = new() { Dock = DockStyle.Fill };

        public ModellerSekmesi(Durum durum)
        {
            d = durum;
            var sekme = new TabControl { Dock = DockStyle.Fill, Font = Ui.Yazi };
            sekme.TabPages.Add(Sayfa("Regresyon · enerji kalibrasyonu", KalibrasyonSayfasi()));
            sekme.TabPages.Add(Sayfa("Sınıflandırma · teklif kabul tahmini", SiniflandirmaSayfasi()));
            sekme.TabPages.Add(Sayfa("Kümeleme · bölge ve müşteri segmentleri", KumelemeSayfasi()));
            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(6, 2, 0, 0) };
            ust.Controls.Add(Ui.Dugme("Yenile", (_, __) => { Cursor = Cursors.WaitCursor; try { d.ModelleriKur(); } finally { Cursor = Cursors.Default; } }, true));
            Controls.Add(sekme); Controls.Add(ust);
            try { harita.Iller.AddRange(HaritaPanel.Oku(Path.Combine(d.Referans, "il_sinirlari.csv"))); } catch { }
            d.ModellerDegisti += Goster;
        }

        static TabPage Sayfa(string ad, Control c) { var t = new TabPage(ad) { BackColor = Ui.Zemin }; c.Dock = DockStyle.Fill; t.Controls.Add(c); return t; }

        // ======================= Regresyon =======================
        Control KalibrasyonSayfasi()
        {
            var iz = Ui.Izgara2(2, 45, 55);
            var sol = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            sol.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); sol.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            var p1 = new Panel(); p1.Controls.Add(kalKatsayi); p1.Controls.Add(kalMetin); kalMetin.Dock = DockStyle.Top; p1.Controls.Add(kalRozet);
            sol.Controls.Add(Ui.Kart("Katsayılar", p1), 0, 0);
            sol.Controls.Add(Ui.Kart("Doğrulama ölçüleri", kalOlcu), 0, 1);
            iz.Controls.Add(sol, 0, 0);
            iz.Controls.Add(Ui.Kart("Gerçek – tahmin", kalGrafik), 1, 0);
            return iz;
        }

        // ======================= Sınıflandırma =======================
        Control SiniflandirmaSayfasi()
        {
            var iz = Ui.Izgara2(2, 50, 50);
            iz.RowCount = 2; iz.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); iz.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            var p1 = new Panel(); p1.Controls.Add(sinAday); p1.Controls.Add(sinMetin); sinMetin.Dock = DockStyle.Top; p1.Controls.Add(sinRozet);
            iz.Controls.Add(Ui.Kart("Aday modeller (eğitim kümesinde 5 katlı tabakalı çapraz doğrulama)", p1), 0, 0);
            iz.Controls.Add(Ui.Kart("Test kümesi (seçimde hiç kullanılmadı)", sinTest), 1, 0);
            iz.Controls.Add(Ui.Kart("Değişken önemleri", sinOnem), 0, 1);
            iz.Controls.Add(Ui.Kart("Satış ekibi için kurallar (derinliği 3 karar ağacı)", sinKural), 1, 1);
            return iz;
        }

        // ======================= Kümeleme =======================
        Control KumelemeSayfasi()
        {
            kumeVeri.Items.AddRange(new object[] { "İller (MGM ısıtma ve soğutma derece-günü)", "Müşteriler (teklif geçmişi)" }); kumeVeri.SelectedIndex = 0;
            kumeK.Items.Add("k otomatik"); for (int k = 2; k <= 8; k++) kumeK.Items.Add($"k = {k}"); kumeK.SelectedIndex = 0;
            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36 };
            ust.Controls.Add(Ui.Etiket("Veri")); ust.Controls.Add(kumeVeri); ust.Controls.Add(Ui.Etiket("Küme sayısı")); ust.Controls.Add(kumeK);
            var iz = Ui.Izgara2(2, 42, 58);
            var sol = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            sol.RowStyles.Add(new RowStyle(SizeType.Percent, 42)); sol.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            sol.Controls.Add(Ui.Kart("k seçimi", kumeTablo), 0, 0);
            sol.Controls.Add(Ui.Kart("Kümeler", kumeSeg), 0, 1);
            iz.Controls.Add(sol, 0, 0);
            var sag = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            sag.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); sag.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            sag.Controls.Add(Ui.Kart("Saçılım", kumeGrafik), 0, 0);
            sag.Controls.Add(Ui.Kart("Harita (iller)", harita), 0, 1);
            iz.Controls.Add(sag, 1, 0);
            var p = new Panel(); p.Controls.Add(iz); p.Controls.Add(kumeMetin); kumeMetin.Dock = DockStyle.Top; p.Controls.Add(ust);
            kumeVeri.SelectedIndexChanged += (_, __) => Kumele(); kumeK.SelectedIndexChanged += (_, __) => Kumele();
            return p;
        }

        void Goster() { KalibrasyonGoster(); SiniflandirmaGoster(); Kumele(); }

        void KalibrasyonGoster()
        {
            kalRozet.Controls.Clear(); kalKatsayi.Columns.Clear(); kalKatsayi.Rows.Clear(); kalOlcu.Columns.Clear(); kalOlcu.Rows.Clear(); kalGrafik.Temizle();
            var k = d.Kalibrasyon;
            if (k == null)
            {
                kalRozet.Controls.Add(Ui.Rozet("Kalibrasyon yok", 2));
                kalMetin.Text = $"Veri sekmesinden en az {KalibrasyonSonucu.MinKayit} tüketim kaydı (fatura) yükleyin. Kalibrasyon olmadan teklifte TS 825 derece-gün modeli olduğu gibi kullanılır.";
                kalGrafik.Invalidate(); return;
            }
            kalRozet.Controls.Add(k.Kullanilabilir ? Ui.Rozet("Teklife uygulanıyor", 1) : Ui.Rozet("Teklife uygulanmıyor", 2));
            kalRozet.Controls.Add(k.Ashrae14Uygun ? Ui.Rozet("ASHRAE 14 sınırları içinde", 1) : Ui.Rozet("ASHRAE 14 sınırının üstünde", 2));
            kalMetin.Text = k.Yorum;
            foreach (var c in new[] { "Değişken", "Katsayı", "Std. hata", "%95 güven aralığı" }) kalKatsayi.Columns.Add(c, c);
            var r = k.Regresyon;
            for (int j = 0; j < r.Katsayi.Length; j++)
                kalKatsayi.Rows.Add(r.Adlar[j], Ui.Sayi(r.Katsayi[j], "N3"), Ui.Sayi(r.StdHata[j], "N3"), $"{Ui.Sayi(r.GA95Alt[j], "N3")} … {Ui.Sayi(r.GA95Ust[j], "N3")}");
            kalOlcu.Columns.Add("o", "Ölçü"); kalOlcu.Columns.Add("d", "Değer"); kalOlcu.Columns.Add("y", "Yorum"); kalOlcu.Columns[2].FillWeight = 170;
            kalOlcu.Rows.Add("Kayıt sayısı", Ui.Sayi(r.N), "");
            kalOlcu.Rows.Add("R² / düzeltilmiş R²", $"{Ui.Sayi(r.R2, "0.000")} / {Ui.Sayi(r.DuzeltilmisR2, "0.000")}", "Tüketimdeki değişkenliğin açıklanan payı");
            kalOlcu.Rows.Add("CV(RMSE) · kalibrasyonsuz", Ui.Yuzde(k.HamCvRmse), "TS 825 modeli + sıcak su baz yükü, görülmemiş kayıtlarda");
            kalOlcu.Rows.Add("CV(RMSE) · kalibre", Ui.Yuzde(k.CvRmse), "ASHRAE Guideline 14 aylık veri sınırı: ≤ %15");
            kalOlcu.Rows.Add("NMBE · kalibre", Ui.Yuzde(k.Nmbe, "0.00"), "Sistematik sapma; sınır ±%5");
            kalOlcu.Rows.Add("Isıtma katsayısı β₁", Ui.Sayi(k.IsitmaKatsayisi, "0.000"), "Gerçek ısıtma / model ısıtma (1'den küçük: kullanıcılar modelden az ısıtıyor)");
            kalOlcu.Rows.Add("Sıcak su + pişirme", Ui.Sayi(k.SicakSuKWhM2, "N1") + " kWh/m²·yıl", "Faturada ısıtma dışındaki baz yük");
            kalOlcu.Rows.Add("Etkili gözlem (Cook > 4/n)", Ui.Sayi(r.EtkiliGozlemler.Length), "İncelenmesi önerilen kayıtlar (kayıt silinmez)");
            kalGrafik.SeriAd = new[] { "Kayıtlar", "Etkili gözlem (Cook)", "Gerçek = tahmin" };
            double mx = 0;
            foreach (var p in k.Noktalar)
            {
                bool etkili = p.Cook > 4.0 / r.N;
                kalGrafik.Noktalar.Add(new SacilimGrafik.Nokta { X = p.CaprazTahmin, Y = p.GercekKWh, Seri = etkili ? 1 : 0, Vurgu = etkili, Etiket = etkili ? p.Id : null });
                mx = Math.Max(mx, Math.Max(p.CaprazTahmin, p.GercekKWh));
            }
            kalGrafik.Cizgiler.Add(new SacilimGrafik.Cizgi { Noktalar = new List<(double, double)> { (0, 0), (mx, mx) }, Seri = 2, Kesikli = true });
            kalGrafik.XMin = 0; kalGrafik.YMin = 0;
            kalGrafik.Invalidate();
        }

        void SiniflandirmaGoster()
        {
            sinRozet.Controls.Clear(); sinAday.Columns.Clear(); sinAday.Rows.Clear(); sinTest.Columns.Clear(); sinTest.Rows.Clear(); sinKural.Items.Clear(); sinOnem.Cubuklar.Clear();
            var m = d.Kabul;
            if (m == null || m.Model == null)
            {
                sinRozet.Controls.Add(Ui.Rozet("Model yok", 2));
                sinMetin.Text = m?.Yorum ?? $"Veri sekmesinden teklif geçmişi yükleyin (en az {KabulModeliSonucu.MinKayit} teklif, her sonuçtan en az {KabulModeliSonucu.MinSinif}).";
                sinOnem.Invalidate(); return;
            }
            sinRozet.Controls.Add(m.Guvenilir ? Ui.Rozet("Kalite kapısı geçildi · teklif ekranında açık", 1) : Ui.Rozet("Kalite kapısı geçilmedi · teklif ekranında kapalı", 3));
            sinMetin.Text = m.Yorum + $" Eğitim {m.EgitimN}, test {m.TestN} teklif; karar eşiği {Ui.Sayi(m.Esik, "0.00")} (eğitim kümesinde F1'i en yükselten).";
            foreach (var c in new[] { "Model", "F1 (ort. ± std)", "ROC-AUC", "Doğruluk" }) sinAday.Columns.Add(c, c);
            foreach (var a in m.Adaylar)
            {
                int i = sinAday.Rows.Add(a.Ad + (a.Ad == m.SecilenModel ? "  ✓ seçildi" : ""), $"{Ui.Sayi(a.F1Ort, "0.000")} ± {Ui.Sayi(a.F1Std, "0.000")}", Ui.Sayi(a.AucOrt, "0.000"), Ui.Sayi(a.DogrulukOrt, "0.000"));
                if (a.Ad == m.SecilenModel) sinAday.Rows[i].DefaultCellStyle.Font = Ui.Kalin;
            }
            var t = m.Test;
            sinTest.Columns.Add("o", "Ölçü"); sinTest.Columns.Add("d", "Değer"); sinTest.Columns.Add("e", "Eşik / karşılaştırma");
            void S(string a, double v, string e, bool? gecti = null) { int i = sinTest.Rows.Add(a, Ui.Sayi(v, "0.000"), e); if (gecti.HasValue) sinTest.Rows[i].Cells[1].Style.ForeColor = gecti.Value ? Ui.Yesil : Ui.Kirmizi; }
            S("F1 (kabul sınıfı)", t.F1, $"≥ {Ui.Sayi(KabulModeliSonucu.F1Esigi, "0.00")}", t.F1 >= KabulModeliSonucu.F1Esigi);
            S("ROC-AUC", t.Auc, $"≥ {Ui.Sayi(KabulModeliSonucu.AucEsigi, "0.00")}", t.Auc >= KabulModeliSonucu.AucEsigi);
            S("\"Hep kabul\" tahmininin F1'i", m.TabanF1, "model bunu belirgin geçmeli", t.F1 > m.TabanF1 + 0.05);
            S("Makro F1", t.MakroF1, "iki sınıfın ortalaması");
            S("Doğruluk", t.Dogruluk, "");
            S("Kesinlik (precision)", t.Kesinlik, "kabul dediklerinin doğru oranı");
            S("Duyarlılık (recall)", t.Duyarlilik, "gerçek kabullerin yakalanan oranı");
            sinTest.Rows.Add("Karmaşıklık matrisi", "", $"DP {t.DP} · YP {t.YP} · YN {t.YN} · DN {t.DN}");
            foreach (var (ad, o) in OzellikVektoru.Adlar.Zip(m.Onem, (a, b) => (a, b)).OrderByDescending(x => x.b).Take(8)) sinOnem.Cubuklar.Add((ad, 100 * o, 0));
            sinOnem.Bicim = v => "%" + v.ToString("0", Ui.TR);
            sinOnem.Invalidate();
            foreach (var k in m.Kurallar) sinKural.Items.Add(k);
        }

        void Kumele()
        {
            kumeTablo.Columns.Clear(); kumeTablo.Rows.Clear(); kumeSeg.Columns.Clear(); kumeSeg.Rows.Clear(); kumeGrafik.Temizle(); harita.Degerler.Clear(); harita.Lejant.Clear();
            int? k = kumeK.SelectedIndex <= 0 ? null : kumeK.SelectedIndex + 1;
            SegmentasyonSonucu s;
            try
            {
                if (kumeVeri.SelectedIndex == 0) s = Segmentasyon.Iller(d.V, k);
                else
                {
                    var (t, _) = d.Teklifler();
                    if (t.Count < 10) { kumeMetin.Text = "Teklif geçmişi yok."; kumeGrafik.Invalidate(); harita.Invalidate(); return; }
                    s = Segmentasyon.Musteriler(t, k is int x && x > 6 ? 6 : k);
                }
            }
            catch (Exception e) { kumeMetin.Text = "Kümeleme yapılamadı: " + e.Message; return; }
            kumeMetin.Text = $"{s.Ad}: {s.Yorum} Değişkenler z-skorla ölçeklendi; k-ortalamalar++ (30 başlangıç, sabit tohum).";
            foreach (var c in new[] { "k", "Davies–Bouldin", "Siluet" }) kumeTablo.Columns.Add(c, c);
            foreach (var (kk, (db, sil)) in s.KTablosu.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)))
            {
                int i = kumeTablo.Rows.Add(kk, Ui.Sayi(db, "0.000"), Ui.Sayi(sil, "0.000"));
                if (kk == s.K) kumeTablo.Rows[i].DefaultCellStyle.Font = Ui.Kalin;
            }
            kumeSeg.Columns.Add("n", "Küme"); kumeSeg.Columns.Add("o", "Özet"); kumeSeg.Columns[1].FillWeight = 420;
            foreach (var g in s.Segmentler) kumeSeg.Rows.Add(g.No, g.Ozet);
            kumeGrafik.XBaslik = s.Degiskenler[0]; kumeGrafik.YBaslik = s.Degiskenler[1];
            kumeGrafik.SeriAd = s.Segmentler.Select(g => $"Küme {g.No}").ToArray();
            foreach (var g in s.Segmentler) foreach (int i in g.Uyeler) kumeGrafik.Noktalar.Add(new SacilimGrafik.Nokta { X = s.HamVeri[i][0], Y = s.HamVeri[i][1], Seri = g.No - 1 });
            kumeGrafik.Invalidate();
            if (kumeVeri.SelectedIndex == 0)
            {
                foreach (var g in s.Segmentler)
                {
                    var renk = Cizim.Seri[(g.No - 1) % Cizim.Seri.Length];
                    harita.Lejant.Add((renk, $"Küme {g.No} ({g.Uyeler.Count} il)"));
                    foreach (int i in g.Uyeler) harita.Degerler[d.V.Iller[i].Ad] = (renk, $"Küme {g.No}");
                }
            }
            harita.Invalidate();
        }
    }
}
