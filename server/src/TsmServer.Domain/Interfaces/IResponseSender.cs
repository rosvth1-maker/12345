using TsmServer.Domain.Interfaces;
using TsmServer.Domain.ValueObjects;

namespace TsmServer.Domain.Interfaces;

public interface IResponseSender
{
    ValueTask SendDisconnectAsync(IGameSession session, int cause);
    ValueTask SendLoginResultAsync(IGameSession session, int result, string message);
    ValueTask SendCharListAsync(IGameSession session, IReadOnlyList<PlayerDataDto> characters);
    ValueTask SendEnterGameAsync(IGameSession session, PlayerDataDto player);
    ValueTask SendChatMessageAsync(IGameSession session, int channel, string sender, string message);
    ValueTask SendPlayerMoveAsync(IGameSession session, int characterId, int targetX, int targetY);
    ValueTask SendSceneInfoAsync(IGameSession session, int mapId, IReadOnlyList<PlayerDataDto> otherPlayers);
    ValueTask SendInventoryAsync(IGameSession session, IReadOnlyList<ThingData> items);
    ValueTask SendGoldUpdateAsync(IGameSession session, long gold);
    ValueTask SendSystemNoticeAsync(IGameSession session, string message);
}
