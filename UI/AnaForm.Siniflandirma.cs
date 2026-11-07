using System.Drawing;
using System.Windows.Forms;
using EcoBuildss.Core;
using EcoBuildss.Depo;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Sınıflandırma sekmesi (karar ağacı): her il için karar motorunun ilin kendi iklim ve TS 825 verisiyle önerdiği paket
    /// etikettir; ağaç bu öneriyi ısıtma derece-günü, soğutma derece-günü ve TS 825 bölgesinden tahmin eden açık kurallar çıkarır.
    /// Derinlik birini dışarıda bırak çapraz doğrulamayla (LOOCV) seçilir. Firma sahada, model çalıştırmadan, bir yerin iklim
    /// verisiyle hangi paketi teklif edeceğini bu kurallardan okuyabilir.
    /// </summary>
    public sealed partial class AnaForm
    {
        SiniflandirmaSonucu? sinifSonuc, sinifTemel;
        string sinifImza = "";
        readonly NumericUpDown numDerinlik = new() { Minimum = 1, Maximum = 6, Value = 3, Width = 70 };
        bool derinlikAyarlaniyor;
        TabPage sSinif = null!;
        readonly AgacCizimi agacCizim = new();
        readonly Label lblDerinlikTablo = new() { AutoSize = true, Font = new Font("Consolas", 9f), ForeColor = Color.FromArgb(29, 35, 33), Margin = new Padding(0, 4, 0, 8) };
        readonly Label lblSinifOzet = new() { AutoSize = true, MaximumSize = new Size(390, 0), Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(29, 35, 33), Margin = new Padding(0, 0, 0, 8) };
        readonly Label lblSinifIl = new() { AutoSize = true, MaximumSize = new Size(390, 0), Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(29, 35, 33), BackColor = Color.FromArgb(236, 244, 239), Padding = new Padding(8, 6, 8, 6), Margin = new Padding(0, 0, 0, 8) };
        readonly DataGridView gridKarmasa = new();
        readonly Label lblHatalar = new() { Dock = DockStyle.Bottom, AutoSize = false, Height = 44, Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(93, 103, 99) };

        TabPage SiniflandirmaSekmesi()
        {
            var s = sSinif = new TabPage("Sınıflandırma") { BackColor = Zemin, Padding = new Padding(12) };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 448)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 62)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            p.Controls.Add(Alan("Ağaç derinliği", numDerinlik, "Agac"));
            p.Controls.Add(lblDerinlikTablo);
            p.Controls.Add(lblSinifOzet);
            p.Controls.Add(lblSinifIl);
            numDerinlik.ValueChanged += (_, _) =>
            {
                if (derinlikAyarlaniyor || sinifTemel == null) return;
                sinifSonuc = Siniflandirma.Derinlikle(sinifTemel, (int)numDerinlik.Value);
                SiniflandirmaGoster();
            };
            t.Controls.Add(Kart(p, "Paket tahmini (karar ağacı)"), 0, 0);
            agacCizim.SinifAdi = k => sinifSonuc != null && sinifSonuc.SinifAdi.TryGetValue(k, out var a) ? a.Replace(" · ", "\n") : k;
            agacCizim.SinifRengi = k => sinifSonuc?.Siniflar.IndexOf(k) ?? 0;
            agacCizim.EsikBicim = (j, e) => j == 2 ? e.ToString("0.#", Ortak.TR) : e.ToString("N0", Ortak.TR);
            t.Controls.Add(Kart(agacCizim, "Karar kuralları", 0, "Agac"), 1, 0);

            gridKarmasa.Dock = DockStyle.Fill; gridKarmasa.ReadOnly = true; gridKarmasa.AllowUserToAddRows = false; gridKarmasa.AllowUserToDeleteRows = false;
            gridKarmasa.AllowUserToResizeRows = false; gridKarmasa.RowHeadersVisible = false; gridKarmasa.SelectionMode = DataGridViewSelectionMode.CellSelect;
            gridKarmasa.BackgroundColor = Color.White; gridKarmasa.BorderStyle = BorderStyle.None; gridKarmasa.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridKarmasa.EnableHeadersVisualStyles = false;
            gridKarmasa.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            gridKarmasa.ColumnHeadersDefaultCellStyle.BackColor = Zemin;
            gridKarmasa.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            gridKarmasa.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize; gridKarmasa.ScrollBars = ScrollBars.Vertical;
            gridKarmasa.DefaultCellStyle.SelectionBackColor = Color.White; gridKarmasa.DefaultCellStyle.SelectionForeColor = Color.Black;
            var alt = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 12, 16, 12), Margin = new Padding(0, 0, 12, 12) };
            var bas = KartBaslik("Karmaşıklık matrisi (her il dışarıda bırakılarak sınandı): satır = karar motorunun önerisi, sütun = ağacın tahmini");
            bas.Dock = DockStyle.Top; bas.AutoSize = false; bas.Height = 28;
            alt.Controls.Add(gridKarmasa); alt.Controls.Add(lblHatalar); alt.Controls.Add(bas);
            t.Controls.Add(alt, 0, 1); t.SetColumnSpan(alt, 2);
            s.Controls.Add(t);
            sekmeler.SelectedIndexChanged += (_, _) => { if (sekmeler.SelectedTab == sSinif) SiniflandirmaGuncelle(); };
            return s;
        }

        /// <summary>Etiketler ekrandaki bina, pencere, doğrama, ısıtma ve öncelikle hesaplanır; bunlar değişmediyse yeniden hesaplanmaz.</summary>
        void SiniflandirmaGuncelle()
        {
            if (veri == null || sonGirdi == null) return;
            var g = sonGirdi;
            string imza = string.Join("|", g.Form.No, g.Form.En, g.Form.Boy, g.Form.Hacim, g.Form.YuzeyAlani, g.Yon, g.CamOrani, g.Yil, g.Dograma, g.Yakit, g.Kademe, g.Sogutma, g.Mevcut, g.EnerjiArtisi,
                                      g.MaliyetEnerji, g.MaliyetKarbon, g.EnerjiKarbon, g.GazTLkWh, g.GazCarpani, g.MalzemeCarpani, veri.Malzemeler.Sum(m => m.FiyatM2));
            try
            {
                if (imza != sinifImza || sinifTemel == null)
                {
                    sinifTemel = Siniflandirma.Yap(veri, g);
                    sinifImza = imza;
                    derinlikAyarlaniyor = true;
                    try { numDerinlik.Value = sinifTemel.SecilenDerinlik; } finally { derinlikAyarlaniyor = false; }
                    sinifSonuc = sinifTemel;
                }
                else if (sinifSonuc == null || sinifSonuc.SecilenDerinlik != (int)numDerinlik.Value)
                    sinifSonuc = Siniflandirma.Derinlikle(sinifTemel, (int)numDerinlik.Value);
            }
            catch (Exception ex) { Gunluk.Hata("Sınıflandırma yapılamadı", ex); sinifSonuc = null; }
            SiniflandirmaGoster();
        }

        void SiniflandirmaGoster()
        {
            var tr = Ortak.TR; var s = sinifSonuc;
            if (s == null) { lblSinifOzet.Text = "Sınıflandırma için bölgesi bilinen en az 3 il gerekir."; agacCizim.Agac = null; agacCizim.Invalidate(); return; }
            string Yuzde(double x) => "%" + (x * 100).ToString("0.0", tr);
            var kt = sinifTemel ?? s;
            var dler = kt.LoocvDogruluk.Keys.OrderBy(k => k).ToList();
            lblDerinlikTablo.Text =
                "Derinlik " + string.Join(" ", dler.Select(d => (d == kt.SecilenDerinlik ? $"[{d}]" : d.ToString()).PadLeft(6))) + "\n" +
                "LOOCV  ↑ " + string.Join(" ", dler.Select(d => Yuzde(kt.LoocvDogruluk[d]).PadLeft(6)));
            var onem = s.Agac.Onem.Select((o, j) => (o, j)).Where(x => x.o > 0.005).OrderByDescending(x => x.o)
                        .Select(x => $"{Siniflandirma.Ozellikler[x.j]} {Yuzde(x.o)}");
            lblSinifOzet.Text =
                $"{s.Iller.Count} il, {s.Siniflar.Count} paket sınıfı. Derinlik {s.SecilenDerinlik}" + (s.SecilenDerinlik == kt.SecilenDerinlik ? " (veriden seçildi: en yüksek LOOCV, eşitlikte en sığ)." : $" (veriden seçilen {kt.SecilenDerinlik}).") + "\n" +
                $"Doğruluk: her il dışarıda bırakılarak {Yuzde(s.Loocv)} · eğitim verisinde {Yuzde(s.EgitimDogruluk)}.\n" +
                $"Karşılaştırma: hep en sık paketi söylemek {Yuzde(s.TabanDogruluk)} doğru olurdu.\n" +
                $"Belirleyici özellik: {string.Join(", ", onem)}. {s.Agac.YaprakSayisi} kural.";

            // seçili il
            var secIl = cmbIl.SelectedItem as Il;
            int ix = secIl != null ? s.Iller.IndexOf(secIl) : -1;
            agacCizim.Yol.Clear(); agacCizim.Vurgu = null;
            if (ix >= 0)
            {
                var x = s.X[ix]; var d = s.Agac.Kok; agacCizim.Yol.Add(d);
                while (!d.Yaprak) { d = x[d.Ozellik] <= d.Esik ? d.Sol! : d.Sag!; agacCizim.Yol.Add(d); }
                agacCizim.Vurgu = d;
                var yol = s.Agac.YolOzet(x, agacCizim.EsikBicim);
                lblSinifIl.Text = $"{secIl!.Ad}: {string.Join(" ve ", yol)} → {s.SinifAdi[d.Sinif]}.\n" +
                                  (d.Sinif == s.Y[ix] ? "Karar motorunun önerisiyle aynı." : $"Karar motoru: {s.SinifAdi[s.Y[ix]]}.");
                lblSinifIl.Visible = true;
            }
            else lblSinifIl.Visible = false;
            agacCizim.Agac = s.Agac; agacCizim.Invalidate();

            // karmaşıklık matrisi
            var m = Siniflandirma.Karmasiklik(s);
            gridKarmasa.Columns.Clear(); gridKarmasa.Rows.Clear();
            gridKarmasa.Columns.Add(new DataGridViewTextBoxColumn { Name = "g", HeaderText = "Karar motoru ↓ / Ağaç →", FillWeight = 30, SortMode = DataGridViewColumnSortMode.NotSortable });
            foreach (var c in s.Siniflar)
                gridKarmasa.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = s.SinifAdi[c].Replace(" · ", "\n"), FillWeight = 16, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            gridKarmasa.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Duyarlılık (doğru / il)", FillWeight = 14, SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            for (int a = 0; a < s.Siniflar.Count; a++)
            {
                var hucre = new object[s.Siniflar.Count + 2];
                hucre[0] = s.SinifAdi[s.Siniflar[a]];
                int top = 0; for (int b = 0; b < s.Siniflar.Count; b++) { hucre[b + 1] = m[a, b]; top += m[a, b]; }
                hucre[hucre.Length - 1] = top == 0 ? "0 il" : $"{Yuzde((double)m[a, a] / top)} ({m[a, a]} / {top})";
                int r = gridKarmasa.Rows.Add(hucre);
                for (int b = 0; b < s.Siniflar.Count; b++)
                {
                    var cs = gridKarmasa.Rows[r].Cells[b + 1].Style;
                    if (a == b && m[a, b] > 0) { cs.BackColor = YesilAcik; cs.Font = FontTabloKalin; }
                    else if (m[a, b] > 0) { cs.BackColor = TuruncuAcik; cs.Font = FontTabloKalin; }
                }
            }
            gridKarmasa.ClearSelection();
            var hatali = Enumerable.Range(0, s.Y.Length).Where(i => s.LoocvTahmin[i] != s.Y[i])
                .Select(i => $"{s.Iller[i].Ad} (ısıtma {s.Iller[i].IsitmaDG.ToString("N0", tr)} DG, {s.Iller[i].Ts825Bolge}. bölge)").ToList();
            lblHatalar.Text = hatali.Count == 0 ? "Dışarıda bırakılan her il doğru tahmin edildi."
                : $"Yanlış tahmin edilen {hatali.Count} il: {string.Join("; ", hatali)}. Bu iller sınır değerlere yakındır; teklif her zaman karar motoruyla hesaplanır, ağaç hızlı ön tahmin ve açıklama içindir.";
        }
    }
}
