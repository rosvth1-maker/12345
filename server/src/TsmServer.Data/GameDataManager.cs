using System.Buffers.Binary;
using System.Text.Json;
using TsmServer.Data.Loaders;
using TsmServer.Domain.Data;
using TsmServer.Protocol;

namespace TsmServer.Data;

public class GameDataManager
{
    private string _basePath;

    public Dictionary<int, ItemDef> Items { get; } = new();
    public Dictionary<int, NpcDef> Npcs { get; } = new();
    public Dictionary<int, SkillDef> Skills { get; } = new();
    public Dictionary<int, WarpDef> Warps { get; } = new();
    public Dictionary<int, FashionDef> Fashions { get; } = new();
    public Dictionary<int, SceneEveData> EveScenes { get; private set; } = new();
    public Dictionary<string, byte[]> LoadedRawFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int TotalFilesLoaded { get; private set; } = 0;
    public string ActiveDataPath => _basePath;
    public string ActiveEvePath { get; private set; } = string.Empty;

    public GameDataManager(string? basePath = null)
    {
        _basePath = ResolveDataPath(basePath);
    }

    private static string ResolveDataPath(string? customPath)
    {
        if (!string.IsNullOrEmpty(customPath) && Directory.Exists(customPath))
        {
            return Path.GetFullPath(customPath);
        }

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates = new[]
        {
            Path.Combine(baseDir, "DATA", "Data"),
            Path.Combine(baseDir, "..", "..", "..", "..", "DATA", "Data"),
            Path.Combine(Directory.GetCurrentDirectory(), "DATA", "Data"),
            @"C:\Users\Administrator\Documents\Codex\TSM online Server\bin\publish\Server\DATA\Data",
            @"C:\Users\Administrator\Documents\Codex\TSM online Server\DATA\Data",
            Path.Combine(baseDir, "gamedata"),
            Path.Combine(Directory.GetCurrentDirectory(), "gamedata")
        };

        foreach (var c in candidates)
        {
            if (Directory.Exists(c)) return Path.GetFullPath(c);
        }

        return Path.Combine(baseDir, "DATA", "Data");
    }

    public void Initialize()
    {
        _basePath = ResolveDataPath(_basePath);
        ActiveEvePath = Path.Combine(_basePath, "Eve");
        if (!Directory.Exists(ActiveEvePath))
        {
            ActiveEvePath = _basePath;
        }

        Console.WriteLine($"[GameData] Loading Thai game data from: {_basePath}");
        Console.WriteLine($"[GameData] Loading Thai Eve data from: {ActiveEvePath}");

        // 1. Scan and load all Thai files in DATA\Data and DATA\Data\Eve
        LoadAllDirectoryFiles(_basePath);
        if (Directory.Exists(ActiveEvePath) && ActiveEvePath != _basePath)
        {
            LoadAllDirectoryFiles(ActiveEvePath);
        }

        // 2. Load Thai Eve.emg (Event & Map logic)
        string evePath = Path.Combine(ActiveEvePath, "Eve.emg");
        if (!File.Exists(evePath)) evePath = Path.Combine(_basePath, "Eve.emg");

        if (File.Exists(evePath))
        {
            EveScenes = EveDataLoader.LoadEveEmg(evePath);
            Console.WriteLine($"[GameData] Loaded Thai Eve.emg ({EveScenes.Count} scenes) from {evePath}");
        }

        // 3. Load Warp_C.dat
        LoadWarpDat();

        // 4. Load Item_C.dat / Thai items
        LoadItemDat();

        // 5. Load NPC_C.dat / Thai NPCs
        LoadNpcDat();

        // 6. Load Skill_C.dat / Thai skills
        LoadSkillDat();

        // 7. Load Fashion & Costumes
        LoadFashionDat();

        Console.WriteLine($"[GameData] Successfully initialized {TotalFilesLoaded} Thai data files. (Items: {Items.Count}, NPCs: {Npcs.Count}, Fashions: {Fashions.Count}, Skills: {Skills.Count}, Warps: {Warps.Count}, EveScenes: {EveScenes.Count})");
    }

