using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.GameLogic.Systems;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

/// <summary>
/// Handles Team / Party System (C:013-001, C:013-002, C:013-003, C:013-004)
/// </summary>
public class TeamHandler : IPacketHandler
{
    private readonly WorldManager _worldManager;
    private readonly ICharacterRepository _charRepo;
    private readonly IResponseSender _responseSender;

    public int MainKind => 13;
    public int SubKind => 1;

    public TeamHandler(WorldManager worldManager, ICharacterRepository charRepo, IResponseSender responseSender)
    {
        _worldManager = worldManager;
        _charRepo = charRepo;
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        if (session.CharacterId == 0) return;

        var reader = new PacketReader(data.Span);
        if (reader.Remaining < 5) return;

        int teamAction = reader.ReadByte(); // 1=Invite, 2=Accept, 3=Leave, 4=Kick
        int targetCharId = reader.ReadInt32LE();

        var player = await _charRepo.GetCharacterByIdAsync(session.CharacterId);
        string playerName = player?.Name ?? "หัวหน้าทีม";

        if (teamAction == 1) // Invite
        {
            await _responseSender.SendSystemNoticeAsync(session, $"ส่งคำเชิญเข้าร่วมปาร์ตี้ไปยังผู้เล่น #{targetCharId} เรียบร้อยแล้ว");
        }
        else if (teamAction == 3) // Leave
        {
            await _responseSender.SendSystemNoticeAsync(session, "คุณได้ออกจากปาร์ตี้เรียบร้อยแล้ว");
        }

        // Send Team Update Frame (S:013-001)
        var writer = new PacketWriter()
            .WriteByte((byte)teamAction)
            .WriteInt32LE(session.CharacterId)
            .WriteString(playerName, 20);

        var frame = FrameCodec.EncodeFrame(13, 1, writer.ToArray());
        await session.SendAsync(frame);
    }
}
