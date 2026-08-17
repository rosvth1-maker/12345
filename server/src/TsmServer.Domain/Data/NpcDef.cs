namespace TsmServer.Domain.Data;

public record NpcDef(
    int Id,
    string Name,
    int Level,
    int Element,
    int Hp,
    int Sp,
    int Atk,
    int Def,
    int Matk,
    int Mdef,
    int Agi,
    IReadOnlyList<int> Skills,
    int DropTableId = 0
);
