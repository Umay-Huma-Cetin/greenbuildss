using System.Drawing;
using System.Windows.Forms;
using EcoBuildss.Core;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Bölgesel analiz sekmesi (kümeleme): 81 il, MGM ısıtma ve soğutma derece-gününe göre k-ortalamalar ile kümelenir.
    /// Her küme için aynı bina, öncelik ve süreyle önerilen paket ve optimum yalıtım kalınlığı hesaplanır; böylece firma
    /// iş aldığı ilin hangi iklim grubuna girdiğini ve o grupta hangi standart paketi kullanacağını görür.
    /// </summary>
    public sealed partial class AnaForm
    {
        KumelemeSonucu? bolgeselSonuc, kTaramasi;
        readonly NumericUpDown numK = new() { Minimum = 2, Maximum = 8, Value = 6, Width = 70 };
        TabPage sBolgesel = null!;
        readonly SacilimGrafik grfKume = new() { XBaslik = "Yıllık ısıtma derece-gün (MGM)", YBaslik = "Yıllık soğutma derece-gün" };
        readonly Label lblKumeOzet = new() { AutoSize = true, MaximumSize = new Size(380, 0), Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(29, 35, 33), Margin = new Padding(0, 4, 0, 8) };
        readonly Label lblKTablo = new() { AutoSize = true, Font = new Font("Consolas", 9f), ForeColor = Color.FromArgb(29, 35, 33), Margin = new Padding(0, 4, 0, 2) };
        readonly DataGridView gridKume = new();

        TabPage BolgeselSekmesi()
        {
            var s = sBolgesel = new TabPage("Bölgesel analiz") { BackColor = Zemin, Padding = new Padding(12) };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 448)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            p.Controls.Add(Alan("Küme sayısı (k)", numK, "Kume"));
            p.Controls.Add(lblKTablo);
            p.Controls.Add(lblKumeOzet);
            numK.ValueChanged += (_, _) => { if (kAyarlaniyor) return; KumelemeYap(); if (hazir) Hesapla(); };
            t.Controls.Add(Kart(p, "İklim kümeleri (kümeleme)"), 0, 0);
            t.Controls.Add(Kart(grfKume, "81 il: ısıtma ve soğutma derece-günü", 0, "DG"), 1, 0);

            gridKume.Dock = DockStyle.Fill; gridKume.ReadOnly = true; gridKume.AllowUserToAddRows = false; gridKume.AllowUserToDeleteRows = false;
            gridKume.AllowUserToResizeRows = false; gridKume.RowHeadersVisible = false; gridKume.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridKume.BackgroundColor = Color.White; gridKume.BorderStyle = BorderStyle.None; gridKume.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridKume.EnableHeadersVisualStyles = false;
            gridKume.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            gridKume.ColumnHeadersDefaultCellStyle.BackColor = Zemin;
            gridKume.DefaultCellStyle.SelectionBackColor = YesilAcik; gridKume.DefaultCellStyle.SelectionForeColor = Color.Black;
            gridKume.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            gridKume.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            (string ad, string baslik, bool sayi, float w)[] kol =
            {
                ("kume", "Küme", false, 6), ("n", "İl", true, 3), ("hdd", "Isıtma DG", true, 6), ("cdd", "Soğutma DG", true, 6),
                ("bolge", "TS 825 bölgesi", false, 11), ("eps", "Opt. EPS", true, 6), ("ty", "Opt. taş yünü", true, 7),
                ("paket", "Önerilen paket ve sonucu", false, 38), ("iller", "İller", false, 24),
            };
            foreach (var k in kol)
            {
                var c = new DataGridViewTextBoxColumn { Name = k.ad, HeaderText = k.baslik, FillWeight = k.w, SortMode = DataGridViewColumnSortMode.NotSortable, MinimumWidth = 40 };
                if (k.sayi) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                gridKume.Columns.Add(c);
            }
            var alt = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 12, 16, 12), Margin = new Padding(0, 0, 12, 12) };
            var not = new Label
            {
                Dock = DockStyle.Bottom, AutoSize = false, Height = 40, ForeColor = Gri, Font = new Font("Segoe UI", 8.5f),
                Text = "Önerilen paket, yıllık ısıtma ve ömür boyu maliyet; teklif ekranındaki bina, pencere oranı, öncelik ve süreyle, küme merkezinin derece-günü kullanılarak hesaplanır. " +
                       "TS 825 bölgesi: kümede il merkezi bölgesi kaynakla belli olan illerin en sık bölgesi; böyle il yoksa paket hesaplanmaz. Ekonomik optimum kalınlık yönetmelikten bağımsızdır, her kümede verilir."
            };
            var bas = KartBaslik("Kümelere göre karar: standart paket ve optimum yalıtım"); bas.Dock = DockStyle.Top; bas.AutoSize = false; bas.Height = 28;
            alt.Controls.Add(gridKume); alt.Controls.Add(bas);
            t.Controls.Add(alt, 0, 1); t.SetColumnSpan(alt, 2);
            s.Controls.Add(t);
            sekmeler.SelectedIndexChanged += (_, _) => { if (sekmeler.SelectedTab == sBolgesel) BolgeselGuncelle(); };
            return s;
        }

        bool kAyarlaniyor;

        void KumelemeYap()
        {
            if (veri == null || veri.Iller.Count < 3) return;
            try
            {
                if (kTaramasi == null)
                {
                    kTaramasi = Kumeleme.Yap(veri.Iller);                      // k = 2…8 taraması (bir kez); k veriden seçilir
                    if (numK.Value != kTaramasi.K)
                    {
                        kAyarlaniyor = true;                                   // olay yeniden tetiklenmesin; küme aşağıda bir kez hesaplanır
                        try { numK.Value = kTaramasi.K; } finally { kAyarlaniyor = false; }
                    }
                }
                bolgeselSonuc = Kumeleme.Yap(veri.Iller, sabitK: (int)numK.Value);
            }
            catch { bolgeselSonuc = null; }
        }

        /// <summary>Küme tablosu ve grafiği (yalnızca sekme açıkken; her küme için karar motoru bir kez çalışır).</summary>
        void BolgeselGuncelle()
        {
            if (veri == null || sonGirdi == null) return;
            var tr = Ortak.TR;
            var ks = bolgeselSonuc;
            if (ks == null) { lblKumeOzet.Text = "İl iklim verisi (iller.csv) bulunamadı; kümeleme yapılamadı."; return; }
            var secIl = cmbIl.SelectedItem as Il;
            var secKume = secIl != null && ks.Kumeler.Any(k => k.Uyeler.Contains(secIl)) ? ks.KumesiNe(secIl) : null;

            var kt = kTaramasi ?? ks;
            var kler = kt.SiluetK.Keys.OrderBy(k => k).ToList();
            lblKTablo.Text =
                "k       " + string.Join(" ", kler.Select(k => (k == kt.K ? $"[{k}]" : k.ToString()).PadLeft(5))) + "\n" +
                "DB ↓    " + string.Join(" ", kler.Select(k => kt.DaviesBouldinK[k].ToString("0.00", tr).PadLeft(5))) + "\n" +
                "Siluet ↑" + string.Join(" ", kler.Select(k => kt.SiluetK[k].ToString("0.00", tr).PadLeft(5)));
            lblKumeOzet.Text = (ks.K == kt.K ? $"k = {ks.K}, veriden seçildi (en düşük Davies–Bouldin)." : $"k = {ks.K} (veriden seçilen k = {kt.K}).") +
                (secKume != null ? $"\n{secIl!.Ad} → {secKume.Ad}" : "");

            // Grafik
            grfKume.Temizle();
            foreach (var k in ks.Kumeler)
                foreach (var il in k.Uyeler)
                    grfKume.Noktalar.Add(new SacilimGrafik.Nokta { X = il.IsitmaDG, Y = il.SogutmaDG, Seri = (k.No - 1) % Cizim.Seri.Length, Vurgu = il == secIl, Etiket = il == secIl ? il.Ad : null });
            foreach (var k in ks.Kumeler)
                grfKume.Noktalar.Add(new SacilimGrafik.Nokta { X = k.OrtIsitma, Y = k.OrtSogutma, Seri = (k.No - 1) % Cizim.Seri.Length, Etiket = k.Ad });
            grfKume.SeriAd = ks.Kumeler.Select(k => k.Ad).ToArray(); grfKume.SeriRenk = ks.Kumeler.Select(k => (k.No - 1) % Cizim.Seri.Length).ToArray();
            grfKume.XBicim = v => v.ToString("N0", tr); grfKume.YBicim = v => v.ToString("N0", tr); grfKume.XMin = 0; grfKume.YMin = 0;
            grfKume.Invalidate();

            // Kümelere göre karar
            var modeller = FiyatModeli.Hepsi(veri);
            gridKume.Rows.Clear();
            foreach (var k in ks.Kumeler)
            {
                var bilinen = k.Uyeler.Where(i => i.Ts825Bolge != null).Select(i => i.Ts825Bolge!.Value).ToList();
                int? bolge = bilinen.Count > 0 ? bilinen.GroupBy(x => x).OrderByDescending(gr => gr.Count()).ThenBy(gr => gr.Key).First().Key : null;
                string bolgeMetni = bolge != null
                    ? $"{bolge}. bölge"
                    : "Bölgesi bilinen il yok";
                var g = sonGirdi.Kopya();
                g.Il = new Il { Ad = k.Ad, IsitmaDG = k.OrtIsitma, SogutmaDG = k.OrtSogutma };
                g.Bolge = bolge ?? sonGirdi.Bolge; g.SadeceTs825 = true;   // bölge yoksa yalnız bölgeden bağımsız ekonomik optimum gösterilir
                var r = KararMotoru.Hesapla(veri, g);
                var oneri = bolge != null ? r.Siralama.FirstOrDefault() : null;
                // Uygun seçenek yoksa ekonomik optimum, ömür boyu maliyeti en düşük birleşimin duvar ve camıyla hesaplanır
                var dayanak = oneri ?? r.Tum.OrderBy(x => x.ToplamMaliyet).First();
                KalinlikSonucu? Ks(string aile)
                {
                    var m = modeller.FirstOrDefault(x => x.Aile == aile);
                    return m == null ? null : Optimizasyon.OptimumKalinlik(veri, g, m, dayanak.Duvar, dayanak.Cam);
                }
                var kEps = Ks("EPS"); var kTy = Ks("Taş yünü");
                string Opt(KalinlikSonucu? x) => x == null ? "Fiyat modeli yok" : x.KisitsizCm.ToString("0.0", tr) + " cm";
                string yok = bolge == null ? "Hesaplanmadı: kümede TS 825 bölgesi kaynakla belli il yok" : "TS 825'e uygun seçenek yok";
                if (oneri == null && bolge != null)
                {
                    var neden = new List<string>();
                    if (kEps != null && kEps.AltSinirCm > kEps.Model.Gozlemler.Max(o => o.Kalinlik * 100) + 1e-9)
                        neden.Add($"duvara en az {(Math.Ceiling(kEps.AltSinirCm * 10) / 10).ToString("0.0", tr)} cm EPS gerekir");
                    if (r.CatiU > r.SinirCati) neden.Add($"çatı U {r.CatiU.ToString("0.00", tr)} > {r.SinirCati.ToString("0.00", tr)}");
                    if (r.ZeminU > r.SinirZemin) neden.Add($"zemin U {r.ZeminU.ToString("0.00", tr)} > {r.SinirZemin.ToString("0.00", tr)}");
                    if (neden.Count > 0) yok += ": " + string.Join("; ", neden);
                }
                int i = gridKume.Rows.Add(k.Ad, k.Uyeler.Count, k.OrtIsitma.ToString("N0", tr), k.OrtSogutma.ToString("N0", tr), bolgeMetni,
                    Opt(kEps), Opt(kTy),
                    oneri != null
                        ? $"{oneri.Yalitim.KisaAd} · {oneri.Cam.KisaAd} · {oneri.DuvarAdi}  ({(oneri.YillikIsitmaKWh / r.KullanimAlani).ToString("N0", tr)} kWh/m², ömür boyu {TL(oneri.ToplamMaliyet)})"
                        : yok,
                    string.Join(", ", k.Uyeler.Select(x => x.Ad)));
                if (k == secKume) { gridKume.Rows[i].DefaultCellStyle.BackColor = YesilAcik; gridKume.Rows[i].DefaultCellStyle.Font = FontTabloKalin; }
            }
            gridKume.ClearSelection();
        }
    }
}
