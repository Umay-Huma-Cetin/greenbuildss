using EcoBuildss.Core.Depo;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Kaydedilen teklifler. Sonuç (Kabul / Red) işaretlendikçe kayıt teklif geçmişine katılır ve kabul tahmini modeli
    /// firmanın kendi sonuçlarından öğrenir. Tahmin ile gerçekleşen sonuç yan yana izlenir.
    /// </summary>
    public sealed class KayitlarSekmesi : UserControl
    {
        readonly Durum d;
        readonly DataGridView liste = Ui.Izgara();
        readonly Label ozet = new() { AutoSize = true, Font = Ui.Kucuk, ForeColor = Ui.Soluk, Margin = new Padding(12, 11, 0, 0) };
        readonly Label[] kpi = Enumerable.Range(0, 4).Select(_ => new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 18f), ForeColor = Ui.Koyu }).ToArray();
        readonly Label[] kpiAlt = Enumerable.Range(0, 4).Select(_ => new Label { AutoSize = true, Font = Ui.Kucuk, ForeColor = Ui.Soluk }).ToArray();
        public event Action<KayitliTeklif>? TeklifeYukle;
        readonly Button silDugmesi;

        /// <summary>Teklif silme yalnız yöneticiye açıktır (veritabanı katmanı da ayrıca denetler).</summary>
        public void YetkiyiYansit() => silDugmesi.Visible = d.Yonetici;

        public KayitlarSekmesi(Durum durum)
        {
            d = durum;
            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 6, 8, 0) };
            ust.Controls.Add(Ui.Dugme("Kabul", (_, __) => Isaretle("Kabul"), true));
            ust.Controls.Add(Ui.Dugme("Red", (_, __) => Isaretle("Red")));
            ust.Controls.Add(Ui.Dugme("Bekliyor", (_, __) => Isaretle("Bekliyor")));
            ust.Controls.Add(Ui.Dugme("Aç", (_, __) => { if (Secili() is KayitliTeklif t) TeklifeYukle?.Invoke(t); }));
            silDugmesi = Ui.Dugme("Sil", (_, __) => Sil());
            ust.Controls.Add(silDugmesi);
            ust.Controls.Add(ozet);
            var bant = new TableLayoutPanel { Dock = DockStyle.Top, Height = 92, ColumnCount = 4, BackColor = Color.White, Padding = new Padding(16, 10, 10, 6) };
            string[] bas = { "BEKLEYEN TEKLİF", "KAZANMA ORANI", "KAZANILAN İŞ (KDV HARİÇ)", "SON 30 GÜN" };
            for (int i = 0; i < 4; i++)
            {
                bant.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
                p.Controls.Add(new Label { Text = bas[i], AutoSize = true, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Ui.Yesil });
                p.Controls.Add(kpi[i]); p.Controls.Add(kpiAlt[i]); bant.Controls.Add(p, i, 0);
            }
            var baslik = new Label { Text = "Tekliflerim", Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI Semibold", 14f), ForeColor = Ui.Koyu, Padding = new Padding(12, 8, 0, 0) };
            Controls.Add(Ui.Kart("Teklifler", liste));
            Controls.Add(ust); Controls.Add(bant); Controls.Add(baslik);
            liste.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && Secili() is KayitliTeklif t) TeklifeYukle?.Invoke(t); };
            d.TekliflerDegisti += Doldur;
            Load += (_, __) => Doldur();
        }

        KayitliTeklif? Secili() => liste.CurrentRow?.Tag as KayitliTeklif;

        void Doldur()
        {
            liste.Columns.Clear(); liste.Rows.Clear();
            if (d.Db == null) { ozet.Text = "Veritabanı açılamadı: " + d.DbHatasi; return; }
            foreach (var c in new[] { "Teklif no", "Tarih", "Müşteri / apartman", "Tip", "İl", "İş", "Alan (m²)", "Paket", "Teklif (KDV hariç)", "TL/m²", "Geri ödeme", "Kazanma olasılığı", "Sonuç", "Hazırlayan" }) liste.Columns.Add(c, c);
            var l = d.Db.Teklifler();
            foreach (var t in l)
            {
                int i = liste.Rows.Add(t.TeklifNo, t.Tarih.ToString("dd.MM.yyyy"), t.Musteri, t.MusteriTipi, t.Il, t.ProjeTuru == "Yenileme" ? (t.DaireSayisi > 0 ? $"Mantolama · {t.DaireSayisi} daire" : "Mantolama") : "Yeni bina", Ui.Sayi(t.KullanimAlani), t.Paket, Ui.Tl(t.TeklifTL),
                                       Ui.Sayi(t.TeklifTLm2), t.GeriOdemeYil.HasValue ? Ui.Sayi(t.GeriOdemeYil.Value, "0.0") + " yıl" : "—",
                                       t.KabulOlasiligi.HasValue ? Ui.Olasilik(t.KabulOlasiligi.Value) : "—", t.Sonuc, t.Hazirlayan);
                liste.Rows[i].Tag = t;
                liste.Rows[i].Cells[12].Style.ForeColor = t.Sonuc == "Kabul" ? Ui.Yesil : t.Sonuc == "Red" ? Ui.Kirmizi : Ui.Soluk;
            }
            int kabul = l.Count(t => t.Sonuc == "Kabul"), red = l.Count(t => t.Sonuc == "Red");
            var tahminli = l.Where(t => t.Sonuc != "Bekliyor" && t.KabulOlasiligi.HasValue).ToList();
            string isabet = tahminli.Count == 0 ? "" : $" · tahmin isabeti %{100.0 * tahminli.Count(t => (t.KabulOlasiligi >= (d.Kabul?.Esik ?? 0.5)) == (t.Sonuc == "Kabul")) / tahminli.Count:0} ({tahminli.Count} teklif)";
            ozet.Text = $"{l.Count} teklif · {kabul} kabul · {red} red · {l.Count - kabul - red} bekliyor{isabet}";
            var bek = l.Where(t => t.Sonuc == "Bekliyor").ToList(); var son = l.Where(t => t.Tarih >= DateTime.Today.AddDays(-30)).ToList();
            kpi[0].Text = bek.Count.ToString(); kpiAlt[0].Text = bek.Count == 0 ? "takip edilecek teklif yok" : $"toplam {Ui.Tl(bek.Sum(t => t.TeklifTL))}";
            kpi[1].Text = kabul + red == 0 ? "—" : Ui.Yuzde((double)kabul / (kabul + red), "0"); kpiAlt[1].Text = $"{kabul} kabul / {red} red";
            kpi[2].Text = Ui.Tl(l.Where(t => t.Sonuc == "Kabul").Sum(t => t.TeklifTL)); kpiAlt[2].Text = "";
            kpi[3].Text = son.Count.ToString(); kpiAlt[3].Text = $"teklif · {Ui.Tl(son.Sum(t => t.TeklifTL))}";
        }

        void Isaretle(string sonuc)
        {
            if (Secili() is not KayitliTeklif t || d.Db == null) return;
            try { d.Db.SonucIsaretle(t.Id, sonuc); } catch (UnauthorizedAccessException e) { Ui.Hata(this, e.Message); return; }
            Doldur();
            d.ModelleriKur();
            d.Bildir($"{t.TeklifNo}: {sonuc}.");
        }

        void Sil()
        {
            if (Secili() is not KayitliTeklif t || d.Db == null) return;
            if (MessageBox.Show(this, $"{t.TeklifNo} silinsin mi? Bu işlem geri alınamaz.", "Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { d.Db.TeklifSil(t.Id); } catch (UnauthorizedAccessException e) { Ui.Hata(this, e.Message); return; }
            Doldur(); d.ModelleriKur();
        }
    }
}
