using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace EcoBuildss.Core
{
    /// <summary>Fiyat belirsizliği (Monte Carlo) sonucu.</summary>
    public sealed class BelirsizlikSonucu
    {
        public int Deneme, Tohum, AraliklıMalzeme;
        public string OneriKod = "";
        public double OneriBirinciOrani;                  // önerinin denemelerde birinci çıkma oranı (0–1)
        public List<(Secenek s, double oran)> Birinciler = new();   // birinci çıkan paketler, orana göre azalan
        public double LccP5, LccP50, LccP95;              // önerinin ömür boyu maliyeti, %5 / %50 / %95 yüzdelik
        public double IlkYatirimP5, IlkYatirimP95;
    }

    /// <summary>
    /// Fiyat belirsizliği: kaynağı fiyatı aralık olarak veren her malzemenin fiyatı (ör. "650-800 TL/m² aralığının ortası")
    /// aralık içinde düzgün dağılımla çekilir; hesaplanan 12/15 cm fiyatları aynı ailenin 10 cm çekilişine levha farkı eklenerek,
    /// çift kat çatı tek katın iki katı olarak izler. Her denemede karar yeniden verilir; önerinin kaç denemede birinci kaldığı sayılır.
    /// </summary>
    public static class Belirsizlik
    {
        static readonly Regex Aralik = new(@"^\s*([\d\.]+(?:,\d+)?)\s*-\s*([\d\.]+(?:,\d+)?)\s*TL/m²", RegexOptions.Compiled);

        /// <summary>Fiyat hesabındaki aralık (alt, üst); yoksa null.</summary>
        public static (double alt, double ust)? FiyatAraligi(Malzeme m)
        {
            var e = Aralik.Match(m.FiyatHesabi);
            if (!e.Success) return null;
            double S(string x) => double.Parse(x.Replace(".", "").Replace(",", "."), CultureInfo.InvariantCulture);
            double a = S(e.Groups[1].Value), b = S(e.Groups[2].Value);
            return a < b ? (a, b) : null;
        }

        public static BelirsizlikSonucu Calistir(VeriDeposu v, Girdi g, int deneme = 1000, int tohum = 42)
        {
            var temel = KararMotoru.Hesapla(v, g);
            var liste0 = temel.Siralama.Count > 0 ? temel.Siralama : KararMotoru.Hesapla(v, Filtresiz(g)).Siralama;
            var sonuc = new BelirsizlikSonucu { Deneme = deneme, Tohum = tohum, OneriKod = liste0.Count > 0 ? liste0[0].Kod : "" };
            var araliklar = v.Malzemeler.ToDictionary(m => m.Id, FiyatAraligi);
            sonuc.AraliklıMalzeme = araliklar.Count(x => x.Value != null);
            var rnd = new Random(tohum);
            var sayac = new Dictionary<string, int>();
            var ornek = new Dictionary<string, Secenek>();
            var lcc = new List<double>(); var yat = new List<double>();
            for (int i = 0; i < deneme; i++)
            {
                var cekilen = new Dictionary<string, double>();
                foreach (var m in v.Malzemeler)
                    if (araliklar[m.Id] is (double a, double b)) cekilen[m.Id] = a + rnd.NextDouble() * (b - a);
                double Fiyat(Malzeme m)
                {
                    if (cekilen.TryGetValue(m.Id, out var f)) return f;
                    if (m.FiyatHesaplanan && m.Tur == 'Y')
                    {
                        // aynı ailenin 10 cm'lik yayımlanmış fiyatı + sabit levha farkı
                        var on = v.Malzemeler.FirstOrDefault(x => x.Tur == 'Y' && x.Aile == m.Aile && !x.FiyatHesaplanan && Math.Abs(x.Kalinlik - 0.10) < 1e-9);
                        if (on != null && cekilen.TryGetValue(on.Id, out var f10)) return f10 + (m.FiyatM2 - on.FiyatM2);
                    }
                    return m.FiyatM2;
                }
                var vi = v.Kopya(Fiyat);
                var r = KararMotoru.Hesapla(vi, g);
                var l = r.Siralama.Count > 0 ? r.Siralama : KararMotoru.Hesapla(vi, Filtresiz(g)).Siralama;
                if (l.Count == 0) continue;
                string k = l[0].Kod;
                sayac[k] = sayac.TryGetValue(k, out var n) ? n + 1 : 1;
                if (!ornek.ContainsKey(k)) ornek[k] = liste0.FirstOrDefault(x => x.Kod == k) ?? l[0];
                var o = l.FirstOrDefault(x => x.Kod == sonuc.OneriKod);
                if (o != null) { lcc.Add(o.ToplamMaliyet); yat.Add(o.IlkYatirim); }
            }
            int toplam = sayac.Values.Sum();
            if (toplam > 0)
            {
                sonuc.Birinciler = sayac.OrderByDescending(x => x.Value).Select(x => (ornek[x.Key], x.Value / (double)toplam)).ToList();
                sonuc.OneriBirinciOrani = sayac.TryGetValue(sonuc.OneriKod, out var no) ? no / (double)toplam : 0;
            }
            if (lcc.Count > 0)
            {
                lcc.Sort(); yat.Sort();
                sonuc.LccP5 = Yuzdelik(lcc, 0.05); sonuc.LccP50 = Yuzdelik(lcc, 0.50); sonuc.LccP95 = Yuzdelik(lcc, 0.95);
                sonuc.IlkYatirimP5 = Yuzdelik(yat, 0.05); sonuc.IlkYatirimP95 = Yuzdelik(yat, 0.95);
            }
            return sonuc;
        }

        static Girdi Filtresiz(Girdi g) { var x = g.Kopya(); x.SadeceTs825 = false; return x; }

        /// <summary>Sıralı dizide doğrusal aradeğerli yüzdelik.</summary>
        public static double Yuzdelik(List<double> s, double p)
        {
            double h = (s.Count - 1) * p; int i = (int)Math.Floor(h);
            return i + 1 < s.Count ? s[i] + (h - i) * (s[i + 1] - s[i]) : s[i];
        }
    }
}
