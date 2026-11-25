using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core.Ogrenme
{
    /// <summary>İkili sınıflandırıcı: pozitif sınıfın (1) olasılığını verir.</summary>
    public interface ISiniflandirici
    {
        string Ad { get; }
        void Egit(IReadOnlyList<double[]> x, IReadOnlyList<int> y);
        double Olasilik(double[] x);
        /// <summary>Değişken önemleri (toplamı 1); modelde yoksa null.</summary>
        double[]? Onem { get; }
        ISiniflandirici YeniKopya();
    }

    // =====================================================================================
    // Lojistik regresyon (L2 düzenlileştirmeli, Newton–Raphson / IRLS)
    // =====================================================================================
    public sealed class LojistikRegresyon : ISiniflandirici
    {
        public double Lambda = 1.0;            // L2 cezası (ölçeklenmiş değişkenlerde; sabit terim cezalanmaz)
        public double[] Katsayi = Array.Empty<double>();   // [0] sabit, sonra ölçekli değişkenler
        Olcekleyici o = new();
        public string Ad => "Lojistik regresyon";
        public double[]? Onem
        {
            get
            {
                if (Katsayi.Length < 2) return null;
                var a = Katsayi.Skip(1).Select(Math.Abs).ToArray(); double s = a.Sum();
                return s == 0 ? null : a.Select(v => v / s).ToArray();
            }
        }
        public ISiniflandirici YeniKopya() => new LojistikRegresyon { Lambda = Lambda };

        public void Egit(IReadOnlyList<double[]> x, IReadOnlyList<int> y)
        {
            o = Olcekleyici.Ogren(x);
            var z = o.Donustur(x);
            int n = z.Length, d = z[0].Length + 1;
            var w = new double[d];
            for (int it = 0; it < 50; it++)
            {
                var g = new double[d]; var h = new double[d, d];
                for (int i = 0; i < n; i++)
                {
                    var xi = Satir(z[i]);
                    double p = Mat.Sigmoid(Carp(w, xi)), r = p * (1 - p);
                    for (int a = 0; a < d; a++)
                    {
                        g[a] += (p - y[i]) * xi[a];
                        for (int b = 0; b < d; b++) h[a, b] += r * xi[a] * xi[b];
                    }
                }
                for (int a = 1; a < d; a++) { g[a] += Lambda * w[a]; h[a, a] += Lambda; }
                h[0, 0] += 1e-9;
                var adim = Mat.Coz(h, g);
                double en = 0;
                for (int a = 0; a < d; a++) { w[a] -= adim[a]; en = Math.Max(en, Math.Abs(adim[a])); }
                if (en < 1e-8) break;
            }
            Katsayi = w;
        }

        public double Olasilik(double[] x) => Mat.Sigmoid(Carp(Katsayi, Satir(o.Donustur(x))));
        static double[] Satir(double[] z) { var r = new double[z.Length + 1]; r[0] = 1; Array.Copy(z, 0, r, 1, z.Length); return r; }
        static double Carp(double[] a, double[] b) { double s = 0; for (int i = 0; i < a.Length; i++) s += a[i] * b[i]; return s; }
    }

    // =====================================================================================
    // Karar ağacı (CART, Gini safsızlığı)
    // =====================================================================================
    public sealed class AgacDugumu
    {
        public int Degisken = -1;        // -1 = yaprak
        public double Esik;              // x[Degisken] <= Esik → Sol
        public AgacDugumu? Sol, Sag;
        public int N, Pozitif;           // düğümdeki eğitim kaydı ve pozitif sayısı
        public double Olasilik => N == 0 ? 0.5 : (Pozitif + 1.0) / (N + 2.0);   // Laplace düzeltmesi
        public bool Yaprak => Degisken < 0;
    }

    public sealed class KararAgaci : ISiniflandirici
    {
        public int MaksDerinlik = 5, MinYaprak = 5;
        public int? DenenecekDegisken;   // rastgele ormanda her bölünmede denenecek değişken sayısı (null = hepsi)
        public Random? Rnd;
        public AgacDugumu Kok = new();
        double[] onem = Array.Empty<double>();
        public string Ad => "Karar ağacı (CART)";
        public double[]? Onem { get { double s = onem.Sum(); return s == 0 ? null : onem.Select(v => v / s).ToArray(); } }
        public ISiniflandirici YeniKopya() => new KararAgaci { MaksDerinlik = MaksDerinlik, MinYaprak = MinYaprak };

        public void Egit(IReadOnlyList<double[]> x, IReadOnlyList<int> y)
        {
            onem = new double[x[0].Length];
            Kok = Kur(x, y, Enumerable.Range(0, x.Count).ToArray(), 0);
        }

        static double Gini(int n, int pos) { if (n == 0) return 0; double p = (double)pos / n; return 2 * p * (1 - p); }

        AgacDugumu Kur(IReadOnlyList<double[]> x, IReadOnlyList<int> y, int[] idx, int derinlik)
        {
            int pos = idx.Count(i => y[i] == 1);
            var dug = new AgacDugumu { N = idx.Length, Pozitif = pos };
            if (derinlik >= MaksDerinlik || idx.Length < 2 * MinYaprak || pos == 0 || pos == idx.Length) return dug;

            int d = x[0].Length;
            IEnumerable<int> adaylar = Enumerable.Range(0, d);
            if (DenenecekDegisken is int m && m < d && Rnd != null) adaylar = adaylar.OrderBy(_ => Rnd.Next()).Take(m).ToArray();
            double ebeveyn = Gini(idx.Length, pos), enIyi = 0; int enD = -1; double enE = 0;
            foreach (int j in adaylar)
            {
                var sirali = idx.OrderBy(i => x[i][j]).ToArray();
                int solN = 0, solP = 0;
                for (int k = 0; k < sirali.Length - 1; k++)
                {
                    solN++; if (y[sirali[k]] == 1) solP++;
                    double a = x[sirali[k]][j], b = x[sirali[k + 1]][j];
                    if (a == b || solN < MinYaprak || sirali.Length - solN < MinYaprak) continue;
                    int sagN = sirali.Length - solN, sagP = pos - solP;
                    double kazanc = ebeveyn - (solN * Gini(solN, solP) + sagN * Gini(sagN, sagP)) / sirali.Length;
                    if (kazanc > enIyi + 1e-12) { enIyi = kazanc; enD = j; enE = (a + b) / 2; }
                }
            }
            if (enD < 0) return dug;
            onem[enD] += enIyi * idx.Length;
            dug.Degisken = enD; dug.Esik = enE;
            dug.Sol = Kur(x, y, idx.Where(i => x[i][enD] <= enE).ToArray(), derinlik + 1);
            dug.Sag = Kur(x, y, idx.Where(i => x[i][enD] > enE).ToArray(), derinlik + 1);
            return dug;
        }

        public double Olasilik(double[] x)
        {
            var d = Kok;
            while (!d.Yaprak) d = x[d.Degisken] <= d.Esik ? d.Sol! : d.Sag!;
            return d.Olasilik;
        }

        /// <summary>Ağacın kuralları ("eğer … ise") okunur metin olarak.</summary>
        public List<string> Kurallar(string[] adlar, Func<int, double, string>? bicim = null)
        {
            var l = new List<string>();
            void Gez(AgacDugumu d, string yol)
            {
                if (d.Yaprak) { l.Add($"{(yol.Length == 0 ? "Her durumda" : "Eğer " + yol)} → kabul olasılığı %{100 * d.Olasilik:0} ({d.N} teklif)"); return; }
                string e = bicim?.Invoke(d.Degisken, d.Esik) ?? d.Esik.ToString("0.##");
                string ek = yol.Length == 0 ? "" : yol + " ve ";
                Gez(d.Sol!, ek + $"{adlar[d.Degisken]} ≤ {e}");
                Gez(d.Sag!, ek + $"{adlar[d.Degisken]} > {e}");
            }
            Gez(Kok, "");
            return l;
        }
    }

    // =====================================================================================
    // Rastgele orman (bootstrap + her bölünmede rastgele değişken alt kümesi)
    // =====================================================================================
    public sealed class RastgeleOrman : ISiniflandirici
    {
        public int AgacSayisi = 200, MaksDerinlik = 8, MinYaprak = 3, Tohum = 42;
        readonly List<KararAgaci> agaclar = new();
        double[] onem = Array.Empty<double>();
        public string Ad => "Rastgele orman";
        public double[]? Onem { get { double s = onem.Sum(); return s == 0 ? null : onem.Select(v => v / s).ToArray(); } }
        public ISiniflandirici YeniKopya() => new RastgeleOrman { AgacSayisi = AgacSayisi, MaksDerinlik = MaksDerinlik, MinYaprak = MinYaprak, Tohum = Tohum };

        public void Egit(IReadOnlyList<double[]> x, IReadOnlyList<int> y)
        {
            agaclar.Clear();
            int d = x[0].Length, n = x.Count;
            onem = new double[d];
            var r = new Random(Tohum);
            int m = Math.Max(1, (int)Math.Round(Math.Sqrt(d)));
            for (int t = 0; t < AgacSayisi; t++)
            {
                var bx = new double[n][]; var by = new int[n];
                for (int i = 0; i < n; i++) { int k = r.Next(n); bx[i] = x[k]; by[i] = y[k]; }
                var a = new KararAgaci { MaksDerinlik = MaksDerinlik, MinYaprak = MinYaprak, DenenecekDegisken = m, Rnd = new Random(r.Next()) };
                a.Egit(bx, by);
                agaclar.Add(a);
                var o = a.Onem; if (o != null) for (int j = 0; j < d; j++) onem[j] += o[j];
            }
        }

        public double Olasilik(double[] x) => agaclar.Average(a => a.Olasilik(x));
    }

    // =====================================================================================
    // Çoklu doğrusal regresyon (en küçük kareler) + tanı ölçüleri
    // =====================================================================================
    public sealed class RegresyonSonucu
    {
        public string[] Adlar = Array.Empty<string>();      // [0] = "Sabit"
        public double[] Katsayi = Array.Empty<double>(), StdHata = Array.Empty<double>(), GA95Alt = Array.Empty<double>(), GA95Ust = Array.Empty<double>();
        public double R2, DuzeltilmisR2, Rmse;
        public int N, P;
        public double[] Tahmin = Array.Empty<double>(), Artik = Array.Empty<double>(), Cook = Array.Empty<double>();
        /// <summary>Cook uzaklığı 4/n eşiğini aşan (etkili) gözlemlerin sırası.</summary>
        public int[] EtkiliGozlemler => Enumerable.Range(0, Cook.Length).Where(i => Cook[i] > 4.0 / N).ToArray();
        public double Tahminle(double[] x) { double s = Katsayi[0]; for (int j = 0; j < x.Length; j++) s += Katsayi[j + 1] * x[j]; return s; }
    }

    public static class DogrusalRegresyon
    {
        public static RegresyonSonucu Ekk(IReadOnlyList<double[]> x, IReadOnlyList<double> y, string[] adlar, bool sabitVar = true)
        {
            int n = x.Count, d = x[0].Length, p = d + (sabitVar ? 1 : 0);
            if (n <= p) throw new InvalidOperationException($"Regresyon için en az {p + 1} kayıt gerekir (şu an {n}).");
            double[] Satir(double[] r) => sabitVar ? new[] { 1.0 }.Concat(r).ToArray() : r;
            var X = x.Select(Satir).ToArray();
            var xtx = new double[p, p]; var xty = new double[p];
            for (int i = 0; i < n; i++)
                for (int a = 0; a < p; a++) { xty[a] += X[i][a] * y[i]; for (int b = 0; b < p; b++) xtx[a, b] += X[i][a] * X[i][b]; }
            var beta = Mat.Coz(xtx, xty);
            var inv = Mat.Ters(xtx);
            var t = X.Select(r => r.Select((v, j) => v * beta[j]).Sum()).ToArray();
            var e = Enumerable.Range(0, n).Select(i => y[i] - t[i]).ToArray();
            double sse = e.Sum(v => v * v), s2 = sse / (n - p);
            var se = Enumerable.Range(0, p).Select(j => Math.Sqrt(s2 * inv[j, j])).ToArray();
            double tk = Mat.T95(n - p);
            var cook = new double[n];
            for (int i = 0; i < n; i++)
            {
                double h = 0;
                for (int a = 0; a < p; a++) for (int b = 0; b < p; b++) h += X[i][a] * inv[a, b] * X[i][b];
                cook[i] = s2 == 0 || h >= 1 ? 0 : e[i] * e[i] / (p * s2) * h / Math.Pow(1 - h, 2);
            }
            double r2 = Metrik.R2(y, t);
            var ad = sabitVar ? new[] { "Sabit" }.Concat(adlar).ToArray() : adlar;
            if (!sabitVar) ad = new[] { "Sabit" }.Concat(adlar).ToArray();
            var sonuc = new RegresyonSonucu
            {
                Adlar = ad, N = n, P = p, R2 = r2, DuzeltilmisR2 = 1 - (1 - r2) * (n - 1) / (n - p), Rmse = Math.Sqrt(sse / n),
                Tahmin = t, Artik = e, Cook = cook,
            };
            // Sabit terimsiz modelde de katsayı dizisi [0] = 0 sabitiyle tutulur
            sonuc.Katsayi = sabitVar ? beta : new[] { 0.0 }.Concat(beta).ToArray();
            sonuc.StdHata = sabitVar ? se : new[] { 0.0 }.Concat(se).ToArray();
            sonuc.GA95Alt = sonuc.Katsayi.Select((b, j) => b - tk * sonuc.StdHata[j]).ToArray();
            sonuc.GA95Ust = sonuc.Katsayi.Select((b, j) => b + tk * sonuc.StdHata[j]).ToArray();
            return sonuc;
        }
    }

    // =====================================================================================
    // k-ortalamalar (k-means++ başlatma, çok başlangıç) + küme geçerlilik ölçüleri
    // =====================================================================================
    public sealed class KOrtalamaSonucu
    {
        public int K;
        public int[] Etiket = Array.Empty<int>();
        public double[][] Merkez = Array.Empty<double[]>();
        public double Ssw, DaviesBouldin, Siluet;
    }

    public static class KOrtalamalar
    {
        public static KOrtalamaSonucu Calistir(IReadOnlyList<double[]> x, int k, int baslangic = 50, int tohum = 42)
        {
            if (k < 2 || k >= x.Count) throw new ArgumentException("k, 2 ile kayıt sayısı − 1 arasında olmalı.");
            var r = new Random(tohum);
            KOrtalamaSonucu? en = null;
            for (int b = 0; b < baslangic; b++)
            {
                var m = Baslat(x, k, r);
                var et = new int[x.Count];
                for (int it = 0; it < 300; it++)
                {
                    bool degisti = false;
                    for (int i = 0; i < x.Count; i++)
                    {
                        int j = Enumerable.Range(0, k).OrderBy(c => Mat.UzaklikKare(x[i], m[c])).First();
                        if (j != et[i] || it == 0) { degisti |= j != et[i]; et[i] = j; }
                    }
                    for (int c = 0; c < k; c++)
                    {
                        var uye = Enumerable.Range(0, x.Count).Where(i => et[i] == c).ToList();
                        if (uye.Count == 0) { m[c] = (double[])x[r.Next(x.Count)].Clone(); degisti = true; continue; }
                        m[c] = Enumerable.Range(0, x[0].Length).Select(d => uye.Average(i => x[i][d])).ToArray();
                    }
                    if (!degisti && it > 0) break;
                }
                double ssw = Enumerable.Range(0, x.Count).Sum(i => Mat.UzaklikKare(x[i], m[et[i]]));
                if (en == null || ssw < en.Ssw - 1e-9) en = new KOrtalamaSonucu { K = k, Etiket = (int[])et.Clone(), Merkez = m.Select(v => (double[])v.Clone()).ToArray(), Ssw = ssw };
            }
            en!.DaviesBouldin = DaviesBouldin(x, en.Etiket, en.Merkez);
            en.Siluet = Siluet(x, en.Etiket, k);
            return en;
        }

        static double[][] Baslat(IReadOnlyList<double[]> x, int k, Random r)
        {
            var m = new List<double[]> { x[r.Next(x.Count)] };
            while (m.Count < k)
            {
                var d2 = x.Select(p => m.Min(c => Mat.UzaklikKare(p, c))).ToArray();
                double s = d2.Sum(), u = r.NextDouble() * s, t = 0; int sec = x.Count - 1;
                for (int i = 0; i < x.Count; i++) { t += d2[i]; if (t >= u) { sec = i; break; } }
                m.Add(x[sec]);
            }
            return m.Select(v => (double[])v.Clone()).ToArray();
        }

        public static double DaviesBouldin(IReadOnlyList<double[]> x, int[] et, double[][] m)
        {
            int k = m.Length;
            var s = Enumerable.Range(0, k).Select(c =>
            {
                var u = Enumerable.Range(0, x.Count).Where(i => et[i] == c).ToList();
                return u.Count == 0 ? 0 : u.Average(i => Math.Sqrt(Mat.UzaklikKare(x[i], m[c])));
            }).ToArray();
            double top = 0;
            for (int a = 0; a < k; a++)
            {
                double en = 0;
                for (int b = 0; b < k; b++) if (a != b) en = Math.Max(en, (s[a] + s[b]) / Math.Sqrt(Mat.UzaklikKare(m[a], m[b])));
                top += en;
            }
            return top / k;
        }

        public static double Siluet(IReadOnlyList<double[]> x, int[] et, int k)
        {
            double top = 0;
            for (int i = 0; i < x.Count; i++)
            {
                var ort = new double[k]; var say = new int[k];
                for (int j = 0; j < x.Count; j++) if (i != j) { ort[et[j]] += Math.Sqrt(Mat.UzaklikKare(x[i], x[j])); say[et[j]]++; }
                if (say[et[i]] == 0) continue;   // tek üyeli küme: siluet 0
                double a = ort[et[i]] / say[et[i]], b = double.MaxValue;
                for (int c = 0; c < k; c++) if (c != et[i] && say[c] > 0) b = Math.Min(b, ort[c] / say[c]);
                top += (b - a) / Math.Max(a, b);
            }
            return top / x.Count;
        }
    }
}
