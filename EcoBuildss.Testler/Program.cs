// EcoBuildss hesap çekirdeği ve veritabanı birim testleri.
// Çalıştırma:  cd EcoBuildss.Testler  →  dotnet run
// Harici test kütüphanesi kullanılmaz; her test bir koşulu doğrular, sonuçta özet yazılır.
using System.Globalization;
using EcoBuildss.Core;
using EcoBuildss.Depo;

int gecen = 0, kalan = 0;
var hatalar = new List<string>();
void Test(string ad, Action govde)
{
    try { govde(); gecen++; Console.WriteLine($"  ✓ {ad}"); }
    catch (Exception e) { kalan++; hatalar.Add($"{ad}: {e.Message}"); Console.WriteLine($"  ✗ {ad}\n      {e.Message}"); }
}
void Dogru(bool kosul, string mesaj) { if (!kosul) throw new Exception(mesaj); }
void Yakin(double beklenen, double gercek, double tol, string ne) =>
    Dogru(Math.Abs(beklenen - gercek) <= tol, $"{ne}: beklenen {beklenen.ToString("R", CultureInfo.InvariantCulture)}, bulunan {gercek.ToString("R", CultureInfo.InvariantCulture)}");

string veriKlasoru = new[] { "../Veri", "../../../../Veri", "Veri" }.Select(Path.GetFullPath).First(Directory.Exists);
var v = VeriDeposu.Yukle(veriKlasoru);
Girdi Varsayilan(int a = -1, int b = -1, int c = 0, bool filtre = true, VeriDeposu? kaynak = null) => new()
{
    Form = (kaynak ?? v).Formlar[0], Yon = 4, CamOrani = 0.25, Yil = 20, SadeceTs825 = filtre,
    MaliyetEnerji = KararMotoru.Saaty(a), MaliyetKarbon = KararMotoru.Saaty(b), EnerjiKarbon = KararMotoru.Saaty(c)
};
Malzeme M(string id) => v.Malzemeler.First(m => m.Id == id);
double Pwf(double r, int n) => (1 - Math.Pow(1 + r, -n)) / r;

// ======================================================================
Console.WriteLine("Veri yönetimi");
Test("Bina tipleri: 12 geometri, numaralar 1–12, yüzey = duvar + 2 × çatı, kompaktlık 6·V^(2/3)/A", () =>
{
    Dogru(v.Formlar.Count == 12 && v.Formlar.Select(f => f.No).SequenceEqual(Enumerable.Range(1, 12)), $"{v.Formlar.Count} tip");
    Dogru(v.Formlar.All(f => Math.Abs(f.YuzeyAlani - (f.DuvarAlani + 2 * f.CatiAlani)) < 1e-6), "yüzey = duvar + 2 × çatı bağıntısı sağlanmıyor");
    // ENB2012 kompaktlığı iki basamağa yuvarlanmış yayımlanır
    Dogru(v.Formlar.All(f => Math.Abs(6 * Math.Pow(f.Hacim, 2.0 / 3) / f.YuzeyAlani - f.Kompaktlik) < 0.006), "kompaktlık");
});
Test("Malzemeler: 10 mantolama (2 aile × 5/8/10/12/15 cm), 2 kaplamalı cam, 3 duvar, 2 çatı, 1 zemin", () =>
{
    Dogru(v.Malzemeler.Count(m => m.Tur == 'Y') == 10 && v.Malzemeler.Count(m => m.Tur == 'P') == 2 && v.Malzemeler.Count(m => m.Tur == 'D') == 3
          && v.Malzemeler.Count(m => m.Tur == 'C') == 2 && v.Malzemeler.Count(m => m.Tur == 'Z') == 1, "malzeme sayıları");
    foreach (var aile in v.Malzemeler.Where(m => m.Tur == 'Y').GroupBy(m => m.Aile))
        Dogru(aile.Select(m => Math.Round(m.Kalinlik * 100)).OrderBy(x => x).SequenceEqual(new double[] { 5, 8, 10, 12, 15 }), $"{aile.Key} kalınlıkları");
    // TS 825:2024 en az bir camın kaplamalı (Low-E) olmasını ister: listede kaplamasız cam yok
    Dogru(v.Malzemeler.Where(m => m.Tur == 'P').All(m => m.CamTipi.Length > 0), "kaplamasız cam");
});
Test("Hesaplanan fiyatlar: 12 ve 15 cm mantolama = aynı ailenin 10 cm uygulanmış fiyatı + aynı ürünün levha fiyat farkı; çift kat çatı = 2 × 10 cm şilte; regresyona gözlem olarak girmez", () =>
{
    var hes = v.Malzemeler.Where(m => m.FiyatHesaplanan).ToList();
    Dogru(hes.Count == 5 && hes.Count(m => m.Tur == 'Y' && (Math.Round(m.Kalinlik * 100) == 12 || Math.Round(m.Kalinlik * 100) == 15)) == 4 && hes.Any(m => m.Id == "C2"), string.Join(",", hes.Select(m => m.Id)));
    foreach (var m in hes.Where(x => x.Tur == 'Y'))
    {
        var on = v.Malzemeler.First(x => x.Tur == 'Y' && x.Aile == m.Aile && Math.Round(x.Kalinlik * 100) == 10);
        double Levha(double cm) => v.Levhalar.First(l => l.Aile == m.Aile && l.KalinlikCm == cm).FiyatM2;
        Yakin(on.FiyatM2 + Levha(Math.Round(m.Kalinlik * 100)) - Levha(10), m.FiyatM2, 0.006, m.Id);
        Dogru(!on.FiyatHesaplanan, "10 cm fiyatı yayımlanmış olmalı");
    }
    Yakin(2 * M("C1").FiyatM2, M("C2").FiyatM2, 1e-9, "çift kat çatı"); Yakin(2 * M("C1").Kalinlik, M("C2").Kalinlik, 1e-12, "çift kat kalınlık");
    foreach (var md in FiyatModeli.Hepsi(v)) Dogru(md.Gozlemler.Count == 3 && md.Gozlemler.All(o => !o.FiyatHesaplanan), md.Aile);
});
Test("Pencere U (TS 825:2024 Ek A.3): 9 doğrama × 2 cam tipi × 2 ara boşluk çizelgesi; PVC 5 odacıklı → Low-E çift 1,7 (12/16 mm'nin büyüğü), üçlü 1,2 (ara boşluk kaynakta yok → 12/16 mm'nin büyüğü)", () =>
{
    Dogru(v.Dogramalar.Count == 9 && v.PencereDegerleri.Count == 26 && v.PencereDegerleri.All(p => p.U > 0 && p.Kaynak.Length > 0), $"{v.Dogramalar.Count} / {v.PencereDegerleri.Count}");
    Dogru(v.Dogramalar.Contains(Girdi.VarsayilanDograma), "varsayılan doğrama çizelgede yok");
    Dogru(M("P3").AraBoslukMm == null && M("P2").AraBoslukMm == null, "ara boşluk kaynakta yazmıyor");
    Yakin(1.7, v.PencereU("PVC Doğrama (5 Odacıklı)", M("P2"))!.Value, 0, "P2 PVC5"); Yakin(1.2, v.PencereU("PVC Doğrama (5 Odacıklı)", M("P3"))!.Value, 0, "P3 PVC5");
    Yakin(1.8, v.PencereU("PVC Doğrama (3 Odacıklı)", M("P2"))!.Value, 0, "P2 PVC3 (1,8 / 1,6 → büyüğü)");
    Dogru(v.PencereU("Alüminyum Doğrama", M("P3")) == null, "alüminyumda üçlü cam çizelgede yok");
    // Seçenekte enerji ve TS 825 kontrolü cam değil pencere U'su ile yapılır; çizelgede olmayan cam seçenek olmaz
    var g = Varsayilan(filtre: false); g.Dograma = "Alüminyum Doğrama";
    var r = KararMotoru.Hesapla(v, g);
    Dogru(r.Tum.All(x => x.Cam.Id == "P2" && x.PencereU == 2.8 && !x.Ts825Uygun) && r.ToplamKombinasyon == 30, $"{r.ToplamKombinasyon}");
    var rv = KararMotoru.Hesapla(v, Varsayilan(filtre: false));
    Dogru(rv.Dograma == Girdi.VarsayilanDograma && rv.Tum.All(x => x.PencereU == rv.PencereU[x.Cam.Id] && x.PencereU != x.Cam.U), "pencere U cam U'su olmamalı");
});
Test("Malzemeler: fiyat, λ / U ve kaynak boş değil", () =>
{
    foreach (var m in v.Malzemeler)
    {
        Dogru(m.FiyatM2 > 0, $"{m.Id} fiyatı {m.FiyatM2}");
        Dogru(m.FiyatKaynagi.Length > 0 && m.FiyatTarihi.Length > 0, $"{m.Id} fiyat kaynağı/tarihi boş");
        if (m.Tur == 'P') Dogru(m.U > 0, $"{m.Id} U"); else Dogru(m.Lambda > 0 && m.Kalinlik > 0, $"{m.Id} λ/kalınlık");
        if (m.Tur is 'Y' or 'P' or 'D') Dogru(m.KarbonM2 > 0, $"{m.Id} karbonu {m.KarbonM2}");
    }
});
Test("Gerekli 27 parametrenin hepsi var ve kaynağı yazılı; veride türetilmiş ya da başka dosyada tekrarlanan parametre yok", () =>
{
    foreach (var p in new[] { "E1", "E2", "E3", "E8", "M1", "M2", "M3", "M4", "M7", "T6", "V1", "V2", "V3", "F1", "F2", "H1", "I1", "I2", "KDV", "EL1", "EL2", "EL3", "S1", "S2", "B1", "B2", "B3" })
    {
        v.P(p);
        Dogru(v.ParametreBilgi.TryGetValue(p, out var b) && b.kaynak.Length > 0, $"{p} kaynağı boş");
    }
    Dogru(v.Parametre.Count == 27, $"{v.Parametre.Count} parametre");
    foreach (var t in new[] { "E4", "E5", "E9", "I3", "K1", "K2", "T1", "T2", "T3", "T4", "T5" }) Dogru(!v.Parametre.ContainsKey(t), $"{t} tekrar/türetilmiş olarak veride");
});
Test("Türetilen değerler: 1 Sm³ = 10,647 kWh, emisyon 0,20196 kgCO₂/kWh, reel iskonto %4,8008, Kademe-1 0,998 TL/kWh", () =>
{
    Yakin(10.647265, v.GazKWhSm3, 1e-9, "1 Sm³ = 9155 kcal × 0,001163 kWh");
    Yakin(0.20196, v.EmisyonKgKWh, 1e-12, "56,1 kgCO₂/GJ × 0,0036 GJ/kWh");
    Yakin(0.048008, Math.Round(v.ReelIskonto, 6), 0, "(0,36 − 0,2977) ÷ 1,2977");
    Yakin(10.625 / 10.647265, v.GazTLkWh(v.P("E1")), 1e-12, "Kademe-1 TL/kWh");
});
Test("Veri dosyalarında boş hücre yok (bütün CSV'ler, bütün sütunlar)", () =>
{
    foreach (var f in Directory.GetFiles(veriKlasoru, "*.csv"))
    {
        var rows = VeriDeposu.CsvOku(f);
        for (int i = 1; i < rows.Count; i++)
        {
            Dogru(rows[i].Length == rows[0].Length, $"{Path.GetFileName(f)} satır {i + 1}: sütun sayısı");
            for (int j = 0; j < rows[i].Length; j++) Dogru(rows[i][j].Trim().Length > 0, $"{Path.GetFileName(f)} satır {i + 1}, sütun '{rows[0][j]}' boş");
        }
    }
});
Test("TS 825 bölgeleri ısıtma derece-günüyle tutarlı: bölge ortalaması ve ortancası bölge numarasıyla artar", () =>
{
    var b = v.Iller.Where(i => i.Ts825Bolge.HasValue).ToList();
    double Ortanca(IEnumerable<double> x) { var l = x.OrderBy(y => y).ToList(); return l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) / 2; }
    var ort = Enumerable.Range(1, 6).Select(z => b.Where(i => i.Ts825Bolge == z).Average(i => i.IsitmaDG)).ToList();
    var med = Enumerable.Range(1, 6).Select(z => Ortanca(b.Where(i => i.Ts825Bolge == z).Select(i => i.IsitmaDG))).ToList();
    for (int z = 1; z < 6; z++) { Dogru(ort[z] > ort[z - 1], $"{z + 1}. bölge ortalaması"); Dogru(med[z] > med[z - 1], $"{z + 1}. bölge ortancası"); }
});
Test("İl merkezi TS 825 bölgesi: 81 ilin hepsi, hepsi iklim tablosunda ve kaynaklı; teklif yalnız bu illerde verilir", () =>
{
    var b = v.Iller.Where(i => i.Ts825Bolge.HasValue).ToList();
    Dogru(b.Count == 81 && b.All(i => i.Ts825Bolge is >= 1 and <= 6 && i.BolgeKaynagi.Length > 0), $"{b.Count} il");
    Dogru(VeriDeposu.CsvOku(Path.Combine(veriKlasoru, "il_bolgeleri.csv")).Skip(1).All(r => v.Iller.Any(i => i.Ad == r[0])), "iklim tablosunda olmayan il");
});
Test("CSV okuyucu: tırnak içindeki virgül, çift tırnak, BOM ve CRLF", () =>
{
    string yol = Path.GetTempFileName();
    File.WriteAllText(yol, "﻿a,b\r\n\"x, y\",\"he said \"\"ok\"\"\"\r\n1,2", new System.Text.UTF8Encoding(false));
    var r = VeriDeposu.CsvOku(yol); File.Delete(yol);
    Dogru(r.Count == 3, $"{r.Count} satır");
    Dogru(r[1][0] == "x, y" && r[1][1] == "he said \"ok\"" && r[2][1] == "2", "alanlar yanlış okundu");
});
Test("Özel ölçü: 10 × 20 m, 2 kat → taban 200, duvar 420, hacim 1400 m³, kullanım 400 m²", () =>
{
    var f = BinaFormu.OzelOlcu(10, 20, 2, v.P("M7"));
    Yakin(200, f.CatiAlani, 1e-9, "taban"); Yakin(420, f.DuvarAlani, 1e-9, "duvar"); Yakin(1400, f.Hacim, 1e-9, "hacim");
    Yakin(400, f.KullanimAlani(v.P("M7")), 1e-9, "kullanım"); Yakin(820, f.YuzeyAlani, 1e-9, "yüzey");
    Yakin(6 * Math.Pow(1400, 2.0 / 3) / 820, f.Kompaktlik, 1e-12, "kompaktlık");
});
Test("ENB formları: kat sayısı yükseklikten (3,5 m) doğru çıkar", () =>
{
    foreach (var f in v.Formlar) Dogru(f.KatSayisi(3.5) == (f.Yukseklik >= 7 ? 2 : 1), $"Tip {f.No}");
});

