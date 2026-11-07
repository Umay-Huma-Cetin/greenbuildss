using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core
{
    /// <summary>Önerinin gerekçesi ve teklifin Excel dökümü (arayüzden bağımsız, test edilebilir).</summary>
    public static class TeklifRaporu
    {
        public static string PaketAdi(Secenek s) => $"{s.Yalitim.KisaAd} · {s.Cam.KisaAd} · {s.DuvarAdi}";

        static string Tl(double v) => Math.Abs(v).ToString("N0", Ortak.TR) + " TL";

        /// <summary>
        /// "Neden bu paket?" — seçilen paketin hangi ölçütle seçildiğini ve en yakın rakibine göre farkını tek cümlede söyler.
        /// liste: puanlanan seçenekler (puana göre sıralı); oneri = liste[0].
        /// </summary>
        public static string Gerekce(Secenek s, IReadOnlyList<Secenek> liste, Sonuc r, Secenek oneri, Secenek ekonomik, Secenek premium, string oncelikAdi)
        {
            var tr = Ortak.TR;
            bool Ayni(Secenek a, Secenek b) => a.Kod == b.Kod;
            if (Ayni(s, oneri))
            {
                if (liste.Count < 2) return "Uygun tek seçenek bu.";
                var b = liste[1];
                double dQ = b.YillikIsitmaKWh - s.YillikIsitmaKWh, dL = b.ToplamMaliyet - s.ToplamMaliyet;
                string q = Math.Abs(dQ) < 0.5 ? "" : $"ısıtması {Math.Abs(dQ).ToString("N0", tr)} kWh {(dQ > 0 ? "az" : "fazla")}";
                string l = Math.Abs(dL) < 0.5 ? "" : $"ömür boyu maliyeti {Tl(dL)} {(dL > 0 ? "düşük" : "yüksek")}";
                string fark = string.Join(", ", new[] { q, l }.Where(x => x.Length > 0));
                return $"{oncelikAdi} önceliğinde {liste.Count} uygun seçenek arasında en yüksek puan." +
                       (fark.Length > 0 ? $" İkinci sıradakine ({PaketAdi(b)}) göre {fark}." : "");
            }
            if (Ayni(s, ekonomik))
                return $"İlk yatırımı en düşük uygun paket: önerilene göre {Tl(oneri.IlkYatirim - s.IlkYatirim)} ucuz, yıllık ısıtma gideri {Tl(s.YillikGiderTL - oneri.YillikGiderTL)} {(s.YillikGiderTL >= oneri.YillikGiderTL ? "fazla" : "az")}.";
            if (Ayni(s, premium))
                return $"Isıtması en düşük uygun paket: önerilene göre {Math.Abs(s.YillikIsitmaKWh - oneri.YillikIsitmaKWh).ToString("N0", tr)} kWh az, ilk yatırımı {Tl(s.IlkYatirim - oneri.IlkYatirim)} {(s.IlkYatirim >= oneri.IlkYatirim ? "fazla" : "az")}.";
            int sira = liste.ToList().FindIndex(x => Ayni(x, s)) + 1;
            return sira > 0 ? $"Sizin seçiminiz: {sira}. sırada; ömür boyu maliyeti önerilenden {Tl(s.ToplamMaliyet - oneri.ToplamMaliyet)} {(s.ToplamMaliyet >= oneri.ToplamMaliyet ? "yüksek" : "düşük")}."
                            : "Sizin seçiminiz TS 825 sınırlarını sağlamıyor.";
        }

        public sealed class Bilgi
        {
            public string Proje = "", Paket = "", Oncelik = "", Tarife = "", Gerekce = "", Ts825 = "";
            public int Yil;
            public DateTime Tarih = DateTime.Today;
        }

        /// <summary>Excel çalışma kitabı: Teklif (keşif ve sonuçlar), Tüm seçenekler, Kaynaklar.</summary>
        public static List<XlsxSayfa> Excel(VeriDeposu v, Secenek s, Sonuc r, Secenek ekonomik, Bilgi b, IReadOnlyList<(Secenek s, double? puan)> tum)
        {
            double kdv = v.P("KDV");
            var t = new XlsxSayfa { Ad = "Teklif", Genislik = new double[] { 46, 44, 14, 18, 18 } };
            t.Kalin("Malzeme teklifi ve keşif özeti");
            t.Satir("Tarih", b.Tarih.ToString("d MMMM yyyy", Ortak.TR));
            t.Satir("Proje", b.Proje);
            t.Satir("Paket", $"{b.Paket}: {PaketAdi(s)}");
            t.Satir("Öncelik", $"{b.Oncelik} (maliyet %{r.Agirlik[0] * 100:0}, enerji %{r.Agirlik[1] * 100:0}, karbon %{r.Agirlik[2] * 100:0})");
            t.Satir("Neden bu paket?", b.Gerekce);
            t.Bos();
            t.Kalin("Kalem", "Malzeme", "Metraj (m²)", "Birim fiyat (TL/m²)", "Tutar (TL)");
            void Kalem(string ad, Malzeme m, double metraj) => t.Satir(ad, m.Ad, Math.Round(metraj, 2), m.FiyatM2, Math.Round(metraj * m.FiyatM2, 2));
            if (!r.Mevcut) Kalem("Dış duvar", s.Duvar, r.OpakDuvar);
            Kalem("Mantolama (işçilik dahil)", s.Yalitim, r.OpakDuvar);
            if (s.YanginEkMaliyet > 0) t.Satir("Yangın yönetmeliği şeridi (zeminden 1,5 m A1, fiyat farkı)", "Md. 27/2-b", Math.Round(r.YanginSeridiM2, 2), null, Math.Round(s.YanginEkMaliyet, 2));
            if (r.CamAlani > 0) Kalem("Pencere camı", s.Cam, r.CamAlani);
            Kalem("Çatı yalıtımı", r.Cati, r.CatiAlani);
            Kalem("Zemin yalıtımı", r.Zemin, r.ZeminAlani);
            t.Kalin("Ara toplam (KDV hariç)", null, null, null, Math.Round(s.IlkYatirim, 2));
            t.Satir($"KDV %{kdv * 100:0}", null, null, null, Math.Round(s.IlkYatirim * kdv, 2));
            t.Kalin("Genel toplam (KDV dahil)", null, null, null, Math.Round(s.IlkYatirim * (1 + kdv), 2));
            t.Satir("Kullanım alanı başına (TL/m², KDV hariç)", null, Math.Round(r.KullanimAlani, 2), null, Math.Round(s.IlkYatirim / r.KullanimAlani, 2));
            t.Bos();
            t.Kalin($"Enerji, maliyet ve karbon ({r.IlAdi}, {r.DG.ToString("N0", Ortak.TR)} derece-gün, TS 825 {r.BolgeNo}. bölge)", null, null, null, "Değer");
            var k = Kpi.Hesapla(s, r, ekonomik, r.EnerjiIskonto, b.Yil);
            t.Satir("Duvar U değeri (W/m²K)", $"TS 825 sınırı {r.SinirDuvar.ToString("0.00", Ortak.TR)}", null, null, Math.Round(s.DuvarU, 3));
            if (r.CamAlani > 0) t.Satir("Pencere U değeri (W/m²K, cam + doğrama)", $"{r.Dograma}; TS 825 sınırı {r.SinirPencere.ToString("0.0", Ortak.TR)}", null, null, s.PencereU);
            t.Satir("Yıllık ısıtma ihtiyacı (kWh)", null, null, null, Math.Round(s.YillikIsitmaKWh, 0));
            t.Satir("Yıllık ısıtma ihtiyacı (kWh/m²)", null, null, null, Math.Round(s.YillikIsitmaKWh / r.KullanimAlani, 2));
            t.Satir($"Yıllık ısıtma gideri (TL, {r.YakitAdi})", b.Tarife, null, null, Math.Round(s.YillikIsitmaTL, 2));
            if (r.Sogutma)
            {
                t.Satir("Yıllık soğutma ihtiyacı (kWh)", $"soğutma derece-gün {r.SogutmaDG.ToString("N0", Ortak.TR)}", null, null, Math.Round(s.YillikSogutmaKWh, 0));
                t.Satir("Yıllık soğutma gideri (TL, klima elektriği)", $"SEER {r.Seer.ToString("0.0", Ortak.TR)}", null, null, Math.Round(s.YillikSogutmaTL, 2));
            }
            if (r.Mevcut)
            {
                var m = Kpi.MevcutaGore(s, r);
                t.Kalin("Mevcut duruma göre yıllık enerji tasarrufu (TL)", "yalıtımsız mevcut binaya göre", null, null, Math.Round(m.tasarrufTL, 2));
                t.Satir("Yatırımın geri dönüşü (iskontolu)", null, null, null, m.geriDonusYil is double gy ? $"{gy.ToString("N1", Ortak.TR)} yıl" : "Dönmüyor");
                t.Satir($"Net bugünkü değer (TL, {b.Yil} yıl)", null, null, null, Math.Round(m.netBugunkuDeger, 2));
            }
            t.Satir("Dış duvar ve yalıtım ağırlığı (t, bilgi)", null, null, null, Math.Round(s.DuvarAgirlikTon, 1));
            if (s.YanginNotu.Length > 0) t.Satir("Yangın yönetmeliği", s.YanginNotu);
            t.Kalin($"Ömür boyu maliyet (TL, {b.Yil} yıl, bugünkü değer)", $"reel iskonto %{(r.Iskonto * 100).ToString("0.0", Ortak.TR)}", null, null, Math.Round(s.ToplamMaliyet, 2));
            if (!r.Mevcut) t.Satir("Ek yatırımın geri dönüşü (iskontolu)", "ekonomik pakete göre", null, null,
                !k.EkYatirim ? "Ek yatırım yok" : !k.Tasarruf ? "Tasarruf yok" : k.GeriOdemeVar && k.GeriOdemeYil <= b.Yil ? $"{k.GeriOdemeYil.ToString("N1", Ortak.TR)} yıl" : $"{b.Yil} yılda geri dönmüyor");
            t.Satir($"{b.Yil} yıllık CO₂ (t, malzeme + ısıtma)", null, null, null, Math.Round(s.ToplamCO2Kg / 1000, 2));
            t.Satir("TS 825:2024", b.Ts825.Replace("\n", " — "));

            var o = new XlsxSayfa { Ad = "Tüm seçenekler", Genislik = new double[] { 6, 22, 28, 22, 10, 12, 16, 16, 16, 14, 18, 12, 10 } };
            o.Kalin("Sıra", "Yalıtım", "Cam", "Duvar", "Duvar U", "TS 825", "İlk yatırım (TL)", "Yıllık ısıtma (kWh)", "Yıllık enerji gideri (TL)", "Duvar ağırlığı (t)", "Ömür boyu maliyet (TL)", "CO₂ (t)", "Puan");
            int i = 0;
            foreach (var (x, p) in tum)
                o.Satir(++i, x.Yalitim.KisaAd, x.Cam.KisaAd, x.DuvarAdi, Math.Round(x.DuvarU, 3), x.Ts825Uygun ? "Uygun" : "Uygun değil",
                        Math.Round(x.IlkYatirim, 2), Math.Round(x.YillikIsitmaKWh, 0), Math.Round(x.YillikGiderTL, 2), Math.Round(x.DuvarAgirlikTon, 1), Math.Round(x.ToplamMaliyet, 2), Math.Round(x.ToplamCO2Kg / 1000, 2),
                        p is double pv ? (object)Math.Round(pv * 100, 1) : "Puanlanmaz");

            var kay = new XlsxSayfa { Ad = "Kaynaklar", Genislik = new double[] { 12, 48, 14, 16, 70, 70, 14 } };
            kay.Kalin("Malzeme", "Ad", "Fiyat (TL/m²)", "Tarih", "Fiyat kaynağı", "Fiyat hesabı", "Fiyat yaşı (ay)");
            foreach (var m in (r.Mevcut ? new[] { s.Yalitim, s.Cam, r.Cati, r.Zemin } : new[] { s.Duvar, s.Yalitim, s.Cam, r.Cati, r.Zemin }))
                kay.Satir(m.Id, m.Ad, m.FiyatM2, m.FiyatTarihi, m.FiyatKaynagi, m.FiyatHesabi, Ortak.FiyatYasiAy(m.FiyatTarihi, b.Tarih) is int ay ? ay : "Tarih okunamadı");
            if (v.Yakitlar.FirstOrDefault(y => y.Id == r.YakitId) is Yakit yk)
            {
                kay.Bos();
                kay.Kalin("Yakıt", "Ad", "Fiyat (TL/birim)", "Tarih", "Fiyat kaynağı", "Fiyat hesabı");
                kay.Satir(yk.Id, $"{yk.Ad} · {yk.Isitici}", yk.FiyatBirim, yk.FiyatTarihi, yk.FiyatKaynagi, yk.FiyatHesabi);
                kay.Satir("", "Isıl değer (kWh/birim)", yk.IsilDegerKWh, null, yk.IsilDegerKaynagi);
                kay.Satir("", "Verim", yk.Verim, null, yk.VerimKaynagi);
                kay.Satir("", "Emisyon (kgCO₂/kWh)", yk.EmisyonKgKWh, null, yk.EmisyonKaynagi);
            }
            if (r.CamAlani > 0)
            {
                kay.Bos();
                kay.Kalin("Pencere", "Cam + doğrama", "U (W/m²K)", null, "Kaynak", "Hangi değer");
                kay.Satir("Pencere U", $"{s.Cam.KisaAd} + {r.Dograma}", s.PencereU, null,
                          v.PencereDegerleri.FirstOrDefault()?.Kaynak ?? "TS 825:2024 Ek A.3",
                          s.Cam.AraBoslukMm is int a ? $"Ek A.3, {a} mm ara boşluk satırı" : "Ek A.3; ara boşluk kaynakta yazmadığından 12 ve 16 mm değerlerinin büyüğü");
            }
            kay.Bos();
            kay.Kalin("Parametre", "Ad", "Değer", "Birim", "Kaynak");
            foreach (var kv in v.Parametre.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                string id = kv.Key; double deger = kv.Value;
                var pb = v.ParametreBilgi.TryGetValue(id, out var bb) ? bb : (id, "", "");
                kay.Satir(id, pb.Item1, deger, pb.Item2, pb.Item3);
            }
            return new List<XlsxSayfa> { t, o, kay };
        }
    }
}
