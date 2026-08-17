using System.Text;
using MySqlConnector;

namespace TsmServer.Persistence.Services;

public class DatabaseBackupService
{
    private readonly string _sqlBaseDir;
    private readonly string _backupDir;

    public DatabaseBackupService(string? baseDir = null)
    {
        string root = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;

        // Find SQL directory in root or parent
        string sqlCandidate = Path.Combine(root, "SQL");
        if (!Directory.Exists(sqlCandidate))
        {
            string parentCandidate = Path.Combine(Directory.GetCurrentDirectory(), "SQL");
            if (Directory.Exists(parentCandidate))
            {
                sqlCandidate = parentCandidate;
            }
        }

        _sqlBaseDir = sqlCandidate;
        _backupDir = Path.Combine(_sqlBaseDir, "Backups");

        if (!Directory.Exists(_backupDir))
        {
            Directory.CreateDirectory(_backupDir);
        }
    }

    public string BackupDirectory => _backupDir;
    public string SqlBaseDirectory => _sqlBaseDir;

    public async Task<(bool Success, string FilePath, string Message)> CreateAutoBackupAsync(string connectionString, string reason = "การแก้ไขข้อมูลอัตโนมัติ")
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"tsm_backup_{timestamp}.sql";
        string filePath = Path.Combine(_backupDir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("-- ====================================================================");
        sb.AppendLine($"-- TS Dark World — Automatic Database Backup");
        sb.AppendLine($"-- วันที่สำรองข้อมูล: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"-- สาเหตุการสำรอง: {reason}");
        sb.AppendLine("-- ====================================================================");
        sb.AppendLine();
        sb.AppendLine("CREATE DATABASE IF NOT EXISTS `tsm_dodo` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        sb.AppendLine("USE `tsm_dodo`;");
        sb.AppendLine();

        try
        {
            // 1. Try to dump actual MySQL Database data if connected
            using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();

            string[] tables = { "accounts", "characters", "character_items", "character_pets", "character_skills", "character_quests", "character_mail", "guilds", "server_config" };

            foreach (var table in tables)
            {
                // Check if table exists
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = $"SHOW TABLES LIKE '{table}';";
                var exists = await checkCmd.ExecuteScalarAsync();
                if (exists == null) continue;

                // Dump Table Create Syntax
                using var createCmd = conn.CreateCommand();
                createCmd.CommandText = $"SHOW CREATE TABLE `{table}`;";
                using var reader = await createCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    sb.AppendLine($"-- โครงสร้างตาราง: {table}");
                    sb.AppendLine($"DROP TABLE IF EXISTS `{table}`;");
                    sb.AppendLine(reader.GetString(1) + ";");
                    sb.AppendLine();
                }
                reader.Close();

                // Dump Table Data Rows
                using var selectCmd = conn.CreateCommand();
                selectCmd.CommandText = $"SELECT * FROM `{table}`;";
                using var dataReader = await selectCmd.ExecuteReaderAsync();

                var rows = new List<string>();
                while (await dataReader.ReadAsync())
                {
                    var values = new string[dataReader.FieldCount];
                    for (int i = 0; i < dataReader.FieldCount; i++)
                    {
                        if (dataReader.IsDBNull(i))
                        {
                            values[i] = "NULL";
                        }
                        else
                        {
                            var val = dataReader.GetValue(i);
                            if (val is bool b) values[i] = b ? "1" : "0";
                            else if (val is int or long or short or byte or float or double or decimal) values[i] = val.ToString()!;
                            else values[i] = $"'{MySqlHelper.EscapeString(val?.ToString() ?? string.Empty)}'";
                        }
                    }
                    rows.Add($"({string.Join(", ", values)})");
                }
                dataReader.Close();

                if (rows.Count > 0)
                {
                    sb.AppendLine($"-- ข้อมูลในตาราง: {table} ({rows.Count} รายการ)");
                    sb.AppendLine($"INSERT INTO `{table}` VALUES");
                    sb.AppendLine(string.Join(",\n", rows) + ";");
                    sb.AppendLine();
                }
            }

            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
            return (true, filePath, $"สำรองฐานข้อมูล MySQL สำเร็จ ({fileName})");
        }
        catch
        {
            // 2. Fallback: Backup active SQL Schema template file if MySQL server is not running
            string schemaPath = Path.Combine(_sqlBaseDir, "tsm_database_schema.sql");
            if (File.Exists(schemaPath))
            {
                string schemaContent = await File.ReadAllTextAsync(schemaPath, Encoding.UTF8);
                sb.AppendLine("-- [สำรองจากแม่แบบ SQL Schema]");
                sb.AppendLine(schemaContent);
            }
            else
            {
                sb.AppendLine(DatabaseFactory.GetDefaultSchemaSql());
            }

            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
            return (true, filePath, $"สำรองไฟล์แม่แบบโครงสร้าง SQL สำเร็จ ({fileName})");
        }
    }

    public List<FileInfo> GetBackupFiles()
    {
        if (!Directory.Exists(_backupDir)) return new List<FileInfo>();
        var dir = new DirectoryInfo(_backupDir);
        return dir.GetFiles("tsm_backup_*.sql").OrderByDescending(f => f.CreationTime).ToList();
    }
}
