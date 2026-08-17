using TsmServer.Data;
using TsmServer.Domain.Constants;
using TsmServer.Domain.Interfaces;
using TsmServer.GameLogic.Systems;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

public class NpcEventHandler : IPacketHandler
{
    private readonly GameDataManager _dataManager;
    private readonly IResponseSender _responseSender;

    public int MainKind => Opcodes.NpcEvent;
    public int SubKind => 1;

    public NpcEventHandler(GameDataManager dataManager, IResponseSender responseSender)
    {
        _dataManager = dataManager;
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        var reader = new PacketReader(data.Span);
        int clickId = reader.ReadInt32LE();

        // Sample NPC dialogue trigger
        await _responseSender.SendChatMessageAsync(session, (int)Domain.Enums.ChatChannel.System, "NPC", $"สวัสดีจอมยุทธ์ ข้าคือ NPC (รหัส: {clickId}) ยินดีต้อนรับสู่ TS Dark World!");
    }
}
