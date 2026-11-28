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

        public Kullanici? Kullanici => Db?.Aktif;
        public bool Yonetici => Db?.Aktif?.Yonetici == true;

        /// <summary>Hata: kullanıcıya gösterilir, işlem günlüğüne "Hata" seviyesinde ve yedek olarak hata.log dosyasına yazılır.</summary>
        public void Hata(string mesaj, Exception? e = null)
        {
            Bildir(mesaj);
            HataDosyasina(mesaj, e);
            try { Db?.Gunluk("Hata", mesaj + (e != null ? $" [{e.GetType().Name}]" : ""), Seviye.Hata); } catch { }
        }

        /// <summary>Veritabanına yazılamasa bile hatalar kaybolmasın diye düz metin dosyası.</summary>
        public static void HataDosyasina(string mesaj, Exception? e = null)
        {
            try
            {
                string k = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EcoBuildss");
                Directory.CreateDirectory(k);
                string dosya = Path.Combine(k, "hata.log");
                if (File.Exists(dosya) && new FileInfo(dosya).Length > 5 * 1024 * 1024) { File.Delete(dosya + ".eski"); File.Move(dosya, dosya + ".eski"); }   // en çok ~10 MB yer kaplar
                File.AppendAllText(dosya, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{mesaj}{(e != null ? "\t" + e : "")}{Environment.NewLine}");
            }
            catch { }
        }
        public void VeriDegistiDuyur() => VeriDegisti?.Invoke();

        /// <summary>Veritabanındaki güncel fiyatları referans veriye uygular (önce hepsi referansa döner, yedekten dönüşte de doğru olsun).</summary>
        public void FiyatlariUygula()
        {
            foreach (var k in V.FiyatKalemleri().Where(k => k.Guncellendi)) V.FiyatReferansaDon(k.Id);
            if (Db == null) return;
            foreach (var (id, fiyat, kaynak, tarih) in Db.FiyatGuncellemeleri())
                try { V.FiyatUygula(id, fiyat, kaynak, tarih); }
                catch (Exception e) { Hata($"{id} fiyatı uygulanamadı: {e.Message}", e); }
        }
        public void ModellerDegistiDuyur() => ModellerDegisti?.Invoke();
        public void TekliflerDegistiDuyur() => TekliflerDegisti?.Invoke();

        public string Referans => Path.Combine(Kok, "veri", "referans");

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
                catch (Exception e) { Hata("Kalibrasyon kurulamadı: " + e.Message, e); }
            var (k, kk) = Teklifler();
            if (k.Count >= 20)
                try { Kabul = KabulTahmini.Egit(V, k); Db?.ModelKaydet("Kabul tahmini (sınıflandırma)", kk, k.Count, Kabul.Yorum); }
                catch (Exception e) { Hata("Kabul modeli kurulamadı: " + e.Message, e); }
            ModellerDegistiDuyur();
        }

    }
}
