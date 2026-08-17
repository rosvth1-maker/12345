namespace TsmServer.Domain.Data;

public record FashionDef(
    int Id,
    string Name,
    string Category, // เช่น ชุดคลุมแฟชั่น, หมวกแฟชั่น, อาวุธแฟชั่น, ปีก/ผ้าคลุม, สัตว์ขี่แฟชั่น, เซ็ตแฟชั่นพิเศษ
    int Slot,        // 7 = Style_Head, 8 = Style_Body, 9 = Style_Hand, 10 = Style_Wrist, 11 = Style_Boots, 100 = Cape, 6 = Mount
    int BonusHp,
    int BonusSp,
    int BonusAtk,
    int BonusDef,
    int BonusMatk,
    int BonusMdef,
    int BonusAgi,
    string SetBonusDesc,
    string AppearanceDesc,
    int Price
);
