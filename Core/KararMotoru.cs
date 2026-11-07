using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core
{
    public sealed class Girdi
    {
        public BinaFormu Form = null!;   // bina_tipleri.csv tipi ya da BinaFormu.OzelOlcu(...)
        public int Yon = 4;              // 2 Kuzey, 3 Doğu, 4 Güney, 5 Batı (yalnızca proje bilgisi ve çizim)
        public Il? Il;                   // proje ili (MGM derece-gün); boşsa parametreler.csv K1 (Malatya)
        public int Bolge = 4;            // TS 825:2024 iklim bölgesi (U sınırları); Malatya = 4
        public double CamOrani = 0.25;   // pencere alanı / kullanım alanı (ENB2012 tanımı)
        public string Dograma = VarsayilanDograma;   // pencere doğrama tipi (TS 825:2024 Ek A.3 çizelgesindeki adıyla)
        public const string VarsayilanDograma = "PVC Doğrama (5 Odacıklı)";
        public string Yakit = "DG";      // ısıtma yakıtı: "DG" doğalgaz (BOTAŞ) ya da yakitlar.csv kimliği
        public int Kademe = 1;           // 1 / 2: doğalgaz ve elektrik tarifesinin kademesi
        public bool Sogutma = false;     // klima ile soğutma var: soğutma derece-günüyle elektrik gideri eklenir
        public bool Mevcut = false;      // mevcut bina (yenileme): duvar yerinde kalır, yalıtım dışarıdan eklenir
        public double EnerjiArtisi = 0;  // enerji fiyatlarının enflasyonun üstündeki yıllık reel artışı (0 = enflasyonla aynı)
        public int Yil = 20;             // analiz süresi (yıl)
        public bool SadeceTs825 = false;
        // AHP ikili karşılaştırmaları: soldaki kriterin sağdakine göre önemi (Saaty 1/9 .. 9)
        public double MaliyetEnerji = 3, MaliyetKarbon = 3, EnerjiKarbon = 1;
        // Doğalgaz fiyatı (TL/kWh). Boş bırakılırsa parametreler.csv'deki E5 (BOTAŞ Kademe-1) kullanılır.
        public double? GazTLkWh;
        // Duyarlılık analizi çarpanları (1 = veri dosyasındaki değer)
        public double GazCarpani = 1, MalzemeCarpani = 1;

        public Girdi Kopya() => (Girdi)MemberwiseClone();
    }

    public sealed class Secenek
    {
        public Malzeme Yalitim = null!, Cam = null!, Duvar = null!;
        public double DuvarU, PencereU, H, YillikSogutmaKWh, YillikSogutmaElektrikKWh, YillikSogutmaTL, YillikIsitmaTL, YanginEkMaliyet, DuvarAgirlikTon, YillikIsitmaKWh, YillikGazKWh, YillikGiderTL, IlkYatirim, ToplamMaliyet, ToplamCO2Kg, Skor;
        public bool Ts825Uygun;
        public string YanginNotu = "";
        public bool MevcutDuvar;             // yenileme: duvar yerinde kalır (Duvar yalnız taşıyıcı nesne)
        public string DuvarAdi => MevcutDuvar ? "Mevcut duvar" : Duvar.KisaAd;       // yangın yönetmeliğinin bu pakete getirdiği koşul (boş = koşul yok)
        public string Kod => Yalitim.Id + Cam.Id + Duvar.Id;
    }

    /// <summary>Hesabın sabit kalan kısmı: geometri, çatı ve zemin katmanları, iklim ve ekonomi parametreleri.</summary>
    public sealed class Sonuc
    {
        public BinaFormu Form = null!;
        public double KullanimAlani, CamAlani, OpakDuvar, CatiAlani, ZeminAlani, Hacim;
        public Malzeme Cati = null!, Zemin = null!;
        public double CatiU, ZeminU, HHavalandirma, HSabit, HBaz;
        public double GazTL, PWF, Iskonto;     // ısıtmada yakıtın TL/kWh'si, bugünkü değer çarpanı, reel iskonto oranı
        public double EnerjiIskonto;           // enerji giderleri için etkin iskonto: (1 + r) / (1 + reel enerji artışı) − 1
        public double IsitmaVerimi, IsitmaEmisyon;   // ısıtıcı verimi, yakıtın kgCO₂/kWh'si
        public string YakitAdi = "Doğalgaz", YakitId = "DG";
        public double SogutmaDG, ElektrikTL, ElektrikEmisyon, Seer;
        public bool Sogutma, Mevcut;
        public double MevcutGiderTL, MevcutIsitmaKWh, MevcutCO2Kg;   // yenilemede yalıtımsız mevcut binanın yıllık gideri, ısıtması, analiz süresince CO₂'si
        public double BinaYuksekligi;
        public int YanginElenen;
        public double YanginSeridiM2;          // zor alevlenici sistemde zeminden 1,5 m hiç yanmaz şerit alanı               // yangın yönetmeliği nedeniyle çıkarılan birleşim sayısı
        public string IlAdi = "Malatya";
        public double DG;                      // yıllık ısıtma derece-gün
        public int BolgeNo = 4;
        public string Dograma = "";
        /// <summary>Seçilen doğramayla her camın pencere U değeri (TS 825:2024 Ek A.3); çizelgede olmayan cam burada yer almaz.</summary>
        public Dictionary<string, double> PencereU = new();
        public double SinirDuvar, SinirCati, SinirZemin, SinirPencere;   // TS 825 U sınırları (W/m²K)
        public double SabitYatirim;            // çatı + zemin yalıtımı (her seçenekte aynı)
        public bool CamDuvariAsiyor;           // istenen pencere alanı dış duvar alanından büyük (pencere alanı duvarla sınırlandı)
        public double[] Agirlik = new double[3];
        public double CR;
        public List<Secenek> Siralama = new();
        public List<Secenek> Tum = new();      // filtre uygulanmadan önceki bütün birleşimler
        public int ToplamKombinasyon;
    }

    /// <summary>
    /// TS 825 yaklaşımıyla yıllık ısıtma enerjisi (derece-gün yöntemi).
    /// H = HT + HV;  HT = ΣU·A (tavan 0,8 · AT·UT, zemin 0,5 · At·Ut);  HV = 0,33 · nh · Vh,  Vh = 0,8 · V
    /// Q (kWh/yıl) = 0,024 · DG · H;  yakıt = Q / ηkazan.
    /// </summary>
    public static class EnerjiModeli
    {
        public static double DuvarU(VeriDeposu v, Malzeme duvar, Malzeme? yalitim, double? yalitimKalinligi = null)
        {
            double r = v.P("T6") + duvar.Kalinlik / duvar.Lambda;
            if (yalitim != null) r += (yalitimKalinligi ?? yalitim.Kalinlik) / yalitim.Lambda;
            return 1 / r;
        }

        /// <summary>Mevcut katmana (U0) ek yalıtım: U = 1 / (1/U0 + d/λ).</summary>
        public static double EkKatmanU(double u0, Malzeme m) => 1 / (1 / u0 + m.Kalinlik / m.Lambda);

        public static double YillikIsitma(double dereceGun, double h) => 0.024 * dereceGun * h;

        /// <summary>Bugünkü değer çarpanı: PWF = [1 − (1 + r)^−N] / r,  r = (i − g) / (1 + g).</summary>
        public static double Pwf(double r, int n) => Math.Abs(r) < 1e-9 ? n : (1 - Math.Pow(1 + r, -n)) / r;
    }

    /// <summary>EcoBuildss karar motoru: Malatya derece-gün enerji hesabı + ömür boyu maliyet + AHP + TOPSIS.</summary>
    public static class KararMotoru
    {
        public static (double[] w, double cr) Ahp(double a12, double a13, double a23)
        {
            var A = new[,] { { 1, a12, a13 }, { 1 / a12, 1, a23 }, { 1 / a13, 1 / a23, 1 } };
            var g = new double[3];
            for (int i = 0; i < 3; i++) g[i] = Math.Pow(A[i, 0] * A[i, 1] * A[i, 2], 1.0 / 3.0);
            double s = g.Sum();
            var w = g.Select(x => x / s).ToArray();
            double lmax = 0;
            for (int i = 0; i < 3; i++)
                lmax += (A[i, 0] * w[0] + A[i, 1] * w[1] + A[i, 2] * w[2]) / w[i];
            lmax /= 3;
            double cr = Math.Max(0, ((lmax - 3) / 2) / 0.58); // RI(3) = 0,58; tam tutarlı matriste yuvarlama hatası negatif çıkmasın
            return (w, cr);
        }

        /// <summary>Geometri, sabit katmanlar ve ekonomi parametreleri (seçenekten bağımsız kısım).</summary>
        public static Sonuc Hazirla(VeriDeposu v, Girdi g)
        {
            var r = new Sonuc { Form = g.Form };
            r.IlAdi = g.Il?.Ad ?? "Malatya";
            r.DG = (g.Il ?? v.VarsayilanIl).IsitmaDG;
            r.BolgeNo = g.Bolge;
            var b = v.Bolge(g.Bolge);
            if (b != null) { r.SinirDuvar = b.UDuvar; r.SinirCati = b.UCati; r.SinirZemin = b.UZemin; r.SinirPencere = b.UPencere; }
            else throw new InvalidDataException($"ts825_bolgeler.csv içinde {g.Bolge}. bölge yok.");
            r.Dograma = g.Dograma;
            r.BinaYuksekligi = g.Form.Yukseklik;
            foreach (var c in v.Malzemeler.Where(m => m.Tur == 'P'))
                if (v.PencereU(g.Dograma, c) is double up) r.PencereU[c.Id] = up;
            if (r.PencereU.Count == 0) throw new InvalidDataException($"ts825_pencere.csv içinde \"{g.Dograma}\" doğraması için cam değeri yok.");
            double katH = v.P("M7");
            r.KullanimAlani = g.Form.KullanimAlani(katH);
            r.CatiAlani = r.ZeminAlani = g.Form.CatiAlani;
            r.Hacim = g.Form.Hacim;
            r.CamAlani = g.CamOrani * r.KullanimAlani;
            if (r.CamAlani > g.Form.DuvarAlani) { r.CamDuvariAsiyor = true; r.CamAlani = g.Form.DuvarAlani; }
            r.OpakDuvar = Math.Max(0, g.Form.DuvarAlani - r.CamAlani);

            // Çatı ve zemin katmanı her seçenekte aynıdır: bölge sınırını sağlayanların en ucuzu (hiçbiri sağlamıyorsa en düşük U'lu)
            Malzeme Katman(char tur, double uBaz, double sinir)
            {
                var l = v.Malzemeler.Where(m => m.Tur == tur).ToList();
                if (l.Count == 0) throw new InvalidDataException($"cati_zemin.csv içinde {(tur == 'C' ? "çatı" : "zemin")} katmanı yok.");
                var uygun = l.Where(m => EnerjiModeli.EkKatmanU(uBaz, m) <= sinir + 1e-12).ToList();
                return uygun.Count > 0 ? uygun.OrderBy(m => m.FiyatM2).First() : l.OrderBy(m => EnerjiModeli.EkKatmanU(uBaz, m)).First();
            }
            r.Cati = Katman('C', v.P("M3"), r.SinirCati);
            r.Zemin = Katman('Z', v.P("M2"), r.SinirZemin);
            r.CatiU = EnerjiModeli.EkKatmanU(v.P("M3"), r.Cati);
            r.ZeminU = EnerjiModeli.EkKatmanU(v.P("M2"), r.Zemin);
            r.HHavalandirma = v.P("V3") * v.P("V1") * v.P("V2") * r.Hacim;
            r.HSabit = v.P("F1") * r.CatiU * r.CatiAlani + v.P("F2") * r.ZeminU * r.ZeminAlani + r.HHavalandirma;
            // Karşılaştırma için yalıtımsız referans kabuk (Tsanas ve Xifara 2012 makalesindeki U değerleri)
            r.HBaz = v.P("M1") * r.OpakDuvar + v.P("M4") * r.CamAlani + v.P("F1") * v.P("M3") * r.CatiAlani
                     + v.P("F2") * v.P("M2") * r.ZeminAlani + r.HHavalandirma;

            // Isıtma yakıtı: doğalgaz (BOTAŞ, üst ısıl değer) ya da yakitlar.csv; fiyat çarpanı (duyarlılık) bütün enerjiye uygulanır
            r.ElektrikTL = v.P(g.Kademe == 2 ? "EL2" : "EL1") * g.GazCarpani;
            r.ElektrikEmisyon = v.P("EL3");
            r.YakitId = g.Yakit;
            if (g.Yakit == "DG")
            {
                r.GazTL = (g.GazTLkWh ?? v.GazTLkWh(v.P(g.Kademe == 2 ? "E2" : "E1"))) * g.GazCarpani;
                r.IsitmaVerimi = v.P("H1"); r.IsitmaEmisyon = v.EmisyonKgKWh; r.YakitAdi = "Doğalgaz";
            }
            else
            {
                var y = v.Yakitlar.FirstOrDefault(x => x.Id == g.Yakit) ?? throw new InvalidDataException($"yakitlar.csv içinde '{g.Yakit}' yok.");
                r.GazTL = y.Elektrikli ? r.ElektrikTL : y.FiyatBirim / y.IsilDegerKWh * g.GazCarpani;
                r.IsitmaVerimi = y.Verim; r.IsitmaEmisyon = y.EmisyonKgKWh; r.YakitAdi = $"{y.Ad} ({y.Isitici.ToLower(Ortak.TR)})";
            }
            r.Sogutma = g.Sogutma; r.Seer = v.P("S1");
            r.SogutmaDG = (g.Il ?? v.VarsayilanIl).SogutmaDG;
            r.Iskonto = v.ReelIskonto;
            r.EnerjiIskonto = (1 + r.Iskonto) / (1 + g.EnerjiArtisi) - 1;
            r.PWF = EnerjiModeli.Pwf(r.EnerjiIskonto, g.Yil);
            r.Mevcut = g.Mevcut;
            if (r.BinaYuksekligi > 0) r.YanginSeridiM2 = Math.Min(v.P("B2"), r.BinaYuksekligi) * g.Form.DuvarAlani / r.BinaYuksekligi;
            if (g.Mevcut)
            {
                // Yalıtımsız mevcut bina (referans kabuk) ile aynı yakıt ve soğutma koşulu: tasarruf ve geri dönüş buna göre
                r.MevcutIsitmaKWh = EnerjiModeli.YillikIsitma(r.DG, r.HBaz);
                double mGaz = r.MevcutIsitmaKWh / r.IsitmaVerimi, mSog = g.Sogutma ? EnerjiModeli.YillikIsitma(r.SogutmaDG, r.HBaz) / r.Seer : 0;
                r.MevcutGiderTL = mGaz * r.GazTL + mSog * r.ElektrikTL;
                r.MevcutCO2Kg = (mGaz * r.IsitmaEmisyon + mSog * r.ElektrikEmisyon) * g.Yil;
            }
            r.SabitYatirim = (r.Cati.FiyatM2 * r.CatiAlani + r.Zemin.FiyatM2 * r.ZeminAlani) * g.MalzemeCarpani;
            return r;
        }

        /// <summary>Tek bir birleşimin enerji, maliyet ve karbon değerleri.</summary>
        public static Secenek Degerlendir(VeriDeposu v, Girdi g, Sonuc r, Malzeme y, Malzeme c, Malzeme d)
        {
            // Yenilemede duvar yerinde kalır: yalıtım mevcut duvarın (referans U) üstüne eklenir, duvar bedeli ve karbonu yoktur
            double uW = g.Mevcut ? EnerjiModeli.EkKatmanU(v.P("M1"), y) : EnerjiModeli.DuvarU(v, d, y);
            double uP = r.PencereU[c.Id];
            double h = uW * r.OpakDuvar + uP * r.CamAlani + r.HSabit;
            double isitma = EnerjiModeli.YillikIsitma(r.DG, h);
            double gaz = isitma / r.IsitmaVerimi;
            double sogutma = g.Sogutma ? EnerjiModeli.YillikIsitma(r.SogutmaDG, h) : 0;   // derece-gün yöntemi, MGM soğutma derece-günü
            double sogElektrik = sogutma / r.Seer;
            double giderIsitma = gaz * r.GazTL, giderSogutma = sogElektrik * r.ElektrikTL, gider = giderIsitma + giderSogutma;
            double duvarFiyat = g.Mevcut ? 0 : d.FiyatM2, duvarKarbon = g.Mevcut ? 0 : d.KarbonM2;
            double yangin = YanginEkMaliyeti(v, r, y);
            double yatirim = (r.OpakDuvar * (duvarFiyat + y.FiyatM2) + r.CamAlani * c.FiyatM2 + yangin) * g.MalzemeCarpani + r.SabitYatirim;
            return new Secenek
            {
                Yalitim = y, Cam = c, Duvar = d, DuvarU = uW, PencereU = uP, H = h, MevcutDuvar = g.Mevcut,
                YillikIsitmaKWh = isitma, YillikGazKWh = gaz, YillikGiderTL = gider, YillikIsitmaTL = giderIsitma,
                YillikSogutmaKWh = sogutma, YillikSogutmaElektrikKWh = sogElektrik, YillikSogutmaTL = giderSogutma,
                IlkYatirim = yatirim, YanginEkMaliyet = yangin * g.MalzemeCarpani, YanginNotu = YanginNotu(v, r, y),
                DuvarAgirlikTon = r.OpakDuvar * ((g.Mevcut ? 0 : d.BirimAgirlik) + y.BirimAgirlik) / 1000,
                ToplamMaliyet = yatirim + r.PWF * gider,                    // ömür boyu maliyet, bugünkü değer
                ToplamCO2Kg = r.OpakDuvar * (duvarKarbon + y.KarbonM2) + r.CamAlani * c.KarbonM2
                              + (gaz * r.IsitmaEmisyon + sogElektrik * r.ElektrikEmisyon) * g.Yil,
                Ts825Uygun = uW <= r.SinirDuvar && (r.CamAlani == 0 || uP <= r.SinirPencere)
                             && r.CatiU <= r.SinirCati && r.ZeminU <= r.SinirZemin,
            };
        }

        public static Sonuc Hesapla(VeriDeposu v, Girdi g)
        {
            var r = Hazirla(v, g);
            var liste = new List<Secenek>();
            // Yenilemede duvar seçilmez (mevcut duvar kalır); seçeneklerde ilk duvar yalnız taşıyıcı nesne olarak kullanılır
            var duvarlar = g.Mevcut ? v.Malzemeler.Where(m => m.Tur == 'D').Take(1) : v.Malzemeler.Where(m => m.Tur == 'D');
            foreach (var y in v.Malzemeler.Where(m => m.Tur == 'Y'))
            {
                if (!YanginaUygun(v, r, y)) { r.YanginElenen += v.Malzemeler.Count(m => m.Tur == 'P' && r.PencereU.ContainsKey(m.Id)) * duvarlar.Count(); continue; }
                foreach (var c in v.Malzemeler.Where(m => m.Tur == 'P' && r.PencereU.ContainsKey(m.Id)))
                foreach (var d in duvarlar)
                    liste.Add(Degerlendir(v, g, r, y, c, d));
            }
            r.ToplamKombinasyon = liste.Count;
            r.Tum = liste;
            if (g.SadeceTs825) liste = liste.Where(s => s.Ts825Uygun).ToList();

            var (w, cr) = Ahp(g.MaliyetEnerji, g.MaliyetKarbon, g.EnerjiKarbon);
            r.Agirlik = w; r.CR = cr;
            if (liste.Count == 0) return r;

            // TOPSIS — üç kriter de "küçük olan iyi": ömür boyu maliyet, yıllık ısıtma ihtiyacı, toplam CO₂
            Func<Secenek, double>[] kr = { s => s.ToplamMaliyet, s => s.YillikIsitmaKWh, s => s.ToplamCO2Kg };
            var norm = kr.Select(f => Math.Sqrt(liste.Sum(s => f(s) * f(s)))).ToArray();
            var V = liste.Select(s => kr.Select((f, j) => w[j] * f(s) / norm[j]).ToArray()).ToList();
            var iyi = Enumerable.Range(0, 3).Select(j => V.Min(x => x[j])).ToArray();
            var kotu = Enumerable.Range(0, 3).Select(j => V.Max(x => x[j])).ToArray();
            for (int i = 0; i < liste.Count; i++)
            {
                double dp = Math.Sqrt(Enumerable.Range(0, 3).Sum(j => Math.Pow(V[i][j] - iyi[j], 2)));
                double dm = Math.Sqrt(Enumerable.Range(0, 3).Sum(j => Math.Pow(V[i][j] - kotu[j], 2)));
                liste[i].Skor = (dp + dm) == 0 ? 1 : dm / (dp + dm);
            }
            r.Siralama = liste.OrderByDescending(s => s.Skor).ToList();
            return r;
        }

        // ---------------- Yangın yönetmeliği (Binaların Yangından Korunması Hakkında Yönetmelik, Md. 27) ----------------
        /// <summary>Bina yüksekliği B1'den (28,50 m) fazlaysa cephe sistemi en az A2-s1,d0 olmalı; değilse yalıtım seçenek olamaz.</summary>
        public static bool YanginaUygun(VeriDeposu v, Sonuc r, Malzeme y)
        {
            if (r.BinaYuksekligi <= v.P("B1") + 1e-9) return true;
            var ys = v.Yangin(y.Aile);
            return ys != null && ys.A2VeyaUstu;
        }

        static bool HicYanmaz(VeriDeposu v, Malzeme y) => v.Yangin(y.Aile)?.Urun.StartsWith("A1") == true;

        /// <summary>
        /// Zor alevlenici (A1 olmayan) sistemde zemin kotundan B2 (1,5 m) yüksekliğe kadar hiç yanmaz malzeme gerekir (Md. 27/2-b):
        /// bu şeritte aynı kalınlıkta taş yünü mantolama kullanılır, fiyat farkı ilk yatırıma eklenir. Çevre = dış duvar alanı ÷ bina yüksekliği.
        /// </summary>
        public static double YanginEkMaliyeti(VeriDeposu v, Sonuc r, Malzeme y)
        {
            if (HicYanmaz(v, y) || r.BinaYuksekligi <= 0) return 0;
            var a1 = v.Malzemeler.Where(m => m.Tur == 'Y' && HicYanmaz(v, m) && Math.Abs(m.Kalinlik - y.Kalinlik) < 1e-9).OrderBy(m => m.FiyatM2).FirstOrDefault();
            if (a1 == null) return 0;
            double cevre = r.Form.DuvarAlani / r.BinaYuksekligi;
            double serit = Math.Min(v.P("B2"), r.BinaYuksekligi) * cevre;
            return serit * Math.Max(0, a1.FiyatM2 - y.FiyatM2);
        }

        public static string YanginNotu(VeriDeposu v, Sonuc r, Malzeme y)
        {
            if (HicYanmaz(v, y)) return "";
            string n = $"Yangın yönetmeliği (Md. 27/2-b): zeminden {v.P("B2").ToString("0.0#", Ortak.TR)} m yüksekliğe kadar hiç yanmaz (A1) yalıtım (bedeli eklendi)";
            if (r.BinaYuksekligi > v.P("B3")) n += $"; bina {v.P("B3").ToString("0.0#", Ortak.TR)} m'den yüksek: pencere yanlarında en az 15 cm, üstünde 30 cm hiç yanmaz yangın bariyeri (pencere ölçüsü girilmediği için bedele eklenmedi)";
            return n + ".";
        }

        /// <summary>Kaydırıcı değerini (-8..8) Saaty oranına çevirir: negatif = soldaki kriter daha önemli.</summary>
        public static double Saaty(int kaydirici) => kaydirici >= 0 ? 1.0 / (kaydirici + 1) : (-kaydirici + 1);
    }

    /// <summary>Bir girdinin değiştiği aralıkta önerinin (TOPSIS 1.) aynı kaldığı dilim.</summary>
    public sealed class Dilim
    {
        public double Bas, Son;
        public Secenek Oneri = null!;
    }

    /// <summary>
    /// Duyarlılık (what-if) analizi: bir girdiyi adım adım değiştirip önerinin nerede değiştiğini bulur.
    /// Veri değiştirilmez; yalnızca hesapta kullanılan çarpan ya da süre değişir.
    /// </summary>
    public static class Duyarlilik
    {
        public static List<Dilim> Tara(VeriDeposu v, Girdi g, Action<Girdi, double> uygula, double bas, double son, double adim)
        {
            var dilimler = new List<Dilim>();
            int n = (int)Math.Round((son - bas) / adim);
            for (int i = 0; i <= n; i++)
            {
                double x = bas + i * adim;
                var gi = g.Kopya(); uygula(gi, x);
                var r = KararMotoru.Hesapla(v, gi);
                if (r.Siralama.Count == 0) { gi.SadeceTs825 = false; r = KararMotoru.Hesapla(v, gi); }
                var o = r.Siralama[0];
                var son_ = dilimler.Count > 0 ? dilimler[dilimler.Count - 1] : null;
                if (son_ != null && son_.Oneri.Kod == o.Kod) son_.Son = x;
                else dilimler.Add(new Dilim { Bas = x, Son = x, Oneri = o });
            }
            return dilimler;
        }

        public static List<Dilim> GazFiyati(VeriDeposu v, Girdi g) =>
            Tara(v, g, (gi, x) => gi.GazCarpani = 1 + x / 100, -50, 200, 1);

        public static List<Dilim> MalzemeFiyati(VeriDeposu v, Girdi g) =>
            Tara(v, g, (gi, x) => gi.MalzemeCarpani = 1 + x / 100, -30, 50, 1);

        public static List<Dilim> AnalizSuresi(VeriDeposu v, Girdi g) =>
            Tara(v, g, (gi, x) => gi.Yil = (int)x, 1, 50, 1);

        /// <summary>Enerji fiyatının enflasyonun üstündeki yıllık reel artışı: −%5 … +%10.</summary>
        public static List<Dilim> EnerjiArtisi(VeriDeposu v, Girdi g) =>
            Tara(v, g, (gi, x) => gi.EnerjiArtisi = x / 100, -5, 10, 0.5);
    }
}
