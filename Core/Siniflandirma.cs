using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core
{
    /// <summary>Karar ağacının bir düğümü. Yaprakta Sinif dolu, iç düğümde Ozellik/Esik dolu.</summary>
    public sealed class AgacDugumu
    {
        public int Ozellik = -1;                 // bölünen özellik (iç düğüm); -1 = yaprak
        public double Esik;                      // x[Ozellik] <= Esik → Sol, değilse Sag
        public AgacDugumu? Sol, Sag;
        public string Sinif = "";                // düğümdeki çoğunluk sınıfı
        public int N;                            // düğüme düşen örnek sayısı
        public Dictionary<string, int> Sayim = new();
        public double Gini;
        public bool Yaprak => Ozellik < 0;
    }

    /// <summary>
    /// Sınıflandırma ve regresyon ağacı (CART; Breiman vd. 1984), Gini safsızlığıyla. Eşikler sıralı farklı değerlerin orta
    /// noktalarıdır. Eşit kazançta önce özellik sırası, sonra küçük eşik seçilir (tekrarlanabilir). Yaprakta çoğunluk sınıfı;
    /// eşitlikte ada göre ilk sınıf (scikit-learn ile aynı kural: sınıflar sıralıdır, ilk en büyük sayım).
    /// </summary>
    public sealed class KararAgaci
    {
        public AgacDugumu Kok = new();
        public string[] OzellikAdlari = Array.Empty<string>();
        public int MaksDerinlik;
        public double[] Onem = Array.Empty<double>();   // Gini azalmasıyla özellik önemi (toplam 1)

        public static KararAgaci Egit(double[][] X, string[] y, int maksDerinlik, string[] ozellikAdlari)
        {
            var a = new KararAgaci { MaksDerinlik = maksDerinlik, OzellikAdlari = ozellikAdlari, Onem = new double[X[0].Length] };
            a.Kok = a.Kur(X, y, Enumerable.Range(0, X.Length).ToArray(), 0);
            double top = a.Onem.Sum();
            if (top > 0) for (int j = 0; j < a.Onem.Length; j++) a.Onem[j] /= top;
            return a;
        }

        public static double Gini(IEnumerable<string> y)
        {
            var l = y.ToList(); if (l.Count == 0) return 0;
            return 1 - l.GroupBy(s => s).Sum(g => Math.Pow((double)g.Count() / l.Count, 2));
        }

        static string Cogunluk(IEnumerable<string> y) =>
            y.GroupBy(s => s).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;

        AgacDugumu Kur(double[][] X, string[] y, int[] idx, int derinlik)
        {
            var yy = idx.Select(i => y[i]).ToList();
            var d = new AgacDugumu
            {
                N = idx.Length, Gini = Gini(yy), Sinif = Cogunluk(yy),
                Sayim = yy.GroupBy(s => s).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()),
            };
            if (d.Gini < 1e-12 || derinlik >= MaksDerinlik || idx.Length < 2) return d;

            double enIyi = 0; int bj = -1; double be = 0; int[]? bs = null, bg = null;
            for (int j = 0; j < X[0].Length; j++)
            {
                var deg = idx.Select(i => X[i][j]).Distinct().OrderBy(v => v).ToArray();
                for (int k = 0; k + 1 < deg.Length; k++)
                {
                    double esik = (deg[k] + deg[k + 1]) / 2;
                    var s = idx.Where(i => X[i][j] <= esik).ToArray(); var g = idx.Where(i => X[i][j] > esik).ToArray();
                    double kazanc = d.Gini - (s.Length * Gini(s.Select(i => y[i])) + g.Length * Gini(g.Select(i => y[i]))) / idx.Length;
                    if (kazanc > enIyi + 1e-12) { enIyi = kazanc; bj = j; be = esik; bs = s; bg = g; }
                }
            }
            if (bj < 0) return d;
            Onem[bj] += enIyi * idx.Length;
            d.Ozellik = bj; d.Esik = be;
            d.Sol = Kur(X, y, bs!, derinlik + 1); d.Sag = Kur(X, y, bg!, derinlik + 1);
            return d;
        }

        public string Tahmin(double[] x) => Yaprak(x).Sinif;

        public AgacDugumu Yaprak(double[] x)
        {
            var d = Kok;
            while (!d.Yaprak) d = x[d.Ozellik] <= d.Esik ? d.Sol! : d.Sag!;
            return d;
        }

        /// <summary>Örneğin kökten yaprağa izlediği koşullar.</summary>
        public List<string> Yol(double[] x, Func<int, double, string> bicim)
        {
            var l = new List<string>(); var d = Kok;
            while (!d.Yaprak)
            {
                bool sol = x[d.Ozellik] <= d.Esik;
                l.Add($"{OzellikAdlari[d.Ozellik]} {(sol ? "≤" : ">")} {bicim(d.Ozellik, d.Esik)}");
                d = sol ? d.Sol! : d.Sag!;
            }
            return l;
        }

        /// <summary>Yoldaki koşulları özellik başına tek aralığa indirger: ör. "2.248 < Isıtma DG ≤ 3.065".</summary>
        public List<string> YolOzet(double[] x, Func<int, double, string> bicim)
        {
            var alt = new Dictionary<int, double>(); var ust = new Dictionary<int, double>(); var sira = new List<int>(); var d = Kok;
            while (!d.Yaprak)
            {
                int j = d.Ozellik; if (!sira.Contains(j)) sira.Add(j);
                if (x[j] <= d.Esik) ust[j] = ust.TryGetValue(j, out var u) ? Math.Min(u, d.Esik) : d.Esik;
                else alt[j] = alt.TryGetValue(j, out var a) ? Math.Max(a, d.Esik) : d.Esik;
                d = x[j] <= d.Esik ? d.Sol! : d.Sag!;
            }
            return sira.Select(j => alt.ContainsKey(j) && ust.ContainsKey(j) ? $"{bicim(j, alt[j])} < {OzellikAdlari[j]} ≤ {bicim(j, ust[j])}"
                                  : alt.ContainsKey(j) ? $"{OzellikAdlari[j]} > {bicim(j, alt[j])}" : $"{OzellikAdlari[j]} ≤ {bicim(j, ust[j])}").ToList();
        }

        public int Derinlik => Der(Kok);
        static int Der(AgacDugumu d) => d.Yaprak ? 0 : 1 + Math.Max(Der(d.Sol!), Der(d.Sag!));
        public int YaprakSayisi => Yap(Kok);
        static int Yap(AgacDugumu d) => d.Yaprak ? 1 : Yap(d.Sol!) + Yap(d.Sag!);
    }

    public sealed class SiniflandirmaSonucu
    {
        public List<Il> Iller = new();
        public double[][] X = Array.Empty<double[]>();
        public string[] Y = Array.Empty<string>();             // karar motorunun ilin kendi verisiyle önerdiği paket (etiket)
        public Dictionary<string, string> SinifAdi = new();     // paket kodu → okunur ad
        public KararAgaci Agac = null!;
        public int SecilenDerinlik;
        public Dictionary<int, double> LoocvDogruluk = new();   // derinlik → birini dışarıda bırak çapraz doğrulama doğruluğu
        public string[] LoocvTahmin = Array.Empty<string>();   // seçilen derinlikte her il dışarıda bırakılınca tahmin
        public double EgitimDogruluk, Loocv, TabanDogruluk;     // taban: hep en sık sınıfı söyleyen model
        public string TabanSinif = "";
        public List<string> Siniflar = new();
    }

    /// <summary>
    /// Sınıflandırma: bir ilin iklim verisinden (ısıtma DG, soğutma DG, TS 825 bölgesi) önerilecek paketi tahmin eden karar ağacı.
    /// Etiket uydurulmaz: her il için karar motoru ilin kendi MGM ve TS 825 verisiyle, ekrandaki bina ve öncelikle çalıştırılır;
    /// önerdiği paket o ilin sınıfıdır. Ağaç, karar motorunun davranışını firmanın sahada kullanabileceği birkaç açık kurala
    /// indirger ve kuralın ne kadar doğru olduğunu birini dışarıda bırak çapraz doğrulamayla (LOOCV) ölçer.
    /// Ağaç derinliği veriden seçilir: 1–6 arasında LOOCV doğruluğu en yüksek olan (eşitlikte en sığ).
    /// </summary>
    public static class Siniflandirma
    {
        public static readonly string[] Ozellikler = { "Isıtma DG", "Soğutma DG", "TS 825 bölgesi" };
        public const string UygunYok = "Uygun seçenek yok";

        public static double[] Ozellik(Il il) => new[] { il.IsitmaDG, il.SogutmaDG, (double)(il.Ts825Bolge ?? 0) };

        public static SiniflandirmaSonucu Yap(VeriDeposu v, Girdi temel, int? sabitDerinlik = null, int maksDerinlik = 6)
        {
            var s = new SiniflandirmaSonucu();
            s.Iller = v.Iller.Where(i => i.Ts825Bolge.HasValue).ToList();
            if (s.Iller.Count < 3) throw new InvalidOperationException("Sınıflandırma için bölgesi bilinen en az 3 il gerekir.");
            s.X = s.Iller.Select(Ozellik).ToArray();
            s.Y = new string[s.Iller.Count];
            for (int i = 0; i < s.Iller.Count; i++)
            {
                var g = temel.Kopya(); g.Il = s.Iller[i]; g.Bolge = s.Iller[i].Ts825Bolge!.Value; g.SadeceTs825 = true;
                var r = KararMotoru.Hesapla(v, g);
                var o = r.Siralama.FirstOrDefault();
                s.Y[i] = o?.Kod ?? UygunYok;
                if (o != null) s.SinifAdi[o.Kod] = TeklifRaporu.PaketAdi(o);
            }
            s.SinifAdi[UygunYok] = UygunYok;
            s.Siniflar = s.Y.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();

            // Taban model: hep en sık sınıf
            s.TabanSinif = s.Y.GroupBy(x => x).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;
            s.TabanDogruluk = (double)s.Y.Count(x => x == s.TabanSinif) / s.Y.Length;

            int n = s.Iller.Count;
            var tahminler = new Dictionary<int, string[]>();
            for (int d = sabitDerinlik ?? 1; d <= (sabitDerinlik ?? maksDerinlik); d++)
            {
                var t = new string[n];
                for (int i = 0; i < n; i++)
                {
                    var ix = Enumerable.Range(0, n).Where(j => j != i).ToArray();
                    var a = KararAgaci.Egit(ix.Select(j => s.X[j]).ToArray(), ix.Select(j => s.Y[j]).ToArray(), d, Ozellikler);
                    t[i] = a.Tahmin(s.X[i]);
                }
                tahminler[d] = t;
                s.LoocvDogruluk[d] = (double)Enumerable.Range(0, n).Count(i => t[i] == s.Y[i]) / n;
            }
            s.SecilenDerinlik = s.LoocvDogruluk.OrderByDescending(x => x.Value).ThenBy(x => x.Key).First().Key;
            s.Loocv = s.LoocvDogruluk[s.SecilenDerinlik];
            s.LoocvTahmin = tahminler[s.SecilenDerinlik];
            s.Agac = KararAgaci.Egit(s.X, s.Y, s.SecilenDerinlik, Ozellikler);
            s.EgitimDogruluk = (double)Enumerable.Range(0, n).Count(i => s.Agac.Tahmin(s.X[i]) == s.Y[i]) / n;
            return s;
        }

        /// <summary>Aynı etiketlerle ağacı başka bir derinlikte yeniden kurar (karar motoru yeniden çalışmaz).</summary>
        public static SiniflandirmaSonucu Derinlikle(SiniflandirmaSonucu s, int derinlik)
        {
            int n = s.Y.Length; var t = new string[n];
            for (int i = 0; i < n; i++)
            {
                var ix = Enumerable.Range(0, n).Where(j => j != i).ToArray();
                t[i] = KararAgaci.Egit(ix.Select(j => s.X[j]).ToArray(), ix.Select(j => s.Y[j]).ToArray(), derinlik, Ozellikler).Tahmin(s.X[i]);
            }
            var y = new SiniflandirmaSonucu
            {
                Iller = s.Iller, X = s.X, Y = s.Y, SinifAdi = s.SinifAdi, Siniflar = s.Siniflar, TabanSinif = s.TabanSinif, TabanDogruluk = s.TabanDogruluk,
                LoocvDogruluk = s.LoocvDogruluk, SecilenDerinlik = derinlik, LoocvTahmin = t,
                Loocv = (double)Enumerable.Range(0, n).Count(i => t[i] == s.Y[i]) / n,
                Agac = KararAgaci.Egit(s.X, s.Y, derinlik, Ozellikler),
            };
            y.EgitimDogruluk = (double)Enumerable.Range(0, n).Count(i => y.Agac.Tahmin(s.X[i]) == s.Y[i]) / n;
            return y;
        }

        /// <summary>Karmaşıklık matrisi (LOOCV): [gerçek, tahmin] sayıları, sınıflar Siniflar sırasında.</summary>
        public static int[,] Karmasiklik(SiniflandirmaSonucu s)
        {
            int k = s.Siniflar.Count; var m = new int[k, k];
            for (int i = 0; i < s.Y.Length; i++) m[s.Siniflar.IndexOf(s.Y[i]), s.Siniflar.IndexOf(s.LoocvTahmin[i])]++;
            return m;
        }
    }
}
