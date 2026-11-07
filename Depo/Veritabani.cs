using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using EcoBuildss.Core;

namespace EcoBuildss.Depo
{
    /// <summary>Veritabanına kaydedilmiş bir teklif: girdiler (yeniden açmak için) ve sonuç özeti.</summary>
    public sealed class KayitliTeklif
    {
        public long Id;
        public DateTime Tarih;
        public string ProjeAdi = "", Paket = "", BinaOzeti = "";
        // girdiler
        public int FormNo;                 // 0 = özel ölçü
        public double En, Boy; public int Kat;
        public int Yon; public double CamOrani; public int Yil; public int Tarife; public bool Ts825Filtre;
        public int Oncelik; public int Tb1, Tb2, Tb3;
        public string Il = ""; public int Bolge; public string Dograma = "";
        public bool Mevcut, Sogutma; public string Yakit = "DG"; public double EnerjiArtisi;
        public string YalitimId = "", CamId = "", DuvarId = "";
        // sonuç özeti (kayıt anındaki fiyatlarla)
        public double IlkYatirim, KdvDahil, YillikIsitmaKWh, YillikGiderTL, OmurBoyuMaliyet, Co2Ton;
        public bool Ts825Uygun;
    }

    /// <summary>
    /// SQLite veritabanı (Veri\ecobuildss.db). Tablolar:
    ///   bina_tipleri (12 geometri) · malzemeler · cati_zemin · parametreler · iller (MGM derece-gün) · il_bolgeleri · ts825_bolgeler ·
    ///   ts825_pencere (Ek A.3 pencere U) · levha_fiyatlari · teklifler · islem_kayitlari (arka plan işlem ve hata günlüğü) · veri_surumu
    /// İlk çalıştırmada CSV dosyalarından oluşturulur; CSV dosyaları güncellenirse referans tablolar yeniden yüklenir
    /// (kayıtlı teklifler korunur). Uygulama veriyi bu veritabanından okur.
    /// </summary>
    public sealed class Veritabani
    {
        public string Dosya { get; }
        static readonly CultureInfo INV = CultureInfo.InvariantCulture;
        static readonly string[] CsvDosyalari = { "bina_tipleri.csv", "malzemeler.csv", "parametreler.csv" };
        static readonly string[] EkCsvDosyalari = { "cati_zemin.csv", "iller.csv", "il_bolgeleri.csv", "ts825_bolgeler.csv", "ts825_pencere.csv", "levha_fiyatlari.csv", "yakitlar.csv", "yangin_siniflari.csv" };   // yoksa atlanır

        public Veritabani(string dosya) { Dosya = dosya; }

        /// <summary>Veritabanını hazırlar (yoksa oluşturur, CSV'ler değiştiyse günceller) ve veriyi veritabanından okur.</summary>
        public VeriDeposu HazirlaVeYukle(string csvKlasoru)
        {
            using var c = SqliteBaglanti.Ac(Dosya);
            SemaKur(c);
            string surum = CsvSurumu(csvKlasoru);
            if (surum != "" && Tek(c, "SELECT deger FROM veri_surumu WHERE anahtar='csv'") != surum)
            {
                var csv = VeriDeposu.Yukle(csvKlasoru);
                ReferansYaz(c, csv, surum);
                KayitYaz(c, "Bilgi", "Veri yüklendi", $"CSV dosyaları değişti; referans tablolar yeniden yüklendi ({csv.Malzemeler.Count} malzeme ve katman, {csv.Iller.Count} il, {csv.Parametre.Count} parametre).");
            }
            return Oku(c);
        }

        static string CsvSurumu(string klasor)
        {
            var parca = new List<string>();
            foreach (var f in CsvDosyalari)
            {
                var fi = new FileInfo(Path.Combine(klasor, f));
                if (!fi.Exists) return "";
                parca.Add($"{f}:{fi.Length}:{fi.LastWriteTimeUtc.Ticks}");
            }
            foreach (var f in EkCsvDosyalari)
            {
                var fi = new FileInfo(Path.Combine(klasor, f));
                parca.Add(fi.Exists ? $"{f}:{fi.Length}:{fi.LastWriteTimeUtc.Ticks}" : $"{f}:yok");
            }
            return string.Join("|", parca);
        }

