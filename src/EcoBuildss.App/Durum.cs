using EcoBuildss.Core.Depo;
using EcoBuildss.Core.Uygulamalar;
using EcoBuildss.Core.Veri;

namespace EcoBuildss.UI
{
    /// <summary>
    /// Uygulamanın ortak durumu: referans veri, veritabanı, eğitilmiş modeller.
    /// Sekmeler birbirini doğrudan çağırmaz; değişiklikler olaylarla duyurulur.
    /// </summary>
    public sealed class Durum
    {
        public VeriDeposu V = null!;
        public Veritabani? Db;
        public string Kok = "";                 // programın veri klasörlerini içeren kök
        public string DbHatasi = "";
        public KalibrasyonSonucu? Kalibrasyon;
        public KabulModeliSonucu? Kabul;
        public SegmentasyonSonucu? IlKumeleri;

        public event Action? VeriDegisti, ModellerDegisti, TekliflerDegisti;
        public event Action<string>? Bildirim;

        public void Bildir(string m) => Bildirim?.Invoke(m);
        public void VeriDegistiDuyur() => VeriDegisti?.Invoke();
        public void ModellerDegistiDuyur() => ModellerDegisti?.Invoke();
        public void TekliflerDegistiDuyur() => TekliflerDegisti?.Invoke();

        public string Referans => Path.Combine(Kok, "veri", "referans");
        public string Ornek => Path.Combine(Kok, "veri", "ornek");

        public Sema TuketimSemasi => Semalar.Tuketim(V);
        public Sema TeklifSemasi => Semalar.Teklif(V);

        /// <summary>Kalibrasyon için tüketim kayıtları (içe aktarılmış).</summary>
        public (List<Kayit> kayit, string kaynak) Tuketim()
        {
            if (Db == null) return (new List<Kayit>(), "");
            var (s, k) = Db.VeriOku(TuketimSemasi);
            return (s.Kayitlar, k);
        }

        /// <summary>Sınıflandırma ve segmentasyon için teklif geçmişi: içe aktarılan kayıtlar + uygulamada kaydedilip sonucu işaretlenen teklifler.</summary>
        public (List<Kayit> kayit, string kaynak) Teklifler()
        {
            if (Db == null) return (new List<Kayit>(), "");
            var (s, k) = Db.VeriOku(TeklifSemasi);
            var l = new List<Kayit>(s.Kayitlar);
            var kendi = IceAktarici.Metinden(Db.SonuclananTekliflerCsv(TeklifSemasi), TeklifSemasi).Kayitlar;
            var anahtar = new HashSet<string>(l.Select(x => x.Metin("teklif_id")));
            l.AddRange(kendi.Where(x => anahtar.Add(x.Metin("teklif_id"))));
            string kaynak = kendi.Count == 0 ? k : k.Length == 0 ? "Gerçek (uygulama kayıtları)" : $"{k} + {kendi.Count} uygulama kaydı";
            return (l, kaynak);
        }

        /// <summary>Kayıtlı veriyle modelleri yeniden kurar (açılışta ve veri değişince). Yetersiz veride model boş kalır.</summary>
        public void ModelleriKur()
        {
            Kalibrasyon = null; Kabul = null;
            try { IlKumeleri = Segmentasyon.Iller(V); } catch { IlKumeleri = null; }
            var (t, tk) = Tuketim();
            if (t.Count >= KalibrasyonSonucu.MinKayit)
                try { Kalibrasyon = Core.Uygulamalar.Kalibrasyon.Calistir(V, t); Db?.ModelKaydet("Kalibrasyon (regresyon)", tk, t.Count, Kalibrasyon.Yorum); }
                catch (Exception e) { Bildir("Kalibrasyon kurulamadı: " + e.Message); }
            var (k, kk) = Teklifler();
            if (k.Count >= 20)
                try { Kabul = KabulTahmini.Egit(V, k); Db?.ModelKaydet("Kabul tahmini (sınıflandırma)", kk, k.Count, Kabul.Yorum); }
                catch (Exception e) { Bildir("Kabul modeli kurulamadı: " + e.Message); }
            ModellerDegistiDuyur();
        }

        public bool OrnekVeriKullaniliyor => Tuketim().kaynak == "Örnek" || Teklifler().kaynak.StartsWith("Örnek");
    }
}
