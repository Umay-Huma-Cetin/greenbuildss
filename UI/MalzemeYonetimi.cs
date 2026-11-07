using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using EcoBuildss.Core;
using EcoBuildss.Depo;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Malzeme yönetimi: malzemeler.csv satırlarını düzenler, yeni satır ekler. Kaynağı, tarihi ve fiyat hesabı yazılmayan
    /// satır kaydedilmez (veride kaynaksız değer olmasın). Kayıttan sonra veritabanı bir sonraki açılışta CSV'den yenilenir.
    /// </summary>
    public sealed class MalzemeYonetimi : Form
    {
        readonly string yol;
        readonly DataGridView grid = new() { Dock = DockStyle.Fill };
        readonly Label lbl = new() { Dock = DockStyle.Bottom, Height = 46, ForeColor = Color.FromArgb(29, 35, 33), Padding = new Padding(4, 6, 4, 0) };
        string[] baslik = Array.Empty<string>();
        static readonly string[] Sayisal = { "kalinlik_m", "yogunluk_kg_m3", "lambda_W_mK", "U_W_m2K", "birim_agirlik_kg_m2", "karbon_faktoru_kgCO2e_kg", "karbon_kgCO2e_m2", "fiyat_TL_m2" };
        public bool Kaydedildi { get; private set; }

        public MalzemeYonetimi(string veriKlasoru)
        {
            yol = Path.Combine(veriKlasoru, "malzemeler.csv");
            Text = "Malzeme yönetimi"; Size = new Size(1280, 640); StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9.5f); BackColor = Color.White;
            grid.AllowUserToAddRows = false; grid.RowHeadersWidth = 28; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(243, 245, 242); grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            Button D(string t, Action a) { var b = new Button { Text = t, AutoSize = true, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand }; b.Click += (_, _) => a(); ust.Controls.Add(b); return b; }
            D("Seçili satırı çoğalt (yeni malzeme)", Cogalt);
            D("Seçili satırı sil", () => { foreach (DataGridViewRow r in grid.SelectedRows) grid.Rows.Remove(r); });
            var kaydet = D("Doğrula ve kaydet", Kaydet); kaydet.BackColor = Color.FromArgb(31, 111, 74); kaydet.ForeColor = Color.White;
            lbl.Text = "Fiyat, kaynak, tarih ve fiyat hesabı her satırda zorunludur. Kimlik Y (cephe yalıtımı), P (pencere camı) ya da D (duvar) ile başlar ve tektir. Değişiklik sonraki açılışta uygulanır.";
            Controls.Add(grid); Controls.Add(ust); Controls.Add(lbl);
            Yukle();
        }

        void Yukle()
        {
            var rows = VeriDeposu.CsvOku(yol);
            baslik = rows[0].Select(h => h.Trim().TrimStart('﻿')).ToArray();
            foreach (var h in baslik) grid.Columns.Add(new DataGridViewTextBoxColumn { Name = h, HeaderText = h, Width = h is "ad" or "fiyat_hesabi" or "fiyat_kaynagi" or "teknik_kaynak" ? 240 : 96, SortMode = DataGridViewColumnSortMode.NotSortable });
            foreach (var r in rows.Skip(1)) grid.Rows.Add(r.Cast<object>().ToArray());
        }

        void Cogalt()
        {
            if (grid.CurrentRow == null) return;
            var v = grid.CurrentRow.Cells.Cast<DataGridViewCell>().Select(c => c.Value).ToArray();
            string tur = (Convert.ToString(v[0]) ?? "Y").Substring(0, 1);
            int n = grid.Rows.Cast<DataGridViewRow>().Select(r => Convert.ToString(r.Cells[0].Value) ?? "").Where(id => id.StartsWith(tur))
                        .Select(id => int.TryParse(id.Substring(1), out var k) ? k : 0).DefaultIfEmpty(0).Max() + 1;
            v[0] = tur + n;
            int ix = grid.Rows.Add(v);
            grid.CurrentCell = grid.Rows[ix].Cells[2];
        }

        List<string> Dogrula()
        {
            var hata = new List<string>(); var idler = new HashSet<string>();
            foreach (DataGridViewRow r in grid.Rows)
            {
                string Al(string s) => Convert.ToString(r.Cells[s].Value)?.Trim() ?? "";
                string id = Al("id");
                if (!Regex.IsMatch(id, @"^[YPD]\d+$")) hata.Add($"Satır {r.Index + 1}: kimlik '{id}' Y, P ya da D ile başlayıp sayıyla bitmeli.");
                if (!idler.Add(id)) hata.Add($"Satır {r.Index + 1}: kimlik '{id}' tekrar ediyor.");
                foreach (var h in baslik)
                {
                    string d = Al(h);
                    if (d.Length == 0) { hata.Add($"{id}: '{h}' boş (kaynaksız ya da eksik değer kaydedilmez)."); continue; }
                    if (Sayisal.Contains(h) && (!double.TryParse(d, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || x < 0))
                        hata.Add($"{id}: '{h}' sayı olmalı (ondalık ayırıcı nokta) ve negatif olamaz.");
                }
                if (double.TryParse(Al("fiyat_TL_m2"), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f <= 0) hata.Add($"{id}: fiyat sıfırdan büyük olmalı.");
            }
            return hata;
        }

        void Kaydet()
        {
            grid.EndEdit();
            var hata = Dogrula();
            if (hata.Count > 0) { MessageBox.Show(this, "Kaydedilmedi:\n\n" + string.Join("\n", hata.Take(15)) + (hata.Count > 15 ? $"\n… ve {hata.Count - 15} hata daha" : ""), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            var eski = VeriDeposu.CsvOku(yol).Skip(1).ToDictionary(r => r[0], r => string.Join("|", r));
            var sb = new StringBuilder();
            string Q(string s) => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
            sb.Append(string.Join(",", baslik.Select(Q))).Append('\n');
            var yeni = new Dictionary<string, string>();
            foreach (DataGridViewRow r in grid.Rows)
            {
                var hucre = baslik.Select(h => Convert.ToString(r.Cells[h].Value)?.Trim() ?? "").ToArray();
                yeni[hucre[0]] = string.Join("|", hucre);
                sb.Append(string.Join(",", hucre.Select(Q))).Append('\n');
            }
            try
            {
                File.Copy(yol, yol + ".yedek", true);
                File.WriteAllText(yol, sb.ToString(), new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                Gunluk.Hata("Malzeme listesi kaydedilemedi", ex);
                MessageBox.Show(this, "Dosya yazılamadı (klasör yazmaya kapalı olabilir):\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return;
            }
            var eklenen = yeni.Keys.Except(eski.Keys).ToList(); var silinen = eski.Keys.Except(yeni.Keys).ToList();
            var degisen = yeni.Keys.Intersect(eski.Keys).Where(k => yeni[k] != eski[k]).ToList();
            Gunluk.Bilgi("Malzeme listesi değiştirildi", $"eklenen: {string.Join(",", eklenen)}; silinen: {string.Join(",", silinen)}; değişen: {string.Join(",", degisen)} (önceki dosya malzemeler.csv.yedek)");
            Kaydedildi = true;
            MessageBox.Show(this, $"Kaydedildi ({eklenen.Count} eklenen, {degisen.Count} değişen, {silinen.Count} silinen). Önceki dosya malzemeler.csv.yedek olarak saklandı.\nDeğişiklikler uygulama yeniden açılınca geçerli olur.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
    }
}
