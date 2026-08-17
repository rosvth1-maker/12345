using TsmClient.Domain;

namespace TsmClient.GameLogic;

public sealed class ClientGameState
{
    public ClientConnectionState ConnectionState { get; private set; }
    public IReadOnlyList<CharacterSummary> Characters => _characters;
    private readonly List<CharacterSummary> _characters = [];
    public int LocalCharacterId { get; private set; }
    public string LocalCharacterName { get; private set; } = string.Empty;
    public WorldPosition Position { get; private set; } = new(0, 0, 0);
    public IReadOnlyDictionary<int, ScenePlayer> ScenePlayers => _scenePlayers;
    private readonly Dictionary<int, ScenePlayer> _scenePlayers = [];
    public IReadOnlyList<NpcPlacement> Npcs { get; private set; } = [];
    public IReadOnlyDictionary<int, QuestState> Quests => _quests;
    private readonly Dictionary<int, QuestState> _quests = [];
    public BattleState? Battle { get; private set; }
    public BattleReward? LastBattleReward { get; private set; }
    public IReadOnlyList<InventoryItem> Inventory { get; private set; } = [];
    public IReadOnlyList<CoreSystemResult> SystemHistory => _systemHistory;
    private readonly List<CoreSystemResult> _systemHistory = [];

    public void SetConnectionState(ClientConnectionState state) => ConnectionState = state;
    public void SetCharacters(IEnumerable<CharacterSummary> characters)
    {
        _characters.Clear();
        _characters.AddRange(characters);
    }

    public void EnterWorld(int characterId, string name, int mapId, short x, short y)
    {
        LocalCharacterId = characterId;
        LocalCharacterName = name;
        Position = new WorldPosition(mapId, x, y);
        _scenePlayers.Clear();
    }

    public void SetScene(SceneSnapshot scene)
    {
        Position = Position with { MapId = scene.MapId };
        _scenePlayers.Clear();
        foreach (var player in scene.Players.Where(p => p.CharacterId != LocalCharacterId))
            _scenePlayers[player.CharacterId] = player;
        Npcs = scene.Npcs.Count > 0 ? scene.Npcs : SceneNpcRegistry.ForMap(scene.MapId);
    }

    public void ApplyMovement(PlayerMovement movement)
    {
        if (movement.CharacterId == LocalCharacterId)
            Position = Position with { X = movement.X, Y = movement.Y };
        else if (_scenePlayers.TryGetValue(movement.CharacterId, out var player))
            _scenePlayers[movement.CharacterId] = player with { X = movement.X, Y = movement.Y };
        else
            _scenePlayers[movement.CharacterId] = new ScenePlayer(movement.CharacterId, $"ผู้เล่น #{movement.CharacterId}", movement.X, movement.Y, 0, 0);
    }

    public void SetLocalPosition(short x, short y) => Position = Position with { X = x, Y = y };
    public void SetQuest(QuestState quest) => _quests[quest.MissionId] = quest;
    public void StartBattle(BattleState battle) { Battle = battle; LastBattleReward = null; }
    public void ApplyBattleAction(BattleActionResult result)
    {
        if (Battle is { } battle) Battle = battle with { EnemyHp = result.EnemyHp };
    }
    public void EndBattle(BattleReward? reward = null) { LastBattleReward = reward; Battle = null; }
    public void SetInventory(IReadOnlyList<InventoryItem> items) => Inventory = items;
    public void AddSystemResult(CoreSystemResult result) { _systemHistory.Insert(0, result); if (_systemHistory.Count > 100) _systemHistory.RemoveAt(100); }
}

public static class SceneNpcRegistry
{
    public static IReadOnlyList<NpcPlacement> ForMap(int mapId) => mapId switch
    {
        10801 =>
        [
            new(10001, "ครูฝึกมือใหม่ประจำเมือง", 530, 730, 5001, SceneNpcType.QuestGiver),
            new(10002, "ผู้ใหญ่บ้านจัวจวิ้น", 610, 735, 5002, SceneNpcType.QuestGiver),
            new(10003, "พ่อค้าเร่แห่งแดนสามก๊ก", 575, 820, 5003, SceneNpcType.Shop)
        ],
        10802 =>
        [
            new(11001, "บาโตวเยา", 360, 430, 0, SceneNpcType.Monster),
            new(11002, "โจรผ้าเหลืองฝึกหัด", 690, 520, 0, SceneNpcType.Monster),
            new(10001, "ครูฝึกมือใหม่ประจำเมือง", 260, 350, 5001, SceneNpcType.QuestGiver)
        ],
        _ => []
    };
}
