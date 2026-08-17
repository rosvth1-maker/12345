using TsmClient.Data;

string source = args.ElementAtOrDefault(0) ?? @"C:\Users\Administrator\Documents\Codex\ตัวเกมTS ไทย 26-8-2569\files";
string cache = args.ElementAtOrDefault(1) ?? Path.Combine(AppContext.BaseDirectory, "phase4-cache");
var before = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
    .ToDictionary(x => x, x => (new FileInfo(x).Length, new FileInfo(x).LastWriteTimeUtc), StringComparer.OrdinalIgnoreCase);
Console.WriteLine($"PHASE 4 INTEGRATION — {before.Count:N0} source files");
var progress = new Progress<(int Done, int Total, string File)>(p => { if (p.Done % 250 == 0 || p.Done == p.Total) Console.WriteLine($"HASH {p.Done:N0}/{p.Total:N0}"); });
AssetCatalog catalog = await new AssetCatalogService().BuildAsync(source, cache, progress);
var after = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
    .ToDictionary(x => x, x => (new FileInfo(x).Length, new FileInfo(x).LastWriteTimeUtc), StringComparer.OrdinalIgnoreCase);

var tests = new (string Name, bool Passed, string Detail)[]
{
    ("Catalog covers every source file", catalog.FileCount == before.Count && catalog.Files.Count == before.Count, $"catalog={catalog.FileCount:N0} source={before.Count:N0}"),
    ("Source metadata unchanged", before.Count == after.Count && before.All(x => after.TryGetValue(x.Key, out var y) && y == x.Value), "length/time preserved"),
    ("SHA-256 complete", catalog.Files.All(x => x.Sha256.Length == 64), $"hashed={catalog.Files.Count(x => x.Sha256.Length == 64):N0}"),
    ("Required legacy formats cataloged", new[]{".dat",".unity3d",".sty",".jmxa",".jmg",".pmg"}.All(x => catalog.ExtensionCounts.ContainsKey(x)), string.Join(", ", catalog.ExtensionCounts.Keys)),
    ("Thai Item/NPC/Skill names", new[]{"Item","NPC","Skill"}.All(k => catalog.ThaiNames.Any(x => x.Kind == k)), $"names={catalog.ThaiNames.Count}"),
    ("Scene 10801 found", catalog.Scenes.Any(x => x.MapId == 10801), catalog.Scenes.FirstOrDefault(x => x.MapId == 10801)?.RelativePath ?? "missing"),
    ("Scene 10802 found", catalog.Scenes.Any(x => x.MapId == 10802), catalog.Scenes.FirstOrDefault(x => x.MapId == 10802)?.RelativePath ?? "missing"),
    ("Cache separated from source", !Path.GetFullPath(cache).StartsWith(Path.GetFullPath(source) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(cache,"asset-catalog.json")), cache)
};
foreach (var test in tests) Console.WriteLine($"{(test.Passed ? "PASS" : "FAIL")}  {test.Name} — {test.Detail}");
int failed = tests.Count(x => !x.Passed);
Console.WriteLine($"\nRESULT {tests.Length - failed}/{tests.Length} passed");
return failed;