// ======================================================================
Console.WriteLine("Enerji modeli (TS 825 derece-gün, Malatya)");
var r0 = KararMotoru.Hesapla(v, Varsayilan(filtre: false));
Test("Duvar U = 1 / (0,17 + d/λ duvar + d/λ yalıtım), bütün birleşimlerde", () =>
{
    foreach (var s in r0.Tum)
        Yakin(1 / (v.P("T6") + s.Duvar.Kalinlik / s.Duvar.Lambda + s.Yalitim.Kalinlik / s.Yalitim.Lambda), s.DuvarU, 1e-12, s.Kod);
});
Test("Çatı ve zemin: U = 1 / (1/U0 + d/λ); çatı 0,231, zemin 0,249 W/m²K", () =>
{
    Yakin(1 / (1 / v.P("M3") + M("C1").Kalinlik / M("C1").Lambda), r0.CatiU, 1e-12, "çatı U");
    Yakin(1 / (1 / v.P("M2") + M("Z1").Kalinlik / M("Z1").Lambda), r0.ZeminU, 1e-12, "zemin U");
    Yakin(0.231, Math.Round(r0.CatiU, 3), 0, "çatı U yuvarlak"); Yakin(0.249, Math.Round(r0.ZeminU, 3), 0, "zemin U yuvarlak");
});
Test("H = ΣU·A + 0,8·Uçatı·Açatı + 0,5·Uzemin·Azemin + 0,33·nh·0,8·V", () =>
{
    double hv = 0.33 * 1.0 * 0.8 * r0.Hacim;
    Yakin(hv, r0.HHavalandirma, 1e-9, "HV");
    foreach (var s in r0.Tum)
        Yakin(s.DuvarU * r0.OpakDuvar + s.PencereU * r0.CamAlani + 0.8 * r0.CatiU * r0.CatiAlani + 0.5 * r0.ZeminU * r0.ZeminAlani + hv, s.H, 1e-9, s.Kod);
});
Test("Yıllık ısıtma = 0,024 · 2425 · H; yakıt = ısıtma ÷ 0,86; gider = yakıt × TL/kWh", () =>
{
    foreach (var s in r0.Tum)
    {
        Yakin(0.024 * 2425 * s.H, s.YillikIsitmaKWh, 1e-6, s.Kod);
        Yakin(s.YillikIsitmaKWh / 0.86, s.YillikGazKWh, 1e-6, s.Kod);
        Yakin(s.YillikGazKWh * v.GazTLkWh(v.P("E1")), s.YillikGiderTL, 1e-6, s.Kod);
    }
});
Test("Geometri (Tip 1, %25 pencere): kullanım 220,5 m², pencere 55,125 m², opak duvar 238,875 m²", () =>
{
    Yakin(220.5, r0.KullanimAlani, 1e-9, "kullanım"); Yakin(55.125, r0.CamAlani, 1e-9, "cam"); Yakin(238.875, r0.OpakDuvar, 1e-9, "opak");
});
Test("Aynı ailede yalıtım kalınlaştıkça ısıtma azalır", () =>
{
    foreach (var c in v.Malzemeler.Where(m => m.Tur == 'P')) foreach (var d in v.Malzemeler.Where(m => m.Tur == 'D'))
    foreach (var aile in v.Malzemeler.Where(m => m.Tur == 'Y').GroupBy(m => m.Aile))
    {
        var l = aile.OrderBy(m => m.Kalinlik).Select(y => r0.Tum.First(s => s.Yalitim == y && s.Cam == c && s.Duvar == d).YillikIsitmaKWh).ToList();
        for (int i = 1; i < l.Count; i++) Dogru(l[i] < l[i - 1], $"{aile.Key} {c.Id} {d.Id}");
    }
});
Test("Pencere oranı 0: cam alanı 0, pencere U TS 825 kontrolüne girmez", () =>
{
    var g = Varsayilan(filtre: false); g.CamOrani = 0;
    var r = KararMotoru.Hesapla(v, g);
    Dogru(r.CamAlani == 0, "cam alanı");
    var s = r.Tum.First(x => x.Yalitim.Id == "Y3" && x.Cam.Id == "P2" && x.Duvar.Id == "D3");
    Dogru(s.Ts825Uygun, "pencere yokken pencere U uygunluğu etkilememeli");
    Yakin(r.OpakDuvar * (s.Duvar.FiyatM2 + s.Yalitim.FiyatM2) + s.YanginEkMaliyet + r.SabitYatirim, s.IlkYatirim, 1e-6, "yatırım");
});
Test("Pencere alanı duvar alanını aşarsa duvarla sınırlanır ve işaretlenir", () =>
{
    var g = Varsayilan(filtre: false); g.Form = BinaFormu.OzelOlcu(80, 80, 1, v.P("M7")); g.CamOrani = 0.40;
    var r = KararMotoru.Hesapla(v, g);
    Dogru(r.CamDuvariAsiyor && Math.Abs(r.CamAlani - g.Form.DuvarAlani) < 1e-9 && r.OpakDuvar == 0, "sınırlama yok");
});