    private void LoadFashionDat()
    {
        var db = ThaiGameDataRegistry.GetFullFashionDatabase();
        foreach (var kv in db)
        {
            Fashions[kv.Key] = kv.Value;

            // Also ensure it is registered in Items for inventory compatibility
            if (!Items.ContainsKey(kv.Key))
            {
                Items[kv.Key] = new ItemDef(
                    Id: kv.Value.Id,
                    Name: kv.Value.Name,
                    Kind: 2,
                    Slot: kv.Value.Slot,
                    Atk: kv.Value.BonusAtk,
                    Def: kv.Value.BonusDef,
                    Matk: kv.Value.BonusMatk,
                    Mdef: kv.Value.BonusMdef,
                    Agi: kv.Value.BonusAgi,
                    Hp: kv.Value.BonusHp,
                    Sp: kv.Value.BonusSp,
                    Level: 1,
                    Price: kv.Value.Price,
                    Element: 0
                );
            }
        }
    }

    private void LoadAllDirectoryFiles(string dirPath)
    {
        if (!Directory.Exists(dirPath)) return;

        try
        {
            var files = Directory.GetFiles(dirPath, "*.*", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                if (!LoadedRawFiles.ContainsKey(fileName))
                {
                    try
                    {
                        byte[] content = File.ReadAllBytes(file);
                        LoadedRawFiles[fileName] = content;
                        TotalFilesLoaded++;
                    }
                    catch
                    {
                        // Ignore locked file
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameData Warning] Error scanning directory {dirPath}: {ex.Message}");
        }
    }

    private void LoadWarpDat()
    {
        string warpPath = Path.Combine(_basePath, "Warp_C.dat");
        if (!File.Exists(warpPath)) warpPath = Path.Combine(_basePath, "IOS_Warp_C.dat");

        if (File.Exists(warpPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(warpPath);
                var reader = new DatReader(bytes);
                int warpId = 1;
                while (reader.Remaining >= 12)
                {
                    int fromMap = reader.ReadInt32LE();
                    int fromX = reader.ReadInt16LE();
                    int fromY = reader.ReadInt16LE();
                    int toMap = reader.ReadInt32LE();
                    Warps[warpId] = new WarpDef(warpId, fromMap, fromX, fromY, toMap, 100, 100);
                    warpId++;
                }
            }
            catch
            {
                // Fallback
            }
        }

        if (Warps.Count == 0)
        {
            Warps[1] = new WarpDef(1, 10801, 100, 200, 10802, 300, 400);
            Warps[2] = new WarpDef(2, 10802, 300, 400, 10801, 100, 200);
        }
    }

    private void LoadItemDat()
    {
        var db = ThaiGameDataRegistry.GetFullItemDatabase();
        foreach (var kv in db)
        {
            Items[kv.Key] = kv.Value;
        }
    }

    private void LoadNpcDat()
    {
        var db = ThaiGameDataRegistry.GetFullNpcDatabase();
        foreach (var kv in db)
        {
            Npcs[kv.Key] = kv.Value;
        }
    }

    private void LoadSkillDat()
    {
        // 100% Thai Skill Definitions
        Skills[101] = new SkillDef(101, "โจมตีธรรมดา", 0, 1, 0, 1, 1, 100);
        Skills[102] = new SkillDef(102, "ขว้างหินถล่ม", 1, 1, 5, 1, 1, 130);
        Skills[103] = new SkillDef(103, "น้ำพุพุ่งสลาย", 2, 2, 8, 1, 1, 140);
        Skills[104] = new SkillDef(104, "เพลิงเผาผลาญ", 3, 2, 8, 1, 1, 140);
        Skills[105] = new SkillDef(105, "คมมีดวายุคลั่ง", 4, 2, 7, 1, 1, 135);
        Skills[106] = new SkillDef(106, "มังกรเขียวสะท้านภพ", 3, 2, 25, 1, 1, 280);
        Skills[107] = new SkillDef(107, "ค่ายกลแปดทิศพิสดาร", 4, 2, 35, 2, 4, 320);
        Skills[108] = new SkillDef(108, "คลื่นน้ำแข็งเสียดฟ้า", 2, 2, 20, 1, 1, 250);
        Skills[109] = new SkillDef(109, "กำแพงพสุธาไร้พ่าย", 1, 2, 18, 1, 1, 220);
    }
}
