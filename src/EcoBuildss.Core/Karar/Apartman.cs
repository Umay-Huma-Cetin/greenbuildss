using System;

namespace EcoBuildss.Core
{
    /// <summary>Apartman mantolamasının kat malikleri için daire başına ekonomisi.</summary>
    public sealed class ApartmanSonucu
    {
        public int DaireSayisi;
        public double TeklifTL, KdvDahilTL, DaireBasiTL;        // tüm iş (KDV hariç / dahil) ve daire başına (KDV dahil, eşit pay)
        public int Taksit; public double AylikTaksit, ToplamOdeme; // daire başına
        public double AzalmaOrani;                               // ısıtma ihtiyacındaki azalma (1 − H_yeni / H_mevcut)
        public double FaturaOnce, FaturaSonra, YillikTasarruf;   // bir dairenin yıllık ısıtma faturası (TL)
        public bool FaturadanHesaplandi;                         // true: kullanıcının girdiği gerçek faturadan; false: modelden tahmin
        public double? GeriOdemeYil;                             // daire başına, iskontolu
        public double KisAylikTasarruf;                          // ısıtma mevsiminde ayda (yaklaşık; 6 aylık mevsim)
    }

    /// <summary>
    /// Kat malikleri toplantısında sorulan üç sorunun cevabı: daire başına ne ödeyeceğim, faturam ne kadar düşecek, kaç yılda döner?
    /// Gerçek fatura girildiyse tasarruf, o faturanın ısıtma payına modelin bulduğu azalma oranı uygulanarak bulunur
    /// (fiyat, dağıtım bedeli ve kullanım alışkanlığı faturanın içindedir). Girilmediyse model tahminidir.
    /// </summary>
    public static class Apartman
    {
        public const int IsitmaAyi = 6;   // Ekim–Mart

        public static ApartmanSonucu Hesapla(VeriDeposu v, Sonuc r, Secenek s, int daireSayisi, double firmaPayi, double indirim,
                                             double? yillikFaturaDaire, double? yazAylikFaturaDaire, int taksit, double aylikVadeFarki)
        {
            if (daireSayisi < 1) throw new ArgumentException("Daire sayısı en az 1 olmalı.");
            if (!r.Mevcut) throw new ArgumentException("Apartman hesabı yenileme (mevcut bina) içindir.");
            double kdv = v.P("KDV");
            var a = new ApartmanSonucu { DaireSayisi = daireSayisi, Taksit = Math.Max(0, taksit) };
            a.TeklifTL = s.IlkYatirim * (1 + firmaPayi) * (1 - indirim);
            a.KdvDahilTL = a.TeklifTL * (1 + kdv);
            a.DaireBasiTL = a.KdvDahilTL / daireSayisi;

            if (a.Taksit <= 1) { a.AylikTaksit = a.DaireBasiTL; a.ToplamOdeme = a.DaireBasiTL; }
            else
            {
                double i = Math.Max(0, aylikVadeFarki);
                a.AylikTaksit = i < 1e-9 ? a.DaireBasiTL / a.Taksit : a.DaireBasiTL * i / (1 - Math.Pow(1 + i, -a.Taksit));
                a.ToplamOdeme = a.AylikTaksit * a.Taksit;
            }

            // Isıtma ihtiyacı H ile orantılı (derece-gün yöntemi): azalma oranı kalibrasyondan ve fiyattan bağımsızdır
            a.AzalmaOrani = r.HBaz > 0 ? Math.Max(0, 1 - s.H / r.HBaz) : 0;
            if (yillikFaturaDaire is double f && f > 0)
            {
                double baz = Math.Max(0, (yazAylikFaturaDaire ?? 0) * 12);   // sıcak su ve ocak: yaz faturasından
                a.FaturaOnce = Math.Max(0, f - baz);
                a.FaturadanHesaplandi = true;
            }
            else
            {
                // Model: mevcut binanın yıllık ısıtma yakıtı × tarife (BOTAŞ) + KDV, daire başına
                a.FaturaOnce = r.MevcutIsitmaKWh / r.IsitmaVerimi * r.GazTL * (1 + kdv) / daireSayisi;
            }
            a.FaturaSonra = a.FaturaOnce * (1 - a.AzalmaOrani);
            a.YillikTasarruf = a.FaturaOnce - a.FaturaSonra;
            a.KisAylikTasarruf = a.YillikTasarruf / IsitmaAyi;
            a.GeriOdemeYil = Kpi.IskontoluGeriOdeme(a.DaireBasiTL, a.YillikTasarruf, r.EnerjiIskonto);
            return a;
        }

        /// <summary>Eşit taksitli ödeme (anüite): aylık vade farkı i ile n ay.</summary>
        public static double Taksit(double anapara, int n, double i) => n <= 1 ? anapara : i < 1e-9 ? anapara / n : anapara * i / (1 - Math.Pow(1 + i, -n));
    }
}
