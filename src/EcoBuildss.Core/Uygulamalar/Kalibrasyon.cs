using System;
using System.Collections.Generic;
using System.Linq;
using EcoBuildss.Core.Ogrenme;
using EcoBuildss.Core.Veri;

namespace EcoBuildss.Core.Uygulamalar
{
    /// <summary>Bir tüketim kaydı için modelin tahmini ve gerçek tüketim.</summary>
    public sealed class KalibrasyonNoktasi
    {
        public string Id = "", Il = "";
        public double Alan, ModelYakitKWh, GercekKWh, SicakSuAlan, DG;
        public double KalibreTahmin, CaprazTahmin;   // tüm veriyle ve kayıt eğitimde yokken (çapraz doğrulama) tahmin
        public double Cook;
    }

    public sealed class KalibrasyonSonucu
    {
        public List<KalibrasyonNoktasi> Noktalar = new();
        public RegresyonSonucu Regresyon = null!;
        /// <summary>Isıtma katsayısı β₁: gerçek ısıtma tüketimi / model ısıtma tahmini. Teklif hesabına çarpan olarak uygulanır.</summary>
        public double IsitmaKatsayisi => Regresyon.Katsayi[1];
        public double IsitmaKatsayisiAlt => Regresyon.GA95Alt[1];
        public double IsitmaKatsayisiUst => Regresyon.GA95Ust[1];
        public double SicakSuKWhM2 => Regresyon.Katsayi.Length > 2 ? Regresyon.Katsayi[2] : 0;
        public double HamCvRmse, HamNmbe;          // kalibrasyonsuz model (çarpan 1) — gerçeğe göre hata
        public double CvRmse, Nmbe;                // kalibre model, kayıt eğitimde görülmeden (5 katlı çapraz doğrulama)
        public bool Iyilestirdi => CvRmse < HamCvRmse;
        /// <summary>ASHRAE Guideline 14'ün aylık kalibrasyon için verdiği sınırlar: CV(RMSE) ≤ %15, |NMBE| ≤ %5.</summary>
        public bool Ashrae14Uygun => CvRmse <= 0.15 && Math.Abs(Nmbe) <= 0.05;
        /// <summary>Kalibrasyon katsayısı teklife ancak çapraz doğrulamada hatayı düşürüyorsa ve katsayı anlamlıysa uygulanır.</summary>
        public bool Kullanilabilir => Iyilestirdi && IsitmaKatsayisiAlt > 0 && Noktalar.Count >= MinKayit;
        public const int MinKayit = 20;
        public string Yorum
        {
            get
            {
                if (Noktalar.Count < MinKayit) return $"En az {MinKayit} kayıt gerekir; şu an {Noktalar.Count}.";
                if (!Iyilestirdi) return "Kalibrasyon, görülmemiş kayıtlarda hatayı düşürmedi; teklifte model olduğu gibi kullanılır.";
                return $"Model gerçek tüketime göre ×{IsitmaKatsayisi:0.00} düzeltildi; görülmemiş kayıtlarda hata %{100 * HamCvRmse:0.0} → %{100 * CvRmse:0.0}."
                     + (Ashrae14Uygun ? " ASHRAE 14 sınırları içinde." : " ASHRAE 14 sınırının (%15) üstünde: binalar arası kullanım farkı büyük.");
            }
        }
    }

    /// <summary>
    /// Enerji modelinin gerçek fatura verisiyle kalibrasyonu (çoklu doğrusal regresyon):
    ///   gerçek yakıt kWh = β₀ + β₁ · (model ısıtma yakıtı kWh) + β₂ · (kullanım alanı × sıcak su dahil)
    /// β₁ ısıtma modelinin sistematik sapmasını, β₂ sıcak su ve pişirmenin alan başına baz yükünü verir.
    /// Model ısıtma yakıtı TS 825 derece-gün yöntemiyle, kaydın kendi U değerleri ve alanlarıyla hesaplanır.
    /// </summary>
    public static class Kalibrasyon
    {
        public static double ModelYakit(VeriDeposu v, Kayit k, out double dg)
        {
            var il = v.Iller.First(i => i.Ad == k.Metin("il"));
            dg = k.SayiVeyaBos("isitma_derece_gun") ?? il.IsitmaDG;
            double h = k.Sayi("duvar_U") * k.Sayi("dis_duvar_alani_m2") + k.Sayi("pencere_U") * k.Sayi("pencere_alani_m2")
                     + v.P("F1") * k.Sayi("cati_U") * k.Sayi("cati_alani_m2") + v.P("F2") * k.Sayi("zemin_U") * k.Sayi("zemin_alani_m2")
                     + v.P("V3") * v.P("V1") * v.P("V2") * k.Sayi("hacim_m3");
            double q = EnerjiModeli.YillikIsitma(dg, h);
            return q / Verim(v, k.Metin("yakit"));
        }

