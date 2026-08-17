using System.Collections.Concurrent;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

/// <summary>Phase 7 systems not covered by legacy handlers. C/S:100-001.</summary>
public sealed class CoreGameSystemsHandler(
    ICharacterRepository characters,
    IInventoryRepository inventory,
    IPetRepository pets) : IPacketHandler
{
    public int MainKind => 100;
    public int SubKind => 1;
    private readonly ConcurrentDictionary<int, long> _bank = new();
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, int>> _skills = new();
    private readonly ConcurrentDictionary<int, HashSet<int>> _friends = new();
    private readonly ConcurrentDictionary<int, List<string>> _mail = new();
    private readonly ConcurrentDictionary<int, string> _guild = new();

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        if (session.CharacterId == 0 || data.Length < 1) return;
        var command = ParseCommand(data.Span);
        if (command is null) return;
        byte action = command.Value.Action;
        bool ok = true; long value = 0; string message;
        var player = await characters.GetCharacterByIdAsync(session.CharacterId);
        if (player is null) return;
        switch (action)
        {
            case 1: // skill upgrade
                int skillId = command.Value.Number;
                var skillMap = _skills.GetOrAdd(session.CharacterId, _ => new());
                value = skillMap.AddOrUpdate(skillId, 1, (_, level) => Math.Min(10, level + 1));
                message = $"อัปเกรดสกิล {skillId} เป็นระดับ {value}"; break;
            case 2: // pet roster
                var petList = await pets.GetPetsAsync(session.CharacterId); value = petList.Count;
                message = petList.Count == 0 ? "ยังไม่มีขุนพล" : string.Join(", ", petList.Select(x => x.CustomName)); break;
            case 3: // shop buy fixed item
                int itemId = command.Value.Number; int count = command.Value.Count; long cost = Math.Max(1, count) * 10L;
                if (player.Gold < cost) { ok = false; message = "เงินไม่เพียงพอ"; break; }
                var items = (await inventory.GetItemsAsync(session.CharacterId, 1)).ToList();
                int slot = items.Count == 0 ? 1 : items.Max(x => x.Slot) + 1;
                items.Add(new TsmServer.Domain.ValueObjects.ThingData(slot, itemId, count));
                await inventory.SaveItemsAsync(session.CharacterId, 1, items);
                await characters.UpdateGoldAsync(session.CharacterId, player.Gold - cost); value = player.Gold - cost;
                message = $"ซื้อไอเทม {itemId} จำนวน {count}"; break;
            case 4: // bank, signed amount: positive deposit, negative withdraw
                long amount = command.Value.Amount; long balance = _bank.GetOrAdd(session.CharacterId, 0);
                if (amount > 0 && player.Gold >= amount) { balance += amount; await characters.UpdateGoldAsync(session.CharacterId, player.Gold - amount); }
                else if (amount < 0 && balance >= -amount) { balance += amount; await characters.UpdateGoldAsync(session.CharacterId, player.Gold - amount); }
                else ok = false;
                _bank[session.CharacterId] = balance; value = balance; message = ok ? $"ยอดธนาคาร {balance}" : "ทำรายการธนาคารไม่ได้"; break;
            case 5: // friend
                int friendId = command.Value.Number; lock (_friends) _friends.GetOrAdd(session.CharacterId, _ => []).Add(friendId);
                value = friendId; message = $"เพิ่มเพื่อน #{friendId}"; break;
            case 6: // mail
                string mail = command.Value.Text; lock (_mail) _mail.GetOrAdd(session.CharacterId, _ => []).Add(mail);
                value = _mail[session.CharacterId].Count; message = "ส่งจดหมายแล้ว"; break;
            case 7: // trade confirmation
                int tradeItem = command.Value.Number; int tradeCount = command.Value.Count; value = tradeItem;
                message = $"ยืนยันการค้าไอเทม {tradeItem} จำนวน {tradeCount}"; break;
            case 8: // guild
                string guild = command.Value.Text; _guild[session.CharacterId] = guild; value = 1; message = $"เข้ากิลด์ {guild}"; break;
            case 9: // leaderboard
                value = player.Level * 1000L + player.Exp; message = $"อันดับคะแนน {value}"; break;
            default: ok = false; message = "ไม่รู้จักคำสั่งระบบเกม"; break;
        }
        var payload = new PacketWriter().WriteByte(action).WriteByte((byte)(ok ? 1 : 0)).WriteInt64LE(value).WriteString(message, 96).ToArray();
        await session.SendAsync(FrameCodec.EncodeFrame(100, 1, payload));
    }

    private static Command? ParseCommand(ReadOnlySpan<byte> data)
    {
        byte action = data[0];
        var r = new PacketReader(data[1..]);
        return action switch
        {
            1 or 5 when r.Remaining >= 4 => new(action, r.ReadInt32LE(), 0, 0, string.Empty),
            3 or 7 when r.Remaining >= 6 => new(action, r.ReadInt32LE(), r.ReadInt16LE(), 0, string.Empty),
            4 when r.Remaining >= 8 => new(action, 0, 0, r.ReadInt64LE(), string.Empty),
            6 when r.Remaining >= 64 => new(action, 0, 0, 0, r.ReadString(64)),
            8 when r.Remaining >= 32 => new(action, 0, 0, 0, r.ReadString(32)),
            2 or 9 => new(action, 0, 0, 0, string.Empty),
            _ => null
        };
    }

    private readonly record struct Command(byte Action, int Number, short Count, long Amount, string Text);
}
