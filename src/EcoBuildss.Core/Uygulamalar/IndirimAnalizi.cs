using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core.Uygulamalar
{
    /// <summary>Belirli bir indirim oranında teklifin kabul olasılığı ve beklenen kârı.</summary>
    public sealed class IndirimNoktasi
    {
        public double Indirim, Olasilik, TeklifTL, Kar;
        /// <summary>Beklenen kâr = kabul olasılığı × (teklif − maliyet). Kabul edilmezse kâr 0.</summary>
        public double BeklenenKar => Olasilik * Kar;
    }

    /// <summary>
    /// İndirim kararı: indirim arttıkça kabul olasılığı artar, kâr marjı azalır. Beklenen kârı en yüksek yapan indirim aranır.
    /// Olasılık firmanın kendi teklif sonuçlarıyla eğitilmiş, kalite eşiğini geçmiş kabul modelinden gelir.
    /// </summary>
    public static class IndirimAnalizi
    {
        /// <param name="hesapla">indirim oranı → (kabul olasılığı, KDV hariç teklif tutarı)</param>
        /// <param name="maliyet">firmanın KDV hariç maliyeti (kârsız tutar)</param>
        public static List<IndirimNoktasi> Egri(Func<double, (double olasilik, double teklifTL)> hesapla, double maliyet, double azami = 0.30, double adim = 0.01)
        {
            if (adim <= 0 || azami < 0) throw new ArgumentException("İndirim aralığı geçersiz.");
            var l = new List<IndirimNoktasi>();
            for (int i = 0; i * adim <= azami + 1e-9; i++)
            {
                double ind = Math.Round(i * adim, 6);
                var (p, tl) = hesapla(ind);
                if (double.IsNaN(p) || p < 0 || p > 1) throw new ArgumentException("Olasılık 0 ile 1 arasında olmalı.");
                l.Add(new IndirimNoktasi { Indirim = ind, Olasilik = p, TeklifTL = tl, Kar = tl - maliyet });
            }
            return l;
        }

        /// <summary>Beklenen kârı en yüksek, zarar etmeyen nokta; eşitlikte daha az indirim seçilir. Kârlı nokta yoksa null.</summary>
        public static IndirimNoktasi? EnIyi(IReadOnlyList<IndirimNoktasi> egri) =>
            egri.Where(n => n.Kar > 0).OrderByDescending(n => Math.Round(n.BeklenenKar, 2)).ThenBy(n => n.Indirim).FirstOrDefault();
    }
}
