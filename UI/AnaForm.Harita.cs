using System.Drawing;
using System.Windows.Forms;
using EcoBuildss.Core;
using EcoBuildss.Depo;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Harita sekmesi: 81 il haritasında seçilen göstergeye göre renklendirme (küme, TS 825 bölgesi, derece-gün,
    /// ekonomik optimum yalıtım kalınlığı, önerilen paket, enerji gideri). Paket ve kalınlık, sol paneldeki bina ve
    /// öncelikle her ilin kendi iklimi ve il merkezi TS 825 bölgesi için hesaplanır.
    /// </summary>
    public sealed partial class AnaForm
    {
        TabPage sHarita = null!;
        readonly HaritaPanel harita = new() { Dock = DockStyle.Fill };
        readonly ComboBox cmbHarita = Combo();
        readonly Label lblHarita = new() { AutoSize = true, MaximumSize = new Size(270, 0), Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(29, 35, 33), Margin = new Padding(0, 10, 0, 0) };

        static string HaritaAdi(Il il) => il.Ad.Split('(')[0].Trim();

        TabPage HaritaSekmesi()
        {
            var s = sHarita = new TabPage("Harita") { BackColor = Zemin, Padding = new Padding(12) };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            cmbHarita.Items.AddRange(new object[] { "Önerilen paket", "Ekonomik optimum EPS kalınlığı", "Yıllık enerji gideri (önerilen paket)", "İklim kümesi", "TS 825 bölgesi (il merkezi)", "Isıtma derece-günü", "Soğutma derece-günü" });
            cmbHarita.SelectedIndex = 0;
            cmbHarita.SelectedIndexChanged += (_, _) => HaritaGuncelle();
            p.Controls.Add(Alan("Gösterge", cmbHarita));
            p.Controls.Add(lblHarita);
            t.Controls.Add(Kart(p, "Türkiye haritası"), 0, 0);
            var hk = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8), Margin = new Padding(0, 0, 0, 12) };
            hk.Controls.Add(harita);
            t.Controls.Add(hk, 1, 0);
            s.Controls.Add(t);
            harita.IlTiklandi += (_, ad) =>
            {
                if (cmbIl.Items.Cast<Il>().FirstOrDefault(i => HaritaAdi(i) == ad) is Il il) { cmbIl.SelectedItem = il; sekmeler.SelectedIndex = 0; }
            };
            try { harita.Iller.AddRange(HaritaPanel.Oku(Path.Combine(AppContext.BaseDirectory, "Veri", "il_sinirlari.csv"))); } catch (Exception ex) { Gunluk.Hata("İl sınırları okunamadı", ex); }
            sekmeler.SelectedIndexChanged += (_, _) => { if (sekmeler.SelectedTab == sHarita) HaritaGuncelle(); };
            return s;
        }

        static Color Ara(Color a, Color b, double t) => Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

        void HaritaGuncelle()
        {
            if (veri == null || sonGirdi == null) return;
            var tr = Ortak.TR;
            harita.Degerler.Clear(); harita.Lejant.Clear();
            harita.Vurgu = cmbIl.SelectedItem is Il sec ? HaritaAdi(sec) : null;
            int secim = cmbHarita.SelectedIndex;
            harita.Baslik = (string)cmbHarita.SelectedItem!;

            // Sürekli değer: 5 eşit aralıklı sınıf, açık → koyu yeşil
            void Surekli(Dictionary<string, double> d, Func<double, string> bicim)
            {
                if (d.Count == 0) return;
                double min = d.Values.Min(), max = d.Values.Max(), adim = (max - min) / 5;
                Color a = Color.FromArgb(222, 240, 228), b = Color.FromArgb(20, 96, 60);
                for (int i = 0; i < 5; i++)
                {
                    double alt = min + i * adim, ust = i == 4 ? max : alt + adim;
                    harita.Lejant.Add((Ara(a, b, i / 4.0), $"{bicim(alt)} – {bicim(ust)}"));
                }
                foreach (var kv in d)
                {
                    int sinif = adim <= 0 ? 0 : Math.Min(4, (int)((kv.Value - min) / adim));
                    harita.Degerler[kv.Key] = (Ara(a, b, sinif / 4.0), bicim(kv.Value));
                }
            }

            if (secim is 3 && bolgeselSonuc != null)
            {
                foreach (var k in bolgeselSonuc.Kumeler)
                {
                    var c = Cizim.Seri[(k.No - 1) % Cizim.Seri.Length];
                    harita.Lejant.Add((c, $"{k.Ad} ({k.OrtIsitma.ToString("N0", tr)} DG)"));
                    foreach (var il in k.Uyeler) harita.Degerler[HaritaAdi(il)] = (c, $"{k.Ad} · ısıtma {il.IsitmaDG.ToString("N0", tr)} DG");
                }
                lblHarita.Text = "k-ortalamalar kümeleri (Bölgesel analiz sekmesindeki k ile).";
            }
            else if (secim == 4)
            {
                foreach (var b in veri.Bolgeler) harita.Lejant.Add((Ara(Color.FromArgb(240, 196, 120), Color.FromArgb(40, 90, 160), (b.No - 1) / 5.0), $"{b.No}. bölge"));
                foreach (var il in veri.Iller.Where(i => i.Ts825Bolge.HasValue))
                    harita.Degerler[HaritaAdi(il)] = (Ara(Color.FromArgb(240, 196, 120), Color.FromArgb(40, 90, 160), (il.Ts825Bolge!.Value - 1) / 5.0), $"{il.Ts825Bolge}. bölge ({il.BolgeKaynagi})");
                lblHarita.Text = "İl merkezinin TS 825:2024 iklim bölgesi (81 il, hepsi kaynaklı). İlçeler farklı bölgede olabilir.";
            }
            else if (secim is 5 or 6)
            {
                Surekli(veri.Iller.ToDictionary(HaritaAdi, i => secim == 5 ? i.IsitmaDG : i.SogutmaDG), x => x.ToString("N0", tr));
                lblHarita.Text = "MGM yıllık derece-gün (78 il 1975–2005; Çankırı, Karabük, Şırnak 1991–2005).";
            }
            else
            {
                // Her il kendi iklimi ve il merkezi bölgesiyle; bina, pencere, yakıt, öncelik ve süre sol panelden
                var model = FiyatModeli.Hepsi(veri).FirstOrDefault(m => m.Aile == "EPS");
                var paketler = new Dictionary<string, Secenek>(); var opt = new Dictionary<string, double>(); var gider = new Dictionary<string, double>();
                int uygunsuz = 0;
                foreach (var il in veri.Iller.Where(i => i.Ts825Bolge.HasValue))
                {
                    var g = sonGirdi.Kopya(); g.Il = il; g.Bolge = il.Ts825Bolge!.Value; g.SadeceTs825 = true;
                    var r = KararMotoru.Hesapla(veri, g);
                    var o = r.Siralama.FirstOrDefault();
                    if (o == null) { uygunsuz++; continue; }
                    paketler[HaritaAdi(il)] = o;
                    gider[HaritaAdi(il)] = o.YillikGiderTL / r.KullanimAlani;
                    if (model != null) opt[HaritaAdi(il)] = Optimizasyon.OptimumKalinlik(veri, g, model, o.Duvar, o.Cam).KisitsizCm;
                }
                if (secim == 0)
                {
                    var siklik = paketler.Values.GroupBy(p => p.Kod).OrderByDescending(x => x.Count()).ToList();
                    for (int i = 0; i < siklik.Count; i++)
                    {
                        var c = i < Cizim.Seri.Length - 1 ? Cizim.Seri[i] : Cizim.Seri[Cizim.Seri.Length - 1];
                        string ad = TeklifRaporu.PaketAdi(siklik[i].First());
                        if (i < Cizim.Seri.Length - 1) harita.Lejant.Add((c, $"{ad} ({siklik[i].Count()} il)"));
                        foreach (var kv in paketler.Where(x => x.Value.Kod == siklik[i].Key)) harita.Degerler[kv.Key] = (c, ad);
                    }
                    if (siklik.Count >= Cizim.Seri.Length) harita.Lejant.Add((Cizim.Seri[Cizim.Seri.Length - 1], "Diğer paketler"));
                    lblHarita.Text = "Her il için, il merkezinin TS 825 bölgesi ve kendi derece-günüyle önerilen paket (sol paneldeki bina, pencere, ısıtma ve öncelikle). İle tıklayınca teklif o il için açılır." +
                                     (uygunsuz > 0 ? $"\n{uygunsuz} ilde listedeki malzemelerle TS 825'e uygun seçenek yok." : "");
                }
                else if (secim == 1)
                {
                    Surekli(opt, x => x.ToString("0.0", tr) + " cm");
                    lblHarita.Text = "Ömür boyu maliyeti en düşük EPS kalınlığı (yönetmelik sınırı uygulanmadan; önerilen paketin duvarı ve camıyla). Koyu renk: kalın yalıtım kendini daha çok ödüyor.";
                }
                else
                {
                    Surekli(gider, x => x.ToString("N0", tr) + " TL/m²");
                    lblHarita.Text = "Önerilen paketle kullanım alanı başına yıllık enerji gideri (ısıtma ve varsa soğutma).";
                }
            }
            harita.Invalidate();
        }
    }
}