        public static double Verim(VeriDeposu v, string yakit) =>
            yakit == "DG" ? v.P("H1") : v.Yakitlar.First(y => y.Id == yakit).Verim;

        public static KalibrasyonSonucu Calistir(VeriDeposu v, IReadOnlyList<Kayit> kayitlar, int tohum = 42)
        {
            if (kayitlar.Count < 5) throw new InvalidOperationException($"Kalibrasyon için en az {KalibrasyonSonucu.MinKayit} kayıt gerekir; şu an {kayitlar.Count}.");
            var s = new KalibrasyonSonucu();
            foreach (var k in kayitlar)
            {
                double m = ModelYakit(v, k, out double dg);
                bool su = k.Metin("sicak_su_dahil") != "Hayır";
                s.Noktalar.Add(new KalibrasyonNoktasi { Id = k.Metin("kayit_id"), Il = k.Metin("il"), Alan = k.Sayi("kullanim_alani_m2"), DG = dg,
                                                        ModelYakitKWh = m, GercekKWh = k.Sayi("yillik_tuketim_kWh"), SicakSuAlan = su ? k.Sayi("kullanim_alani_m2") : 0 });
            }
            bool suVar = s.Noktalar.Any(p => p.SicakSuAlan > 0);
            double[] X(KalibrasyonNoktasi p) => suVar ? new[] { p.ModelYakitKWh, p.SicakSuAlan } : new[] { p.ModelYakitKWh };
            var adlar = suVar ? new[] { "Model ısıtma yakıtı (kWh)", "Alan × sıcak su (m²)" } : new[] { "Model ısıtma yakıtı (kWh)" };
            var y = s.Noktalar.Select(p => p.GercekKWh).ToArray();

            s.Regresyon = DogrusalRegresyon.Ekk(s.Noktalar.Select(X).ToArray(), y, adlar);
            for (int i = 0; i < s.Noktalar.Count; i++) { s.Noktalar[i].KalibreTahmin = s.Regresyon.Tahmin[i]; s.Noktalar[i].Cook = s.Regresyon.Cook[i]; }

            // Görülmemiş veride başarı: 5 katlı çapraz doğrulama (her kayıt, kendisinin olmadığı veriyle kurulan modelle tahmin edilir)
            int katSayisi = Math.Min(5, s.Noktalar.Count);
            foreach (var test in Katlar.Basit(s.Noktalar.Count, katSayisi, tohum))
            {
                var egitim = Enumerable.Range(0, s.Noktalar.Count).Except(test).ToArray();
                var r = DogrusalRegresyon.Ekk(egitim.Select(i => X(s.Noktalar[i])).ToArray(), egitim.Select(i => y[i]).ToArray(), adlar);
                foreach (int i in test) s.Noktalar[i].CaprazTahmin = r.Tahminle(X(s.Noktalar[i]));
            }
            var cv = s.Noktalar.Select(p => p.CaprazTahmin).ToArray();
            (s.CvRmse, s.Nmbe) = Metrik.Ashrae14(y, cv, s.Regresyon.P);
            // Kalibrasyonsuz model: ısıtma tahmini olduğu gibi; sıcak su için TS 825'te değer olmadığından baz yük yok sayılmaz,
            // adil karşılaştırma için baz yük de aynı katlarda eğitim verisinden ortalama kWh/m² olarak eklenir.
            var ham = new double[y.Length];
            foreach (var test in Katlar.Basit(s.Noktalar.Count, katSayisi, tohum))
            {
                var egitim = Enumerable.Range(0, s.Noktalar.Count).Except(test).ToArray();
                double alanTop = egitim.Sum(i => s.Noktalar[i].SicakSuAlan);
                double baz = alanTop > 0 ? egitim.Sum(i => y[i] - s.Noktalar[i].ModelYakitKWh) / alanTop : 0;
                foreach (int i in test) ham[i] = s.Noktalar[i].ModelYakitKWh + Math.Max(0, baz) * s.Noktalar[i].SicakSuAlan;
            }
            (s.HamCvRmse, s.HamNmbe) = Metrik.Ashrae14(y, ham, 0);
            return s;
        }
    }
}
