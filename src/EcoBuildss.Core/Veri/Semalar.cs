using System;
using System.Collections.Generic;
using System.Linq;

namespace EcoBuildss.Core.Veri
{
    /// <summary>
    /// Firmanın kendi gerçek verisi için iki dosya şeması:
    /// 1) Tüketim kayıtları (faturalar) → enerji modelinin kalibrasyonu (regresyon)
    /// 2) Teklif geçmişi (CRM) → teklif kabul tahmini (sınıflandırma) ve müşteri segmentasyonu (kümeleme)
    /// Uygulamanın ürettiği sentetik örnek dosyalar da aynı şemadan, aynı denetimle geçer.
    /// </summary>
    public static class Semalar
    {
        public static readonly string[] MusteriTipleri = { "Bireysel", "Site yönetimi", "Müteahhit", "Kurum" };
        public static readonly string[] Paketler = { "Ekonomik", "Önerilen", "Yüksek performans", "Özel" };
        public static readonly string[] Oncelikler = { "Bütçe", "Dengeli", "Enerji ve karbon" };
        public static readonly string[] ProjeTurleri = { "Yeni bina", "Yenileme" };
        public static readonly string[] Sonuclar = { "Kabul", "Red" };
        public static readonly string[] EvetHayir = { "Evet", "Hayır" };

        public static string[] YakitKodlari(VeriDeposu v) => new[] { "DG" }.Concat(v.Yakitlar.Select(y => y.Id)).ToArray();

        public static Sema Tuketim(VeriDeposu v)
        {
            var s = new Sema
            {
                Ad = "Tüketim kayıtları", DosyaAdi = "tuketim_kayitlari.csv", AnahtarAlan = "kayit_id",
                Aciklama = "Her satır bir binanın bir yıllık ısıtma yakıtı tüketimi (faturalardan). Enerji modelinin gerçek tüketime göre kalibrasyonunda kullanılır.",
                Alanlar =
                {
                    new Alan { Ad = "kayit_id", Aciklama = "Benzersiz kayıt kodu", Ornek = "T-0001" },
                    new Alan { Ad = "il", Tur = AlanTuru.Secenek, Secenekler = v.Iller.Select(i => i.Ad).ToArray(), Aciklama = "Binanın ili", Ornek = "Malatya" },
                    new Alan { Ad = "fatura_yili", Tur = AlanTuru.Tamsayi, Min = 2000, Max = 2100, Aciklama = "Tüketimin ait olduğu yıl", Ornek = "2025" },
                    new Alan { Ad = "kullanim_alani_m2", Tur = AlanTuru.Sayi, Min = 20, Max = 200000, Birim = "m²", Aciklama = "Isıtılan net kullanım alanı", Ornek = "220" },
                    new Alan { Ad = "hacim_m3", Tur = AlanTuru.Sayi, Min = 50, Max = 1000000, Birim = "m³", Aciklama = "Isıtılan brüt hacim", Ornek = "770" },
                    new Alan { Ad = "dis_duvar_alani_m2", Tur = AlanTuru.Sayi, Min = 10, Max = 200000, Birim = "m²", Aciklama = "Pencereler hariç opak dış duvar alanı", Ornek = "250" },
                    new Alan { Ad = "pencere_alani_m2", Tur = AlanTuru.Sayi, Min = 0, Max = 100000, Birim = "m²", Aciklama = "Toplam pencere alanı", Ornek = "45" },
                    new Alan { Ad = "cati_alani_m2", Tur = AlanTuru.Sayi, Min = 0, Max = 100000, Birim = "m²", Aciklama = "Isı kaybeden çatı/tavan alanı", Ornek = "110" },
                    new Alan { Ad = "zemin_alani_m2", Tur = AlanTuru.Sayi, Min = 0, Max = 100000, Birim = "m²", Aciklama = "Zemine oturan döşeme alanı", Ornek = "110" },
                    new Alan { Ad = "duvar_U", Tur = AlanTuru.Sayi, Min = 0.1, Max = 4, Birim = "W/m²K", Aciklama = "Dış duvar U değeri (proje ya da enerji kimlik belgesi)", Ornek = "0.45" },
                    new Alan { Ad = "pencere_U", Tur = AlanTuru.Sayi, Min = 0.5, Max = 6, Birim = "W/m²K", Aciklama = "Pencere U değeri", Ornek = "1.8" },
                    new Alan { Ad = "cati_U", Tur = AlanTuru.Sayi, Min = 0.1, Max = 4, Birim = "W/m²K", Aciklama = "Çatı U değeri", Ornek = "0.35" },
                    new Alan { Ad = "zemin_U", Tur = AlanTuru.Sayi, Min = 0.1, Max = 4, Birim = "W/m²K", Aciklama = "Döşeme U değeri", Ornek = "0.6" },
                    new Alan { Ad = "yakit", Tur = AlanTuru.Secenek, Secenekler = YakitKodlari(v), Aciklama = "DG = doğal gaz; diğerleri yakitlar.csv kodları", Ornek = "DG" },
                    new Alan { Ad = "yillik_tuketim_kWh", Tur = AlanTuru.Sayi, Min = 100, Max = 1e9, Birim = "kWh", Aciklama = "Yıllık yakıt tüketimi (doğal gazda Sm³ × fatura ısıl değeri ÷ 860)", Ornek = "32500" },
                    new Alan { Ad = "sicak_su_dahil", Tur = AlanTuru.Secenek, Secenekler = EvetHayir, Zorunlu = false, Aciklama = "Tüketime sıcak su ve pişirme dahil mi (boşsa Evet)", Ornek = "Evet" },
                    new Alan { Ad = "isitma_derece_gun", Tur = AlanTuru.Sayi, Min = 0, Max = 8000, Zorunlu = false, Birim = "°C·gün", Aciklama = "O yılın gerçekleşen ısıtma derece-günü (boşsa ilin uzun dönem MGM değeri)", Ornek = "" },
                },
            };
            s.SatirKurali = k =>
            {
                // Fiziksel tutarlılık: alan başına tüketim gerçekçi aralıkta olmalı (Türkiye konutlarında ~20–600 kWh/m²·yıl)
                double kwhm2 = k.Sayi("yillik_tuketim_kWh") / k.Sayi("kullanim_alani_m2");
                if (kwhm2 < 10 || kwhm2 > 800) return $"Alan başına tüketim {kwhm2:0} kWh/m²·yıl; birim ya da alan hatalı olabilir (beklenen 10–800).";
                if (k.Sayi("hacim_m3") < k.Sayi("kullanim_alani_m2") * 2) return "Hacim, kullanım alanının en az 2 katı olmalı (kat yüksekliği ≥ 2 m).";
                return null;
            };
            return s;
        }

