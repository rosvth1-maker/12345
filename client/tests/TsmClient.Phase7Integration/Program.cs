using System.Net.Sockets;
using TsmClient.Domain;
using TsmClient.Protocol;

var results = new List<(string Name, bool Ok, string Detail)>();
await using var p = await Probe.EnterAsync();

var initial = ServerPackets.ReadInventory((await p.Expect(25, 1)).Payload);
Rec("Inventory loaded", initial.Count >= 2, $"items={initial.Count}");
byte itemSlot = initial.First().Slot;
await p.Send(ClientPackets.ItemOperation(1, itemSlot));
var afterUse = ServerPackets.ReadInventory((await p.Expect(25, 1)).Payload);
Rec("Item use persists", afterUse.Any(x => x.Slot == itemSlot && x.Count == initial.First().Count - 1), $"slot={itemSlot}");

byte equipSlot = initial.Skip(1).First().Slot;
await p.Send(ClientPackets.ItemOperation(2, equipSlot));
var equipped = ServerPackets.ReadInventory((await p.Expect(25, 1)).Payload);
Rec("Equip removes bag item", equipped.All(x => x.Slot != equipSlot), $"slot={equipSlot}");
await p.Send(ClientPackets.ItemOperation(3, equipSlot));
var unequipped = ServerPackets.ReadInventory((await p.Expect(25, 1)).Payload);
Rec("Unequip restores bag item", unequipped.Any(x => x.Slot == equipSlot), $"slot={equipSlot}");

await Core(1, ClientPackets.CoreSystem(1, value: 101), x => x.Success && x.Value == 1, "Skill level 1");
await Core(1, ClientPackets.CoreSystem(1, value: 101), x => x.Success && x.Value == 2, "Skill level persists");
await Core(2, ClientPackets.CoreSystem(2), x => x.Success && x.Value >= 1, "Pet roster");
await p.Send(ClientPackets.PetCommand(1, 1)); var pet = ServerPackets.ReadPet((await p.Expect(15, 1)).Payload);
Rec("Pet deployment", pet.Deployed, pet.Name);
await p.Send(ClientPackets.TeamCommand(1, 1)); var team = ServerPackets.ReadTeam((await p.Expect(13, 1)).Payload);
Rec("Team command", team.Action == 1, team.Name);

await Core(3, ClientPackets.CoreSystem(3, value: 10001, count: 2), x => x.Success, "Shop purchase");
await Core(4, ClientPackets.CoreSystem(4, amount: 100), x => x.Success && x.Value == 100, "Bank deposit");
await Core(4, ClientPackets.CoreSystem(4, amount: -50), x => x.Success && x.Value == 50, "Bank withdraw");
await Core(5, ClientPackets.CoreSystem(5, value: 1), x => x.Success, "Friend system");
await Core(6, ClientPackets.CoreSystem(6, text: "ทดสอบจดหมาย"), x => x.Success && x.Value >= 1, "Mail system");
await Core(7, ClientPackets.CoreSystem(7, value: 10001, count: 1), x => x.Success, "Trade confirmation");
await Core(8, ClientPackets.CoreSystem(8, text: "Dark World"), x => x.Success, "Guild system");
await Core(9, ClientPackets.CoreSystem(9), x => x.Success && x.Value > 0, "Leaderboard");
await p.Send(ClientPackets.Chat(0, "สวัสดีระยะที่เจ็ด")); var chat = ServerPackets.ReadChat((await p.Expect(2, 1)).Payload);
Rec("Chat broadcast", chat.Message.Contains("ระยะที่เจ็ด"), chat.Message);

int failed = results.Count(x => !x.Ok);
Console.WriteLine($"\nRESULT {results.Count - failed}/{results.Count} passed");
return failed;

async Task Core(byte action, byte[] request, Func<TsmClient.Domain.CoreSystemResult, bool> check, string name)
{
    await p.Send(request); var response = ServerPackets.ReadCoreSystem((await p.Expect(100, 1)).Payload);
    Rec(name, response.Action == action && check(response), $"value={response.Value} {response.Message}");
}
void Rec(string name, bool ok, string detail) { results.Add((name, ok, detail)); Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name} — {detail}"); }

sealed class Probe : IAsyncDisposable
{
    readonly TcpClient client; readonly NetworkStream stream; readonly List<byte> buffer = [];
    Probe(TcpClient client) { this.client = client; stream = client.GetStream(); }
    public static async Task<Probe> EnterAsync()
    {
        var client = new TcpClient { NoDelay = true }; await client.ConnectAsync("127.0.0.1", 6613); var p = new Probe(client);
        await p.Send(ClientPackets.Handshake(258)); await p.Expect(1, 16);
        string password = Environment.GetEnvironmentVariable("TSM_DEV_ADMIN_PASSWORD") ?? throw new InvalidOperationException("Set TSM_DEV_ADMIN_PASSWORD before running this integration test.");
        await p.Send(ClientPackets.Login("admin", password)); await p.Expect(1, 1);
        var list = await p.Expect(3, 1); var reader = new PacketReader(list.Payload); reader.ReadByte(); int id = reader.ReadInt32();
        await p.Send(ClientPackets.SelectCharacter(id)); await p.Expect(3, 2); await p.Expect(7, 1); return p;
    }
    public Task Send(byte[] frame) => stream.WriteAsync(frame).AsTask();
    public async Task<GamePacket> Expect(byte main, byte sub)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6)); var chunk = new byte[4096];
        while (true)
        {
            while (FrameCodec.TryDecode(buffer, out var packet) && packet is not null)
                if (packet.MainKind == main && packet.SubKind == sub) return packet;
            int read = await stream.ReadAsync(chunk, timeout.Token); if (read == 0) throw new IOException("Server disconnected");
            buffer.AddRange(chunk.AsSpan(0, read).ToArray());
        }
    }
    public ValueTask DisposeAsync() { stream.Dispose(); client.Dispose(); return ValueTask.CompletedTask; }
}
