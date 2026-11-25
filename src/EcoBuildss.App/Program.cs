using System.Globalization;
using System.Threading;

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
            Application.ThreadException += (_, e) => Ui.Hata(null, "Beklenmeyen hata: " + e.Exception.Message);
            try { Application.Run(new AnaForm()); }
            catch (Exception e) { MessageBox.Show("EcoBuildss başlatılamadı:\n\n" + e.Message, "EcoBuildss", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
