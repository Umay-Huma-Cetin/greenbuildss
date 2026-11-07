using System.Drawing;
using System.Windows.Forms;
using EcoBuildss.Core;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Analitik sekmeler: Gösterge paneli (KPI) ve Duyarlılık (what-if).
    /// Optimizasyon ve regresyon AnaForm.Optimizasyon.cs dosyasındadır.
    /// </summary>
    public sealed partial class AnaForm
    {
        // Gösterge paneli
        TabPage sGosterge = null!;
        readonly FlowLayoutPanel kpiAlani = new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 0, 0, 4) };
        readonly KpiKarti[] kpi = Enumerable.Range(0, 8).Select(_ => new KpiKarti()).ToArray();
        readonly CubukGrafik grfDagilim = new() { Dock = DockStyle.Fill, EtiketGenisligi = 170 };
        readonly CubukGrafik grfKarsilastir = new() { Dock = DockStyle.Fill, EtiketGenisligi = 210 };

        // Duyarlılık (what-if)
        TabPage? sDuyarlilik;
        Girdi? sonGirdi;
        readonly DilimGrafik grfGaz = new() { Dock = DockStyle.Top }, grfMalzeme = new() { Dock = DockStyle.Top }, grfSure = new() { Dock = DockStyle.Top }, grfArtis = new() { Dock = DockStyle.Top };
        readonly Label lblGaz = DuyAciklama(), lblMalzeme = DuyAciklama(), lblSure = DuyAciklama(), lblArtis = DuyAciklama(), lblBelirsizlik = DuyAciklama();
        readonly CubukGrafik grfBelirsizlik = new() { Dock = DockStyle.Fill, EtiketGenisligi = 260 };
        static Label DuyAciklama() => new() { AutoSize = true, Dock = DockStyle.Top, Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(29, 35, 33), Padding = new Padding(0, 8, 0, 0), MaximumSize = new Size(900, 0) };

        static Label Govde() => new() { AutoSize = true, MaximumSize = new Size(380, 0), Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(29, 35, 33), Margin = new Padding(0, 0, 0, 8) };
        static Label KartBaslik(string s) => new() { Text = s, AutoSize = true, Font = new Font("Segoe UI Semibold", 11f), ForeColor = Color.FromArgb(29, 35, 33), Margin = new Padding(0, 0, 0, 6) };

        static Panel Kart(Control ic, string? baslik = null, int yukseklik = 0, string? terim = null)
        {
            var p = new Panel { BackColor = Color.White, Padding = new Padding(16, 12, 16, 12), Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 12) };
            ic.Dock = DockStyle.Fill;
            p.Controls.Add(ic);
            if (baslik != null && terim != null)
            {
                var f = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 28, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0) };
                var b = KartBaslik(baslik); b.Margin = new Padding(0);
                var q = Soru(terim); q.Margin = new Padding(6, 3, 0, 0);
                f.Controls.Add(b); f.Controls.Add(q); p.Controls.Add(f);
            }
            else if (baslik != null) { var b = KartBaslik(baslik); b.Dock = DockStyle.Top; b.AutoSize = false; b.Height = 28; p.Controls.Add(b); }
            if (yukseklik > 0) p.Height = yukseklik;
            return p;
        }

        // ---------------- Sekme kurulumu ----------------
        void AnalizSekmeleriniKur()
        {
            var tr = Ortak.TR;

            // GÖSTERGE PANELİ
            var sG = sGosterge = new TabPage("Gösterge paneli") { BackColor = Zemin, Padding = new Padding(12) };
            foreach (var k in kpi) kpiAlani.Controls.Add(k);
            // kartlar satırı boşluksuz doldursun: 4 sütun, en az 200 piksel
            kpiAlani.SizeChanged += (_, _) =>
            {
                int w = Math.Max(200, (kpiAlani.ClientSize.Width - kpiAlani.Padding.Horizontal - 4 * 12) / 4);
                foreach (var k in kpi) if (k.Width != w) k.Width = w;
            };
            var gAlt = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            gAlt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); gAlt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            gAlt.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            gAlt.Controls.Add(Kart(grfDagilim, "Seçiminizin maliyet dağılımı"), 0, 0);
            gAlt.Controls.Add(Kart(grfKarsilastir, "Ömür boyu maliyet karşılaştırması (bugünkü değer)"), 1, 0);
            sG.Controls.Add(gAlt);
            sG.Controls.Add(kpiAlani);

            // DUYARLILIK (WHAT-IF)
            sDuyarlilik = new TabPage("Duyarlılık") { BackColor = Zemin, Padding = new Padding(12), AutoScroll = true };
            var dT = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
            dT.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dT.Controls.Add(BelirsizlikKarti());
            dT.Controls.Add(DuyKart("Enerji fiyatı değişirse", grfGaz, lblGaz));
            dT.Controls.Add(DuyKart("Enerji fiyatı enflasyondan hızlı artarsa (yıllık reel artış)", grfArtis, lblArtis));
            dT.Controls.Add(DuyKart("Malzeme fiyatları birlikte değişirse", grfMalzeme, lblMalzeme));
            dT.Controls.Add(DuyKart("Analiz süresi değişirse", grfSure, lblSure));
            sDuyarlilik.Controls.Add(dT);
            sekmeler.SelectedIndexChanged += (_, _) => { if (sekmeler.SelectedTab == sDuyarlilik) DuyarlilikGuncelle(); };

        }

        // ---------------- Güncelleme ----------------
        void AnalizleriGuncelle(Sonuc r, Sonuc rT, Secenek sec, Secenek? oneri, Girdi g)
        {
            var tr = Ortak.TR;
            double gazTL = r.GazTL;
            var liste = r.Siralama.Count > 0 ? r.Siralama : rT.Siralama;
            var enUcuz = liste.OrderBy(s => s.IlkYatirim).First();
            var enAzEnerji = liste.OrderBy(s => s.YillikIsitmaKWh).First();

            // --- KPI ---
            var k = Kpi.Hesapla(sec, r, enUcuz, r.EnerjiIskonto, g.Yil);
            var ko = oneri != null ? Kpi.Hesapla(oneri, r, enUcuz, r.EnerjiIskonto, g.Yil) : null;
            double refIsitma = EnerjiModeli.YillikIsitma(r.DG, r.HBaz) / r.KullanimAlani;
            string Fark(double a, double b, string birim) => Math.Abs(a - b) < 1e-9 ? "Önerilen çözümle aynı" : $"Öneri: {b.ToString(b < 100 ? "N1" : "N0", tr)} {birim}";
            int Daha(double a, double? b) => b == null ? 0 : a <= b + 1e-9 ? 1 : 2;
            Doldur(kpi[0], "İlk yatırım", TL(sec.IlkYatirim), oneri != null ? Fark(sec.IlkYatirim, oneri.IlkYatirim, "TL") : "", Daha(sec.IlkYatirim, oneri?.IlkYatirim));
            Doldur(kpi[1], "Kullanım alanı başına yatırım", $"{k.YatirimM2.ToString("N0", tr)} TL/m²", $"{r.KullanimAlani.ToString("N1", tr)} m² kullanım alanı · KDV hariç", 0);
            Doldur(kpi[2], "Yıllık ısıtma ihtiyacı", $"{k.IsitmaM2.ToString("N1", tr)} kWh/m²", $"{r.IlAdi}, {r.DG.ToString("N0", tr)} derece-gün · yalıtımsız {refIsitma.ToString("N0", tr)} kWh/m²", Daha(k.IsitmaM2, ko?.IsitmaM2));
            Doldur(kpi[3], r.Sogutma ? "Yıllık enerji gideri (ısıtma + soğutma)" : "Yıllık ısıtma gideri", TL(k.YillikEnerjiTL), $"{r.YakitAdi} · {TarifeAdi} · verim %{(r.IsitmaVerimi * 100).ToString("0", tr)}", 0);
            bool ekVar = k.EkYatirim && k.Tasarruf;
            Doldur(kpi[4], "Geri ödeme süresi (iskontolu)", !k.EkYatirim ? "Ek yatırım yok" : !k.Tasarruf ? "Tasarruf yok" : k.GeriOdemeVar && k.GeriOdemeYil <= g.Yil ? $"{k.GeriOdemeYil.ToString("N1", tr)} yıl" : $"> {g.Yil} yıl",
                   ekVar ? "En düşük yatırımlı seçeneğe göre ek yatırımın geri dönüşü" : "En düşük yatırımlı seçenek ya da ek tasarruf yok",
                   ekVar ? (k.GeriOdemeVar && k.GeriOdemeYil <= g.Yil ? 1 : 2) : 0);
            Doldur(kpi[5], "Isı kaybındaki azalma", $"%{(k.IsiKaybiAzalma * 100).ToString("0", tr)}", "Yalıtımsız referans kabuğa göre", 0);
            Doldur(kpi[6], $"Karbon yoğunluğu ({g.Yil} yıl)", $"{k.KarbonM2.ToString("N0", tr)} kg CO₂/m²", $"Malzemeden gelen pay %{(k.MalzemeKarbonPayi * 100).ToString("0", tr)}", Daha(sec.ToplamCO2Kg, oneri?.ToplamCO2Kg));
            Doldur(kpi[7], "TS 825 uyumu", sec.Ts825Uygun ? "Uygun" : "Uygun değil", $"Duvar U {sec.DuvarU.ToString("0.00", tr)} ≤ {r.SinirDuvar.ToString("0.00", tr)} · çatı {r.CatiU.ToString("0.00", tr)} · zemin {r.ZeminU.ToString("0.00", tr)}", sec.Ts825Uygun ? 1 : 2);

            grfDagilim.Cubuklar.Clear();
            grfDagilim.Cubuklar.Add(("Duvar malzemesi", k.YatirimDuvar, 2));
            grfDagilim.Cubuklar.Add(("Mantolama", k.YatirimYalitim, 0));
            grfDagilim.Cubuklar.Add(("Pencere camı", k.YatirimCam, 3));
            grfDagilim.Cubuklar.Add(("Çatı ve zemin yalıtımı", k.YatirimCatiZemin, 0));
            grfDagilim.Cubuklar.Add(($"Enerji gideri ({g.Yil} yıl, BD)", k.EnerjiToplamTL, 1));
            if (sec.YanginEkMaliyet > 0) grfDagilim.Cubuklar.Add(("Yangın şeridi (A1 farkı)", sec.YanginEkMaliyet, 2));
            grfDagilim.Bicim = v => Cizim.Kisa(v) + " TL";
            grfDagilim.Invalidate();
            grfKarsilastir.Cubuklar.Clear();
            grfKarsilastir.Cubuklar.Add(("Seçiminiz", sec.ToplamMaliyet, 2));
            if (oneri != null) grfKarsilastir.Cubuklar.Add(("Önerilen çözüm", oneri.ToplamMaliyet, 0));
            grfKarsilastir.Cubuklar.Add(("En düşük yatırımlı", enUcuz.ToplamMaliyet, 3));
            grfKarsilastir.Cubuklar.Add(("En az enerji tüketen", enAzEnerji.ToplamMaliyet, 1));
            grfKarsilastir.Bicim = v => Cizim.Kisa(v) + " TL";
            grfKarsilastir.Invalidate();

            sonGirdi = g;
            if (sekmeler.SelectedTab == sDuyarlilik) DuyarlilikGuncelle();
        }

        static void Doldur(KpiKarti k, string baslik, string deger, string alt, int durum)
        {
            k.Baslik = baslik; k.Deger = deger; k.Alt = alt; k.Durum = durum; k.Invalidate();
        }

        Control BelirsizlikKarti()
        {
            var t = new TableLayoutPanel { BackColor = Color.White, Padding = new Padding(16, 12, 16, 14), Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new Padding(0, 0, 0, 12) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var bas = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            var b = KartBaslik("Fiyat belirsizliği: öneri ne kadar sağlam?"); b.Margin = new Padding(0);
            var q = Soru("MonteCarlo"); q.Margin = new Padding(6, 3, 0, 0);
            bas.Controls.Add(b); bas.Controls.Add(q);
            grfBelirsizlik.Margin = new Padding(0);
            lblBelirsizlik.Dock = DockStyle.None; lblBelirsizlik.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            t.Controls.Add(bas, 0, 0); t.Controls.Add(grfBelirsizlik, 0, 1); t.Controls.Add(lblBelirsizlik, 0, 2);
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.Absolute, 120)); t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Resize += (_, _) => lblBelirsizlik.MaximumSize = new Size(Math.Max(200, t.ClientSize.Width - 36), 0);
            return t;
        }

        static Control DuyKart(string baslik, DilimGrafik grf, Label aciklama)
        {
            var t = new TableLayoutPanel { BackColor = Color.White, Padding = new Padding(16, 12, 16, 14), Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new Padding(0, 0, 0, 12) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var b = KartBaslik(baslik);
            grf.Dock = DockStyle.Fill; grf.Margin = new Padding(0);
            aciklama.Dock = DockStyle.None; aciklama.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            t.Controls.Add(b, 0, 0); t.Controls.Add(grf, 0, 1); t.Controls.Add(aciklama, 0, 2);
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.Absolute, 96)); t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Resize += (_, _) => aciklama.MaximumSize = new Size(Math.Max(200, t.ClientSize.Width - 36), 0);
            return t;
        }

        /// <summary>Üç duyarlılık analizini çalıştırır (yalnızca sekme açıkken; her biri onlarca kez yeniden hesaplar).</summary>
        void DuyarlilikGuncelle()
        {
            if (veri == null || sonGirdi == null) return;
            var tr = Ortak.TR;
            var g = sonGirdi;
            string Yuzde(double v) => v == 0 ? "%0" : (v > 0 ? "+%" : "−%") + Math.Abs(v).ToString("0", tr);
            string Ad(Secenek s) => $"{s.Yalitim.KisaAd} · {s.Cam.KisaAd} · {s.DuvarAdi}";

            void Ciz(DilimGrafik grf, Label lbl, List<Dilim> dilimler, double mevcut, Func<double, string> bicim, string ne, string birimAciklama)
            {
                // renkler: şu anki öneri yeşil, diğerleri sırayla
                var mevcutDilim = dilimler.FirstOrDefault(d => mevcut >= d.Bas && mevcut <= d.Son) ?? dilimler.OrderBy(d => Math.Min(Math.Abs(d.Bas - mevcut), Math.Abs(d.Son - mevcut))).First();
                var anahtar = new List<string> { Ad(mevcutDilim.Oneri) };
                foreach (var d in dilimler) if (!anahtar.Contains(Ad(d.Oneri))) anahtar.Add(Ad(d.Oneri));
                grf.Dilimler.Clear();
                foreach (var d in dilimler) { int i = anahtar.IndexOf(Ad(d.Oneri)); grf.Dilimler.Add((d.Bas, d.Son, i, (i + 1).ToString(tr))); }
                grf.Bas = dilimler[0].Bas; grf.Son = dilimler[dilimler.Count - 1].Son; grf.Mevcut = mevcut; grf.Bicim = bicim;
                grf.Invalidate();

                int ix = dilimler.IndexOf(mevcutDilim);
                var sb = new System.Text.StringBuilder();
                if (dilimler.Count == 1)
                    sb.Append($"Önerilen paket incelenen aralığın tamamında ({bicim(grf.Bas)} … {bicim(grf.Son)}) değişmiyor: öneri bu girdiye karşı kararlı.");
                else
                {
                    sb.Append($"Şu anki öneri, {ne} {bicim(mevcutDilim.Bas)} ile {bicim(mevcutDilim.Son)} arasında kaldıkça değişmez.");
                    if (ix + 1 < dilimler.Count) sb.Append($" {bicim(dilimler[ix + 1].Bas)} ve üstünde öneri {Ad(dilimler[ix + 1].Oneri)} olur.");
                    if (ix > 0) sb.Append($" {bicim(dilimler[ix - 1].Son)} ve altında öneri {Ad(dilimler[ix - 1].Oneri)} olur.");
                }
                sb.Append("\n");
                for (int i = 0; i < anahtar.Count; i++) sb.Append($"\n{i + 1}  {anahtar[i]}{(i == 0 ? "  (şu anki öneri)" : "")}");
                lbl.Text = sb.ToString();
            }

            // Fiyat belirsizliği (Monte Carlo): kaynakların verdiği fiyat aralıkları içinde 1.000 çekiliş
            var mc = Belirsizlik.Calistir(veri, g, 1000, 42);
            grfBelirsizlik.Cubuklar.Clear();
            int ci = 0;
            foreach (var (s, oran) in mc.Birinciler.Take(5)) grfBelirsizlik.Cubuklar.Add((Ad(s), oran * 100, s.Kod == mc.OneriKod ? 0 : 1 + (ci++ % 3)));
            grfBelirsizlik.Bicim = v => "%" + v.ToString("0", tr);
            grfBelirsizlik.Invalidate();
            lblBelirsizlik.Text = mc.Birinciler.Count == 0 ? "Hesaplanacak seçenek yok." :
                $"Fiyatı aralık olarak yayımlanan {mc.AraliklıMalzeme} malzemenin fiyatı, aralığı içinde {mc.Deneme.ToString("N0", tr)} kez rastgele çekildi ve her seferinde karar yeniden verildi. " +
                $"Şu anki öneri denemelerin %{(mc.OneriBirinciOrani * 100).ToString("0", tr)}'{(mc.OneriBirinciOrani >= 0.5 ? "ında" : "ında")} birinci çıktı" +
                (mc.OneriBirinciOrani >= 0.9 ? ": fiyat belirsizliğine karşı sağlam." : mc.OneriBirinciOrani >= 0.5 ? ": çoğunlukla birinci, ama fiyatlar aralığın ucunda olursa sıra değişebilir." : ": fiyatlara duyarlı; satıcıdan kesin fiyat alınmadan karar verilmemeli.") +
                $" Önerinin ömür boyu maliyeti %90 olasılıkla {TL(mc.LccP5)} ile {TL(mc.LccP95)} arasında.";

            // Enerji fiyatı: seçili tarifeye göre yüzde değişim; doğalgazda diğer tarife kademesi şerit üzerinde işaretlenir
            double e1 = veri.P("E1"), e2 = veri.P("E2");
            bool k2 = cmbTarife.SelectedIndex == 1;
            grfGaz.Isaretler.Clear();
            if (g.Yakit == "DG") grfGaz.Isaretler.Add((k2 ? (e1 / e2 - 1) * 100 : (e2 / e1 - 1) * 100, k2 ? "Kademe-1 tarifesi" : "Kademe-2 tarifesi"));
            Ciz(grfGaz, lblGaz, Duyarlilik.GazFiyati(veri, g), 0, Yuzde, "enerji fiyatı",
                g.Yakit == "DG" ? $"Başlangıç: {TarifeAdi} ({(k2 ? e2 : e1).ToString("N3", tr)} TL/Sm³, BOTAŞ). Kesikli çizgi, BOTAŞ'ın diğer kademe fiyatının ({(k2 ? e1 : e2).ToString("N3", tr)} TL/Sm³) bu ölçekteki yeridir."
                                : $"Başlangıç: {sonTeklif?.r.YakitAdi} ve elektrik fiyatı. Isıtma yakıtı ve (varsa) soğutma elektriği aynı oranda değiştirilir.");
            Ciz(grfArtis, lblArtis, Duyarlilik.EnerjiArtisi(veri, g), g.EnerjiArtisi * 100, v => (v >= 0 ? "%" : "−%") + Math.Abs(v).ToString("0.#", tr), "yıllık reel enerji fiyatı artışı",
                "0 = enerji fiyatı enflasyonla aynı hızda artar. Geçmişte enerji fiyatları enflasyondan farklı hızda değişmiştir; bu grafik, öneriyi değiştiren artış hızını gösterir.");
            Ciz(grfMalzeme, lblMalzeme, Duyarlilik.MalzemeFiyati(veri, g), 0, Yuzde, "tüm malzeme fiyatları",
                "Mantolama, cam, duvar, çatı ve zemin fiyatları aynı oranda değiştirilir (2026 fiyatlarına göre).");
            Ciz(grfSure, lblSure, Duyarlilik.AnalizSuresi(veri, g), g.Yil, v => v.ToString("0", tr) + " yıl", "analiz süresi",
                "Analiz süresi uzadıkça ısıtma giderinin (bugünkü değer) ve işletme karbonunun ağırlığı artar.");
        }
    }
}
