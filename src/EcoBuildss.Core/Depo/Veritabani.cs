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
        public double Cevre, Taban;   // dikdörtgen olmayan planda dış çevre (m) ve taban alanı (m²); dikdörtgende 0
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
        /// <summary>
        /// Teklif kaydı. Düz sütunlarda yalnız teklif no, tarih ve sonuç kalır; müşteri, il, bina, tutar, paket, hazırlayan dahil
        /// geri kalan her şey tek bir şifreli alanda (gizli) tutulur.
        /// </summary>
        public long TeklifKaydet(KayitliTeklif t)
        {
            Yetki(yonetici: false, "Teklif kaydetme");
            if (dek == null) throw new InvalidOperationException("Veri anahtarı açık değil; yeniden giriş yapın.");
            t.Hazirlayan = Aktif!.KullaniciAdi;
            b.Islem(() =>
            {
            t.Id = b.Yaz(@"INSERT INTO teklifler (teklif_no, tarih, musteri, musteri_tipi, il, proje_turu, kullanim_alani, paket, oncelik, teklif_tl, teklif_tl_m2, indirim_orani, yanit_suresi_gun, rakip_teklif, sonuc, daire_sayisi, hazirlayan, gizli)
                VALUES (@no, @tarih, '', '', '', '', 0, '', '', 0, 0, 0, 0, 0, @son, 0, '', '')",
                new Dictionary<string, object?> { ["no"] = t.TeklifNo, ["tarih"] = t.Tarih.ToString("yyyy-MM-dd"), ["son"] = t.Sonuc });
            TeklifGizliYaz(t);
            });
            Gunluk("Teklif kaydedildi", $"{t.TeklifNo} · {t.Il} · {t.Paket} · {t.TeklifTL:N0} TL");
            return t.Id;
        }

        void TeklifGizliYaz(KayitliTeklif t) =>
            b.Yaz(@"UPDATE teklifler SET musteri = '', musteri_tipi = '', il = '', proje_turu = '', kullanim_alani = 0, paket = '', oncelik = '', yalitim_id = NULL, cam_id = NULL,
                duvar_id = NULL, dograma = NULL, yakit = NULL, en = NULL, boy = NULL, kat_sayisi = NULL, kat_yuksekligi = NULL, cam_orani = NULL, sogutma = NULL, teklif_tl = 0,
                teklif_tl_m2 = 0, geri_odeme_yil = NULL, indirim_orani = 0, yanit_suresi_gun = 0, rakip_teklif = 0, kabul_olasiligi = NULL, daire_sayisi = 0, hazirlayan = '', gizli = @g WHERE id = @id",
                new Dictionary<string, object?> { ["g"] = Sifrele(TeklifPaketle(t)), ["id"] = t.Id });

        const char Ayrac = '\u001f', Esit = '\u001d';
        static string Ic(double x) => x.ToString("R", CultureInfo.InvariantCulture);
        static double Dis(string x) => double.Parse(x, CultureInfo.InvariantCulture);
        static string Temiz(string x) => x.Replace(Ayrac, ' ').Replace(Esit, ' ');

        static string TeklifPaketle(KayitliTeklif t)
        {
            var d = new Dictionary<string, string>
            {
                ["musteri"] = t.Musteri, ["mt"] = t.MusteriTipi, ["il"] = t.Il, ["pt"] = t.ProjeTuru, ["alan"] = Ic(t.KullanimAlani), ["paket"] = t.Paket, ["onc"] = t.Oncelik,
                ["y"] = t.YalitimId, ["c"] = t.CamId, ["d"] = t.DuvarId, ["dog"] = t.Dograma, ["yak"] = t.Yakit, ["en"] = Ic(t.En), ["boy"] = Ic(t.Boy), ["kat"] = t.KatSayisi.ToString(CultureInfo.InvariantCulture),
                ["kh"] = Ic(t.KatYuksekligi), ["co"] = Ic(t.CamOrani), ["sog"] = t.Sogutma ? "1" : "0", ["tl"] = Ic(t.TeklifTL), ["tlm2"] = Ic(t.TeklifTLm2),
                ["go"] = t.GeriOdemeYil.HasValue ? Ic(t.GeriOdemeYil.Value) : "", ["ind"] = Ic(t.IndirimOrani), ["yan"] = Ic(t.YanitSuresiGun), ["rak"] = t.RakipTeklif ? "1" : "0",
                ["ko"] = t.KabulOlasiligi.HasValue ? Ic(t.KabulOlasiligi.Value) : "", ["ds"] = t.DaireSayisi.ToString(CultureInfo.InvariantCulture), ["haz"] = t.Hazirlayan,
                ["cev"] = Ic(t.Cevre), ["tab"] = Ic(t.Taban),
            };
            return "v2" + Ayrac + string.Join(Ayrac.ToString(), d.Select(kv => kv.Key + Esit + Temiz(kv.Value ?? "")));
        }

        static void TeklifAc(string duz, KayitliTeklif t)
        {
            var d = duz.Split(Ayrac).Skip(1).Select(x => x.Split(new[] { Esit }, 2)).Where(x => x.Length == 2).ToDictionary(x => x[0], x => x[1]);
            string G(string k) => d.TryGetValue(k, out var v) ? v : "";
            double N(string k) => G(k).Length == 0 ? 0 : Dis(G(k));
            t.Musteri = G("musteri"); t.MusteriTipi = G("mt"); t.Il = G("il"); t.ProjeTuru = G("pt"); t.KullanimAlani = N("alan"); t.Paket = G("paket"); t.Oncelik = G("onc");
            t.YalitimId = G("y"); t.CamId = G("c"); t.DuvarId = G("d"); t.Dograma = G("dog"); t.Yakit = G("yak"); t.En = N("en"); t.Boy = N("boy"); t.KatSayisi = (int)N("kat");
            t.KatYuksekligi = N("kh"); t.CamOrani = N("co"); t.Sogutma = G("sog") == "1"; t.TeklifTL = N("tl"); t.TeklifTLm2 = N("tlm2");
            t.GeriOdemeYil = G("go").Length == 0 ? null : N("go"); t.IndirimOrani = N("ind"); t.YanitSuresiGun = N("yan"); t.RakipTeklif = G("rak") == "1";
            t.KabulOlasiligi = G("ko").Length == 0 ? null : N("ko"); t.DaireSayisi = (int)N("ds"); t.Hazirlayan = G("haz");
            t.Cevre = N("cev"); t.Taban = N("tab");
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

        public List<KayitliTeklif> Teklifler() => TekliflerHam("");

        List<KayitliTeklif> TekliflerHam(string kosul) => b.Sorgu("SELECT * FROM teklifler " + kosul + " ORDER BY tarih DESC, id DESC").Select(r => GizliAc(S(r["gizli"]), new KayitliTeklif
        {
            Id = (long)r["id"]!, TeklifNo = S(r["teklif_no"]), Tarih = DateTime.ParseExact(S(r["tarih"]), "yyyy-MM-dd", CultureInfo.InvariantCulture), Musteri = S(r["musteri"]),
            MusteriTipi = S(r["musteri_tipi"]), Il = S(r["il"]), ProjeTuru = S(r["proje_turu"]), KullanimAlani = D(r["kullanim_alani"]), Paket = S(r["paket"]), Oncelik = S(r["oncelik"]),
            YalitimId = S(r["yalitim_id"]), CamId = S(r["cam_id"]), DuvarId = S(r["duvar_id"]), Dograma = S(r["dograma"]), Yakit = S(r["yakit"]),
            En = D(r["en"]), Boy = D(r["boy"]), KatSayisi = (int)(r["kat_sayisi"] as long? ?? 1), KatYuksekligi = D(r["kat_yuksekligi"]), CamOrani = D(r["cam_orani"]), Sogutma = (r["sogutma"] as long?) == 1,
            TeklifTL = D(r["teklif_tl"]), TeklifTLm2 = D(r["teklif_tl_m2"]), GeriOdemeYil = r["geri_odeme_yil"] == null ? null : D(r["geri_odeme_yil"]),
            IndirimOrani = D(r["indirim_orani"]), YanitSuresiGun = D(r["yanit_suresi_gun"]), RakipTeklif = (r["rakip_teklif"] as long?) == 1,
            KabulOlasiligi = r["kabul_olasiligi"] == null ? null : D(r["kabul_olasiligi"]), Sonuc = S(r["sonuc"]), DaireSayisi = (int)(r["daire_sayisi"] as long? ?? 0), Hazirlayan = S(r["hazirlayan"]),
        })).ToList();

        /// <summary>Şifreli alan çözülür. Yeni biçimde (v2) teklifin tüm ayrıntıları, eski biçimde yalnız müşteri ve tutarlar bulunur.</summary>
        KayitliTeklif GizliAc(string gizli, KayitliTeklif t)
        {
            if (gizli.Length == 0) return t;
            string duz = Coz(gizli);
            if (duz.StartsWith("v2" + Ayrac, StringComparison.Ordinal)) { TeklifAc(duz, t); return t; }
            var p = duz.Split(Ayrac);
            if (p.Length != 5) { t.Musteri = duz; return t; }   // "(şifreli)" ya da bozuk
            t.Musteri = p[0]; t.TeklifTL = Dis(p[1]); t.TeklifTLm2 = Dis(p[2]); t.IndirimOrani = Dis(p[3]); t.KabulOlasiligi = p[4].Length == 0 ? null : Dis(p[4]);
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
            // Silinen teklif olsa da numara çakışmasın: o yılın en büyük numarasının bir fazlası
            string on = $"EB-{DateTime.Today:yyyy}-";
            long enBuyuk = b.Sorgu("SELECT teklif_no FROM teklifler WHERE teklif_no LIKE @p", new Dictionary<string, object?> { ["p"] = on + "%" })
                .Select(r => long.TryParse(S(r["teklif_no"]).Substring(on.Length), out var n) ? n : 0).DefaultIfEmpty(0).Max();
            return on + (enBuyuk + 1).ToString("0000");
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

        // ---------------- Fiyat güncellemeleri ----------------
        /// <summary>Yöneticinin girdiği güncel fiyat (referans dosya değişmez). Kim, ne zaman, eski ve yeni değer günlüğe yazılır.</summary>
        public void FiyatKaydet(string id, double fiyat, string kaynak, double eski)
        {
            Yetki(yonetici: true, "Fiyat güncelleme");
            string tarih = DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            b.Yaz("INSERT OR REPLACE INTO ayarlar (anahtar, deger) VALUES (@a, @d)", new Dictionary<string, object?>
            { ["a"] = "fiyat:" + id, ["d"] = string.Join("\u001f", fiyat.ToString("R", CultureInfo.InvariantCulture), kaynak.Trim(), tarih) });
            Gunluk("Fiyat güncellendi", $"{id}: {eski:N2} → {fiyat:N2} · {kaynak.Trim()}", Seviye.Uyari);
        }

        public void FiyatSil(string id)
        {
            Yetki(yonetici: true, "Fiyat güncelleme");
            b.Yaz("DELETE FROM ayarlar WHERE anahtar = @a", new Dictionary<string, object?> { ["a"] = "fiyat:" + id });
            Gunluk("Fiyat referansa döndü", id, Seviye.Uyari);
        }

        public List<(string id, double fiyat, string kaynak, string tarih)> FiyatGuncellemeleri() =>
            b.Sorgu("SELECT anahtar, deger FROM ayarlar WHERE anahtar LIKE 'fiyat:%'").Select(r =>
            {
                var p = S(r["deger"]).Split('\u001f');
                return (S(r["anahtar"]).Substring(6), double.Parse(p[0], CultureInfo.InvariantCulture), p.Length > 1 ? p[1] : "", p.Length > 2 ? p[2] : "");
            }).ToList();

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
