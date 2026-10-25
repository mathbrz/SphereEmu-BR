using System.Net;
using System.Net.Sockets;
using System.Text;

// Logging TCP proxy: client (127.0.0.1) -> official Sphere server. One jsonl line per received chunk.
string upstreamHost = args.Length > 0 ? args[0] : "77.223.107.68";
int port = args.Length > 1 ? int.Parse(args[1]) : 25860;
string outPath = args.Length > 2 ? args[2] : $"sessao_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl";

var listener = new TcpListener(IPAddress.Loopback, port);
listener.Start();
var writeLock = new object();
using var writer = new StreamWriter(outPath, append: true, new UTF8Encoding(false)) { AutoFlush = true };
int connCounter = 0;
Console.WriteLine($"Proxy 127.0.0.1:{port} -> {upstreamHost}:{port}, log: {Path.GetFullPath(outPath)}");

void Log(int conn, string dir, byte[] buf, int len)
{
    var line = $"{{\"t\":\"{DateTime.Now:HH:mm:ss.fff}\",\"c\":{conn},\"d\":\"{dir}\",\"n\":{len},\"hex\":\"{Convert.ToHexString(buf, 0, len)}\"}}";
    lock (writeLock) writer.WriteLine(line);
}

void Event(int conn, string text)
{
    lock (writeLock) writer.WriteLine($"{{\"t\":\"{DateTime.Now:HH:mm:ss.fff}\",\"c\":{conn},\"d\":\"EVT\",\"msg\":\"{text}\"}}");
    Console.WriteLine($"{DateTime.Now:HH:mm:ss} [{conn}] {text}");
}

async Task Pump(NetworkStream from, NetworkStream to, int conn, string dir)
{
    var buf = new byte[65536];
    try
    {
        int n;
        while ((n = await from.ReadAsync(buf)) > 0)
        {
            Log(conn, dir, buf, n);
            await to.WriteAsync(buf.AsMemory(0, n));
        }
    }
    catch (Exception) { }
}

while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    int conn = ++connCounter;
    _ = Task.Run(async () =>
    {
        try
        {
            using var up = new TcpClient();
            await up.ConnectAsync(upstreamHost, port);
            client.NoDelay = up.NoDelay = true;
            Event(conn, "connected");
            using var cs = client.GetStream();
            using var us = up.GetStream();
            await Task.WhenAny(Pump(cs, us, conn, "C2S"), Pump(us, cs, conn, "S2C"));
            Event(conn, "closed");
        }
        catch (Exception e) { Event(conn, "error " + e.GetType().Name); }
        finally { client.Close(); }
    });
}
