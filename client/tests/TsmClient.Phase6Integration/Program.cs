using System.Net.Sockets;
using TsmClient.Domain;
using TsmClient.Protocol;
var results = new List<(string, bool, string)>();
await using (var p = await Probe.EnterAsync())
{
    await p.Send(ClientPackets.BattleCommand(0)); var start = ServerPackets.ReadBattleStart((await p.Expect(53, 1)).Payload);
    Rec("Battle starts", start.EnemyId == 11001 && start.EnemyHp == 120 && start.PlayerHp > 0, $"{start.EnemyName} HP={start.EnemyHp}");
    Rec("Companion present", start.CompanionPresent, "บาโตวเยา");
    await p.Send(ClientPackets.BattleCommand(3)); var guard = ServerPackets.ReadBattleAction((await p.Expect(51, 2)).Payload); Rec("Guard command", guard.Action == 3 && guard.Damage == 0, $"enemyHP={guard.EnemyHp}");
    await p.Send(ClientPackets.BattleCommand(5)); var item = ServerPackets.ReadBattleAction((await p.Expect(51, 2)).Payload); Rec("Item command", item.Action == 5 && item.Damage == 15, $"enemyHP={item.EnemyHp}");
    await p.Send(ClientPackets.BattleCommand(1)); var attack = ServerPackets.ReadBattleAction((await p.Expect(51, 2)).Payload); Rec("Attack command", attack.Action == 1 && attack.Damage > 0, $"damage={attack.Damage}");
    while (attack.EnemyHp > 0) { await p.Send(ClientPackets.BattleCommand(2, 0, 0, 104)); attack = ServerPackets.ReadBattleAction((await p.Expect(51, 2)).Payload); }
    var reward = ServerPackets.ReadBattleReward((await p.Expect(53, 2)).Payload); Rec("Victory rewards", reward.Victory && reward.Exp == 50 && reward.Gold == 25 && reward.ItemId == 10001, $"EXP={reward.Exp} Gold={reward.Gold} Item={reward.ItemId}");
    await p.Send(ClientPackets.BattleCommand(0)); await p.Expect(53, 1); await p.Send(ClientPackets.BattleCommand(4)); Rec("Escape returns safe", (await p.Expect(53, 3)).Payload[0] == 1, "safe");
}
await using (var p = await Probe.EnterAsync()) { await p.Send(ClientPackets.BattleCommand(0)); await p.Expect(53, 1); }
await using (var p = await Probe.EnterAsync()) { await p.Send(ClientPackets.BattleCommand(0)); var s = ServerPackets.ReadBattleStart((await p.Expect(53, 1)).Payload); Rec("Reconnect after battle disconnect", s.EnemyHp == 120, "fresh battle state"); await p.Send(ClientPackets.BattleCommand(4)); await p.Expect(53, 3); }
int failed = results.Count(x => !x.Item2); Console.WriteLine($"\nRESULT {results.Count - failed}/{results.Count} passed"); return failed;
void Rec(string n, bool ok, string d) { results.Add((n, ok, d)); Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {n} — {d}"); }
sealed class Probe : IAsyncDisposable
{
    readonly TcpClient c; readonly NetworkStream s; readonly List<byte> b = []; Probe(TcpClient c) { this.c = c; s = c.GetStream(); }
    public static async Task<Probe> EnterAsync() { string password = Environment.GetEnvironmentVariable("TSM_DEV_ADMIN_PASSWORD") ?? throw new InvalidOperationException("Set TSM_DEV_ADMIN_PASSWORD before running this integration test."); var c = new TcpClient { NoDelay = true }; await c.ConnectAsync("127.0.0.1", 6613); var p = new Probe(c); await p.Send(ClientPackets.Handshake(258)); await p.Expect(1, 16); await p.Send(ClientPackets.Login("admin", password)); await p.Expect(1, 1); var l = await p.Expect(3, 1); var r = new PacketReader(l.Payload); r.ReadByte(); int id = r.ReadInt32(); await p.Send(ClientPackets.SelectCharacter(id)); await p.Expect(3, 2); await p.Expect(7, 1); return p; }
    public Task Send(byte[] f) => s.WriteAsync(f).AsTask(); public async Task<GamePacket> Expect(byte m, byte sub) { using var t = new CancellationTokenSource(TimeSpan.FromSeconds(6)); var x = new byte[4096]; while (true) { while (FrameCodec.TryDecode(b, out var p) && p != null) if (p.MainKind == m && p.SubKind == sub) return p; int n = await s.ReadAsync(x, t.Token); if (n == 0) throw new IOException(); b.AddRange(x.AsSpan(0, n).ToArray()); } }
    public ValueTask DisposeAsync() { s.Dispose(); c.Dispose(); return ValueTask.CompletedTask; }
}