// ======================================================================
Console.WriteLine("Ekonomi – ömür boyu maliyet");
Test("PWF = [1 − (1 + r)^−N] / r; r = %4,8008, N = 20 → 12,68", () =>
{
    Yakin(Pwf(v.ReelIskonto, 20), r0.PWF, 1e-12, "PWF"); Yakin(12.68, Math.Round(r0.PWF, 2), 0, "PWF yuvarlak");
    Yakin(7, EnerjiModeli.Pwf(0, 7), 0, "r = 0 iken PWF = N");
});
Test("Ömür boyu maliyet = ilk yatırım + PWF × yıllık gider; ilk yatırım = kalemlerin toplamı", () =>
{
    foreach (var s in r0.Tum)
    {
        Yakin(s.IlkYatirim + r0.PWF * s.YillikGiderTL, s.ToplamMaliyet, 1e-6, s.Kod);
        double kalem = r0.OpakDuvar * (s.Duvar.FiyatM2 + s.Yalitim.FiyatM2) + r0.CamAlani * s.Cam.FiyatM2 + r0.CatiAlani * M("C1").FiyatM2 + r0.ZeminAlani * M("Z1").FiyatM2 + s.YanginEkMaliyet;
        Yakin(kalem, s.IlkYatirim, 1e-6, s.Kod);
    }
});
Test("İskontolu geri ödeme: tasarrufların bugünkü değeri, bulunan sürede ek yatırıma eşit", () =>
{
    var eko = r0.Tum.OrderBy(x => x.IlkYatirim).First();     // en ucuz birleşime göre (yalıtım kalınlaştırmanın geri dönüşü)
    int sayi = 0;
    foreach (var s in r0.Tum)
    {
        var k = Kpi.Hesapla(s, r0, eko, r0.Iskonto, 20);
        if (!k.GeriOdemeVar) continue;
        sayi++;
        double t = k.GeriOdemeYil, tas = eko.YillikGiderTL - s.YillikGiderTL; int tam = (int)Math.Floor(t);
        double bd = Enumerable.Range(1, tam).Sum(i => tas / Math.Pow(1 + r0.Iskonto, i)) + (t - tam) * tas / Math.Pow(1 + r0.Iskonto, tam + 1);
        Yakin(s.IlkYatirim - eko.IlkYatirim, bd, 1e-6 * s.IlkYatirim, s.Kod);
    }
    Dogru(sayi > 0, "geri ödemesi olan seçenek yok");
});
Test("Bulgu: TS 825'e uygun en ucuz pakete göre ek yatırımların hiçbiri iskontolu olarak geri dönmez (Tip 1, %25)", () =>
{
    var uygun = r0.Tum.Where(x => x.Ts825Uygun).ToList(); var eko = uygun.OrderBy(x => x.IlkYatirim).First();
    Dogru(uygun.All(s => !Kpi.Hesapla(s, r0, eko, r0.Iskonto, 20).GeriOdemeVar), "geri dönen ek yatırım var");
});
Test("Kontrol değerleri (README): Tip 1, %25, 20 yıl, Dengeli, PVC 5 odacıklı → EPS 12 cm · Üçlü Low-E · Bims", () =>
{
    var t = KararMotoru.Hesapla(v, Varsayilan()).Siralama[0];
    Dogru(t.Kod == "Y7P3D3", t.Kod);
    Yakin(517355, Math.Round(t.IlkYatirim), 0, "ilk yatırım"); Yakin(832167, Math.Round(t.ToplamMaliyet), 0, "ömür boyu maliyet");
    Yakin(21404, Math.Round(t.YillikIsitmaKWh), 0, "yıllık ısıtma");
});
Test("Kontrol değerleri: Ekonomik paket 448.380 TL (25.200 TL yangın şeridi dahil), Yüksek performans 789.019 TL", () =>
{
    var l = KararMotoru.Hesapla(v, Varsayilan()).Siralama;
    var eko = l.OrderBy(s => s.IlkYatirim).First(); var yp = l.OrderBy(s => s.YillikIsitmaKWh).First();
    Dogru(eko.Kod == "Y3P2D3" && yp.Kod == "Y10P3D2", $"{eko.Kod} / {yp.Kod}");
    Yakin(448380, Math.Round(eko.IlkYatirim), 0, "ekonomik"); Yakin(25200, Math.Round(eko.YanginEkMaliyet), 0, "yangın şeridi"); Yakin(789019, Math.Round(yp.IlkYatirim), 0, "yüksek performans");
});

// ======================================================================
Console.WriteLine("Model yönetimi – AHP ve TOPSIS");
Test("AHP Bütçe ön ayarı: 0,714 / 0,143 / 0,143, CR = 0", () =>
{
    var (w, cr) = KararMotoru.Ahp(5, 5, 1);
    Yakin(5.0 / 7, w[0], 1e-12, "maliyet"); Yakin(1.0 / 7, w[1], 1e-12, "enerji"); Yakin(0, cr, 1e-12, "CR");
});
Test("AHP Dengeli: 0,50 / 0,25 / 0,25", () => { var (w, _) = KararMotoru.Ahp(2, 2, 1); Yakin(0.5, w[0], 1e-12, "maliyet"); Yakin(0.25, w[2], 1e-12, "karbon"); });
Test("AHP Enerji ve karbon: 0,143 / 0,429 / 0,429", () => { var (w, _) = KararMotoru.Ahp(1.0 / 3, 1.0 / 3, 1); Yakin(1.0 / 7, w[0], 1e-12, "maliyet"); Yakin(3.0 / 7, w[1], 1e-12, "enerji"); });
Test("AHP tutarsız yargı (A>B, B>C, C>A) CR > 0,10 verir", () => { var (_, cr) = KararMotoru.Ahp(5, 1.0 / 5, 5); Dogru(cr > 0.10, $"CR = {cr}"); });
Test("AHP ağırlıkları her kaydırıcı birleşiminde toplam 1, CR ≥ 0", () =>
{
    for (int a = -8; a <= 8; a++) for (int b = -8; b <= 8; b++) for (int c = -8; c <= 8; c++)
    {
        var (w, cr) = KararMotoru.Ahp(KararMotoru.Saaty(a), KararMotoru.Saaty(b), KararMotoru.Saaty(c));
        Dogru(Math.Abs(w.Sum() - 1) < 1e-12 && w.All(x => x > 0) && cr >= 0 && !double.IsNaN(cr), $"({a},{b},{c})");
    }
});
Test("TS 825 filtresi: kalanların hepsi duvar, pencere, çatı ve zemin sınırlarını sağlar (42 / 60)", () =>
{
    var r = KararMotoru.Hesapla(v, Varsayilan());
    Dogru(r.ToplamKombinasyon == 60 && r.Siralama.Count == 42, $"{r.Siralama.Count} / {r.ToplamKombinasyon}");
    Dogru(r.Siralama.All(s => s.DuvarU <= v.Bolge(4)!.UDuvar && s.PencereU <= v.Bolge(4)!.UPencere && r.CatiU <= v.Bolge(4)!.UCati && r.ZeminU <= v.Bolge(4)!.UZemin), "uygun olmayan seçenek listede");
});
Test("TOPSIS puanı 0–1 aralığında, sıralama azalan", () =>
{
    var r = KararMotoru.Hesapla(v, Varsayilan());
    Dogru(r.Siralama.All(s => s.Skor >= 0 && s.Skor <= 1), "aralık dışı puan");
    for (int i = 1; i < r.Siralama.Count; i++) Dogru(r.Siralama[i - 1].Skor >= r.Siralama[i].Skor, "sıra bozuk");
});
Test("TOPSIS: her kriterde başka bir seçenekçe geçilen seçenek 1. olamaz (üç öncelikte)", () =>
{
    foreach (var (a, b, c) in new[] { (-4, -4, 0), (-1, -1, 0), (2, 2, 0) })
    {
        var r = KararMotoru.Hesapla(v, Varsayilan(a, b, c)); var t = r.Siralama[0];
        Dogru(!r.Siralama.Any(o => o != t && o.ToplamMaliyet <= t.ToplamMaliyet && o.YillikIsitmaKWh <= t.YillikIsitmaKWh && o.ToplamCO2Kg <= t.ToplamCO2Kg &&
                                   (o.ToplamMaliyet < t.ToplamMaliyet || o.YillikIsitmaKWh < t.YillikIsitmaKWh || o.ToplamCO2Kg < t.ToplamCO2Kg)), $"öncelik ({a},{b},{c})");
    }
});
Test("12 form + özel ölçüler × 4 pencere oranı × 3 öncelik × 2 filtre: sonuç sonlu ve boş değil", () =>
{
    var formlar = v.Formlar.Concat(new[] { BinaFormu.OzelOlcu(4, 4, 1, 3.5), BinaFormu.OzelOlcu(12, 15, 3, 3.5), BinaFormu.OzelOlcu(80, 80, 10, 3.5) });
    foreach (var f in formlar) foreach (double cam in new[] { 0, 0.10, 0.25, 0.40 })
    foreach (var (a, b, c) in new[] { (-4, -4, 0), (-1, -1, 0), (2, 2, 0) }) foreach (bool fl in new[] { false, true })
    {
        var g = Varsayilan(a, b, c, fl); g.Form = f; g.CamOrani = cam;
        var r = KararMotoru.Hesapla(v, g);
        var l = r.Siralama.Count > 0 ? r.Siralama : r.Tum;
        Dogru(l.Count > 0, "boş liste");
        Dogru(l.All(s => double.IsFinite(s.Skor) && double.IsFinite(s.ToplamMaliyet) && s.YillikIsitmaKWh > 0), $"sonlu olmayan değer: {f} cam {cam}");
    }
});

