using System.Text.Json;
using TsmClient.Domain;

namespace TsmClient.Data;

public sealed class ClientStorage(string rootDirectory)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    public string SettingsPath => Path.Combine(rootDirectory, "config", "clientsettings.json");
    public string AssetCacheDirectory => Path.Combine(rootDirectory, "cache", "assets");
    public Task<AssetCatalog> BuildAssetCatalogAsync(string sourceDirectory, IProgress<(int Done, int Total, string File)>? progress = null,
        CancellationToken cancellationToken = default) => new AssetCatalogService().BuildAsync(sourceDirectory, AssetCacheDirectory, progress, cancellationToken);

    public async Task<ClientSettings> LoadSettingsAsync()
    {
        if (!File.Exists(SettingsPath)) return new ClientSettings();
        await using var stream = File.OpenRead(SettingsPath);
        return await JsonSerializer.DeserializeAsync<ClientSettings>(stream) ?? new ClientSettings();
    }

    public async Task SaveSettingsAsync(ClientSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions);
    }

    public AssetSummary ScanAssets(string directory)
    {
        if (!Directory.Exists(directory)) return new AssetSummary(0, 0, new Dictionary<string, int>());
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToArray();
        var byExtension = files.GroupBy(Path.GetExtension, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => string.IsNullOrWhiteSpace(g.Key) ? "(ไม่มีนามสกุล)" : g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        long bytes = files.Sum(f => new FileInfo(f).Length);
        return new AssetSummary(files.Length, bytes, byExtension);
    }
}

public sealed record AssetSummary(int FileCount, long TotalBytes, IReadOnlyDictionary<string, int> ByExtension);
