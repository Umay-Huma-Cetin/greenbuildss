using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EcoBuildss.Core.Veri;

namespace EcoBuildss.Core.Depo
{
    /// <summary>Uygulamada kaydedilen bir teklif (müşteriye verilen) ve sonucu.</summary>
    public sealed class KayitliTeklif
    {
        public long Id;
        public string TeklifNo = "", Musteri = "", MusteriTipi = "Bireysel", Il = "", ProjeTuru = "Yeni bina", Paket = "Önerilen", Oncelik = "Dengeli";
        public DateTime Tarih = DateTime.Today;
        public double KullanimAlani, TeklifTL, TeklifTLm2, IndirimOrani, YanitSuresiGun;
        public double? GeriOdemeYil, KabulOlasiligi;
        public bool RakipTeklif;
        public string YalitimId = "", CamId = "", DuvarId = "", Dograma = "", Yakit = "DG";
        public double En, Boy, KatYuksekligi, CamOrani; public int KatSayisi; public bool Sogutma;
        public int DaireSayisi;   // apartman mantolamasında daire başına paylaşım için
        public string Hazirlayan = "";   // teklifi kaydeden kullanıcı
        public string Sonuc = "Bekliyor";   // Bekliyor / Kabul / Red
    }

    /// <summary>
    /// Operasyonel veritabanı (SQLite): içe aktarılan gerçek veri, kaydedilen teklifler ve sonuçları, model çalıştırma geçmişi, işlem günlüğü.
    /// Referans veri (iklim, yönetmelik, fiyat) kaynak gösterimli CSV dosyalarında kalır ve değiştirilmez.
    /// </summary>
    public sealed partial class Veritabani : IDisposable
    {
        readonly SqliteBaglanti b;
        public string Dosya => b.Dosya;

