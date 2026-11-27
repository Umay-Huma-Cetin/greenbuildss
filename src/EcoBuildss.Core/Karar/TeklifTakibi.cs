using System;
using System.Collections.Generic;
using System.Linq;
using EcoBuildss.Core.Depo;

namespace EcoBuildss.Core
{
    /// <summary>Bir satışçının teklif sonuçları.</summary>
    public sealed class SatisciOzeti
    {
        public string Hazirlayan = "";
        public int Teklif, Kabul, Red, Bekleyen, TakipGereken;
        public double KazanilanTL, BekleyenTL;
        /// <summary>Sonuçlanan teklifler içinde kabul oranı; sonuçlanan yoksa null.</summary>
        public double? KazanmaOrani => Kabul + Red == 0 ? null : (double)Kabul / (Kabul + Red);
    }

    /// <summary>
    /// Teklif takibi: cevap bekleyen tekliflerin yaşı ve satışçı bazında sonuçlar.
    /// Uzun süre cevapsız kalan teklif kaybedilmeye yakındır; müşteri aranmalıdır.
    /// </summary>
    public static class TeklifTakibi
    {
        /// <summary>Bu kadar gün cevapsız kalan teklif için müşteri aranmalı.</summary>
        public const int TakipGunu = 14;

        public static int BeklemeGunu(KayitliTeklif t, DateTime bugun) => Math.Max(0, (int)(bugun.Date - t.Tarih.Date).TotalDays);

        public static bool TakipGerekli(KayitliTeklif t, DateTime bugun) => t.Sonuc == "Bekliyor" && BeklemeGunu(t, bugun) >= TakipGunu;

        public static List<SatisciOzeti> Satiscilar(IEnumerable<KayitliTeklif> teklifler, DateTime bugun) =>
            teklifler.GroupBy(t => string.IsNullOrEmpty(t.Hazirlayan) ? "(bilinmiyor)" : t.Hazirlayan)
                .Select(g => new SatisciOzeti
                {
                    Hazirlayan = g.Key, Teklif = g.Count(), Kabul = g.Count(t => t.Sonuc == "Kabul"), Red = g.Count(t => t.Sonuc == "Red"),
                    Bekleyen = g.Count(t => t.Sonuc == "Bekliyor"), TakipGereken = g.Count(t => TakipGerekli(t, bugun)),
                    KazanilanTL = g.Where(t => t.Sonuc == "Kabul").Sum(t => t.TeklifTL), BekleyenTL = g.Where(t => t.Sonuc == "Bekliyor").Sum(t => t.TeklifTL),
                })
                .OrderByDescending(s => s.KazanilanTL).ThenBy(s => s.Hazirlayan).ToList();
    }
}
