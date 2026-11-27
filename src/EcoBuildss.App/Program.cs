using System.Globalization;
using System.Threading;
using EcoBuildss.Core.Depo;

namespace EcoBuildss.UI
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = Ortak.TR;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Durum? d = null;
            Application.ThreadException += (_, e) => { (d != null ? (Action<string, Exception>)d.Hata : Durum.HataDosyasina)("Beklenmeyen hata: " + e.Exception.Message, e.Exception); Ui.Hata(null, "Beklenmeyen hata: " + e.Exception.Message); };
            try
            {
                d = Baslat();
                if (d == null) return;
                Application.Run(new AnaForm(d));
            }
            catch (Exception e)
            {
                Durum.HataDosyasina("Başlatılamadı: " + e.Message, e);
                MessageBox.Show("EcoBuildss başlatılamadı:\n\n" + e.Message, "EcoBuildss", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (d?.Db != null) { try { d.Db.OturumKapat("program kapatıldı"); d.Db.Gunluk("Program kapandı"); } catch { } d.Db.Dispose(); }
            }
        }

        /// <summary>Referans veri, veritabanı, günlük otomatik yedek ve oturum açma. Oturum açılmazsa program açılmaz.</summary>
        static Durum? Baslat()
        {
            var d = new Durum { Kok = AnaForm.Kok() };
            d.V = VeriDeposu.Yukle(d.Referans);
            try { d.Db = new Veritabani(Path.Combine(AnaForm.VeriKlasoru(), "ecobuildss.db")); }
            catch (Exception e)
            {
                Durum.HataDosyasina("Veritabanı açılamadı: " + e.Message, e);
                MessageBox.Show("Veritabanı açılamadı:\n\n" + e.Message + "\n\nKullanıcı hesapları veritabanında tutulduğu için program bu durumda açılmaz.", "EcoBuildss", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            d.Db.Gunluk("Program açıldı", $"sürüm {typeof(Program).Assembly.GetName().Version} · {Environment.MachineName}");
            var bozuk = ReferansButunluk.Denetle(d.Referans);
            if (bozuk.Count > 0)
            {
                d.Db.Gunluk("Referans veri değiştirilmiş", string.Join("; ", bozuk), Seviye.Guvenlik);
                MessageBox.Show("Referans veri dosyaları değiştirilmiş:\n\n" + string.Join("\n", bozuk.Take(5)) + "\n\nHesaplar güvenilir değil. Programı yeniden kurun.", "EcoBuildss", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            try { if (d.Db.OtomatikYedek() is string y) d.Bildir("Yedek alındı."); }
            catch (Exception e) { d.Hata("Otomatik yedek alınamadı: " + e.Message, e); }
            using var g = new GirisFormu(d.Db);
            return g.ShowDialog() == DialogResult.OK ? d : null;
        }
    }
}
