namespace EcoBuildss.Core
{
    /// <summary>
    /// Müşteri önceliğinin AHP ikili karşılaştırmalarına çevrilmesi (Saaty 1–9 ölçeği).
    /// Üç hazır öncelikte de matris tam tutarlıdır (CR = 0).
    /// </summary>
    public static class Oncelikler
    {
        public static void Uygula(Girdi g, string oncelik)
        {
            switch (oncelik)
            {
                case "Bütçe":            g.MaliyetEnerji = 5; g.MaliyetKarbon = 5; g.EnerjiKarbon = 1; break;   // maliyet %71, enerji %14, karbon %14
                case "Enerji ve karbon": g.MaliyetEnerji = 1.0 / 3; g.MaliyetKarbon = 1.0 / 3; g.EnerjiKarbon = 1; break;   // %14, %43, %43
                default:                 g.MaliyetEnerji = 2; g.MaliyetKarbon = 2; g.EnerjiKarbon = 1; break;   // Dengeli: %50, %25, %25
            }
        }
    }
}
