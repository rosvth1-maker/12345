using System.Net.Sockets;
using TsmClient.Domain;
using TsmClient.Protocol;

string host = args.ElementAtOrDefault(0) ?? "127.0.0.1";
int port = int.TryParse(args.ElementAtOrDefault(1), out int parsedPort) ? parsedPort : 6613;
var results = new List<(string Name, bool Passed, string Detail)>();

Console.WriteLine($"PHASE 1 INTEGRATION TEST — {host}:{port}");

for (int i = 1; i <= 10; i++)
{
    try
    {
        await using var probe = await ServerProbe.ConnectAsync(host, port);
        await probe.SendAsync(ClientPackets.Handshake(258));
        var response = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
        bool passed = response is { MainKind: 1, SubKind: 16 };
        results.Add(($"Connect/Handshake {i}/10", passed, passed ? "OK" : $"unexpected {response.MainKind}/{response.SubKind}"));
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  Connect/Handshake {i}/10");
    }
    catch (Exception ex)
    {
        results.Add(($"Connect/Handshake {i}/10", false, ex.GetType().Name));
        Console.WriteLine($"FAIL  Connect/Handshake {i}/10: {ex.GetType().Name}");
    }
}

string testAccount = $"phase1_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
const string testPassword = "Phase1-Test-Only";

try
{
    await using var probe = await ServerProbe.ConnectAsync(host, port);
    await probe.SendAsync(ClientPackets.Handshake(258));
    _ = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
    await probe.SendAsync(ClientPackets.Login(testAccount, testPassword));
    var login = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
    var loginReader = new PacketReader(login.Payload);
    bool loginPassed = login is { MainKind: 1, SubKind: 1 } && loginReader.ReadByte() == 1;
    results.Add(("Login success", loginPassed, loginPassed ? "OK" : "server rejected test login"));
    Console.WriteLine($"{(loginPassed ? "PASS" : "FAIL")}  Login success");

    var characters = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
    bool charListPassed = characters is { MainKind: 3, SubKind: 1 } && characters.Payload.Length >= 1;
    results.Add(("Character list", charListPassed, charListPassed ? $"count={characters.Payload[0]}" : "wrong opcode/payload"));
    Console.WriteLine($"{(charListPassed ? "PASS" : "FAIL")}  Character list");
}
catch (Exception ex)
{
    results.Add(("Login success", false, ex.GetType().Name));
    results.Add(("Character list", false, "not received"));
    Console.WriteLine($"FAIL  Login/Character list: {ex.GetType().Name}");
}

try
{
    await using var probe = await ServerProbe.ConnectAsync(host, port);
    await probe.SendAsync(ClientPackets.Handshake(258));
    _ = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
    await probe.SendAsync(ClientPackets.Login(testAccount, "Wrong-Test-Password"));
    var login = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
    var reader = new PacketReader(login.Payload);
    bool passed = login is { MainKind: 1, SubKind: 1 } && reader.ReadByte() == 0;
    results.Add(("Login rejected", passed, passed ? "OK" : "wrong-password login was not rejected"));
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  Login rejected");
}
catch (Exception ex)
{
    results.Add(("Login rejected", false, ex.GetType().Name));
    Console.WriteLine($"FAIL  Login rejected: {ex.GetType().Name}");
}

try
{
    await using var probe = await ServerProbe.ConnectAsync(host, port);
    await probe.SendAsync(ClientPackets.Handshake(258));
    _ = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
    for (int i = 1; i <= 3; i++)
    {
        Console.WriteLine($"WAIT  Keepalive {i}/3 — 20 seconds");
        await Task.Delay(TimeSpan.FromSeconds(20));
        await probe.SendAsync(ClientPackets.Keepalive());
        var response = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
        bool passed = response is { MainKind: 10, SubKind: 1 };
        results.Add(($"Keepalive {i}/3", passed, passed ? "OK" : $"unexpected {response.MainKind}/{response.SubKind}"));
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  Keepalive {i}/3");
    }
}
catch (Exception ex)
{
    results.Add(("Keepalive sequence", false, ex.GetType().Name));
    Console.WriteLine($"FAIL  Keepalive sequence: {ex.GetType().Name}");
}

int failed = results.Count(r => !r.Passed);
Console.WriteLine($"\nRESULT  {results.Count - failed}/{results.Count} passed");
foreach (var result in results.Where(r => !r.Passed)) Console.WriteLine($"FAILED  {result.Name}: {result.Detail}");
return failed == 0 ? 0 : 1;

sealed class ServerProbe : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly List<byte> _buffer = [];
    private ServerProbe(TcpClient client) { _client = client; _stream = client.GetStream(); }

    public static async Task<ServerProbe> ConnectAsync(string host, int port)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(host, port, timeout.Token);
        return new ServerProbe(client);
    }

    public Task SendAsync(byte[] frame) => _stream.WriteAsync(frame).AsTask();

    public async Task<GamePacket> ReceiveAsync(TimeSpan timeoutDuration)
    {
        using var timeout = new CancellationTokenSource(timeoutDuration);
        var chunk = new byte[4096];
        while (true)
        {
            if (FrameCodec.TryDecode(_buffer, out var packet) && packet is not null) return packet;
            int count = await _stream.ReadAsync(chunk, timeout.Token);
            if (count == 0) throw new IOException("Server disconnected");
            _buffer.AddRange(chunk.AsSpan(0, count).ToArray());
        }
    }

    public ValueTask DisposeAsync()
    {
        _stream.Dispose();
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
