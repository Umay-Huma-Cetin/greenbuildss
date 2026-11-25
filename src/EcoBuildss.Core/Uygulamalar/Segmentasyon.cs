using System;
using System.Collections.Generic;
using System.Linq;
using EcoBuildss.Core.Ogrenme;
using EcoBuildss.Core.Veri;

namespace EcoBuildss.Core.Uygulamalar
{
    public sealed class Segment
    {
        public int No;
        public List<int> Uyeler = new();       // kaynak listesindeki sıra
        public Dictionary<string, double> Ortalama = new();
        public string Ozet = "";
    }

    public sealed class SegmentasyonSonucu
    {
        public string Ad = "";
        public string[] Degiskenler = Array.Empty<string>();
        public int K;
        public Dictionary<int, (double db, double siluet)> KTablosu = new();
        public KOrtalamaSonucu Kume = null!;
        public List<Segment> Segmentler = new();
        public double[][] HamVeri = Array.Empty<double[]>();   // çizim için gerçek birimde değişkenler
        public string Yorum => $"k = {K} (Davies–Bouldin {Kume.DaviesBouldin:0.000}, siluet {Kume.Siluet:0.000}); " +
                               (Kume.Siluet >= 0.5 ? "kümeler belirgin." : Kume.Siluet >= 0.25 ? "kümeler makul ayrışıyor." : "kümeler zayıf ayrışıyor; segmentler dikkatle yorumlanmalı.");
    }

    /// <summary>
    /// Kümeleme (k-ortalamalar++). Değişkenler z-skorla ölçeklenir; k, 2…kMax arasında Davies–Bouldin endeksi en düşük olanla seçilir
    /// (siluet katsayısı ikinci ölçüt olarak raporlanır). İki kullanım:
    ///  • İl iklim bölgeleri (MGM ısıtma ve soğutma derece-günü) → bölgeye standart paket ve stok planı
    ///  • Müşteri segmentleri (teklif geçmişi) → hangi müşteri grubuna nasıl teklif verileceği
    /// </summary>
    public static class Segmentasyon
    {
        public static SegmentasyonSonucu Calistir(string ad, string[] degiskenler, double[][] veri, int? sabitK = null, int kMax = 8, int tohum = 42)
        {
            if (veri.Length < 10) throw new InvalidOperationException("Kümeleme için en az 10 kayıt gerekir.");
            var o = Olcekleyici.Ogren(veri);
            var z = o.Donustur(veri);
            var s = new SegmentasyonSonucu { Ad = ad, Degiskenler = degiskenler, HamVeri = veri };
            KOrtalamaSonucu? en = null;
            for (int k = 2; k <= Math.Min(kMax, veri.Length - 1); k++)
            {
                var r = KOrtalamalar.Calistir(z, k, 30, tohum);
                s.KTablosu[k] = (r.DaviesBouldin, r.Siluet);
                if (sabitK == k || sabitK == null && (en == null || r.DaviesBouldin < en.DaviesBouldin)) en = r;
            }
            s.Kume = en!; s.K = en!.K;
            // Kümeleri ilk değişkene göre küçükten büyüğe numaralandır (yorum kolaylığı)
            var sira = Enumerable.Range(0, s.K).OrderBy(c => en.Merkez[c][0]).ToArray();
            for (int n = 0; n < s.K; n++)
            {
                int c = sira[n];
                var seg = new Segment { No = n + 1, Uyeler = Enumerable.Range(0, veri.Length).Where(i => en.Etiket[i] == c).ToList() };
                for (int j = 0; j < degiskenler.Length; j++) seg.Ortalama[degiskenler[j]] = seg.Uyeler.Average(i => veri[i][j]);
                s.Segmentler.Add(seg);
            }
            return s;
        }

        public static SegmentasyonSonucu Iller(VeriDeposu v, int? k = null)
        {
            var veri = v.Iller.Select(i => new[] { i.IsitmaDG, i.SogutmaDG }).ToArray();
            var s = Calistir("İl iklim bölgeleri", new[] { "Isıtma derece-günü", "Soğutma derece-günü" }, veri, k);
            foreach (var seg in s.Segmentler)
                seg.Ozet = $"{seg.Uyeler.Count} il · ısıtma {seg.Ortalama["Isıtma derece-günü"]:N0} °C·gün · soğutma {seg.Ortalama["Soğutma derece-günü"]:N0} °C·gün · "
                         + string.Join(", ", seg.Uyeler.Take(5).Select(i => v.Iller[i].Ad)) + (seg.Uyeler.Count > 5 ? " …" : "");
            return s;
        }

        public static readonly string[] MusteriDegiskenleri = { "Kullanım alanı (log m²)", "Teklif (TL/m²)", "Geri ödeme (yıl)", "İndirim oranı" };

        public static SegmentasyonSonucu Musteriler(IReadOnlyList<Kayit> teklifler, int? k = null)
        {
            var veri = teklifler.Select(t => new[]
            {
                Math.Log(t.Sayi("kullanim_alani_m2")), t.Sayi("teklif_TL_m2"),
                Math.Min(OzellikVektoru.GeriOdemeTavan, t.SayiVeyaBos("geri_odeme_yil") ?? OzellikVektoru.GeriOdemeTavan), t.Sayi("indirim_orani"),
            }).ToArray();
            var s = Calistir("Müşteri segmentleri", MusteriDegiskenleri, veri, k, 6);
            foreach (var seg in s.Segmentler)
            {
                var u = seg.Uyeler.Select(i => teklifler[i]).ToList();
                string Baskin(string alan) => u.GroupBy(t => t.Metin(alan)).OrderByDescending(g => g.Count()).First().Key;
                double kabul = u.Count(t => t.Metin("sonuc") == "Kabul") / (double)u.Count;
                seg.Ozet = $"{u.Count} teklif · ort. {Math.Exp(seg.Ortalama[MusteriDegiskenleri[0]]):N0} m² · {seg.Ortalama[MusteriDegiskenleri[1]]:N0} TL/m² · "
                         + $"geri ödeme {seg.Ortalama[MusteriDegiskenleri[2]]:0.0} yıl · kabul %{100 * kabul:0} · çoğunluk: {Baskin("musteri_tipi")}, {Baskin("oncelik")}";
            }
            return s;
        }
    }
}
