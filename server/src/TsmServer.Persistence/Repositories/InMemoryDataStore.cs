using System.Collections.Concurrent;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.Domain.ValueObjects;

namespace TsmServer.Persistence.Repositories;

public class InMemoryDataStore :
    IAccountRepository,
    ICharacterRepository,
    IInventoryRepository,
    IPetRepository,
    IQuestRepository,
    IBitFlagRepository
{
    private readonly ConcurrentDictionary<string, (int Id, string Account, string PasswordHash, int GmLevel)> _accounts = new();
    private readonly ConcurrentDictionary<int, List<PlayerDataDto>> _accountCharacters = new();
    private readonly ConcurrentDictionary<int, PlayerDataDto> _characters = new();
    private readonly ConcurrentDictionary<(int CharId, int BagType), List<ThingData>> _inventories = new();
    private readonly ConcurrentDictionary<int, List<FollowNpcData>> _pets = new();
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, int>> _missions = new();
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, int>> _bitFlags = new();
    private int _accountIdCounter = 1;
    private int _charIdCounter = 1;

    public InMemoryDataStore()
    {
        // Development seed is opt-in. Never store a default credential in source control.
        string? developmentPassword = Environment.GetEnvironmentVariable("TSM_DEV_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(developmentPassword)) return;
        string defaultHash = BCrypt.Net.BCrypt.HashPassword(developmentPassword);
        _accounts["admin"] = (_accountIdCounter++, "admin", defaultHash, 10);

        // Seed default character for admin
        var adminChar = new PlayerDataDto(
            CharacterId: _charIdCounter++,
            Account: "admin",
            Name: "ผู้ดูแลระบบ (Admin)",
            Level: 100,
            Element: 3, // Fire
            Gender: 1,
            Hair: 1,
            HairColor: 1,
            SkinColor: 1,
            Hp: 5000,
            MaxHp: 5000,
            Sp: 3000,
            MaxSp: 3000,
            IntVal: 100,
            AtkVal: 150,
            DefVal: 80,
            HpaVal: 80,
            SpaVal: 50,
            AgiVal: 90,
            FreePoints: 0,
            SkillPoints: 50,
            Exp: 1000000,
            Gold: 9999999,
            MapId: 10801,
            X: 570,
            Y: 770,
            GmLevel: 10
        );
        _characters[adminChar.CharacterId] = adminChar;
        _accountCharacters[1] = new List<PlayerDataDto> { adminChar };

        // Seed basic inventory items
        _inventories[(adminChar.CharacterId, 1)] = new List<ThingData>
        {
            new ThingData(1, 10001, 50), // ซาลาเปา
            new ThingData(2, 20001, 1),  // ดาบเหล็ก
            new ThingData(3, 20002, 1)   // ชุดผ้า
        };

        // Seed basic pet
        _pets[adminChar.CharacterId] = new List<FollowNpcData>
        {
            new FollowNpcData(1, 11001, "บาโตวเยา (บา豆妖)", 5, 500, 150, 150, 50, 50, 10, 15, 12, 10, 8, 14, 100, IsDeployed: true)
        };
    }

    // --- IAccountRepository ---
    public Task<(int Id, string Account, string PasswordHash, int GmLevel)?> FindByAccountAsync(string account)
    {
        if (_accounts.TryGetValue(account.ToLower(), out var acc))
            return Task.FromResult<(int, string, string, int)?>(acc);
        return Task.FromResult<(int, string, string, int)?>(null);
    }

    public Task<int> CreateAccountAsync(string account, string passwordHash)
    {
        int id = Interlocked.Increment(ref _accountIdCounter);
        _accounts[account.ToLower()] = (id, account, passwordHash, 0);
        return Task.FromResult(id);
    }

    public Task UpdateLastLoginAsync(int accountId) => Task.CompletedTask;

    // --- ICharacterRepository ---
    public Task<IReadOnlyList<PlayerDataDto>> GetCharactersByAccountIdAsync(int accountId)
    {
        if (_accountCharacters.TryGetValue(accountId, out var list))
            return Task.FromResult<IReadOnlyList<PlayerDataDto>>(list.ToList());
        return Task.FromResult<IReadOnlyList<PlayerDataDto>>(Array.Empty<PlayerDataDto>());
    }

    public Task<PlayerDataDto?> GetCharacterByIdAsync(int characterId)
    {
        _characters.TryGetValue(characterId, out var c);
        return Task.FromResult(c);
    }

    public Task<int> CreateCharacterAsync(int accountId, PlayerDataDto data)
    {
        int id = Interlocked.Increment(ref _charIdCounter);
        var created = data with { CharacterId = id };
        _characters[id] = created;
        var list = _accountCharacters.GetOrAdd(accountId, _ => new List<PlayerDataDto>());
        lock (list) list.Add(created);
        return Task.FromResult(id);
    }

    public Task UpdatePositionAsync(int characterId, int mapId, int x, int y)
    {
        if (_characters.TryGetValue(characterId, out var c))
            _characters[characterId] = c with { MapId = mapId, X = x, Y = y };
        return Task.CompletedTask;
    }

    public Task SavePlayerDataAsync(PlayerDataDto player)
    {
        _characters[player.CharacterId] = player;
        return Task.CompletedTask;
    }

    public Task UpdateGoldAsync(int characterId, long gold)
    {
        if (_characters.TryGetValue(characterId, out var c))
            _characters[characterId] = c with { Gold = gold };
        return Task.CompletedTask;
    }

    public Task UpdateStatsAsync(int characterId, int hp, int sp, int level, long exp)
    {
        if (_characters.TryGetValue(characterId, out var c))
            _characters[characterId] = c with { Hp = hp, Sp = sp, Level = level, Exp = exp };
        return Task.CompletedTask;
    }

    // --- IInventoryRepository ---
    public Task<IReadOnlyList<ThingData>> GetItemsAsync(int characterId, int bagType)
    {
        if (_inventories.TryGetValue((characterId, bagType), out var items))
            return Task.FromResult<IReadOnlyList<ThingData>>(items.ToList());
        return Task.FromResult<IReadOnlyList<ThingData>>(Array.Empty<ThingData>());
    }

    public Task SaveItemsAsync(int characterId, int bagType, IReadOnlyList<ThingData> items)
    {
        _inventories[(characterId, bagType)] = items.ToList();
        return Task.CompletedTask;
    }

    public Task UpdateItemSlotAsync(int characterId, int bagType, ThingData item)
    {
        var list = _inventories.GetOrAdd((characterId, bagType), _ => new List<ThingData>());
        lock (list)
        {
            list.RemoveAll(x => x.Slot == item.Slot);
            list.Add(item);
        }
        return Task.CompletedTask;
    }

    public Task DeleteItemSlotAsync(int characterId, int bagType, int slot)
    {
        if (_inventories.TryGetValue((characterId, bagType), out var list))
            lock (list) list.RemoveAll(x => x.Slot == slot);
        return Task.CompletedTask;
    }

    // --- IPetRepository ---
    public Task<IReadOnlyList<FollowNpcData>> GetPetsAsync(int characterId)
    {
        if (_pets.TryGetValue(characterId, out var list))
            return Task.FromResult<IReadOnlyList<FollowNpcData>>(list.ToList());
        return Task.FromResult<IReadOnlyList<FollowNpcData>>(Array.Empty<FollowNpcData>());
    }

    public Task SavePetsAsync(int characterId, IReadOnlyList<FollowNpcData> pets)
    {
        _pets[characterId] = pets.ToList();
        return Task.CompletedTask;
    }

    public Task AddPetAsync(int characterId, FollowNpcData pet)
    {
        var list = _pets.GetOrAdd(characterId, _ => new List<FollowNpcData>());
        lock (list) list.Add(pet);
        return Task.CompletedTask;
    }

    public Task RemovePetAsync(int characterId, int slot)
    {
        if (_pets.TryGetValue(characterId, out var list))
            lock (list) list.RemoveAll(x => x.Slot == slot);
        return Task.CompletedTask;
    }

    // --- IQuestRepository ---
    public Task<IReadOnlyDictionary<int, int>> GetActiveMissionsAsync(int characterId)
    {
        var dict = _missions.GetOrAdd(characterId, _ => new ConcurrentDictionary<int, int>());
        return Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>(dict));
    }

    public Task<IReadOnlySet<int>> GetCompletedMissionFlagsAsync(int characterId)
    {
        return Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());
    }

    public Task SetMissionStepAsync(int characterId, int missionId, int step)
    {
        var dict = _missions.GetOrAdd(characterId, _ => new ConcurrentDictionary<int, int>());
        dict[missionId] = step;
        return Task.CompletedTask;
    }

    public Task SetMissionCompletedAsync(int characterId, int missionId)
    {
        var dict = _missions.GetOrAdd(characterId, _ => new ConcurrentDictionary<int, int>());
        dict[missionId] = 999;
        return Task.CompletedTask;
    }

    // --- IBitFlagRepository ---
    public Task<IReadOnlyDictionary<int, int>> GetBitFlagsAsync(int characterId)
    {
        var dict = _bitFlags.GetOrAdd(characterId, _ => new ConcurrentDictionary<int, int>());
        return Task.FromResult<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>(dict));
    }

    public Task SetBitFlagAsync(int characterId, int flagId, int value)
    {
        var dict = _bitFlags.GetOrAdd(characterId, _ => new ConcurrentDictionary<int, int>());
        dict[flagId] = value;
        return Task.CompletedTask;
    }
}
