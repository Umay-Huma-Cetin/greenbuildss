using System;

namespace EcoBuildss.Core
{
    /// <summary>MGM derece-gün tablosundaki bir il.</summary>
    public sealed class Il
    {
        public string Ad = "";
        public double IsitmaDG, SogutmaDG;   // yıllık ısıtma (18/15 °C) ve soğutma (22 °C) derece-gün
        public int? Ts825Bolge;              // il merkezinin TS 825:2024 bölgesi (il_bolgeleri.csv)
        public string BolgeKaynagi = "";
        public override string ToString() => Ad;
    }

    /// <summary>TS 825:2024 iklim bölgesinin U değeri sınırları (W/m²K).</summary>
    public sealed class Ts825Bolge
    {
        public int No;
        public double UDuvar, UCati, UZemin, UPencere;
        public override string ToString() => $"{No}. bölge";
    }
}
