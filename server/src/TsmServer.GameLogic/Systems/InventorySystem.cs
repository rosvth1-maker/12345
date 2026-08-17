using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.Domain.ValueObjects;

namespace TsmServer.GameLogic.Systems;

public class InventorySystem
{
    private readonly IInventoryRepository _repo;

    public InventorySystem(IInventoryRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<ThingData>> GetBagItemsAsync(int characterId)
    {
        return await _repo.GetItemsAsync(characterId, 1);
    }

    public async Task<bool> AddItemAsync(int characterId, int itemId, int count = 1)
    {
        var items = (await _repo.GetItemsAsync(characterId, 1)).ToList();

        // Find existing stack
        var existing = items.FirstOrDefault(x => x.ItemId == itemId && x.Count < 999);
        if (existing != null)
        {
            var updated = existing with { Count = existing.Count + count };
            await _repo.UpdateItemSlotAsync(characterId, 1, updated);
            return true;
        }

        // Find empty slot (1..25)
        var usedSlots = items.Select(x => x.Slot).ToHashSet();
        for (int slot = 1; slot <= 25; slot++)
        {
            if (!usedSlots.Contains(slot))
            {
                var newItem = new ThingData(slot, itemId, count);
                await _repo.UpdateItemSlotAsync(characterId, 1, newItem);
                return true;
            }
        }

        return false; // Bag full
    }

    public async Task<bool> RemoveItemAsync(int characterId, int slot, int count = 1)
    {
        var items = await _repo.GetItemsAsync(characterId, 1);
        var item = items.FirstOrDefault(x => x.Slot == slot);
        if (item == null || item.Count < count) return false;

        if (item.Count == count)
        {
            await _repo.DeleteItemSlotAsync(characterId, 1, slot);
        }
        else
        {
            await _repo.UpdateItemSlotAsync(characterId, 1, item with { Count = item.Count - count });
        }
        return true;
    }
}
