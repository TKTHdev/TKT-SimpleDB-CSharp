using AyeAyeDB.Jdbc;
using AyeAyeDB.Jdbc.Network;
using AyeAyeDB.Record;

namespace AyeAyeDB.Demo.Client;

/// <summary>
/// Demo client: connects to a SimpleDbServer over TCP and runs SQL statements.
///
/// Usage:
///     dotnet run --project Demo/Client -- [host] [port] [sql ...]
///
/// If no SQL is given on the command line, an interactive REPL is started.
/// SELECT statements print a tabular result set; other statements report the
/// number of affected rows.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string host = "localhost";
        int port = SimpleDbServer.DefaultPort;
        var sqlArgs = new List<string>();

        int i = 0;
        if (i < args.Length && !LooksLikeSql(args[i])) host = args[i++];
        if (i < args.Length && int.TryParse(args[i], out var p)) { port = p; i++; }
        for (; i < args.Length; i++) sqlArgs.Add(args[i]);

        IDriver driver = new NetworkDriver(port);
        IConnection conn;
        try
        {
            conn = driver.Connect(host);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"failed to connect to {host}:{port} - {e.Message}");
            return 1;
        }

        using var _ = new ConnectionScope(conn);
        IStatement stmt = conn.CreateStatement();

        if (sqlArgs.Count > 0)
        {
            string sql = string.Join(' ', sqlArgs).Trim().TrimEnd(';');
            return ExecuteSql(stmt, sql) ? 0 : 1;
        }

        Console.WriteLine($"connected to {host}:{port}. type SQL terminated by ';'. \\q to quit.");
        return RunRepl(stmt);
    }

    private static int RunRepl(IStatement stmt)
    {
        var buf = new System.Text.StringBuilder();
        while (true)
        {
            Console.Write(buf.Length == 0 ? "sql> " : "  -> ");
            string? line = Console.ReadLine();
            if (line == null) break;
            string trimmed = line.Trim();
            if (buf.Length == 0 && (trimmed == "\\q" || trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase)))
                break;
            buf.Append(line).Append('\n');
            string text = buf.ToString();
            int semi = text.IndexOf(';');
            if (semi < 0) continue;

            string sql = text[..semi].Trim();
            buf.Clear();
            string rest = text[(semi + 1)..].Trim();
            if (rest.Length > 0) buf.Append(rest).Append('\n');
            if (sql.Length == 0) continue;
            ExecuteSql(stmt, sql);
        }
        return 0;
    }

    private static bool ExecuteSql(IStatement stmt, string sql)
    {
        try
        {
            if (IsQuery(sql))
            {
                IResultSet rs = stmt.ExecuteQuery(sql);
                try { PrintResultSet(rs); }
                finally { rs.Close(); }
            }
            else
            {
                int n = stmt.ExecuteUpdate(sql);
                Console.WriteLine($"{n} row(s) affected.");
            }
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return false;
        }
    }

    private static bool IsQuery(string sql)
    {
        string s = sql.TrimStart();
        return s.Length >= 6 && s[..6].Equals("select", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeSql(string s)
    {
        string t = s.TrimStart();
        if (t.Length == 0) return false;
        ReadOnlySpan<string> kws = new[] { "select", "insert", "update", "delete", "create" };
        foreach (var k in kws)
            if (t.Length >= k.Length && t[..k.Length].Equals(k, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static void PrintResultSet(IResultSet rs)
    {
        var md = rs.GetMetaData();
        int cols = md.GetColumnCount();
        var names = new string[cols];
        var widths = new int[cols];
        var types = new int[cols];
        for (int c = 1; c <= cols; c++)
        {
            names[c - 1] = md.GetColumnName(c);
            widths[c - 1] = Math.Max(md.GetColumnDisplaySize(c), names[c - 1].Length);
            types[c - 1] = md.GetColumnType(c);
        }

        PrintSeparator(widths);
        PrintRow(names, widths, types, isHeader: true);
        PrintSeparator(widths);

        int rows = 0;
        while (rs.Next())
        {
            var row = new string[cols];
            for (int c = 0; c < cols; c++)
            {
                row[c] = types[c] == Schema.SqlType.INTEGER
                    ? rs.GetInt(names[c]).ToString()
                    : rs.GetString(names[c]);
            }
            PrintRow(row, widths, types, isHeader: false);
            rows++;
        }
        PrintSeparator(widths);
        Console.WriteLine($"{rows} row(s).");
    }

    private static void PrintSeparator(int[] widths)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('+');
        foreach (var w in widths) sb.Append(new string('-', w + 2)).Append('+');
        Console.WriteLine(sb.ToString());
    }

    private static void PrintRow(string[] cells, int[] widths, int[] types, bool isHeader)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('|');
        for (int c = 0; c < cells.Length; c++)
        {
            string cell = cells[c] ?? "";
            bool right = !isHeader && types[c] == Schema.SqlType.INTEGER;
            sb.Append(' ');
            sb.Append(right ? cell.PadLeft(widths[c]) : cell.PadRight(widths[c]));
            sb.Append(" |");
        }
        Console.WriteLine(sb.ToString());
    }

    private sealed class ConnectionScope : IDisposable
    {
        private readonly IConnection _conn;
        public ConnectionScope(IConnection conn) => _conn = conn;
        public void Dispose()
        {
            try { _conn.Close(); } catch { }
        }
    }
}
