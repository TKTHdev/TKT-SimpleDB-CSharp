using AyeAyeDB;
using AyeAyeDB.Jdbc.Network;

namespace AyeAyeDB.Demo.Server;

/// <summary>
/// Demo server: opens (or creates) a SimpleDB database directory and starts
/// a TCP SimpleDbServer that accepts SQL queries/updates from clients.
///
/// Usage:
///     dotnet run --project Demo/Server -- [dbdir] [port]
///
///     dbdir : database directory (default: "demodb")
///     port  : TCP port to listen on (default: 1099)
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        string dbdir = args.Length > 0 ? args[0] : "demodb";
        int port = args.Length > 1 && int.TryParse(args[1], out var p) ? p : SimpleDbServer.DefaultPort;

        var db = new SimpleDB(dbdir);
        var server = new SimpleDbServer(db, port);

        Console.CancelKeyPress += (_, e) =>
        {
            Console.WriteLine();
            Console.WriteLine("shutting down...");
            server.Stop();
            e.Cancel = true;
        };

        Console.WriteLine($"database directory: {dbdir}");
        server.Start();
    }
}
