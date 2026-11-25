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
        readonly Label ozet = new() { AutoSize = true, Font = Ui.Kalin, Margin = new Padding(12, 9, 0, 0) };
        public event Action<KayitliTeklif>? TeklifeYukle;

        public KayitlarSekmesi(Durum durum)
        {
            d = durum;
            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 6, 8, 0) };
            ust.Controls.Add(Ui.Dugme("Kabul edildi", (_, __) => Isaretle("Kabul"), true));
            ust.Controls.Add(Ui.Dugme("Reddedildi", (_, __) => Isaretle("Red")));
            ust.Controls.Add(Ui.Dugme("Bekliyor", (_, __) => Isaretle("Bekliyor")));
            ust.Controls.Add(Ui.Dugme("Teklif ekranında aç", (_, __) => { if (Secili() is KayitliTeklif t) TeklifeYukle?.Invoke(t); }));
            ust.Controls.Add(Ui.Dugme("Sil", (_, __) => Sil()));
            ust.Controls.Add(ozet);
            Controls.Add(Ui.Kart("Kayıtlı teklifler", liste, "Sonucu belli olan teklifleri işaretleyin: kabul tahmini modeli bu kayıtlardan öğrenir"));
            Controls.Add(ust);
            liste.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && Secili() is KayitliTeklif t) TeklifeYukle?.Invoke(t); };
            d.TekliflerDegisti += Doldur;
            Load += (_, __) => Doldur();
        }

        KayitliTeklif? Secili() => liste.CurrentRow?.Tag as KayitliTeklif;

        void Doldur()
        {
            liste.Columns.Clear(); liste.Rows.Clear();
            if (d.Db == null) { ozet.Text = "Veritabanı açılamadı: " + d.DbHatasi; return; }
            foreach (var c in new[] { "Teklif no", "Tarih", "Müşteri", "Tip", "İl", "Proje", "Alan (m²)", "Paket", "Teklif (KDV hariç)", "TL/m²", "Geri ödeme", "Tahmini kabul", "Sonuç" }) liste.Columns.Add(c, c);
            var l = d.Db.Teklifler();
            foreach (var t in l)
            {
                int i = liste.Rows.Add(t.TeklifNo, t.Tarih.ToString("dd.MM.yyyy"), t.Musteri, t.MusteriTipi, t.Il, t.ProjeTuru, Ui.Sayi(t.KullanimAlani), t.Paket, Ui.Tl(t.TeklifTL),
                                       Ui.Sayi(t.TeklifTLm2), t.GeriOdemeYil.HasValue ? Ui.Sayi(t.GeriOdemeYil.Value, "0.0") + " yıl" : "—",
                                       t.KabulOlasiligi.HasValue ? Ui.Yuzde(t.KabulOlasiligi.Value, "0") : "—", t.Sonuc);
                liste.Rows[i].Tag = t;
                liste.Rows[i].Cells[12].Style.ForeColor = t.Sonuc == "Kabul" ? Ui.Yesil : t.Sonuc == "Red" ? Ui.Kirmizi : Ui.Soluk;
            }
            int kabul = l.Count(t => t.Sonuc == "Kabul"), red = l.Count(t => t.Sonuc == "Red");
            var tahminli = l.Where(t => t.Sonuc != "Bekliyor" && t.KabulOlasiligi.HasValue).ToList();
            string isabet = tahminli.Count == 0 ? "" : $" · tahmin isabeti %{100.0 * tahminli.Count(t => (t.KabulOlasiligi >= (d.Kabul?.Esik ?? 0.5)) == (t.Sonuc == "Kabul")) / tahminli.Count:0} ({tahminli.Count} teklif)";
            ozet.Text = $"{l.Count} teklif · {kabul} kabul · {red} red · {l.Count - kabul - red} bekliyor{isabet}";
        }

        void Isaretle(string sonuc)
        {
            if (Secili() is not KayitliTeklif t || d.Db == null) return;
            d.Db.SonucIsaretle(t.Id, sonuc);
            Doldur();
            d.ModelleriKur();
            d.Bildir($"{t.TeklifNo}: {sonuc}. Kabul tahmini modeli güncellendi.");
        }

        void Sil()
        {
            if (Secili() is not KayitliTeklif t || d.Db == null) return;
            if (MessageBox.Show(this, $"{t.TeklifNo} silinsin mi? Bu işlem geri alınamaz.", "Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            d.Db.TeklifSil(t.Id); Doldur(); d.ModelleriKur();
        }
    }
}
