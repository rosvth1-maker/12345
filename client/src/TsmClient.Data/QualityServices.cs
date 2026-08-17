using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TsmClient.Data;

public sealed record ServerLinkInfo(string Directory, string Host, int Port, int ClientVersion, string CdnBaseUrl, string DataDirectory);

public static class ServerLinkService
{
    public static ServerLinkInfo Read(string serverDirectory)
    {
        string root = Path.GetFullPath(serverDirectory);
        string appSettings = Path.Combine(root, "src", "TsmServer.App", "appsettings.json");
        string config = Path.Combine(root, "src", "TsmServer.App", "Config.JSON");
        if (!File.Exists(appSettings) || !File.Exists(config)) throw new DirectoryNotFoundException("ไม่พบไฟล์ตั้งค่า TS Dark World Server ในโฟลเดอร์ที่ระบุ");
        using var appDoc = JsonDocument.Parse(File.ReadAllText(appSettings));
        using var configDoc = JsonDocument.Parse(File.ReadAllText(config));
        var server = appDoc.RootElement.GetProperty("Server"); var game = appDoc.RootElement.GetProperty("Game");
        var publicServer = configDoc.RootElement.GetProperty("Server");
        string host = server.TryGetProperty("Host", out var h) ? h.GetString() ?? "127.0.0.1" : "127.0.0.1";
        int port = server.GetProperty("Port").GetInt32(); int version = game.GetProperty("ClientVersion").GetInt32();
        int cdnPort = publicServer.GetProperty("CdnPort").GetInt32();
        string dataRelative = game.GetProperty("ServerDataPath").GetString() ?? "DATA\\Data";
        return new(root, host, port, version, $"http://{host}:{cdnPort}", Path.GetFullPath(Path.Combine(root, dataRelative)));
    }
}

public sealed record UpdateFile(string Path, long Length, string Sha256, string Url);
public sealed record UpdateManifest(string Version, DateTimeOffset PublishedUtc, IReadOnlyList<UpdateFile> Files);
public sealed record UpdateCheckResult(string Version, int RequiredFiles, long DownloadBytes, IReadOnlyList<UpdateFile> Files);

public sealed class ClientUpdateService(HttpClient? httpClient = null)
{
    private readonly HttpClient _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<UpdateCheckResult> CheckAsync(string cdnBaseUrl, string installRoot, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(new Uri(cdnBaseUrl.TrimEnd('/') + "/"), "manifest.json");
        await using var stream = await _http.GetStreamAsync(uri, cancellationToken);
        var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Update manifest ไม่ถูกต้อง");
        var required = new List<UpdateFile>();
        foreach (var file in manifest.Files)
        {
            string relative = ValidateRelativePath(file.Path);
            string local = Path.GetFullPath(Path.Combine(installRoot, relative));
            if (!local.StartsWith(Path.GetFullPath(installRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Manifest พยายามเขียนนอกโฟลเดอร์เกม");
            if (!File.Exists(local) || new FileInfo(local).Length != file.Length || !await HasHashAsync(local, file.Sha256, cancellationToken)) required.Add(file);
        }
        return new(manifest.Version, required.Count, required.Sum(x => x.Length), required);
    }

    public async Task DownloadToStagingAsync(string cdnBaseUrl, string stagingRoot, IEnumerable<UpdateFile> files, CancellationToken cancellationToken = default)
    {
        foreach (var file in files)
        {
            string relative = ValidateRelativePath(file.Path); string target = Path.Combine(stagingRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var uri = string.IsNullOrWhiteSpace(file.Url) ? new Uri(new Uri(cdnBaseUrl.TrimEnd('/') + "/"), relative.Replace('\\', '/')) : new Uri(file.Url);
            await using (var source = await _http.GetStreamAsync(uri, cancellationToken))
            await using (var output = File.Create(target)) { await source.CopyToAsync(output, cancellationToken); await output.FlushAsync(cancellationToken); }
            if (new FileInfo(target).Length != file.Length || !await HasHashAsync(target, file.Sha256, cancellationToken)) throw new InvalidDataException($"SHA-256 ไม่ตรง: {relative}");
        }
    }

    public static string ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Split('/', '\\').Any(x => x == "..")) throw new InvalidDataException("พบ path ไม่ปลอดภัยใน manifest");
        return path.Replace('/', Path.DirectorySeparatorChar);
    }
    private static async Task<bool> HasHashAsync(string path, string expected, CancellationToken token)
    { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).Equals(expected, StringComparison.OrdinalIgnoreCase); }
}

public static class CrashReportService
{
    private static readonly Regex Sensitive = new("(?i)(password|passwd|รหัสผ่าน|account|username|บัญชี)\\s*[:=]\\s*[^\\s,;]+", RegexOptions.Compiled);
    public static string Redact(string value) => Sensitive.Replace(value, "$1=[REDACTED]");
    public static string Write(Exception exception, string directory)
    {
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, $"crash-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.log");
        string safe = Redact($"UTC: {DateTimeOffset.UtcNow:O}{Environment.NewLine}Version: {Environment.Version}{Environment.NewLine}OS: {Environment.OSVersion}{Environment.NewLine}{exception}");
        File.WriteAllText(path, safe); return path;
    }
}

public sealed record PerformanceSnapshot(long ManagedBytes, double WorkingSetMb, TimeSpan CpuTime, int Gen0, int Gen1, int Gen2)
{
    public static PerformanceSnapshot Capture()
    { using var process = Process.GetCurrentProcess(); return new(GC.GetTotalMemory(false), process.WorkingSet64 / 1024d / 1024d, process.TotalProcessorTime, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)); }
}
