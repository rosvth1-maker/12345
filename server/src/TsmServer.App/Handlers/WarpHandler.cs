using TsmServer.Data;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.GameLogic.Systems;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

/// <summary>
/// Handles Map Teleportation & Scene Transitions (C:006-002, C:012-001, C:068-002)
/// </summary>
public class WarpHandler : IPacketHandler
{
    private readonly WorldManager _worldManager;
    private readonly ICharacterRepository _charRepo;
    private readonly GameDataManager _gameData;
    private readonly IResponseSender _responseSender;

    public int MainKind => 6;
    public int SubKind => 2;

    public WarpHandler(WorldManager worldManager, ICharacterRepository charRepo, GameDataManager gameData, IResponseSender responseSender)
    {
        _worldManager = worldManager;
        _charRepo = charRepo;
        _gameData = gameData;
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        if (session.CharacterId == 0) return;

        var reader = new PacketReader(data.Span);
        int targetMap = reader.Remaining >= 4 ? reader.ReadInt32LE() : 10801;
        int targetX = reader.Remaining >= 2 ? reader.ReadInt16LE() : 570;
        int targetY = reader.Remaining >= 2 ? reader.ReadInt16LE() : 770;

        // 1. Move player in WorldManager
        session.X = targetX;
        session.Y = targetY;
        _worldManager.EnterMap(targetMap, session);

        // 2. Update character location in database repository
        await _charRepo.UpdatePositionAsync(session.CharacterId, targetMap, targetX, targetY);

        // 3. Send Scene Change response to client
        var playersInMap = _worldManager.GetPlayersInMap(targetMap);
        await _responseSender.SendSceneInfoAsync(session, targetMap, playersInMap);

        // 4. Send Confirmation Move packet
        await _responseSender.SendPlayerMoveAsync(session, session.CharacterId, targetX, targetY);

        Console.WriteLine($"[Warp] Player #{session.CharacterId} warped to Map {targetMap} at ({targetX}, {targetY})");
    }
}
