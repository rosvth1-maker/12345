using TsmServer.Domain.Constants;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.GameLogic.Systems;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

public class MoveHandler : IPacketHandler
{
    private readonly WorldManager _worldManager;
    private readonly ICharacterRepository _charRepo;

    public int MainKind => Opcodes.Move;
    public int SubKind => 1;

    public MoveHandler(WorldManager worldManager, ICharacterRepository charRepo)
    {
        _worldManager = worldManager;
        _charRepo = charRepo;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        if (session.CharacterId == 0) return;

        var reader = new PacketReader(data.Span);
        int targetX = reader.ReadInt16LE();
        int targetY = reader.ReadInt16LE();

        session.X = targetX;
        session.Y = targetY;
        if (session.PlayerData != null)
        {
            session.PlayerData = session.PlayerData with { X = targetX, Y = targetY };
        }

        // Broadcast movement to all other players in the same map
        var writer = new PacketWriter()
            .WriteInt32LE(session.CharacterId)
            .WriteInt16LE((short)targetX)
            .WriteInt16LE((short)targetY);
        var moveFrame = FrameCodec.EncodeFrame(Opcodes.Move, 1, writer.ToArray());

        await _worldManager.BroadcastToMapAsync(session.CurrentMapId, moveFrame, session.SessionId);
        await _charRepo.UpdatePositionAsync(session.CharacterId, session.CurrentMapId, targetX, targetY);
    }
}
