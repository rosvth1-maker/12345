using System.Collections.Concurrent;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.ValueObjects;

namespace TsmServer.GameLogic.Systems;

public class WorldManager
{
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, IGameSession>> _mapSessions = new();

    public void EnterMap(int mapId, IGameSession session)
    {
        LeaveCurrentMap(session);
        session.CurrentMapId = mapId;
        var map = _mapSessions.GetOrAdd(mapId, _ => new ConcurrentDictionary<string, IGameSession>());
        map[session.SessionId] = session;
    }

    public void LeaveCurrentMap(IGameSession session)
    {
        if (_mapSessions.TryGetValue(session.CurrentMapId, out var map))
        {
            map.TryRemove(session.SessionId, out _);
        }
    }

    public IReadOnlyList<PlayerDataDto> GetPlayersInMap(int mapId, string excludeSessionId = "")
    {
        if (_mapSessions.TryGetValue(mapId, out var map))
        {
            return map.Values
                .Where(s => s.SessionId != excludeSessionId && s.PlayerData != null)
                .Select(s => s.PlayerData!)
                .ToList();
        }
        return Array.Empty<PlayerDataDto>();
    }

    public async ValueTask BroadcastToMapAsync(int mapId, byte[] packet, string excludeSessionId = "")
    {
        if (_mapSessions.TryGetValue(mapId, out var map))
        {
            foreach (var session in map.Values)
            {
                if (session.SessionId != excludeSessionId)
                {
                    await session.SendAsync(packet);
                }
            }
        }
    }
}
