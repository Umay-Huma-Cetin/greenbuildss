using EcoBuildss.UI;

namespace EcoBuildss
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            // Beklenmeyen hatalar arka plan günlüğüne (veritabanı) yazılır; kullanıcıya kısa bir mesaj gösterilir
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                EcoBuildss.Depo.Gunluk.Hata("Beklenmeyen hata", e.Exception);
                MessageBox.Show("Beklenmeyen bir hata oluştu; işlem tamamlanamadı.\n\n" + e.Exception.Message, "EcoBuildss", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                EcoBuildss.Depo.Gunluk.Hata("Beklenmeyen hata (uygulama kapandı)", e.ExceptionObject?.ToString() ?? "");
            // Açılış animasyonu: çizgi dolunca ana pencere kurulur, animasyon solarak kapanır, ardından ana pencere açılır
            AnaForm? ana = null;
            using (var acilis = new AcilisEkrani { Hazirla = () => ana = new AnaForm() })
                Application.Run(acilis);
            Application.Run(ana ?? new AnaForm());
        }
    }
}
