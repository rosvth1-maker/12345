using System.Data;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace TsmServer.Persistence;

public class DatabaseFactory
{
    private readonly string _connectionString;
    private readonly bool _useInMemoryFallback;

    public DatabaseFactory(string connectionString, bool useInMemoryFallback = true)
    {
        _connectionString = connectionString;
        _useInMemoryFallback = useInMemoryFallback;
    }

    public IDbConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync()
    {
        try
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            return (true, $"เชื่อมต่อ MySQL สำเร็จ (เวอร์ชัน: {conn.ServerVersion})");
        }
        catch (Exception ex)
        {
            return (false, $"ไม่สามารถเชื่อมต่อ MySQL ได้: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> InitializeDatabaseAndTablesAsync(string? sqlScript = null)
    {
        try
        {
            // 1. First connect without database parameter to ensure DB exists
            var builder = new MySqlConnectionStringBuilder(_connectionString);
            string targetDb = string.IsNullOrEmpty(builder.Database) ? "tsm_dodo" : builder.Database;

            var rootBuilder = new MySqlConnectionStringBuilder(_connectionString)
            {
                Database = ""
            };

            using (var rootConn = new MySqlConnection(rootBuilder.ConnectionString))
            {
                await rootConn.OpenAsync();
                using var createDbCmd = rootConn.CreateCommand();
                createDbCmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{targetDb}` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                await createDbCmd.ExecuteNonQueryAsync();
            }

            // 2. Connect to the target database and execute table creation
            using (var targetConn = new MySqlConnection(_connectionString))
            {
                await targetConn.OpenAsync();

                string script = sqlScript ?? GetDefaultSchemaSql();

                // Split statements by semicolon
                var statements = Regex.Split(script, @";\s*[\r\n]+")
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith("--"));

                int count = 0;
                foreach (var stmt in statements)
                {
                    if (string.IsNullOrWhiteSpace(stmt)) continue;
                    using var cmd = targetConn.CreateCommand();
                    cmd.CommandText = stmt;
                    await cmd.ExecuteNonQueryAsync();
                    count++;
                }

                return (true, $"สร้างและอัปเดตฐานข้อมูล `{targetDb}` สำเร็จ (รัน {count} คำสั่ง SQL)");
            }
        }
        catch (Exception ex)
        {
            return (false, $"เกิดข้อผิดพลาดในการสร้างฐานข้อมูล: {ex.Message}");
        }
    }

    public static string GetDefaultSchemaSql()
    {
        return @"
CREATE TABLE IF NOT EXISTS `accounts` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `username` VARCHAR(64) NOT NULL UNIQUE,
    `password_hash` VARCHAR(255) NOT NULL,
    `gm_level` INT NOT NULL DEFAULT 0,
    `status` INT NOT NULL DEFAULT 1,
    `email` VARCHAR(128) NULL,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `last_login` DATETIME NULL,
    INDEX `idx_username` (`username`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `characters` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `account_id` INT NOT NULL,
    `account_name` VARCHAR(64) NOT NULL,
    `name` VARCHAR(64) NOT NULL,
    `element` TINYINT NOT NULL DEFAULT 1,
    `gender` TINYINT NOT NULL DEFAULT 1,
    `level` INT NOT NULL DEFAULT 1,
    `exp` BIGINT NOT NULL DEFAULT 0,
    `map_id` INT NOT NULL DEFAULT 10801,
    `x` INT NOT NULL DEFAULT 570,
    `y` INT NOT NULL DEFAULT 770,
    `hp` INT NOT NULL DEFAULT 500,
    `max_hp` INT NOT NULL DEFAULT 500,
    `sp` INT NOT NULL DEFAULT 200,
    `max_sp` INT NOT NULL DEFAULT 200,
    `int_val` INT NOT NULL DEFAULT 10,
    `atk_val` INT NOT NULL DEFAULT 10,
    `def_val` INT NOT NULL DEFAULT 10,
    `hpa_val` INT NOT NULL DEFAULT 10,
    `spa_val` INT NOT NULL DEFAULT 10,
    `agi_val` INT NOT NULL DEFAULT 10,
    `free_points` INT NOT NULL DEFAULT 0,
    `skill_points` INT NOT NULL DEFAULT 0,
    `gold` BIGINT NOT NULL DEFAULT 10000,
    `silver` BIGINT NOT NULL DEFAULT 0,
    `yuanbao` BIGINT NOT NULL DEFAULT 0,
    `pk_mode` TINYINT NOT NULL DEFAULT 0,
    `state` INT NOT NULL DEFAULT 0,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_account_id` (`account_id`),
    INDEX `idx_char_name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `character_items` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `character_id` INT NOT NULL,
    `slot` INT NOT NULL,
    `item_id` INT NOT NULL,
    `count` INT NOT NULL DEFAULT 1,
    `durability` INT NOT NULL DEFAULT 100,
    `refine_level` INT NOT NULL DEFAULT 0,
    `gem_slot_1` INT NOT NULL DEFAULT 0,
    `gem_slot_2` INT NOT NULL DEFAULT 0,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX `idx_char_items` (`character_id`, `slot`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `character_pets` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `character_id` INT NOT NULL,
    `slot` INT NOT NULL,
    `npc_id` INT NOT NULL,
    `custom_name` VARCHAR(64) NOT NULL,
    `level` INT NOT NULL DEFAULT 1,
    `exp` BIGINT NOT NULL DEFAULT 0,
    `hp` INT NOT NULL DEFAULT 500,
    `max_hp` INT NOT NULL DEFAULT 500,
    `sp` INT NOT NULL DEFAULT 200,
    `max_sp` INT NOT NULL DEFAULT 200,
    `int_val` INT NOT NULL DEFAULT 10,
    `atk_val` INT NOT NULL DEFAULT 10,
    `def_val` INT NOT NULL DEFAULT 10,
    `hpa_val` INT NOT NULL DEFAULT 10,
    `spa_val` INT NOT NULL DEFAULT 10,
    `agi_val` INT NOT NULL DEFAULT 10,
    `loyalty` INT NOT NULL DEFAULT 100,
    `is_deployed` BOOLEAN NOT NULL DEFAULT TRUE,
    `skill_1` INT NOT NULL DEFAULT 0,
    `skill_2` INT NOT NULL DEFAULT 0,
    `skill_3` INT NOT NULL DEFAULT 0,
    `skill_4` INT NOT NULL DEFAULT 0,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX `idx_char_pets` (`character_id`, `slot`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `character_skills` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `character_id` INT NOT NULL,
    `skill_id` INT NOT NULL,
    `skill_level` INT NOT NULL DEFAULT 1,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY `uk_char_skill` (`character_id`, `skill_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `character_quests` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `character_id` INT NOT NULL,
    `quest_id` INT NOT NULL,
    `state` INT NOT NULL DEFAULT 0,
    `step` INT NOT NULL DEFAULT 0,
    `data` VARCHAR(255) NULL,
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY `uk_char_quest` (`character_id`, `quest_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `server_config` (
    `config_key` VARCHAR(64) PRIMARY KEY,
    `config_val` TEXT NOT NULL,
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
";
    }
}
