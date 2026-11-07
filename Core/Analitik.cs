using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core
{
    /// <summary>KPI'lar (temel performans göstergeleri) — gösterge paneli ve teklif belgesi için.</summary>
    public sealed class Kpi
    {
        public double YatirimM2, IsitmaM2, YillikEnerjiTL, KarbonM2, IsiKaybiAzalma, GeriOdemeYil, MalzemeKarbonPayi;
        public double YatirimYalitim, YatirimCam, YatirimDuvar, YatirimCatiZemin, EnerjiToplamTL;
        public bool GeriOdemeVar;     // ek yatırım analiz ufku içinde değil, 100 yıla kadar aranır
        public bool EkYatirim, Tasarruf;  // referansa göre ek yatırım var mı, yıllık gider düşüyor mu

        /// <summary>Geri ödeme: ek yatırımın, yıllık yakıt tasarrufunun bugünkü değeriyle kaç yılda karşılandığı (iskontolu).</summary>
        public static Kpi Hesapla(Secenek s, Sonuc r, Secenek referans, double iskonto, int yil)
        {
            double alan = r.KullanimAlani;
            var k = new Kpi
            {
                YatirimM2 = s.IlkYatirim / alan,
                IsitmaM2 = s.YillikIsitmaKWh / alan,
                YillikEnerjiTL = s.YillikGiderTL,
                KarbonM2 = s.ToplamCO2Kg / alan,
                IsiKaybiAzalma = 1 - s.H / r.HBaz,
                YatirimYalitim = r.OpakDuvar * s.Yalitim.FiyatM2,
                YatirimCam = r.CamAlani * s.Cam.FiyatM2,
                YatirimDuvar = r.OpakDuvar * s.Duvar.FiyatM2,
                YatirimCatiZemin = r.SabitYatirim,
                EnerjiToplamTL = s.YillikGiderTL * r.PWF,
            };
            double malzemeCO2 = r.OpakDuvar * (s.Duvar.KarbonM2 + s.Yalitim.KarbonM2) + r.CamAlani * s.Cam.KarbonM2;
            k.MalzemeKarbonPayi = malzemeCO2 / s.ToplamCO2Kg;
            double tasarruf = referans.YillikGiderTL - s.YillikGiderTL;
            double ek = s.IlkYatirim - referans.IlkYatirim;
            k.EkYatirim = ek > 1e-6; k.Tasarruf = tasarruf > 1e-6;
            if (k.Tasarruf && k.EkYatirim)
            {
                // İskontolu geri ödeme: Σ tasarruf/(1+r)^t ≥ ek yatırım olan ilk t (en çok 100 yıl aranır)
                double birikim = 0;
                for (int t = 1; t <= 100; t++)
                {
                    double onceki = birikim;
                    birikim += tasarruf / Math.Pow(1 + iskonto, t);
                    if (birikim >= ek) { k.GeriOdemeVar = true; k.GeriOdemeYil = t - 1 + (ek - onceki) / (birikim - onceki); break; }
                }
            }
            return k;
        }

        /// <summary>
        /// Yenileme (mevcut bina): pakete göre yıllık enerji gideri tasarrufu, iskontolu geri dönüş süresi (yıl; dönmüyorsa null)
        /// ve analiz süresi sonunda net bugünkü değer (tasarrufların bugünkü değeri − ilk yatırım).
        /// </summary>
        public static (double tasarrufTL, double? geriDonusYil, double netBugunkuDeger, double co2AzalmaKg) MevcutaGore(Secenek s, Sonuc r)
        {
            double tasarruf = r.MevcutGiderTL - s.YillikGiderTL;
            double? yil = null;
            if (tasarruf > 1e-6)
            {
                double birikim = 0;
                for (int t = 1; t <= 100; t++)
                {
                    double onceki = birikim;
                    birikim += tasarruf / Math.Pow(1 + r.EnerjiIskonto, t);
                    if (birikim >= s.IlkYatirim) { yil = t - 1 + (s.IlkYatirim - onceki) / (birikim - onceki); break; }
                }
            }
            return (tasarruf, yil, r.PWF * tasarruf - s.IlkYatirim, r.MevcutCO2Kg - s.ToplamCO2Kg);
        }
    }
}
