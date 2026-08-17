using System.Security.Cryptography;
using System.Text.Json;

namespace TsmClient.Data;

public sealed record AssetEntry(string RelativePath, string Extension, long Length, string Sha256, string Format, string Signature);
public sealed record ThaiDataName(string Kind, int Id, string Name, string Source);
public sealed record SceneAsset(int MapId, string RelativePath, long Length, string Sha256, bool NativeRenderingSupported);
public sealed record AssetCatalog(string SourceRoot, DateTimeOffset CreatedUtc, int FileCount, long TotalBytes,
    IReadOnlyDictionary<string, int> ExtensionCounts, IReadOnlyList<AssetEntry> Files,
    IReadOnlyList<ThaiDataName> ThaiNames, IReadOnlyList<SceneAsset> Scenes);

public sealed class AssetCatalogService
{
    private static readonly HashSet<string> AnalyzedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".dat", ".unity3d", ".sty", ".jmxa", ".jmg", ".pmg", ".emg" };

    public async Task<AssetCatalog> BuildAsync(string sourceRoot, string cacheDirectory, IProgress<(int Done, int Total, string File)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(sourceRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        string cache = Path.GetFullPath(cacheDirectory);
        if (cache.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cache ต้องอยู่นอกโฟลเดอร์ทรัพยากรต้นฉบับ");

        string[] paths = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var entries = new List<AssetEntry>(paths.Length);
        for (int i = 0; i < paths.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = paths[i];
            var info = new FileInfo(path);
            string extension = info.Extension.ToLowerInvariant();
            string hash;
            await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            byte[] header = new byte[Math.Min(24, (int)Math.Min(info.Length, 24))];
            if (header.Length > 0)
            {
                await using var headerStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                await headerStream.ReadExactlyAsync(header, cancellationToken);
            }
            entries.Add(new AssetEntry(Path.GetRelativePath(root, path), extension, info.Length, hash,
                DescribeFormat(extension, header), Convert.ToHexString(header)));
            progress?.Report((i + 1, paths.Length, info.Name));
        }

        var scenes = entries.Where(x => x.Extension == ".unity3d")
            .Select(x => (Entry: x, Match: System.Text.RegularExpressions.Regex.Match(Path.GetFileNameWithoutExtension(x.RelativePath), "^l(?<id>\\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
            .Where(x => x.Match.Success && int.TryParse(x.Match.Groups["id"].Value, out _))
            .Select(x => new SceneAsset(int.Parse(x.Match.Groups["id"].Value), x.Entry.RelativePath, x.Entry.Length, x.Entry.Sha256, false)).ToArray();
        var counts = entries.GroupBy(x => string.IsNullOrEmpty(x.Extension) ? "(ไม่มีนามสกุล)" : x.Extension, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        var catalog = new AssetCatalog(root, DateTimeOffset.UtcNow, entries.Count, entries.Sum(x => x.Length), counts, entries, ThaiServerNames.All, scenes);

        Directory.CreateDirectory(cache);
        string output = Path.Combine(cache, "asset-catalog.json");
        await using var outputStream = File.Create(output);
        await JsonSerializer.SerializeAsync(outputStream, catalog, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        return catalog;
    }

    public static bool IsAnalyzedExtension(string extension) => AnalyzedExtensions.Contains(extension);

    private static string DescribeFormat(string extension, byte[] header) => extension switch
    {
        ".unity3d" when System.Text.Encoding.ASCII.GetString(header).Contains("UnityFS") => "Unity AssetBundle (UnityFS) — catalog only",
        ".unity3d" => "Unity AssetBundle — catalog only",
        ".dat" => "TS binary table — read-only indexed",
        ".emg" => "TS Eve scene/event binary — read-only indexed",
        ".sty" => "TS style binary — signature cataloged",
        ".jmxa" => "TS animation binary — signature cataloged",
        ".jmg" => "TS graphic binary — signature cataloged",
        ".pmg" => "TS packed graphic — signature cataloged",
        ".lua" => "Lua source",
        _ => "Binary/other"
    };
}

public static class ThaiServerNames
{
    public static IReadOnlyList<ThaiDataName> All { get; } =
    [
        new("Item", 10001, "ซาลาเปาหมูสับ", "ThaiGameDataRegistry"),
        new("Item", 10002, "ไข่ต้มใบชาสมุนไพร", "ThaiGameDataRegistry"),
        new("Item", 20001, "ดาบเหล็กกล้าชั้นดี", "ThaiGameDataRegistry"),
        new("Item", 20004, "ง้าวมังกรเขียว (กวนอู)", "ThaiGameDataRegistry"),
        new("NPC", 10001, "ครูฝึกมือใหม่ประจำเมือง", "ThaiGameDataRegistry"),
        new("NPC", 10002, "ผู้ใหญ่บ้านจัวจวิ้น", "ThaiGameDataRegistry"),
        new("NPC", 11001, "บาโตวเยา (บา豆妖)", "ThaiGameDataRegistry"),
        new("NPC", 30001, "กวนอู (關羽 - เทพเจ้าแห่งความซื่อสัตย์)", "ThaiGameDataRegistry"),
        new("Skill", 101, "โจมตีธรรมดา", "GameDataManager"),
        new("Skill", 102, "ขว้างหินถล่ม", "GameDataManager"),
        new("Skill", 103, "น้ำพุพุ่งสลาย", "GameDataManager"),
        new("Skill", 104, "เพลิงเผาผลาญ", "GameDataManager"),
        new("Skill", 105, "คมมีดวายุคลั่ง", "GameDataManager")
    ];
}
