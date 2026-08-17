namespace TsmServer.Domain.ValueObjects;

public record ThingData(
    int Slot,
    int ItemId,
    int Count,
    int Durability = 100,
    int MaxDurability = 100,
    int Damage = 0,
    int Element = 0,
    int EnhanceLevel = 0
);
