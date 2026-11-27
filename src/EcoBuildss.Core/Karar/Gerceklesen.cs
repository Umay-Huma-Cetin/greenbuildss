using System;
using System.Collections.Generic;
using System.Linq;
using EcoBuildss.Core.Depo;

namespace EcoBuildss.Core
{
    /// <summary>
    /// İş bittikten sonra: söz verilen tasarruf gerçekleşti mi?
    /// Karşılaştırma doğal gaz tüketimiyle (m³) yapılır, TL ile değil: tarife değişince TL faturası tasarruftan bağımsız oynar.
    /// Hava koşulları yıldan yıla farklı olduğundan birkaç puanlık sapma olağandır.
    /// </summary>
    public static class Gerceklesen
    {
        /// <summary>Gerçekleşen azalma oranı = 1 − sonraki yıl tüketimi ÷ önceki yıl tüketimi.</summary>
        public static double Azalma(double onceM3, double sonraM3)
        {
            if (onceM3 <= 0 || sonraM3 < 0) throw new ArgumentException("Tüketim değerleri geçersiz (önceki yıl sıfırdan büyük olmalı).");
            return 1 - sonraM3 / onceM3;
        }

        /// <summary>Gerçekleşen − söz verilen (puan; pozitif = sözden fazla tasarruf).</summary>
        public static double Fark(double soz, double onceM3, double sonraM3) => Azalma(onceM3, sonraM3) - soz;

        /// <summary>Ölçülen işlerde ortalama fark (puan) ve iş sayısı; ölçülen iş yoksa null.</summary>
        public static (double ortFark, int n)? Ozet(IEnumerable<KayitliTeklif> teklifler)
        {
            var l = teklifler.Where(t => t.GercekOnceM3 > 0 && t.SozAzalma > 0).Select(t => Fark(t.SozAzalma, t.GercekOnceM3, t.GercekSonraM3)).ToList();
            return l.Count == 0 ? null : (l.Average(), l.Count);
        }
    }
}
