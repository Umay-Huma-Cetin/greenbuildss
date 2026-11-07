using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EcoBuildss.Core
{
    /// <summary>
    /// Bina geometrisi: bina_tipleri.csv'deki 12 tipten biri (Tsanas ve Xifara 2012, ENB2012 geometrileri) ya da kullanıcının girdiği özel ölçüler.
    /// Hesapta kullanılan alanların hepsi buradan türetilir.
    /// </summary>
    public sealed class BinaFormu
    {
        public int No { get; set; }                 // 0 = kullanıcının girdiği özel ölçü
        public double Kompaktlik, YuzeyAlani, DuvarAlani, CatiAlani, Yukseklik;
        public double? En, Boy;                     // yalnızca özel ölçüde dolu (m)
        public bool Ozel => No == 0;
        public int KatSayisi(double katYuksekligi) => Math.Max(1, (int)Math.Round(Yukseklik / katYuksekligi));
        public double Hacim { get; set; }                         // brüt hacim (m³); ENB2012 tiplerinde makaledeki 771,75 m³
        /// <summary>Kullanım (döşeme) alanı = hacim ÷ kat yüksekliği; dikdörtgen prizmada taban × kat sayısına eşittir.</summary>
        public double KullanimAlani(double katYuksekligi) => Hacim / katYuksekligi;

        /// <summary>Dikdörtgen planlı özel bina: en × boy taban, kat sayısı × kat yüksekliği.</summary>
        public static BinaFormu OzelOlcu(double en, double boy, int kat, double katYuksekligi)
        {
            double h = kat * katYuksekligi, taban = en * boy, duvar = 2 * (en + boy) * h, yuzey = duvar + 2 * taban;
            return new BinaFormu
            {
                No = 0, En = en, Boy = boy, CatiAlani = taban, DuvarAlani = duvar, Yukseklik = h, YuzeyAlani = yuzey, Hacim = taban * h,
                // ENB2012 / Tsanas ve Xifara (2012) kompaktlık tanımı: 6·V^(2/3) / A
                Kompaktlik = 6 * Math.Pow(taban * h, 2.0 / 3.0) / yuzey,
            };
        }

        public override string ToString() => Ozel
            ? $"Özel ölçü · {En!.Value.ToString("0.#", Ortak.TR)} × {Boy!.Value.ToString("0.#", Ortak.TR)} m · {KatSayisi(3.5)} katlı"
            : $"Tip {No} · {(Yukseklik >= 7 ? "2 katlı" : "1 katlı")} · çatı {CatiAlani.ToString("N0", Ortak.TR)} m²";
    }

    public sealed class Malzeme
    {
        public string Id = "", Kategori = "", Ad = "";
        public double Kalinlik, Lambda, U, KarbonM2, FiyatM2;
        public double BirimAgirlik;                  // kg/m² (birim_agirlik_kg_m2; çatı ve zemin katmanında yok)
        public Malzeme Kopya() => (Malzeme)MemberwiseClone();
        public string FiyatKaynagi = "", FiyatTarihi = "", FiyatHesabi = "", TeknikKaynak = "", KarbonKaynagi = "";
        /// <summary>Fiyat doğrudan yayımlanmış değil, iki kaynaklı değerden hesaplanmış (fiyat_hesabi "Hesaplanan:" ile başlar).
        /// Regresyonda gözlem olarak kullanılmaz.</summary>
        public bool FiyatHesaplanan => FiyatHesabi.StartsWith("Hesaplanan", StringComparison.Ordinal);
        /// <summary>Camın TS 825:2024 Ek A.3 çizelgesindeki tipi (kaplamasız cam çizelgede yoktur → boş).</summary>
        public string CamTipi
        {
            get
            {
                if (Tur != 'P') return "";
                string a = Ad.ToLower(Ortak.TR);
                return !a.Contains("low-e") ? "" : a.Contains("üçlü") ? "Üçlü, iki camı Low-E kaplamalı" : "Çift camlı Low-E kaplamalı";
            }
        }
        /// <summary>Cam adında yazılı ara boşluk (mm), ör. "4+12+4" → 12; adda yazmıyorsa null.</summary>
        public int? AraBoslukMm
        {
            get
            {
                var m = System.Text.RegularExpressions.Regex.Match(Ad, @"\d+\+(\d+)\+\d+");
                return m.Success ? int.Parse(m.Groups[1].Value, Ortak.INV) : null;
            }
        }
        public char Tur => Id[0]; // Y = cephe yalıtımı (mantolama), P = pencere camı, D = duvar, C = çatı yalıtımı, Z = zemin yalıtımı
        /// <summary>Yalıtım ailesi (fiyat regresyonu ve optimum kalınlık hesabı için): "EPS", "Taş yünü" …</summary>
        public string Aile
        {
            get
            {
                string a = Ad.ToLower(Ortak.TR);
                return a.Contains("eps") ? "EPS" : a.Contains("xps") ? "XPS" : a.Contains("taş") ? "Taş yünü" : a.Contains("cam yünü") ? "Cam yünü" : Ad;
            }
        }
        /// <summary>Kartlarda ve tabloda kullanılan kısa ad; yalıtımda kalınlık eklenir.</summary>
        public string KisaAd
        {
            get
            {
                string a = Ad.ToLower(Ortak.TR);
                if (Tur == 'Y' || Tur == 'C' || Tur == 'Z')
                    return $"{Aile} {(Kalinlik * 100).ToString("0.#", Ortak.TR)} cm";
                if (Tur == 'P') return a.Contains("üçlü") ? "Üçlü Low-E" : a.Contains("low-e") ? "Low-E + argon" : "Standart çift cam";
                return a.Contains("tuğla") ? "Tuğla" : a.Contains("gazbeton") ? "Gazbeton" : a.Contains("bims") ? "Bims" : Ad;
            }
        }
    }

    /// <summary>Tedarikçi fiyat listesindeki tek bir yalıtım levhası (yalnız malzeme): fiyat–kalınlık regresyonunun verisi.</summary>
    public sealed class LevhaFiyati
    {
        public string Aile = "", Urun = "", Kaynak = "", Tarih = "";
        public double KalinlikCm, FiyatM2;
    }

    /// <summary>TS 825:2024 Ek A.3: doğrama tipi, cam tipi ve ara boşluğa göre pencere (cam + doğrama) U değeri.</summary>
    public sealed class PencereDegeri
    {
        public string Dograma = "", CamTipi = "", Kaynak = "";
        public int AraBoslukMm;
        public double U;
    }

    /// <summary>Isıtma yakıtı / ısıtıcı (yakitlar.csv). Doğalgaz BOTAŞ tarifesiyle parametreler.csv'den gelir.</summary>
    public sealed class Yakit
    {
        public string Id = "", Ad = "", Isitici = "", Birim = "", FiyatHesabi = "", FiyatKaynagi = "", FiyatTarihi = "", IsilDegerKaynagi = "", VerimKaynagi = "", EmisyonKaynagi = "";
        public double FiyatBirim, IsilDegerKWh, Verim, EmisyonKgKWh;
        public bool Elektrikli => Birim == "kWh";
        public override string ToString() => $"{Ad} · {Isitici}";
    }

    /// <summary>Yalıtım ailesinin yangın tepki sınıfı (yangin_siniflari.csv).</summary>
    public sealed class YanginSinifi
    {
        public string Aile = "", Urun = "", Sistem = "", Karsilik = "", Kaynak = "";
        /// <summary>Sistem en az A2-s1,d0 (hiç yanmaz / zor yanıcı) mi: 28,50 m'den yüksek binalarda istenen.</summary>
        public bool A2VeyaUstu => Urun.StartsWith("A1") || Urun.StartsWith("A2") || Sistem.StartsWith("A1") || Sistem.StartsWith("A2");
    }

    public static class Ortak
    {
        static readonly string[] Aylar = { "ocak", "şubat", "mart", "nisan", "mayıs", "haziran", "temmuz", "ağustos", "eylül", "ekim", "kasım", "aralık" };

        /// <summary>
        /// Fiyat tarihinin ayı ("Şubat 2026", "3 Kasım 2026", "Haziran 2026"); metinde ilk geçen ay + yıl alınır.
        /// Yalnız yıl yazılıysa (ör. "2026 (erişim Kasım 2026)") erişim ayı kullanılır. Bulunamazsa null.
        /// </summary>
        public static DateTime? FiyatAyi(string tarih)
        {
            string t = tarih.ToLower(TR);
            for (int i = 0; i < t.Length; i++)
                foreach (var (ad, ay) in Aylar.Select((a, k) => (a, k + 1)))
                    if (string.CompareOrdinal(t, i, ad, 0, ad.Length) == 0)
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(t.Substring(i + ad.Length), @"^\s*(\d{4})");
                        if (m.Success) return new DateTime(int.Parse(m.Groups[1].Value, INV), ay, 1);
                    }
            return null;
        }

        /// <summary>Fiyatın bugüne göre yaşı (tam ay); tarih okunamazsa null.</summary>
        public static int? FiyatYasiAy(string tarih, DateTime bugun) =>
            FiyatAyi(tarih) is DateTime d ? (bugun.Year - d.Year) * 12 + bugun.Month - d.Month : null;

        public static readonly CultureInfo TR = new CultureInfo("tr-TR");
        public static readonly CultureInfo INV = CultureInfo.InvariantCulture;
    }

    /// <summary>Veri klasöründeki üç CSV dosyasını okur.</summary>
    public sealed class VeriDeposu
    {
        public List<BinaFormu> Formlar { get; } = new();
        public List<Malzeme> Malzemeler { get; } = new();
        public Dictionary<string, double> Parametre { get; } = new();
        /// <summary>Parametrenin adı, birimi ve kaynağı (bilgi ve veritabanı için).</summary>
        public Dictionary<string, (string ad, string birim, string kaynak)> ParametreBilgi { get; } = new();
        public List<Il> Iller { get; } = new();                 // MGM derece-gün tablosu (iller.csv)
        public List<Ts825Bolge> Bolgeler { get; } = new();      // TS 825:2024 bölge U sınırları (ts825_bolgeler.csv)
        public List<LevhaFiyati> Levhalar { get; } = new();     // kalınlık serisi fiyat listesi (levha_fiyatlari.csv)
        public List<PencereDegeri> PencereDegerleri { get; } = new();   // TS 825:2024 Ek A.3 (ts825_pencere.csv)
        public List<Yakit> Yakitlar { get; } = new();                   // doğalgaz dışındaki yakıtlar (yakitlar.csv)
        public List<YanginSinifi> YanginSiniflari { get; } = new();     // yalıtım ailelerinin yangın sınıfı (yangin_siniflari.csv)
        public YanginSinifi? Yangin(string aile) => YanginSiniflari.FirstOrDefault(y => y.Aile == aile);

        /// <summary>Malzemeleri kopyalanmış veri (fiyat belirsizliği için: fiyat değiştirilir, asıl veri değişmez).</summary>
        public VeriDeposu Kopya(Func<Malzeme, double>? fiyat = null)
        {
            var d = new VeriDeposu { Kaynak = Kaynak };
            d.Formlar.AddRange(Formlar);
            foreach (var m in Malzemeler) { var k = m.Kopya(); if (fiyat != null) k.FiyatM2 = fiyat(m); d.Malzemeler.Add(k); }
            foreach (var kv in Parametre) d.Parametre[kv.Key] = kv.Value;
            foreach (var kv in ParametreBilgi) d.ParametreBilgi[kv.Key] = kv.Value;
            d.Iller.AddRange(Iller); d.Bolgeler.AddRange(Bolgeler); d.Levhalar.AddRange(Levhalar);
            d.PencereDegerleri.AddRange(PencereDegerleri); d.Yakitlar.AddRange(Yakitlar); d.YanginSiniflari.AddRange(YanginSiniflari);
            return d;
        }
        public Ts825Bolge? Bolge(int no) => Bolgeler.FirstOrDefault(b => b.No == no);
        /// <summary>Çizelgedeki doğrama tipleri (dosyadaki sırayla).</summary>
        public List<string> Dogramalar => PencereDegerleri.Select(p => p.Dograma).Distinct().ToList();

        /// <summary>
        /// Camın seçilen doğramayla pencere U değeri (TS 825:2024 Ek A.3). Camın ara boşluğu adında yazılıysa o satır alınır;
        /// yazılı değilse çizelgedeki ara boşluk değerlerinin en büyüğü (en elverişsiz) alınır. Çizelgede yoksa null.
        /// </summary>
        public double? PencereU(string dograma, Malzeme cam)
        {
            var l = PencereDegerleri.Where(p => p.Dograma == dograma && p.CamTipi == cam.CamTipi).ToList();
            if (l.Count == 0) return null;
            if (cam.AraBoslukMm is int a) return l.FirstOrDefault(p => p.AraBoslukMm == a)?.U;
            return l.Max(p => p.U);
        }

        // Türetilen değerler: veride yalnız ham değer tutulur (tekrar olmasın), türetme burada yapılır.
        /// <summary>1 Sm³ doğalgazın enerjisi (kWh): üst ısıl değer E3 (kcal/Sm³) × 0,001163 kWh/kcal.</summary>
        public double GazKWhSm3 => P("E3") * 0.001163;
        /// <summary>Doğalgaz fiyatı TL/kWh (Sm³ fiyatı ÷ Sm³ başına kWh).</summary>
        public double GazTLkWh(double tlSm3) => tlSm3 / GazKWhSm3;
        /// <summary>Doğalgaz emisyonu kgCO₂/kWh: E8 (tCO₂/TJ = kgCO₂/GJ) × 0,0036 GJ/kWh.</summary>
        public double EmisyonKgKWh => P("E8") * 0.0036;
        /// <summary>Reel iskonto oranı r = (i − g) ÷ (1 + g): I1 nominal faiz, I2 enflasyon.</summary>
        public double ReelIskonto => (P("I1") - P("I2")) / (1 + P("I2"));
        /// <summary>Varsayılan proje ili (Malatya): iklimi iller.csv'den, bölgesi il_bolgeleri.csv'den gelir.</summary>
        public Il VarsayilanIl => Iller.FirstOrDefault(i => i.Ad == "Malatya") ?? throw new InvalidDataException("iller.csv içinde Malatya yok.");
        /// <summary>Verinin nereden okunduğu: "CSV" ya da "SQLite veritabanı".</summary>
        public string Kaynak { get; set; } = "CSV";

        public static VeriDeposu Yukle(string klasor)
        {
            var d = new VeriDeposu();

            var bRows = CsvOku(Path.Combine(klasor, "bina_tipleri.csv"));
            var bh = Basliklar(bRows[0]);
            foreach (var r in bRows.Skip(1))
                d.Formlar.Add(new BinaFormu
                {
                    No = (int)Sayi(r[bh["tip"]]), Kompaktlik = Sayi(r[bh["kompaktlik"]]), YuzeyAlani = Sayi(r[bh["yuzey_alani_m2"]]),
                    DuvarAlani = Sayi(r[bh["duvar_alani_m2"]]), CatiAlani = Sayi(r[bh["cati_alani_m2"]]), Yukseklik = Sayi(r[bh["yukseklik_m"]]),
                    Hacim = Sayi(r[bh["hacim_m3"]]),
                });

            // Karar seçenekleri (malzemeler.csv: mantolama, cam, duvar) ve her seçenekte aynı olan çatı ve zemin katmanı (cati_zemin.csv)
            foreach (var dosya in new[] { "malzemeler.csv", "cati_zemin.csv" })
            {
                var mRows = CsvOku(Path.Combine(klasor, dosya));
                var mh = Basliklar(mRows[0]);
                foreach (var r in mRows.Skip(1))
                    d.Malzemeler.Add(new Malzeme
                    {
                        Id = r[mh["id"]], Kategori = r[mh["kategori"]], Ad = r[mh["ad"]],
                        Kalinlik = Sayi(r[mh["kalinlik_m"]]), Lambda = Sayi(r[mh["lambda_W_mK"]]),
                        U = Sayi(Alan(r, mh, "U_W_m2K")), KarbonM2 = Sayi(Alan(r, mh, "karbon_kgCO2e_m2")), BirimAgirlik = Sayi(Alan(r, mh, "birim_agirlik_kg_m2")),
                        FiyatM2 = Sayi(r[mh["fiyat_TL_m2"]]), FiyatKaynagi = r[mh["fiyat_kaynagi"]], FiyatTarihi = r[mh["fiyat_tarihi"]],
                        FiyatHesabi = Alan(r, mh, "fiyat_hesabi"), TeknikKaynak = Alan(r, mh, "teknik_kaynak"), KarbonKaynagi = Alan(r, mh, "karbon_kaynagi"),
                    });
            }

            var pRows = CsvOku(Path.Combine(klasor, "parametreler.csv"));
            var ph = Basliklar(pRows[0]);
            foreach (var r in pRows.Skip(1))
            {
                if (double.TryParse(r[ph["deger"]], NumberStyles.Float, Ortak.INV, out var val))
                {
                    d.Parametre[r[ph["id"]]] = val;
                    d.ParametreBilgi[r[ph["id"]]] = (Alan(r, ph, "parametre"), Alan(r, ph, "birim"), Alan(r, ph, "kaynak"));
                }
            }
            d.EkDosyalariOku(klasor);
            return d;
        }

        /// <summary>İl iklimi, il merkezlerinin TS 825 bölgesi, bölge sınırları ve levha fiyat serisi (dosya yoksa atlanır; uygulama Malatya parametreleriyle çalışır).</summary>
        void EkDosyalariOku(string klasor)
        {
            string yol = Path.Combine(klasor, "iller.csv");
            if (File.Exists(yol))
            {
                var rows = CsvOku(yol); var h = Basliklar(rows[0]);
                foreach (var r in rows.Skip(1))
                    Iller.Add(new Il { Ad = r[h["il"]], IsitmaDG = Sayi(r[h["isitma_derece_gun"]]), SogutmaDG = Sayi(r[h["sogutma_derece_gun"]]) });
            }
            yol = Path.Combine(klasor, "il_bolgeleri.csv");
            if (File.Exists(yol))
            {
                var rows = CsvOku(yol); var h = Basliklar(rows[0]);
                foreach (var r in rows.Skip(1))
                    if (Iller.FirstOrDefault(i => i.Ad == r[h["il"]]) is Il il) { il.Ts825Bolge = (int)Sayi(r[h["ts825_bolge"]]); il.BolgeKaynagi = r[h["kaynak"]]; }
            }
            yol = Path.Combine(klasor, "ts825_bolgeler.csv");
            if (File.Exists(yol))
            {
                var rows = CsvOku(yol); var h = Basliklar(rows[0]);
                foreach (var r in rows.Skip(1))
                    Bolgeler.Add(new Ts825Bolge { No = (int)Sayi(r[h["bolge"]]), UDuvar = Sayi(r[h["U_duvar"]]), UCati = Sayi(r[h["U_cati"]]), UZemin = Sayi(r[h["U_zemin"]]), UPencere = Sayi(r[h["U_pencere"]]) });
            }
            yol = Path.Combine(klasor, "ts825_pencere.csv");
            if (File.Exists(yol))
            {
                var rows = CsvOku(yol); var h = Basliklar(rows[0]);
                foreach (var r in rows.Skip(1))
                    PencereDegerleri.Add(new PencereDegeri { Dograma = r[h["dograma"]], CamTipi = r[h["cam_tipi"]], AraBoslukMm = (int)Sayi(r[h["ara_bosluk_mm"]]),
                                                             U = Sayi(r[h["U_pencere_W_m2K"]]), Kaynak = Alan(r, h, "kaynak") });
            }
            yol = Path.Combine(klasor, "yakitlar.csv");
            if (File.Exists(yol))
            {
                var rows = CsvOku(yol); var h = Basliklar(rows[0]);
                foreach (var r in rows.Skip(1))
                    Yakitlar.Add(new Yakit
                    {
                        Id = r[h["id"]], Ad = r[h["yakit"]], Isitici = r[h["ısıtıcı"]], Birim = r[h["birim"]], FiyatBirim = Sayi(r[h["fiyat_TL_birim"]]),
                        FiyatHesabi = r[h["fiyat_hesabi"]], FiyatKaynagi = r[h["fiyat_kaynagi"]], FiyatTarihi = r[h["fiyat_tarihi"]],
                        IsilDegerKWh = Sayi(r[h["isil_deger_kWh_birim"]]), IsilDegerKaynagi = r[h["isil_deger_kaynagi"]],
                        Verim = Sayi(r[h["verim"]]), VerimKaynagi = r[h["verim_kaynagi"]], EmisyonKgKWh = Sayi(r[h["emisyon_kgCO2_kWh"]]), EmisyonKaynagi = r[h["emisyon_kaynagi"]],
                    });
            }
            yol = Path.Combine(klasor, "yangin_siniflari.csv");
            if (File.Exists(yol))
            {
                var rows = CsvOku(yol); var h = Basliklar(rows[0]);
                foreach (var r in rows.Skip(1))
                    YanginSiniflari.Add(new YanginSinifi { Aile = r[h["aile"]], Urun = r[h["urun_yangin_sinifi"]], Sistem = r[h["sistem_yangin_sinifi"]], Karsilik = r[h["yonetmelik_karsiligi"]], Kaynak = r[h["kaynak"]] });
            }
            yol = Path.Combine(klasor, "levha_fiyatlari.csv");
            if (File.Exists(yol))
            {
                var rows = CsvOku(yol); var h = Basliklar(rows[0]);
                foreach (var r in rows.Skip(1))
                    Levhalar.Add(new LevhaFiyati { Aile = r[h["aile"]], KalinlikCm = Sayi(r[h["kalinlik_cm"]]), FiyatM2 = Sayi(r[h["fiyat_TL_m2"]]),
                                                   Urun = Alan(r, h, "urun"), Kaynak = Alan(r, h, "kaynak"), Tarih = Alan(r, h, "tarih") });
            }
        }

        public double P(string id) =>
            Parametre.TryGetValue(id, out var v) ? v : throw new InvalidDataException($"parametreler.csv içinde '{id}' bulunamadı.");

        internal static string Alan(string[] r, Dictionary<string, int> h, string ad) => h.TryGetValue(ad, out int i) && i < r.Length ? r[i] : "";

        internal static double Sayi(string s) => string.IsNullOrWhiteSpace(s) ? 0 : double.Parse(s, NumberStyles.Float, Ortak.INV);

        internal static Dictionary<string, int> Basliklar(string[] satir) =>
            satir.Select((h, i) => (h.Trim().TrimStart('﻿'), i)).ToDictionary(t => t.Item1, t => t.i);

        /// <summary>RFC 4180 uyumlu basit CSV okuyucu (tırnak içi virgül ve çift tırnak destekli).</summary>
        public static List<string[]> CsvOku(string yol)
        {
            var sonuc = new List<string[]>();
            var metin = File.ReadAllText(yol, Encoding.UTF8);
            var alan = new StringBuilder();
            var satir = new List<string>();
            bool tirnak = false;
            for (int i = 0; i < metin.Length; i++)
            {
                char c = metin[i];
                if (tirnak)
                {
                    if (c == '"')
                    {
                        if (i + 1 < metin.Length && metin[i + 1] == '"') { alan.Append('"'); i++; }
                        else tirnak = false;
                    }
                    else alan.Append(c);
                }
                else if (c == '"') tirnak = true;
                else if (c == ',') { satir.Add(alan.ToString()); alan.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < metin.Length && metin[i + 1] == '\n') i++;
                    satir.Add(alan.ToString()); alan.Clear();
                    if (satir.Count > 1 || satir[0].Length > 0) sonuc.Add(satir.ToArray());
                    satir.Clear();
                }
                else alan.Append(c);
            }
            if (alan.Length > 0 || satir.Count > 0) { satir.Add(alan.ToString()); sonuc.Add(satir.ToArray()); }
            return sonuc;
        }
    }
}
