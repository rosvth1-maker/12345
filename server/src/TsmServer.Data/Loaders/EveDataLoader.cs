using System.Buffers.Binary;
using TsmServer.Domain.Data;

namespace TsmServer.Data.Loaders;

public static class EveDataLoader
{
    public static Dictionary<int, SceneEveData> LoadEveEmg(string filePath)
    {
        var result = new Dictionary<int, SceneEveData>();
        if (!File.Exists(filePath)) return result;

        byte[] bytes = File.ReadAllBytes(filePath);
        if (bytes.Length < 4) return result;

        int offset = 0;
        int totalScenes = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
        offset += 4;

        for (int s = 0; s < totalScenes && offset + 8 <= bytes.Length; s++)
        {
            int sceneId = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
            int npcCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 4));
            offset += 8;

            var npcs = new List<EveNpcPlacement>();
            for (int n = 0; n < npcCount && offset + 12 <= bytes.Length; n++)
            {
                int id = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
                int npcId = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 4));
                int x = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 8));
                int y = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 10));
                offset += 12;

                int eventCount = offset + 4 <= bytes.Length ? BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset)) : 0;
                offset += 4;

                var events = new List<int>();
                for (int e = 0; e < eventCount && offset + 4 <= bytes.Length; e++)
                {
                    events.Add(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset)));
                    offset += 4;
                }

                npcs.Add(new EveNpcPlacement(id, npcId, events, x, y));
            }

            result[sceneId] = new SceneEveData(sceneId, npcs, new Dictionary<int, NpcEventData>());
        }

        return result;
    }
}
