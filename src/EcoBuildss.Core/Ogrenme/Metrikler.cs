using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core.Ogrenme
{
    /// <summary>İkili sınıflandırma başarı ölçüleri. Pozitif sınıf = 1 (ör. teklif kabul edildi).</summary>
    public sealed class SiniflandirmaMetrikleri
    {
        public int DP, YP, DN, YN;   // doğru pozitif, yanlış pozitif, doğru negatif, yanlış negatif
        public double Auc = double.NaN;
        public int N => DP + YP + DN + YN;
        public double Dogruluk => N == 0 ? 0 : (double)(DP + DN) / N;
        public double Kesinlik => DP + YP == 0 ? 0 : (double)DP / (DP + YP);        // precision
        public double Duyarlilik => DP + YN == 0 ? 0 : (double)DP / (DP + YN);      // recall
        public double F1 => Kesinlik + Duyarlilik == 0 ? 0 : 2 * Kesinlik * Duyarlilik / (Kesinlik + Duyarlilik);
        /// <summary>Negatif sınıfın F1'i (makro F1 için).</summary>
        public double F1Negatif
        {
            get
            {
                double k = DN + YN == 0 ? 0 : (double)DN / (DN + YN), d = DN + YP == 0 ? 0 : (double)DN / (DN + YP);
                return k + d == 0 ? 0 : 2 * k * d / (k + d);
            }
        }
        public double MakroF1 => (F1 + F1Negatif) / 2;

        public static SiniflandirmaMetrikleri Hesapla(IReadOnlyList<int> gercek, IReadOnlyList<double> olasilik, double esik = 0.5)
        {
            var m = new SiniflandirmaMetrikleri();
            for (int i = 0; i < gercek.Count; i++)
            {
                bool t = olasilik[i] >= esik;
                if (gercek[i] == 1) { if (t) m.DP++; else m.YN++; }
                else { if (t) m.YP++; else m.DN++; }
            }
            m.Auc = Metrik.RocAuc(gercek, olasilik);
            return m;
        }
    }

    public static class Metrik
    {
        /// <summary>ROC eğrisi altındaki alan (Mann–Whitney U; eşit puanlara yarım puan).</summary>
        public static double RocAuc(IReadOnlyList<int> y, IReadOnlyList<double> p)
        {
            var idx = Enumerable.Range(0, y.Count).OrderBy(i => p[i]).ToArray();
            var sira = new double[y.Count];
            for (int i = 0; i < idx.Length;)
            {
                int j = i; while (j + 1 < idx.Length && p[idx[j + 1]] == p[idx[i]]) j++;
                double ort = (i + j) / 2.0 + 1;
                for (int k = i; k <= j; k++) sira[idx[k]] = ort;
                i = j + 1;
            }
            int npos = y.Count(v => v == 1), nneg = y.Count - npos;
            if (npos == 0 || nneg == 0) return double.NaN;
            double toplam = 0; for (int i = 0; i < y.Count; i++) if (y[i] == 1) toplam += sira[i];
            return (toplam - npos * (npos + 1) / 2.0) / ((double)npos * nneg);
        }

        public static double Rmse(IReadOnlyList<double> y, IReadOnlyList<double> t) => Math.Sqrt(Enumerable.Range(0, y.Count).Average(i => Math.Pow(y[i] - t[i], 2)));
        public static double Mae(IReadOnlyList<double> y, IReadOnlyList<double> t) => Enumerable.Range(0, y.Count).Average(i => Math.Abs(y[i] - t[i]));

        public static double R2(IReadOnlyList<double> y, IReadOnlyList<double> t)
        {
            double m = y.Average(), ssr = 0, sst = 0;
            for (int i = 0; i < y.Count; i++) { ssr += Math.Pow(y[i] - t[i], 2); sst += Math.Pow(y[i] - m, 2); }
            return sst == 0 ? double.NaN : 1 - ssr / sst;
        }

        /// <summary>
        /// ASHRAE Guideline 14 kalibrasyon ölçüleri: CV(RMSE) = RMSE / ȳ ve NMBE = Σ(y − ŷ) / (n · ȳ).
        /// p = modeldeki parametre sayısı (serbestlik derecesi düzeltmesi).
        /// </summary>
        public static (double cvRmse, double nmbe) Ashrae14(IReadOnlyList<double> y, IReadOnlyList<double> t, int p)
        {
            int n = y.Count; double m = y.Average(), ss = 0, s = 0;
            for (int i = 0; i < n; i++) { ss += Math.Pow(y[i] - t[i], 2); s += y[i] - t[i]; }
            int sd = Math.Max(1, n - p);
            return (Math.Sqrt(ss / sd) / m, s / (sd * m));
        }
    }

    /// <summary>Çapraz doğrulama kat bölümleri. Tabakalı: her katta sınıf oranı aynı kalır.</summary>
    public static class Katlar
    {
        public static List<int[]> Tabakali(IReadOnlyList<int> y, int k, int tohum)
        {
            var r = new Random(tohum);
            var katlar = Enumerable.Range(0, k).Select(_ => new List<int>()).ToList();
            foreach (var sinif in y.Distinct().OrderBy(v => v))
            {
                var idx = Enumerable.Range(0, y.Count).Where(i => y[i] == sinif).ToList();
                Rastgele.Karistir(idx, r);
                for (int i = 0; i < idx.Count; i++) katlar[i % k].Add(idx[i]);
            }
            return katlar.Select(l => l.OrderBy(v => v).ToArray()).ToList();
        }

        public static List<int[]> Basit(int n, int k, int tohum)
        {
            var idx = Enumerable.Range(0, n).ToList();
            Rastgele.Karistir(idx, new Random(tohum));
            var katlar = Enumerable.Range(0, k).Select(_ => new List<int>()).ToList();
            for (int i = 0; i < n; i++) katlar[i % k].Add(idx[i]);
            return katlar.Select(l => l.OrderBy(v => v).ToArray()).ToList();
        }

        /// <summary>Tabakalı eğitim / test ayrımı (test oranı kadar kayıt hiç eğitimde görülmez).</summary>
        public static (int[] egitim, int[] test) EgitimTest(IReadOnlyList<int> y, double testOrani, int tohum)
        {
            var r = new Random(tohum); var tr = new List<int>(); var te = new List<int>();
            foreach (var sinif in y.Distinct().OrderBy(v => v))
            {
                var idx = Enumerable.Range(0, y.Count).Where(i => y[i] == sinif).ToList();
                Rastgele.Karistir(idx, r);
                int nt = (int)Math.Round(idx.Count * testOrani);
                te.AddRange(idx.Take(nt)); tr.AddRange(idx.Skip(nt));
            }
            return (tr.OrderBy(v => v).ToArray(), te.OrderBy(v => v).ToArray());
        }
    }
}
