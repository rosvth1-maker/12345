using System.Net.Sockets;
using TsmClient.Domain;
using TsmClient.Protocol;

string host = args.ElementAtOrDefault(0) ?? "127.0.0.1";
int port = int.TryParse(args.ElementAtOrDefault(1), out int parsedPort) ? parsedPort : 6613;
string account = $"phase2_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
const string password = "Phase2-Test-Only";
const string characterName = "ระยะสอง";
var results = new List<(string Name, bool Passed, string Detail)>();
int characterId = 0;

Console.WriteLine($"PHASE 2 INTEGRATION TEST — {host}:{port}");

try
{
    await using var probe = await ServerProbe.ConnectAndLoginAsync(host, port, account, password);
    var initialList = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
    bool emptyPassed = initialList is { MainKind: 3, SubKind: 1 } && initialList.Payload.Length >= 1 && initialList.Payload[0] == 0;
    Record("Empty character list", emptyPassed, emptyPassed ? "count=0" : "expected empty list");

    var creation = new CharacterCreation(characterName, Gender: 1, Element: 2, Hair: 3, HairColor: 4, SkinColor: 5);
    await probe.SendAsync(ClientPackets.CreateCharacter(creation));
    var createdList = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
    bool listOpcode = createdList is { MainKind: 3, SubKind: 1 } && createdList.Payload.Length >= 1 && createdList.Payload[0] == 1;
    Record("Character list after create", listOpcode, listOpcode ? "count=1" : "wrong list response");
    if (listOpcode)
    {
        var character = ReadFirstCharacter(createdList.Payload);
        characterId = character.Id;
        Record("Thai name round trip", character.Name == characterName, $"name={character.Name}");
        Record("Appearance round trip", character.Gender == 1 && character.Element == 2 && character.Hair == 3 && character.HairColor == 4 && character.SkinColor == 5,
            $"g={character.Gender} e={character.Element} h={character.Hair} hc={character.HairColor} sc={character.SkinColor}");
    }
}
catch (Exception ex)
{
    Record("Create character flow", false, ex.GetType().Name);
}

try
{
    await using var probe = await ServerProbe.ConnectAndLoginAsync(host, port, account, password);
    var list = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
    bool persisted = list is { MainKind: 3, SubKind: 1 } && list.Payload.Length >= 1 && list.Payload[0] == 1;
    Record("Character list after reconnect", persisted, persisted ? "count=1" : "character missing");
    if (persisted && characterId == 0) characterId = ReadFirstCharacter(list.Payload).Id;

    await probe.SendAsync(ClientPackets.SelectCharacter(characterId));
    var enterGame = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
    bool enterOpcode = enterGame is { MainKind: 3, SubKind: 2 };
    Record("Enter game opcode", enterOpcode, enterOpcode ? "3/2" : $"{enterGame.MainKind}/{enterGame.SubKind}");
    if (enterOpcode)
    {
        var data = ReadEnterGame(enterGame.Payload);
        Record("Enter game character", data.Id == characterId && data.Name == characterName, $"id={data.Id} name={data.Name}");
        Record("Enter game base stats", data.Level == 1 && data.Hp == 100 && data.MaxHp == 100 && data.Sp == 50 && data.MaxSp == 50,
            $"lv={data.Level} hp={data.Hp}/{data.MaxHp} sp={data.Sp}/{data.MaxSp}");
        Record("Enter game location", data.MapId == 10801 && data.X == 570 && data.Y == 770, $"map={data.MapId} x={data.X} y={data.Y}");
    }
}
catch (Exception ex)
{
    Record("Reconnect/select flow", false, ex.GetType().Name);
}

int failed = results.Count(r => !r.Passed);
Console.WriteLine($"\nRESULT  {results.Count - failed}/{results.Count} passed");
foreach (var result in results.Where(r => !r.Passed)) Console.WriteLine($"FAILED  {result.Name}: {result.Detail}");
return failed == 0 ? 0 : 1;

void Record(string name, bool passed, string detail)
{
    results.Add((name, passed, detail));
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {name} — {detail}");
}

static CharacterSummary ReadFirstCharacter(byte[] payload)
{
    var reader = new PacketReader(payload);
    if (reader.ReadByte() < 1) throw new InvalidDataException("Character list is empty");
    return new CharacterSummary(reader.ReadInt32(), reader.ReadFixedString(16), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(),
        reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadInt32());
}

static EnterGameData ReadEnterGame(byte[] payload)
{
    if (payload.Length < 94) throw new InvalidDataException($"Enter Game payload is too short: {payload.Length}");
    var r = new PacketReader(payload);
    int id = r.ReadInt32(); string name = r.ReadFixedString(16); byte level = r.ReadByte(); byte element = r.ReadByte();
    int hp = r.ReadInt32(); int maxHp = r.ReadInt32(); int sp = r.ReadInt32(); int maxSp = r.ReadInt32();
    for (int i = 0; i < 8; i++) r.ReadInt32();
    long exp = r.ReadInt64(); long gold = r.ReadInt64(); int map = r.ReadInt32(); short x = r.ReadInt16(); short y = r.ReadInt16();
    return new EnterGameData(id, name, level, element, hp, maxHp, sp, maxSp, exp, gold, map, x, y);
}

sealed record EnterGameData(int Id, string Name, byte Level, byte Element, int Hp, int MaxHp, int Sp, int MaxSp, long Exp, long Gold, int MapId, short X, short Y);

sealed class ServerProbe : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly List<byte> _buffer = [];
    private ServerProbe(TcpClient client) { _client = client; _stream = client.GetStream(); }

    public static async Task<ServerProbe> ConnectAndLoginAsync(string host, int port, string account, string password)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(host, port, timeout.Token);
        var probe = new ServerProbe(client);
        await probe.SendAsync(ClientPackets.Handshake(258));
        var handshake = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
        if (handshake is not { MainKind: 1, SubKind: 16 }) throw new InvalidDataException("Handshake failed");
        await probe.SendAsync(ClientPackets.Login(account, password));
        var login = await probe.ReceiveAsync(TimeSpan.FromSeconds(5));
        var reader = new PacketReader(login.Payload);
        if (login is not { MainKind: 1, SubKind: 1 } || reader.ReadByte() != 1) throw new InvalidDataException("Login failed");
        return probe;
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
    public ValueTask DisposeAsync() { _stream.Dispose(); _client.Dispose(); return ValueTask.CompletedTask; }
}
