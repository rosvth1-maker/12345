namespace TsmServer.Domain.Interfaces.Repositories;

public interface IBitFlagRepository
{
    Task<IReadOnlyDictionary<int, int>> GetBitFlagsAsync(int characterId);
    Task SetBitFlagAsync(int characterId, int flagId, int value);
}
