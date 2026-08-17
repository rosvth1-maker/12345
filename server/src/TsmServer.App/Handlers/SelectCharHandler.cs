using TsmServer.Domain.Constants;
using TsmServer.Domain.Enums;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.GameLogic.Systems;
using TsmServer.Network;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

public class SelectCharHandler : IPacketHandler
{
    private readonly ICharacterRepository _charRepo;
    private readonly IInventoryRepository _invRepo;
    private readonly WorldManager _worldManager;
    private readonly SessionManager _sessionManager;
    private readonly IResponseSender _responseSender;

    public int MainKind => Opcodes.Character;
    public int SubKind => 1;

    public SelectCharHandler(
        ICharacterRepository charRepo,
        IInventoryRepository invRepo,
        WorldManager worldManager,
        SessionManager sessionManager,
        IResponseSender responseSender)
    {
        _charRepo = charRepo;
        _invRepo = invRepo;
        _worldManager = worldManager;
        _sessionManager = sessionManager;
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        var reader = new PacketReader(data.Span);
        int charId = reader.ReadInt32LE();

        var player = await _charRepo.GetCharacterByIdAsync(charId);
        if (player == null) return;

        session.CharacterId = player.CharacterId;
        session.CharacterName = player.Name;
        session.PlayerData = player;
        session.CurrentMapId = player.MapId;
        session.X = player.X;
        session.Y = player.Y;
        session.State = SessionState.InGame;

        _sessionManager.RegisterPlayer(player.CharacterId, session);
        _worldManager.EnterMap(player.MapId, session);

        await _responseSender.SendEnterGameAsync(session, player);

        // Send Scene and other players in map
        var otherPlayers = _worldManager.GetPlayersInMap(player.MapId, session.SessionId);
        await _responseSender.SendSceneInfoAsync(session, player.MapId, otherPlayers);

        // Send Inventory
        var items = await _invRepo.GetItemsAsync(player.CharacterId, 1);
        await _responseSender.SendInventoryAsync(session, items);

        await _responseSender.SendSystemNoticeAsync(session, "ยินดีต้อนรับสู่ TS Dark World ภาษาไทย 100%!");
    }
}
