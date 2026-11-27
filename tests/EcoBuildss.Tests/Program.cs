using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EcoBuildss.Core;
using EcoBuildss.Core.Depo;
using EcoBuildss.Core.Ogrenme;
using EcoBuildss.Tests;
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
        if (args.Contains("--butunluk-yaz")) { ReferansButunluk.Yaz(Path.Combine(kok, "veri", "referans")); Console.WriteLine("BUTUNLUK.sha256 yazıldı."); }
        var v = VeriDeposu.Yukle(Path.Combine(kok, "veri", "referans"));
        var sw = Stopwatch.StartNew();

        Bolum("Referans veri bütünlüğü");
        {
            var sorun = ReferansButunluk.Denetle(Path.Combine(kok, "veri", "referans"));
            Dogrula(sorun.Count == 0, "Referans dosyaları imzalı özetlerle aynı", string.Join("; ", sorun));
            string gecici = Path.Combine(Path.GetTempPath(), "eb_ref_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(gecici);
            foreach (var f in Directory.GetFiles(Path.Combine(kok, "veri", "referans"))) File.Copy(f, Path.Combine(gecici, Path.GetFileName(f)));
            File.AppendAllText(Path.Combine(gecici, "malzemeler.csv"), "\n");
            File.WriteAllText(Path.Combine(gecici, "iller.csv"), File.ReadAllText(Path.Combine(gecici, "iller.csv")).Replace("\n", "\r\n"));
            var s2 = ReferansButunluk.Denetle(gecici);
            Dogrula(s2.Count == 1 && s2[0].StartsWith("malzemeler.csv"), "Değiştirilen dosya yakalanır, satır sonu farkı yanlış alarm vermez", string.Join("; ", s2));
            Directory.Delete(gecici, true);
            var uygunsuz = v.Iller.Where(il => KararMotoru.Hesapla(v, new Girdi { Bina = Bina.Dikdortgen(12, 18, 4, 2.9), Il = il, Bolge = il.Ts825Bolge ?? 4, SadeceTs825 = true }).Siralama.Count == 0).Select(il => il.Ad).ToList();
            Dogrula(uygunsuz.Count == 0, "81 ilin hepsinde TS 825'e uyan en az bir paket var", string.Join(", ", uygunsuz));
            {
                var bugun = new DateTime(2026, 11, 28);
                var tl = new List<KayitliTeklif>
                {
                    new() { Hazirlayan = "a", Sonuc = "Kabul", TeklifTL = 100, Tarih = bugun.AddDays(-40) },
                    new() { Hazirlayan = "a", Sonuc = "Red", TeklifTL = 50, Tarih = bugun.AddDays(-30) },
                    new() { Hazirlayan = "a", Sonuc = "Bekliyor", TeklifTL = 70, Tarih = bugun.AddDays(-14) },
                    new() { Hazirlayan = "b", Sonuc = "Bekliyor", TeklifTL = 20, Tarih = bugun.AddDays(-13) },
                };
                var ss = TeklifTakibi.Satiscilar(tl, bugun);
                Dogrula(ss[0].Hazirlayan == "a" && ss[0].KazanmaOrani == 0.5 && ss[0].KazanilanTL == 100 && ss[0].TakipGereken == 1 && ss[1].TakipGereken == 0 && ss[1].KazanmaOrani == null,
                        "Teklif takibi: 14 günü geçen bekleyen teklif işaretlenir; satışçı bazında kazanma oranı");
            }
            {
                // İndirim: olasılık artar, marj azalır → beklenen kâr içeride en yüksek
                var egri = EcoBuildss.Core.Uygulamalar.IndirimAnalizi.Egri(i => (1 / (1 + Math.Exp(-(i * 30 - 1.5))), 118_000 * (1 - i)), 100_000);
                var en = EcoBuildss.Core.Uygulamalar.IndirimAnalizi.EnIyi(egri)!;
                double kaba = egri.Max(n => n.Kar > 0 ? n.BeklenenKar : double.MinValue);
                Dogrula(en.Indirim > 0 && en.Indirim < 0.153 && Math.Abs(en.BeklenenKar - kaba) < 0.01 && egri.Last().Kar < 0,
                        "İndirim analizi: beklenen kârı en yüksek indirim bulunur, zarar eden indirim seçilmez", $"%{en.Indirim * 100:0}");
                Dogrula(EcoBuildss.Core.Uygulamalar.IndirimAnalizi.EnIyi(EcoBuildss.Core.Uygulamalar.IndirimAnalizi.Egri(i => (0.5, 90_000), 100_000)) == null, "Hiçbir indirimde kâr yoksa öneri yapılmaz");
            }
            {
                var pl = Bina.Plan(80, 300, 5, 3);
                bool kisa = false; try { Bina.Plan(40, 300, 5, 3); } catch (ArgumentException) { kisa = true; }
                Dogrula(pl.DisDuvarAlani == 1200 && pl.KullanimAlani == 1500 && pl.Hacim == 4500 && kisa, "Dikdörtgen olmayan plan: çevre ve taban alanından bina; imkânsız çevre reddedilir");
            }
            {
                var r4 = KararMotoru.Hesapla(v, new Girdi { Bina = Bina.Dikdortgen(12, 18, 4, 2.9), Il = v.VarsayilanIl, Bolge = 4 });
                var r2 = KararMotoru.Hesapla(v, new Girdi { Bina = Bina.Dikdortgen(12, 18, 2, 2.9), Il = v.VarsayilanIl, Bolge = 4 });
                double b4 = KararMotoru.BariyerAlani(v, r4);
                var rb = KararMotoru.Hesapla(v, new Girdi { Bina = Bina.Dikdortgen(12, 18, 4, 2.9), Il = v.VarsayilanIl, Bolge = 4, PencereEn = 2.0, PencereBoy = 1.5 });
                double birPencere = 2 * 0.15 * 1.5 + 0.30 * (2.0 + 0.30);   // bir pencerede bariyer alanı
                Dogrula(Math.Abs(KararMotoru.BariyerAlani(v, rb) - rb.CamAlani / 3.0 * birPencere) < 1e-6, "Girilen pencere ölçüsüyle bariyer alanı hesaplanır");
                Dogrula(KararMotoru.BariyerAlani(v, r2) == 0 && Math.Abs(b4 - r4.CamAlani * 0.5) < 1e-6,
                        "Pencere yangın bariyeri: 6,5 m altı yok; üstünde 120×150 pencerede pencere alanının yarısı kadar", $"{b4:0.0} m², cam {r4.CamAlani:0.0} m²");
            }
            {
                var vf = VeriDeposu.Yukle(Path.Combine(Kok(), "veri", "referans"));
                double ilk = vf.Malzemeler.First(m => m.Id == "Y2").FiyatM2, gazIlk = vf.P("E1");
                vf.FiyatUygula("Y2", ilk * 1.5, "Tedarikçi teklifi", "01.12.2026"); vf.FiyatUygula("E1", gazIlk + 1, "BOTAŞ", "");
                bool degisti = vf.Malzemeler.First(m => m.Id == "Y2").FiyatM2 == ilk * 1.5 && vf.P("E1") == gazIlk + 1 && vf.FiyatKalemleri().Count(k => k.Guncellendi) == 2;
                vf.FiyatReferansaDon("Y2"); vf.FiyatReferansaDon("E1");
                Dogrula(degisti && vf.Malzemeler.First(m => m.Id == "Y2").FiyatM2 == ilk && vf.P("E1") == gazIlk && vf.FiyatKalemleri().All(k => !k.Guncellendi),
                        "Fiyat güncellenir ve referansa geri döner");
                bool red = false; try { vf.FiyatUygula("KDV", 0.5, "x", ""); } catch (ArgumentException) { red = true; }
                Dogrula(red, "Fiyat olmayan parametre güncellenemez");
            }
        }

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

        Bolum("Apartman mantolaması");
        {
            var g = new Girdi { Bina = Bina.Dikdortgen(16, 22, 6, 2.9), Il = v.VarsayilanIl, Bolge = 4, Mevcut = true, PencereDegisir = false, CatiYalitimi = true, ZeminYalitimi = false };
            var r = KararMotoru.Hesapla(v, g);
            Dogrula(r.Tum.Select(x => x.Cam.Id).Distinct().Count() == 1, "Pencereler değişmezse cam seçeneği çoğaltılmaz");
            var s = r.Tum.First(x => x.Yalitim.Id == "Y3");
            double beklenen = r.OpakDuvar * s.Yalitim.FiyatM2 + s.YanginEkMaliyet + r.Cati.FiyatM2 * r.CatiAlani;
            Yakin(s.IlkYatirim, beklenen, 1e-6, "Maliyet = mantolama + yangın şeridi + çatı (pencere ve zemin yok)");
            Yakin(s.PencereU, v.P("M4"), 1e-12, "Mevcut pencere U değeri kullanılır");
            var a = Apartman.Hesapla(v, r, s, 24, 0.18, 0, 30000, 900, 12, 0.03);
            Yakin(a.FaturaOnce, 30000 - 12 * 900, 1e-9, "Isıtma faturası = yıllık − 12 × yaz faturası");
            Yakin(a.YillikTasarruf, a.FaturaOnce * (1 - s.H / r.HBaz), 1e-9, "Tasarruf = ısıtma faturası × azalma oranı");
            Yakin(a.AylikTaksit, Apartman.Taksit(a.DaireBasiTL, 12, 0.03), 1e-9, "Taksit (anüite)");
            Yakin(Apartman.Taksit(1200, 12, 0), 100, 1e-12, "Vade farksız taksit");
            Yakin(Apartman.Taksit(1000, 12, 0.01), 88.8487886783417, 1e-6, "Anüite formülü (1.000 TL, 12 ay, %1)");
            Dogrula(a.GeriOdemeYil > 0, "Daire başına geri ödeme hesaplanır");
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
            string? kurtarma = null;
            using (var db = new Veritabani(dosya))
            {
                var sema = Semalar.Teklif(v);
                bool red = false; try { db.VeriKaydet(sema, tek.Kayitlar, ornek: true); } catch (UnauthorizedAccessException) { red = true; }
                Dogrula(red, "Oturum açılmadan veri yazılamaz");
                db.IlkYonetici("umay", "Umay Hüma Çetin", "Gizli2026!");
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
                Dogrula(l[0].Hazirlayan == "umay", "Teklifi kimin hazırladığı kaydedilir");

                // ---- Güvenlik ----
                Bolum("Güvenlik ve işlem günlüğü");
                string ozet = Parola.Ozetle("Deneme123", 1000);
                Dogrula(Parola.Dogrula("Deneme123", ozet) && !Parola.Dogrula("deneme123", ozet), "Parola doğrulama (büyük/küçük harf duyarlı)");
                Dogrula(!ozet.Contains("Deneme123") && ozet.StartsWith("pbkdf2-sha256$1000$"), "Parola açık metin saklanmaz (PBKDF2-SHA256)");
                Dogrula(Parola.Ozetle("Deneme123", 1000) != ozet, "Her parola özeti farklı tuzla üretilir");
                Dogrula(Parola.Kural("kisa1", "x") != null && Parola.Kural("sadeceharf", "x") != null && Parola.Kural("umay2026x", "umay") != null && Parola.Kural("Guvenli99", "umay") == null, "Parola kuralı");
                var satis = db.KullaniciEkle("mehmet", "Mehmet Yılmaz", Rol.Satis, "Satis2026!");
                db.OturumKapat();
                Dogrula(db.GirisYap("mehmet", "yanlis-parola1") == GirisSonucu.HataliBilgi, "Hatalı parola reddedilir");
                Dogrula(db.GirisYap("olmayan", "Herhangi123") == GirisSonucu.HataliBilgi, "Bilinmeyen kullanıcı aynı yanıtı alır");
                Dogrula(db.GirisYap("MEHMET", "Satis2026!") == GirisSonucu.Basarili && db.Aktif!.Rol == Rol.Satis, "Kullanıcı adı büyük/küçük harften bağımsız, rol yüklenir");
                Dogrula(db.Aktif!.ParolaDegismeli, "Yöneticinin verdiği geçici parola ilk girişte değiştirilmeli");
                db.ParolaDegistir("Satis2026!", "Kendi2026x"); db.OturumKapat();
                Dogrula(db.GirisYap("mehmet", "Kendi2026x") == GirisSonucu.Basarili && !db.Aktif!.ParolaDegismeli, "Parola değişince zorunluluk kalkar");
                bool yasak = false; try { db.TeklifSil(t.Id); } catch (UnauthorizedAccessException) { yasak = true; }
                Dogrula(yasak, "Satış rolü teklif silemez");
                Dogrula(db.Teklifler().Count == 0, "Satış rolü başkasının teklifini görmez");
                yasak = false; try { db.SonucIsaretle(t.Id, "Kabul"); } catch (UnauthorizedAccessException) { yasak = true; }
                bool acamaz = false; try { db.TeklifAc(t.Id); } catch (UnauthorizedAccessException) { acamaz = true; }
                Dogrula(yasak && acamaz, "Satış rolü başkasının teklifini açamaz ve işaretleyemez");
                yasak = false; try { db.KullaniciEkle("x123", "X", Rol.Yonetici, "Parola123"); } catch (UnauthorizedAccessException) { yasak = true; }
                Dogrula(yasak, "Satış rolü kullanıcı ekleyemez");
                yasak = false; try { db.Kullanicilar(); } catch (UnauthorizedAccessException) { yasak = true; }
                Dogrula(yasak, "Satış rolü kullanıcı listesini göremez");
                Dogrula(db.GirisYap("mehmet", "yanlis-parola9") == GirisSonucu.HataliBilgi && db.Aktif == null, "Başarısız yeni giriş önceki oturumu açık bırakmaz");
                db.GirisYap("mehmet", "Kendi2026x");
                db.OturumKapat();
                for (int i = 0; i < Veritabani.AzamiHataliDeneme - 1; i++) db.GirisYap("mehmet", "yanlis" + i + "abc");
                Dogrula(db.GirisYap("mehmet", "yanlis-son-1") == GirisSonucu.Kilitli, "5 hatalı denemede hesap kilitlenir");
                Dogrula(db.GirisYap("mehmet", "Kendi2026x") == GirisSonucu.Kilitli, "Kilitliyken doğru parola da kabul edilmez");
                Dogrula(db.GirisYap("umay", "Gizli2026!") == GirisSonucu.Basarili, "Yönetici girişi");
                yasak = false; try { db.KullaniciGuncelle(db.Aktif!.Id, Rol.Satis, true); } catch (InvalidOperationException) { yasak = true; }
                Dogrula(yasak, "Son yönetici yetkisi kaldırılamaz");
                db.ParolaSifirla(satis.Id, "YeniParola7");
                Dogrula(db.GunlukOku().Any(g => g.Islem == "Başarısız giriş" && g.Seviye == "Güvenlik") && db.GunlukOku().Any(g => g.Islem == "Yetkisiz istek"), "Başarısız giriş ve yetkisiz istek günlüğe yazılır");
                Dogrula(db.GunlukOku().Any(g => g.Islem == "Teklif kaydedildi" && g.Kullanici == "umay"), "Günlükte işlemi yapan kullanıcı var");
                var (n, sorun) = db.GunlukDogrula();
                Dogrula(sorun == null && n > 10, "Günlük zinciri sağlam", sorun ?? "");
                // Yedek
                string yedek = db.Yedekle();
                Dogrula(File.Exists(yedek), "Elle yedek alınır");
                db.TeklifKaydet(new KayitliTeklif { TeklifNo = db.YeniTeklifNo(), Il = "Malatya", KullanimAlani = 100, TeklifTL = 1, TeklifTLm2 = 1 });
                Dogrula(db.Teklifler().Count == 2, "Yedekten sonra yeni teklif");
                db.FiyatKaydet("Y2", 900, "Tedarikçi teklifi", 865);
                Dogrula(db.FiyatGuncellemeleri().Single() is var fg && fg.id == "Y2" && fg.fiyat == 900 && fg.kaynak == "Tedarikçi teklifi", "Fiyat güncellemesi veritabanında saklanır");
                db.FiyatSil("Y2");
                var t2 = db.Teklifler().First(x => x.Il == "Malatya" && x.TeklifTL == 1);
                Dogrula(t2.KullanimAlani == 100 && t2.Hazirlayan == "umay", "Şifreli teklif ayrıntıları eksiksiz geri okunur");
                string sonNo = db.YeniTeklifNo(); db.TeklifSil(db.Teklifler().OrderBy(x => x.TeklifNo).First().Id);
                Dogrula(db.YeniTeklifNo() == sonNo, "Teklif silinse de yeni numara çakışmaz");
                db.GeriYukle(yedek);
                Dogrula(db.Aktif == null, "Geri yüklemeden sonra yeniden oturum açılmalı");
                db.GirisYap("umay", "Gizli2026!");
                Dogrula(db.Teklifler().Count == 1 && db.Teklifler()[0].Musteri == t.Musteri, "Yedekten geri yükleme (şifreli alanlar çözülür)");
                Dogrula(db.Yedekler().Count >= 2, "Geri yüklemeden önce mevcut durum yedeklenir");
                Dogrula(db.OtomatikYedek() != null && db.OtomatikYedek() == null, "Otomatik yedek günde bir kez alınır");

                // ---- Şifreleme ----
                Bolum("Şifreleme");
                var k64 = Sifreleme.YeniAnahtar();
                string c = Sifreleme.Sifrele(k64, "Gül Apartmanı 1.073.387 TL");
                Dogrula(Sifreleme.Coz(k64, c) == "Gül Apartmanı 1.073.387 TL" && c != Sifreleme.Sifrele(k64, "Gül Apartmanı 1.073.387 TL"), "AES-256 + HMAC: çözülür, her şifrelemede farklı çıktı");
                var bozuk = Convert.FromBase64String(c.Substring(3)); bozuk[20] ^= 1;
                bool yakalandi = false; try { Sifreleme.Coz(k64, "e1:" + Convert.ToBase64String(bozuk)); } catch (System.Security.Cryptography.CryptographicException) { yakalandi = true; }
                Dogrula(yakalandi, "Şifreli değerde tek bit değişikliği yakalanır");
                yakalandi = false; try { Sifreleme.Coz(Sifreleme.YeniAnahtar(), c); } catch (System.Security.Cryptography.CryptographicException) { yakalandi = true; }
                Dogrula(yakalandi, "Yanlış anahtarla çözülemez");
                kurtarma = db.YeniKurtarmaAnahtari;
            }
            // Diskteki dosyada müşteri adı, tutar ve içe aktarılan veri düz metin olarak bulunmamalı
            using (var ham = new SqliteBaglanti(dosya))
            {
                var tek1 = ham.Sorgu("SELECT musteri, teklif_tl, gizli FROM teklifler")[0];
                Dogrula((string)tek1["musteri"]! == "" && Convert.ToDouble(tek1["teklif_tl"]) == 0 && ((string)tek1["gizli"]!).StartsWith("e1:"), "Teklifte müşteri ve tutar diskte şifreli");
                Dogrula(ham.Sorgu("SELECT il, paket, hazirlayan, kullanim_alani, en FROM teklifler").All(r => (string)r["il"]! == "" && (string)r["paket"]! == "" && (string)r["hazirlayan"]! == "" && Convert.ToDouble(r["kullanim_alani"]) == 0 && r["en"] == null),
                        "Teklifin il, paket, alan, ölçü ve hazırlayanı da diskte şifreli");
                Dogrula(ham.Sorgu("SELECT satir FROM veri_kayitlari").All(r => ((string)r["satir"]!).StartsWith("e1:")), "İçe aktarılan veri diskte şifreli");
                Dogrula(!ham.Sorgu("SELECT ayrinti FROM islem_gunlugu WHERE islem = 'Teklif kaydedildi'").Any(r => ((string)r["ayrinti"]!).Contains("Malatya")), "Günlük ayrıntısı diskte şifreli");
                Dogrula(ham.Sorgu("SELECT sarili FROM anahtarlar").Count >= 3, "Veri anahtarı her kullanıcı ve kurtarma için ayrı sarılı");
            }
            using (var db = new Veritabani(dosya))
            {
                bool hata = false; try { db.KurtarmaIleSifirla("umay", "AAAAA-BBBBB-CCCCC-DDDDD-EEEEE", "Yeni2026xx"); } catch (ArgumentException) { hata = true; }
                Dogrula(hata, "Yanlış kurtarma anahtarı reddedilir");
                Dogrula(kurtarma != null && kurtarma.Length == 29, "İlk kurulumda kurtarma anahtarı üretilir");
                db.KurtarmaIleSifirla("umay", kurtarma!.ToLowerInvariant().Replace("-", " "), "Yeni2026xx");
                Dogrula(db.GirisYap("umay", "Yeni2026xx") == GirisSonucu.Basarili && db.Teklifler()[0].TeklifTL == 500000, "Kurtarma anahtarıyla parola yenilenir, veri okunur");
                bool red = false; try { db.KurtarmaAnahtariYenile("yanlis-parola1"); } catch (UnauthorizedAccessException) { red = true; }
                Dogrula(red, "Kurtarma anahtarı yenilemek için parola gerekir");
                string yeniK = db.KurtarmaAnahtariYenile("Yeni2026xx");
                bool eskiGecmez = false; try { db.KurtarmaIleSifirla("umay", kurtarma!, "Baska2026xx"); } catch (ArgumentException) { eskiGecmez = true; }
                Dogrula(eskiGecmez && yeniK != kurtarma, "Yeni kurtarma anahtarından sonra eskisi geçersiz");
                db.OturumKapat();
                Dogrula(db.GirisYap("umay", "Gizli2026!") == GirisSonucu.HataliBilgi, "Eski parola artık geçmez");
            }
            // Zincir anahtarsızdır; mühür, zincirin baştan yazılmasını ve son kayıtların silinmesini yakalar
            using (var db = new Veritabani(dosya))
            {
                db.GirisYap("umay", "Yeni2026xx");
                Dogrula(db.GunlukDogrula().sorun == null && db.TeklifAc(db.Teklifler()[0].Id) != null && db.GunlukOku().Any(g => g.Islem == "Teklif açıldı"),
                        "Teklif açma günlüğe yazılır; sağlam günlükte sorun yok");
            }
            string kopya = dosya + ".kopya";
            File.Copy(dosya, kopya, true);
            using (var ham = new SqliteBaglanti(dosya)) ham.Yaz("DELETE FROM islem_gunlugu WHERE id > (SELECT MAX(id) - 3 FROM islem_gunlugu)");
            using (var db = new Veritabani(dosya))
            {
                Dogrula(db.GunlukDogrula().sorun == null, "Sondan silme anahtarsız zincirde görünmez (mühürsüz denetim)");
                db.GirisYap("umay", "Yeni2026xx");
                var (_, s1) = db.GunlukDogrula();
                Dogrula(s1 != null && s1.Contains("silinmiş"), "Sondan silinen günlük kayıtları mühürle yakalanır", s1 ?? "bulunamadı");
            }
            // Bütün zinciri SHA-256 ile baştan yeniden hesaplayan saldırı
            using (var ham = new SqliteBaglanti(kopya))
            {
                ham.Yaz("UPDATE islem_gunlugu SET islem = 'değiştirildi' WHERE id = 2");
                string onceki = "";
                foreach (var r in ham.Sorgu("SELECT id, tarih, kullanici, seviye, islem, ayrinti FROM islem_gunlugu ORDER BY id"))
                {
                    using var h = System.Security.Cryptography.SHA256.Create();
                    var oz = BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\u001f", onceki, ((long)r["id"]!).ToString(), (string)r["tarih"]!, (string)r["kullanici"]!, (string)r["seviye"]!, (string)r["islem"]!, (string)r["ayrinti"]!)))).Replace("-", "").ToLowerInvariant();
                    ham.Yaz("UPDATE islem_gunlugu SET ozet = @o WHERE id = @i", new Dictionary<string, object?> { ["o"] = oz, ["i"] = r["id"] });
                    onceki = oz;
                }
            }
            using (var db = new Veritabani(kopya))
            {
                db.GirisYap("umay", "Yeni2026xx");
                var (_, s2) = db.GunlukDogrula();
                Dogrula(s2 != null && s2.Contains("baştan"), "Baştan yeniden hesaplanan günlük zinciri mühürle yakalanır", s2 ?? "bulunamadı");
            }
            File.Delete(kopya);
            // Günlük sonradan değiştirilirse zincir bozulur
            using (var ham = new SqliteBaglanti(dosya)) ham.Yaz("UPDATE islem_gunlugu SET ayrinti = 'değiştirildi' WHERE id = 3");
            using (var db = new Veritabani(dosya))
            {
                var (_, sorun) = db.GunlukDogrula();
                Dogrula(sorun != null && sorun.Contains("#3"), "Günlükte değiştirilen kayıt tespit edilir", sorun ?? "bulunamadı");
            }
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(dosya) + "*")) File.Delete(f);
            if (Directory.Exists(Path.Combine(Path.GetTempPath(), "yedek"))) Directory.Delete(Path.Combine(Path.GetTempPath(), "yedek"), true);
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