// ======================================================================
Console.WriteLine("Optimizasyon ve regresyon");
var modeller = FiyatModeli.Hepsi(v);
Test("Fiyat–kalınlık regresyonu: EPS uygulanmış fiyat 473,68 + 49,737·cm (elle hesap); kullanılan doğru levha eğimiyle ağırlık merkezinden geçer", () =>
{
    var m = modeller.First(x => x.Aile == "EPS");
    // x = 5, 8, 10; y = 725, 865, 975 → Sxx = 38/3, Sxy = 630
    Yakin(630.0 / (38.0 / 3), m.UygulamaEgim, 1e-9, "uygulanmış eğim"); Yakin(855 - m.UygulamaEgim * 23.0 / 3, m.UygulamaSabit, 1e-9, "uygulanmış sabit");
    Yakin(855 - m.EgimCm * 23.0 / 3, m.Sabit, 1e-9, "kullanılan sabit = ȳ − b·x̄");
    Yakin(0.8595, Math.Round(m.R2, 4), 0, "kullanılan doğrunun uygulanmış fiyatlara uyumu");
});
Test("Regresyon: artıkların toplamı 0 ve kalınlıkla dik (en küçük kareler koşulu; uygulanmış fiyat ve levha serisi)", () =>
{
    foreach (var m in modeller)
    {
        double Uy(double cm) => m.UygulamaSabit + m.UygulamaEgim * cm;
        double e0 = m.Gozlemler.Sum(o => o.FiyatM2 - Uy(o.Kalinlik * 100)), e1 = m.Gozlemler.Sum(o => (o.FiyatM2 - Uy(o.Kalinlik * 100)) * o.Kalinlik * 100);
        Yakin(0, e0, 1e-8, $"{m.Aile} Σe"); Yakin(0, e1, 1e-7, $"{m.Aile} Σxe");
        if (m.LevhaEgimiKullaniliyor)
        {
            double l0 = m.Levhalar.Sum(l => l.FiyatM2 - (m.LevhaSabit + m.LevhaEgim * l.KalinlikCm)), l1 = m.Levhalar.Sum(l => (l.FiyatM2 - (m.LevhaSabit + m.LevhaEgim * l.KalinlikCm)) * l.KalinlikCm);
            Yakin(0, l0, 1e-8, $"{m.Aile} levha Σe"); Yakin(0, l1, 1e-7, $"{m.Aile} levha Σxe");
            // kullanılan doğru levha eğimine sahip ve uygulanmış fiyatların ağırlık merkezinden geçer
            Yakin(m.LevhaEgim, m.EgimCm, 1e-12, "eğim");
            Yakin(m.Gozlemler.Average(o => o.FiyatM2), m.Fiyat(m.Gozlemler.Average(o => o.Kalinlik * 100)), 1e-9, "ağırlık merkezi");
        }
    }
});
Test("Levha regresyonu (taş yünü F 150 Pro, 8 kalınlık 4–15 cm, Ekim 2026): b = 71,72 TL/m² her cm, R² = 0,9996, b'nin %95 aralığı ±1,49", () =>
{
    var m = modeller.First(x => x.Aile == "Taş yünü");
    Dogru(m.LevhaEgimiKullaniliyor && m.Levhalar.Count == 8 && m.Levhalar.All(l => l.Urun.StartsWith("Bonus Premium F 150 Pro")), $"{m.Levhalar.Count} levha");
    Yakin(71.7203, Math.Round(m.LevhaEgim, 4), 0, "eğim"); Yakin(0.9996, Math.Round(m.LevhaR2, 4), 0, "R²");
    Yakin(1.49, Math.Round(2.447 * m.LevhaHataStd, 2), 0, "%95 aralığı (t 0,975; 6 = 2,447)");
    Yakin(74.34, Math.Round(m.UygulamaEgim, 2), 0, "uygulanmış fiyat eğimi (3 nokta)");
});
Test("Levha regresyonu (Expert EPS Beyaz, 12 kalınlık 4–15 cm, Kasım 2026): b = 31,21 TL/m² her cm, R² = 0,9992, b'nin %95 aralığı ±0,60", () =>
{
    var m = modeller.First(x => x.Aile == "EPS");
    Dogru(m.LevhaEgimiKullaniliyor && m.Levhalar.Count == 12 && m.Levhalar.All(l => l.Urun.StartsWith("Expert EPS Beyaz")), $"{m.Levhalar.Count} levha");
    Yakin(31.2117, Math.Round(m.LevhaEgim, 4), 0, "eğim"); Yakin(0.9992, Math.Round(m.LevhaR2, 4), 0, "R²");
    Yakin(0.60, Math.Round(2.228 * m.LevhaHataStd, 2), 0, "%95 aralığı (t 0,975; 10 = 2,228)");
    Yakin(49.74, Math.Round(m.UygulamaEgim, 2), 0, "uygulanmış fiyat eğimi (3 nokta)");
    Yakin(31.2117, Math.Round(m.EgimCm, 4), 0, "optimizasyonda kullanılan eğim levha serisinden");
});
Test("Optimum kalınlık: sayısal çözüm kapalı form çözümle aynı (2 aile × 3 duvar × 2 cam × 3 süre)", () =>
{
    foreach (var m in modeller) foreach (var d in v.Malzemeler.Where(x => x.Tur == 'D')) foreach (var c in v.Malzemeler.Where(x => x.Tur == 'P'))
    foreach (int yil in new[] { 10, 20, 40 })
    {
        var g = Varsayilan(); g.Yil = yil;
        var k = Optimizasyon.OptimumKalinlik(v, g, m, d, c);
        Yakin(k.KapaliFormCm, k.KisitsizCm, 1e-3, $"{m.Aile} {d.Id} {c.Id} {yil} yıl");
    }
});
Test("Optimum kalınlık: TS 825 alt sınırında U = 0,35; kısıtlı optimum sınırı ihlal etmez", () =>
{
    foreach (var m in modeller) foreach (var d in v.Malzemeler.Where(x => x.Tur == 'D'))
    {
        var k = Optimizasyon.OptimumKalinlik(v, Varsayilan(), m, d, M("P3"));
        double r0w = v.P("T6") + d.Kalinlik / d.Lambda;
        Yakin(v.Bolge(4)!.UDuvar, 1 / (r0w + k.AltSinirCm / 100 / m.Lambda), 1e-9, "alt sınırda U");
        Dogru(k.UOptimum <= v.Bolge(4)!.UDuvar + 1e-9 && k.OptimumCm >= k.AltSinirCm - 1e-9, "kısıt ihlali");
        double cmMin = k.Egri.OrderBy(e => e.lcc).First().cm;
        Dogru(Math.Abs(cmMin - k.KisitsizCm) <= 0.5, $"eğrinin en küçüğü ({cmMin}) ekonomik optimuma ({k.KisitsizCm}) yakın değil");
    }
});
Test("Optimum kalınlık: Tip 1, Bims + Üçlü cam, 20 yıl → EPS ekonomik 7,49 cm < TS 825 sınırı 8,45 cm (yönetmelik bağlayıcı)", () =>
{
    var k = Optimizasyon.OptimumKalinlik(v, Varsayilan(), modeller.First(x => x.Aile == "EPS"), M("D3"), M("P3"));
    Yakin(7.49, Math.Round(k.KisitsizCm, 2), 0, "ekonomik optimum"); Yakin(8.45, Math.Round(k.AltSinirCm, 2), 0, "TS 825 sınırı");
    Dogru(k.KisitBaglayici && k.EnIyiUrun?.Id == "Y3", $"en iyi ürün {k.EnIyiUrun?.Id}");
});
Test("En uygun satılık ürün, aynı aile içinde TS 825'i sağlayanlar arasında ömür boyu maliyeti en düşük olandır", () =>
{
    foreach (var m in modeller) foreach (var d in v.Malzemeler.Where(x => x.Tur == 'D'))
    {
        var k = Optimizasyon.OptimumKalinlik(v, Varsayilan(), m, d, M("P3"));
        var aday = r0.Tum.Where(s => s.Cam.Id == "P3" && s.Duvar == d && s.Yalitim.Aile == m.Aile && s.DuvarU <= v.Bolge(4)!.UDuvar + 1e-12).OrderBy(s => s.ToplamMaliyet).FirstOrDefault();
        Dogru(aday?.Yalitim == k.EnIyiUrun, $"{m.Aile} {d.Id}: {aday?.Yalitim.Id} ≠ {k.EnIyiUrun?.Id}");
        if (aday != null) Yakin(aday.ToplamMaliyet, k.EnIyiUrunLcc, 1e-6, "LCC");
    }
});
Test("Pareto cephesi: cephedeki hiçbir seçenek başka biri tarafından geçilmez; cephe dışındakiler geçilir", () =>
{
    var uygun = r0.Tum.Where(s => s.Ts825Uygun).ToList();
    foreach (ButceAmaci amac in Enum.GetValues(typeof(ButceAmaci)))
    {
        var f = Optimizasyon.AmacFonksiyonu(amac);
        var cephe = Optimizasyon.Pareto(uygun, f);
        bool Gecer(Secenek a, Secenek b) => a.IlkYatirim <= b.IlkYatirim && f(a) <= f(b) && (a.IlkYatirim < b.IlkYatirim || f(a) < f(b));
        Dogru(cephe.All(c => !uygun.Any(o => Gecer(o, c))), $"{amac}: cephede geçilen seçenek var");
        Dogru(uygun.Except(cephe).All(c => uygun.Any(o => Gecer(o, c))), $"{amac}: geçilmeyen seçenek cephe dışında");
    }
});
Test("Bütçe kısıtlı seçim: tam sayımla bulunan en iyi seçenekle aynı; bütçeyi aşmaz", () =>
{
    var uygun = r0.Tum.Where(s => s.Ts825Uygun).ToList();
    foreach (ButceAmaci amac in Enum.GetValues(typeof(ButceAmaci)))
    {
        var f = Optimizasyon.AmacFonksiyonu(amac);
        for (double butce = 300_000; butce <= 800_000; butce += 25_000)
        {
            var b = Optimizasyon.Butce(r0.Tum, butce, amac);
            var beklenen = uygun.Where(s => s.IlkYatirim <= butce).OrderBy(f).ThenBy(s => s.IlkYatirim).FirstOrDefault();
            Dogru(b.Secim == beklenen, $"{amac} {butce}");
            if (b.Secim != null) Dogru(b.Secim.IlkYatirim <= butce && b.Secim.Ts825Uygun, "kısıt ihlali");
            if (b.SonrakiAdim != null) Dogru(b.SonrakiAdim.IlkYatirim > butce && f(b.SonrakiAdim) < f(b.Secim!), "sonraki adım daha iyi değil");
        }
    }
});
Test("Bütçe: 300.000 TL'de uygun seçenek yok; sınırsız bütçede en az ısıtma = Yüksek performans paketi", () =>
{
    Dogru(Optimizasyon.Butce(r0.Tum, 300_000, ButceAmaci.Isitma).Secim == null, "300 bin TL ile seçenek bulundu");
    Dogru(Optimizasyon.Butce(r0.Tum, 1e12, ButceAmaci.Isitma).Secim?.Kod == "Y10P3D2", "sınırsız bütçe");
});


