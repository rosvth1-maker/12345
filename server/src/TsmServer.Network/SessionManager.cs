using System.Collections.Concurrent;
using TsmServer.Domain.Interfaces;

namespace TsmServer.Network;

public class SessionManager
{
    private readonly ConcurrentDictionary<string, IGameSession> _sessions = new();
    private readonly ConcurrentDictionary<int, IGameSession> _playerSessions = new();

    public void AddSession(IGameSession session)
    {
        _sessions[session.SessionId] = session;
    }

    public void RemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            if (session.CharacterId > 0)
                _playerSessions.TryRemove(session.CharacterId, out _);
        }
    }

    public void RegisterPlayer(int characterId, IGameSession session)
    {
        _playerSessions[characterId] = session;
    }

    public IGameSession? GetByCharacterId(int characterId)
    {
        _playerSessions.TryGetValue(characterId, out var s);
        return s;
    }

    public IReadOnlyCollection<IGameSession> AllSessions => _sessions.Values.ToList();
    public IReadOnlyCollection<IGameSession> InGameSessions => _playerSessions.Values.ToList();
}
