using System.Drawing;
using System.Windows.Forms;
using EcoBuildss.Core;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Optimizasyon sekmesi. Üç parça birbirini besler:
    ///  1) Fiyat–kalınlık regresyonu: piyasada yalnızca 5/8/10 cm fiyatı var; aradaki kalınlıkların maliyeti regresyon doğrusundan alınır.
    ///  2) Optimum yalıtım kalınlığı: ömür boyu maliyeti en küçükleyen kalınlık; kısıt TS 825 duvar U sınırı.
    ///  3) Bütçe kısıtlı seçim: bütçeyi aşmayan, TS 825'e uyan seçenekler içinde amaca göre en iyisi ve Pareto cephesi.
    /// </summary>
    public sealed partial class AnaForm
    {
        readonly ComboBox cmbAile = Combo();
        readonly Label lblOptSonuc = OptGovde(), lblRegresyon = OptGovde();
        readonly Button btnOptUygula = YesilDugme("Bu yalıtımı teklifte kullan");
        readonly SacilimGrafik grfLcc = new() { XBaslik = "Yalıtım kalınlığı (cm)", YBaslik = "Ömür boyu maliyet (TL)" };
        readonly SacilimGrafik grfRegresyon = new() { XBaslik = "Kalınlık (cm)", YBaslik = "Uygulanmış fiyat (TL/m²)", XMin = 0 };

        readonly NumericUpDown numButce = new() { Minimum = 0, Maximum = 100_000_000, Increment = 10_000, ThousandsSeparator = true, Width = 140 };
        readonly ComboBox cmbAmac = Combo();
        readonly Label lblButceSonuc = OptGovde();
        readonly Button btnButceUygula = YesilDugme("Bu seçeneği teklifte kullan");
        readonly SacilimGrafik grfPareto = new() { XBaslik = "İlk yatırım (TL, KDV hariç)" };

        bool butceSifirla = true, optYukleniyor;
        (Sonuc r, Secenek sec, Girdi g)? optSon;
        Malzeme? optUrun;
        Secenek? butceSecim;

        static Label OptGovde() => new() { AutoSize = true, MaximumSize = new Size(380, 0), Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(29, 35, 33), Margin = new Padding(0, 4, 0, 8) };
        static Button YesilDugme(string t)
        {
            var b = new Button { Text = t, AutoSize = true, BackColor = Color.FromArgb(31, 111, 74), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 4, 0, 0), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        TabPage OptimizasyonSekmesi()
        {
            var s = new TabPage("Optimizasyon") { BackColor = Zemin, Padding = new Padding(12) };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 448)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 54)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 46));

            // 1) Optimum kalınlık
            var p1 = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            p1.Controls.Add(Alan("Yalıtım ailesi", cmbAile, "Optimum"));
            p1.Controls.Add(lblOptSonuc);
            p1.Controls.Add(btnOptUygula);
            p1.Controls.Add(lblRegresyon);
            t.Controls.Add(Kart(p1, "Optimum yalıtım kalınlığı"), 0, 0);

            var ust = new TableLayoutPanel { ColumnCount = 2, Margin = new Padding(0) };
            ust.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); ust.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            ust.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            ust.Controls.Add(Kart(grfLcc, "Ömür boyu maliyet – yalıtım kalınlığı", 0, "Optimum"), 0, 0);
            ust.Controls.Add(Kart(grfRegresyon, "Fiyat–kalınlık regresyonu", 0, "Regresyon"), 1, 0);
            ust.Dock = DockStyle.Fill;
            t.Controls.Add(ust, 1, 0);

            // 2) Bütçe
            var p2 = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            var satir = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            satir.Controls.Add(Alan("Bütçe (TL, KDV hariç)", numButce, "Butce"));
            var amacAlan = Alan("Amaç", cmbAmac); cmbAmac.Width = 220; amacAlan.Margin = new Padding(12, 0, 0, 8);
            satir.Controls.Add(amacAlan);
            p2.Controls.Add(satir);
            p2.Controls.Add(lblButceSonuc);
            p2.Controls.Add(btnButceUygula);
            t.Controls.Add(Kart(p2, "Bütçe kısıtlı seçim"), 0, 1);
            t.Controls.Add(Kart(grfPareto, "Etkin seçenekler (Pareto cephesi) ve bütçe", 0, "Butce"), 1, 1);
            s.Controls.Add(t);

            cmbAmac.Items.AddRange(new object[] { new Secim("Yıllık ısıtma en az", (int)ButceAmaci.Isitma), new Secim("Toplam CO₂ en az", (int)ButceAmaci.Karbon), new Secim("Ömür boyu maliyet en az", (int)ButceAmaci.OmurBoyuMaliyet) });
            cmbAmac.SelectedIndex = 0;
            cmbAile.SelectedIndexChanged += (_, _) => { if (!optYukleniyor) OptimizasyonYenidenCiz(); };
            cmbAmac.SelectedIndexChanged += (_, _) => { if (!optYukleniyor) OptimizasyonYenidenCiz(); };
            numButce.ValueChanged += (_, _) => { if (!optYukleniyor) OptimizasyonYenidenCiz(); };
            btnOptUygula.Click += (_, _) =>
            {
                if (optUrun == null) return;
                secY = optUrun; secP ??= optSon?.sec.Cam; secD ??= optSon?.sec.Duvar;
                sekmeler.SelectedIndex = 0; Hesapla();
            };
            btnButceUygula.Click += (_, _) =>
            {
                if (butceSecim == null) return;
                secY = butceSecim.Yalitim; secP = butceSecim.Cam; secD = butceSecim.Duvar;
                sekmeler.SelectedIndex = 0; Hesapla();
            };
            return s;
        }

        /// <summary>Student t dağılımı, iki yönlü %95 kritik değeri (serbestlik derecesi 1–30; üstü için 1,96).</summary>
        static double TKritik(int sd) => sd < 1 ? double.NaN : sd > 30 ? 1.96 : new[] { 12.706, 4.303, 3.182, 2.776, 2.571, 2.447, 2.365, 2.306, 2.262, 2.228, 2.201, 2.179, 2.160, 2.145, 2.131,
            2.120, 2.110, 2.101, 2.093, 2.086, 2.080, 2.074, 2.069, 2.064, 2.060, 2.056, 2.052, 2.048, 2.045, 2.042 }[sd - 1];

        static Label Aciklama(string t) => new() { Text = t, AutoSize = true, MaximumSize = new Size(380, 0), Font = new Font("Segoe UI", 9f), ForeColor = Gri, Margin = new Padding(0, 0, 0, 8) };

        void OptimizasyonGuncelle(Sonuc r, Sonuc rT, Secenek sec, Secenek oneri, Girdi g)
        {
            optSon = (rT, sec, g);
            optYukleniyor = true;
            if (cmbAile.Items.Count == 0)
            {
                foreach (var m in FiyatModeli.Hepsi(veri!)) cmbAile.Items.Add(m.Aile);
                if (cmbAile.Items.Count > 0) cmbAile.SelectedIndex = 0;
            }
            // Ailesi teklifteki yalıtımla aynı olsun (kullanıcı değiştirebilir)
            int ix = cmbAile.Items.IndexOf(sec.Yalitim.Aile);
            if (ix >= 0 && butceSifirla) cmbAile.SelectedIndex = ix;
            if (butceSifirla)
            {
                // Varsayılan bütçe: önerilen paketin ilk yatırımı, 10.000 TL'ye yukarı yuvarlanmış
                decimal b = (decimal)(Math.Ceiling(oneri.IlkYatirim / 10_000) * 10_000);
                numButce.Value = Math.Min(numButce.Maximum, b);
                butceSifirla = false;
            }
            optYukleniyor = false;
            OptimizasyonYenidenCiz();
        }

        void OptimizasyonYenidenCiz()
        {
            if (veri == null || optSon == null) return;
            var (r, sec, g) = optSon.Value;
            var tr = Ortak.TR;
            var modeller = FiyatModeli.Hepsi(veri);

            // ---------- Regresyon ----------
            grfRegresyon.Temizle();
            var adlar = new List<string>(); var renk = new List<int>();
            for (int i = 0; i < modeller.Count; i++)
            {
                var m = modeller[i];
                foreach (var o in m.Gozlemler) grfRegresyon.Noktalar.Add(new SacilimGrafik.Nokta { X = o.Kalinlik * 100, Y = o.FiyatM2, Seri = i });
                grfRegresyon.Cizgiler.Add(new SacilimGrafik.Cizgi { Seri = i, Kesikli = true, Kalinlik = 1.6f, Noktalar = { (2, m.Fiyat(2)), (16, m.Fiyat(16)) } });
                adlar.Add(m.Aile); renk.Add(i);
                if (m.LevhaEgimiKullaniliyor)
                {
                    int sl = modeller.Count + i;
                    foreach (var l in m.Levhalar) grfRegresyon.Noktalar.Add(new SacilimGrafik.Nokta { X = l.KalinlikCm, Y = l.FiyatM2, Seri = sl });
                    grfRegresyon.Cizgiler.Add(new SacilimGrafik.Cizgi { Seri = sl, Kesikli = true, Kalinlik = 1.4f, Noktalar = { (2, m.LevhaSabit + m.LevhaEgim * 2), (16, m.LevhaSabit + m.LevhaEgim * 16) } });
                    adlar.Add(m.Aile == "Taş yünü" ? "TY levha" : m.Aile + " levha"); renk.Add(sl);
                }
            }
            grfRegresyon.SeriAd = adlar.ToArray(); grfRegresyon.SeriRenk = renk.ToArray();
            grfRegresyon.YBaslik = "Fiyat (TL/m²)";
            grfRegresyon.XBicim = v => v.ToString("0", tr); grfRegresyon.YBicim = v => v.ToString("N0", tr);
            grfRegresyon.Invalidate();
            var rb = new System.Text.StringBuilder("Regresyon: her ek cm'nin maliyeti\n");
            foreach (var m in modeller)
                rb.Append(m.LevhaEgimiKullaniliyor
                    ? $"• {m.Aile}: {m.LevhaEgim.ToString("N1", tr)} TL/m² (R² {m.LevhaR2.ToString("0.000", tr)}, {m.Levhalar.Count} fiyat)\n"
                    : $"• {m.Aile}: {m.EgimCm.ToString("N1", tr)} TL/m² (R² {m.R2.ToString("0.000", tr)}, {m.Gozlemler.Count} fiyat)\n");
            lblRegresyon.Text = rb.ToString().TrimEnd();

            // ---------- Optimum kalınlık ----------
            var model = modeller.FirstOrDefault(m => m.Aile == cmbAile.SelectedItem as string) ?? modeller.FirstOrDefault();
            if (model != null)
            {
                var k = Optimizasyon.OptimumKalinlik(veri, g, model, sec.Duvar, sec.Cam);
                optUrun = k.EnIyiUrun;
                string Cm(double v) => v.ToString("0.0", tr) + " cm";
                // Alt sınır yukarı yuvarlanır: 8,45 cm "8,4" yazılırsa okuyan sınırı sağlamayan bir kalınlık görür
                string CmYukari(double v) => (Math.Ceiling(v * 10 - 1e-9) / 10).ToString("0.0", tr) + " cm";
                var sb = new System.Text.StringBuilder();
                sb.Append($"• Ekonomik optimum: {Cm(k.KisitsizCm)}\n");
                sb.Append($"• TS 825 alt sınırı: {CmYukari(k.AltSinirCm)}\n");
                sb.Append(k.KisitBaglayici
                    ? $"• Sonuç: yönetmelik belirleyici, en az {CmYukari(k.OptimumCm)}\n"
                    : $"• Sonuç: ekonomik optimum sınırın üstünde, {Cm(k.OptimumCm)}\n");
                if (k.EnIyiUrun != null)
                {
                    sb.Append($"• En uygun ürün: {k.EnIyiUrun.KisaAd}");
                    var mevcut = r.Tum.FirstOrDefault(x => x.Yalitim == k.EnIyiUrun && x.Cam == sec.Cam && x.Duvar == sec.Duvar);
                    if (sec.Yalitim == k.EnIyiUrun) sb.Append(" (teklifte zaten bu)");
                    else if (mevcut != null && sec.DuvarU <= r.SinirDuvar + 1e-12 && sec.ToplamMaliyet > mevcut.ToplamMaliyet)
                        sb.Append($"\n  Teklife alınırsa ömür boyu maliyet {TL(sec.ToplamMaliyet - mevcut.ToplamMaliyet)} azalır.");
                }
                else sb.Append($"• {model.Aile} ailesinde bu duvarla TS 825'i sağlayan satılık kalınlık yok.");
                lblOptSonuc.Text = sb.ToString();
                btnOptUygula.Visible = k.EnIyiUrun != null && k.EnIyiUrun != sec.Yalitim;
                btnOptUygula.Text = k.EnIyiUrun != null ? $"{k.EnIyiUrun.KisaAd} yalıtımını teklifte kullan" : "";

                grfLcc.Temizle();
                grfLcc.Cizgiler.Add(new SacilimGrafik.Cizgi { Seri = 0, Noktalar = k.Egri.Select(e => (e.cm, e.lcc)).ToList() });
                // satılan kalınlıklar (gerçek fiyatlarıyla)
                foreach (var o in model.Gozlemler)
                {
                    var x = r.Tum.FirstOrDefault(s => s.Yalitim == o && s.Cam == sec.Cam && s.Duvar == sec.Duvar);
                    if (x != null) grfLcc.Noktalar.Add(new SacilimGrafik.Nokta { X = o.Kalinlik * 100, Y = x.ToplamMaliyet, Seri = x.Ts825Uygun ? 0 : 1, Vurgu = o == k.EnIyiUrun, Etiket = o == k.EnIyiUrun ? "en uygun ürün" : null });
                }
                grfLcc.Dikeyler.Add((k.KisitsizCm, $"ekonomik optimum {Cm(k.KisitsizCm)}", 2));
                grfLcc.Dikeyler.Add((k.AltSinirCm, $"TS 825 alt sınırı {CmYukari(k.AltSinirCm)}", 1));
                grfLcc.SeriAd = new[] { "Ömür boyu maliyet (model)", "TS 825'i sağlamayan ürün" }; grfLcc.SeriRenk = new[] { 0, 1 };
                grfLcc.XBicim = v => v.ToString("0", tr); grfLcc.YBicim = Cizim.Kisa;
                grfLcc.Invalidate();
            }

            // ---------- Bütçe ----------
            var amac = (ButceAmaci)(int)((Secim)cmbAmac.SelectedItem!).Deger;
            var f = Optimizasyon.AmacFonksiyonu(amac);
            double butce = (double)numButce.Value;
            var bs = Optimizasyon.Butce(r.Tum, butce, amac);
            butceSecim = bs.Secim;
            var uygun = r.Tum.Where(x => x.Ts825Uygun).ToList();
            (string ad, Func<double, string> bicim) olcu = amac switch
            {
                ButceAmaci.Karbon => ($"{g.Yil} yıllık CO₂", v => (v / 1000).ToString("N1", tr) + " t"),
                ButceAmaci.OmurBoyuMaliyet => ("ömür boyu maliyet", v => TL(v)),
                _ => ("yıllık ısıtma", v => v.ToString("N0", tr) + " kWh"),
            };
            string Ad(Secenek x) => $"{x.Yalitim.KisaAd} · {x.Cam.KisaAd} · {x.DuvarAdi}";
            if (uygun.Count == 0)
                lblButceSonuc.Text = "TS 825'e uygun seçenek yok; malzeme listesine daha kalın yalıtım ekleyin.";
            else if (bs.Secim == null)
                lblButceSonuc.Text = $"Bu bütçeyle TS 825'e uyan seçenek yok.\nEn düşük uygun ilk yatırım: {TL(uygun.Min(x => x.IlkYatirim))} ({Ad(uygun.OrderBy(x => x.IlkYatirim).First())}).";
            else
            {
                var x = bs.Secim;
                var sb = new System.Text.StringBuilder();
                sb.Append($"En iyi seçenek: {Ad(x)}\n");
                sb.Append($"İlk yatırım {TL(x.IlkYatirim)} · {olcu.ad} {olcu.bicim(f(x))}");
                if (bs.SonrakiAdim != null)
                {
                    var n = bs.SonrakiAdim;
                    double ekPara = n.IlkYatirim - butce, kazanc = f(x) - f(n);
                    sb.Append($"\n\n+{TL(ekPara)} ile: {Ad(n)} ({olcu.bicim(kazanc)} daha az)");
                }
                lblButceSonuc.Text = sb.ToString();
            }
            btnButceUygula.Visible = bs.Secim != null && !Ayni(bs.Secim, sec);

            grfPareto.Temizle();
            foreach (var x in uygun.Where(x => !bs.Pareto.Contains(x)))
                grfPareto.Noktalar.Add(new SacilimGrafik.Nokta { X = x.IlkYatirim, Y = f(x), Soluk = true });
            var pc = new SacilimGrafik.Cizgi { Seri = 0 };
            foreach (var x in bs.Pareto) { pc.Noktalar.Add((x.IlkYatirim, f(x))); grfPareto.Noktalar.Add(new SacilimGrafik.Nokta { X = x.IlkYatirim, Y = f(x), Seri = 0 }); }
            grfPareto.Cizgiler.Add(pc);
            if (butce > 0 && uygun.Count > 0 && butce <= uygun.Max(x => x.IlkYatirim) * 1.5) grfPareto.Dikeyler.Add((butce, $"bütçe {Cizim.Kisa(butce)} TL", 1));
            if (bs.Secim != null) grfPareto.Noktalar.Add(new SacilimGrafik.Nokta { X = bs.Secim.IlkYatirim, Y = f(bs.Secim), Seri = 0, Vurgu = true, Etiket = "bütçeye göre en iyi" });
            if (sec.Ts825Uygun && (bs.Secim == null || !Ayni(sec, bs.Secim)))
                grfPareto.Noktalar.Add(new SacilimGrafik.Nokta { X = sec.IlkYatirim, Y = f(sec), Seri = 2, Vurgu = true, Etiket = "teklifteki seçim" });
            grfPareto.YBaslik = olcu.ad.Substring(0, 1).ToUpper(tr) + olcu.ad.Substring(1) + (amac == ButceAmaci.Isitma ? " (kWh)" : amac == ButceAmaci.Karbon ? " (kg CO₂)" : " (TL)");
            grfPareto.SeriAd = new[] { "Pareto cephesi (gri: diğer uygun seçenekler)" }; grfPareto.SeriRenk = new[] { 0 };
            grfPareto.XBicim = Cizim.Kisa; grfPareto.YBicim = Cizim.Kisa;
            grfPareto.Invalidate();
        }
    }
}
