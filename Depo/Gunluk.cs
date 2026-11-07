namespace EcoBuildss.Depo
{
    /// <summary>
    /// Arka plan işlem günlüğü: uygulamanın açılıp kapanması, veri yüklemesi, teklif işlemleri ve hatalar
    /// veritabanındaki islem_kayitlari tablosuna yazılır. Kullanıcıya gösterilmez; yalnız veritabanından incelenir.
    /// </summary>
    public static class Gunluk
    {
        public static Veritabani? Db { get; set; }
        public static void Bilgi(string islem, string ayrinti = "") => Db?.Kaydet("Bilgi", islem, ayrinti);
        public static void Uyari(string islem, string ayrinti = "") => Db?.Kaydet("Uyarı", islem, ayrinti);
        public static void Hata(string islem, System.Exception ex) => Db?.Kaydet("Hata", islem, ex.ToString());
        public static void Hata(string islem, string ayrinti) => Db?.Kaydet("Hata", islem, ayrinti);
    }
}
