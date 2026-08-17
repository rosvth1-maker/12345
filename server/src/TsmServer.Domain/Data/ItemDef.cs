namespace TsmServer.Domain.Data;

public record ItemDef(
    int Id,
    string Name,
    int Kind,
    int Slot,
    int Atk,
    int Def,
    int Matk,
    int Mdef,
    int Agi,
    int Hp,
    int Sp,
    int Level,
    int Price,
    int Element,
    int StackLimit = 999
);
