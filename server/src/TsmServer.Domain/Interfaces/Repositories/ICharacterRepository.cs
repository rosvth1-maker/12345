using TsmServer.Domain.ValueObjects;

namespace TsmServer.Domain.Interfaces.Repositories;

public interface ICharacterRepository
{
    Task<IReadOnlyList<PlayerDataDto>> GetCharactersByAccountIdAsync(int accountId);
    Task<PlayerDataDto?> GetCharacterByIdAsync(int characterId);
    Task<int> CreateCharacterAsync(int accountId, PlayerDataDto data);
    Task UpdatePositionAsync(int characterId, int mapId, int x, int y);
    Task SavePlayerDataAsync(PlayerDataDto player);
    Task UpdateGoldAsync(int characterId, long gold);
    Task UpdateStatsAsync(int characterId, int hp, int sp, int level, long exp);
}
