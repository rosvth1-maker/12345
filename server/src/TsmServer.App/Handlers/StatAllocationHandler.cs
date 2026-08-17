using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

/// <summary>
/// Handles Stat Point Allocation & Skill Point Upgrades (C:042-001, C:028-001)
/// </summary>
public class StatAllocationHandler : IPacketHandler
{
    private readonly ICharacterRepository _charRepo;
    private readonly IResponseSender _responseSender;

    public int MainKind => 42;
    public int SubKind => 1;

    public StatAllocationHandler(ICharacterRepository charRepo, IResponseSender responseSender)
    {
        _charRepo = charRepo;
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        if (session.CharacterId == 0) return;

        var reader = new PacketReader(data.Span);
        if (reader.Remaining < 2) return;

        int statType = reader.ReadByte(); // 1=INT, 2=ATK, 3=DEF, 4=HPA, 5=SPA, 6=AGI
        int points = reader.ReadByte();

        var player = await _charRepo.GetCharacterByIdAsync(session.CharacterId);
        if (player == null) return;

        if (player.FreePoints < points)
        {
            await _responseSender.SendSystemNoticeAsync(session, "แต้มสเตตัสคงเหลือไม่เพียงพอ");
            return;
        }

        int newInt = player.IntVal + (statType == 1 ? points : 0);
        int newAtk = player.AtkVal + (statType == 2 ? points : 0);
        int newDef = player.DefVal + (statType == 3 ? points : 0);
        int newHpa = player.HpaVal + (statType == 4 ? points : 0);
        int newSpa = player.SpaVal + (statType == 5 ? points : 0);
        int newAgi = player.AgiVal + (statType == 6 ? points : 0);
        int remainingPoints = player.FreePoints - points;

        var updated = player with
        {
            IntVal = newInt,
            AtkVal = newAtk,
            DefVal = newDef,
            HpaVal = newHpa,
            SpaVal = newSpa,
            AgiVal = newAgi,
            FreePoints = remainingPoints
        };

        await _charRepo.SavePlayerDataAsync(updated);

        string statName = statType switch
        {
            1 => "ปัญญา (INT)",
            2 => "พลังโจมตี (ATK)",
            3 => "พลังป้องกัน (DEF)",
            4 => "พลังกาย (HPA)",
            5 => "พลังจิต (SPA)",
            6 => "ความเร็ว (AGI)",
            _ => "สเตตัส"
        };

        await _responseSender.SendSystemNoticeAsync(session, $"เพิ่มค่า {statName} +{points} สำเร็จ! (แต้มคงเหลือ: {remainingPoints})");
        await _responseSender.SendEnterGameAsync(session, updated);
    }
}