        static void SemaKur(DbConnection c)
        {
            Calistir(c, @"
CREATE TABLE IF NOT EXISTS veri_surumu (anahtar TEXT PRIMARY KEY, deger TEXT);
DROP TABLE IF EXISTS bina_kayitlari;
CREATE TABLE IF NOT EXISTS bina_tipleri (
  tip INTEGER PRIMARY KEY, kompaktlik REAL, yuzey_alani_m2 REAL, duvar_alani_m2 REAL, cati_alani_m2 REAL, yukseklik_m REAL, hacim_m3 REAL);
CREATE TABLE IF NOT EXISTS malzemeler (
  id TEXT PRIMARY KEY, kategori TEXT NOT NULL, ad TEXT NOT NULL, kalinlik_m REAL NOT NULL, lambda_W_mK REAL NOT NULL, U_W_m2K REAL NOT NULL,
  karbon_kgCO2e_m2 REAL NOT NULL, fiyat_TL_m2 REAL NOT NULL, fiyat_hesabi TEXT NOT NULL, fiyat_kaynagi TEXT NOT NULL, fiyat_tarihi TEXT NOT NULL,
  teknik_kaynak TEXT NOT NULL, karbon_kaynagi TEXT NOT NULL, birim_agirlik_kg_m2 REAL NOT NULL);
CREATE TABLE IF NOT EXISTS yakitlar (id TEXT PRIMARY KEY, yakit TEXT NOT NULL, isitici TEXT NOT NULL, birim TEXT NOT NULL, fiyat_TL_birim REAL NOT NULL, fiyat_hesabi TEXT NOT NULL,
  fiyat_kaynagi TEXT NOT NULL, fiyat_tarihi TEXT NOT NULL, isil_deger_kWh_birim REAL NOT NULL, isil_deger_kaynagi TEXT NOT NULL, verim REAL NOT NULL, verim_kaynagi TEXT NOT NULL,
  emisyon_kgCO2_kWh REAL NOT NULL, emisyon_kaynagi TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS yangin_siniflari (aile TEXT PRIMARY KEY, urun_yangin_sinifi TEXT NOT NULL, sistem_yangin_sinifi TEXT NOT NULL, yonetmelik_karsiligi TEXT NOT NULL, kaynak TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS parametreler (id TEXT PRIMARY KEY, parametre TEXT, deger REAL NOT NULL, birim TEXT, kaynak TEXT);
CREATE TABLE IF NOT EXISTS cati_zemin (id TEXT PRIMARY KEY, kategori TEXT NOT NULL, ad TEXT NOT NULL, kalinlik_m REAL NOT NULL, lambda_W_mK REAL NOT NULL,
  fiyat_TL_m2 REAL NOT NULL, fiyat_hesabi TEXT NOT NULL, fiyat_kaynagi TEXT NOT NULL, fiyat_tarihi TEXT NOT NULL, teknik_kaynak TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS iller (il TEXT PRIMARY KEY, isitma_derece_gun REAL NOT NULL, sogutma_derece_gun REAL NOT NULL);
CREATE TABLE IF NOT EXISTS il_bolgeleri (il TEXT PRIMARY KEY REFERENCES iller(il), ts825_bolge INTEGER NOT NULL REFERENCES ts825_bolgeler(bolge), kaynak TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS ts825_bolgeler (bolge INTEGER PRIMARY KEY, U_duvar REAL, U_cati REAL, U_zemin REAL, U_pencere REAL);
CREATE TABLE IF NOT EXISTS ts825_pencere (dograma TEXT NOT NULL, cam_tipi TEXT NOT NULL, ara_bosluk_mm INTEGER NOT NULL, U_pencere_W_m2K REAL NOT NULL, kaynak TEXT NOT NULL,
  PRIMARY KEY (dograma, cam_tipi, ara_bosluk_mm));
CREATE TABLE IF NOT EXISTS levha_fiyatlari (id INTEGER PRIMARY KEY, aile TEXT NOT NULL, kalinlik_cm REAL NOT NULL, fiyat_TL_m2 REAL NOT NULL, urun TEXT NOT NULL, kaynak TEXT NOT NULL, tarih TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS islem_kayitlari (
  id INTEGER PRIMARY KEY AUTOINCREMENT, tarih TEXT NOT NULL, tur TEXT NOT NULL CHECK (tur IN ('Bilgi','Uyarı','Hata')),
  islem TEXT NOT NULL, ayrinti TEXT NOT NULL, kullanici TEXT NOT NULL, bilgisayar TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS teklifler (
  id INTEGER PRIMARY KEY AUTOINCREMENT, tarih TEXT NOT NULL, proje_adi TEXT, paket TEXT, bina_ozeti TEXT,
  form_no INTEGER, en REAL, boy REAL, kat INTEGER, yon INTEGER, cam_orani REAL, yil INTEGER, tarife INTEGER, ts825_filtre INTEGER,
  oncelik INTEGER, tb1 INTEGER, tb2 INTEGER, tb3 INTEGER, il TEXT, bolge INTEGER,
  yalitim_id TEXT REFERENCES malzemeler(id), cam_id TEXT REFERENCES malzemeler(id), duvar_id TEXT REFERENCES malzemeler(id),
  ilk_yatirim REAL, kdv_dahil REAL, yillik_isitma_kwh REAL, yillik_gider_tl REAL, omur_boyu_maliyet REAL, co2_ton REAL, ts825_uygun INTEGER);");
            // Önceki sürümün teklifler tablosuna il ve bölge sütunlarını ekle
            var sutun = new HashSet<string>();
            using (var cmd = Komut(c, "PRAGMA table_info(teklifler)"))
            using (var r = cmd.ExecuteReader()) while (r.Read()) sutun.Add(S(r, 1));
            if (!sutun.Contains("il")) Calistir(c, "ALTER TABLE teklifler ADD COLUMN il TEXT");
            if (!sutun.Contains("bolge")) Calistir(c, "ALTER TABLE teklifler ADD COLUMN bolge INTEGER");
            if (!sutun.Contains("dograma")) Calistir(c, "ALTER TABLE teklifler ADD COLUMN dograma TEXT");
            if (!sutun.Contains("mevcut")) Calistir(c, "ALTER TABLE teklifler ADD COLUMN mevcut INTEGER");
            if (!sutun.Contains("yakit")) Calistir(c, "ALTER TABLE teklifler ADD COLUMN yakit TEXT");
            if (!sutun.Contains("sogutma")) Calistir(c, "ALTER TABLE teklifler ADD COLUMN sogutma INTEGER");
            if (!sutun.Contains("enerji_artisi")) Calistir(c, "ALTER TABLE teklifler ADD COLUMN enerji_artisi REAL");
        }

        static void ReferansYaz(DbConnection c, VeriDeposu v, string surum)
        {
            // Referans tablolar her güncellemede baştan kurulur (önceki sürümün sütunları kalmasın); teklifler tablosuna dokunulmaz
            Calistir(c, "DROP TABLE IF EXISTS bina_tipleri; DROP TABLE IF EXISTS malzemeler; DROP TABLE IF EXISTS cati_zemin; DROP TABLE IF EXISTS parametreler; " +
                        "DROP TABLE IF EXISTS il_bolgeleri; DROP TABLE IF EXISTS iller; DROP TABLE IF EXISTS ts825_bolgeler; DROP TABLE IF EXISTS levha_fiyatlari; DROP TABLE IF EXISTS ts825_pencere; DROP TABLE IF EXISTS yakitlar; DROP TABLE IF EXISTS yangin_siniflari;");
            SemaKur(c);
            using var t = c.BeginTransaction();

            foreach (var il in v.Iller)
                Calistir(c, "INSERT INTO iller VALUES (@il,@h,@s)", t, ("@il", il.Ad), ("@h", il.IsitmaDG), ("@s", il.SogutmaDG));
            foreach (var b in v.Bolgeler)
                Calistir(c, "INSERT INTO ts825_bolgeler VALUES (@n,@d,@c,@z,@p)", t, ("@n", b.No), ("@d", b.UDuvar), ("@c", b.UCati), ("@z", b.UZemin), ("@p", b.UPencere));
            foreach (var il in v.Iller.Where(i => i.Ts825Bolge.HasValue))
                Calistir(c, "INSERT INTO il_bolgeleri VALUES (@il,@b,@k)", t, ("@il", il.Ad), ("@b", il.Ts825Bolge!.Value), ("@k", il.BolgeKaynagi));
            foreach (var p in v.PencereDegerleri)
                Calistir(c, "INSERT INTO ts825_pencere VALUES (@d,@c,@a,@u,@k)", t, ("@d", p.Dograma), ("@c", p.CamTipi), ("@a", p.AraBoslukMm), ("@u", p.U), ("@k", p.Kaynak));
            foreach (var y in v.Yakitlar)
                Calistir(c, "INSERT INTO yakitlar VALUES (@id,@y,@i,@b,@f,@fh,@fk,@ft,@id2,@idk,@v,@vk,@e,@ek)", t, ("@id", y.Id), ("@y", y.Ad), ("@i", y.Isitici), ("@b", y.Birim),
                    ("@f", y.FiyatBirim), ("@fh", y.FiyatHesabi), ("@fk", y.FiyatKaynagi), ("@ft", y.FiyatTarihi), ("@id2", y.IsilDegerKWh), ("@idk", y.IsilDegerKaynagi),
                    ("@v", y.Verim), ("@vk", y.VerimKaynagi), ("@e", y.EmisyonKgKWh), ("@ek", y.EmisyonKaynagi));
            foreach (var y in v.YanginSiniflari)
                Calistir(c, "INSERT INTO yangin_siniflari VALUES (@a,@u,@s,@k,@kk)", t, ("@a", y.Aile), ("@u", y.Urun), ("@s", y.Sistem), ("@k", y.Karsilik), ("@kk", y.Kaynak));
            int lno = 0;
            foreach (var l in v.Levhalar)
                Calistir(c, "INSERT INTO levha_fiyatlari VALUES (@id,@a,@cm,@f,@u,@k,@t)", t, ("@id", ++lno), ("@a", l.Aile), ("@cm", l.KalinlikCm), ("@f", l.FiyatM2), ("@u", l.Urun), ("@k", l.Kaynak), ("@t", l.Tarih));
            foreach (var f in v.Formlar)
                Calistir(c, "INSERT INTO bina_tipleri VALUES (@no,@x1,@x2,@x3,@x4,@x5,@v)", t,
                    ("@no", f.No), ("@x1", f.Kompaktlik), ("@x2", f.YuzeyAlani), ("@x3", f.DuvarAlani), ("@x4", f.CatiAlani), ("@x5", f.Yukseklik), ("@v", f.Hacim));
            foreach (var m in v.Malzemeler.Where(m => m.Tur is not ('C' or 'Z')))
                Calistir(c, "INSERT INTO malzemeler VALUES (@id,@kat,@ad,@d,@l,@u,@k,@f,@fh,@fk,@ft,@tk,@kk,@ba)", t, ("@ba", m.BirimAgirlik),
                    ("@id", m.Id), ("@kat", m.Kategori), ("@ad", m.Ad), ("@d", m.Kalinlik), ("@l", m.Lambda), ("@u", m.U),
                    ("@k", m.KarbonM2), ("@f", m.FiyatM2), ("@fh", m.FiyatHesabi), ("@fk", m.FiyatKaynagi), ("@ft", m.FiyatTarihi),
                    ("@tk", m.TeknikKaynak), ("@kk", m.KarbonKaynagi));
            foreach (var m in v.Malzemeler.Where(m => m.Tur is 'C' or 'Z'))
                Calistir(c, "INSERT INTO cati_zemin VALUES (@id,@kat,@ad,@d,@l,@f,@fh,@fk,@ft,@tk)", t,
                    ("@id", m.Id), ("@kat", m.Kategori), ("@ad", m.Ad), ("@d", m.Kalinlik), ("@l", m.Lambda), ("@f", m.FiyatM2),
                    ("@fh", m.FiyatHesabi), ("@fk", m.FiyatKaynagi), ("@ft", m.FiyatTarihi), ("@tk", m.TeknikKaynak));
            foreach (var kv in v.Parametre)
            {
                var bilgi = v.ParametreBilgi.TryGetValue(kv.Key, out var b) ? b : ("", "", "");
                Calistir(c, "INSERT INTO parametreler VALUES (@id,@ad,@d,@b,@k)", t,
                    ("@id", kv.Key), ("@ad", bilgi.Item1), ("@d", kv.Value), ("@b", bilgi.Item2), ("@k", bilgi.Item3));
            }
            Calistir(c, "INSERT OR REPLACE INTO veri_surumu VALUES ('csv', @s)", t, ("@s", surum));
            t.Commit();
        }

        static VeriDeposu Oku(DbConnection c)
        {
            var v = new VeriDeposu { Kaynak = "SQLite veritabanı" };
            using (var cmd = Komut(c, "SELECT tip,kompaktlik,yuzey_alani_m2,duvar_alani_m2,cati_alani_m2,yukseklik_m,hacim_m3 FROM bina_tipleri ORDER BY tip"))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    v.Formlar.Add(new BinaFormu { No = I(r, 0), Kompaktlik = D(r, 1), YuzeyAlani = D(r, 2), DuvarAlani = D(r, 3), CatiAlani = D(r, 4), Yukseklik = D(r, 5), Hacim = D(r, 6) });
            using (var cmd = Komut(c, "SELECT id,kategori,ad,kalinlik_m,lambda_W_mK,U_W_m2K,karbon_kgCO2e_m2,fiyat_TL_m2,fiyat_hesabi,fiyat_kaynagi,fiyat_tarihi,teknik_kaynak,karbon_kaynagi,birim_agirlik_kg_m2 FROM malzemeler ORDER BY rowid"))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    v.Malzemeler.Add(new Malzeme
                    {
                        Id = S(r, 0), Kategori = S(r, 1), Ad = S(r, 2), Kalinlik = D(r, 3), Lambda = D(r, 4), U = D(r, 5), KarbonM2 = D(r, 6), FiyatM2 = D(r, 7),
                        FiyatHesabi = S(r, 8), FiyatKaynagi = S(r, 9), FiyatTarihi = S(r, 10), TeknikKaynak = S(r, 11), KarbonKaynagi = S(r, 12), BirimAgirlik = D(r, 13),
                    });
            using (var cmd = Komut(c, "SELECT id,parametre,deger,birim,kaynak FROM parametreler"))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) { v.Parametre[S(r, 0)] = D(r, 2); v.ParametreBilgi[S(r, 0)] = (S(r, 1), S(r, 3), S(r, 4)); }
            using (var cmd = Komut(c, "SELECT id,kategori,ad,kalinlik_m,lambda_W_mK,fiyat_TL_m2,fiyat_hesabi,fiyat_kaynagi,fiyat_tarihi,teknik_kaynak FROM cati_zemin ORDER BY rowid"))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    v.Malzemeler.Add(new Malzeme { Id = S(r, 0), Kategori = S(r, 1), Ad = S(r, 2), Kalinlik = D(r, 3), Lambda = D(r, 4), FiyatM2 = D(r, 5),
                                                   FiyatHesabi = S(r, 6), FiyatKaynagi = S(r, 7), FiyatTarihi = S(r, 8), TeknikKaynak = S(r, 9) });
            using (var cmd = Komut(c, "SELECT il,isitma_derece_gun,sogutma_derece_gun FROM iller ORDER BY rowid"))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) v.Iller.Add(new Il { Ad = S(r, 0), IsitmaDG = D(r, 1), SogutmaDG = D(r, 2) });
            using (var cmd = Komut(c, "SELECT il,ts825_bolge,kaynak FROM il_bolgeleri"))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) if (v.Iller.FirstOrDefault(i => i.Ad == S(r, 0)) is Il il) { il.Ts825Bolge = I(r, 1); il.BolgeKaynagi = S(r, 2); }
            using (var cmd = Komut(c, "SELECT bolge,U_duvar,U_cati,U_zemin,U_pencere FROM ts825_bolgeler ORDER BY bolge"))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) v.Bolgeler.Add(new Ts825Bolge { No = I(r, 0), UDuvar = D(r, 1), UCati = D(r, 2), UZemin = D(r, 3), UPencere = D(r, 4) });
            using (var cmd = Komut(c, "SELECT id,yakit,isitici,birim,fiyat_TL_birim,fiyat_hesabi,fiyat_kaynagi,fiyat_tarihi,isil_deger_kWh_birim,isil_deger_kaynagi,verim,verim_kaynagi,emisyon_kgCO2_kWh,emisyon_kaynagi FROM yakitlar ORDER BY rowid"))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) v.Yakitlar.Add(new Yakit { Id = S(r, 0), Ad = S(r, 1), Isitici = S(r, 2), Birim = S(r, 3), FiyatBirim = D(r, 4), FiyatHesabi = S(r, 5), FiyatKaynagi = S(r, 6),
                    FiyatTarihi = S(r, 7), IsilDegerKWh = D(r, 8), IsilDegerKaynagi = S(r, 9), Verim = D(r, 10), VerimKaynagi = S(r, 11), EmisyonKgKWh = D(r, 12), EmisyonKaynagi = S(r, 13) });
            using (var cmd = Komut(c, "SELECT aile,urun_yangin_sinifi,sistem_yangin_sinifi,yonetmelik_karsiligi,kaynak FROM yangin_siniflari"))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) v.YanginSiniflari.Add(new YanginSinifi { Aile = S(r, 0), Urun = S(r, 1), Sistem = S(r, 2), Karsilik = S(r, 3), Kaynak = S(r, 4) });
            using (var cmd = Komut(c, "SELECT dograma,cam_tipi,ara_bosluk_mm,U_pencere_W_m2K,kaynak FROM ts825_pencere ORDER BY rowid"))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) v.PencereDegerleri.Add(new PencereDegeri { Dograma = S(r, 0), CamTipi = S(r, 1), AraBoslukMm = I(r, 2), U = D(r, 3), Kaynak = S(r, 4) });
            using (var cmd = Komut(c, "SELECT aile,kalinlik_cm,fiyat_TL_m2,urun,kaynak,tarih FROM levha_fiyatlari ORDER BY id"))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) v.Levhalar.Add(new LevhaFiyati { Aile = S(r, 0), KalinlikCm = D(r, 1), FiyatM2 = D(r, 2), Urun = S(r, 3), Kaynak = S(r, 4), Tarih = S(r, 5) });
            if (v.Formlar.Count == 0 || v.Malzemeler.Count == 0 || v.Parametre.Count == 0)
                throw new InvalidDataException("Veritabanındaki referans tablolar boş. Veri klasöründeki CSV dosyalarını kontrol edin.");
            return v;
        }

