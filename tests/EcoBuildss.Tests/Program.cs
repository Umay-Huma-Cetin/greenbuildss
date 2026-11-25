using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EcoBuildss.Core;
using EcoBuildss.Core.Depo;
using EcoBuildss.Core.Ogrenme;
using EcoBuildss.Core.Sentetik;
using EcoBuildss.Core.Uygulamalar;
using EcoBuildss.Core.Veri;

// EcoBuildss birim ve bütünleşik testleri. Dış test kütüphanesi kullanılmaz: `dotnet run --project tests/EcoBuildss.Tests`
// Çıkış kodu 0 = bütün testler geçti. "--ornek-yaz <klasör>" ile örnek veri dosyaları da üretilir.
static class Testler
{
    static int gecen, kalan;
    static readonly List<string> hatalar = new();

    static void Dogrula(bool kosul, string ad, string ayrinti = "")
    {
        if (kosul) gecen++; else { kalan++; hatalar.Add($"✗ {ad} {ayrinti}"); }
    }
    static void Yakin(double a, double b, double tol, string ad) => Dogrula(Math.Abs(a - b) <= tol, ad, $"(beklenen {b}, bulunan {a})");

    static int Main(string[] args)
    {
        string kok = Kok();
        var v = VeriDeposu.Yukle(Path.Combine(kok, "veri", "referans"));
        var sw = Stopwatch.StartNew();

        Bolum("Matematik ve metrikler");
        {
            var x = Mat.Coz(new double[,] { { 4, 1 }, { 1, 3 } }, new double[] { 1, 2 });
            Yakin(x[0], 1.0 / 11, 1e-12, "Doğrusal sistem çözümü x0"); Yakin(x[1], 7.0 / 11, 1e-12, "Doğrusal sistem çözümü x1");
            // AUC: elle hesaplanabilen örnek (scikit-learn roc_auc_score ile aynı: 0.75)
            Yakin(Metrik.RocAuc(new[] { 0, 0, 1, 1 }, new[] { 0.1, 0.4, 0.35, 0.8 }), 0.75, 1e-12, "ROC-AUC");
            var m = SiniflandirmaMetrikleri.Hesapla(new[] { 1, 1, 1, 0, 0, 0, 1, 0 }, new[] { .9, .8, .3, .2, .6, .1, .7, .4 });
            Dogrula(m.DP == 3 && m.YN == 1 && m.YP == 1 && m.DN == 3, "Karmaşıklık matrisi");
            Yakin(m.F1, 0.75, 1e-12, "F1"); Yakin(m.MakroF1, 0.75, 1e-12, "Makro F1");
            var (cv, nmbe) = Metrik.Ashrae14(new[] { 10.0, 20, 30 }, new[] { 12.0, 18, 30 }, 0);
            Yakin(cv, Math.Sqrt(8.0 / 3) / 20, 1e-12, "CV(RMSE)"); Yakin(nmbe, 0, 1e-12, "NMBE");
        }

        Bolum("Regresyon");
        {
            // y = 2 + 3x tam doğrusu: katsayılar birebir, R² = 1
            var xs = Enumerable.Range(1, 10).Select(i => new[] { (double)i }).ToArray();
            var r = DogrusalRegresyon.Ekk(xs, xs.Select(a => 2 + 3 * a[0]).ToArray(), new[] { "x" });
            Yakin(r.Katsayi[0], 2, 1e-9, "Sabit"); Yakin(r.Katsayi[1], 3, 1e-9, "Eğim"); Yakin(r.R2, 1, 1e-12, "R²");
            // Gerçek levha fiyatları: EPS ve taş yünü (levha_fiyatlari.csv) — eğim pozitif ve anlamlı olmalı
            foreach (var aile in v.Levhalar.Select(l => l.Aile).Distinct())
            {
                var l = v.Levhalar.Where(a => a.Aile == aile).ToList();
                var rr = DogrusalRegresyon.Ekk(l.Select(a => new[] { a.KalinlikCm }).ToArray(), l.Select(a => a.FiyatM2).ToArray(), new[] { "kalınlık" });
                Dogrula(rr.GA95Alt[1] > 0 && rr.R2 > 0.95, $"Levha fiyat regresyonu ({aile})", $"R² {rr.R2:0.000}");
            }
        }

        Bolum("Enerji motoru (gerçek referans veri)");
        {
            var g = new Girdi { Bina = Bina.Dikdortgen(10.5, 10.5, 2, 3.5), Il = v.VarsayilanIl, Bolge = 4 };
            var s = KararMotoru.Hesapla(v, g);
            Dogrula(s.ToplamKombinasyon == 60, "60 birleşim (10 mantolama × 2 cam × 3 duvar)", s.ToplamKombinasyon.ToString());
            Dogrula(s.Siralama.Count > 0 && s.Siralama.All(o => o.Skor >= 0 && o.Skor <= 1), "TOPSIS puanı 0–1");
            Yakin(s.CR, 0, 1e-9, "Varsayılan AHP tutarlı");
            // Elle: bir birleşimin ısıtma enerjisi = 0,024 · DG · H
            var o = s.Siralama[0];
            Yakin(o.YillikIsitmaKWh, 0.024 * s.DG * o.H, 1e-6, "Derece-gün formülü");
            // Kalibrasyon çarpanı enerjiyi doğrusal ölçekler
            var g2 = g.Kopya(); g2.Kalibrasyon = 0.8;
            var s2 = KararMotoru.Hesapla(v, g2);
            Yakin(s2.Tum.First(t => t.Kod == o.Kod).YillikIsitmaKWh, 0.8 * o.YillikIsitmaKWh, 1e-6, "Kalibrasyon çarpanı");
            // 28,5 m üstü binada yalnız A2 ve üstü (taş yünü) cephe kalır
            var y = KararMotoru.Hesapla(v, new Girdi { Bina = Bina.Dikdortgen(15, 25, 11, 3), Il = v.VarsayilanIl, Bolge = 4 });
            Dogrula(y.Tum.All(t => t.Yalitim.Ad.StartsWith("Taş yünü")), "Yangın yönetmeliği: 33 m binada EPS elenir");
            bool hata = false; try { KararMotoru.Hesapla(v, new Girdi { Bina = g.Bina, Il = v.VarsayilanIl, CamOrani = 0.95 }); } catch (ArgumentException) { hata = true; }
            Dogrula(hata, "Geçersiz pencere oranı reddedilir");
        }

        Bolum("İçe aktarma ve veri denetimi");
        {
            var sema = Semalar.Teklif(v);
            string kotu = "teklif_id;tarih;il;musteri_tipi;proje_turu;kullanim_alani_m2;paket;oncelik;teklif_TL_m2;geri_odeme_yil;indirim_orani;yanit_suresi_gun;rakip_teklif;sonuc\n"
                        + "A1;14.03.2025;malatya;Bireysel;Yenileme;1.250,5;Önerilen;Dengeli;2.350;7,9;0,05;3;Evet;Kabul\n"   // Türkçe biçim: geçerli
                        + "A2;2025-13-01;Malatya;Bireysel;Yenileme;250;Önerilen;Dengeli;2350;;0.05;3;Evet;Kabul\n"         // geçersiz tarih
                        + "A3;2025-03-01;Atlantis;Bireysel;Yenileme;250;Önerilen;Dengeli;2350;;0.05;3;Evet;Belki\n"       // geçersiz il ve sonuç
                        + "A1;2025-03-01;Malatya;Bireysel;Yenileme;250;Önerilen;Dengeli;2350;;0.05;3;Evet;Red\n";         // tekrar eden anahtar
            var s = IceAktarici.Metinden(kotu, sema);
            Dogrula(s.Kayitlar.Count == 1 && s.Reddedilen == 3, "Hatalı satırlar reddedilir", s.Ozet);
            Yakin(s.Kayitlar[0].Sayi("kullanim_alani_m2"), 1250.5, 1e-9, "Türkçe sayı biçimi (1.250,5)");
            Dogrula(s.Kayitlar[0].Metin("il") == "Malatya", "Seçenek büyük/küçük harf bağımsız");
            Dogrula(s.Bulgular.Any(b => b.Sutun == "il") && s.Bulgular.Any(b => b.Sutun == "sonuc") && s.Bulgular.Any(b => b.Mesaj.StartsWith("Tekrarlanan")), "Hata nedenleri raporlanır");
            var eksik = IceAktarici.Metinden("teklif_id,tarih\nA,2025-01-01\n", sema);
            Dogrula(eksik.DosyaGecersiz, "Zorunlu sütun eksikse dosya reddedilir");
        }

        // ---------------- Örnek (sentetik) veriyle uçtan uca ----------------
        string tCsv = SentetikUretici.Tuketim(v), kCsv = SentetikUretici.Teklifler(v);
        var tuk = IceAktarici.Metinden(tCsv, Semalar.Tuketim(v));
        var tek = IceAktarici.Metinden(kCsv, Semalar.Teklif(v));
        int ornekYaz = Array.IndexOf(args, "--ornek-yaz");
        if (ornekYaz >= 0)
        {
            string k = args[ornekYaz + 1]; Directory.CreateDirectory(k);
            File.WriteAllText(Path.Combine(k, "tuketim_kayitlari.csv"), tCsv, new System.Text.UTF8Encoding(true));
            File.WriteAllText(Path.Combine(k, "teklif_gecmisi.csv"), kCsv, new System.Text.UTF8Encoding(true));
        }

        int sozluk = Array.IndexOf(args, "--sozluk");
        if (sozluk >= 0)
        {
            var sb = new System.Text.StringBuilder("# Veri sözlüğü\n\nBu dosya `dotnet run --project tests/EcoBuildss.Tests -- --sozluk docs/VERI_SOZLUGU.md` ile şemalardan üretilir; elle düzenlemeyin.\n");
            foreach (var sm in new[] { Semalar.Tuketim(v), Semalar.Teklif(v) })
            {
                sb.Append($"\n## {sm.Ad} (`{sm.DosyaAdi}`)\n\n{sm.Aciklama}\n\n| Sütun | Açıklama | Biçim | Zorunlu | Örnek |\n|---|---|---|---|---|\n");
                foreach (var a in sm.Alanlar)
                {
                    string bicim = a.Tur switch
                    {
                        AlanTuru.Sayi or AlanTuru.Tamsayi => $"{(a.Tur == AlanTuru.Tamsayi ? "tam sayı" : "sayı")}, {a.Min.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} – {a.Max.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} {a.Birim}".Trim(),
                        AlanTuru.Tarih => "tarih (yyyy-aa-gg ya da gg.aa.yyyy)", AlanTuru.Secenek => a.Secenekler.Length > 10 ? "81 il adından biri" : string.Join(" / ", a.Secenekler), _ => "metin",
                    };
                    sb.Append($"| `{a.Ad}` | {a.Aciklama} | {bicim} | {(a.Zorunlu ? "evet" : "hayır")} | {a.Ornek} |\n");
                }
            }
            File.WriteAllText(args[sozluk + 1], sb.ToString());
        }

        Bolum("Sentetik veri kendi şemasından geçer");
        {
            Dogrula(tuk.Reddedilen == 0 && tuk.Kayitlar.Count == 300, "Tüketim örneği şemaya uygun", tuk.Ozet + " " + string.Join(" | ", tuk.Bulgular.Take(3)));
            Dogrula(tek.Reddedilen == 0 && tek.Kayitlar.Count == 800, "Teklif örneği şemaya uygun", tek.Ozet + " " + string.Join(" | ", tek.Bulgular.Take(3)));
            double kabul = tek.Kayitlar.Count(k => k.Metin("sonuc") == "Kabul") / (double)tek.Kayitlar.Count;
            Console.WriteLine($"  Kabul oranı: %{100 * kabul:0.0}");
            Dogrula(kabul > 0.3 && kabul < 0.7, "Sınıflar dengeli (%30–70 kabul)");
        }

        Bolum("Kalibrasyon regresyonu");
        {
            var k = Kalibrasyon.Calistir(v, tuk.Kayitlar);
            Console.WriteLine($"  β₁ = {k.IsitmaKatsayisi:0.000} [%95 GA {k.IsitmaKatsayisiAlt:0.000}–{k.IsitmaKatsayisiUst:0.000}], sıcak su {k.SicakSuKWhM2:0.0} kWh/m², R² {k.Regresyon.R2:0.000}");
            Console.WriteLine($"  Çapraz doğrulama CV(RMSE): ham %{100 * k.HamCvRmse:0.0} → kalibre %{100 * k.CvRmse:0.0}; NMBE %{100 * k.Nmbe:0.00}");
            // Kayıtların %40'ında o yılın derece-günü yok (uzun dönem değeri kullanılır): açıklayıcı değişkendeki bu hata β₁'i biraz küçültür (zayıflama yanlılığı)
            Dogrula(Math.Abs(k.IsitmaKatsayisi - SentetikUretici.GercekIsitmaKatsayisi) < 0.06, "β₁ gerçek katsayıya (0,85) yakın", k.IsitmaKatsayisi.ToString("0.000"));
            Dogrula(Math.Abs(k.SicakSuKWhM2 - SentetikUretici.SicakSuKWhM2) < 5, "Sıcak su baz yükü bulunur (25 kWh/m²)", k.SicakSuKWhM2.ToString("0.0"));
            Dogrula(k.Iyilestirdi && k.Kullanilabilir, "Kalibrasyon görülmemiş veride hatayı düşürür");
            Dogrula(Math.Abs(k.Nmbe) < 0.05, "NMBE ±%5 içinde");
        }

        Bolum("Teklif kabul tahmini (sınıflandırma)");
        {
            var m = KabulTahmini.Egit(v, tek.Kayitlar);
            foreach (var a in m.Adaylar) Console.WriteLine($"  {a.Ad,-22} CV F1 {a.F1Ort:0.000} ± {a.F1Std:0.000} · AUC {a.AucOrt:0.000} · doğruluk {a.DogrulukOrt:0.000}");
            Console.WriteLine($"  Seçilen: {m.SecilenModel}, eşik {m.Esik:0.00} → TEST (n={m.TestN}): F1 {m.Test.F1:0.000}, makro F1 {m.Test.MakroF1:0.000}, AUC {m.Test.Auc:0.000}, doğruluk {m.Test.Dogruluk:0.000}");
            Console.WriteLine($"  Karmaşıklık: DP {m.Test.DP} YP {m.Test.YP} YN {m.Test.YN} DN {m.Test.DN} · \"hep kabul\" tahmini F1 {m.TabanF1:0.000}");
            var onem = OzellikVektoru.Adlar.Zip(m.Onem, (a, b) => (a, b)).OrderByDescending(t => t.b).Take(5);
            Console.WriteLine("  En etkili: " + string.Join(", ", onem.Select(t => $"{t.a} {t.b:0.00}")));
            foreach (var kural in m.Kurallar.Take(4)) Console.WriteLine("  " + kural);
            Dogrula(m.YeterliVeri, "Yeterli veri");
            Dogrula(m.Test.F1 >= KabulModeliSonucu.F1Esigi, "Test F1 ≥ 0,87", m.Test.F1.ToString("0.000"));
            Dogrula(m.Guvenilir, "Kalite kapısı geçildi");
            // Az veriyle model güvenilir sayılmaz
            var az = KabulTahmini.Egit(v, tek.Kayitlar.Take(60).ToList());
            Dogrula(!az.Guvenilir, "60 kayıtla model güvenilir sayılmaz");
            // Etiketler karıştırılırsa (sinyal yok) kalite kapısı modeli reddetmeli
            var r = new Random(3); var karisik = tek.Kayitlar.Select(k => k.Metin("sonuc")).OrderBy(_ => r.Next()).ToList();
            var bozuk = tek.Kayitlar.Select((k, i) => { var c = new Kayit { SatirNo = k.SatirNo }; foreach (var kv in k.Deger) c.Deger[kv.Key] = kv.Value; c.Deger["sonuc"] = karisik[i]; return c; }).ToList();
            var rast = KabulTahmini.Egit(v, bozuk);
            Console.WriteLine($"  Karıştırılmış etiketle test F1 {rast.Test.F1:0.000}, AUC {rast.Test.Auc:0.000}");
            Dogrula(!rast.Guvenilir && rast.Test.Auc < 0.65, "Sinyalsiz veride kalite kapısı modeli reddeder");
        }

        Bolum("Kümeleme");
        {
            var il = Segmentasyon.Iller(v);
            Console.WriteLine($"  İller: {il.Yorum}");
            foreach (var sg in il.Segmentler) Console.WriteLine($"   Küme {sg.No}: {sg.Ozet}");
            Dogrula(il.Segmentler.Sum(s => s.Uyeler.Count) == 81, "81 il kümelendi");
            Dogrula(il.Kume.Siluet > 0.4, "İl kümelerinin siluet katsayısı > 0,4");
            var mu = Segmentasyon.Musteriler(tek.Kayitlar);
            Console.WriteLine($"  Müşteriler: {mu.Yorum}");
            foreach (var sg in mu.Segmentler) Console.WriteLine($"   Segment {sg.No}: {sg.Ozet}");
            Dogrula(mu.Segmentler.Sum(s => s.Uyeler.Count) == 800, "800 teklif segmentlendi");
            // Bilinen 3 ayrık grup: k = 3 bulunmalı
            var r = new Random(5);
            var uc = Enumerable.Range(0, 90).Select(i => new[] { (i % 3) * 10 + Rastgele.Normal(r, 0, 0.5), (i % 3) * -7 + Rastgele.Normal(r, 0, 0.5) }).ToArray();
            Dogrula(Segmentasyon.Calistir("deneme", new[] { "a", "b" }, uc).K == 3, "Ayrık 3 grupta k = 3 seçilir");
        }

        Bolum("Veritabanı (SQLite)");
        {
            string dosya = Path.Combine(Path.GetTempPath(), $"ecobuildss_test_{Guid.NewGuid():N}.db");
            using (var db = new Veritabani(dosya))
            {
                var sema = Semalar.Teklif(v);
                db.VeriKaydet(sema, tek.Kayitlar, ornek: true);
                var (geri, kaynak) = db.VeriOku(sema);
                Dogrula(geri.Kayitlar.Count == 800 && geri.Reddedilen == 0 && kaynak == "Örnek", "Veri kaydedilip aynen okunur", geri.Ozet);
                Yakin(geri.Kayitlar[0].Sayi("teklif_TL_m2"), tek.Kayitlar.OrderBy(k => k.Metin("teklif_id"), StringComparer.Ordinal).First().Sayi("teklif_TL_m2"), 1e-9, "Sayı hassasiyeti korunur");
                db.VeriKaydet(sema, tek.Kayitlar.Take(10), ornek: false);
                Dogrula(db.VeriOku(sema).sonuc.Kayitlar.Count == 10, "Gerçek veri yüklenince örnek veri silinir");
                var t = new KayitliTeklif { TeklifNo = db.YeniTeklifNo(), Il = "Malatya", Musteri = "O'Brien; DROP TABLE teklifler", KullanimAlani = 220, TeklifTL = 500000, TeklifTLm2 = 2272.7, GeriOdemeYil = 7.5 };
                db.TeklifKaydet(t);
                db.SonucIsaretle(t.Id, "Kabul");
                var l = db.Teklifler();
                Dogrula(l.Count == 1 && l[0].Sonuc == "Kabul" && l[0].Musteri == t.Musteri, "Teklif kaydı ve sonuç (parametreli SQL)");
                var csv = IceAktarici.Metinden(db.SonuclananTekliflerCsv(sema), sema);
                Dogrula(csv.Kayitlar.Count == 1 && csv.Reddedilen == 0, "Sonuçlanan teklifler eğitim verisine dönüşür", csv.Ozet);
            }
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(dosya) + "*")) File.Delete(f);
        }

        Console.WriteLine($"\n{gecen} test geçti, {kalan} test kaldı · {sw.Elapsed.TotalSeconds:0.0} sn");
        foreach (var h in hatalar) Console.WriteLine(h);
        return kalan == 0 ? 0 : 1;
    }

    static void Bolum(string ad) => Console.WriteLine($"\n== {ad}");

    static string Kok()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "veri", "referans"))) d = d.Parent;
        return d?.FullName ?? throw new DirectoryNotFoundException("veri/referans klasörü bulunamadı.");
    }
}
