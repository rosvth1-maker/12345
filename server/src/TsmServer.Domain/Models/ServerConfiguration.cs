using System.Text.Json.Serialization;

namespace TsmServer.Domain.Models;

public class ServerConfiguration
{
    [JsonPropertyName("Server")]
    public ServerSettings Server { get; set; } = new();

    [JsonPropertyName("Database")]
    public DatabaseSettings Database { get; set; } = new();

    [JsonPropertyName("Rates")]
    public RatesSettings Rates { get; set; } = new();

    [JsonPropertyName("GameData")]
    public GameDataSettings GameData { get; set; } = new();

    [JsonPropertyName("Logging")]
    public LoggingSettings Logging { get; set; } = new();
}

public class ServerSettings
{
    public string ServerName { get; set; } = "TS Dark World";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 6613;
    public int CdnPort { get; set; } = 8443;
    public int MaxConnections { get; set; } = 1000;
    public int Version { get; set; } = 258;
    public string Protocol { get; set; } = "DirectTcpSocket";
    public string Description { get; set; } = "เซิร์ฟเวอร์ TS Dark World ภาษาไทย 100% เชื่อมต่อตรงผ่าน Socket พอร์ต 6613";
}

public class DatabaseSettings
{
    public string Provider { get; set; } = "MySQL";
    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 3306;
    public string Database { get; set; } = "tsm_dodo";
    public string UserId { get; set; } = "root";
    public string Password { get; set; } = "";
    public string ConnectionString { get; set; } = "Server=localhost;Port=3306;Database=tsm_dodo;Uid=root;Pwd=;AllowUserVariables=True;CharSet=utf8mb4;";
    public bool AutoInitialize { get; set; } = true;
    public bool AutoBackup { get; set; } = true;
    public string BackupDirectory { get; set; } = @"SQL\Backups";
    public string Description { get; set; } = "การเชื่อมต่อฐานข้อมูล MySQL สำหรับ XAMPP (Localhost พอร์ต 3306 บัญชี root)";
}

public class RatesSettings
{
    public double ExpMultiplier { get; set; } = 1.0;
    public double DropMultiplier { get; set; } = 1.0;
    public double GoldMultiplier { get; set; } = 1.0;
    public double PetExpMultiplier { get; set; } = 1.0;
}

public class GameDataSettings
{
    public string DataDirectory { get; set; } = @"DATA\Data";
    public string EveDirectory { get; set; } = @"DATA\Data\Eve";
    public string Encoding { get; set; } = "windows-874";
    public string Language { get; set; } = "Thai";
    public string Description { get; set; } = "โฟลเดอร์ไฟล์ข้อมูลเกมภาษาไทย 308 ไฟล์ และระบบเควสต์ Eve.emg 10.5MB";
}

public class LoggingSettings
{
    public string LogLevel { get; set; } = "Information";
    public bool LogToFile { get; set; } = true;
    public string LogDirectory { get; set; } = "logs";
}
