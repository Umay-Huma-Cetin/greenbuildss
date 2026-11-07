using System.Data.Common;

namespace EcoBuildss.Depo
{
    /// <summary>
    /// SQLite bağlantısını açar. Uygulama Microsoft.Data.Sqlite paketini kullanır (NuGet, Visual Studio otomatik yükler).
    /// MONO_SQLITE sembolü yalnızca geliştirme sırasında Linux/Mono üzerinde yapılan testler içindir.
    /// </summary>
    internal static class SqliteBaglanti
    {
        public static DbConnection Ac(string dosya)
        {
#if MONO_SQLITE
            var c = new Mono.Data.Sqlite.SqliteConnection($"Data Source={dosya};Version=3");
#else
            var c = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dosya }.ToString());
#endif
            c.Open();
            return c;
        }
    }
}
