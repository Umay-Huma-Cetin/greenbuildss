using System;
using System.Collections.Generic;
using System.Linq;
using EcoBuildss.Core.Ogrenme;
using EcoBuildss.Core.Veri;

namespace EcoBuildss.Core.Uygulamalar
{
    /// <summary>Sınıflandırıcıya giren tek bir teklifin özellikleri.</summary>
    public sealed class TeklifOzellik
    {
        public string Il = "", MusteriTipi = "Bireysel", ProjeTuru = "Yeni bina", Paket = "Önerilen", Oncelik = "Dengeli";
        public double KullanimAlani, TeklifTLm2, IndirimOrani, YanitSuresiGun;
        public double? GeriOdemeYil;
        public bool RakipTeklif;

        public static TeklifOzellik Kayittan(Kayit k) => new TeklifOzellik
        {
            Il = k.Metin("il"), MusteriTipi = k.Metin("musteri_tipi"), ProjeTuru = k.Metin("proje_turu"), Paket = k.Metin("paket"), Oncelik = k.Metin("oncelik"),
            KullanimAlani = k.Sayi("kullanim_alani_m2"), TeklifTLm2 = k.Sayi("teklif_TL_m2"), IndirimOrani = k.Sayi("indirim_orani"),
            YanitSuresiGun = k.Sayi("yanit_suresi_gun"), GeriOdemeYil = k.SayiVeyaBos("geri_odeme_yil"), RakipTeklif = k.Metin("rakip_teklif") == "Evet",
        };

        /// <summary>Müşterinin önceliği ile teklif edilen paket örtüşüyor mu (bütçe → ekonomik, dengeli → önerilen, enerji → yüksek performans).</summary>
        public bool OncelikUyumu =>
            Oncelik == "Bütçe" && Paket == "Ekonomik" || Oncelik == "Dengeli" && Paket == "Önerilen" || Oncelik == "Enerji ve karbon" && Paket == "Yüksek performans";
    }

    /// <summary>Teklif özelliklerinin sayısal vektöre çevrilmesi (kategorik → 0/1 kukla değişken).</summary>
    public static class OzellikVektoru
    {
        public const double GeriOdemeTavan = 40;   // geri ödemeyen teklif 40 yıl sayılır (analiz ufkunun ötesi)
        public static readonly string[] Adlar =
        {
            "Fiyat endeksi", "Geri ödeme (yıl)", "İndirim oranı", "Yanıt süresi (gün)", "Rakip teklif var", "Yenileme projesi",
            "Kullanım alanı (log m²)", "Öncelik–paket uyumu", "Paket: ekonomik", "Paket: yüksek performans", "Paket: özel",
            "Müşteri: site yönetimi", "Müşteri: müteahhit", "Müşteri: kurum", "Isıtma derece-günü",
        };

        /// <summary>Fiyat endeksi: teklifin m² fiyatı ÷ aynı proje türündeki eğitim tekliflerinin medyan m² fiyatı (müşteri fiyatı piyasaya göre değerlendirir).</summary>
        public static double[] Vektor(TeklifOzellik t, VeriDeposu v, IReadOnlyDictionary<string, double> medyan)
        {
            double med = medyan.TryGetValue(t.ProjeTuru, out var m) && m > 0 ? m : medyan.Values.DefaultIfEmpty(t.TeklifTLm2).Average();
            double dg = v.Iller.FirstOrDefault(i => i.Ad == t.Il)?.IsitmaDG ?? v.VarsayilanIl.IsitmaDG;
            return new[]
            {
                t.TeklifTLm2 / med, Math.Min(GeriOdemeTavan, t.GeriOdemeYil ?? GeriOdemeTavan), t.IndirimOrani, t.YanitSuresiGun,
                t.RakipTeklif ? 1.0 : 0, t.ProjeTuru == "Yenileme" ? 1.0 : 0, Math.Log(t.KullanimAlani), t.OncelikUyumu ? 1.0 : 0,
                t.Paket == "Ekonomik" ? 1.0 : 0, t.Paket == "Yüksek performans" ? 1.0 : 0, t.Paket == "Özel" ? 1.0 : 0,
                t.MusteriTipi == "Site yönetimi" ? 1.0 : 0, t.MusteriTipi == "Müteahhit" ? 1.0 : 0, t.MusteriTipi == "Kurum" ? 1.0 : 0, dg,
            };
        }
    }

