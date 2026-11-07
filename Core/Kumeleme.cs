using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core
{
    /// <summary>MGM derece-gün tablosundaki bir il (ya da istasyon).</summary>
    public sealed class Il
    {
        public string Ad = "";
        public double IsitmaDG, SogutmaDG;   // yıllık ısıtma (18/15 °C) ve soğutma (22 °C) derece-gün
        public int? Ts825Bolge;              // il merkezinin TS 825:2024 bölgesi (il_bolgeleri.csv; yalnız kaynağı olan iller teklifte seçilebilir)
        public string BolgeKaynagi = "";
        public override string ToString() => Ad;
    }

    /// <summary>TS 825:2024 iklim bölgesinin U değeri sınırları (W/m²K).</summary>
    public sealed class Ts825Bolge
    {
        public int No;
        public double UDuvar, UCati, UZemin, UPencere;
        public override string ToString() => $"{No}. bölge";
    }

    public sealed class Kume
    {
        public int No;                                       // 1 = en ılık küme (ortalama ısıtma derece-günü en düşük)
        public List<Il> Uyeler = new();
        public double OrtIsitma, OrtSogutma;                 // küme merkezi, gerçek birimde
        public string Ad => $"Küme {No}";
    }

    public sealed class KumelemeSonucu
    {
        public int K;
        public List<Kume> Kumeler = new();
        public Dictionary<int, double> SiluetK = new();      // denenen her k için ortalama siluet katsayısı (yüksek = iyi)
        public Dictionary<int, double> DaviesBouldinK = new();   // denenen her k için Davies–Bouldin endeksi (düşük = iyi)
        public double Siluet, DaviesBouldin;                 // seçilen k'nin siluet katsayısı ve Davies–Bouldin endeksi
        /// <summary>k ≥ 3 arasında siluet katsayısı en yüksek k (k = 2 yalnız iki gruba ayırır; ikinci ölçüt olarak raporlanır).</summary>
        public int SiluetEnIyiK3 => SiluetK.Where(x => x.Key >= 3).OrderByDescending(x => x.Value).Select(x => x.Key).DefaultIfEmpty(K).First();
        public double Wcss;                                  // küme içi kareler toplamı (standart birimde)
        public Kume KumesiNe(Il il) => Kumeler.First(k => k.Uyeler.Contains(il));
    }

    /// <summary>
    /// İllerin iklime göre kümelenmesi: özellikler yıllık ısıtma ve soğutma derece-günü (z-skoru ile standartlaştırılır).
    /// k-ortalamalar (k-means++ başlatma, sabit tohum, 200 tekrar). Küme sayısı veriden seçilir: k = 2–8 arasında
    /// Davies–Bouldin endeksi (Davies ve Bouldin 1979) en düşük olan k; siluet katsayısı (Rousseeuw 1987) ikinci ölçüt olarak raporlanır.
    /// </summary>
    public static class Kumeleme
    {
        public static KumelemeSonucu Yap(IReadOnlyList<Il> iller, int? sabitK = null, int kMin = 2, int kMax = 8, int tohum = 42)
        {
            if (iller.Count < 3) throw new InvalidOperationException("Kümeleme için en az 3 il gerekir.");
            var X = Standart(iller);
            var sonuc = new KumelemeSonucu();
            int[]? enIyiEtiket = null; double enIyiDb = double.PositiveInfinity; int enIyiK = 0; double enIyiW = 0;
            int ust = Math.Min(kMax, iller.Count - 1);
            for (int k = sabitK ?? kMin; k <= (sabitK ?? ust); k++)
            {
                var (etiket, w) = KMeans(X, k, tohum);
                sonuc.SiluetK[k] = Siluet(X, etiket, k);
                double db = sonuc.DaviesBouldinK[k] = DaviesBouldinEndeksi(X, etiket, k);
                if (db < enIyiDb - 1e-12) { enIyiDb = db; enIyiEtiket = etiket; enIyiK = k; enIyiW = w; }
            }
            sonuc.K = enIyiK; sonuc.Siluet = sonuc.SiluetK[enIyiK]; sonuc.DaviesBouldin = enIyiDb; sonuc.Wcss = enIyiW;
            // kümeleri ortalama ısıtma derece-gününe göre sırala (1 = en ılık)
            var gruplar = Enumerable.Range(0, enIyiK).Select(c => Enumerable.Range(0, iller.Count).Where(i => enIyiEtiket![i] == c).Select(i => iller[i]).ToList())
                .Where(g => g.Count > 0).OrderBy(g => g.Average(i => i.IsitmaDG)).ToList();
            for (int c = 0; c < gruplar.Count; c++)
                sonuc.Kumeler.Add(new Kume { No = c + 1, Uyeler = gruplar[c].OrderBy(i => i.IsitmaDG).ToList(), OrtIsitma = gruplar[c].Average(i => i.IsitmaDG), OrtSogutma = gruplar[c].Average(i => i.SogutmaDG) });
            return sonuc;
        }

        public static double[][] Standart(IReadOnlyList<Il> iller)
        {
            double[] Z(Func<Il, double> f)
            {
                var v = iller.Select(f).ToArray(); double m = v.Average(), s = Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / v.Length);
                return v.Select(x => s == 0 ? 0 : (x - m) / s).ToArray();
            }
            var a = Z(i => i.IsitmaDG); var b = Z(i => i.SogutmaDG);
            return Enumerable.Range(0, iller.Count).Select(i => new[] { a[i], b[i] }).ToArray();
        }

        static double D2(double[] a, double[] b) { double s = 0; for (int j = 0; j < a.Length; j++) s += (a[j] - b[j]) * (a[j] - b[j]); return s; }

        /// <summary>k-ortalamalar: k-means++ başlatma, 200 farklı başlangıç, en küçük WCSS'li çözüm.</summary>
        public static (int[] etiket, double wcss) KMeans(double[][] X, int k, int tohum, int tekrar = 200, int maxIter = 300)
        {
            var rnd = new Random(tohum);
            int n = X.Length, d = X[0].Length;
            int[]? enIyi = null; double enIyiW = double.PositiveInfinity;
            for (int t = 0; t < tekrar; t++)
            {
                // k-means++
                var C = new List<double[]> { (double[])X[rnd.Next(n)].Clone() };
                while (C.Count < k)
                {
                    var dist = X.Select(x => C.Min(c => D2(x, c))).ToArray();
                    double top = dist.Sum(), r = rnd.NextDouble() * top, acc = 0; int sec = n - 1;
                    for (int i = 0; i < n; i++) { acc += dist[i]; if (acc >= r) { sec = i; break; } }
                    C.Add((double[])X[sec].Clone());
                }
                var et = new int[n];
                for (int it = 0; it < maxIter; it++)
                {
                    bool degisti = false;
                    for (int i = 0; i < n; i++)
                    {
                        int b = 0; double bd = double.PositiveInfinity;
                        for (int c = 0; c < k; c++) { double dd = D2(X[i], C[c]); if (dd < bd - 1e-15) { bd = dd; b = c; } }
                        if (et[i] != b) { degisti = true; et[i] = b; }
                    }
                    for (int c = 0; c < k; c++)
                    {
                        var uye = Enumerable.Range(0, n).Where(i => et[i] == c).ToList();
                        if (uye.Count == 0) continue;
                        for (int j = 0; j < d; j++) C[c][j] = uye.Average(i => X[i][j]);
                    }
                    if (!degisti && it > 0) break;
                }
                double w = Enumerable.Range(0, n).Sum(i => D2(X[i], C[et[i]]));
                if (w < enIyiW - 1e-12) { enIyiW = w; enIyi = (int[])et.Clone(); }
            }
            return (enIyi!, enIyiW);
        }

        /// <summary>
        /// Davies–Bouldin endeksi: her küme için, kendisine en çok benzeyen kümeyle (S_i + S_j) / M_ij oranının ortalaması;
        /// S = üyelerin merkeze ortalama uzaklığı, M = merkezler arası uzaklık. Küçük değer = sıkı ve birbirinden ayrık kümeler.
        /// </summary>
        public static double DaviesBouldinEndeksi(double[][] X, int[] et, int k)
        {
            int n = X.Length, d = X[0].Length;
            var dolu = Enumerable.Range(0, k).Where(c => et.Contains(c)).ToList();
            var C = dolu.ToDictionary(c => c, c => Enumerable.Range(0, d).Select(j => Enumerable.Range(0, n).Where(i => et[i] == c).Average(i => X[i][j])).ToArray());
            var S = dolu.ToDictionary(c => c, c => Enumerable.Range(0, n).Where(i => et[i] == c).Average(i => Math.Sqrt(D2(X[i], C[c]))));
            if (dolu.Count < 2) return double.PositiveInfinity;
            return dolu.Average(i => dolu.Where(j => j != i).Max(j => (S[i] + S[j]) / Math.Sqrt(D2(C[i], C[j]))));
        }

        /// <summary>Ortalama siluet katsayısı (Rousseeuw 1987); tek elemanlı kümelerde s = 0.</summary>
        public static double Siluet(double[][] X, int[] et, int k)
        {
            int n = X.Length; double top = 0;
            for (int i = 0; i < n; i++)
            {
                int ci = et[i];
                var ayni = Enumerable.Range(0, n).Where(j => j != i && et[j] == ci).ToList();
                if (ayni.Count == 0) continue;
                double a = ayni.Average(j => Math.Sqrt(D2(X[i], X[j])));
                double b = double.PositiveInfinity;
                for (int c = 0; c < k; c++)
                {
                    if (c == ci) continue;
                    var diger = Enumerable.Range(0, n).Where(j => et[j] == c).ToList();
                    if (diger.Count == 0) continue;
                    b = Math.Min(b, diger.Average(j => Math.Sqrt(D2(X[i], X[j]))));
                }
                top += (b - a) / Math.Max(a, b);
            }
            return top / n;
        }
    }
}
