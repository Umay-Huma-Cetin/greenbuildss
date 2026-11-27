namespace EcoBuildss.Core
{
    /// <summary>
    /// Mantolama öncesi deprem güvenliği uyarısı (bina yapım dönemine göre).
    /// Kaynaklar: 4708 sayılı Yapı Denetimi Hakkında Kanun (RG 13.07.2001) — öncesinde inşaatlar bağımsız denetimden geçmedi;
    /// İBB'nin ücretsiz hızlı taramasının 2000 öncesi binalarla sınırlı tutulması; 6306 sayılı Afet Riski Altındaki Alanların
    /// Dönüştürülmesi Hakkında Kanun — riskli yapı tespitini malik başvurusuyla lisanslı kuruluşlar yapar.
    /// Mantolama binanın ömrünü uzatan bir yatırımdır; yıkılması gerekebilecek binaya yapılması para kaybıdır.
    /// </summary>
    public static class Deprem
    {
        public static readonly string[] Donemler = { "Bilinmiyor", "2001 öncesi", "2001–2018", "2019 ve sonrası" };

        /// <summary>Uyarı metni; gerek yoksa boş.</summary>
        public static string Uyari(string donem) => donem switch
        {
            "2001 öncesi" => "Deprem: 2001 öncesi bina (yapı denetimi yok). Mantolamadan önce riskli yapı tespiti (6306 sayılı Kanun) yaptırılmalı.",
            _ => "",
        };

        public static bool UyariVar(string donem) => Uyari(donem).Length > 0;
    }
}