        public static Sema Teklif(VeriDeposu v) => new Sema
        {
            Ad = "Teklif geçmişi", DosyaAdi = "teklif_gecmisi.csv", AnahtarAlan = "teklif_id",
            Aciklama = "Her satır firmanın verdiği bir teklif ve sonucu (CRM kaydı). Kabul tahmini ve müşteri segmentasyonunda kullanılır.",
            Alanlar =
            {
                new Alan { Ad = "teklif_id", Aciklama = "Benzersiz teklif kodu", Ornek = "TK-2025-001" },
                new Alan { Ad = "tarih", Tur = AlanTuru.Tarih, Aciklama = "Teklif tarihi", Ornek = "2025-03-14" },
                new Alan { Ad = "il", Tur = AlanTuru.Secenek, Secenekler = v.Iller.Select(i => i.Ad).ToArray(), Aciklama = "Projenin ili", Ornek = "Malatya" },
                new Alan { Ad = "musteri_tipi", Tur = AlanTuru.Secenek, Secenekler = MusteriTipleri, Ornek = "Site yönetimi" },
                new Alan { Ad = "proje_turu", Tur = AlanTuru.Secenek, Secenekler = ProjeTurleri, Ornek = "Yenileme" },
                new Alan { Ad = "kullanim_alani_m2", Tur = AlanTuru.Sayi, Min = 20, Max = 200000, Birim = "m²", Ornek = "1450" },
                new Alan { Ad = "paket", Tur = AlanTuru.Secenek, Secenekler = Paketler, Ornek = "Önerilen" },
                new Alan { Ad = "oncelik", Tur = AlanTuru.Secenek, Secenekler = Oncelikler, Aciklama = "Müşterinin belirttiği öncelik", Ornek = "Dengeli" },
                new Alan { Ad = "teklif_TL_m2", Tur = AlanTuru.Sayi, Min = 50, Max = 50000, Birim = "TL/m²", Aciklama = "Teklif tutarı ÷ kullanım alanı (KDV hariç)", Ornek = "2350" },
                new Alan { Ad = "geri_odeme_yil", Tur = AlanTuru.Sayi, Min = 0, Max = 100, Birim = "yıl", Zorunlu = false, Aciklama = "İskontolu geri ödeme süresi (boş = analiz süresinde geri ödemiyor)", Ornek = "7.9" },
                new Alan { Ad = "indirim_orani", Tur = AlanTuru.Sayi, Min = 0, Max = 0.6, Ornek = "0.05", Aciklama = "Liste fiyatına uygulanan indirim (0,05 = %5)" },
                new Alan { Ad = "yanit_suresi_gun", Tur = AlanTuru.Sayi, Min = 0, Max = 365, Birim = "gün", Aciklama = "Talepten teklife geçen süre", Ornek = "3" },
                new Alan { Ad = "rakip_teklif", Tur = AlanTuru.Secenek, Secenekler = EvetHayir, Aciklama = "Müşteri başka firmadan da teklif aldı mı", Ornek = "Evet" },
                new Alan { Ad = "sonuc", Tur = AlanTuru.Secenek, Secenekler = Sonuclar, Aciklama = "Teklifin sonucu", Ornek = "Kabul" },
            },
        };
    }
}
