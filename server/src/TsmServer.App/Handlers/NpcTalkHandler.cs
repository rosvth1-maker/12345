using TsmServer.Data;
using TsmServer.Domain.Enums;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

/// <summary>
/// Handles NPC clicking and dialogue (C:022-001 / S:022-002).
/// </summary>
public class NpcTalkHandler : IPacketHandler
{
    private readonly GameDataManager _gameData;
    private readonly ICharacterRepository _charRepo;
    private readonly IResponseSender _responseSender;

    public int MainKind => 22;
    public int SubKind => 1;

    public NpcTalkHandler(GameDataManager gameData, ICharacterRepository charRepo, IResponseSender responseSender)
    {
        _gameData = gameData;
        _charRepo = charRepo;
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        if (session.CharacterId == 0) return;

        var reader = new PacketReader(data.Span);
        int npcId = reader.Remaining >= 4 ? reader.ReadInt32LE() : 10001;

        string npcName = "NPC";
        string dialogText = "สวัสดีจอมยุทธ์! ยินดีต้อนรับสู่ดินแดน TS Dark World";

        if (_gameData.Npcs.TryGetValue(npcId, out var npcDef))
        {
            npcName = npcDef.Name;
            dialogText = $"ข้าคือ {npcName} ยินดีที่ได้พบท่านในการผจญภัยครั้งนี้!";
        }

        int questId = npcId switch { 10001 => 5001, 10002 => 5002, 10003 => 5003, _ => 0 };
        string option = questId == 0 ? "ลาก่อน" : "รับภารกิจ";
        var writer = new PacketWriter()
            .WriteInt32LE(npcId)
            .WriteString(npcName, 64)
            .WriteNullTerminatedString(dialogText)
            .WriteByte(2)
            .WriteString(option, 32)
            .WriteString("ไว้คราวหน้า", 32)
            .WriteInt32LE(questId);

        var frame = FrameCodec.EncodeFrame(22, 2, writer.ToArray());
        await session.SendAsync(frame);

        // Also send system announcement for clear feedback
        await _responseSender.SendChatMessageAsync(session, (int)ChatChannel.General, npcName, dialogText);
    }
}
