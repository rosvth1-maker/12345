namespace TsmClient.Domain;

public sealed class ClientSettings
{
    public string ServerName { get; set; } = "TS Dark World";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 6613;
    public int ClientVersion { get; set; } = 258;
    public string AssetDirectory { get; set; } = @"..\..\ตัวเกมTS ไทย 26-8-2569\files";
    public string ServerDirectory { get; set; } = @"C:\Users\Administrator\Documents\Codex\TS Dark World online Server new 2026";
    public string ActiveProfile { get; set; } = "Local";
    public string CdnBaseUrl { get; set; } = "http://127.0.0.1:8443";
    public int RenderQuality { get; set; } = 2;
    public int MasterVolume { get; set; } = 80;
    public bool Fullscreen { get; set; }
    public string MoveUpKey { get; set; } = "W";
    public string MoveDownKey { get; set; } = "S";
    public string MoveLeftKey { get; set; } = "A";
    public string MoveRightKey { get; set; } = "D";
    public List<ConnectionProfile> Profiles { get; set; } = ConnectionProfile.Defaults.ToList();
}

public sealed record ConnectionProfile(string Name, string Host, int Port, string CdnBaseUrl)
{
    public static IReadOnlyList<ConnectionProfile> Defaults { get; } =
    [
        new("Local", "127.0.0.1", 6613, "http://127.0.0.1:8443"),
        new("LAN", "192.168.1.100", 6613, "http://192.168.1.100:8443"),
        new("VPS", "game.example.com", 6613, "https://cdn.example.com")
    ];
}

public sealed record GamePacket(byte MainKind, byte SubKind, byte Compress, byte[] Payload);

public sealed record CharacterSummary(
    int Id, string Name, byte Level, byte Element, byte Gender,
    byte Hair, byte HairColor, byte SkinColor, int MapId);

public sealed record CharacterCreation(
    string Name, byte Gender, byte Element, byte Hair, byte HairColor, byte SkinColor);

public sealed record WorldPosition(int MapId, short X, short Y);

public sealed record ScenePlayer(
    int CharacterId, string Name, short X, short Y, byte Level, byte Element);

public sealed record SceneSnapshot(int MapId, IReadOnlyList<ScenePlayer> Players, IReadOnlyList<NpcPlacement> Npcs);

public sealed record PlayerMovement(int CharacterId, short X, short Y);
public sealed record NpcPlacement(int NpcId, string Name, short X, short Y, int QuestId);
public sealed record NpcDialog(int NpcId, string NpcName, string Text, IReadOnlyList<string> Options, int QuestId);
public sealed record QuestState(int MissionId, string Title, byte Status, long Reward, string Message);
public sealed record BattleState(int EnemyId, string EnemyName, int PlayerHp, int PlayerMaxHp, int PlayerSp, int PlayerMaxSp, int EnemyHp, int EnemyMaxHp, bool CompanionPresent);
public sealed record BattleActionResult(int CharacterId, byte Action, int Damage, string ActionName, int EnemyHp);
public sealed record BattleReward(bool Victory, long Exp, long Gold, int ItemId, int PlayerHp, int PlayerSp, long TotalExp);
public sealed record InventoryItem(byte Slot, int ItemId, short Count, byte Durability)
{
    public string Name => ItemId switch { 10001 => "ซาลาเปาหมูสับ", 10002 => "ไข่ต้มใบชาสมุนไพร", 20001 => "ดาบเหล็กกล้าชั้นดี", 20002 => "กระบี่ไม้ไผ่ฝึกหัด", _ => $"ไอเทม #{ItemId}" };
    public string Display => $"ช่อง {Slot}: {Name} ×{Count}";
}
public sealed record CoreSystemResult(byte Action, bool Success, long Value, string Message);
public sealed record PetStatus(byte Slot, bool Deployed, int NpcId, string Name);
public sealed record TeamStatus(byte Action, int CharacterId, string Name);
public sealed record ChatMessage(byte Channel, string Sender, string Message);

public enum ClientConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    HandshakeComplete,
    Authenticated,
    InGame
}
