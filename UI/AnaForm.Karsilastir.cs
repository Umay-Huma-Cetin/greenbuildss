using System.Drawing;
using System.Windows.Forms;
using EcoBuildss.Core;

namespace EcoBuildss.UI
{
    /// <summary>Senaryo karşılaştırma: iki teklif durumunu (il, bina, ısıtma, paket…) yan yana gösterir.</summary>
    public sealed partial class AnaForm
    {
        sealed class Senaryo { public string Ad = ""; public Secenek S = null!; public Sonuc R = null!; public string Proje = ""; public int Yil; }
        readonly Senaryo?[] senaryo = new Senaryo?[2];
        readonly DataGridView gridKars = new();
        readonly Label lblKars = new() { AutoSize = true, MaximumSize = new Size(900, 0), Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(29, 35, 33), Margin = new Padding(0, 6, 0, 8) };

        TabPage KarsilastirSekmesi()
        {
            var s = new TabPage("Karşılaştır") { BackColor = Color.White, Padding = new Padding(12) };
            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
            Button D(string t, int i)
            {
                var b = new Button { Text = t, AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Yesil, BackColor = Color.White, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 0, 10, 0), Cursor = Cursors.Hand };
                b.FlatAppearance.BorderColor = Yesil;
                b.Click += (_, _) => SenaryoAl(i);
                return b;
            }
            ust.Controls.Add(D("Şu anki teklifi A olarak al", 0));
            ust.Controls.Add(D("Şu anki teklifi B olarak al", 1));
            var bas = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            bas.Controls.Add(ust); bas.Controls.Add(lblKars);
            lblKars.Text = "Teklif sekmesinde bir durumu (il, bina, ısıtma, paket…) hazırlayıp A'ya, başka bir durumu B'ye alın; iki senaryo burada yan yana karşılaştırılır.";

            gridKars.Dock = DockStyle.Fill; gridKars.ReadOnly = true; gridKars.AllowUserToAddRows = false; gridKars.RowHeadersVisible = false;
            gridKars.BackgroundColor = Color.White; gridKars.BorderStyle = BorderStyle.None; gridKars.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridKars.EnableHeadersVisualStyles = false; gridKars.ColumnHeadersDefaultCellStyle.BackColor = Zemin;
            gridKars.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            gridKars.DefaultCellStyle.WrapMode = DataGridViewTriState.True; gridKars.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            gridKars.DefaultCellStyle.SelectionBackColor = YesilAcik; gridKars.DefaultCellStyle.SelectionForeColor = Color.Black;
            foreach (var (ad, b, w) in new[] { ("k", "Gösterge", 22f), ("a", "Senaryo A", 30f), ("b", "Senaryo B", 30f), ("f", "Fark (B − A)", 18f) })
            {
                var c = new DataGridViewTextBoxColumn { Name = ad, HeaderText = b, FillWeight = w, SortMode = DataGridViewColumnSortMode.NotSortable };
                if (ad == "f") c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopRight;
                gridKars.Columns.Add(c);
            }
            s.Controls.Add(gridKars);
            s.Controls.Add(bas);
            return s;
        }

        void SenaryoAl(int i)
        {
            if (sonTeklif == null) return;
            var (sec, r, _, paketAdi, yil) = sonTeklif.Value;
            senaryo[i] = new Senaryo { Ad = paketAdi, S = sec, R = r, Proje = projeMetni, Yil = yil };
            KarsilastirmaYaz();
            Bildir($"Şu anki teklif senaryo {(i == 0 ? "A" : "B")} olarak alındı.");
        }

        void KarsilastirmaYaz()
        {
            var tr = Ortak.TR;
            gridKars.Rows.Clear();
            var a = senaryo[0]; var b = senaryo[1];
            string Metin(Senaryo? x, Func<Senaryo, string> f) => x == null ? "Henüz alınmadı" : f(x);
            void Satir(string ad, Func<Senaryo, string> f, Func<Senaryo, double>? sayi = null, Func<double, string>? bicim = null, bool azIyi = true)
            {
                string fark = "";
                Color? renk = null;
                if (a != null && b != null && sayi != null)
                {
                    double d = sayi(b) - sayi(a);
                    fark = Math.Abs(d) < 1e-9 ? "Aynı" : (d > 0 ? "+" : "−") + bicim!(Math.Abs(d));
                    if (Math.Abs(d) > 1e-9) renk = (d < 0) == azIyi ? Yesil : Turuncu;
                }
                int ix = gridKars.Rows.Add(ad, Metin(a, f), Metin(b, f), fark);
                if (renk is Color c) gridKars.Rows[ix].Cells["f"].Style.ForeColor = c;
            }
            string T(double v) => TL(v);
            Satir("Proje", x => x.Proje);
            Satir("Proje türü ve ısıtma", x => $"{(x.R.Mevcut ? "Mevcut bina (yenileme)" : "Yeni bina")} · {x.R.YakitAdi}{(x.R.Sogutma ? " · klima" : "")}");
            Satir("Paket", x => $"{x.Ad}: {TeklifRaporu.PaketAdi(x.S)}");
            Satir("İlk yatırım (KDV hariç)", x => T(x.S.IlkYatirim), x => x.S.IlkYatirim, T);
            Satir("Kullanım alanı başına yatırım", x => (x.S.IlkYatirim / x.R.KullanimAlani).ToString("N0", tr) + " TL/m²", x => x.S.IlkYatirim / x.R.KullanimAlani, v => v.ToString("N0", tr) + " TL/m²");
            Satir("Yıllık ısıtma ihtiyacı", x => (x.S.YillikIsitmaKWh / x.R.KullanimAlani).ToString("N1", tr) + " kWh/m²", x => x.S.YillikIsitmaKWh / x.R.KullanimAlani, v => v.ToString("N1", tr) + " kWh/m²");
            Satir("Yıllık enerji gideri", x => T(x.S.YillikGiderTL), x => x.S.YillikGiderTL, T);
            Satir("Ömür boyu maliyet (bugünkü değer)", x => $"{T(x.S.ToplamMaliyet)} ({x.Yil} yıl)", x => x.S.ToplamMaliyet, T);
            Satir("Toplam CO₂", x => (x.S.ToplamCO2Kg / 1000).ToString("N1", tr) + " t", x => x.S.ToplamCO2Kg / 1000, v => v.ToString("N1", tr) + " t");
            Satir("Duvar U · pencere U", x => $"{x.S.DuvarU.ToString("0.00", tr)} · {x.S.PencereU.ToString("0.0", tr)} W/m²K");
            Satir("TS 825:2024", x => x.S.Ts825Uygun ? $"Uygun ({x.R.BolgeNo}. bölge)" : $"Uygun değil ({x.R.BolgeNo}. bölge)");
            Satir("Duvar ağırlığı (bilgi)", x => x.S.DuvarAgirlikTon.ToString("N0", tr) + " t", x => x.S.DuvarAgirlikTon, v => v.ToString("N0", tr) + " t");
            Satir("Yangın yönetmeliği", x => x.S.YanginNotu.Length > 0 ? x.S.YanginNotu : "Ek koşul yok (hiç yanmaz yalıtım)");
            if ((a?.R.Mevcut ?? false) || (b?.R.Mevcut ?? false))
                Satir("Mevcut duruma göre yıllık tasarruf", x => x.R.Mevcut ? T(Kpi.MevcutaGore(x.S, x.R).tasarrufTL) : "Yeni bina", (a?.R.Mevcut ?? false) && (b?.R.Mevcut ?? false) ? x => Kpi.MevcutaGore(x.S, x.R).tasarrufTL : null, T, false);
        }
    }
}
