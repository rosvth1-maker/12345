using TsmServer.Data;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.GameLogic.Systems;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

/// <summary>
/// Handles Item Usage, Equipment, and Inventory Management (C:005-001, C:005-002, C:005-003, C:005-004)
/// </summary>
public class ItemOperationHandler : IPacketHandler
{
    private readonly InventorySystem _inventorySystem;
    private readonly ICharacterRepository _charRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly GameDataManager _gameData;
    private readonly IResponseSender _responseSender;

    public int MainKind => 5;
    public int SubKind => 1;

    public ItemOperationHandler(
        InventorySystem inventorySystem,
        ICharacterRepository charRepo,
        IInventoryRepository inventoryRepo,
        GameDataManager gameData,
        IResponseSender responseSender)
    {
        _inventorySystem = inventorySystem;
        _charRepo = charRepo;
        _inventoryRepo = inventoryRepo;
        _gameData = gameData;
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        if (session.CharacterId == 0) return;

        var reader = new PacketReader(data.Span);
        if (reader.Remaining < 2) return;

        int opType = reader.ReadByte(); // 1=Use, 2=Equip, 3=Unequip, 4=Discard
        int slot = reader.ReadByte();

        var player = await _charRepo.GetCharacterByIdAsync(session.CharacterId);
        if (player == null) return;

        int sourceBag = opType == 3 ? 2 : 1;
        var items = await _inventoryRepo.GetItemsAsync(session.CharacterId, sourceBag);
        var targetItem = items.FirstOrDefault(i => i.Slot == slot);

        if (targetItem == null) return;

        if (opType == 1) // Use Item (e.g. Bun / Potion)
        {
            if (_gameData.Items.TryGetValue(targetItem.ItemId, out var itemDef))
            {
                int healHp = itemDef.Hp;
                int healSp = itemDef.Sp;

                int newHp = Math.Min(player.Hp + healHp, player.MaxHp);
                int newSp = Math.Min(player.Sp + healSp, player.MaxSp);

                var updatedPlayer = player with { Hp = newHp, Sp = newSp };
                await _charRepo.SavePlayerDataAsync(updatedPlayer);

                // Consume 1 item count
                if (targetItem.Count > 1)
                {
                    await _inventoryRepo.UpdateItemSlotAsync(session.CharacterId, 1, targetItem with { Count = targetItem.Count - 1 });
                }
                else
                {
                    await _inventoryRepo.DeleteItemSlotAsync(session.CharacterId, 1, slot);
                }

                await _responseSender.SendSystemNoticeAsync(session, $"คุณได้ใช้ {itemDef.Name} ฟื้นฟู HP +{healHp}, SP +{healSp}");
            }
        }
        else if (opType == 2) // Equip
        {
            await _inventoryRepo.DeleteItemSlotAsync(session.CharacterId, 1, slot);
            await _inventoryRepo.UpdateItemSlotAsync(session.CharacterId, 2, targetItem);
            await _responseSender.SendSystemNoticeAsync(session, "สวมใส่อุปกรณ์เรียบร้อยแล้ว");
        }
        else if (opType == 3) // Unequip
        {
            await _inventoryRepo.DeleteItemSlotAsync(session.CharacterId, 2, slot);
            await _inventoryRepo.UpdateItemSlotAsync(session.CharacterId, 1, targetItem);
            await _responseSender.SendSystemNoticeAsync(session, "ถอดอุปกรณ์เรียบร้อยแล้ว");
        }
        else if (opType == 4) // Discard / Drop Item
        {
            await _inventoryRepo.DeleteItemSlotAsync(session.CharacterId, 1, slot);
            await _responseSender.SendSystemNoticeAsync(session, "ทิ้งไอเทมเรียบร้อยแล้ว");
        }

        // Refresh client inventory
        var updatedItems = await _inventoryRepo.GetItemsAsync(session.CharacterId, 1);
        await _responseSender.SendInventoryAsync(session, updatedItems);
    }
}
