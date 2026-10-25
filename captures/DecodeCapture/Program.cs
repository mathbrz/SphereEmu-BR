using System.Text.Json;
using SphServer.Helpers.Networking;

// Reassembles frames from a LogProxy jsonl and decodes them with the MBC catalog.
// Usage: DecodeCapture <file.jsonl> <fromHH:mm:ss.fff> <toHH:mm:ss.fff> [all|hex]
//   default: only frames that carry more than the periodic noise; "all" keeps everything; "hex" adds raw bodies.
byte[] mask = [0x4B, 0x0D, 0xEF, 0x60, 0xC9, 0x9A, 0x70, 0x0E, 0x03];

byte[] DecodeClient(byte[] b)
{
    var o = (byte[])b.Clone();
    byte m3 = 0;
    for (var i = 0; i < b.Length - 9; i++)
    {
        var c = (byte)(b[9 + i] ^ mask[i % 9] ^ m3);
        o[9 + i] = c;
        m3 = (byte)(c * i + 2 * m3);
    }
    return o;
}

var decoder = MbcProtocolDecoder.CreateDefault();
var bufs = new Dictionary<string, List<byte>> { ["C2S"] = new(), ["S2C"] = new() };
var lo = args[1];
var hi = args[2];
var all = args.Length > 3 && args[3] == "all";
var hex = args.Length > 3 && args[3] == "hex";

foreach (var line in File.ReadLines(args[0]))
{
    var r = JsonDocument.Parse(line).RootElement;
    var d = r.GetProperty("d").GetString()!;
    if (!bufs.TryGetValue(d, out var buf)) continue;
    var t = r.GetProperty("t").GetString()!;
    buf.AddRange(Convert.FromHexString(r.GetProperty("hex").GetString()!));
    while (buf.Count >= 2)
    {
        var n = buf[0] | (buf[1] << 8);
        if (n < 2 || buf.Count < n) break;
        var f = buf.GetRange(0, n).ToArray();
        buf.RemoveRange(0, n);
        var inWindow = string.CompareOrdinal(t, lo) >= 0 && string.CompareOrdinal(t, hi) <= 0;

        var client = d == "C2S";
        var channel = f.Length >= 8 ? (client ? f[6] | (f[7] << 8) : f[2] | (f[3] << 8)) : 0;
        if (channel != 0x012C && !inWindow) continue;
        if (channel != 0x012C)
        {
            if (all) Console.WriteLine($"{t} {d} len={n} channel={channel:X4}");
            continue;
        }

        var raw = client && n > 9 ? DecodeClient(f) : f;
        var payload = client ? raw[8..] : raw[4..];
        var res = decoder.DecodeGame(payload, client ? MbcDirection.Client : MbcDirection.Server);
        if (!inWindow) continue;
        var noise = !client && (n == 23 || n == 18 || n == 15 || n == 16) || client && (n == 38 || n == 8);
        if (noise && !all) continue;
        Console.WriteLine($"{t} {d} len={n} {res.Status} pid={res.ProcessId:X4} module={res.Module}" + (res.HasPosition ? $" origin={string.Join(',', res.BasePosition!)}" : ""));
        foreach (var e in res.Events.Where(ev => args.Length < 5 || ev.ProcessId == Convert.ToInt32(args[4], 16)))
        {
            Console.WriteLine($"    [{e.Module}.{(string.IsNullOrEmpty(e.EventName) ? e.RecoveredName : e.EventName)}] pid={e.ProcessId:X4} region={e.Region} cmd={e.Command} {e.Summary}");
            foreach (var fl in e.Fields) Console.WriteLine($"        {fl.Name}: {fl.Display}" + (fl.ArrayValue is { } av ? " arr=" + string.Join(",", av) : "") + $" bits={fl.BitOffset}+{fl.BitLength}");
        }
        if (!string.IsNullOrEmpty(res.Error)) Console.WriteLine($"    error: {res.Error}");
        if (hex) Console.WriteLine($"    {Convert.ToHexString(payload)}");
    }
}
