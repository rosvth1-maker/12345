using TsmServer.Data;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.GameLogic.Battle;
using TsmServer.Protocol;
using System.Collections.Concurrent;

namespace TsmServer.App.Handlers;

/// <summary>
/// Handles Turn-Based Combat Action Commands (C:050-002, C:051-001)
/// </summary>
public class BattleCommandHandler : IPacketHandler
{
    private readonly ICharacterRepository _charRepo;
    private readonly GameDataManager _gameData;
    private readonly IResponseSender _responseSender;
    private readonly IInventoryRepository _inventory;
    private readonly ConcurrentDictionary<int, int> _enemyHp = new();

    public int MainKind => 50;
    public int SubKind => 2;

    public BattleCommandHandler(ICharacterRepository charRepo, GameDataManager gameData, IResponseSender responseSender, IInventoryRepository inventory)
    {
        _charRepo = charRepo;
        _gameData = gameData;
        _responseSender = responseSender;
        _inventory = inventory;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        if (session.CharacterId == 0) return;

        var reader = new PacketReader(data.Span);
        if (reader.Remaining < 3) return;

        int actionType = reader.ReadByte(); // 1=Attack, 2=Skill, 3=Guard, 4=Escape, 5=Item
        int targetRow = reader.ReadByte();
        int targetCol = reader.ReadByte();
        int skillId = reader.Remaining >= 2 ? reader.ReadUInt16LE() : 101;

        var player = await _charRepo.GetCharacterByIdAsync(session.CharacterId);
        if (player == null) return;

        if (actionType == 0)
        {
            _enemyHp[session.CharacterId] = 120;
            var start = new PacketWriter().WriteInt32LE(11001).WriteString("บาโตวเยา", 32)
                .WriteInt32LE(player.Hp).WriteInt32LE(player.MaxHp).WriteInt32LE(player.Sp).WriteInt32LE(player.MaxSp)
                .WriteInt32LE(120).WriteInt32LE(120).WriteByte(1).ToArray();
            await session.SendAsync(FrameCodec.EncodeFrame(53, 1, start));
            return;
        }
        if (actionType == 4)
        {
            _enemyHp.TryRemove(session.CharacterId, out _);
            await session.SendAsync(FrameCodec.EncodeFrame(53, 3, new PacketWriter().WriteByte(1).ToArray()));
            return;
        }
        if (!_enemyHp.ContainsKey(session.CharacterId)) return;

        // Calculate Damage
        int baseAtk = player.AtkVal;
        int skillPower = 100;
        string skillName = "โจมตีธรรมดา";

        if (_gameData.Skills.TryGetValue(skillId, out var skillDef))
        {
            skillName = skillDef.Name;
            skillPower = skillDef.Power;
        }

        int dealtDamage = BattleDamageCalc.CalculatePhysicalDamage(
            baseAtk,
            player.Level,
            player.Element,
            20,
            1,
            0,
            skillPower);
        if (actionType == 3) dealtDamage = 0;
        if (actionType == 5) dealtDamage = 15;
        int remainingHp = _enemyHp.AddOrUpdate(session.CharacterId, Math.Max(0, 120 - dealtDamage), (_, hp) => Math.Max(0, hp - dealtDamage));

        // Send Combat Action Broadcast Frame (S:051-002)
        var writer = new PacketWriter()
            .WriteInt32LE(session.CharacterId)
            .WriteByte((byte)actionType)
            .WriteByte((byte)targetRow)
            .WriteByte((byte)targetCol)
            .WriteInt32LE(dealtDamage)
            .WriteString(skillName, 32)
            .WriteInt32LE(remainingHp);

        var frame = FrameCodec.EncodeFrame(51, 2, writer.ToArray());
        await session.SendAsync(frame);

        if (remainingHp == 0)
        {
            const long expReward = 50, goldReward = 25;
            var updated = player with { Exp = player.Exp + expReward, Gold = player.Gold + goldReward };
            await _charRepo.SavePlayerDataAsync(updated);
            var items = (await _inventory.GetItemsAsync(session.CharacterId, 1)).ToList();
            int rewardSlot = items.Count == 0 ? 1 : items.Max(x => x.Slot) + 1;
            items.Add(new TsmServer.Domain.ValueObjects.ThingData(rewardSlot, 10001, 1));
            await _inventory.SaveItemsAsync(session.CharacterId, 1, items);
            session.PlayerData = updated;
            _enemyHp.TryRemove(session.CharacterId, out _);
            var end = new PacketWriter().WriteByte(1).WriteInt64LE(expReward).WriteInt64LE(goldReward).WriteInt32LE(10001)
                .WriteInt32LE(updated.Hp).WriteInt32LE(updated.Sp).WriteInt64LE(updated.Exp).ToArray();
            await session.SendAsync(FrameCodec.EncodeFrame(53, 2, end));
        }

        Console.WriteLine($"[Battle] Player #{session.CharacterId} executed {skillName}, dealt {dealtDamage} damage to ({targetRow}, {targetCol})");
    }
}