// ======================================================================
Console.WriteLine("Yeni modüller: yakıt, soğutma, yenileme, yangın, enerji artışı, belirsizlik");
Test("Yakıt: LPG, kömür, elektrik ve ısı pompası; TL/kWh = fiyat ÷ ısıl değer, yakıt = ısıtma ÷ verim, gider ve CO₂ yakıta göre", () =>
{
    Dogru(v.Yakitlar.Count == 4 && v.Yakitlar.All(y => y.Verim > 0 && y.IsilDegerKWh > 0 && y.EmisyonKgKWh > 0 && y.FiyatKaynagi.Length > 0 && y.VerimKaynagi.Length > 0), "yakıt verisi");
    Yakin(1900 / 1.2 / 12, v.Yakitlar.First(y => y.Id == "LPG").FiyatBirim, 1e-5, "LPG TL/kg");
    Yakin(47.3 / 3.6, v.Yakitlar.First(y => y.Id == "LPG").IsilDegerKWh, 1e-5, "LPG kWh/kg");
    Yakin(5000 * 0.001163, v.Yakitlar.First(y => y.Id == "KOMUR").IsilDegerKWh, 1e-9, "kömür kWh/kg");
    foreach (var y in v.Yakitlar)
    {
        var g = Varsayilan(filtre: false); g.Yakit = y.Id;
        var r = KararMotoru.Hesapla(v, g);
        double tlkwh = y.Elektrikli ? v.P("EL1") : y.FiyatBirim / y.IsilDegerKWh;
        foreach (var s in r.Tum)
        {
            Yakin(s.YillikIsitmaKWh / y.Verim, s.YillikGazKWh, 1e-6, $"{y.Id} yakıt");
            Yakin(s.YillikGazKWh * tlkwh, s.YillikGiderTL, 1e-6, $"{y.Id} gider");
        }
        var x = r.Tum[0]; var x0 = r0.Tum.First(z => z.Kod == x.Kod);
        Yakin(x0.YillikIsitmaKWh, x.YillikIsitmaKWh, 1e-9, "ısıtma ihtiyacı yakıttan bağımsız");
    }
    // Isı pompası (SCOP 3,8) dirençli ısıtıcıdan 3,8 kat az elektrik çeker
    var gE = Varsayilan(filtre: false); gE.Yakit = "ELEK"; var gH = Varsayilan(filtre: false); gH.Yakit = "ISIPOMPASI";
    Yakin(KararMotoru.Hesapla(v, gE).Tum[0].YillikGiderTL / 3.8, KararMotoru.Hesapla(v, gH).Tum[0].YillikGiderTL, 1e-6, "SCOP");
});
Test("Soğutma: 0,024 × soğutma derece-günü × H; elektrik = ÷ SEER 4,6; gider ve CO₂ eklenir; kapalıyken sıfır", () =>
{
    var g = Varsayilan(filtre: false); g.Sogutma = true;
    var r = KararMotoru.Hesapla(v, g);
    Yakin(374, r.SogutmaDG, 0, "Malatya soğutma DG");
    foreach (var s in r.Tum)
    {
        var s0 = r0.Tum.First(z => z.Kod == s.Kod);
        Yakin(0.024 * 374 * s.H, s.YillikSogutmaKWh, 1e-6, s.Kod);
        Yakin(s.YillikSogutmaKWh / 4.6 * v.P("EL1"), s.YillikSogutmaTL, 1e-6, s.Kod);
        Yakin(s0.YillikGiderTL + s.YillikSogutmaTL, s.YillikGiderTL, 1e-6, s.Kod);
        Yakin(s0.ToplamCO2Kg + s.YillikSogutmaElektrikKWh * v.P("EL3") * 20, s.ToplamCO2Kg, 1e-6, s.Kod);
    }
    Dogru(r0.Tum.All(s => s.YillikSogutmaKWh == 0 && s.YillikSogutmaTL == 0), "soğutma kapalıyken sıfır değil");
});
Test("Yenileme (mevcut bina): duvar seçilmez, U = 1/(1/1,78 + d/λ), duvar bedeli ve karbonu yok; tasarruf ve geri dönüş yalıtımsız binaya göre", () =>
{
    var g = Varsayilan(filtre: false); g.Mevcut = true;
    var r = KararMotoru.Hesapla(v, g);
    Dogru(r.ToplamKombinasyon == 20 && r.Tum.All(s => s.MevcutDuvar && s.DuvarAdi == "Mevcut duvar"), $"{r.ToplamKombinasyon}");
    Yakin(EnerjiModeli.YillikIsitma(r.DG, r.HBaz) / 0.86 * r.GazTL, r.MevcutGiderTL, 1e-6, "mevcut gider");
    foreach (var s in r.Tum)
    {
        Yakin(1 / (1 / 1.78 + s.Yalitim.Kalinlik / s.Yalitim.Lambda), s.DuvarU, 1e-12, s.Kod);
        Yakin(r.OpakDuvar * s.Yalitim.FiyatM2 + r.CamAlani * s.Cam.FiyatM2 + s.YanginEkMaliyet + r.SabitYatirim, s.IlkYatirim, 1e-6, s.Kod + " yatırım");
        var m = Kpi.MevcutaGore(s, r);
        Yakin(r.MevcutGiderTL - s.YillikGiderTL, m.tasarrufTL, 1e-9, "tasarruf");
        Yakin(r.PWF * m.tasarrufTL - s.IlkYatirim, m.netBugunkuDeger, 1e-6, "NBD");
        if (m.geriDonusYil is double t)
        {
            int tam = (int)Math.Floor(t);
            double bd = Enumerable.Range(1, tam).Sum(i => m.tasarrufTL / Math.Pow(1 + r.EnerjiIskonto, i)) + (t - tam) * m.tasarrufTL / Math.Pow(1 + r.EnerjiIskonto, tam + 1);
            Yakin(s.IlkYatirim, bd, 1e-6 * s.IlkYatirim, "geri dönüş");
        }
    }
});
Test("Yangın yönetmeliği: 28,50 m'den yüksek binada EPS seçenek olmaz; altında EPS'ye zeminden 1,5 m A1 şerit farkı eklenir, taş yününe eklenmez", () =>
{
    Dogru(v.Yangin("EPS")!.A2VeyaUstu == false && v.Yangin("Taş yünü")!.A2VeyaUstu, "yangın sınıfları");
    // Tip 1 (7 m): çevre 294 / 7 = 42 m, şerit 1,5 × 42 = 63 m²
    Yakin(63, r0.YanginSeridiM2, 1e-9, "şerit alanı");
    foreach (var s in r0.Tum)
    {
        if (s.Yalitim.Aile == "Taş yünü") { Dogru(s.YanginEkMaliyet == 0 && s.YanginNotu == "", s.Kod); continue; }
        var a1 = v.Malzemeler.First(m => m.Tur == 'Y' && m.Aile == "Taş yünü" && Math.Abs(m.Kalinlik - s.Yalitim.Kalinlik) < 1e-9);
        Yakin(63 * (a1.FiyatM2 - s.Yalitim.FiyatM2), s.YanginEkMaliyet, 1e-6, s.Kod);
        Dogru(s.YanginNotu.Contains("pencere"), "7 m bina 6,5 m'den yüksek: pencere bariyeri notu");
    }
    var g = Varsayilan(filtre: false); g.Form = BinaFormu.OzelOlcu(12, 15, 10, 3.5);   // 35 m
    var r = KararMotoru.Hesapla(v, g);
    Dogru(r.Tum.All(s => s.Yalitim.Aile == "Taş yünü") && r.YanginElenen == 30 && r.ToplamKombinasyon == 30, $"{r.YanginElenen}");
    var g8 = Varsayilan(filtre: false); g8.Form = BinaFormu.OzelOlcu(12, 15, 8, 3.5);  // 28 m: sınırın altında
    Dogru(KararMotoru.Hesapla(v, g8).Tum.Any(s => s.Yalitim.Aile == "EPS"), "28 m'de EPS olmalı");
});
Test("Enerji fiyatı reel artışı: PWF = Σ ((1+e)/(1+r))^t; e = 0'da değişmez, artınca ömür boyu maliyet artar", () =>
{
    foreach (double e in new[] { -0.03, 0.0, 0.03, 0.08 })
    {
        var g = Varsayilan(filtre: false); g.EnerjiArtisi = e;
        var r = KararMotoru.Hesapla(v, g);
        double pwf = Enumerable.Range(1, 20).Sum(t => Math.Pow((1 + e) / (1 + r.Iskonto), t));
        Yakin(pwf, r.PWF, 1e-9, $"e = {e}");
        if (e > 0) Dogru(r.Tum.Zip(r0.Tum).All(p => p.First.ToplamMaliyet > p.Second.ToplamMaliyet), "maliyet artmadı");
    }
});
Test("Duvar ağırlığı = opak duvar × (duvar + yalıtım birim ağırlığı); yenilemede yalnız yalıtım", () =>
{
    foreach (var s in r0.Tum) Yakin(r0.OpakDuvar * (s.Duvar.BirimAgirlik + s.Yalitim.BirimAgirlik) / 1000, s.DuvarAgirlikTon, 1e-9, s.Kod);
    Dogru(r0.Tum.Where(s => s.Duvar.Id == "D2").All(s => r0.Tum.First(z => z.Yalitim == s.Yalitim && z.Cam == s.Cam && z.Duvar.Id == "D1").DuvarAgirlikTon > s.DuvarAgirlikTon), "gazbeton tuğladan hafif");
});
Test("Fiyat belirsizliği (Monte Carlo): aralıklar fiyat hesabından okunur, sabit tohumla tekrarlanabilir, oranlar toplamı 1, öneri %66,5 birinci", () =>
{
    Dogru(Belirsizlik.FiyatAraligi(M("Y1")) is (650, 800) && Belirsizlik.FiyatAraligi(M("P2")) is (1450, 1900) && Belirsizlik.FiyatAraligi(M("D1")) == null, "aralık okuma");
    var g = Varsayilan();
    var a = Belirsizlik.Calistir(v, g, 1000, 42); var b = Belirsizlik.Calistir(v, g, 1000, 42);
    Dogru(a.OneriKod == "Y7P3D3" && a.AraliklıMalzeme == 8, a.OneriKod);
    Yakin(1, a.Birinciler.Sum(x => x.oran), 1e-9, "oran toplamı");
    Dogru(a.OneriBirinciOrani == b.OneriBirinciOrani && a.LccP50 == b.LccP50, "tekrarlanabilirlik");
    Yakin(0.665, Math.Round(a.OneriBirinciOrani, 3), 0, "öneri oranı");
    Dogru(a.LccP5 < a.LccP50 && a.LccP50 < a.LccP95, "yüzdelik sırası");
    // Fiyatlar aralığın ortasındayken hesaplanan ömür boyu maliyet çekilişlerin %5–%95 aralığında
    var t = KararMotoru.Hesapla(v, g).Siralama[0];
    Dogru(t.ToplamMaliyet > a.LccP5 && t.ToplamMaliyet < a.LccP95, "orta değer aralıkta değil");
});
Test("Fiyat yaşı: tarih metninden ay okunur ('Şubat 2026' → 9 ay, '3 Kasım 2026' → 0), okunamazsa null", () =>
{
    var bugun = new DateTime(2026, 11, 7);
    Dogru(Ortak.FiyatYasiAy("Şubat 2026", bugun) == 9 && Ortak.FiyatYasiAy("3 Kasım 2026", bugun) == 0 && Ortak.FiyatYasiAy("2026 (erişim Kasım 2026)", bugun) == 0 && Ortak.FiyatYasiAy("2026", bugun) == null, "fiyat yaşı");
    Dogru(v.Malzemeler.All(m => Ortak.FiyatAyi(m.FiyatTarihi) != null) && v.Yakitlar.All(y => Ortak.FiyatAyi(y.FiyatTarihi) != null), "okunamayan fiyat tarihi");
});
// ======================================================================
Console.WriteLine("İl iklimi, TS 825 bölgeleri ve kümeleme");
Test("İller: 81 il (78'i Şensoy vd./MGM, Çankırı, Karabük, Şırnak MGM gün-derece sayfası 1991–2005 ortalaması; hepsi kaynaklı), Malatya 2425 / 374; tablo ısıtma derece-gününe göre artan", () =>
{
    Dogru(v.Iller.Count == 81 && VeriDeposu.CsvOku(Path.Combine(veriKlasoru, "iller.csv")).Skip(1).All(r => r.Length == 4 && r[3].Length > 0), $"{v.Iller.Count} il");
    var mal = v.Iller.First(i => i.Ad == "Malatya");
    Yakin(2425, mal.IsitmaDG, 0, "Malatya ısıtma"); Yakin(374, mal.SogutmaDG, 0, "Malatya soğutma"); Dogru(mal.Ts825Bolge == 4, "Malatya bölgesi");
    for (int i = 1; i < v.Iller.Count; i++) Dogru(v.Iller[i].IsitmaDG >= v.Iller[i - 1].IsitmaDG, $"sıra {v.Iller[i].Ad}");
    Dogru(v.Iller.Select(i => i.Ad).Distinct().Count() == 81, "tekrarlayan il");
    Dogru(v.Iller.Where(i => i.Ts825Bolge != null).All(i => i.BolgeKaynagi.Length > 0), "bölgesi olup kaynağı olmayan il");
});
Test("TS 825 bölgeleri: 6 bölge; 4. bölge (Malatya): 0,35 / 0,25 / 0,30 / 1,8; sınırlar soğuk bölgeye doğru sıkılaşır", () =>
{
    Dogru(v.Bolgeler.Count == 6, $"{v.Bolgeler.Count}");
    var b4 = v.Bolge(4)!;
    Yakin(0.35, b4.UDuvar, 0, "duvar"); Yakin(0.25, b4.UCati, 0, "çatı"); Yakin(0.30, b4.UZemin, 0, "zemin"); Yakin(1.8, b4.UPencere, 0, "pencere");
    for (int i = 2; i <= 6; i++) Dogru(v.Bolge(i)!.UDuvar <= v.Bolge(i - 1)!.UDuvar && v.Bolge(i)!.UCati <= v.Bolge(i - 1)!.UCati, $"{i}. bölge");
});
Test("İl seçimi: ısıtma derece-günle doğru orantılı; Malatya seçimi varsayılanla aynı", () =>
{
    var g1 = Varsayilan(filtre: false); g1.Il = v.Iller.First(i => i.Ad == "Malatya");
    var a = KararMotoru.Hesapla(v, Varsayilan(filtre: false)); var b = KararMotoru.Hesapla(v, g1);
    Dogru(a.Tum.Zip(b.Tum).All(p => p.First.YillikIsitmaKWh == p.Second.YillikIsitmaKWh && p.First.ToplamMaliyet == p.Second.ToplamMaliyet), "Malatya farklı");
    var g2 = Varsayilan(filtre: false); g2.Il = v.Iller.First(i => i.Ad == "Erzurum");
    var c = KararMotoru.Hesapla(v, g2);
    foreach (var (x, y) in a.Tum.Zip(c.Tum)) Yakin(x.YillikIsitmaKWh * 4642.0 / 2425.0, y.YillikIsitmaKWh, 1e-6, x.Kod);
    Yakin(4642, c.DG, 0, "Erzurum DG");
});
Test("Bölge sınırları: Antalya (1. bölge) 52 seçenek; Erzurum (6. bölge, duvar ≤ 0,25, çatı ≤ 0,20) 20 seçenek, hepsi 10 cm ve üstü yalıtımla, çift kat çatıyla", () =>
{
    var g1 = Varsayilan(); g1.Il = v.Iller.First(i => i.Ad == "Antalya"); g1.Bolge = 1;
    var g6 = Varsayilan(); g6.Il = v.Iller.First(i => i.Ad == "Erzurum"); g6.Bolge = 6;
    var r1 = KararMotoru.Hesapla(v, g1); var r6 = KararMotoru.Hesapla(v, g6);
    Dogru(r1.Siralama.Count == 52 && r1.Cati.Id == "C1", $"Antalya {r1.Siralama.Count}");
    Dogru(r6.Siralama.Count == 20 && r6.Cati.Id == "C2" && r6.CatiU <= r6.SinirCati && r6.Siralama.All(s => s.Yalitim.Kalinlik >= 0.10 && s.DuvarU <= 0.25), $"Erzurum {r6.Siralama.Count}");
    // Çatı katmanı: bölge sınırını sağlayanların en ucuzu (4. bölgede tek kat yeter)
    Dogru(KararMotoru.Hesapla(v, Varsayilan()).Cati.Id == "C1", "4. bölge çatı");
    Dogru(r1.Siralama.All(s => s.DuvarU <= 0.45 && r1.CatiU <= 0.35), "1. bölge sınırı");
});
var kume6 = Kumeleme.Yap(v.Iller, sabitK: 6);
var kTarama = Kumeleme.Yap(v.Iller);
Test("Kümeleme: her il tam bir kümede; kümeler ortalama ısıtma derece-gününe göre numaralı; merkezler üyelerin ortalaması", () =>
{
    Dogru(kume6.K == 6 && kume6.Kumeler.Count == 6, $"k = {kume6.K}");
    Dogru(kume6.Kumeler.Sum(k => k.Uyeler.Count) == 81 && v.Iller.All(i => kume6.Kumeler.Count(k => k.Uyeler.Contains(i)) == 1), "üyelik");
    for (int i = 1; i < 6; i++) Dogru(kume6.Kumeler[i].OrtIsitma > kume6.Kumeler[i - 1].OrtIsitma, "sıra");
    foreach (var k in kume6.Kumeler) { Yakin(k.Uyeler.Average(i => i.IsitmaDG), k.OrtIsitma, 1e-9, k.Ad); Yakin(k.Uyeler.Average(i => i.SogutmaDG), k.OrtSogutma, 1e-9, k.Ad); }
});
Test("Kümeleme: her il kendi küme merkezine en yakın (k-ortalamalar yakınsama koşulu, standart birimde)", () =>
{
    var X = Kumeleme.Standart(v.Iller);
    var merkez = kume6.Kumeler.Select(k => { var idx = k.Uyeler.Select(i => v.Iller.IndexOf(i)).ToList(); return new[] { idx.Average(i => X[i][0]), idx.Average(i => X[i][1]) }; }).ToList();
    for (int i = 0; i < v.Iller.Count; i++)
    {
        int ki = kume6.Kumeler.FindIndex(k => k.Uyeler.Contains(v.Iller[i]));
        double d(int c) => Math.Pow(X[i][0] - merkez[c][0], 2) + Math.Pow(X[i][1] - merkez[c][1], 2);
        Dogru(Enumerable.Range(0, 6).All(c => d(ki) <= d(c) + 1e-12), v.Iller[i].Ad);
    }
});
Test("Kümeleme: k = 2…8'in hepsinde her kümede il merkezi bölgesi kaynakla belli en az bir il var (paket her kümede hesaplanır)", () =>
{
    for (int k = 2; k <= 8; k++)
        foreach (var c in Kumeleme.Yap(v.Iller, sabitK: k).Kumeler)
            Dogru(c.Uyeler.Any(i => i.Ts825Bolge.HasValue), $"k = {k}, {c.Ad}");
});
Test("Küme sayısı veriden: Davies–Bouldin en düşük k = 2 (0,594; scikit-learn ile aynı), siluet de en yüksek k = 2 (0,570); k ≥ 3 arasında ikisi de k = 6 (0,613 / 0,524)", () =>
{
    Dogru(kTarama.K == 2 && kTarama.DaviesBouldin == kTarama.DaviesBouldinK.Values.Min(), $"k = {kTarama.K}");
    Yakin(0.594, Math.Round(kTarama.DaviesBouldin, 3), 0, "Davies–Bouldin (k = 2)");
    Yakin(0.613, Math.Round(kTarama.DaviesBouldinK[6], 3), 0, "Davies–Bouldin (k = 6)");
    Dogru(kTarama.SiluetK[2] == kTarama.SiluetK.Values.Max(), "siluet en yüksek k = 2");
    Dogru(kTarama.DaviesBouldinK.Where(x => x.Key >= 3).OrderBy(x => x.Value).First().Key == 6, "DB k ≥ 3 en iyi");
    Yakin(0.570, Math.Round(kTarama.Siluet, 3), 0, "siluet (k = 2)"); Yakin(0.524, Math.Round(kTarama.SiluetK[6], 3), 0, "siluet (k = 6)");
    Dogru(kTarama.SiluetEnIyiK3 == 6, $"siluet k ≥ 3 en iyi {kTarama.SiluetEnIyiK3}");
});
Test("Kümeleme: siluet −1…1; k taraması 2–8 arası; sabit tohumla tekrarlanabilir; Malatya ve Elazığ aynı kümede, Erzurum ile Antalya ayrı", () =>
{
    Dogru(kTarama.SiluetK.Keys.OrderBy(k => k).SequenceEqual(Enumerable.Range(2, 7)), "k aralığı");
    Dogru(kTarama.SiluetK.Values.All(s => s > -1 && s <= 1) && kTarama.Siluet == kTarama.SiluetK[kTarama.K], "siluet");
    var tekrar = Kumeleme.Yap(v.Iller, sabitK: 6);
    Dogru(tekrar.Kumeler.Select(k => string.Join(",", k.Uyeler.Select(i => i.Ad))).SequenceEqual(kume6.Kumeler.Select(k => string.Join(",", k.Uyeler.Select(i => i.Ad)))), "tekrarlanamıyor");
    Il I(string ad) => v.Iller.First(i => i.Ad == ad);
    Dogru(kume6.KumesiNe(I("Malatya")) == kume6.KumesiNe(I("Elazığ")), "Malatya–Elazığ");
    Dogru(kume6.KumesiNe(I("Erzurum")) != kume6.KumesiNe(I("Antalya")), "Erzurum–Antalya");
});

