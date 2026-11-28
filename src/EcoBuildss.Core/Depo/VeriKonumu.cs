using System;
using System.IO;

namespace EcoBuildss.Core.Depo
{
    /// <summary>
    /// Veritabanının yeri. Sırasıyla:
    ///  1) Yöneticinin seçtiği klasör (ağ paylaşımı olabilir) — ortak klasördeki veri_konumu.txt dosyasında yazılıdır;
    ///  2) bu bilgisayarın ortak klasörü (C:\ProgramData\EcoBuildss) — bilgisayardaki bütün Windows kullanıcıları aynı veriyi görür;
    ///  3) yazılamıyorsa kullanıcının kendi klasörü (%AppData%\EcoBuildss).
    /// Eski sürümde kullanıcı klasöründe kalan veritabanı, ortak klasör boşsa oraya kopyalanır (eskisi silinmez).
    /// </summary>
    public static class VeriKonumu
    {
        public const string DbAdi = "ecobuildss.db", KonumDosyasi = "veri_konumu.txt";

        public sealed class Sonuc
        {
            public string Klasor = "";
            public string? Tasindi;      // eski konumdan kopyalandıysa eski klasör
            public bool Secilmis;         // yöneticinin seçtiği klasör mü
        }

        public static Sonuc Bul(string ortak, string kisisel)
        {
            string konum = Path.Combine(ortak, KonumDosyasi);
            if (File.Exists(konum))
            {
                string k = File.ReadAllText(konum).Trim();
                if (k.Length > 0)
                {
                    if (!Path.IsPathRooted(k) || k.IndexOfAny(Path.GetInvalidPathChars()) >= 0) throw new IOException($"Veri konumu dosyası geçersiz: {konum}");
                    try { Directory.CreateDirectory(k); }
                    catch (Exception e) { throw new IOException($"Seçilen veri klasörüne ulaşılamıyor: {k}\n({e.Message})\nAğ bağlantısını kontrol edin.", e); }
                    if (!File.Exists(Path.Combine(k, DbAdi)) && !Yazilabilir(k)) throw new IOException($"Seçilen veri klasörüne yazılamıyor: {k}");
                    return new Sonuc { Klasor = k, Secilmis = true };
                }
            }
            if (Yazilabilir(ortak))
            {
                var s = new Sonuc { Klasor = ortak };
                string eski = Path.Combine(kisisel, DbAdi), yeni = Path.Combine(ortak, DbAdi);
                if (!File.Exists(yeni) && File.Exists(eski))
                {
                    foreach (var ek in new[] { "", "-wal", "-shm" })
                        if (File.Exists(eski + ek)) File.Copy(eski + ek, yeni + ek);
                    s.Tasindi = kisisel;
                }
                return s;
            }
            Directory.CreateDirectory(kisisel);
            return new Sonuc { Klasor = kisisel };
        }

        /// <summary>Yönetici veri klasörünü seçer (null = bu bilgisayarın ortak klasörü).</summary>
        public static void Ayarla(string ortak, string? klasor)
        {
            Directory.CreateDirectory(ortak);
            string konum = Path.Combine(ortak, KonumDosyasi);
            if (string.IsNullOrWhiteSpace(klasor)) { if (File.Exists(konum)) File.Delete(konum); return; }
            if (!Path.IsPathRooted(klasor)) throw new ArgumentException("Tam klasör yolu girilmeli.");
            File.WriteAllText(konum, klasor!.Trim());
        }

        public static bool Yazilabilir(string klasor)
        {
            try
            {
                Directory.CreateDirectory(klasor);
                string t = Path.Combine(klasor, $".yazma_denemesi_{Guid.NewGuid():N}");
                File.WriteAllText(t, "");
                File.Delete(t);
                return true;
            }
            catch { return false; }
        }
    }
}
