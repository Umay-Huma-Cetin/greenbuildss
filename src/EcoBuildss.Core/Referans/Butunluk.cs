using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EcoBuildss.Core
{
    /// <summary>
    /// Referans veri bütünlüğü: fiyat, iklim ve yönetmelik dosyalarının SHA-256 özetleri BUTUNLUK.sha256 dosyasında tutulur.
    /// Program açılırken denetlenir; bir dosya elle değiştirilmişse (ör. fiyat oynanmışsa) uyarı verilir ve güvenlik günlüğüne yazılır.
    /// Satır sonları (CRLF/LF) özetten önce eşitlenir; Windows'ta git'in satır sonu dönüşümü yanlış alarm vermez.
    /// </summary>
    public static class ReferansButunluk
    {
        public const string Dosya = "BUTUNLUK.sha256";

        public static string Ozet(string yol)
        {
            var metin = File.ReadAllText(yol, Encoding.UTF8).Replace("\r\n", "\n");
            using var h = SHA256.Create();
            return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(metin))).Replace("-", "").ToLowerInvariant();
        }

        public static void Yaz(string klasor)
        {
            var satirlar = Directory.GetFiles(klasor, "*.csv").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
                .Select(f => $"{Ozet(f)}  {Path.GetFileName(f)}");
            File.WriteAllText(Path.Combine(klasor, Dosya), string.Join("\n", satirlar) + "\n");
        }

        /// <summary>Sorun yoksa boş liste; varsa değişen, eksik ya da listede olmayan dosyalar.</summary>
        public static List<string> Denetle(string klasor)
        {
            var sorun = new List<string>();
            string m = Path.Combine(klasor, Dosya);
            if (!File.Exists(m)) { sorun.Add($"{Dosya} bulunamadı"); return sorun; }
            var beklenen = File.ReadAllLines(m).Where(s => s.Trim().Length > 0).Select(s => s.Split(new[] { "  " }, 2, StringSplitOptions.None))
                               .ToDictionary(p => p[1].Trim(), p => p[0].Trim());
            foreach (var kv in beklenen)
            {
                string f = Path.Combine(klasor, kv.Key);
                if (!File.Exists(f)) sorun.Add($"{kv.Key} eksik");
                else if (Ozet(f) != kv.Value) sorun.Add($"{kv.Key} değiştirilmiş");
            }
            foreach (var f in Directory.GetFiles(klasor, "*.csv").Select(Path.GetFileName))
                if (!beklenen.ContainsKey(f!)) sorun.Add($"{f} listede yok");
            return sorun;
        }
    }
}
