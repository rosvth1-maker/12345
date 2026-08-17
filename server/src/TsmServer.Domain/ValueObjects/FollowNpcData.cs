namespace TsmServer.Domain.ValueObjects;

public record FollowNpcData(
    int Slot,
    int NpcId,
    string CustomName,
    int Level,
    long Exp,
    int Hp,
    int MaxHp,
    int Sp,
    int MaxSp,
    int IntVal,
    int AtkVal,
    int DefVal,
    int HpaVal,
    int SpaVal,
    int AgiVal,
    int Loyalty = 100,
    bool IsDeployed = false,
    bool IsRiding = false
);
