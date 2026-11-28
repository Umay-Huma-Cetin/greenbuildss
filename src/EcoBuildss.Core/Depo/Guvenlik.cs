using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EcoBuildss.Core.Depo
{
    public enum Rol { Satis, Yonetici }
    public enum Seviye { Bilgi, Uyari, Hata, Guvenlik }
    public enum GirisSonucu { Basarili, HataliBilgi, Kilitli, Pasif, AnahtarYok }

    public sealed class Kullanici
    {
        public long Id;
        public string KullaniciAdi = "", AdSoyad = "";
        public Rol Rol = Rol.Satis;
        public bool Aktif = true;
        public DateTime? SonGiris;
        public bool ParolaDegismeli;   // yöneticinin verdiği geçici parola: ilk girişte değiştirilmeli
        public bool Yonetici => Rol == Rol.Yonetici;
        public string RolAdi => Rol == Rol.Yonetici ? "Yönetici" : "Satış";
        public override string ToString() => $"{AdSoyad} ({RolAdi})";
    }

    public sealed class GunlukKaydi
    {
        public long Id; public DateTime Tarih; public string Kullanici = "", Seviye = "", Islem = "", Ayrinti = "";
    }

    /// <summary>
    /// Parola saklama: PBKDF2-HMAC-SHA256, kullanıcıya özel 16 baytlık tuz, 600.000 tekrar (OWASP Parola Saklama Rehberi, 2023).
    /// Parolanın kendisi hiçbir yerde tutulmaz; karşılaştırma sabit sürede yapılır.
    /// </summary>
    public static class Parola
    {
        public const int Tekrar = 600_000, TuzBayt = 16, OzetBayt = 32, EnAzUzunluk = 8;

        public static string Ozetle(string parola, int tekrar = Tekrar)
        {
            var tuz = new byte[TuzBayt];
            using (var r = RandomNumberGenerator.Create()) r.GetBytes(tuz);
            return $"pbkdf2-sha256${tekrar}${Convert.ToBase64String(tuz)}${Convert.ToBase64String(Turet(parola, tuz, tekrar))}";
        }

        public static bool Dogrula(string parola, string kayit)
        {
            var p = kayit.Split('$');
            if (p.Length != 4 || p[0] != "pbkdf2-sha256" || !int.TryParse(p[1], out int tekrar)) return false;
            var beklenen = Convert.FromBase64String(p[3]);
            var bulunan = Turet(parola, Convert.FromBase64String(p[2]), tekrar);
            int fark = beklenen.Length ^ bulunan.Length;
            for (int i = 0; i < Math.Min(beklenen.Length, bulunan.Length); i++) fark |= beklenen[i] ^ bulunan[i];   // sabit süreli karşılaştırma
            return fark == 0;
        }

        static byte[] Turet(string parola, byte[] tuz, int tekrar)
        {
            using var k = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(parola), tuz, tekrar, HashAlgorithmName.SHA256);
            return k.GetBytes(OzetBayt);
        }

        /// <summary>Parola kuralı: en az 8 karakter, en az bir harf ve bir rakam. Uygunsa null, değilse neden.</summary>
        // Sık kullanılan parolaların harf kısmı (rakam eklenerek kullanılırlar: parola123, qwerty2024…)
        static readonly HashSet<string> Yaygin = new(StringComparer.Ordinal)
        {
            "parola", "sifre", "şifre", "password", "passw", "qwerty", "qwe", "asdf", "asd", "abc", "abcd", "admin", "yonetici", "yönetici",
            "deneme", "test", "kullanici", "kullanıcı", "galatasaray", "fenerbahce", "fenerbahçe", "besiktas", "beşiktaş", "trabzonspor",
            "iloveyou", "welcome", "letmein", "ecobuildss", "mantolama", "a", "aa", "aaa", "aaaa",
        };

        public static string? Kural(string parola, string kullaniciAdi)
        {
            if (parola.Length < EnAzUzunluk) return $"Parola en az {EnAzUzunluk} karakter olmalı.";
            if (!parola.Any(char.IsLetter) || !parola.Any(char.IsDigit)) return "Parolada en az bir harf ve bir rakam olmalı.";
            if (kullaniciAdi.Length > 0 && parola.IndexOf(kullaniciAdi, StringComparison.OrdinalIgnoreCase) >= 0) return "Parola kullanıcı adını içermemeli.";
            if (parola.Length > 128) return "Parola en çok 128 karakter olabilir.";
            string sade = new string(parola.ToLowerInvariant().Where(char.IsLetter).ToArray());
            if (Yaygin.Contains(sade) || parola.Distinct().Count() < 4) return "Bu parola çok kolay tahmin edilir; başka bir parola seçin.";
            return null;
        }
    }

    public sealed partial class Veritabani
    {
        public const int AzamiHataliDeneme = 5;
        public static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(15);
        public const int YedekSaklamaGun = 14;

        /// <summary>Oturum açmış kullanıcı. Oturum yokken veriyi değiştiren hiçbir işlem yapılamaz.</summary>
        public Kullanici? Aktif { get; private set; }
        public int KullaniciSayisi => (int)(long)b.Sorgu("SELECT COUNT(*) AS n FROM kullanicilar")[0]["n"]!;

        void GuvenlikKur()
        {
            b.Calistir(@"
CREATE TABLE IF NOT EXISTS kullanicilar (
  id INTEGER PRIMARY KEY AUTOINCREMENT, kullanici_adi TEXT NOT NULL UNIQUE COLLATE NOCASE, ad_soyad TEXT NOT NULL,
  rol TEXT NOT NULL CHECK (rol IN ('Yönetici','Satış')), parola TEXT NOT NULL, aktif INTEGER NOT NULL DEFAULT 1,
  hatali_deneme INTEGER NOT NULL DEFAULT 0, kilit_bitis TEXT, son_giris TEXT, olusturma TEXT NOT NULL)");
            b.Calistir("CREATE TABLE IF NOT EXISTS anahtarlar (sahip TEXT PRIMARY KEY, tuz TEXT NOT NULL, sarili TEXT NOT NULL)");
            if (!b.Sorgu("PRAGMA table_info(teklifler)").Any(r => (string?)r["name"] == "gizli"))
                b.Calistir("ALTER TABLE teklifler ADD COLUMN gizli TEXT NOT NULL DEFAULT ''");
            var sutun = b.Sorgu("PRAGMA table_info(islem_gunlugu)").Select(r => (string)r["name"]!).ToList();
            if (!b.Sorgu("PRAGMA table_info(kullanicilar)").Any(r => (string?)r["name"] == "parola_degismeli"))
                b.Calistir("ALTER TABLE kullanicilar ADD COLUMN parola_degismeli INTEGER NOT NULL DEFAULT 0");
            if (!sutun.Contains("kullanici")) b.Calistir("ALTER TABLE islem_gunlugu ADD COLUMN kullanici TEXT NOT NULL DEFAULT ''");
            if (!sutun.Contains("seviye")) b.Calistir("ALTER TABLE islem_gunlugu ADD COLUMN seviye TEXT NOT NULL DEFAULT 'Bilgi'");
            if (!sutun.Contains("ozet"))
            {
                // Eski kayıtlar zincire bir kez bağlanır
                b.Calistir("ALTER TABLE islem_gunlugu ADD COLUMN ozet TEXT NOT NULL DEFAULT ''");
                string onceki = "";
                foreach (var r in b.Sorgu("SELECT id, tarih, kullanici, seviye, islem, ayrinti FROM islem_gunlugu ORDER BY id"))
                {
                    onceki = ZincirOzeti(onceki, (long)r["id"]!, S(r["tarih"]), S(r["kullanici"]), S(r["seviye"]), S(r["islem"]), S(r["ayrinti"]));
                    b.Yaz("UPDATE islem_gunlugu SET ozet = @o WHERE id = @id", new Dictionary<string, object?> { ["o"] = onceki, ["id"] = r["id"] });
                }
            }
        }

        // ======================= İşlem günlüğü (değiştirilemez zincir) =======================
        /// <summary>
        /// Her kayıt bir öncekinin özetini içeren SHA-256 özetiyle zincirlenir. Bir kayıt sonradan değiştirilir ya da silinirse
        /// GunlukDogrula() zincirin koptuğu yeri gösterir.
        /// </summary>
        public void Gunluk(string islem, string ayrinti = "", Seviye seviye = Seviye.Bilgi)
        {
            string tarih = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string kim = Aktif?.KullaniciAdi ?? "(oturum yok)", sev = SeviyeAdi(seviye);
            if (dek != null) ayrinti = Sifreleme.Sifrele(dek, ayrinti);   // ayrıntıda müşteri adı ve tutar olabilir
            b.Islem(() =>
            {
                var son = b.Sorgu("SELECT id, ozet FROM islem_gunlugu ORDER BY id DESC LIMIT 1");
                string onceki = son.Count == 0 ? "" : S(son[0]["ozet"]);
                long id = son.Count == 0 ? 1 : (long)son[0]["id"]! + 1;
                string ozet = ZincirOzeti(onceki, id, tarih, kim, sev, islem, ayrinti);
                b.Yaz("INSERT INTO islem_gunlugu (id, tarih, kullanici, seviye, islem, ayrinti, ozet) VALUES (@id, @t, @k, @s, @i, @a, @o)",
                      new Dictionary<string, object?> { ["id"] = id, ["t"] = tarih, ["k"] = kim, ["s"] = sev, ["i"] = islem, ["a"] = ayrinti, ["o"] = ozet });
                if (dek != null) MuhurYaz(id, ozet);
            });
        }

        // ---- Günlük mühürü ----
        // SHA-256 zinciri anahtarsızdır: dosyaya erişen biri zinciri baştan yeniden hesaplayabilir ya da son kayıtları silebilir.
        // Bu yüzden oturum açıkken son kaydın özeti veri anahtarından türetilen anahtarla HMAC'lenir ("mühür").
        // Veri anahtarını bilmeyen biri geçerli mühür üretemez; denetim mühürlü kaydın hâlâ yerinde ve aynı olduğunu doğrular.
        const string MuhurAnahtari = "gunluk_muhur";

        string MuhurHesapla(long id, string ozet)
        {
            using var t = new HMACSHA256(dek!.Skip(32).Take(32).ToArray());
            var k = t.ComputeHash(Encoding.UTF8.GetBytes("gunluk-muhur-v1"));
            using var h = new HMACSHA256(k);
            return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(id.ToString(CultureInfo.InvariantCulture) + "\u001f" + ozet)));
        }

        static bool SabitZamanEsit(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int f = 0; for (int i = 0; i < a.Length; i++) f |= a[i] ^ b[i];
            return f == 0;
        }

        void MuhurYaz(long id, string ozet) =>
            b.Yaz("INSERT OR REPLACE INTO ayarlar (anahtar, deger) VALUES (@a, @d)", new Dictionary<string, object?>
            { ["a"] = MuhurAnahtari, ["d"] = id.ToString(CultureInfo.InvariantCulture) + "|" + MuhurHesapla(id, ozet) });

        /// <summary>Eski sürümden gelen ya da mühürü olmayan günlüğü oturum açılınca mühürler; bu durum günlüğe yazılır.</summary>
        void MuhurYoksaOlustur()
        {
            if (dek == null || b.Sorgu("SELECT 1 FROM ayarlar WHERE anahtar = @a", new Dictionary<string, object?> { ["a"] = MuhurAnahtari }).Count > 0) return;
            Gunluk("Günlük mühürü oluşturuldu", "önceki mühür bulunamadı", Seviye.Guvenlik);
        }

        static string SeviyeAdi(Seviye s) => s switch { Seviye.Uyari => "Uyarı", Seviye.Hata => "Hata", Seviye.Guvenlik => "Güvenlik", _ => "Bilgi" };

        static string ZincirOzeti(string onceki, long id, string tarih, string kim, string sev, string islem, string ayrinti)
        {
            using var h = SHA256.Create();
            var b = h.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\u001f", onceki, id.ToString(CultureInfo.InvariantCulture), tarih, kim, sev, islem, ayrinti)));
            return BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Günlük zincirini baştan doğrular. Bozuk kayıt yoksa null, varsa ilk bozuk kaydın sırası ve nedeni.</summary>
        public (int kayit, string? sorun) GunlukDogrula()
        {
            var (n, zs) = ZincirDogrula(b);
            if (zs != null) return (n, zs);
            var ihlal = b.Sorgu("SELECT deger FROM ayarlar WHERE anahtar = 'gunluk_ihlal'");
            if (ihlal.Count > 0) return (n, S(ihlal[0]["deger"]));
            if (dek != null && MuhurSorunu(b) is string ms) return (n, ms);
            return (n, null);
        }

        static (int kayit, string? sorun) ZincirDogrula(SqliteBaglanti c)
        {
            string onceki = ""; long beklenenId = 0; int n = 0;
            foreach (var r in c.Sorgu("SELECT id, tarih, kullanici, seviye, islem, ayrinti, ozet FROM islem_gunlugu ORDER BY id"))
            {
                n++;
                long id = (long)r["id"]!;
                if (beklenenId > 0 && id != beklenenId) return (n, $"#{beklenenId} numaralı kayıt silinmiş.");
                string ozet = ZincirOzeti(onceki, id, S(r["tarih"]), S(r["kullanici"]), S(r["seviye"]), S(r["islem"]), S(r["ayrinti"]));
                if (ozet != S(r["ozet"])) return (n, $"#{id} numaralı kayıt sonradan değiştirilmiş.");
                onceki = ozet; beklenenId = id + 1;
            }
            return (n, null);
        }

        /// <summary>Mühürlü son kaydın yerinde ve değişmemiş olduğunu denetler (veri anahtarı açıkken).</summary>
        string? MuhurSorunu() => MuhurSorunu(b);


        string? MuhurSorunu(SqliteBaglanti c, bool zorunlu = false)
        {
            var m = c.Sorgu("SELECT deger FROM ayarlar WHERE anahtar = @a", new Dictionary<string, object?> { ["a"] = MuhurAnahtari });
            if (m.Count == 0) return zorunlu ? "Günlük mühürü yok." : null;   // henüz mühür yok (eski sürüm); girişte oluşturulur
            var p = S(m[0]["deger"]).Split('|');
            if (p.Length != 2 || !long.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long mid)) return "Günlük mühürü bozuk.";
            var satir = c.Sorgu("SELECT ozet FROM islem_gunlugu WHERE id = @id", new Dictionary<string, object?> { ["id"] = mid });
            if (satir.Count == 0) return $"#{mid} ve sonrasındaki günlük kayıtları silinmiş.";
            if (!SabitZamanEsit(MuhurHesapla(mid, S(satir[0]["ozet"])), p[1])) return "Günlük sonradan baştan yazılmış (mühür tutmuyor).";
            return null;
        }

        /// <summary>
        /// Girişte, yeni kayıt mühürü ilerletmeden önce eski mühür denetlenir. Bozuksa bu kalıcı olarak işaretlenir:
        /// sonraki kayıtlar mühürü ilerletse de ihlal Denetle'de görünmeye devam eder.
        /// </summary>
        void MuhurGirisDenetimi()
        {
            if (MuhurSorunu() is not string sorun) return;
            string kayit = $"{sorun} (tespit: {DateTime.Now:dd.MM.yyyy HH:mm}, {Aktif?.KullaniciAdi} girişinde)";
            b.Yaz("INSERT OR REPLACE INTO ayarlar (anahtar, deger) VALUES ('gunluk_ihlal', @d)", new Dictionary<string, object?> { ["d"] = kayit });
            Gunluk("Günlük bütünlüğü bozuk", kayit, Seviye.Guvenlik);
        }

        public List<GunlukKaydi> GunlukOku(int n = 500, Seviye? seviye = null)
        {
            Yetki(yonetici: true, "İşlem günlüğünü görüntüleme");
            string kosul = seviye.HasValue ? "WHERE seviye = @s" : "";
            return b.Sorgu($"SELECT id, tarih, kullanici, seviye, islem, ayrinti FROM islem_gunlugu {kosul} ORDER BY id DESC LIMIT {n}",
                           new Dictionary<string, object?> { ["s"] = seviye.HasValue ? SeviyeAdi(seviye.Value) : null })
                .Select(r => new GunlukKaydi
                {
                    Id = (long)r["id"]!, Tarih = DateTime.ParseExact(S(r["tarih"]).Length > 19 ? S(r["tarih"]).Substring(0, 19) : S(r["tarih"]), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    Kullanici = S(r["kullanici"]), Seviye = S(r["seviye"]), Islem = S(r["islem"]), Ayrinti = Coz(S(r["ayrinti"])),
                }).ToList();
        }

        // ======================= Yetki =======================
        /// <summary>Oturum ve rol denetimi. Arayüz düğmeyi gizlese bile yetkisiz istek veritabanı katmanında reddedilir ve günlüğe yazılır.</summary>
        void Yetki(bool yonetici, string islem)
        {
            if (Aktif == null) { Gunluk("Yetkisiz istek", $"{islem} (oturum yok)", Seviye.Guvenlik); throw new UnauthorizedAccessException("Bu işlem için oturum açmalısınız."); }
            if (yonetici && !Aktif.Yonetici) { Gunluk("Yetkisiz istek", islem, Seviye.Guvenlik); throw new UnauthorizedAccessException($"{islem} yalnız yönetici yetkisiyle yapılabilir."); }
        }

        // ======================= Kullanıcılar ve oturum =======================
        /// <summary>İlk kurulum: hiç kullanıcı yokken ilk yönetici oluşturulur ve oturumu açılır.</summary>
        public Kullanici IlkYonetici(string kullaniciAdi, string adSoyad, string parola)
        {
            if (KullaniciSayisi > 0) throw new InvalidOperationException("İlk yönetici zaten oluşturulmuş.");
            var k = Ekle(kullaniciAdi, adSoyad, Rol.Yonetici, parola);
            AnahtarOlustur();
            AnahtarSar(k.KullaniciAdi, parola);
            Aktif = k;
            Gunluk("İlk kurulum", $"Yönetici hesabı oluşturuldu: {k.KullaniciAdi}", Seviye.Guvenlik);
            return k;
        }

        public Kullanici KullaniciEkle(string kullaniciAdi, string adSoyad, Rol rol, string parola)
        {
            Yetki(yonetici: true, "Kullanıcı ekleme");
            var k = Ekle(kullaniciAdi, adSoyad, rol, parola);
            b.Yaz("UPDATE kullanicilar SET parola_degismeli = 1 WHERE id = @id", new Dictionary<string, object?> { ["id"] = k.Id });
            AnahtarSar(k.KullaniciAdi, parola);
            Gunluk("Kullanıcı eklendi", $"{k.KullaniciAdi} · {k.RolAdi}", Seviye.Guvenlik);
            return k;
        }

        Kullanici Ekle(string kullaniciAdi, string adSoyad, Rol rol, string parola)
        {
            kullaniciAdi = kullaniciAdi.Trim(); adSoyad = adSoyad.Trim();
            kullaniciAdi = kullaniciAdi.ToLowerInvariant();
            if (kullaniciAdi.Length < 3 || kullaniciAdi.Length > 32 || !kullaniciAdi.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_'))
                throw new ArgumentException("Kullanıcı adı 3–32 karakter olmalı; yalnız İngilizce harf (ç, ğ, ı, ö, ş, ü olmadan), rakam, nokta ve alt çizgi.");
            if (adSoyad.Length == 0) throw new ArgumentException("Ad soyad boş olamaz.");
            if (Parola.Kural(parola, kullaniciAdi) is string k) throw new ArgumentException(k);
            if (b.Sorgu("SELECT 1 FROM kullanicilar WHERE kullanici_adi = @k COLLATE NOCASE", new Dictionary<string, object?> { ["k"] = kullaniciAdi }).Count > 0) throw new ArgumentException("Bu kullanıcı adı kullanılıyor.");
            long id = b.Yaz("INSERT INTO kullanicilar (kullanici_adi, ad_soyad, rol, parola, olusturma) VALUES (@k, @a, @r, @p, @o)", new Dictionary<string, object?>
            { ["k"] = kullaniciAdi, ["a"] = adSoyad, ["r"] = rol == Rol.Yonetici ? "Yönetici" : "Satış", ["p"] = Parola.Ozetle(parola), ["o"] = DateTime.Now });
            return new Kullanici { Id = id, KullaniciAdi = kullaniciAdi, AdSoyad = adSoyad, Rol = rol };
        }

        /// <summary>Giriş: 5 hatalı denemede hesap 15 dakika kilitlenir. Kullanıcı adının var olup olmadığı dışarıya belli edilmez.</summary>
        public GirisSonucu GirisYap(string kullaniciAdi, string parola)
        {
            if (Aktif != null) OturumKapat("yeni giriş");   // önceki oturum ve veri anahtarı, giriş başarısız olsa da kalmaz
            var r = b.Sorgu("SELECT * FROM kullanicilar WHERE kullanici_adi = @k COLLATE NOCASE", new Dictionary<string, object?> { ["k"] = kullaniciAdi.Trim() });
            if (r.Count == 0)
            {
                Parola.Dogrula(parola, Parola.Ozetle("zamanlama-esitleme", 1000));   // var olmayan kullanıcıda da benzer süre
                Gunluk("Başarısız giriş", $"bilinmeyen kullanıcı adı: {kullaniciAdi.Trim()}", Seviye.Guvenlik);
                return GirisSonucu.HataliBilgi;
            }
            var u = r[0]; long id = (long)u["id"]!; string ad = S(u["kullanici_adi"]);
            if ((long)u["aktif"]! == 0) { Gunluk("Başarısız giriş", $"{ad}: hesap devre dışı", Seviye.Guvenlik); return GirisSonucu.Pasif; }
            if (u["kilit_bitis"] is string kb && DateTime.ParseExact(kb, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) > DateTime.Now)
            { Gunluk("Başarısız giriş", $"{ad}: hesap kilitli", Seviye.Guvenlik); return GirisSonucu.Kilitli; }
            if (!Parola.Dogrula(parola, S(u["parola"])))
            {
                long deneme = (long)u["hatali_deneme"]! + 1;
                bool kilit = deneme >= AzamiHataliDeneme;
                b.Yaz("UPDATE kullanicilar SET hatali_deneme = @d, kilit_bitis = @kb WHERE id = @id", new Dictionary<string, object?>
                { ["d"] = kilit ? 0 : deneme, ["kb"] = kilit ? DateTime.Now.Add(KilitSuresi) : null, ["id"] = id });
                Gunluk("Başarısız giriş", kilit ? $"{ad}: {AzamiHataliDeneme} hatalı deneme, hesap {KilitSuresi.TotalMinutes:0} dakika kilitlendi" : $"{ad}: hatalı parola ({deneme}/{AzamiHataliDeneme})", Seviye.Guvenlik);
                return kilit ? GirisSonucu.Kilitli : GirisSonucu.HataliBilgi;
            }
            // Veri anahtarı bu kullanıcının parolasıyla açılır
            var kullanici = Oku(u);
            byte[]? anahtar = AnahtarAc(ad, parola);
            if (anahtar == null)
            {
                if (AnahtarSayisi == 0 && kullanici.Yonetici) { AnahtarOlustur(); AnahtarSar(ad, parola); anahtar = dek; }   // şifrelemesiz eski sürümden geçiş
                else { Gunluk("Başarısız giriş", $"{ad}: veri anahtarı yok, yönetici parolayı sıfırlamalı", Seviye.Guvenlik); return GirisSonucu.AnahtarYok; }
            }
            dek = anahtar;
            b.Yaz("UPDATE kullanicilar SET hatali_deneme = 0, kilit_bitis = NULL, son_giris = @t WHERE id = @id", new Dictionary<string, object?> { ["t"] = DateTime.Now, ["id"] = id });
            Aktif = kullanici;
            MuhurGirisDenetimi();
            Gunluk("Oturum açıldı", $"{Aktif.KullaniciAdi} · {Aktif.RolAdi}", Seviye.Guvenlik);
            TeklifSifrelemesiniTamamla();
            MuhurYoksaOlustur();
            return GirisSonucu.Basarili;
        }

        public void OturumKapat(string neden = "çıkış")
        {
            if (Aktif == null) return;
            Gunluk("Oturum kapandı", $"{Aktif.KullaniciAdi} · {neden}", Seviye.Guvenlik);
            Aktif = null;
            if (dek != null) { Array.Clear(dek, 0, dek.Length); dek = null; }   // anahtar bellekten silinir
        }

        public void ParolaDegistir(string eski, string yeni)
        {
            Yetki(yonetici: false, "Parola değiştirme");
            var u = b.Sorgu("SELECT parola FROM kullanicilar WHERE id = @id", new Dictionary<string, object?> { ["id"] = Aktif!.Id })[0];
            if (!Parola.Dogrula(eski, S(u["parola"]))) { Gunluk("Parola değiştirilemedi", $"{Aktif.KullaniciAdi}: eski parola hatalı", Seviye.Guvenlik); throw new ArgumentException("Mevcut parola hatalı."); }
            if (Parola.Kural(yeni, Aktif.KullaniciAdi) is string k) throw new ArgumentException(k);
            if (yeni == eski) throw new ArgumentException("Yeni parola eskisiyle aynı olamaz.");
            // Parola ve veri anahtarının sarılması birlikte yazılır: biri yarım kalırsa kullanıcı verisine erişimini kaybetmesin
            b.Islem(() =>
            {
                b.Yaz("UPDATE kullanicilar SET parola = @p, parola_degismeli = 0 WHERE id = @id", new Dictionary<string, object?> { ["p"] = Parola.Ozetle(yeni), ["id"] = Aktif.Id });
                AnahtarSar(Aktif.KullaniciAdi, yeni);
            });
            Aktif.ParolaDegismeli = false;
            Gunluk("Parola değiştirildi", Aktif.KullaniciAdi, Seviye.Guvenlik);
        }

        public void ParolaSifirla(long id, string yeni)
        {
            Yetki(yonetici: true, "Parola sıfırlama");
            var k = TumKullanicilar().First(x => x.Id == id);
            if (Parola.Kural(yeni, k.KullaniciAdi) is string m) throw new ArgumentException(m);
            b.Islem(() =>
            {
                b.Yaz("UPDATE kullanicilar SET parola = @p, hatali_deneme = 0, kilit_bitis = NULL, parola_degismeli = 1 WHERE id = @id", new Dictionary<string, object?> { ["p"] = Parola.Ozetle(yeni), ["id"] = id });
                AnahtarSar(k.KullaniciAdi, yeni);
            });
            Gunluk("Parola sıfırlandı", k.KullaniciAdi, Seviye.Guvenlik);
        }

        public void KullaniciGuncelle(long id, Rol rol, bool aktif)
        {
            Yetki(yonetici: true, "Kullanıcı yetkisi değiştirme");
            var k = TumKullanicilar().First(x => x.Id == id);
            bool sonYonetici = k.Yonetici && k.Aktif && TumKullanicilar().Count(x => x.Yonetici && x.Aktif) == 1;
            if (sonYonetici && (rol != Rol.Yonetici || !aktif)) throw new InvalidOperationException("Sistemde en az bir etkin yönetici kalmalı.");
            b.Yaz("UPDATE kullanicilar SET rol = @r, aktif = @a WHERE id = @id", new Dictionary<string, object?> { ["r"] = rol == Rol.Yonetici ? "Yönetici" : "Satış", ["a"] = aktif, ["id"] = id });
            Gunluk("Kullanıcı güncellendi", $"{k.KullaniciAdi}: {(rol == Rol.Yonetici ? "Yönetici" : "Satış")}, {(aktif ? "etkin" : "devre dışı")}", Seviye.Guvenlik);
        }

        /// <summary>Kullanıcı listesi (yönetici).</summary>
        public List<Kullanici> Kullanicilar()
        {
            Yetki(yonetici: true, "Kullanıcı listesini görüntüleme");
            return TumKullanicilar();
        }

        List<Kullanici> TumKullanicilar() => b.Sorgu("SELECT * FROM kullanicilar ORDER BY ad_soyad").Select(Oku).ToList();

        static Kullanici Oku(Dictionary<string, object?> u) => new()
        {
            Id = (long)u["id"]!, KullaniciAdi = S(u["kullanici_adi"]), AdSoyad = S(u["ad_soyad"]), Rol = S(u["rol"]) == "Yönetici" ? Rol.Yonetici : Rol.Satis,
            Aktif = (long)u["aktif"]! == 1, ParolaDegismeli = u.TryGetValue("parola_degismeli", out var pd) && pd is long l && l == 1, SonGiris = u["son_giris"] is string sg ? DateTime.ParseExact(sg, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : null,
        };

        // ======================= Şifreleme anahtarı =======================
        byte[]? dek;
        /// <summary>İlk kurulumda (ya da şifrelemesiz sürümden geçişte) üretilen kurtarma anahtarı: bir kez gösterilir, sonra silinir.</summary>
        public string? YeniKurtarmaAnahtari { get; private set; }
        public void KurtarmaAnahtariGosterildi() => YeniKurtarmaAnahtari = null;

        /// <summary>
        /// Yeni kurtarma anahtarı üretir (yönetici, kendi parolasıyla onaylar). Eski anahtar bu andan itibaren geçersizdir.
        /// Anahtar kaybolduysa ya da başkasının eline geçtiyse kullanılır.
        /// </summary>
        public string KurtarmaAnahtariYenile(string parola)
        {
            Yetki(yonetici: true, "Kurtarma anahtarı yenileme");
            var dogrulama = AnahtarAc(Aktif!.KullaniciAdi, parola);
            if (dogrulama == null) { Gunluk("Kurtarma anahtarı yenilenemedi", "parola hatalı", Seviye.Guvenlik); throw new UnauthorizedAccessException("Parola hatalı."); }
            Array.Clear(dogrulama, 0, dogrulama.Length);
            string kurtarma = Sifreleme.KurtarmaAnahtari();
            AnahtarSar("#kurtarma", Sifreleme.KurtarmaSade(kurtarma));
            YeniKurtarmaAnahtari = kurtarma;
            Gunluk("Kurtarma anahtarı yenilendi", "eski anahtar geçersiz", Seviye.Guvenlik);
            return kurtarma;
        }
        public bool Sifreli => dek != null;
        int AnahtarSayisi => (int)(long)b.Sorgu("SELECT COUNT(*) AS n FROM anahtarlar")[0]["n"]!;

        void AnahtarOlustur()
        {
            dek = Sifreleme.YeniAnahtar();
            string kurtarma = Sifreleme.KurtarmaAnahtari();
            AnahtarSar("#kurtarma", Sifreleme.KurtarmaSade(kurtarma));
            YeniKurtarmaAnahtari = kurtarma;
            MevcutVeriyiSifrele();
        }

        void AnahtarSar(string sahip, string sir)
        {
            if (dek == null) throw new InvalidOperationException("Veri anahtarı açık değil.");
            var tuz = Sifreleme.RastgeleBayt(16);
            var kek = Sifreleme.Turet(sir, tuz);
            b.Yaz("INSERT OR REPLACE INTO anahtarlar (sahip, tuz, sarili) VALUES (@s, @t, @w)", new Dictionary<string, object?>
            { ["s"] = sahip.ToLowerInvariant(), ["t"] = Convert.ToBase64String(tuz), ["w"] = Convert.ToBase64String(Sifreleme.SifreleBayt(kek, dek)) });
            Array.Clear(kek, 0, kek.Length);
        }

        byte[]? AnahtarAc(string sahip, string sir)
        {
            var r = b.Sorgu("SELECT tuz, sarili FROM anahtarlar WHERE sahip = @s", new Dictionary<string, object?> { ["s"] = sahip.ToLowerInvariant() });
            if (r.Count == 0) return null;
            var kek = Sifreleme.Turet(sir, Convert.FromBase64String(S(r[0]["tuz"])));
            try { return Sifreleme.CozBayt(kek, Convert.FromBase64String(S(r[0]["sarili"]))); }
            catch (CryptographicException) { return null; }
            finally { Array.Clear(kek, 0, kek.Length); }
        }

        /// <summary>Kurtarma anahtarıyla bir yöneticinin parolasını yeniler (parola unutulduğunda veriye erişimin tek yolu).</summary>
        public void KurtarmaIleSifirla(string kullaniciAdi, string kurtarmaAnahtari, string yeniParola)
        {
            var k = TumKullanicilar().FirstOrDefault(x => string.Equals(x.KullaniciAdi, kullaniciAdi.Trim(), StringComparison.OrdinalIgnoreCase));
            if (k == null || !k.Yonetici || !k.Aktif) { Gunluk("Kurtarma reddedildi", $"{kullaniciAdi.Trim()}: etkin yönetici değil", Seviye.Guvenlik); throw new ArgumentException("Kurtarma yalnız etkin bir yönetici hesabı için yapılabilir."); }
            if (Parola.Kural(yeniParola, k.KullaniciAdi) is string m) throw new ArgumentException(m);
            var acik = AnahtarAc("#kurtarma", Sifreleme.KurtarmaSade(kurtarmaAnahtari));
            if (acik == null) { Gunluk("Kurtarma reddedildi", $"{k.KullaniciAdi}: kurtarma anahtarı hatalı", Seviye.Guvenlik); throw new ArgumentException("Kurtarma anahtarı hatalı."); }
            var onceki = dek; dek = acik;
            try
            {
                b.Islem(() =>
                {
                    b.Yaz("UPDATE kullanicilar SET parola = @p, parola_degismeli = 0, hatali_deneme = 0, kilit_bitis = NULL WHERE id = @id", new Dictionary<string, object?> { ["p"] = Parola.Ozetle(yeniParola), ["id"] = k.Id });
                    AnahtarSar(k.KullaniciAdi, yeniParola);
                });
                Gunluk("Kurtarma anahtarıyla parola yenilendi", k.KullaniciAdi, Seviye.Guvenlik);
            }
            finally { Array.Clear(acik, 0, acik.Length); dek = onceki; }
        }

        string Sifrele(string s) => dek == null ? throw new UnauthorizedAccessException("Oturum açık değil.") : Sifreleme.Sifrele(dek, s);
        string Coz(string s)
        {
            if (!Sifreleme.Sifreli(s)) return s;
            if (dek == null) return "(şifreli)";
            try { return Sifreleme.Coz(dek, s); } catch (CryptographicException) { return "(çözülemedi: değiştirilmiş)"; }
        }

        /// <summary>Şifrelemesiz eski sürümden kalan teklif ve veri kayıtlarını şifreler (günlük kayıtları zincir bozulmasın diye olduğu gibi kalır).</summary>
        void MevcutVeriyiSifrele()
        {
            b.Islem(() =>
            {
                foreach (var r in b.Sorgu("SELECT veri_seti, anahtar, satir FROM veri_kayitlari"))
                    if (!Sifreleme.Sifreli(S(r["satir"])))
                        b.Yaz("UPDATE veri_kayitlari SET satir = @s WHERE veri_seti = @v AND anahtar = @a", new Dictionary<string, object?> { ["s"] = Sifrele(S(r["satir"])), ["v"] = r["veri_seti"], ["a"] = r["anahtar"] });
            });
            TeklifSifrelemesiniTamamla();
            EskiSayfalariTemizle();
        }

        /// <summary>Şifrelemeye geçişten sonra dosyada eski düz metin sayfası kalmasın: WAL boşaltılır, dosya yeniden yazılır.</summary>
        void EskiSayfalariTemizle()
        {
            try { b.Calistir("PRAGMA wal_checkpoint(TRUNCATE)"); b.Calistir("VACUUM"); b.Calistir("PRAGMA wal_checkpoint(TRUNCATE)"); }
            catch (Exception e) { Gunluk("Temizlik yapılamadı", e.Message, Seviye.Uyari); }
        }

        /// <summary>Eski sürümlerde düz ya da kısmen şifreli kalan teklif satırlarını tam şifreli biçime çevirir.</summary>
        void TeklifSifrelemesiniTamamla()
        {
            if (dek == null) return;
            var eski = TekliflerHam("WHERE il <> '' OR gizli = '' OR gizli NOT LIKE 'e1:%'");
            if (eski.Count == 0) return;
            b.Islem(() => { foreach (var t in eski) TeklifGizliYaz(t); });
            Gunluk("Teklifler şifrelendi", $"{eski.Count} teklif tam şifreli biçime çevrildi", Seviye.Guvenlik);
            EskiSayfalariTemizle();
        }

        static string GizliBirlestir(string musteri, double tl, double tlm2, double indirim, double? kabul) =>
            string.Join("\u001f", musteri, tl.ToString("R", CultureInfo.InvariantCulture), tlm2.ToString("R", CultureInfo.InvariantCulture),
                        indirim.ToString("R", CultureInfo.InvariantCulture), kabul?.ToString("R", CultureInfo.InvariantCulture) ?? "");

        // ======================= Yedekleme =======================
        public string YedekKlasoru => Path.Combine(Path.GetDirectoryName(Dosya)!, "yedek");

        /// <summary>Elle yedek (yönetici). Tutarlı kopya, veritabanı açıkken alınır.</summary>
        public string Yedekle()
        {
            Yetki(yonetici: true, "Yedek alma");
            return YedekAl("elle");
        }

        /// <summary>Günlük otomatik yedek: bugün yedek alınmadıysa alır, 14 günden eski otomatik yedekleri siler.</summary>
        public string? OtomatikYedek()
        {
            Directory.CreateDirectory(YedekKlasoru);
            string bugun = $"ecobuildss_{DateTime.Today:yyyyMMdd}_otomatik.db";
            if (File.Exists(Path.Combine(YedekKlasoru, bugun))) return null;
            string yol = YedekAl("otomatik", bugun);
            foreach (var f in Directory.GetFiles(YedekKlasoru, "ecobuildss_*_otomatik.db").Where(f => File.GetCreationTime(f) < DateTime.Today.AddDays(-YedekSaklamaGun)))
                try { File.Delete(f); } catch { }
            return yol;
        }

        string YedekAl(string tur, string? ad = null)
        {
            Directory.CreateDirectory(YedekKlasoru);
            string yol = Path.Combine(YedekKlasoru, ad ?? $"ecobuildss_{DateTime.Now:yyyyMMdd_HHmmss}_{tur}.db");
            b.Yedekle(yol);
            Gunluk("Yedek alındı", $"{tur}: {Path.GetFileName(yol)} ({new FileInfo(yol).Length / 1024} KB)");
            return yol;
        }

        /// <summary>Veritabanının tutarlı kopyasını verilen dosyaya yazar (yönetici; veri klasörü değiştirilirken).</summary>
        public void KopyaYaz(string hedefDosya)
        {
            Yetki(yonetici: true, "Veritabanını taşıma");
            if (File.Exists(hedefDosya)) throw new IOException("Hedefte zaten bir veritabanı var.");
            b.Yedekle(hedefDosya);
            Gunluk("Veritabanı kopyalandı", hedefDosya, Seviye.Guvenlik);
        }

        public List<FileInfo> Yedekler() => Directory.Exists(YedekKlasoru)
            ? new DirectoryInfo(YedekKlasoru).GetFiles("ecobuildss_*.db").OrderByDescending(f => f.LastWriteTime).ToList() : new List<FileInfo>();

        /// <summary>Yedekten geri yükleme (yönetici). Önce mevcut durumun yedeği alınır; geri yüklenen dosyanın günlük zinciri doğrulanır.</summary>
        public void GeriYukle(string yedekDosyasi)
        {
            Yetki(yonetici: true, "Yedekten geri yükleme");
            var kim = Aktif!;
            using (var kontrol = new SqliteBaglanti(yedekDosyasi, pragmasiz: true))
            {
                if (kontrol.Sorgu("SELECT name FROM sqlite_master WHERE type='table' AND name IN ('teklifler','islem_gunlugu','ayarlar')").Count < 3) throw new InvalidDataException("Seçilen dosya bir EcoBuildss yedeği değil.");
                // Yabancı ya da değiştirilmiş yedek reddedilir: zincir sağlam olmalı ve mühür bu firmanın veri anahtarıyla atılmış olmalı
                // (aksi hâlde ortak klasöre bırakılmış sahte bir veritabanı, kendi yönetici hesabıyla firmanın yerine geçebilirdi).
                string? sorun = ZincirDogrula(kontrol).sorun ?? MuhurSorunu(kontrol, zorunlu: true);
                if (sorun != null)
                {
                    Gunluk("Geri yükleme reddedildi", $"{Path.GetFileName(yedekDosyasi)}: {sorun}", Seviye.Guvenlik);
                    throw new InvalidDataException("Bu yedek bu firmanın veri anahtarıyla oluşturulmamış ya da sonradan değiştirilmiş: " + sorun);
                }
            }
            string onceki = YedekAl("geri-yukleme-oncesi");
            Gunluk("Yedekten geri yükleniyor", $"{Path.GetFileName(yedekDosyasi)} · önceki durum: {Path.GetFileName(onceki)}", Seviye.Guvenlik);
            b.Yedekle(yedekDosyasi, geriYukle: true);
            GuvenlikKur();
            // Yedekteki kullanıcılar ve anahtarlar geçerli olur: yeniden oturum açılmalı
            Aktif = null; if (dek != null) { Array.Clear(dek, 0, dek.Length); dek = null; }
            Gunluk("Yedekten geri yüklendi", $"{kim.KullaniciAdi}: {Path.GetFileName(yedekDosyasi)}; yeniden oturum açılmalı", Seviye.Guvenlik);
        }
    }
}
