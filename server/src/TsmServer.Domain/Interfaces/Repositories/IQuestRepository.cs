namespace TsmServer.Domain.Interfaces.Repositories;

public interface IQuestRepository
{
    Task<IReadOnlyDictionary<int, int>> GetActiveMissionsAsync(int characterId);
    Task<IReadOnlySet<int>> GetCompletedMissionFlagsAsync(int characterId);
    Task SetMissionStepAsync(int characterId, int missionId, int step);
    Task SetMissionCompletedAsync(int characterId, int missionId);
}
