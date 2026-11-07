using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using EcoBuildss.Core;
using EcoBuildss.Depo;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Teklif belgesi: keşif özetini müşteriye verilebilecek tek sayfalık bir belge olarak yazdırır.
    /// Windows'ta yazıcı listesinden "Microsoft Print to PDF" seçilirse PDF dosyası oluşur.
    /// </summary>
    public sealed partial class AnaForm
    {
        void TeklifiYazdir()
        {
            if (sonTeklif == null || veri == null) return;
            using var doc = new PrintDocument { DocumentName = "Teklif" };
            doc.PrintPage += (_, e) => { TeklifCiz(e.Graphics!, e.MarginBounds); e.HasMorePages = false; };
            try
            {
                using var dlg = new PrintDialog { Document = doc, UseEXDialog = true, AllowSomePages = false, AllowSelection = false };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                doc.Print();
                Gunluk.Bilgi("Teklif yazdırıldı", $"{sonTeklif.Value.sec.Kod} · {doc.PrinterSettings.PrinterName}");
            }
            catch (InvalidPrinterException ex)
            {
                Gunluk.Uyari("Yazıcı bulunamadı", ex.Message);
                MessageBox.Show("Bu bilgisayarda kullanılabilir bir yazıcı bulunamadı. PDF için Windows'un \"Microsoft Print to PDF\" yazıcısını etkinleştirin.",
                    Marka.Ad, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Teklif sayfasını verilen alana çizer (yazıcı ya da önizleme için).</summary>
        internal void TeklifCiz(Graphics g, Rectangle alan)
        {
            if (sonTeklif == null || veri == null) return;
            var (s, r, ekonomik, paketAdi, yil) = sonTeklif.Value;
            var tr = Ortak.TR;
            Cizim.Hazirla(g);
            using var fBaslik = new Font("Segoe UI Semibold", 16f);
            using var fAra = new Font("Segoe UI Semibold", 11f);
            using var f = new Font("Segoe UI", 9.5f);
            using var fk = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var fKucuk = new Font("Segoe UI", 8f);
            using var yesil = new SolidBrush(Yesil);
            using var metin = new SolidBrush(Cizim.Metin);
            using var soluk = new SolidBrush(Cizim.Soluk);
            using var turuncu = new SolidBrush(Turuncu);
            using var cizgi = new Pen(Color.FromArgb(200, 205, 200));
            float x = alan.Left, y = alan.Top, w = alan.Width;

            float Yaz(string t, Font ft, Brush b, float sol, float genislik, float ek = 2)
            {
                var sz = g.MeasureString(t, ft, (int)genislik);
                g.DrawString(t, ft, b, new RectangleF(sol, y, genislik, sz.Height + 2));
                y += sz.Height + ek;
                return sz.Height;
            }
            void Cizgi(float ek = 8) { y += 2; g.DrawLine(cizgi, x, y, x + w, y); y += ek; }

            // Başlık
            Yaz("Malzeme teklifi ve keşif özeti", fBaslik, yesil, x, w, 0);
            Yaz($"{DateTime.Today.ToString("d MMMM yyyy", tr)}  ·  {projeMetni}", f, soluk, x, w, 4);
            Cizgi();

            // Paket
            Yaz($"{paketAdi}: {s.Yalitim.KisaAd} · {s.Cam.KisaAd} · {s.DuvarAdi}", fAra, metin, x, w, 2);
            string onc = oncelik >= 0 ? OnAyar[oncelik].ad : "Özel ağırlıklar";
            Yaz($"Öncelik: {onc} (maliyet %{r.Agirlik[0] * 100:0}, enerji %{r.Agirlik[1] * 100:0}, karbon %{r.Agirlik[2] * 100:0})  ·  Doğalgaz: BOTAŞ, {TarifeAdi}  ·  Analiz süresi {yil} yıl", f, soluk, x, w, 10);

            // Kalem tablosu
            float[] kol = { x, x + w * 0.46f, x + w * 0.64f, x + w * 0.82f };
            float[] gen = { w * 0.46f, w * 0.18f, w * 0.18f, w * 0.18f };
            using var sag = new StringFormat { Alignment = StringAlignment.Far };
            void Satir(string a, string b, string c, string d, bool kalin = false)
            {
                var ft = kalin ? fk : f;
                float h = g.MeasureString(a, ft, (int)gen[0]).Height;
                g.DrawString(a, ft, metin, new RectangleF(kol[0], y, gen[0], h + 2));
                g.DrawString(b, ft, metin, new RectangleF(kol[1], y, gen[1], h), sag);
                g.DrawString(c, ft, metin, new RectangleF(kol[2], y, gen[2], h), sag);
                g.DrawString(d, ft, metin, new RectangleF(kol[3], y, gen[3], h), sag);
                y += h + 4;
            }
            double kdv = veri.P("KDV");
            Satir("Kalem", "Metraj", "Birim fiyat", "Tutar", true);
            Cizgi(4);
            void Kalem(string ad, Malzeme m, double metraj) =>
                Satir($"{ad} – {m.Ad}", $"{metraj.ToString("N1", tr)} m²", $"{m.FiyatM2.ToString("N2", tr)} TL/m²", TL(metraj * m.FiyatM2));
            if (!r.Mevcut) Kalem("Dış duvar", s.Duvar, r.OpakDuvar);
            Kalem("Mantolama (işçilik dahil)", s.Yalitim, r.OpakDuvar);
            if (s.YanginEkMaliyet > 0) Satir("Yangın yönetmeliği: zeminden 1,5 m hiç yanmaz (A1) şerit, fiyat farkı", $"{r.YanginSeridiM2.ToString("N1", tr)} m²", "", TL(s.YanginEkMaliyet));
            if (r.CamAlani > 0) Kalem("Pencere camı", s.Cam, r.CamAlani);
            Kalem("Çatı yalıtımı", r.Cati, r.CatiAlani);
            Kalem("Zemin yalıtımı", r.Zemin, r.ZeminAlani);
            Cizgi(4);
            Satir("Ara toplam (KDV hariç)", "", "", TL(s.IlkYatirim), true);
            Satir($"KDV %{(kdv * 100).ToString("0", tr)}", "", "", TL(s.IlkYatirim * kdv));
            Satir("Genel toplam (KDV dahil)", "", "", TL(s.IlkYatirim * (1 + kdv)), true);
            Satir("Kullanım alanı başına (KDV hariç)", $"{r.KullanimAlani.ToString("N1", tr)} m²", "", (s.IlkYatirim / r.KullanimAlani).ToString("N0", tr) + " TL/m²");
            y += 10;

            // Enerji ve çevre
            Yaz($"Enerji, maliyet ve karbon ({r.IlAdi} iklimi, TS 825 {r.BolgeNo}. bölge)", fAra, metin, x, w, 4);
            var k = Kpi.Hesapla(s, r, ekonomik, r.EnerjiIskonto, yil);
            string geri = !k.EkYatirim ? "Ek yatırım yok" : !k.Tasarruf ? "Tasarruf yok" : k.GeriOdemeVar && k.GeriOdemeYil <= yil ? $"{k.GeriOdemeYil.ToString("N1", tr)} yıl" : $"{yil} yılda geri dönmüyor";
            Satir("U değeri (W/m²K): duvar · pencere", "", "", $"{s.DuvarU.ToString("0.00", tr)} · {s.PencereU.ToString("0.00", tr)}");
            Satir("U değeri (W/m²K): çatı · zemin", "", "", $"{r.CatiU.ToString("0.00", tr)} · {r.ZeminU.ToString("0.00", tr)}");
            Satir("Yıllık ısıtma ihtiyacı", "", (s.YillikIsitmaKWh / r.KullanimAlani).ToString("N1", tr) + " kWh/m²", s.YillikIsitmaKWh.ToString("N0", tr) + " kWh");
            Satir($"Yıllık ısıtma gideri ({r.YakitAdi})", "", "", TL(s.YillikIsitmaTL));
            if (r.Sogutma) Satir("Yıllık soğutma gideri (klima, elektrik)", "", (s.YillikSogutmaKWh / r.KullanimAlani).ToString("N1", tr) + " kWh/m²", TL(s.YillikSogutmaTL));
            if (r.Mevcut)
            {
                var m = Kpi.MevcutaGore(s, r);
                Satir("Mevcut duruma göre yıllık enerji tasarrufu", "", "", TL(m.tasarrufTL), true);
                Satir("Yatırımın geri dönüşü (iskontolu)", "", "", m.geriDonusYil is double gy ? $"{gy.ToString("N1", tr)} yıl" : "Dönmüyor");
                Satir($"Net bugünkü değer ({yil} yıl)", "", "", TL(m.netBugunkuDeger));
            }
            Satir($"Ömür boyu maliyet ({yil} yıl, bugünkü değer)", "", "", TL(s.ToplamMaliyet), true);
            if (!r.Mevcut) Satir("Ekonomik pakete göre ek yatırımın geri dönüşü (iskontolu)", "", "", geri);
            Satir("Dış duvar ve yalıtım ağırlığı (bilgi)", "", "", s.DuvarAgirlikTon.ToString("N0", tr) + " t");
            Satir($"{yil} yıllık toplam CO₂ (malzeme + ısıtma)", "", "", (s.ToplamCO2Kg / 1000).ToString("N1", tr) + " t");
            y += 6;
            Yaz(Ts825Metni(s, r, out bool tsUygun).Replace("\n", " — "), fk, tsUygun ? yesil : turuncu, x, w, s.YanginNotu.Length > 0 ? 4 : 12);
            if (s.YanginNotu.Length > 0) Yaz(s.YanginNotu, f, turuncu, x, w, 12);

            // Kapsam ve kaynaklar
            Yaz("Kapsam ve kaynaklar", fAra, metin, x, w, 4);
            Yaz("Mantolama fiyatı uygulanmış sistem fiyatıdır (levha, yapıştırıcı, dübel, file, sıva ve işçilik dahil). Duvar, cam, çatı ve zemin kalemleri malzeme fiyatıdır; işçilik ve nakliye dahil değildir. " +
                $"Isıtma ihtiyacı TS 825 derece-gün yöntemiyle {r.IlAdi} için ({r.DG.ToString("N0", tr)} derece-gün, MGM) hesaplanmıştır; ömür boyu maliyet reel iskonto oranı %{(r.Iskonto * 100).ToString("0.0", tr)} ile bugünkü değerdir.", f, metin, x, w, 6);
            foreach (var m in new[] { s.Duvar, s.Yalitim, s.Cam, r.Cati, r.Zemin }.Where(m => m != s.Cam || r.CamAlani > 0))
                Yaz($"• {m.Ad}: {m.FiyatKaynagi} ({m.FiyatTarihi})" + (m.FiyatHesaplanan ? $". {m.FiyatHesabi}" : ""), fKucuk, soluk, x, w, 1);
            Yaz($"• Isıtma: {r.YakitAdi}. Doğalgaz: BOTAŞ tarifesi, Nisan 2026 ({TarifeAdi}); elektrik: EPDK, Nisan 2026. Karbon: ICE v4.1, IPCC 2006, ETKB şebeke emisyon faktörü. İklim: MGM derece-gün. Yangın: Binaların Yangından Korunması Hakkında Yönetmelik Md. 27.", fKucuk, soluk, x, w, 1);

            // Alt bilgi
            g.DrawString($"{Marka.Ad}  ·  Fiyatlar belge tarihindeki veri dosyasına göredir.", fKucuk, soluk, x, alan.Bottom - 14);
        }
    }
}
