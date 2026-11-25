using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EcoBuildss.Core.Ogrenme;
using EcoBuildss.Core.Uygulamalar;
using EcoBuildss.Core.Veri;

namespace EcoBuildss.Core.Sentetik
{
    /// <summary>
    /// ÖRNEK (SENTETİK) VERİ ÜRETECİ — gerçek veri yerine geçmez.
    /// Firmanın kendi faturası ve teklif kaydı henüz yokken uygulamanın bütün adımlarını denemek için, gerçek veriyle
    /// aynı şemada dosya üretir. Binalar gerçek il iklimi ve gerçek malzeme/fiyat verisiyle hesaplanır; davranış kısmı
    /// (kullanıcı alışkanlığı, müşteri kararı) aşağıda açıkça yazılı varsayımlarla ve gürültüyle üretilir.
    /// Gerçek veri içe aktarıldığında bu dosyalar hiç kullanılmaz.
    /// </summary>
    public static class SentetikUretici
    {
        // ---- Tüketim davranışı varsayımları (performans açığı literatürüyle uyumlu büyüklükler) ----
        public const double GercekIsitmaKatsayisi = 0.85;   // kullanıcılar binayı sürekli 19–20 °C tutmaz: gerçek ısıtma modelin ~%85'i
        public const double DavranisGurultusu = 0.18;       // binadan binaya kullanım farkı (log-normal σ)
        public const double SicakSuKWhM2 = 25;              // sıcak su + pişirme baz yükü (kWh/m²·yıl)
        public const double HavaYillikSapma = 0.08;         // yılın derece-günü uzun dönem ortalamasından sapması (σ)

        /// <summary>Yapım dönemine göre kabuk U değerleri aralığı (W/m²K): duvar, pencere, çatı, zemin.</summary>
        static readonly (string ad, double[] duvar, double[] pencere, double[] cati, double[] zemin)[] Donemler =
        {
            ("2000 öncesi", new[] { 1.2, 1.9 }, new[] { 2.8, 3.5 }, new[] { 0.8, 1.5 }, new[] { 0.8, 1.2 }),
            ("2000–2008", new[] { 0.6, 0.9 }, new[] { 2.4, 2.9 }, new[] { 0.45, 0.7 }, new[] { 0.6, 0.9 }),
            ("2009–2023", new[] { 0.4, 0.6 }, new[] { 1.8, 2.4 }, new[] { 0.3, 0.45 }, new[] { 0.45, 0.65 }),
        };

        public static string Tuketim(VeriDeposu v, int n = 300, int tohum = 7)
        {
            var r = new Random(tohum);
            var sema = Semalar.Tuketim(v);
            var sb = new StringBuilder(string.Join(",", sema.Alanlar.Select(a => a.Ad)) + "\n");
            for (int i = 0; i < n; i++)
            {
                var il = v.Iller[r.Next(v.Iller.Count)];
                var b = RastgeleBina(r, out double camOrani);
                var d = Donemler[r.Next(Donemler.Length)];
                double U(double[] a) => Math.Round(a[0] + r.NextDouble() * (a[1] - a[0]), 2);
                double uD = U(d.duvar), uP = U(d.pencere), uC = U(d.cati), uZ = U(d.zemin);
                double pencere = Math.Round(camOrani * b.DisDuvarAlani, 1), opak = Math.Round(b.DisDuvarAlani - pencere, 1);
                double p = r.NextDouble();
                string yakit = p < 0.85 ? "DG" : p < 0.95 ? "KOMUR" : "ISIPOMPASI";
                if (!Semalar.YakitKodlari(v).Contains(yakit)) yakit = "DG";
                double dgYil = Math.Round(il.IsitmaDG * Math.Max(0.7, 1 + Rastgele.Normal(r, 0, HavaYillikSapma)));
                bool dgVar = r.NextDouble() < 0.6;          // kayıtların bir kısmında o yılın derece-günü bilinmiyor
                bool su = r.NextDouble() < 0.8;
                double h = uD * opak + uP * pencere + v.P("F1") * uC * b.CatiAlani + v.P("F2") * uZ * b.ZeminAlani + v.P("V3") * v.P("V1") * v.P("V2") * b.Hacim;
                double modelYakit = EnerjiModeli.YillikIsitma(dgYil, h) / Kalibrasyon.Verim(v, yakit);
                double gercek = GercekIsitmaKatsayisi * modelYakit * Math.Exp(Rastgele.Normal(r, -DavranisGurultusu * DavranisGurultusu / 2, DavranisGurultusu))
                              + (su ? SicakSuKWhM2 * b.KullanimAlani * Math.Max(0.3, 1 + Rastgele.Normal(r, 0, 0.2)) : 0);
                sb.AppendLine(string.Join(",", new[]
                {
                    $"S-{i + 1:0000}", Csv.Kacis(il.Ad), (2021 + r.Next(5)).ToString(), F(b.KullanimAlani, "0"), F(b.Hacim, "0"), F(opak), F(pencere),
                    F(b.CatiAlani), F(b.ZeminAlani), F(uD, "0.00"), F(uP, "0.00"), F(uC, "0.00"), F(uZ, "0.00"), yakit, F(Math.Round(gercek / 10) * 10, "0"),
                    su ? "Evet" : "Hayır", dgVar ? F(dgYil, "0") : "",
                }));
            }
            return sb.ToString();
        }

