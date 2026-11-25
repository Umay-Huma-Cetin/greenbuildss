using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace EcoBuildss.Core.Depo
{
    /// <summary>
    /// SQLite'a doğrudan C arayüzüyle bağlanan küçük katman. Windows 10/11'de işletim sistemiyle gelen winsqlite3.dll,
    /// Linux'ta libsqlite3 kullanılır; NuGet paketi gerekmez. Parametreler her zaman bağlanır (SQL enjeksiyonu olmaz).
    /// </summary>
    public sealed class SqliteBaglanti : IDisposable
    {
#if LINUX_SQLITE
        const string Lib = "libsqlite3.so.0";
#else
        const string Lib = "winsqlite3";
#endif
        const int OK = 0, ROW = 100, DONE = 101;
        const int INTEGER = 1, FLOAT = 2, TEXT = 3, NULL = 5;
        static readonly IntPtr Transient = new IntPtr(-1);

        [DllImport(Lib)] static extern int sqlite3_open_v2(byte[] dosya, out IntPtr db, int bayrak, IntPtr vfs);
        [DllImport(Lib)] static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Lib)] static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int n, out IntPtr stmt, IntPtr kalan);
        [DllImport(Lib)] static extern int sqlite3_step(IntPtr stmt);
        [DllImport(Lib)] static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport(Lib)] static extern int sqlite3_bind_int64(IntPtr stmt, int i, long v);
        [DllImport(Lib)] static extern int sqlite3_bind_double(IntPtr stmt, int i, double v);
        [DllImport(Lib)] static extern int sqlite3_bind_text(IntPtr stmt, int i, byte[] v, int n, IntPtr yikici);
        [DllImport(Lib)] static extern int sqlite3_bind_null(IntPtr stmt, int i);
        [DllImport(Lib)] static extern int sqlite3_bind_parameter_index(IntPtr stmt, byte[] ad);
        [DllImport(Lib)] static extern int sqlite3_column_count(IntPtr stmt);
        [DllImport(Lib)] static extern IntPtr sqlite3_column_name(IntPtr stmt, int i);
        [DllImport(Lib)] static extern int sqlite3_column_type(IntPtr stmt, int i);
        [DllImport(Lib)] static extern long sqlite3_column_int64(IntPtr stmt, int i);
        [DllImport(Lib)] static extern double sqlite3_column_double(IntPtr stmt, int i);
        [DllImport(Lib)] static extern IntPtr sqlite3_column_text(IntPtr stmt, int i);
        [DllImport(Lib)] static extern int sqlite3_column_bytes(IntPtr stmt, int i);
        [DllImport(Lib)] static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(Lib)] static extern long sqlite3_last_insert_rowid(IntPtr db);

        IntPtr db;
        public string Dosya { get; }

        public SqliteBaglanti(string dosya)
        {
            Dosya = dosya;
            int rc = sqlite3_open_v2(Utf8(dosya), out db, 0x02 | 0x04, IntPtr.Zero);   // READWRITE | CREATE
            if (rc != OK) { string m = Hata(); sqlite3_close_v2(db); db = IntPtr.Zero; throw new InvalidOperationException($"Veritabanı açılamadı ({dosya}): {m}"); }
            Calistir("PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;");
        }

        static byte[] Utf8(string s) { var b = Encoding.UTF8.GetBytes(s); Array.Resize(ref b, b.Length + 1); return b; }
        static string Oku(IntPtr p, int n = -1)
        {
            if (p == IntPtr.Zero) return "";
            if (n < 0) { n = 0; while (Marshal.ReadByte(p, n) != 0) n++; }
            var b = new byte[n]; Marshal.Copy(p, b, 0, n); return Encoding.UTF8.GetString(b);
        }
        string Hata() => db == IntPtr.Zero ? "bağlantı yok" : Oku(sqlite3_errmsg(db));

        IntPtr Hazirla(string sql, IReadOnlyDictionary<string, object?>? p)
        {
            if (sqlite3_prepare_v2(db, Utf8(sql), -1, out IntPtr st, IntPtr.Zero) != OK) throw new InvalidOperationException($"SQL hatası: {Hata()}\n{sql}");
            if (p != null)
                foreach (var kv in p)
                {
                    int i = sqlite3_bind_parameter_index(st, Utf8(kv.Key.StartsWith("@") ? kv.Key : "@" + kv.Key));
                    if (i == 0) continue;
                    switch (kv.Value)
                    {
                        case null: sqlite3_bind_null(st, i); break;
                        case int v: sqlite3_bind_int64(st, i, v); break;
                        case long v: sqlite3_bind_int64(st, i, v); break;
                        case bool v: sqlite3_bind_int64(st, i, v ? 1 : 0); break;
                        case double v: if (double.IsNaN(v)) sqlite3_bind_null(st, i); else sqlite3_bind_double(st, i, v); break;
                        case DateTime v: var t = Encoding.UTF8.GetBytes(v.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)); sqlite3_bind_text(st, i, t, t.Length, Transient); break;
                        default: var b = Encoding.UTF8.GetBytes(Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? ""); sqlite3_bind_text(st, i, b, b.Length, Transient); break;
                    }
                }
            return st;
        }

        /// <summary>Birden çok deyimi parametresiz çalıştırır (şema kurulumu).</summary>
        public void Calistir(string sql)
        {
            foreach (var parca in sql.Split(';'))
            {
                if (parca.Trim().Length == 0) continue;
                var st = Hazirla(parca, null);
                try { int rc; while ((rc = sqlite3_step(st)) == ROW) { } if (rc != DONE) throw new InvalidOperationException($"SQL hatası: {Hata()}"); }
                finally { sqlite3_finalize(st); }
            }
        }

        public long Yaz(string sql, IReadOnlyDictionary<string, object?>? p = null)
        {
            var st = Hazirla(sql, p);
            try { if (sqlite3_step(st) != DONE) throw new InvalidOperationException($"SQL hatası: {Hata()}"); return sqlite3_last_insert_rowid(db); }
            finally { sqlite3_finalize(st); }
        }

        public List<Dictionary<string, object?>> Sorgu(string sql, IReadOnlyDictionary<string, object?>? p = null)
        {
            var l = new List<Dictionary<string, object?>>();
            var st = Hazirla(sql, p);
            try
            {
                int rc;
                while ((rc = sqlite3_step(st)) == ROW)
                {
                    var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < sqlite3_column_count(st); i++)
                    {
                        string ad = Oku(sqlite3_column_name(st, i));
                        d[ad] = sqlite3_column_type(st, i) switch
                        {
                            INTEGER => sqlite3_column_int64(st, i),
                            FLOAT => sqlite3_column_double(st, i),
                            NULL => null,
                            _ => Oku(sqlite3_column_text(st, i), sqlite3_column_bytes(st, i)),
                        };
                    }
                    l.Add(d);
                }
                if (rc != DONE) throw new InvalidOperationException($"SQL hatası: {Hata()}");
            }
            finally { sqlite3_finalize(st); }
            return l;
        }

        public void Islem(Action a)
        {
            Calistir("BEGIN");
            try { a(); Calistir("COMMIT"); } catch { Calistir("ROLLBACK"); throw; }
        }

        public void Dispose() { if (db != IntPtr.Zero) { sqlite3_close_v2(db); db = IntPtr.Zero; } }
    }
}
