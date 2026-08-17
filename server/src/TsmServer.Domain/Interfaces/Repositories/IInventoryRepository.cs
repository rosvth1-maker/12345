using TsmServer.Domain.ValueObjects;

namespace TsmServer.Domain.Interfaces.Repositories;

public interface IInventoryRepository
{
    Task<IReadOnlyList<ThingData>> GetItemsAsync(int characterId, int bagType);
    Task SaveItemsAsync(int characterId, int bagType, IReadOnlyList<ThingData> items);
    Task UpdateItemSlotAsync(int characterId, int bagType, ThingData item);
    Task DeleteItemSlotAsync(int characterId, int bagType, int slot);
}