        // ---------------- Teklifler ----------------
        public long TeklifKaydet(KayitliTeklif k)
        {
            using var c = SqliteBaglanti.Ac(Dosya);
            SemaKur(c);
            Calistir(c, @"INSERT INTO teklifler (tarih,proje_adi,paket,bina_ozeti,form_no,en,boy,kat,yon,cam_orani,yil,tarife,ts825_filtre,oncelik,tb1,tb2,tb3,
yalitim_id,cam_id,duvar_id,ilk_yatirim,kdv_dahil,yillik_isitma_kwh,yillik_gider_tl,omur_boyu_maliyet,co2_ton,ts825_uygun,il,bolge,dograma,mevcut,yakit,sogutma,enerji_artisi)
VALUES (@tarih,@proje,@paket,@bina,@form,@en,@boy,@kat,@yon,@cam,@yil,@tarife,@filtre,@onc,@tb1,@tb2,@tb3,@y,@p,@d,@iy,@kdv,@isi,@gid,@lcc,@co2,@ts,@il,@bolge,@dog,@mev,@yak,@sog,@art)", null,
                ("@tarih", k.Tarih.ToString("yyyy-MM-dd HH:mm:ss", INV)), ("@proje", k.ProjeAdi), ("@paket", k.Paket), ("@bina", k.BinaOzeti),
                ("@form", k.FormNo), ("@en", k.En), ("@boy", k.Boy), ("@kat", k.Kat), ("@yon", k.Yon), ("@cam", k.CamOrani), ("@yil", k.Yil),
                ("@tarife", k.Tarife), ("@filtre", k.Ts825Filtre ? 1 : 0), ("@onc", k.Oncelik), ("@tb1", k.Tb1), ("@tb2", k.Tb2), ("@tb3", k.Tb3),
                ("@y", k.YalitimId), ("@p", k.CamId), ("@d", k.DuvarId), ("@iy", k.IlkYatirim), ("@kdv", k.KdvDahil), ("@isi", k.YillikIsitmaKWh),
                ("@gid", k.YillikGiderTL), ("@lcc", k.OmurBoyuMaliyet), ("@co2", k.Co2Ton), ("@ts", k.Ts825Uygun ? 1 : 0), ("@il", k.Il), ("@bolge", k.Bolge), ("@dog", k.Dograma), ("@mev", k.Mevcut ? 1 : 0), ("@yak", k.Yakit), ("@sog", k.Sogutma ? 1 : 0), ("@art", k.EnerjiArtisi));
            return Convert.ToInt64(Tek(c, "SELECT last_insert_rowid()"), INV);
        }

        public List<KayitliTeklif> Teklifler()
        {
            using var c = SqliteBaglanti.Ac(Dosya);
            SemaKur(c);
            var l = new List<KayitliTeklif>();
            using var cmd = Komut(c, @"SELECT id,tarih,proje_adi,paket,bina_ozeti,form_no,en,boy,kat,yon,cam_orani,yil,tarife,ts825_filtre,oncelik,tb1,tb2,tb3,
yalitim_id,cam_id,duvar_id,ilk_yatirim,kdv_dahil,yillik_isitma_kwh,yillik_gider_tl,omur_boyu_maliyet,co2_ton,ts825_uygun,il,bolge,dograma,mevcut,yakit,sogutma,enerji_artisi FROM teklifler ORDER BY id DESC");
            using var r = cmd.ExecuteReader();
            while (r.Read())
                l.Add(new KayitliTeklif
                {
                    Id = Convert.ToInt64(r.GetValue(0), INV),
                    Tarih = DateTime.ParseExact(S(r, 1), "yyyy-MM-dd HH:mm:ss", INV),
                    ProjeAdi = S(r, 2), Paket = S(r, 3), BinaOzeti = S(r, 4),
                    FormNo = I(r, 5), En = D(r, 6), Boy = D(r, 7), Kat = I(r, 8), Yon = I(r, 9), CamOrani = D(r, 10), Yil = I(r, 11),
                    Tarife = I(r, 12), Ts825Filtre = I(r, 13) == 1, Oncelik = I(r, 14), Tb1 = I(r, 15), Tb2 = I(r, 16), Tb3 = I(r, 17),
                    YalitimId = S(r, 18), CamId = S(r, 19), DuvarId = S(r, 20),
                    IlkYatirim = D(r, 21), KdvDahil = D(r, 22), YillikIsitmaKWh = D(r, 23), YillikGiderTL = D(r, 24), OmurBoyuMaliyet = D(r, 25),
                    Co2Ton = D(r, 26), Ts825Uygun = I(r, 27) == 1, Il = S(r, 28), Bolge = I(r, 29), Dograma = S(r, 30), Mevcut = I(r, 31) == 1, Yakit = S(r, 32) is { Length: > 0 } yk ? yk : "DG", Sogutma = I(r, 33) == 1, EnerjiArtisi = D(r, 34),
                });
            return l;
        }

        // ---------------- İşlem günlüğü (yalnız veritabanında; arayüzde gösterilmez) ----------------
        public const int GunlukSiniri = 20000;   // en eski kayıtlar silinir, dosya büyümez

        /// <summary>İşlem ya da hata kaydı yazar. Günlük yazılamazsa uygulama çalışmaya devam eder (hata yutulur).</summary>
        public void Kaydet(string tur, string islem, string ayrinti = "")
        {
            try
            {
                using var c = SqliteBaglanti.Ac(Dosya);
                SemaKur(c);
                KayitYaz(c, tur, islem, ayrinti);
            }
            catch { }
        }

        static void KayitYaz(DbConnection c, string tur, string islem, string ayrinti)
        {
            Calistir(c, "INSERT INTO islem_kayitlari (tarih,tur,islem,ayrinti,kullanici,bilgisayar) VALUES (@t,@tur,@i,@a,@k,@b)", null,
                ("@t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", INV)), ("@tur", tur), ("@i", islem), ("@a", ayrinti ?? ""),
                ("@k", Environment.UserName), ("@b", Environment.MachineName));
            Calistir(c, $"DELETE FROM islem_kayitlari WHERE id <= (SELECT MAX(id) FROM islem_kayitlari) - {GunlukSiniri}");
        }

        /// <summary>Son günlük kayıtları (testler ve inceleme için), en yeni üstte.</summary>
        public List<(DateTime tarih, string tur, string islem, string ayrinti)> IslemKayitlari(int adet = 100)
        {
            using var c = SqliteBaglanti.Ac(Dosya);
            SemaKur(c);
            var l = new List<(DateTime, string, string, string)>();
            using var cmd = Komut(c, "SELECT tarih,tur,islem,ayrinti FROM islem_kayitlari ORDER BY id DESC LIMIT @n", null, ("@n", adet));
            using var r = cmd.ExecuteReader();
            while (r.Read()) l.Add((DateTime.ParseExact(S(r, 0), "yyyy-MM-dd HH:mm:ss", INV), S(r, 1), S(r, 2), S(r, 3)));
            return l;
        }

        public void TeklifSil(long id)
        {
            using var c = SqliteBaglanti.Ac(Dosya);
            Calistir(c, "DELETE FROM teklifler WHERE id=@id", null, ("@id", id));
        }

        /// <summary>Tablo satır sayıları (bilgi ekranı ve testler için).</summary>
        public Dictionary<string, long> SatirSayilari()
        {
            using var c = SqliteBaglanti.Ac(Dosya);
            SemaKur(c);
            return new[] { "bina_tipleri", "malzemeler", "cati_zemin", "parametreler", "iller", "il_bolgeleri", "ts825_bolgeler", "ts825_pencere", "levha_fiyatlari", "yakitlar", "yangin_siniflari", "teklifler", "islem_kayitlari" }
                .ToDictionary(t => t, t => Convert.ToInt64(Tek(c, $"SELECT COUNT(*) FROM {t}"), INV));
        }

        // ---------------- Yardımcılar ----------------
        static DbCommand Komut(DbConnection c, string sql, DbTransaction? t = null, params (string ad, object deger)[] p)
        {
            var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = t;
            foreach (var (ad, deger) in p)
            {
                var par = cmd.CreateParameter();
                par.ParameterName = ad;
                par.Value = deger ?? DBNull.Value;
                cmd.Parameters.Add(par);
            }
            return cmd;
        }

        static void Calistir(DbConnection c, string sql, DbTransaction? t = null, params (string, object)[] p)
        {
            using var cmd = Komut(c, sql, t, p);
            cmd.ExecuteNonQuery();
        }

        static string? Tek(DbConnection c, string sql)
        {
            using var cmd = Komut(c, sql);
            var o = cmd.ExecuteScalar();
            return o == null || o is DBNull ? null : Convert.ToString(o, INV);
        }

        /// <summary>REAL sütunu tam (double) duyarlıkla okur. GetDouble, sürücünün sütun tipi eşlemesinden bağımsız olarak sqlite3_column_double kullanır.</summary>
        static double D(DbDataReader r, int i)
        {
            if (r.IsDBNull(i)) return 0;
            try { return r.GetDouble(i); }
            catch (InvalidCastException) { return Convert.ToDouble(r.GetValue(i), INV); }
        }
        static int I(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i), INV);
        static string S(DbDataReader r, int i) => r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i), INV) ?? "";
    }
}