    public sealed class AdayDegerlendirmesi
    {
        public string Ad = "";
        public double F1Ort, F1Std, AucOrt, DogrulukOrt;
    }

    public sealed class KabulModeliSonucu
    {
        public List<AdayDegerlendirmesi> Adaylar = new();
        public string SecilenModel = "";
        public double Esik = 0.5;                          // F1'i en yükselten karar eşiği (yalnız eğitim verisinden)
        public SiniflandirmaMetrikleri Test = new();       // hiç görülmemiş test kümesinde
        public int EgitimN, TestN, PozitifN, NegatifN;
        public double[] Onem = Array.Empty<double>();
        public List<string> Kurallar = new();              // açıklanabilirlik için sığ karar ağacının kuralları
        public ISiniflandirici Model = null!;
        public const double F1Esigi = 0.87, AucEsigi = 0.80;
        /// <summary>Her teklife "kabul" diyen saf tahminin F1'i: modelin bunu açıkça geçmesi gerekir.</summary>
        public double TabanF1 => Test.N == 0 ? 0 : 2.0 * (Test.DP + Test.YN) / (Test.N + Test.DP + Test.YN);
        public const int MinKayit = 100, MinSinif = 20;
        public bool YeterliVeri => PozitifN >= MinSinif && NegatifN >= MinSinif && EgitimN + TestN >= MinKayit;
        /// <summary>Model ancak yeterli veriyle eğitildiyse ve görülmemiş test verisinde F1 ≥ 0,87, AUC ≥ 0,80 ve "hep kabul" tahmininden belirgin iyiyse gösterilir.</summary>
        public bool Guvenilir => YeterliVeri && Test.F1 >= F1Esigi && Test.Auc >= AucEsigi && Test.F1 > TabanF1 + 0.05;
        public string Yorum => !YeterliVeri
            ? $"Yetersiz veri: en az {MinKayit} teklif ve her sonuçtan en az {MinSinif} kayıt gerekir (kabul {PozitifN}, red {NegatifN})."
            : Guvenilir ? $"{SecilenModel}: test F1 {Test.F1:0.000} (eşik {F1Esigi:0.00}), AUC {Test.Auc:0.000}; teklif ekranında kabul olasılığı gösterilir."
                        : $"{SecilenModel}: test F1 {Test.F1:0.000}, AUC {Test.Auc:0.000} (eşikler F1 {F1Esigi:0.00}, AUC {AucEsigi:0.00}; \"hep kabul\" tahmini F1 {TabanF1:0.000}). Model güvenilir değil, teklif ekranında gösterilmez; daha çok ve daha temiz kayıt gerekir.";

        public Dictionary<string, double> MedyanTLm2 = new();   // eğitim verisinden, proje türüne göre
        public double Olasilik(TeklifOzellik t, VeriDeposu v) => Model.Olasilik(OzellikVektoru.Vektor(t, v, MedyanTLm2));
    }

    /// <summary>
    /// Teklif kabul tahmini. Üç aday model (lojistik regresyon, karar ağacı, rastgele orman) yalnız eğitim kümesinde
    /// 5 katlı tabakalı çapraz doğrulamayla karşılaştırılır; F1'i en yüksek olan seçilir. Karar eşiği de eğitim kümesindeki
    /// kat dışı tahminlerden bulunur. Son başarı, seçimde hiç kullanılmamış %25'lik test kümesinde bir kez ölçülür.
    /// </summary>
    public static class KabulTahmini
    {
        public static IEnumerable<ISiniflandirici> Adaylar()
        {
            yield return new LojistikRegresyon { Lambda = 1 };
            yield return new KararAgaci { MaksDerinlik = 5, MinYaprak = 8 };
            yield return new RastgeleOrman { AgacSayisi = 150, MaksDerinlik = 8, MinYaprak = 3 };
        }

