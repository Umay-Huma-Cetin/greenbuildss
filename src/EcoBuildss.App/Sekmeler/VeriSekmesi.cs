using System.Text;
using EcoBuildss.Core.Veri;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Firmanın kendi gerçek verisini içeri alma: tüketim kayıtları (faturalar) ve teklif geçmişi (CRM).
    /// Her dosya şemaya göre satır satır denetlenir; hatalı satırlar alınmaz ve nedenleri listelenir.
    /// </summary>
    public sealed class VeriSekmesi : UserControl
    {
        readonly Durum d;
        readonly ComboBox veriSeti = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        readonly Label veriDurumu = new() { AutoSize = true, Font = Ui.Kalin, Margin = new Padding(0, 9, 12, 0) };
        readonly DataGridView onizleme = Ui.Izgara(), sema = Ui.Izgara();
        readonly ListBox bulgular = new() { Dock = DockStyle.Fill, Font = Ui.Kucuk, BorderStyle = BorderStyle.None, HorizontalScrollbar = true };
        readonly Label bulguOzet = new() { Dock = DockStyle.Top, Height = 22, Font = Ui.Kalin };

        Sema Secili => veriSeti.SelectedIndex == 0 ? d.TuketimSemasi : d.TeklifSemasi;

        public VeriSekmesi(Durum durum)
        {
            d = durum;
            veriSeti.Items.AddRange(new object[] { "Tüketim kayıtları (faturalar)", "Teklif geçmişi" });
            veriSeti.SelectedIndex = 0;

            var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 6, 8, 0), WrapContents = true };
            ust.Controls.Add(Ui.Etiket("Veri seti")); ust.Controls.Add(veriSeti); ust.Controls.Add(veriDurumu);
            ust.Controls.Add(Ui.Dugme("İçe aktar", (_, __) => IceAktar(), true));
            ust.Controls.Add(Ui.Dugme("Şablon", (_, __) => Sablon()));
            ust.Controls.Add(Ui.Dugme("Sil", (_, __) => Sil()));

            var izgara = Ui.Izgara2(2, 62, 38);
            izgara.Controls.Add(Ui.Kart("Kayıtlar", onizleme), 0, 0);
            var sag = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            sag.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); sag.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            sag.Controls.Add(Ui.Kart("Sütunlar", sema), 0, 0);
            var bp = new Panel(); bp.Controls.Add(bulgular); bp.Controls.Add(bulguOzet);
            sag.Controls.Add(Ui.Kart("Denetim", bp), 0, 1);
            izgara.Controls.Add(sag, 1, 0);

            Controls.Add(izgara); Controls.Add(ust);
            veriSeti.SelectedIndexChanged += (_, __) => Goster();
            d.TekliflerDegisti += Goster;
            Load += (_, __) => Goster();
        }

        void Goster()
        {
            var s = Secili;
            sema.Columns.Clear(); sema.Rows.Clear();
            foreach (var c in new[] { "Sütun", "Açıklama", "Biçim", "Zorunlu" }) sema.Columns.Add(c, c);
            sema.Columns[1].FillWeight = 180;
            foreach (var a in s.Alanlar)
            {
                string bicim = a.Tur switch
                {
                    AlanTuru.Sayi or AlanTuru.Tamsayi => $"{(a.Tur == AlanTuru.Tamsayi ? "tam sayı" : "sayı")} {(double.IsInfinity(a.Min) ? "" : Ui.Sayi(a.Min, "0.##"))}–{(double.IsInfinity(a.Max) ? "" : Ui.Sayi(a.Max, "#,0.##"))} {a.Birim}".Trim(),
                    AlanTuru.Tarih => "yyyy-aa-gg", AlanTuru.Secenek => a.Secenekler.Length > 6 ? "il adı" : string.Join(" / ", a.Secenekler), _ => "metin",
                };
                sema.Rows.Add(a.Ad, a.Aciklama, bicim, a.Zorunlu ? "evet" : "hayır");
            }
            onizleme.Columns.Clear(); onizleme.Rows.Clear();
            if (d.Db == null) { veriDurumu.Text = "Veritabanı kapalı"; return; }
            var (kayit, kaynak) = veriSeti.SelectedIndex == 0 ? d.Tuketim() : d.Teklifler();
            veriDurumu.Text = kayit.Count == 0 ? "Veri yok" : $"{Ui.Sayi(kayit.Count)} kayıt";
            veriDurumu.ForeColor = kayit.Count == 0 ? Ui.Soluk : Ui.Yesil;
            foreach (var a in s.Alanlar) onizleme.Columns.Add(a.Ad, a.Ad);
            foreach (var k in kayit.Take(500))
                onizleme.Rows.Add(s.Alanlar.Select(a => k.Deger.TryGetValue(a.Ad, out var v) ? v switch
                { null => "", double x => x.ToString("#,0.###", Ui.TR), DateTime t => t.ToString("dd.MM.yyyy"), _ => v.ToString() } : "").Cast<object>().ToArray());
        }

        void IceAktar()
        {
            if (d.Db == null) { Ui.Hata(this, "Veritabanı açılamadı: " + d.DbHatasi); return; }
            using var f = new OpenFileDialog { Filter = "CSV dosyası|*.csv;*.txt|Tüm dosyalar|*.*", Title = $"{Secili.Ad} dosyasını seçin" };
            if (f.ShowDialog(this) != DialogResult.OK) return;
            Yukle(IceAktarici.Dosyadan(f.FileName, Secili), Path.GetFileName(f.FileName));
        }


        void Yukle(IceAktarmaSonucu s, string ad)
        {
            bulgular.Items.Clear();
            bulguOzet.Text = $"{ad}: {s.Ozet}";
            bulguOzet.ForeColor = s.DosyaGecersiz || s.Kayitlar.Count == 0 ? Ui.Kirmizi : s.Reddedilen > 0 ? Color.FromArgb(160, 100, 20) : Ui.Yesil;
            foreach (var b in s.Bulgular.Take(2000)) bulgular.Items.Add((b.Hata ? "✗ " : "• ") + b);
            if (s.DosyaGecersiz || s.Kayitlar.Count == 0) { d.Bildir("İçe aktarma yapılmadı: " + s.Ozet); return; }
            if (s.Reddedilen > 0 && MessageBox.Show(this, $"{s.Reddedilen} hatalı satır alınmayacak. {s.Kayitlar.Count} satır kaydedilsin mi?",
                    "Denetim", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            d.Db!.VeriKaydet(Secili, s.Kayitlar, false);
            d.Bildir($"{s.Kayitlar.Count} kayıt alındı.");
            Goster();
            d.ModelleriKur();
            d.VeriDegistiDuyur();
        }

        void Sablon()
        {
            using var f = new SaveFileDialog { Filter = "CSV dosyası|*.csv", FileName = Secili.DosyaAdi };
            if (f.ShowDialog(this) != DialogResult.OK) return;
            File.WriteAllText(f.FileName, Secili.SablonCsv(), new UTF8Encoding(true));
            d.Bildir("Şablon kaydedildi: " + f.FileName);
        }

        void Sil()
        {
            if (d.Db == null) return;
            if (MessageBox.Show(this, $"{Secili.Ad}: tüm kayıtlar silinsin mi?", "Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            d.Db.VeriSil(Secili); Goster(); d.ModelleriKur(); d.VeriDegistiDuyur();
        }
    }
}