        // ---- Müşteri kararı varsayımları (fayda modeli; katsayılar açıkça burada) ----
        public const double GurultuOlcegi = 0.45;   // lojistik gürültü: aynı koşulda müşteriler farklı karar verebilir (VARSAYIM; F1 bu değere bağlıdır)

        public static string Teklifler(VeriDeposu v, int n = 800, int tohum = 11)
        {
            var r = new Random(tohum);
            var sema = Semalar.Teklif(v);
            var satirlar = new List<(string[] hucre, double fiyatM2, bool yenileme, double fayda0)>();
            for (int i = 0; i < n; i++)
            {
                var il = v.Iller[r.Next(v.Iller.Count)];
                double u = r.NextDouble();
                string musteri = u < 0.5 ? "Bireysel" : u < 0.75 ? "Site yönetimi" : u < 0.9 ? "Müteahhit" : "Kurum";
                bool yenileme = musteri == "Müteahhit" ? r.NextDouble() < 0.15 : r.NextDouble() < 0.7;
                string oncelik = Semalar.Oncelikler[r.Next(3)];
                var b = MusteriBinasi(r, musteri, out double camOrani);
                var g = new Girdi { Bina = b, Il = il, Bolge = il.Ts825Bolge ?? 4, CamOrani = camOrani, Mevcut = yenileme, Yakit = "DG" };
                Oncelikler.Uygula(g, oncelik);
                var sonuc = KararMotoru.Hesapla(v, g);
                var uygun = sonuc.Siralama.Where(s => s.Ts825Uygun).ToList();
                if (uygun.Count == 0) uygun = sonuc.Siralama;
                var eko = uygun.OrderBy(s => s.IlkYatirim).First();
                var yuksek = uygun.OrderBy(s => s.YillikIsitmaKWh).First();
                double pk = r.NextDouble();
                string paket = pk < 0.3 ? "Ekonomik" : pk < 0.75 ? "Önerilen" : pk < 0.92 ? "Yüksek performans" : "Özel";
                var sec = paket switch { "Ekonomik" => eko, "Önerilen" => uygun[0], "Yüksek performans" => yuksek, _ => uygun[r.Next(uygun.Count)] };
                double indirim = Math.Round(Math.Max(0, Math.Min(0.25, Rastgele.Normal(r, musteri == "Müteahhit" ? 0.08 : 0.04, 0.03))), 3);
                double marj = 1 + Math.Max(0, Rastgele.Normal(r, 0.18, 0.06));     // firma kâr ve işçilik payı
                double fiyatM2 = sec.IlkYatirim * marj * (1 - indirim) / sonuc.KullanimAlani;
                double? geri = GeriOdeme(sonuc, sec, eko, yenileme, fiyatM2 * sonuc.KullanimAlani, eko.IlkYatirim * marj * (1 - indirim));
                double yanit = Math.Round(-Math.Log(1 - r.NextDouble()) * 4, 0);
                bool rakip = r.NextDouble() < 0.55;
                var t = new TeklifOzellik { Il = il.Ad, MusteriTipi = musteri, ProjeTuru = yenileme ? "Yenileme" : "Yeni bina", Paket = paket, Oncelik = oncelik,
                                            KullanimAlani = sonuc.KullanimAlani, TeklifTLm2 = fiyatM2, IndirimOrani = indirim, YanitSuresiGun = yanit, GeriOdemeYil = geri, RakipTeklif = rakip };
                // Fiyat etkisi dışındaki fayda (fiyat etkisi aşağıda aynı tür projelerin medyanına göre eklenir)
                double fayda = 0.9 - 0.12 * (Math.Min(OzellikVektoru.GeriOdemeTavan, geri ?? OzellikVektoru.GeriOdemeTavan) - 8) / 4 + 6 * indirim
                             - 0.2 * (yanit - 4) - 0.9 * (rakip ? 1 : 0) + 1.4 * (t.OncelikUyumu ? 1 : 0)
                             + (musteri == "Site yönetimi" ? 0.4 : musteri == "Kurum" ? 0.6 : musteri == "Müteahhit" ? -0.3 : 0);
                var tarih = new DateTime(2023, 1, 1).AddDays(r.Next(3 * 365));
                var hucre = new[]
                {
                    $"TK-{tarih:yyyy}-{i + 1:0000}", tarih.ToString("yyyy-MM-dd"), Csv.Kacis(il.Ad), musteri, t.ProjeTuru, F(sonuc.KullanimAlani, "0"), paket, oncelik,
                    F(fiyatM2, "0"), geri.HasValue ? F(geri.Value, "0.0") : "", F(indirim, "0.000"), F(yanit, "0"), rakip ? "Evet" : "Hayır", "",
                };
                satirlar.Add((hucre, fiyatM2, yenileme, fayda));
            }
            double medYeni = Mat.Kuantil(satirlar.Where(s => !s.yenileme).Select(s => s.fiyatM2), 0.5);
            double medYen = Mat.Kuantil(satirlar.Where(s => s.yenileme).Select(s => s.fiyatM2), 0.5);
            var sb = new StringBuilder(string.Join(",", sema.Alanlar.Select(a => a.Ad)) + "\n");
            foreach (var (hucre, fiyat, yen, fayda0) in satirlar)
            {
                double fiyatEndeksi = fiyat / (yen ? medYen : medYeni);
                double fayda = fayda0 - 3.0 * (fiyatEndeksi - 1);
                double e = Math.Log(1 / (1 - r.NextDouble()) - 1) * GurultuOlcegi;   // lojistik gürültü
                hucre[hucre.Length - 1] = fayda + e > 0 ? "Kabul" : "Red";
                sb.AppendLine(string.Join(",", hucre));
            }
            return sb.ToString();
        }

