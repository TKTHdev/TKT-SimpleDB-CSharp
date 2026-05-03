using System.Net.Sockets;

namespace DBSharp.Jdbc.Network;

/// <summary>
/// Manages the TCP connection to the SimpleDbServer.
/// All remote stub objects (connection, statement, result set) share one TcpSession.
/// </summary>
internal class TcpSession : IDisposable
{
    private readonly TcpClient _client;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    public TcpSession(string host, int port)
    {
        _client = new TcpClient(host, port);
        var stream = _client.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
        string? greeting = _reader.ReadLine();
        if (greeting != "OK")
            throw new InvalidOperationException("server greeting failed: " + greeting);
    }

    public string SendQuery(string sql)
    {
        _writer.WriteLine("QUERY " + sql);
        return ReadLine();
    }

    public int SendUpdate(string sql)
    {
        _writer.WriteLine("UPDATE " + sql);
        string response = ReadLine();
        if (response.StartsWith("OK "))
            return int.Parse(response[3..]);
        throw new InvalidOperationException("update failed: " + response);
    }

    public bool Next()
    {
        _writer.WriteLine("NEXT");
        return ReadLine() == "true";
    }

    public int GetInt(string fieldname)
    {
        _writer.WriteLine("GETINT " + fieldname);
        return int.Parse(ReadLine());
    }

    public string GetString(string fieldname)
    {
        _writer.WriteLine("GETSTRING " + fieldname);
        return ReadLine();
    }

    public (int count, List<(string name, int type, int displaySize)> cols) GetMetaData()
    {
        _writer.WriteLine("METADATA");
        int count = int.Parse(ReadLine());
        var cols = new List<(string, int, int)>(count);
        for (int i = 0; i < count; i++)
        {
            string[] parts = ReadLine().Split('\t');
            cols.Add((parts[0], int.Parse(parts[1]), int.Parse(parts[2])));
        }
        return (count, cols);
    }

    public void CloseResultSet()
    {
        _writer.WriteLine("CLOSERS");
        ReadLine();
    }

    public void CloseConnection()
    {
        _writer.WriteLine("CLOSE");
        ReadLine();
    }

    private string ReadLine()
    {
        string? line = _reader.ReadLine();
        if (line == null)
            throw new InvalidOperationException("connection closed by server");
        if (line.StartsWith("ERROR "))
            throw new InvalidOperationException(line[6..]);
        return line;
    }

    public void Dispose()
    {
        _reader.Dispose();
        _writer.Dispose();
        _client.Dispose();
    }
}
