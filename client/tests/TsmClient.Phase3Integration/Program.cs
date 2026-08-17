using System.Net.Sockets;
using TsmClient.Domain;
using TsmClient.Protocol;

string host = args.ElementAtOrDefault(0) ?? "127.0.0.1";
int port = int.TryParse(args.ElementAtOrDefault(1), out int value) ? value : 6613;
string stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
var results = new List<(string Name, bool Passed, string Detail)>();
Console.WriteLine($"PHASE 3 INTEGRATION TEST — {host}:{port}");

try
{
    await using var first = await PlayerProbe.CreateAsync(host, port, $"p3a_{stamp}", "Phase3-Test", "เดินหนึ่ง");
    EnterData firstEnter = await first.EnterAsync();
    SceneSnapshot firstScene = await first.ReceiveSceneAsync();
    Record("First client enters map", firstEnter.MapId == 10801 && firstScene.MapId == 10801, $"map={firstScene.MapId}");
    Record("Server sends map NPC data", firstScene.Npcs.Count >= 3 && firstScene.Npcs.Any(x => x.NpcId == 10001), $"npcs={firstScene.Npcs.Count}");

    await using var second = await PlayerProbe.CreateAsync(host, port, $"p3b_{stamp}", "Phase3-Test", "เดินสอง");
    EnterData secondEnter = await second.EnterAsync();
    SceneSnapshot secondScene = await second.ReceiveSceneAsync();
    bool seesFirst = secondScene.Players.Any(p => p.CharacterId == firstEnter.Id);
    Record("Second client sees first", seesFirst, $"players={secondScene.Players.Count}");

    await first.SendAsync(ClientPackets.Move(620, 820));
    PlayerMovement firstMove = await second.ReceiveMoveAsync();
    Record("Second client sees first move", firstMove == new PlayerMovement(firstEnter.Id, 620, 820), $"id={firstMove.CharacterId} ({firstMove.X},{firstMove.Y})");

    await second.SendAsync(ClientPackets.Move(650, 850));
    PlayerMovement secondMove = await first.ReceiveMoveAsync();
    Record("First client sees second move", secondMove == new PlayerMovement(secondEnter.Id, 650, 850), $"id={secondMove.CharacterId} ({secondMove.X},{secondMove.Y})");

    await first.SendAsync(ClientPackets.Warp(10802, 300, 400));
    SceneSnapshot warped = await first.ReceiveSceneAsync();
    PlayerMovement warpConfirm = await first.ReceiveMoveAsync();
    Record("Warp to second map", warped.MapId == 10802 && warpConfirm.X == 300 && warpConfirm.Y == 400, $"map={warped.MapId} ({warpConfirm.X},{warpConfirm.Y})");

    await first.SendAsync(ClientPackets.Warp(10801, 570, 770));
    SceneSnapshot returned = await first.ReceiveSceneAsync();
    PlayerMovement returnConfirm = await first.ReceiveMoveAsync();
    Record("Warp back to first map", returned.MapId == 10801 && returnConfirm.X == 570 && returnConfirm.Y == 770, $"map={returned.MapId} ({returnConfirm.X},{returnConfirm.Y})");
}
catch (Exception ex)
{
    Record("Phase 3 flow", false, $"{ex.GetType().Name}: {ex.Message}");
}

int failed = results.Count(x => !x.Passed);
Console.WriteLine($"\nRESULT  {results.Count - failed}/{results.Count} passed");
return failed == 0 ? 0 : 1;

void Record(string name, bool passed, string detail)
{
    results.Add((name, passed, detail));
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {name} — {detail}");
}

sealed record EnterData(int Id, int MapId, short X, short Y);

sealed class PlayerProbe : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly List<byte> _buffer = [];
    private int _characterId;
    private PlayerProbe(TcpClient client) { _client = client; _stream = client.GetStream(); }

    public static async Task<PlayerProbe> CreateAsync(string host, int port, string account, string password, string name)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var tcp = new TcpClient { NoDelay = true };
        await tcp.ConnectAsync(host, port, timeout.Token);
        var probe = new PlayerProbe(tcp);
        await probe.SendAsync(ClientPackets.Handshake(258));
        await probe.ExpectAsync(1, 16);
        await probe.SendAsync(ClientPackets.Login(account, password));
        GamePacket login = await probe.ExpectAsync(1, 1);
        var loginReader = new PacketReader(login.Payload);
        if (loginReader.ReadByte() != 1) throw new InvalidDataException("Login failed");
        GamePacket list = await probe.ExpectAsync(3, 1);
        if (list.Payload[0] == 0)
        {
            await probe.SendAsync(ClientPackets.CreateCharacter(new CharacterCreation(name, 1, 2, 1, 1, 1)));
            list = await probe.ExpectAsync(3, 1);
        }
        var reader = new PacketReader(list.Payload);
        if (reader.ReadByte() < 1) throw new InvalidDataException("Character creation failed");
        probe._characterId = reader.ReadInt32();
        return probe;
    }

    public async Task<EnterData> EnterAsync()
    {
        await SendAsync(ClientPackets.SelectCharacter(_characterId));
        GamePacket packet = await ExpectAsync(3, 2);
        var r = new PacketReader(packet.Payload);
        int id = r.ReadInt32(); r.ReadFixedString(16); r.ReadByte(); r.ReadByte();
        for (int i = 0; i < 12; i++) r.ReadInt32();
        r.ReadInt64(); r.ReadInt64();
        return new EnterData(id, r.ReadInt32(), r.ReadInt16(), r.ReadInt16());
    }

    public async Task<SceneSnapshot> ReceiveSceneAsync() => ServerPackets.ReadScene((await ExpectAsync(7, 1)).Payload);
    public async Task<PlayerMovement> ReceiveMoveAsync() => ServerPackets.ReadMovement((await ExpectAsync(6, 1)).Payload);
    public Task SendAsync(byte[] frame) => _stream.WriteAsync(frame).AsTask();

    private async Task<GamePacket> ExpectAsync(byte main, byte sub)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var chunk = new byte[4096];
        while (true)
        {
            while (FrameCodec.TryDecode(_buffer, out var packet) && packet is not null)
                if (packet.MainKind == main && packet.SubKind == sub) return packet;
            int count = await _stream.ReadAsync(chunk, timeout.Token);
            if (count == 0) throw new IOException("Server disconnected");
            _buffer.AddRange(chunk.AsSpan(0, count).ToArray());
        }
    }

    public ValueTask DisposeAsync() { _stream.Dispose(); _client.Dispose(); return ValueTask.CompletedTask; }
}