        /// <summary>İskontolu geri ödeme: ek yatırım, yıllık tasarrufun bugünkü değeriyle kaç yılda karşılanıyor (40 yıla kadar).</summary>
        static double? GeriOdeme(Sonuc r, Secenek s, Secenek eko, bool yenileme, double yatirim, double ekoYatirim)
        {
            double tasarruf = yenileme ? r.MevcutGiderTL - s.YillikGiderTL : eko.YillikGiderTL - s.YillikGiderTL;
            double ek = yenileme ? yatirim : yatirim - ekoYatirim;
            if (ek <= 0) return 0;
            if (tasarruf <= 0) return null;
            for (int y = 1; y <= 40; y++) if (EnerjiModeli.Pwf(r.EnerjiIskonto, y) * tasarruf >= ek) return y - 1 + (ek - EnerjiModeli.Pwf(r.EnerjiIskonto, y - 1) * tasarruf) / (tasarruf / Math.Pow(1 + r.EnerjiIskonto, y));
            return null;
        }

        static Bina RastgeleBina(Random r, out double camOrani)
        {
            camOrani = 0.12 + r.NextDouble() * 0.18;
            return Bina.Dikdortgen(Math.Round(8 + r.NextDouble() * 14, 1), Math.Round(10 + r.NextDouble() * 22, 1), 1 + r.Next(6), 2.9 + r.NextDouble() * 0.3);
        }

        static Bina MusteriBinasi(Random r, string musteri, out double camOrani)
        {
            camOrani = 0.12 + r.NextDouble() * 0.18;
            int kat = musteri switch { "Bireysel" => 1 + r.Next(3), "Site yönetimi" => 4 + r.Next(9), "Müteahhit" => 3 + r.Next(8), _ => 2 + r.Next(5) };
            double en = musteri == "Bireysel" ? 8 + r.NextDouble() * 6 : 12 + r.NextDouble() * 12, boy = musteri == "Bireysel" ? 9 + r.NextDouble() * 8 : 16 + r.NextDouble() * 24;
            return Bina.Dikdortgen(Math.Round(en, 1), Math.Round(boy, 1), kat, 3.0);
        }

        static string F(double d, string b = "0.0") => d.ToString(b, CultureInfo.InvariantCulture);
    }
}
