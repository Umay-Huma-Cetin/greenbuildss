using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EcoBuildss.Core.Veri
{
    public enum AlanTuru { Metin, Sayi, Tamsayi, Tarih, Secenek }

    /// <summary>Bir CSV sütununun beklenen biçimi. İçe aktarmada her hücre buna göre denetlenir.</summary>
    public sealed class Alan
    {
        public string Ad = "", Aciklama = "", Birim = "";
        public AlanTuru Tur = AlanTuru.Metin;
        public bool Zorunlu = true;
        public double Min = double.NegativeInfinity, Max = double.PositiveInfinity;
        public string[] Secenekler = Array.Empty<string>();
        public string Ornek = "";
    }

    /// <summary>Bir veri dosyasının şeması (sütunlar ve satırlar arası kurallar).</summary>
    public sealed class Sema
    {
        public string Ad = "", DosyaAdi = "", Aciklama = "";
        public List<Alan> Alanlar = new();
        /// <summary>Satır düzeyinde ek kural: hata metni döner, sorun yoksa null.</summary>
        public Func<Kayit, string?>? SatirKurali;
        public string? AnahtarAlan;   // benzersiz olması gereken sütun

        /// <summary>Kullanıcıya verilecek boş şablon: başlık satırı + bir örnek satır.</summary>
        public string SablonCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Alanlar.Select(a => a.Ad)));
            sb.AppendLine(string.Join(",", Alanlar.Select(a => Csv.Kacis(a.Ornek))));
            return sb.ToString();
        }
    }

    /// <summary>Denetimden geçmiş bir satır. Sayılar double, tarihler DateTime, diğerleri string olarak tutulur.</summary>
    public sealed class Kayit
    {
        public int SatirNo;
        public Dictionary<string, object?> Deger = new(StringComparer.OrdinalIgnoreCase);
        public double Sayi(string ad) => Deger.TryGetValue(ad, out var v) && v is double d ? d : double.NaN;
        public double? SayiVeyaBos(string ad) => Deger.TryGetValue(ad, out var v) && v is double d ? d : (double?)null;
        public string Metin(string ad) => Deger.TryGetValue(ad, out var v) && v != null ? v.ToString()! : "";
        public DateTime Tarih(string ad) => Deger.TryGetValue(ad, out var v) && v is DateTime t ? t : DateTime.MinValue;
    }

    public sealed class Bulgu
    {
        public int Satir; public string Sutun = "", Mesaj = "";
        public bool Hata = true;   // false = uyarı (satır yine alınır)
        public override string ToString() => $"{(Satir > 0 ? $"Satır {Satir}" : "Dosya")}{(Sutun.Length > 0 ? $", {Sutun}" : "")}: {Mesaj}";
    }

    public sealed class IceAktarmaSonucu
    {
        public List<Kayit> Kayitlar = new();
        public List<Bulgu> Bulgular = new();
        public int OkunanSatir;
        public int Reddedilen => OkunanSatir - Kayitlar.Count;
        public bool DosyaGecersiz;   // başlık ya da biçim hatası: hiç satır alınmadı
        public string Ozet => DosyaGecersiz
            ? "Dosya okunamadı: " + string.Join("; ", Bulgular.Where(b => b.Satir == 0).Select(b => b.Mesaj))
            : $"{OkunanSatir} satır okundu, {Kayitlar.Count} kabul edildi, {Reddedilen} reddedildi ({Bulgular.Count(b => !b.Hata)} uyarı).";
    }

    /// <summary>
    /// Gerçek veriyi içe aktarır: ayırıcı (virgül / noktalı virgül) ve ondalık biçimi (1.234,5 ya da 1234.5) otomatik tanınır,
    /// başlıklar büyük-küçük harf ve Türkçe karakterden bağımsız eşleşir, her hücre şemaya göre denetlenir.
    /// Hatalı satır alınmaz ve nedeni satır/sütun bazında raporlanır; veri sessizce düzeltilmez.
    /// </summary>
    public static class IceAktarici
    {
        public const long AzamiDosya = 50L * 1024 * 1024;   // 50 MB: on binlerce satır için fazlasıyla yeter, belleği tüketmez

        public static IceAktarmaSonucu Dosyadan(string yol, Sema sema)
        {
            if (!File.Exists(yol)) return new IceAktarmaSonucu { DosyaGecersiz = true, Bulgular = { new Bulgu { Mesaj = "Dosya bulunamadı." } } };
            if (new FileInfo(yol).Length > AzamiDosya) return new IceAktarmaSonucu { DosyaGecersiz = true, Bulgular = { new Bulgu { Mesaj = $"Dosya çok büyük (en çok {AzamiDosya / 1024 / 1024} MB)." } } };
            return Metinden(File.ReadAllText(yol, Encoding.UTF8), sema);
        }

        public static IceAktarmaSonucu Metinden(string metin, Sema sema)
        {
            var s = new IceAktarmaSonucu();
            var satirlar = Csv.Ayristir(metin, out char ayirici);
            if (satirlar.Count == 0) { s.DosyaGecersiz = true; s.Bulgular.Add(new Bulgu { Mesaj = "Dosya boş." }); return s; }
            bool virgulOndalik = ayirici == ';';
            var baslik = satirlar[0].Select(Csv.Sade).ToList();
            var konum = new Dictionary<string, int>();
            foreach (var a in sema.Alanlar)
            {
                int i = baslik.IndexOf(Csv.Sade(a.Ad));
                if (i >= 0) konum[a.Ad] = i;
                else if (a.Zorunlu) s.Bulgular.Add(new Bulgu { Sutun = a.Ad, Mesaj = $"Zorunlu sütun yok: \"{a.Ad}\" ({a.Aciklama})." });
            }
            if (s.Bulgular.Count > 0) { s.DosyaGecersiz = true; return s; }
            foreach (var fazla in satirlar[0].Where(b => !sema.Alanlar.Any(a => Csv.Sade(a.Ad) == Csv.Sade(b))))
                s.Bulgular.Add(new Bulgu { Hata = false, Sutun = fazla, Mesaj = "Şemada olmayan sütun; kullanılmadı." });

            var anahtarlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int r = 1; r < satirlar.Count; r++)
            {
                var hucre = satirlar[r];
                if (hucre.All(string.IsNullOrWhiteSpace)) continue;
                s.OkunanSatir++;
                var k = new Kayit { SatirNo = r + 1 };
                int hataSayisi = 0;
                foreach (var a in sema.Alanlar)
                {
                    string ham = konum.TryGetValue(a.Ad, out int i) && i < hucre.Count ? hucre[i].Trim() : "";
                    if (ham.Length == 0)
                    {
                        if (a.Zorunlu) { s.Bulgular.Add(new Bulgu { Satir = k.SatirNo, Sutun = a.Ad, Mesaj = "Boş bırakılamaz." }); hataSayisi++; }
                        k.Deger[a.Ad] = null;
                        continue;
                    }
                    string? hata = Donustur(a, ham, virgulOndalik, out object? deger);
                    if (hata != null) { s.Bulgular.Add(new Bulgu { Satir = k.SatirNo, Sutun = a.Ad, Mesaj = hata }); hataSayisi++; }
                    else k.Deger[a.Ad] = deger;
                }
                if (hataSayisi == 0 && sema.AnahtarAlan != null)
                {
                    string anahtar = k.Metin(sema.AnahtarAlan);
                    if (!anahtarlar.Add(anahtar)) { s.Bulgular.Add(new Bulgu { Satir = k.SatirNo, Sutun = sema.AnahtarAlan, Mesaj = $"Tekrarlanan kayıt: \"{anahtar}\"." }); hataSayisi++; }
                }
                if (hataSayisi == 0 && sema.SatirKurali?.Invoke(k) is string kural) { s.Bulgular.Add(new Bulgu { Satir = k.SatirNo, Mesaj = kural }); hataSayisi++; }
                if (hataSayisi == 0) s.Kayitlar.Add(k);
            }
            if (s.OkunanSatir == 0) s.Bulgular.Add(new Bulgu { Mesaj = "Başlık dışında satır yok.", Hata = false });
            return s;
        }

        static string? Donustur(Alan a, string ham, bool virgulOndalik, out object? deger)
        {
            deger = null;
            switch (a.Tur)
            {
                case AlanTuru.Sayi:
                case AlanTuru.Tamsayi:
                    if (!Csv.SayiOku(ham, virgulOndalik, out double d)) return $"Sayı değil: \"{ham}\".";
                    if (a.Tur == AlanTuru.Tamsayi && Math.Abs(d - Math.Round(d)) > 1e-9) return $"Tam sayı olmalı: \"{ham}\".";
                    if (d < a.Min || d > a.Max) return $"{d.ToString("0.###", Ortak.TR)} aralık dışında ({a.Min.ToString("0.###", Ortak.TR)} – {a.Max.ToString("0.###", Ortak.TR)}{(a.Birim.Length > 0 ? " " + a.Birim : "")}).";
                    deger = d; return null;
                case AlanTuru.Tarih:
                    string[] bicim = { "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy" };
                    if (!DateTime.TryParseExact(ham, bicim, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return $"Tarih tanınmadı: \"{ham}\" (yyyy-aa-gg ya da gg.aa.yyyy).";
                    deger = t; return null;
                case AlanTuru.Secenek:
                    var esles = a.Secenekler.FirstOrDefault(x => Csv.Sade(x) == Csv.Sade(ham));
                    if (esles == null) return $"Geçersiz değer \"{ham}\"; beklenen: {string.Join(" / ", a.Secenekler.Take(8))}{(a.Secenekler.Length > 8 ? " …" : "")}.";
                    deger = esles; return null;
                default:
                    deger = ham; return null;
            }
        }
    }

    /// <summary>RFC 4180 CSV yardımcıları.</summary>
    public static class Csv
    {
        public static List<List<string>> Ayristir(string metin, out char ayirici)
        {
            metin = metin.TrimStart('﻿');
            int ilkSatirSonu = metin.IndexOf('\n'); string ilk = ilkSatirSonu < 0 ? metin : metin.Substring(0, ilkSatirSonu);
            ayirici = ilk.Count(c => c == ';') > ilk.Count(c => c == ',') ? ';' : ',';
            var sonuc = new List<List<string>>(); var satir = new List<string>(); var sb = new StringBuilder(); bool tirnak = false;
            for (int i = 0; i < metin.Length; i++)
            {
                char c = metin[i];
                if (tirnak)
                {
                    if (c == '"') { if (i + 1 < metin.Length && metin[i + 1] == '"') { sb.Append('"'); i++; } else tirnak = false; }
                    else sb.Append(c);
                }
                else if (c == '"') tirnak = true;
                else if (c == ayirici) { satir.Add(sb.ToString()); sb.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < metin.Length && metin[i + 1] == '\n') i++;
                    satir.Add(sb.ToString()); sb.Clear(); sonuc.Add(satir); satir = new List<string>();
                }
                else sb.Append(c);
            }
            if (sb.Length > 0 || satir.Count > 0) { satir.Add(sb.ToString()); sonuc.Add(satir); }
            return sonuc;
        }

        /// <summary>"1.234,56", "1234,56", "1234.56" ve "1,234.56" biçimlerini okur.</summary>
        public static bool SayiOku(string s, bool virgulOndalik, out double d)
        {
            s = s.Replace(" ", "").Replace(" ", "");
            if (s.Contains(',') && s.Contains('.'))
                s = s.LastIndexOf(',') > s.LastIndexOf('.') ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
            else if (virgulOndalik && s.Contains('.') && System.Text.RegularExpressions.Regex.IsMatch(s, @"^[-+]?\d{1,3}(\.\d{3})+$"))
                s = s.Replace(".", "");   // noktalı virgüllü (Türkçe Excel) dosyada 2.350 = iki bin üç yüz elli
            else if (s.Contains(','))
                s = virgulOndalik || s.Count(c => c == ',') == 1 && s.Length - s.IndexOf(',') - 1 != 3 ? s.Replace(',', '.') : s.Replace(",", "");
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && !double.IsNaN(d) && !double.IsInfinity(d);
        }

        /// <summary>Başlık ve seçenek karşılaştırması için sadeleştirme: küçük harf, Türkçe karakter → ASCII, boşluk/tire → alt çizgi.</summary>
        public static string Sade(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s.Trim().ToLower(Ortak.TR))
                sb.Append(c switch { 'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', 'â' => 'a', 'î' => 'i', ' ' or '-' => '_', _ => c });
            return sb.ToString();
        }

        public static string Kacis(string s) => s.IndexOfAny(new[] { ',', '"', '\n', ';' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        public static string Sayi(double d, string bicim = "0.######") => d.ToString(bicim, CultureInfo.InvariantCulture);
    }
}
