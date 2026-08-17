using System.Net.Sockets;
using TsmClient.Domain;
using TsmClient.Protocol;

var results = new List<(string Name, bool Pass, string Detail)>();
string testPassword = Environment.GetEnvironmentVariable("TSM_DEV_ADMIN_PASSWORD") ?? throw new InvalidOperationException("Set TSM_DEV_ADMIN_PASSWORD before running this integration test.");
await using var p = await Probe.LoginAsync("127.0.0.1", 6613, "admin", testPassword);
GamePacket list = await p.ExpectAsync(3, 1); var lr = new PacketReader(list.Payload); lr.ReadByte(); int charId = lr.ReadInt32();
await p.SendAsync(ClientPackets.SelectCharacter(charId)); await p.ExpectAsync(3, 2); await p.ExpectAsync(7, 1);

var npcs = new[] { (10001, 5001, "ครูฝึกมือใหม่ประจำเมือง"), (10002, 5002, "ผู้ใหญ่บ้านจัวจวิ้น"), (10003, 5003, "พ่อค้าเร่แห่งแดนสามก๊ก") };
foreach (var (npcId, questId, expectedName) in npcs)
{
    await p.SendAsync(ClientPackets.TalkToNpc(npcId));
    NpcDialog dialog = ServerPackets.ReadNpcDialog((await p.ExpectAsync(22, 2)).Payload);
    Record($"Thai dialogue NPC {npcId}", dialog.NpcId == npcId && dialog.QuestId == questId && dialog.NpcName == expectedName && dialog.Options.Count == 2 && dialog.Text.Contains(expectedName), dialog.NpcName);
    await p.SendAsync(ClientPackets.Mission(questId, 1));
    QuestState accepted = ServerPackets.ReadQuest((await p.ExpectAsync(47, 1)).Payload);
    Record($"Accept quest {questId}", accepted.Status == 1 && accepted.MissionId == questId, accepted.Title);
    await p.SendAsync(ClientPackets.Mission(questId, 2));
    QuestState completed = ServerPackets.ReadQuest((await p.ExpectAsync(47, 1)).Payload);
    Record($"Complete quest {questId}", completed.Status == 2 && completed.Reward > 0, $"reward={completed.Reward}");
}
int failed = results.Count(x => !x.Pass); Console.WriteLine($"\nRESULT {results.Count - failed}/{results.Count} passed"); return failed;
void Record(string n, bool ok, string d) { results.Add((n, ok, d)); Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {n} — {d}"); }

sealed class Probe : IAsyncDisposable
{
    readonly TcpClient c; readonly NetworkStream s; readonly List<byte> b = [];
    Probe(TcpClient c) { this.c = c; s = c.GetStream(); }
    public static async Task<Probe> LoginAsync(string h, int port, string a, string pw)
    {
        var c = new TcpClient { NoDelay = true }; await c.ConnectAsync(h, port); var p = new Probe(c);
        await p.SendAsync(ClientPackets.Handshake(258)); await p.ExpectAsync(1, 16);
        await p.SendAsync(ClientPackets.Login(a, pw)); var login = await p.ExpectAsync(1, 1); var r = new PacketReader(login.Payload);
        if (r.ReadByte() != 1) throw new InvalidDataException("login failed"); return p;
    }
    public Task SendAsync(byte[] f) => s.WriteAsync(f).AsTask();
    public async Task<GamePacket> ExpectAsync(byte main, byte sub)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6)); var chunk = new byte[4096];
        while (true)
        {
            while (FrameCodec.TryDecode(b, out var p) && p != null) if (p.MainKind == main && p.SubKind == sub) return p;
            int n = await s.ReadAsync(chunk, timeout.Token); if (n == 0) throw new IOException("disconnected"); b.AddRange(chunk.AsSpan(0, n).ToArray());
        }
    }
    public ValueTask DisposeAsync() { s.Dispose(); c.Dispose(); return ValueTask.CompletedTask; }
}