// ======================================================================
Console.WriteLine("Sınıflandırma (karar ağacı)");
Test("Karar ağacı (CART, Gini): Gini formülü, orta nokta eşiği, saf düğümde durma, eşit sayımda ada göre ilk sınıf, tekrarlanabilirlik", () =>
{
    Yakin(0.5, KararAgaci.Gini(new[] { "a", "b" }), 1e-12, "Gini(a,b)"); Yakin(0, KararAgaci.Gini(new[] { "a", "a" }), 1e-12, "Gini saf");
    Yakin(1 - (16.0 + 1 + 1) / 36, KararAgaci.Gini(new[] { "a", "a", "a", "a", "b", "c" }), 1e-12, "Gini(4a, b, c)");
    double[][] X = { new[] { 1.0, 0 }, new[] { 2.0, 0 }, new[] { 3.0, 1 }, new[] { 4.0, 1 } };
    var a = KararAgaci.Egit(X, new[] { "a", "a", "b", "b" }, 3, new[] { "x", "y" });
    Dogru(!a.Kok.Yaprak && a.Kok.Ozellik == 0 && a.Kok.Esik == 2.5 && a.Derinlik == 1 && a.YaprakSayisi == 2, $"kök {a.Kok.Ozellik} {a.Kok.Esik}");  // eşit kazançta ilk özellik
    Dogru(a.Tahmin(new[] { 2.4, 1.0 }) == "a" && a.Tahmin(new[] { 2.6, 0.0 }) == "b", "tahmin");
    Yakin(1, a.Onem[0], 1e-12, "önem");
    var e = KararAgaci.Egit(new[] { new[] { 1.0 }, new[] { 1.0 } }, new[] { "b", "a" }, 2, new[] { "x" });
    Dogru(e.Kok.Yaprak && e.Kok.Sinif == "a", "eşit sayımda 'a'");
    var d0 = KararAgaci.Egit(X, new[] { "a", "b", "a", "b" }, 0, new[] { "x", "y" });
    Dogru(d0.Kok.Yaprak, "derinlik 0 = tek yaprak");
});
var sinif = Siniflandirma.Yap(v, Varsayilan());
Test("Sınıflandırma: etiket = karar motorunun ilin kendi verisiyle önerisi; 81 il, 4 paket (EPS 8/10/12/15 cm · Üçlü Low-E · Bims)", () =>
{
    Dogru(sinif.Iller.Count == 81 && sinif.Siniflar.SequenceEqual(new[] { "Y2P3D3", "Y3P3D3", "Y7P3D3", "Y8P3D3" }), string.Join(",", sinif.Siniflar));
    Dogru(sinif.Y.Count(x => x == "Y2P3D3") == 33 && sinif.Y.Count(x => x == "Y3P3D3") == 7 && sinif.Y.Count(x => x == "Y7P3D3") == 28 && sinif.Y.Count(x => x == "Y8P3D3") == 13, "sınıf sayıları");
    foreach (var ad in new[] { "Malatya", "Antalya", "Erzurum", "Gaziantep", "Kastamonu", "Çankırı" })
    {
        int i = sinif.Iller.FindIndex(x => x.Ad == ad); var il = sinif.Iller[i];
        var g = Varsayilan(); g.Il = il; g.Bolge = il.Ts825Bolge!.Value;
        Dogru(KararMotoru.Hesapla(v, g).Siralama[0].Kod == sinif.Y[i], ad);
    }
    Dogru(sinif.Y[sinif.Iller.FindIndex(x => x.Ad == "Malatya")] == KararMotoru.Hesapla(v, Varsayilan()).Siralama[0].Kod, "Malatya = teklif ekranı önerisi");
    Yakin(33.0 / 81, sinif.TabanDogruluk, 1e-12, "taban (en sık sınıf)");
});
Test("Sınıflandırma: derinlik LOOCV ile seçilir (1: %74,1, 2: %88,9, 3–6: %97,5 → 3); eğitim %100, taban %40,7; ağaç ısıtma DG 2.054 / 3.064,5 / 2.248 ile bölünür (scikit-learn ile aynı)", () =>
{
    Dogru(sinif.SecilenDerinlik == 3, $"derinlik {sinif.SecilenDerinlik}");
    Yakin(60.0 / 81, sinif.LoocvDogruluk[1], 1e-12, "d=1"); Yakin(72.0 / 81, sinif.LoocvDogruluk[2], 1e-12, "d=2");
    for (int d = 3; d <= 6; d++) Yakin(79.0 / 81, sinif.LoocvDogruluk[d], 1e-12, $"d={d}");
    Yakin(1, sinif.EgitimDogruluk, 1e-12, "eğitim");
    var k = sinif.Agac.Kok;
    Dogru(k.Ozellik == 0 && k.Esik == 2054 && k.Sol!.Yaprak && k.Sol.Sinif == "Y2P3D3" && k.Sag!.Ozellik == 0 && k.Sag.Esik == 3064.5
          && k.Sag.Sol!.Esik == 2248 && k.Sag.Sol.Sol!.Sinif == "Y3P3D3" && k.Sag.Sol.Sag!.Sinif == "Y7P3D3" && k.Sag.Sag!.Sinif == "Y8P3D3", "ağaç yapısı");
    var hatali = Enumerable.Range(0, 81).Where(i => sinif.LoocvTahmin[i] != sinif.Y[i]).Select(i => sinif.Iller[i].Ad).OrderBy(x => x).ToList();
    Dogru(hatali.SequenceEqual(new[] { "Gaziantep", "Kastamonu" }), string.Join(",", hatali));
    var m = Siniflandirma.Karmasiklik(sinif); int top = 0, kosegen = 0;
    for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) { top += m[a, b]; if (a == b) kosegen += m[a, b]; }
    Dogru(top == 81 && kosegen == 79, $"matris {top} / {kosegen}");
    var d2 = Siniflandirma.Derinlikle(sinif, 2);
    Yakin(sinif.LoocvDogruluk[2], d2.Loocv, 1e-12, "Derinlikle(2) = tarama"); Dogru(d2.Agac.Derinlik <= 2 && d2.Y == sinif.Y, "yeniden eğitimde etiket aynı");
    var mal = sinif.Iller.First(x => x.Ad == "Malatya");
    Dogru(string.Join(" ve ", sinif.Agac.YolOzet(Siniflandirma.Ozellik(mal), (j, e) => e.ToString("N0", Ortak.TR))) == "2.248 < Isıtma DG ≤ 3.065", "Malatya kuralı");
});

