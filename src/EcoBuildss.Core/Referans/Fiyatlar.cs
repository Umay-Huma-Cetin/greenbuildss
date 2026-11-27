using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core
{
    /// <summary>Fiyatı güncellenebilen bir kalem: malzeme (TL/m²), yakıt (TL/birim) ya da enerji tarifesi.</summary>
    public sealed class FiyatKalemi
    {
        public string Id = "", Ad = "", Tur = "", Birim = "", Kaynak = "", Tarih = "";
        public double Fiyat, ReferansFiyat;
        public bool Guncellendi => Math.Abs(Fiyat - ReferansFiyat) > 1e-9 || Kaynak != ReferansKaynak;
        internal string ReferansKaynak = "";
    }

    /// <summary>
    /// Fiyat güncelleme. Referans CSV dosyaları değiştirilmez (bütünlük özeti bozulmasın); güncel fiyat veritabanında tutulur
    /// ve program açılırken bellekteki veriye uygulanır. Referans değer her zaman geri alınabilir.
    /// </summary>
    public sealed partial class VeriDeposu
    {
        /// <summary>Tarife olarak güncellenebilen parametreler (doğal gaz ve elektrik).</summary>
        public static readonly string[] TarifeParametreleri = { "E1", "E2", "EL1", "EL2" };

        readonly Dictionary<string, (double fiyat, string kaynak, string tarih)> referans = new();
        readonly Dictionary<string, string> referansHesap = new();

        public List<FiyatKalemi> FiyatKalemleri()
        {
            var l = new List<FiyatKalemi>();
            foreach (var m in Malzemeler)
                l.Add(Kalem(m.Id, m.Ad, m.Kategori, "TL/m²", m.FiyatM2, m.FiyatKaynagi, m.FiyatTarihi));
            foreach (var y in Yakitlar.Where(y => !y.Elektrikli))   // elektrik fiyatı tarifeden (EL1, EL2) gelir
                l.Add(Kalem(y.Id, y.Ad, "Yakıt", "TL/" + y.Birim, y.FiyatBirim, y.FiyatKaynagi, y.FiyatTarihi));
            foreach (var p in TarifeParametreleri.Where(Parametre.ContainsKey))
            {
                var b = ParametreBilgi.TryGetValue(p, out var x) ? x : (ad: p, birim: "", kaynak: "");
                l.Add(Kalem(p, b.ad, "Tarife", b.birim, Parametre[p], b.kaynak, "")); 
            }
            return l;
        }

        FiyatKalemi Kalem(string id, string ad, string tur, string birim, double fiyat, string kaynak, string tarih)
        {
            var r = referans.TryGetValue(id, out var x) ? x : (fiyat, kaynak, tarih);
            return new FiyatKalemi { Id = id, Ad = ad, Tur = tur, Birim = birim, Fiyat = fiyat, Kaynak = kaynak, Tarih = tarih, ReferansFiyat = r.fiyat, ReferansKaynak = r.kaynak };
        }

        /// <summary>Kalemin fiyatını değiştirir. Bilinmeyen kod ya da geçersiz fiyatta hata verir.</summary>
        public void FiyatUygula(string id, double fiyat, string kaynak, string tarih)
        {
            if (!(fiyat > 0) || double.IsInfinity(fiyat)) throw new ArgumentException("Fiyat sıfırdan büyük olmalı.");
            if (string.IsNullOrWhiteSpace(kaynak)) throw new ArgumentException("Fiyatın kaynağı yazılmalı.");
            var m = Malzemeler.FirstOrDefault(x => x.Id == id);
            var y = Yakitlar.FirstOrDefault(x => x.Id == id && !x.Elektrikli);
            bool tarife = TarifeParametreleri.Contains(id) && Parametre.ContainsKey(id);
            if (m == null && y == null && !tarife) throw new ArgumentException($"'{id}' fiyatı güncellenebilen bir kalem değil.");
            if (m != null)
            {
                if (!referans.ContainsKey(id)) { referans[id] = (m.FiyatM2, m.FiyatKaynagi, m.FiyatTarihi); referansHesap[id] = m.FiyatHesabi; }
                m.FiyatM2 = fiyat; m.FiyatKaynagi = kaynak; m.FiyatTarihi = tarih; m.FiyatHesabi = "Firma güncellemesi";
            }
            else if (y != null)
            {
                if (!referans.ContainsKey(id)) referans[id] = (y.FiyatBirim, y.FiyatKaynagi, y.FiyatTarihi);
                y.FiyatBirim = fiyat; y.FiyatKaynagi = kaynak; y.FiyatTarihi = tarih;
            }
            else
            {
                var b = ParametreBilgi.TryGetValue(id, out var x) ? x : (ad: id, birim: "", kaynak: "");
                if (!referans.ContainsKey(id)) referans[id] = (Parametre[id], b.kaynak, "");
                Parametre[id] = fiyat; ParametreBilgi[id] = (b.ad, b.birim, kaynak + (tarih.Length > 0 ? " · " + tarih : ""));
            }
        }

        /// <summary>Kalemi referans dosyasındaki fiyatına döndürür.</summary>
        public void FiyatReferansaDon(string id)
        {
            if (!referans.TryGetValue(id, out var r)) return;
            var m = Malzemeler.FirstOrDefault(x => x.Id == id);
            var y = Yakitlar.FirstOrDefault(x => x.Id == id);
            if (m != null) { m.FiyatM2 = r.fiyat; m.FiyatKaynagi = r.kaynak; m.FiyatTarihi = r.tarih; m.FiyatHesabi = referansHesap[id]; referansHesap.Remove(id); }
            else if (y != null) { y.FiyatBirim = r.fiyat; y.FiyatKaynagi = r.kaynak; y.FiyatTarihi = r.tarih; }
            else { var b = ParametreBilgi[id]; Parametre[id] = r.fiyat; ParametreBilgi[id] = (b.ad, b.birim, r.kaynak); }
            referans.Remove(id);
        }
    }
}