        public Veritabani(string dosya)
        {
            b = new SqliteBaglanti(dosya);
            b.Calistir(@"
CREATE TABLE IF NOT EXISTS veri_kayitlari (
  veri_seti TEXT NOT NULL, anahtar TEXT NOT NULL, kaynak TEXT NOT NULL CHECK (kaynak IN ('Gerçek','Örnek')),
  satir TEXT NOT NULL, eklenme TEXT NOT NULL, PRIMARY KEY (veri_seti, anahtar));
CREATE TABLE IF NOT EXISTS teklifler (
  id INTEGER PRIMARY KEY AUTOINCREMENT, teklif_no TEXT NOT NULL UNIQUE, tarih TEXT NOT NULL, musteri TEXT, musteri_tipi TEXT NOT NULL,
  il TEXT NOT NULL, proje_turu TEXT NOT NULL, kullanim_alani REAL NOT NULL, paket TEXT NOT NULL, oncelik TEXT NOT NULL,
  yalitim_id TEXT, cam_id TEXT, duvar_id TEXT, dograma TEXT, yakit TEXT, en REAL, boy REAL, kat_sayisi INTEGER, kat_yuksekligi REAL, cam_orani REAL, sogutma INTEGER,
  teklif_tl REAL NOT NULL, teklif_tl_m2 REAL NOT NULL, geri_odeme_yil REAL, indirim_orani REAL NOT NULL DEFAULT 0, yanit_suresi_gun REAL NOT NULL DEFAULT 0,
  rakip_teklif INTEGER NOT NULL DEFAULT 0, kabul_olasiligi REAL,
  sonuc TEXT NOT NULL DEFAULT 'Bekliyor' CHECK (sonuc IN ('Bekliyor','Kabul','Red')));
CREATE TABLE IF NOT EXISTS model_calismalari (
  id INTEGER PRIMARY KEY AUTOINCREMENT, tarih TEXT NOT NULL, model TEXT NOT NULL, veri_kaynagi TEXT NOT NULL, kayit_sayisi INTEGER NOT NULL, ozet TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS islem_gunlugu (id INTEGER PRIMARY KEY AUTOINCREMENT, tarih TEXT NOT NULL, islem TEXT NOT NULL, ayrinti TEXT);
CREATE TABLE IF NOT EXISTS ayarlar (anahtar TEXT PRIMARY KEY, deger TEXT NOT NULL)");
            // Eski sürümden gelen veritabanına yeni sütun eklenir (varsa hata yok sayılır)
            if (!b.Sorgu("PRAGMA table_info(teklifler)").Any(r => (string?)r["name"] == "daire_sayisi"))
                b.Calistir("ALTER TABLE teklifler ADD COLUMN daire_sayisi INTEGER NOT NULL DEFAULT 0");
            if (!b.Sorgu("PRAGMA table_info(teklifler)").Any(r => (string?)r["name"] == "hazirlayan"))
                b.Calistir("ALTER TABLE teklifler ADD COLUMN hazirlayan TEXT NOT NULL DEFAULT ''");
            GuvenlikKur();
        }


        // ---------------- İçe aktarılan veri ----------------
        /// <summary>Denetimden geçmiş kayıtları saklar. Aynı anahtar varsa güncellenir. Gerçek ve örnek veri karıştırılmaz: biri yüklenince öteki silinir.</summary>
        public int VeriKaydet(Sema sema, IEnumerable<Kayit> kayitlar, bool ornek)
        {
            Yetki(yonetici: true, "Veri içe aktarma");
            string kaynak = ornek ? "Örnek" : "Gerçek";
            int n = 0;
            b.Islem(() =>
            {
                b.Yaz("DELETE FROM veri_kayitlari WHERE veri_seti = @s AND kaynak <> @k", new Dictionary<string, object?> { ["s"] = sema.DosyaAdi, ["k"] = kaynak });
                foreach (var k in kayitlar)
                {
                    b.Yaz("INSERT OR REPLACE INTO veri_kayitlari (veri_seti, anahtar, kaynak, satir, eklenme) VALUES (@s, @a, @k, @r, @e)", new Dictionary<string, object?>
                    { ["s"] = sema.DosyaAdi, ["a"] = k.Metin(sema.AnahtarAlan!), ["k"] = kaynak, ["r"] = Sifrele(Satir(sema, k)), ["e"] = DateTime.Now });
                    n++;
                }
            });
            Gunluk("Veri içe aktarıldı", $"{sema.Ad}: {n} kayıt");
            return n;
        }

        static string Satir(Sema sema, Kayit k) => string.Join(",", sema.Alanlar.Select(a => k.Deger.TryGetValue(a.Ad, out var v) ? v switch
        {
            null => "", double d => d.ToString("R", CultureInfo.InvariantCulture), DateTime t => t.ToString("yyyy-MM-dd"), _ => Csv.Kacis(v.ToString()!),
        } : ""));

        /// <summary>Saklanan kayıtları şemaya göre yeniden denetleyerek okur (şema değiştiyse uyumsuz satırlar raporlanır).</summary>
        public (IceAktarmaSonucu sonuc, string kaynak) VeriOku(Sema sema)
        {
            var rows = b.Sorgu("SELECT satir, kaynak FROM veri_kayitlari WHERE veri_seti = @s ORDER BY anahtar", new Dictionary<string, object?> { ["s"] = sema.DosyaAdi });
            var sb = new StringBuilder(string.Join(",", sema.Alanlar.Select(a => a.Ad)) + "\n");
            foreach (var r in rows) sb.AppendLine(Coz((string)r["satir"]!));
            string kaynak = rows.Count == 0 ? "" : (string)rows[0]["kaynak"]!;
            return (IceAktarici.Metinden(sb.ToString(), sema), kaynak);
        }

        public void VeriSil(Sema sema) { Yetki(yonetici: true, "Veri silme"); b.Yaz("DELETE FROM veri_kayitlari WHERE veri_seti = @s", new Dictionary<string, object?> { ["s"] = sema.DosyaAdi }); Gunluk("Veri silindi", sema.Ad); }

        // ---------------- Teklifler ----------------
        public long TeklifKaydet(KayitliTeklif t)
        {
            Yetki(yonetici: false, "Teklif kaydetme");
            t.Hazirlayan = Aktif!.KullaniciAdi;
            var p = new Dictionary<string, object?>
            {
                ["no"] = t.TeklifNo, ["tarih"] = t.Tarih.ToString("yyyy-MM-dd"), ["musteri"] = "", ["mt"] = t.MusteriTipi, ["il"] = t.Il, ["pt"] = t.ProjeTuru,
                ["alan"] = t.KullanimAlani, ["paket"] = t.Paket, ["onc"] = t.Oncelik, ["y"] = t.YalitimId, ["c"] = t.CamId, ["d"] = t.DuvarId, ["dog"] = t.Dograma,
                ["yak"] = t.Yakit, ["en"] = t.En, ["boy"] = t.Boy, ["kat"] = t.KatSayisi, ["kh"] = t.KatYuksekligi, ["co"] = t.CamOrani, ["sog"] = t.Sogutma,
                ["tl"] = 0.0, ["tlm2"] = 0.0, ["go"] = t.GeriOdemeYil, ["ind"] = 0.0, ["yan"] = t.YanitSuresiGun, ["rak"] = t.RakipTeklif,
                ["ko"] = null, ["son"] = t.Sonuc, ["ds"] = t.DaireSayisi, ["haz"] = t.Hazirlayan,
                ["gizli"] = Sifrele(GizliBirlestir(t.Musteri, t.TeklifTL, t.TeklifTLm2, t.IndirimOrani, t.KabulOlasiligi)),
            };
            t.Id = b.Yaz(@"INSERT INTO teklifler (teklif_no, tarih, musteri, musteri_tipi, il, proje_turu, kullanim_alani, paket, oncelik, yalitim_id, cam_id, duvar_id, dograma, yakit,
                en, boy, kat_sayisi, kat_yuksekligi, cam_orani, sogutma, teklif_tl, teklif_tl_m2, geri_odeme_yil, indirim_orani, yanit_suresi_gun, rakip_teklif, kabul_olasiligi, sonuc, daire_sayisi, hazirlayan, gizli)
                VALUES (@no, @tarih, @musteri, @mt, @il, @pt, @alan, @paket, @onc, @y, @c, @d, @dog, @yak, @en, @boy, @kat, @kh, @co, @sog, @tl, @tlm2, @go, @ind, @yan, @rak, @ko, @son, @ds, @haz, @gizli)", p);
            Gunluk("Teklif kaydedildi", $"{t.TeklifNo} · {t.Il} · {t.Paket} · {t.TeklifTL:N0} TL");
            return t.Id;
        }

        public void SonucIsaretle(long id, string sonuc)
        {
            if (sonuc != "Bekliyor" && sonuc != "Kabul" && sonuc != "Red") throw new ArgumentException("Sonuç Bekliyor, Kabul ya da Red olmalı.");
            Yetki(yonetici: false, "Teklif sonucu işaretleme");
            string no = TeklifNo(id);
            b.Yaz("UPDATE teklifler SET sonuc = @s WHERE id = @id", new Dictionary<string, object?> { ["s"] = sonuc, ["id"] = id });
            Gunluk("Teklif sonucu", $"{no} → {sonuc}");
        }

        public void TeklifSil(long id)
        {
            Yetki(yonetici: true, "Teklif silme");
            var t = Teklifler().FirstOrDefault(x => x.Id == id);
            b.Yaz("DELETE FROM teklifler WHERE id = @id", new Dictionary<string, object?> { ["id"] = id });
            Gunluk("Teklif silindi", t == null ? $"#{id}" : $"{t.TeklifNo} · {t.Musteri} · {t.Il} · {t.TeklifTL:N0} TL", Seviye.Uyari);
        }

        string TeklifNo(long id) => b.Sorgu("SELECT teklif_no FROM teklifler WHERE id = @id", new Dictionary<string, object?> { ["id"] = id }).Select(r => S(r["teklif_no"])).FirstOrDefault() ?? $"#{id}";

        public List<KayitliTeklif> Teklifler() => b.Sorgu("SELECT * FROM teklifler ORDER BY tarih DESC, id DESC").Select(r => GizliAc(S(r["gizli"]), new KayitliTeklif
        {
            Id = (long)r["id"]!, TeklifNo = S(r["teklif_no"]), Tarih = DateTime.ParseExact(S(r["tarih"]), "yyyy-MM-dd", CultureInfo.InvariantCulture), Musteri = S(r["musteri"]),
            MusteriTipi = S(r["musteri_tipi"]), Il = S(r["il"]), ProjeTuru = S(r["proje_turu"]), KullanimAlani = D(r["kullanim_alani"]), Paket = S(r["paket"]), Oncelik = S(r["oncelik"]),
            YalitimId = S(r["yalitim_id"]), CamId = S(r["cam_id"]), DuvarId = S(r["duvar_id"]), Dograma = S(r["dograma"]), Yakit = S(r["yakit"]),
            En = D(r["en"]), Boy = D(r["boy"]), KatSayisi = (int)(r["kat_sayisi"] as long? ?? 1), KatYuksekligi = D(r["kat_yuksekligi"]), CamOrani = D(r["cam_orani"]), Sogutma = (r["sogutma"] as long?) == 1,
            TeklifTL = D(r["teklif_tl"]), TeklifTLm2 = D(r["teklif_tl_m2"]), GeriOdemeYil = r["geri_odeme_yil"] == null ? null : D(r["geri_odeme_yil"]),
            IndirimOrani = D(r["indirim_orani"]), YanitSuresiGun = D(r["yanit_suresi_gun"]), RakipTeklif = (r["rakip_teklif"] as long?) == 1,
            KabulOlasiligi = r["kabul_olasiligi"] == null ? null : D(r["kabul_olasiligi"]), Sonuc = S(r["sonuc"]), DaireSayisi = (int)(r["daire_sayisi"] as long? ?? 0), Hazirlayan = S(r["hazirlayan"]),
        })).ToList();

        /// <summary>Şifreli alanlar (müşteri, tutarlar, indirim, kabul olasılığı) çözülür.</summary>
        KayitliTeklif GizliAc(string gizli, KayitliTeklif t)
        {
            if (gizli.Length == 0) return t;
            string duz = Coz(gizli);
            var p = duz.Split('\u001f');
            if (p.Length != 5) { t.Musteri = duz; return t; }   // "(şifreli)" ya da bozuk
            double Sayi(string x) => double.Parse(x, CultureInfo.InvariantCulture);
            t.Musteri = p[0]; t.TeklifTL = Sayi(p[1]); t.TeklifTLm2 = Sayi(p[2]); t.IndirimOrani = Sayi(p[3]); t.KabulOlasiligi = p[4].Length == 0 ? null : Sayi(p[4]);
            return t;
        }

        /// <summary>Sonucu belli olan (kabul/red) kayıtlı teklifler, teklif geçmişi şemasında satır olarak — modeli firmanın kendi sonuçlarıyla besler.</summary>
        public string SonuclananTekliflerCsv(Sema teklifSemasi)
        {
            var sb = new StringBuilder(string.Join(",", teklifSemasi.Alanlar.Select(a => a.Ad)) + "\n");
            foreach (var t in Teklifler().Where(t => t.Sonuc != "Bekliyor"))
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv.Kacis(t.TeklifNo), t.Tarih.ToString("yyyy-MM-dd"), Csv.Kacis(t.Il), t.MusteriTipi, t.ProjeTuru, Csv.Sayi(t.KullanimAlani), t.Paket, t.Oncelik,
                    Csv.Sayi(t.TeklifTLm2), t.GeriOdemeYil.HasValue ? Csv.Sayi(t.GeriOdemeYil.Value) : "", Csv.Sayi(t.IndirimOrani), Csv.Sayi(t.YanitSuresiGun),
                    t.RakipTeklif ? "Evet" : "Hayır", t.Sonuc,
                }));
            return sb.ToString();
        }

