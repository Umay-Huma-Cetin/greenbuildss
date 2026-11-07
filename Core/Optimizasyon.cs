using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core
{
    /// <summary>
    /// Fiyat–kalınlık regresyonu: bir yalıtım ailesinin uygulanmış mantolama fiyatını kalınlığın doğrusal
    /// fonksiyonu olarak modeller (fiyat = a + b · kalınlık). Optimum kalınlık hesabı, piyasada satılan
    /// 5 / 8 / 10 cm dışındaki kalınlıkların maliyetini bu modelden alır.
    /// </summary>
    public sealed class FiyatModeli
    {
        public string Aile = "";
        public double Sabit, EgimCm, R2, Lambda, KarbonKgM3;   // kullanılan model: a (TL/m²), b (TL/m² her cm); R² uygulama fiyatlarına uyum
        public double UygulamaEgim, UygulamaSabit;             // yalnız uygulanmış fiyatlardan (n = 3) kurulan doğru
        public List<Malzeme> Gozlemler = new();
        // Levha fiyat listesi (kalınlık serisi) varsa: ek bir santimetrenin maliyeti bu daha geniş veriden alınır
        public List<LevhaFiyati> Levhalar = new();
        public double LevhaSabit, LevhaEgim, LevhaR2, LevhaHataStd;   // levha regresyonu ve eğimin standart hatası
        public bool LevhaEgimiKullaniliyor => Levhalar.Count >= 3;
        public double Fiyat(double cm) => Sabit + EgimCm * cm;

        /// <summary>Basit doğrusal regresyon (en küçük kareler): b, a, R², eğimin standart hatası.</summary>
        public static (double a, double b, double r2, double seB) Ols(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            int n = x.Count; double mx = x.Average(), my = y.Average();
            double sxx = 0, sxy = 0, sst = 0;
            for (int i = 0; i < n; i++) { sxx += (x[i] - mx) * (x[i] - mx); sxy += (x[i] - mx) * (y[i] - my); sst += (y[i] - my) * (y[i] - my); }
            double b = sxy / sxx, a = my - b * mx, sse = 0;
            for (int i = 0; i < n; i++) sse += Math.Pow(y[i] - (a + b * x[i]), 2);
            double se = n > 2 ? Math.Sqrt(sse / (n - 2) / sxx) : double.NaN;
            return (a, b, sst == 0 ? 1 : 1 - sse / sst, se);
        }

        public static FiyatModeli Kur(IEnumerable<Malzeme> urunler)
        {
            // yalnız fiyatı doğrudan yayımlanmış ürünler gözlemdir (hesaplanan kalın ürünler regresyona girmez)
            var l = urunler.Where(m => !m.FiyatHesaplanan).OrderBy(m => m.Kalinlik).ToList();
            if (l.Count < 2) throw new InvalidOperationException("Regresyon için aynı aileden en az iki kalınlık gerekir.");
            double[] x = l.Select(m => m.Kalinlik * 100).ToArray(), y = l.Select(m => m.FiyatM2).ToArray();
            var (a, b, r2, _) = Ols(x, y);
            return new FiyatModeli
            {
                Aile = l[0].Aile, Sabit = a, EgimCm = b, R2 = r2, UygulamaSabit = a, UygulamaEgim = b, Gozlemler = l,
                Lambda = l[0].Lambda, KarbonKgM3 = l.Average(m => m.KarbonM2 / m.Kalinlik),
            };
        }

        /// <summary>
        /// Aile için levha fiyat serisi varsa (≥ 3 kalınlık) ek santimetre maliyeti (eğim) o seriden alınır: kalınlık arttıkça
        /// yalnız levha bedeli artar, yapıştırıcı, dübel, file, sıva ve işçilik kalınlıktan bağımsızdır. Doğru, uygulanmış
        /// fiyatların ortalamasından geçecek biçimde konumlanır (sabit = ȳ − b·x̄).
        /// </summary>
        public void LevhaIleGuncelle(IEnumerable<LevhaFiyati> levhalar)
        {
            Levhalar = levhalar.OrderBy(l => l.KalinlikCm).ToList();
            if (Levhalar.Count < 3) return;
            var (a, b, r2, se) = Ols(Levhalar.Select(l => l.KalinlikCm).ToList(), Levhalar.Select(l => l.FiyatM2).ToList());
            LevhaSabit = a; LevhaEgim = b; LevhaR2 = r2; LevhaHataStd = se;
            double mx = Gozlemler.Average(m => m.Kalinlik * 100), my = Gozlemler.Average(m => m.FiyatM2);
            EgimCm = b; Sabit = my - b * mx;
            R2 = 1 - Gozlemler.Sum(m => Math.Pow(m.FiyatM2 - Fiyat(m.Kalinlik * 100), 2)) / Gozlemler.Sum(m => Math.Pow(m.FiyatM2 - my, 2));
        }

        public static List<FiyatModeli> Hepsi(VeriDeposu v)
        {
            var l = v.Malzemeler.Where(m => m.Tur == 'Y').GroupBy(m => m.Aile).Where(gr => gr.Count(m => !m.FiyatHesaplanan) >= 2).Select(gr => Kur(gr)).ToList();
            foreach (var m in l) m.LevhaIleGuncelle(v.Levhalar.Where(x => x.Aile == m.Aile));
            return l;
        }
    }

    public sealed class KalinlikSonucu
    {
        public FiyatModeli Model = null!;
        public double KisitsizCm, AltSinirCm, OptimumCm;      // ekonomik optimum, TS 825 alt sınırı, kısıtlı optimum
        public double LccOptimum, UOptimum, YillikIsitmaOptimum;
        public bool KisitBaglayici;                            // TS 825 alt sınırı ekonomik optimumdan büyük mü
        public Malzeme? EnIyiUrun;                             // piyasada satılan kalınlıklar içinde en düşük ömür boyu maliyetli uygun ürün
        public double EnIyiUrunLcc;
        public List<(double cm, double lcc)> Egri = new();
        public double KapaliFormCm;                            // literatürdeki kapalı form çözüm (doğrulama için)
    }

    public sealed class ButceSonucu
    {
        public Secenek? Secim;
        public List<Secenek> Pareto = new();
        public Secenek? SonrakiAdim;                           // bütçe biraz artarsa geçilecek Pareto noktası
    }

    public enum ButceAmaci { Isitma, Karbon, OmurBoyuMaliyet }

    public static class Optimizasyon
    {
        /// <summary>
        /// Optimum yalıtım kalınlığı: ömür boyu maliyet LCC(d) = A_duvar · fiyat(d) + PWF · yıllık yakıt gideri(d)
        /// en küçüklenir; kısıt: duvar U(d) ≤ TS 825 sınırı. Altın oran araması ile sürekli d (0–40 cm) için çözülür.
        /// </summary>
        public static KalinlikSonucu OptimumKalinlik(VeriDeposu v, Girdi g, FiyatModeli model, Malzeme duvar, Malzeme cam)
        {
            var r = KararMotoru.Hazirla(v, g);
            double hDiger = r.PencereU[cam.Id] * r.CamAlani + r.HSabit;
            // Yenilemede yalıtım mevcut duvarın (referans U, M1) üstüne eklenir ve duvar bedeli yoktur
            double r0 = g.Mevcut ? 1 / v.P("M1") : v.P("T6") + duvar.Kalinlik / duvar.Lambda, lam = model.Lambda;
            double dg = r.DG;
            double sabitYatirim = (r.OpakDuvar * (g.Mevcut ? 0 : duvar.FiyatM2) + r.CamAlani * cam.FiyatM2) * g.MalzemeCarpani + r.SabitYatirim;
            // 1 W/K ısı kaybının yıllık enerji gideri (TL): ısıtma (yakıt ÷ verim) + varsa soğutma (elektrik ÷ SEER)
            double kWK = 0.024 * (dg / r.IsitmaVerimi * r.GazTL + (r.Sogutma ? r.SogutmaDG / r.Seer * r.ElektrikTL : 0));

            double U(double cm) => 1 / (r0 + cm / 100 / lam);
            double Isitma(double cm) => 0.024 * dg * (U(cm) * r.OpakDuvar + hDiger);
            double Gider(double cm) => kWK * (U(cm) * r.OpakDuvar + hDiger);
            double Lcc(double cm) => r.OpakDuvar * model.Fiyat(cm) * g.MalzemeCarpani + sabitYatirim + r.PWF * Gider(cm);

            // Altın oran araması (LCC kalınlığa göre dışbükey)
            double a = 0, b = 40, phi = (Math.Sqrt(5) - 1) / 2;
            double c1 = b - phi * (b - a), c2 = a + phi * (b - a);
            for (int i = 0; i < 200 && b - a > 1e-6; i++)
            {
                if (Lcc(c1) < Lcc(c2)) { b = c2; c2 = c1; c1 = b - phi * (b - a); }
                else { a = c1; c1 = c2; c2 = a + phi * (b - a); }
            }
            double kisitsiz = (a + b) / 2;
            double altSinir = Math.Max(0, lam * (1 / r.SinirDuvar - r0) * 100);
            double opt = Math.Max(kisitsiz, altSinir);

            // Kapalı form (Bolattürk 2006; Akpınar ve Demir 2018): d* = √(PWF·Cf·0,024·DG·λ / (η·Ci)) − λ·R0
            // (soğutma varsa Cf·0,024·DG/η yerine 1 W/K'nin yıllık toplam enerji gideri kWK kullanılır)
            double ciTlPerM3 = model.EgimCm * 100 * g.MalzemeCarpani;     // TL / m² her metre kalınlık
            double kapali = Math.Max(0, (Math.Sqrt(r.PWF * kWK * lam / ciTlPerM3) - lam * r0) * 100);

            var sonuc = new KalinlikSonucu
            {
                Model = model, KisitsizCm = kisitsiz, AltSinirCm = altSinir, OptimumCm = opt, KisitBaglayici = altSinir > kisitsiz,
                LccOptimum = Lcc(opt), UOptimum = U(opt), YillikIsitmaOptimum = Isitma(opt), KapaliFormCm = kapali,
            };
            for (double cm = 1; cm <= 25.0001; cm += 0.5) sonuc.Egri.Add((cm, Lcc(cm)));

            // Piyasada satılan kalınlıklar: TS 825'i sağlayan en düşük ömür boyu maliyetli ürün
            foreach (var m in model.Gozlemler)
            {
                double cm = m.Kalinlik * 100;
                if (U(cm) > r.SinirDuvar + 1e-12 || !KararMotoru.YanginaUygun(v, r, m)) continue;
                // satılık üründe yangın yönetmeliğinin zemin şeridi farkı da (teklifteki gibi) ilk yatırıma girer
                double lcc = (r.OpakDuvar * m.FiyatM2 + KararMotoru.YanginEkMaliyeti(v, r, m)) * g.MalzemeCarpani + sabitYatirim + r.PWF * Gider(cm);
                if (sonuc.EnIyiUrun == null || lcc < sonuc.EnIyiUrunLcc) { sonuc.EnIyiUrun = m; sonuc.EnIyiUrunLcc = lcc; }
            }
            return sonuc;
        }

        public static Func<Secenek, double> AmacFonksiyonu(ButceAmaci amac) => amac switch
        {
            ButceAmaci.Karbon => s => s.ToplamCO2Kg,
            ButceAmaci.OmurBoyuMaliyet => s => s.ToplamMaliyet,
            _ => s => s.YillikIsitmaKWh,
        };

        /// <summary>
        /// Pareto (etkin) cephe — ilk yatırım ile amaç (varsayılan: yıllık ısıtma) arasında: daha düşük ya da eşit yatırımla
        /// amacı daha iyi olan başka bir seçenek yoksa seçenek etkindir.
        /// </summary>
        public static List<Secenek> Pareto(IEnumerable<Secenek> secenekler, Func<Secenek, double>? amac = null)
        {
            amac ??= s => s.YillikIsitmaKWh;
            var l = secenekler.OrderBy(s => s.IlkYatirim).ThenBy(amac).ToList();
            var cephe = new List<Secenek>();
            double enIyi = double.PositiveInfinity;
            foreach (var s in l)
                if (amac(s) < enIyi - 1e-9) { cephe.Add(s); enIyi = amac(s); }
            return cephe;
        }

        /// <summary>
        /// Bütçe kısıtlı seçim (0-1 tam sayılı model; seçenek sayısı küçük olduğu için tam sayımla kesin çözülür):
        /// en küçükle  amaç(x)   öyle ki  ilk yatırım(x) ≤ bütçe,  x TS 825'e uygun,  her kategoriden tam bir malzeme.
        /// </summary>
        public static ButceSonucu Butce(IEnumerable<Secenek> tum, double butce, ButceAmaci amac)
        {
            var uygun = tum.Where(s => s.Ts825Uygun).ToList();
            var f = AmacFonksiyonu(amac);
            var sonuc = new ButceSonucu { Pareto = Pareto(uygun, f) };
            sonuc.Secim = uygun.Where(s => s.IlkYatirim <= butce).OrderBy(f).ThenBy(s => s.IlkYatirim).FirstOrDefault();
            if (sonuc.Secim != null)
            {
                double hedef = f(sonuc.Secim);
                sonuc.SonrakiAdim = uygun.Where(s => s.IlkYatirim > butce && f(s) < hedef - 1e-9).OrderBy(s => s.IlkYatirim).FirstOrDefault();
            }
            return sonuc;
        }
    }
}