        public static KabulModeliSonucu Egit(VeriDeposu v, IReadOnlyList<Kayit> teklifler, int tohum = 42)
        {
            var ozellik = teklifler.Select(TeklifOzellik.Kayittan).ToArray();
            var y = teklifler.Select(k => k.Metin("sonuc") == "Kabul" ? 1 : 0).ToArray();
            var s = new KabulModeliSonucu { PozitifN = y.Count(v_ => v_ == 1), NegatifN = y.Count(v_ => v_ == 0) };
            if (s.PozitifN < 5 || s.NegatifN < 5) { s.EgitimN = y.Length; return s; }

            var (tr, te) = Katlar.EgitimTest(y, 0.25, tohum);
            // Medyanlar yalnız eğitim kümesinden (test kümesi bilgisi modele sızmaz)
            s.MedyanTLm2 = tr.GroupBy(i => ozellik[i].ProjeTuru).ToDictionary(g => g.Key, g => Mat.Kuantil(g.Select(i => ozellik[i].TeklifTLm2), 0.5));
            var x = ozellik.Select(o => OzellikVektoru.Vektor(o, v, s.MedyanTLm2)).ToArray();
            s.EgitimN = tr.Length; s.TestN = te.Length;
            var xtr = tr.Select(i => x[i]).ToArray(); var ytr = tr.Select(i => y[i]).ToArray();
            var katlar = Katlar.Tabakali(ytr, 5, tohum);

            ISiniflandirici? enIyi = null; double enF1 = -1; double[] enOof = Array.Empty<double>();
            foreach (var aday in Adaylar())
            {
                var f1 = new List<double>(); var auc = new List<double>(); var acc = new List<double>();
                var oof = new double[ytr.Length];
                foreach (var test in katlar)
                {
                    var eg = Enumerable.Range(0, ytr.Length).Except(test).ToArray();
                    var m = aday.YeniKopya();
                    m.Egit(eg.Select(i => xtr[i]).ToArray(), eg.Select(i => ytr[i]).ToArray());
                    var p = test.Select(i => m.Olasilik(xtr[i])).ToArray();
                    foreach (var (i, pi) in test.Zip(p, (i, pi) => (i, pi))) oof[i] = pi;
                    var mt = SiniflandirmaMetrikleri.Hesapla(test.Select(i => ytr[i]).ToArray(), p);
                    f1.Add(mt.F1); auc.Add(mt.Auc); acc.Add(mt.Dogruluk);
                }
                s.Adaylar.Add(new AdayDegerlendirmesi { Ad = aday.Ad, F1Ort = f1.Average(), F1Std = Mat.StdSapma(f1), AucOrt = auc.Average(), DogrulukOrt = acc.Average() });
                if (f1.Average() > enF1) { enF1 = f1.Average(); enIyi = aday; enOof = oof; }
            }

            // Karar eşiği: eğitim kümesinin kat dışı tahminlerinde F1'i en yükselten eşik (0,30 … 0,70)
            double enEsik = 0.5, enEsikF1 = -1;
            for (double e = 0.30; e <= 0.701; e += 0.01)
            {
                double f = SiniflandirmaMetrikleri.Hesapla(ytr, enOof, e).F1;
                if (f > enEsikF1 + 1e-12) { enEsikF1 = f; enEsik = e; }
            }
            s.Esik = Math.Round(enEsik, 2);

            s.Model = enIyi!.YeniKopya();
            s.Model.Egit(xtr, ytr);
            s.SecilenModel = s.Model.Ad;
            s.Test = SiniflandirmaMetrikleri.Hesapla(te.Select(i => y[i]).ToArray(), te.Select(i => s.Model.Olasilik(x[i])).ToArray(), s.Esik);
            s.Onem = s.Model.Onem ?? new double[OzellikVektoru.Adlar.Length];

            // Açıklanabilirlik: derinliği 3 olan bir ağaç, satış ekibinin okuyabileceği kurallar üretir
            var aciklayici = new KararAgaci { MaksDerinlik = 3, MinYaprak = 15 };
            aciklayici.Egit(xtr, ytr);
            s.Kurallar = aciklayici.Kurallar(OzellikVektoru.Adlar, (j, e) => OzellikVektoru.Adlar[j].StartsWith("Paket") || OzellikVektoru.Adlar[j].StartsWith("Müşteri")
                || OzellikVektoru.Adlar[j].StartsWith("Rakip") || OzellikVektoru.Adlar[j].StartsWith("Yenileme") || OzellikVektoru.Adlar[j].StartsWith("Öncelik")
                ? "0 (hayır)" : OzellikVektoru.Adlar[j].StartsWith("Kullanım") ? Math.Exp(e).ToString("N0", Ortak.TR) + " m²"
                : OzellikVektoru.Adlar[j].StartsWith("İndirim") ? "%" + (100 * e).ToString("0.#", Ortak.TR) : e.ToString("N2", Ortak.TR));
            return s;
        }
    }
}