        public string YeniTeklifNo()
        {
            string on = $"EB-{DateTime.Today:yyyy}-";
            var r = b.Sorgu("SELECT COUNT(*) AS n FROM teklifler WHERE teklif_no LIKE @p", new Dictionary<string, object?> { ["p"] = on + "%" });
            return on + ((long)r[0]["n"]! + 1).ToString("0000");
        }

        // ---------------- Ayarlar (firma bilgileri) ----------------
        public string Ayar(string anahtar, string varsayilan = "")
        {
            var r = b.Sorgu("SELECT deger FROM ayarlar WHERE anahtar = @a", new Dictionary<string, object?> { ["a"] = anahtar });
            return r.Count == 0 ? varsayilan : (string)r[0]["deger"]!;
        }
        public void AyarYaz(string anahtar, string deger)
        {
            Yetki(yonetici: true, "Firma bilgilerini değiştirme");
            Gunluk("Ayar değişti", $"{anahtar} = {deger}");
            b.Yaz("INSERT OR REPLACE INTO ayarlar (anahtar, deger) VALUES (@a, @d)", new Dictionary<string, object?> { ["a"] = anahtar, ["d"] = deger });
        }

        // ---------------- Model çalışmaları ----------------
        public void ModelKaydet(string model, string kaynak, int n, string ozet) =>
            b.Yaz("INSERT INTO model_calismalari (tarih, model, veri_kaynagi, kayit_sayisi, ozet) VALUES (@t, @m, @k, @n, @o)",
                  new Dictionary<string, object?> { ["t"] = DateTime.Now, ["m"] = model, ["k"] = kaynak, ["n"] = n, ["o"] = ozet });

        public List<Dictionary<string, object?>> ModelGecmisi() => b.Sorgu("SELECT tarih, model, veri_kaynagi, kayit_sayisi, ozet FROM model_calismalari ORDER BY id DESC LIMIT 200");


        static string S(object? o) => o?.ToString() ?? "";
        static double D(object? o) => o switch { double d => d, long l => l, _ => 0 };
        public void Dispose() => b.Dispose();
    }
}
