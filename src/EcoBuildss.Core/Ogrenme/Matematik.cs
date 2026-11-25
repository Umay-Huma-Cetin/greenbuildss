using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core.Ogrenme
{
    /// <summary>Doğrusal cebir ve istatistik yardımcıları (dış kütüphane kullanılmaz).</summary>
    public static class Mat
    {
        /// <summary>Simetrik pozitif tanımlı A için A·x = b (Gauss eleme, kısmi pivot). Tekil matriste hata verir.</summary>
        public static double[] Coz(double[,] a, double[] b)
        {
            int n = b.Length;
            var m = (double[,])a.Clone(); var y = (double[])b.Clone();
            for (int k = 0; k < n; k++)
            {
                int p = k;
                for (int i = k + 1; i < n; i++) if (Math.Abs(m[i, k]) > Math.Abs(m[p, k])) p = i;
                if (Math.Abs(m[p, k]) < 1e-12) throw new InvalidOperationException("Matris tekil: değişkenler birbirinin doğrusal bileşimi (çoklu doğrusal bağlantı) olabilir.");
                if (p != k) { for (int j = 0; j < n; j++) (m[k, j], m[p, j]) = (m[p, j], m[k, j]); (y[k], y[p]) = (y[p], y[k]); }
                for (int i = k + 1; i < n; i++)
                {
                    double f = m[i, k] / m[k, k];
                    for (int j = k; j < n; j++) m[i, j] -= f * m[k, j];
                    y[i] -= f * y[k];
                }
            }
            var x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double s = y[i];
                for (int j = i + 1; j < n; j++) s -= m[i, j] * x[j];
                x[i] = s / m[i, i];
            }
            return x;
        }

        /// <summary>A⁻¹ (Gauss–Jordan).</summary>
        public static double[,] Ters(double[,] a)
        {
            int n = a.GetLength(0);
            var t = new double[n, n];
            for (int j = 0; j < n; j++)
            {
                var e = new double[n]; e[j] = 1;
                var x = Coz(a, e);
                for (int i = 0; i < n; i++) t[i, j] = x[i];
            }
            return t;
        }

        public static double Ortalama(IEnumerable<double> x) { double s = 0; int n = 0; foreach (var v in x) { s += v; n++; } return n == 0 ? double.NaN : s / n; }

        public static double StdSapma(IReadOnlyList<double> x)
        {
            if (x.Count < 2) return 0;
            double m = Ortalama(x), s = 0;
            foreach (var v in x) s += (v - m) * (v - m);
            return Math.Sqrt(s / (x.Count - 1));
        }

        public static double Kuantil(IEnumerable<double> x, double q)
        {
            var s = x.OrderBy(v => v).ToArray();
            if (s.Length == 0) return double.NaN;
            double h = (s.Length - 1) * q; int a = (int)Math.Floor(h), b = Math.Min(s.Length - 1, a + 1);
            return s[a] + (h - a) * (s[b] - s[a]);
        }

        /// <summary>Student t dağılımının iki yönlü kritik değeri (%95). Küçük örneklemde tablo, büyükte normal yaklaşımı.</summary>
        public static double T95(int sd)
        {
            double[] tablo = { double.NaN, 12.706, 4.303, 3.182, 2.776, 2.571, 2.447, 2.365, 2.306, 2.262, 2.228, 2.201, 2.179, 2.160, 2.145, 2.131,
                               2.120, 2.110, 2.101, 2.093, 2.086, 2.080, 2.074, 2.069, 2.064, 2.060, 2.056, 2.052, 2.048, 2.045, 2.042 };
            if (sd <= 0) return double.NaN;
            if (sd < tablo.Length) return tablo[sd];
            if (sd <= 40) return 2.021; if (sd <= 60) return 2.000; if (sd <= 120) return 1.980;
            return 1.960;
        }

        public static double Sigmoid(double z) => z >= 0 ? 1 / (1 + Math.Exp(-z)) : Math.Exp(z) / (1 + Math.Exp(z));

        public static double UzaklikKare(double[] a, double[] b) { double s = 0; for (int i = 0; i < a.Length; i++) { double d = a[i] - b[i]; s += d * d; } return s; }
    }

    /// <summary>Z-skor ölçekleyici. Ortalama ve standart sapma yalnız eğitim verisinden öğrenilir (veri sızıntısı olmaz).</summary>
    public sealed class Olcekleyici
    {
        public double[] Ort = Array.Empty<double>(), Std = Array.Empty<double>();

        public static Olcekleyici Ogren(IReadOnlyList<double[]> x)
        {
            int d = x[0].Length;
            var o = new Olcekleyici { Ort = new double[d], Std = new double[d] };
            for (int j = 0; j < d; j++)
            {
                var sutun = x.Select(r => r[j]).ToList();
                o.Ort[j] = Mat.Ortalama(sutun);
                double s = Mat.StdSapma(sutun);
                o.Std[j] = s < 1e-12 ? 1 : s;   // sabit sütun: bölme hatası olmasın
            }
            return o;
        }

        public double[] Donustur(double[] r) { var z = new double[r.Length]; for (int j = 0; j < r.Length; j++) z[j] = (r[j] - Ort[j]) / Std[j]; return z; }
        public double[][] Donustur(IReadOnlyList<double[]> x) => x.Select(Donustur).ToArray();
    }

    /// <summary>Tekrarlanabilir rastgele sayı üreteci yardımcıları (sabit tohum).</summary>
    public static class Rastgele
    {
        public static double Normal(Random r, double ort = 0, double std = 1)
        {
            double u1 = 1 - r.NextDouble(), u2 = r.NextDouble();
            return ort + std * Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        public static void Karistir<T>(IList<T> l, Random r)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = r.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
        }
    }
}