// ======================================================================
Console.WriteLine("Duyarlılık");
Test("Duyarlılık dilimleri aralığı sıralı kapsar, ardışık dilimlerin önerisi farklıdır", () =>
{
    foreach (var d in new[] { Duyarlilik.GazFiyati(v, Varsayilan()), Duyarlilik.MalzemeFiyati(v, Varsayilan()), Duyarlilik.AnalizSuresi(v, Varsayilan()) })
    {
        Dogru(d.Count > 0, "boş");
        for (int i = 1; i < d.Count; i++) Dogru(d[i].Bas > d[i - 1].Son && d[i].Oneri.Kod != d[i - 1].Oneri.Kod, "dilimler sırasız ya da aynı öneri iki dilimde");
    }
});

// ======================================================================
Console.WriteLine("Kullanım kolaylığı: gerekçe ve Excel");
Test("\"Neden bu paket?\" gerekçesi: önerilen, ekonomik, yüksek performans ve özel seçim için doğru ölçütü söyler", () =>
{
    var r = KararMotoru.Hesapla(v, Varsayilan());
    var l = r.Siralama; var oneri = l[0];
    var eko = l.OrderBy(s => s.IlkYatirim).ThenByDescending(s => s.Skor).First();
    var pre = l.OrderBy(s => s.YillikIsitmaKWh).ThenBy(s => s.IlkYatirim).First();
    string g1 = TeklifRaporu.Gerekce(oneri, l, r, oneri, eko, pre, "Dengeli");
    Dogru(g1.StartsWith($"Dengeli önceliğinde {l.Count} uygun seçenek arasında en yüksek puan") && g1.Contains(TeklifRaporu.PaketAdi(l[1])), g1);
    if (eko.Kod != oneri.Kod) Dogru(TeklifRaporu.Gerekce(eko, l, r, oneri, eko, pre, "Dengeli").StartsWith("İlk yatırımı en düşük uygun paket"), "ekonomik");
    if (pre.Kod != oneri.Kod) Dogru(TeklifRaporu.Gerekce(pre, l, r, oneri, eko, pre, "Dengeli").StartsWith("Isıtması en düşük uygun paket"), "yüksek performans");
    var diger = l.First(x => x.Kod != oneri.Kod && x.Kod != eko.Kod && x.Kod != pre.Kod);
    Dogru(TeklifRaporu.Gerekce(diger, l, r, oneri, eko, pre, "Dengeli").Contains($"{l.IndexOf(diger) + 1}. sırada"), "özel seçim");
});
Test("Excel'e aktarma: geçerli .xlsx (3 sayfa), keşif tutarları ve tüm seçenekler sayı olarak yazılır", () =>
{
    var r = KararMotoru.Hesapla(v, Varsayilan()); var s = r.Siralama[0];
    var eko = r.Siralama.OrderBy(x => x.IlkYatirim).First();
    var bilgi = new TeklifRaporu.Bilgi { Proje = "Tip 1", Paket = "Önerilen paket", Oncelik = "Dengeli", Tarife = "BOTAŞ", Gerekce = "g", Ts825 = "✓ <uygun> & \"tamam\"", Yil = 20 };
    var sayfalar = TeklifRaporu.Excel(v, s, r, eko, bilgi, r.Siralama.Select(x => (x, (double?)x.Skor)).ToList());
    string yol = Path.Combine(Path.GetTempPath(), $"teklif_test_{Guid.NewGuid():N}.xlsx");
    XlsxYazici.Yaz(yol, sayfalar);
    using (var z = System.IO.Compression.ZipFile.OpenRead(yol))
    {
        var adlar = z.Entries.Select(e => e.FullName).ToHashSet();
        foreach (var a in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml", "xl/worksheets/sheet3.xml" })
            Dogru(adlar.Contains(a), a);
        foreach (var e in z.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels")))
            using (var st = e.Open()) System.Xml.Linq.XDocument.Load(st);          // her parça geçerli XML
        string Oku(string ad) { using var st = z.GetEntry(ad)!.Open(); using var rd = new StreamReader(st); return rd.ReadToEnd(); }
        string t = Oku("xl/worksheets/sheet1.xml");
        Dogru(t.Contains("Genel toplam (KDV dahil)") && t.Contains("<v>" + Math.Round(s.IlkYatirim * (1 + v.P("KDV")), 2).ToString("R", CultureInfo.InvariantCulture) + "</v>"), "genel toplam");
        Dogru(t.Contains("&lt;uygun&gt; &amp; &quot;tamam&quot;"), "XML kaçışı");
        Dogru(Oku("xl/worksheets/sheet2.xml").Split("<row ").Length - 1 == r.Siralama.Count + 1, "tüm seçenekler satır sayısı");
        Dogru(Oku("xl/workbook.xml").Contains("name=\"Tüm seçenekler\""), "sayfa adı");
    }
    File.Delete(yol);
    Dogru(XlsxYazici.SutunAdi(0) == "A" && XlsxYazici.SutunAdi(25) == "Z" && XlsxYazici.SutunAdi(26) == "AA" && XlsxYazici.SutunAdi(701) == "ZZ", "sütun adları");
});

Console.WriteLine("Veritabanı (SQLite)");
string tmp = Path.Combine(Path.GetTempPath(), "ecobuildss_test_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tmp);
foreach (var f in Directory.GetFiles(veriKlasoru, "*.csv")) File.Copy(f, Path.Combine(tmp, Path.GetFileName(f)));
var db = new Veritabani(Path.Combine(tmp, "ecobuildss.db"));
VeriDeposu? vd = null;
Test("İlk açılışta veritabanı CSV'den oluşturulur; tablo satır sayıları CSV ile aynı", () =>
{
    vd = db.HazirlaVeYukle(tmp);
    var say = db.SatirSayilari();
    Dogru(say["bina_tipleri"] == 12 && say["malzemeler"] == 15 && say["cati_zemin"] == 3 && say["ts825_pencere"] == 26 && say["yakitlar"] == 4 && say["yangin_siniflari"] == 2 && say["parametreler"] == v.Parametre.Count && say["teklifler"] == 0
          && say["iller"] == 81 && say["il_bolgeleri"] == 81 && say["ts825_bolgeler"] == 6 && say["levha_fiyatlari"] == v.Levhalar.Count,
          string.Join(", ", say.Select(k => $"{k.Key}={k.Value}")));
    Dogru(vd.Kaynak == "SQLite veritabanı", vd.Kaynak);
});
Test("Veritabanından okunan veri CSV ile birebir aynı (malzeme alanları, parametreler, formlar)", () =>
{
    Dogru(vd != null, "veritabanı açılmadı");
    foreach (var m in v.Malzemeler)
    {
        var x = vd!.Malzemeler.First(y => y.Id == m.Id);
        Dogru(x.Ad == m.Ad && x.Kategori == m.Kategori && x.FiyatKaynagi == m.FiyatKaynagi && x.FiyatTarihi == m.FiyatTarihi && x.FiyatHesabi == m.FiyatHesabi, $"{m.Id} metin alanı");
        Dogru(x.Kalinlik == m.Kalinlik && x.Lambda == m.Lambda && x.U == m.U && x.KarbonM2 == m.KarbonM2 && x.FiyatM2 == m.FiyatM2 && x.BirimAgirlik == m.BirimAgirlik, $"{m.Id} sayı alanı");
    }
    foreach (var kv in v.Parametre) Dogru(vd!.Parametre[kv.Key] == kv.Value, kv.Key);
    Dogru(vd!.Iller.Zip(v.Iller).All(p => p.First.Ad == p.Second.Ad && p.First.IsitmaDG == p.Second.IsitmaDG && p.First.SogutmaDG == p.Second.SogutmaDG && p.First.Ts825Bolge == p.Second.Ts825Bolge), "iller");
    Dogru(vd.Bolgeler.Zip(v.Bolgeler).All(p => p.First.No == p.Second.No && p.First.UDuvar == p.Second.UDuvar && p.First.UCati == p.Second.UCati), "bölgeler");
    Dogru(vd.Levhalar.Zip(v.Levhalar).All(p => p.First.KalinlikCm == p.Second.KalinlikCm && p.First.FiyatM2 == p.Second.FiyatM2), "levhalar");
    Dogru(vd!.Yakitlar.Zip(v.Yakitlar).All(p => p.First.Id == p.Second.Id && p.First.FiyatBirim == p.Second.FiyatBirim && p.First.Verim == p.Second.Verim && p.First.IsilDegerKWh == p.Second.IsilDegerKWh && p.First.EmisyonKgKWh == p.Second.EmisyonKgKWh) && vd.Yakitlar.Count == 4, "yakıtlar");
    Dogru(vd!.YanginSiniflari.Count == 2 && vd.Yangin("EPS")!.Sistem == v.Yangin("EPS")!.Sistem, "yangın sınıfları");
    Dogru(vd!.PencereDegerleri.Count == v.PencereDegerleri.Count, "pencere değerleri");
    Dogru(vd!.Formlar.Count == 12 && vd.Formlar.Zip(v.Formlar).All(p => p.First.CatiAlani == p.Second.CatiAlani && p.First.DuvarAlani == p.Second.DuvarAlani), "formlar");
});
Test("Karar sonucu veritabanı verisiyle ve CSV verisiyle aynı", () =>
{
    var a = KararMotoru.Hesapla(v, Varsayilan());
    var b = KararMotoru.Hesapla(vd!, Varsayilan(kaynak: vd));
    Dogru(a.Siralama.Select(s => s.Kod).SequenceEqual(b.Siralama.Select(s => s.Kod)), "sıralama farklı");
    Yakin(a.Siralama[0].ToplamMaliyet, b.Siralama[0].ToplamMaliyet, 1e-9, "ömür boyu maliyet");
});
Test("Teklif kaydet → listele → sil", () =>
{
    var k = new KayitliTeklif { Tarih = new DateTime(2026, 11, 5, 10, 30, 0), ProjeAdi = "Deneme, \"tırnak\" ve virgül", Paket = "Önerilen paket", BinaOzeti = "Tip 1",
        FormNo = 0, En = 12.5, Boy = 15, Kat = 3, Yon = 3, CamOrani = 0.4, Yil = 25, Tarife = 1, Ts825Filtre = true, Oncelik = 2, Tb1 = 2, Tb2 = 2, Tb3 = 0,
        YalitimId = "Y5", CamId = "P3", DuvarId = "D2", IlkYatirim = 1234567.89, KdvDahil = 1481481.468, YillikIsitmaKWh = 20372.1, YillikGiderTL = 23638.2,
        OmurBoyuMaliyet = 771041.3, Co2Ton = 103.04, Ts825Uygun = true, Il = "Elazığ", Bolge = 4, Dograma = "PVC Doğrama (6 Odacıklı)", Mevcut = true, Yakit = "KOMUR", Sogutma = true, EnerjiArtisi = 0.025 };
    long id = db.TeklifKaydet(k);
    long id2 = db.TeklifKaydet(new KayitliTeklif { Tarih = DateTime.Now, ProjeAdi = "İkinci", YalitimId = "Y1", CamId = "P2", DuvarId = "D1" });
    var l = db.Teklifler();
    Dogru(l.Count == 2 && l[0].Id == id2, "sıralama (en yeni üstte)");
    var o = l.First(x => x.Id == id);
    Dogru(o.ProjeAdi == k.ProjeAdi && o.Tarih == k.Tarih && o.En == 12.5 && o.CamOrani == 0.4 && o.Kat == 3 && o.Yon == 3 && o.Tarife == 1 && o.Ts825Filtre && o.Oncelik == 2
          && o.YalitimId == "Y5" && o.IlkYatirim == 1234567.89 && o.Co2Ton == 103.04 && o.Ts825Uygun && o.Il == "Elazığ" && o.Bolge == 4 && o.Dograma == k.Dograma && o.Mevcut && o.Yakit == "KOMUR" && o.Sogutma && o.EnerjiArtisi == 0.025, "alanlar aynı okunmadı");
    db.TeklifSil(id2);
    Dogru(db.Teklifler().Count == 1, "silinmedi");
});
Test("CSV değişirse referans tablolar yeniden yüklenir, kayıtlı teklifler korunur", () =>
{
    string yol = Path.Combine(tmp, "malzemeler.csv");
    File.WriteAllText(yol, File.ReadAllText(yol).Replace(",725,", ",726,"), new System.Text.UTF8Encoding(true));
    File.SetLastWriteTimeUtc(yol, DateTime.UtcNow.AddMinutes(1));
    var yeni = db.HazirlaVeYukle(tmp);
    Yakin(726, yeni.Malzemeler.First(m => m.Id == "Y1").FiyatM2, 0, "yeni fiyat");
    Dogru(db.Teklifler().Count == 1, "teklif kayboldu");
});
Test("Önceki sürümün veritabanı (il / bölge / doğrama sütunu olmayan teklifler tablosu) açılınca sütunlar eklenir, kayıt korunur", () =>
{
    string eskiYol = Path.Combine(tmp, "eski.db");
    var eski = new Veritabani(eskiYol);
    // eski şema: teklifler tablosunda il ve bölge yok
    using (var c = SqliteBaglanti.Ac(eskiYol))
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "CREATE TABLE teklifler (id INTEGER PRIMARY KEY AUTOINCREMENT, tarih TEXT NOT NULL, proje_adi TEXT, paket TEXT, bina_ozeti TEXT, form_no INTEGER, en REAL, boy REAL, kat INTEGER, yon INTEGER, cam_orani REAL, yil INTEGER, tarife INTEGER, ts825_filtre INTEGER, oncelik INTEGER, tb1 INTEGER, tb2 INTEGER, tb3 INTEGER, yalitim_id TEXT, cam_id TEXT, duvar_id TEXT, ilk_yatirim REAL, kdv_dahil REAL, yillik_isitma_kwh REAL, yillik_gider_tl REAL, omur_boyu_maliyet REAL, co2_ton REAL, ts825_uygun INTEGER);" +
                          "INSERT INTO teklifler (tarih, proje_adi, yalitim_id, cam_id, duvar_id) VALUES ('2026-11-04 12:00:00', 'Eski kayıt', 'Y3', 'P3', 'D3');";
        cmd.ExecuteNonQuery();
    }
    eski.HazirlaVeYukle(tmp);
    var l = eski.Teklifler();
    Dogru(l.Count == 1 && l[0].ProjeAdi == "Eski kayıt" && l[0].Il == "", "eski kayıt okunamadı");
    eski.TeklifKaydet(new KayitliTeklif { Tarih = DateTime.Now, ProjeAdi = "Yeni", Il = "Van", Bolge = 6, Dograma = "Ahşap Doğrama (90s)", YalitimId = "Y3", CamId = "P3", DuvarId = "D3" });
    Dogru(eski.Teklifler().First().Il == "Van" && eski.Teklifler().First().Dograma == "Ahşap Doğrama (90s)", "yeni sütun yazılamadı");
});
Test("İşlem günlüğü: veri yüklemesi kendiliğinden yazılır; Bilgi / Uyarı / Hata kayıtları tarih, kullanıcı ve bilgisayarla saklanır; yalnız veritabanında", () =>
{
    var gdb = new Veritabani(Path.Combine(tmp, "gunluk.db"));
    gdb.HazirlaVeYukle(tmp);
    var ilk = gdb.IslemKayitlari();
    Dogru(ilk.Count == 1 && ilk[0].islem == "Veri yüklendi" && ilk[0].tur == "Bilgi", string.Join(" | ", ilk.Select(x => x.islem)));
    Gunluk.Db = gdb;
    Gunluk.Bilgi("Teklif kaydedildi", "1 · Deneme");
    Gunluk.Uyari("Yazıcı bulunamadı", "yok");
    Gunluk.Hata("Beklenmeyen hata", new InvalidOperationException("deneme hatası"));
    var l = gdb.IslemKayitlari();
    Dogru(l.Count == 4 && l[0].tur == "Hata" && l[0].ayrinti.Contains("deneme hatası") && l[1].tur == "Uyarı" && l[2].islem == "Teklif kaydedildi", string.Join(" | ", l.Select(x => x.tur + ":" + x.islem)));
    Dogru(l.All(x => (DateTime.Now - x.tarih).TotalMinutes < 5), "tarih");
    gdb.Kaydet("Geçersiz", "tür kısıtı");   // CHECK kısıtı: kayıt reddedilir, uygulama durmaz
    Dogru(gdb.IslemKayitlari().Count == 4, "geçersiz tür yazıldı");
    gdb.HazirlaVeYukle(tmp);               // CSV değişmedi: yeniden yükleme kaydı yok
    Dogru(gdb.IslemKayitlari().Count == 4 && gdb.SatirSayilari()["islem_kayitlari"] == 4, "gereksiz kayıt");
    Gunluk.Db = null; Gunluk.Bilgi("yazılmaz");   // veritabanı yoksa sessizce atlanır
});
try { Directory.Delete(tmp, true); } catch { }

Console.WriteLine();
Console.WriteLine($"Sonuç: {gecen} test geçti, {kalan} test kaldı.");
foreach (var h in hatalar) Console.WriteLine("  - " + h);
return kalan == 0 ? 0 : 1;
