using TsmServer.Data;

namespace TsmServer.App.Response;

public sealed record SceneNpcInfo(int NpcId, string Name, int X, int Y, int QuestId);

public static class SceneNpcCatalog
{
    public const int TalkRange = 180;

    public static IReadOnlyList<SceneNpcInfo> Resolve(GameDataManager gameData, int mapId)
    {
        if (gameData.EveScenes.TryGetValue(mapId, out var scene) && scene.Npcs.Count > 0)
        {
            var loaded = scene.Npcs
                .Where(x => x.NpcId > 0)
                .Select(x => new SceneNpcInfo(x.NpcId,
                    gameData.Npcs.TryGetValue(x.NpcId, out var def) ? def.Name : $"NPC #{x.NpcId}",
                    Math.Clamp(x.X, 0, 1600), Math.Clamp(x.Y, 0, 1200), x.Events.FirstOrDefault()))
                .ToArray();
            if (loaded.Length > 0) return loaded;
        }

        string Name(int id, string fallback) => gameData.Npcs.TryGetValue(id, out var def) ? def.Name : fallback;
        return mapId switch
        {
            10801 =>
            [
                new(10001, Name(10001, "ครูฝึกมือใหม่ประจำเมือง"), 530, 730, 5001),
                new(10002, Name(10002, "ผู้ใหญ่บ้านจัวจวิ้น"), 610, 735, 5002),
                new(10003, Name(10003, "พ่อค้าเร่แห่งแดนสามก๊ก"), 575, 820, 5003)
            ],
            10802 =>
            [
                new(11001, Name(11001, "บาโตวเยา"), 360, 430, 0),
                new(11002, Name(11002, "โจรผ้าเหลืองฝึกหัด"), 690, 520, 0),
                new(10001, Name(10001, "ครูฝึกมือใหม่ประจำเมือง"), 260, 350, 5001)
            ],
            _ => []
        };
    }

    public static bool IsWithinTalkRange(int playerX, int playerY, SceneNpcInfo npc) =>
        DistanceSquared(playerX, playerY, npc.X, npc.Y) <= TalkRange * TalkRange;

    private static long DistanceSquared(int x1, int y1, int x2, int y2)
    {
        long dx = x1 - x2;
        long dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}
