using TsmServer.Domain.ValueObjects;

namespace TsmServer.Domain.Interfaces.Repositories;

public interface IPetRepository
{
    Task<IReadOnlyList<FollowNpcData>> GetPetsAsync(int characterId);
    Task SavePetsAsync(int characterId, IReadOnlyList<FollowNpcData> pets);
    Task AddPetAsync(int characterId, FollowNpcData pet);
    Task RemovePetAsync(int characterId, int slot);
}
