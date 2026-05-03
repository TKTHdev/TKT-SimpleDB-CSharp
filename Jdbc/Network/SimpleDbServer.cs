using System.Net;
using System.Net.Sockets;

namespace AyeAyeDB.Jdbc.Network;

/// <summary>
/// TCP server that accepts JDBC-like connections to a SimpleDB database.
/// Each client connection is handled in its own thread and maps to a
/// RemoteConnectionImpl session (equivalent to Java RMI's UnicastRemoteObject).
/// Default port is 1099 to match Java RMI convention.
/// </summary>
public class SimpleDbServer
{
    public const int DefaultPort = 1099;

    private readonly SimpleDB _db;
    private readonly int _port;
    private TcpListener? _listener;
    private volatile bool _running;

    public SimpleDbServer(SimpleDB db, int port = DefaultPort)
    {
        _db = db;
        _port = port;
    }

    public void Start()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _running = true;
        Console.WriteLine($"SimpleDB server started on port {_port}");

        while (_running)
        {
            try
            {
                TcpClient client = _listener.AcceptTcpClient();
                var conn = new RemoteConnectionImpl(_db);
                Thread t = new Thread(() => HandleClient(client, conn));
                t.IsBackground = true;
                t.Start();
            }
            catch (SocketException) when (!_running)
            {
                break;
            }
        }
    }

    public void Stop()
    {
        _running = false;
        _listener?.Stop();
    }

    private static void HandleClient(TcpClient client, RemoteConnectionImpl conn)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream))
        using (var writer = new StreamWriter(stream) { AutoFlush = true })
        {
            IRemoteStatement? currentStmt = null;
            IRemoteResultSet? currentRs = null;

            try
            {
                writer.WriteLine("OK");

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("QUERY "))
                    {
                        string sql = line[6..];
                        try
                        {
                            currentStmt ??= conn.CreateStatement();
                            currentRs?.Close();
                            currentRs = currentStmt.ExecuteQuery(sql);
                            writer.WriteLine("OK");
                        }
                        catch (Exception e)
                        {
                            writer.WriteLine("ERROR " + e.Message);
                        }
                    }
                    else if (line.StartsWith("UPDATE "))
                    {
                        string sql = line[7..];
                        try
                        {
                            currentStmt ??= conn.CreateStatement();
                            int count = currentStmt.ExecuteUpdate(sql);
                            writer.WriteLine("OK " + count);
                        }
                        catch (Exception e)
                        {
                            writer.WriteLine("ERROR " + e.Message);
                        }
                    }
                    else if (line == "NEXT")
                    {
                        if (currentRs == null) { writer.WriteLine("ERROR no active result set"); continue; }
                        try { writer.WriteLine(currentRs.Next() ? "true" : "false"); }
                        catch (Exception e) { writer.WriteLine("ERROR " + e.Message); }
                    }
                    else if (line.StartsWith("GETINT "))
                    {
                        string fld = line[7..];
                        if (currentRs == null) { writer.WriteLine("ERROR no active result set"); continue; }
                        try { writer.WriteLine(currentRs.GetInt(fld)); }
                        catch (Exception e) { writer.WriteLine("ERROR " + e.Message); }
                    }
                    else if (line.StartsWith("GETSTRING "))
                    {
                        string fld = line[10..];
                        if (currentRs == null) { writer.WriteLine("ERROR no active result set"); continue; }
                        try { writer.WriteLine(currentRs.GetString(fld)); }
                        catch (Exception e) { writer.WriteLine("ERROR " + e.Message); }
                    }
                    else if (line == "METADATA")
                    {
                        if (currentRs == null) { writer.WriteLine("ERROR no active result set"); continue; }
                        try
                        {
                            var md = currentRs.GetMetaData();
                            int count = md.GetColumnCount();
                            writer.WriteLine(count);
                            for (int i = 1; i <= count; i++)
                                writer.WriteLine($"{md.GetColumnName(i)}\t{md.GetColumnType(i)}\t{md.GetColumnDisplaySize(i)}");
                        }
                        catch (Exception e) { writer.WriteLine("ERROR " + e.Message); }
                    }
                    else if (line == "CLOSERS")
                    {
                        currentRs?.Close();
                        currentRs = null;
                        writer.WriteLine("OK");
                    }
                    else if (line == "CLOSE")
                    {
                        currentRs?.Close();
                        conn.Close();
                        writer.WriteLine("OK");
                        break;
                    }
                    else
                    {
                        writer.WriteLine("ERROR unknown command: " + line);
                    }
                }
            }
            catch (Exception)
            {
                // client disconnected
            }
        }
    }
}
