namespace TsmServer.Domain.Data;

public record SkillDef(
    int Id,
    string Name,
    int Element,
    int Type,
    int SpCost,
    int TargetType,
    int TargetArea,
    int Power,
    int MaxLevel = 10
);
