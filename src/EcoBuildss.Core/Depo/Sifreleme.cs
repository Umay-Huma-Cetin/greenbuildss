using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EcoBuildss.Core.Depo
{
    /// <summary>
    /// Alan düzeyinde şifreleme: AES-256-CBC + HMAC-SHA256 (önce şifrele, sonra doğrula), her değer için rastgele IV.
    /// Veri anahtarı (64 bayt: 32 şifreleme + 32 doğrulama) rastgele üretilir ve hiçbir zaman düz olarak saklanmaz;
    /// her kullanıcının parolasından PBKDF2 ile türetilen anahtarla ayrı ayrı sarılır (zarf şifreleme).
    /// </summary>
    public static class Sifreleme
    {
        public const string Onek = "e1:";
        public const int AnahtarBayt = 64, KekTekrar = 600_000;

        public static byte[] YeniAnahtar() { var k = new byte[AnahtarBayt]; using (var r = RandomNumberGenerator.Create()) r.GetBytes(k); return k; }
        public static byte[] RastgeleBayt(int n) { var k = new byte[n]; using (var r = RandomNumberGenerator.Create()) r.GetBytes(k); return k; }

        public static bool Sifreli(string? s) => s != null && s.StartsWith(Onek, StringComparison.Ordinal);

        public static string Sifrele(byte[] anahtar, string duz) => Onek + Convert.ToBase64String(SifreleBayt(anahtar, Encoding.UTF8.GetBytes(duz)));

        /// <summary>Şifreli değeri çözer. Önekli olmayan (eski sürümden kalma düz) değer olduğu gibi döner. Değer değiştirilmişse hata verir.</summary>
        public static string Coz(byte[] anahtar, string s) => !Sifreli(s) ? s : Encoding.UTF8.GetString(CozBayt(anahtar, Convert.FromBase64String(s.Substring(Onek.Length))));

        public static byte[] SifreleBayt(byte[] anahtar, byte[] duz)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            aes.Key = anahtar.Take(32).ToArray(); aes.GenerateIV();
            byte[] ct;
            using (var e = aes.CreateEncryptor()) ct = e.TransformFinalBlock(duz, 0, duz.Length);
            var govde = aes.IV.Concat(ct).ToArray();
            using var h = new HMACSHA256(anahtar.Skip(32).Take(32).ToArray());
            return govde.Concat(h.ComputeHash(govde)).ToArray();
        }

        public static byte[] CozBayt(byte[] anahtar, byte[] veri)
        {
            if (veri.Length < 16 + 16 + 32) throw new CryptographicException("Şifreli değer bozuk.");
            var govde = veri.Take(veri.Length - 32).ToArray(); var etiket = veri.Skip(veri.Length - 32).ToArray();
            using (var h = new HMACSHA256(anahtar.Skip(32).Take(32).ToArray()))
            {
                var beklenen = h.ComputeHash(govde); int fark = 0;
                for (int i = 0; i < 32; i++) fark |= beklenen[i] ^ etiket[i];
                if (fark != 0) throw new CryptographicException("Şifreli değer değiştirilmiş ya da anahtar yanlış.");
            }
            using var aes = Aes.Create();
            aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            aes.Key = anahtar.Take(32).ToArray(); aes.IV = govde.Take(16).ToArray();
            using var d = aes.CreateDecryptor();
            return d.TransformFinalBlock(govde, 16, govde.Length - 16);
        }

        /// <summary>Paroladan (ya da kurtarma anahtarından) anahtar sarma anahtarı: PBKDF2-HMAC-SHA256, 600.000 tekrar, 64 bayt.</summary>
        public static byte[] Turet(string parola, byte[] tuz, int tekrar = KekTekrar)
        {
            using var k = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(parola), tuz, tekrar, HashAlgorithmName.SHA256);
            return k.GetBytes(AnahtarBayt);
        }

        /// <summary>İnsanın yazabileceği kurtarma anahtarı: 25 karakter, 5'li gruplar (karışan harfler yok).</summary>
        public static string KurtarmaAnahtari()
        {
            const string abc = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var b = RastgeleBayt(25);
            var s = new string(b.Select(x => abc[x % abc.Length]).ToArray());
            return string.Join("-", Enumerable.Range(0, 5).Select(i => s.Substring(i * 5, 5)));
        }

        public static string KurtarmaSade(string s) => new string(s.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}
