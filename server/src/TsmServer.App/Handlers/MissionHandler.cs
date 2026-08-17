using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

public sealed class MissionHandler(IQuestRepository quests, ICharacterRepository characters) : IPacketHandler
{
    public int MainKind => 47;
    public int SubKind => 1;

    private static readonly Dictionary<int, (string Title, long Reward)> Definitions = new()
    {
        [5001] = ("ก้าวแรกของจอมยุทธ์", 100),
        [5002] = ("ช่วยเหลือผู้ใหญ่บ้าน", 250),
        [5003] = ("เสบียงจากพ่อค้าเร่", 500)
    };

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        if (session.CharacterId == 0 || data.Length < 5) return;
        var reader = new PacketReader(data.Span);
        int missionId = reader.ReadInt32LE();
        byte action = reader.ReadByte(); // 1 accept, 2 turn in
        if (!Definitions.TryGetValue(missionId, out var definition)) return;
        byte status;
        long reward = 0;
        string message;
        if (action == 1)
        {
            await quests.SetMissionStepAsync(session.CharacterId, missionId, 1);
            status = 1;
            message = "รับภารกิจแล้ว";
        }
        else
        {
            await quests.SetMissionCompletedAsync(session.CharacterId, missionId);
            reward = definition.Reward;
            if (session.PlayerData is { } player)
            {
                session.PlayerData = player with { Gold = player.Gold + reward };
                await characters.UpdateGoldAsync(session.CharacterId, session.PlayerData.Gold);
            }
            status = 2;
            message = $"ส่งภารกิจสำเร็จ รับเงิน {reward:N0}";
        }
        var payload = new PacketWriter().WriteInt32LE(missionId).WriteByte(status).WriteInt64LE(reward)
            .WriteString(definition.Title, 40).WriteString(message, 48).ToArray();
        await session.SendAsync(FrameCodec.EncodeFrame(47, 1, payload));
    }
}
