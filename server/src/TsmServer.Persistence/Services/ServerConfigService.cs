using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using TsmServer.Domain.Models;

namespace TsmServer.Persistence.Services;

public class ServerConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public string ConfigFilePath { get; private set; } = string.Empty;
    public ServerConfiguration CurrentConfig { get; private set; } = new();

    public ServerConfigService(string? customPath = null)
    {
        ConfigFilePath = ResolveConfigPath(customPath);
        LoadConfig();
    }

    private static string ResolveConfigPath(string? customPath)
    {
        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
        {
            return Path.GetFullPath(customPath);
        }

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates = new[]
        {
            Path.Combine(baseDir, "DATA", "Config.JSON"),
            Path.Combine(baseDir, "Config.JSON"),
            Path.Combine(baseDir, "..", "..", "..", "..", "DATA", "Config.JSON"),
            Path.Combine(Directory.GetCurrentDirectory(), "DATA", "Config.JSON"),
            @"C:\Users\Administrator\Documents\Codex\TSM online Server\bin\publish\Server\DATA\Config.JSON",
            @"C:\Users\Administrator\Documents\Codex\TSM online Server\DATA\Config.JSON"
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return Path.GetFullPath(c);
        }

        string defaultPath = Path.Combine(baseDir, "DATA", "Config.JSON");
        string? dir = Path.GetDirectoryName(defaultPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return defaultPath;
    }

    public ServerConfiguration LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var cfg = JsonSerializer.Deserialize<ServerConfiguration>(json, JsonOptions);
                if (cfg != null)
                {
                    CurrentConfig = cfg;
                    return CurrentConfig;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config Warning] Failed to read {ConfigFilePath}: {ex.Message}");
        }

        CurrentConfig = new ServerConfiguration();
        SaveConfig(CurrentConfig);
        return CurrentConfig;
    }

    public bool SaveConfig(ServerConfiguration config)
    {
        try
        {
            CurrentConfig = config;
            string json = JsonSerializer.Serialize(config, JsonOptions);

            string? dir = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(ConfigFilePath, json);

            // Also mirror save to project root DATA/Config.JSON and publish folders for safety
            string[] mirrorPaths = new[]
            {
                @"C:\Users\Administrator\Documents\Codex\TSM online Server\DATA\Config.JSON",
                @"C:\Users\Administrator\Documents\Codex\TSM online Server\bin\publish\Server\DATA\Config.JSON",
                @"C:\Users\Administrator\Documents\Codex\TSM online Server\bin\publish\UI\DATA\Config.JSON"
            };

            foreach (var p in mirrorPaths)
            {
                if (!p.Equals(ConfigFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string? pDir = Path.GetDirectoryName(p);
                        if (!string.IsNullOrEmpty(pDir) && !Directory.Exists(pDir)) Directory.CreateDirectory(pDir);
                        File.WriteAllText(p, json);
                    }
                    catch
                    {
                        // Ignore mirror errors
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config Error] Failed to write {ConfigFilePath}: {ex.Message}");
            return false;
        }
    }

    public string GetFormattedJson()
    {
        return JsonSerializer.Serialize(CurrentConfig, JsonOptions);
    }

    public bool SaveFromJsonString(string jsonString, out ServerConfiguration? parsedConfig, out string errorMessage)
    {
        try
        {
            var config = JsonSerializer.Deserialize<ServerConfiguration>(jsonString, JsonOptions);
            if (config == null)
            {
                parsedConfig = null;
                errorMessage = "ไม่สามารถแปลงรูปแบบ JSON ได้ (ข้อมูลว่างเปล่า)";
                return false;
            }

            bool saved = SaveConfig(config);
            parsedConfig = config;
            errorMessage = saved ? string.Empty : "ไม่สามารถบันทึกไฟล์ลงดิสก์ได้";
            return saved;
        }
        catch (Exception ex)
        {
            parsedConfig = null;
            errorMessage = ex.Message;
            return false;
        }
    }
}
