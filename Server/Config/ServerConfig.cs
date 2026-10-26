using System.Text.Json;
using SphServer.Helpers;
using SphServer.Shared.Logger;

namespace SphServer.Server.Config;

public class AppConfig
{
    // An existing configured path wins; otherwise the path is the repository root the process was
    // launched from
    public string RepositoryPath { get; init; } = string.Empty;

    public string PacketDefinitionPath { get; init; } = string.Empty;

    public string DecodedGameDataPath { get; init; } = string.Empty;

    public string LiteDbConnectionString { get; init; } =
        @"Filename=sph.db;Connection=shared;";

    public ushort Port { get; init; } = 25860;
    public string LogPath { get; init; } = @"logs\server.log";
    public bool DebugMode { get; init; } = true;

    /// <summary>
    ///     Lets a use-on-self frame (Alt-self, or an AoE aimed at self) hit the attacker. Off by default:
    ///     any stray self-targeted gesture with a weapon in hand otherwise hurts or kills the player.
    /// </summary>
    public bool AllowSelfAttack { get; init; }

    /// <summary>Seconds from the death hit until the player is revived (retail: 8.3 s, measured twice).</summary>
    public float RespawnDelaySeconds { get; init; } = 8.3f;

    /// <summary>Revive in the nearest city, as retail does, instead of where the player died.</summary>
    public bool RespawnAtCity { get; init; } = true;

    /// <summary>Percent of title and degree experience lost on death. Retail: about 5.75 (one level 31 character, two
    /// deaths), so it is off by default until more levels confirm it. 0 means no loss.</summary>
    public float DeathExperienceLossPercent { get; init; }

    /// <summary>Account logins allowed to use GM commands, separated by commas. Empty means nobody.</summary>
    public string GmAccounts { get; init; } = string.Empty;

    /// <summary>
    ///     Lets a GM login be created by its first login. Off by default: on a fresh database the first person to
    ///     log in under a listed name would otherwise become GM. Turn it on once, on your own machine, to create
    ///     your own GM account, then turn it off.
    /// </summary>
    public bool GmAccountBootstrap { get; init; }

    public bool IsGmAccount(string? login) =>
        !string.IsNullOrWhiteSpace(login) &&
        GmAccounts.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(login, StringComparer.OrdinalIgnoreCase);

    /// <summary>Monsters path-find to players inside their aggro radius. Off in the original project, never verified.</summary>
    public bool MobChaseEnabled { get; init; }

    /// <summary>Sends the world clock (ClientSky) to clients. Off: it only moved the HUD date, not the time of day.</summary>
    public bool GameClockSync { get; init; }

    /// <summary>Monsters leave players near a city centre alone (PvP unaffected). /safezone flips it at runtime.</summary>
    public bool CityMonsterProtection { get; init; } = true;

    /// <summary>Radius around each city centre covered by CityMonsterProtection.</summary>
    public float CityProtectionRadiusMeters { get; init; } = 150f;

    public float ObjectVisibilityDistance { get; init; } = 100.0f;
    public int ReceiveBufferSize { get; init; } = 1024;
    public int CurrentCharacterInventoryId { get; init; } = 0xA001;
    public float Spawn_X { get; init; } = 80.0f;
    public float Spawn_Y { get; init; } = 150.0f;
    public float Spawn_Z { get; init; } = -200.0f;
    public float Spawn_Angle { get; init; } = 0.75f;
    public int Spawn_Money { get; init; } = 99999999;
}

public static class ServerConfig
{
    private static readonly object AppConfigLock = new();
    private static AppConfig? _appConfig;

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new() { WriteIndented = true };

    public static AppConfig AppConfig
    {
        get
        {
            if (_appConfig is not null)
            {
                return _appConfig;
            }

            lock (AppConfigLock)
            {
                return _appConfig ??= Get();
            }
        }
        private set => _appConfig = value ?? new AppConfig();
    }

    // Note: no static ctor here on purpose. Godot can load types in unusual orders / threads;
    // lazy initialization avoids intermittent nulls and makes first-access deterministic.

    public static AppConfig Get()
    {
        try
        {
            var configPath = FindConfigPath("appsettings.json");

            if (!File.Exists(configPath))
            {
                SphLogger.Info($"Configuration file not found, creating default: {configPath}");
                CreateDefaultAppConfig(configPath);
            }
            else
            {
                SphLogger.Info($"Loading configuration from: {configPath}");
            }

            // Not held open: SaveAppConfig's rewrite hits a Windows sharing violation, and that
            // catch drops the whole config
            var configJson = File.ReadAllText(configPath);

            var configDict = JsonSerializer.Deserialize<Dictionary<string, string>>(configJson, JsonReadOptions) ??
                             new();

            var defaultSettings = GetDefaultAppConfigDict();
            var configChanged = false;

            foreach (var defaultSetting in defaultSettings.Where(defaultSetting =>
                         !configDict.ContainsKey(defaultSetting.Key)))
            {
                configDict[defaultSetting.Key] = defaultSetting.Value;
                configChanged = true;
            }

            if (configChanged)
            {
                SaveAppConfig(configPath, configDict);
            }

            // A configured path is used only when Sphere.PacketDefinitions exists there; a
            // hardcoded default boots an empty world because those load failures are swallowed
            var repositoryPath = configDict.GetValueOrDefault("RepositoryPath") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(repositoryPath) ||
                !Directory.Exists(Path.Combine(repositoryPath, "Sphere.PacketDefinitions")))
            {
                var repositoryRoot = FindRepositoryRoot(configPath);
                if (repositoryRoot is not null)
                {
                    SphLogger.Info(string.IsNullOrWhiteSpace(repositoryPath)
                        ? $"Using repository root: {repositoryRoot}"
                        : $"RepositoryPath '{repositoryPath}' is not a repository; using repository root: {repositoryRoot}");
                    repositoryPath = repositoryRoot;
                }
                else if (string.IsNullOrWhiteSpace(repositoryPath))
                {
                    // "." so Path.Combine below cannot throw; the next check reports the missing
                    // repository
                    repositoryPath = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".";
                }
            }

            var packetDefinitionPath = configDict.GetValueOrDefault("PacketDefinitionPath",
                Path.Combine(repositoryPath, "Sphere.PacketDefinitions"));
            if (!Directory.Exists(packetDefinitionPath))
            {
                SphLogger.Error($"Packet definitions not found at '{packetDefinitionPath}' — entity spawn " +
                                "frames will fail and the world will look empty. Set RepositoryPath in appsettings.json.");
            }

            var logPath = configDict.GetValueOrDefault("LogPath", @"logs\server.log");
            if (!Path.IsPathRooted(logPath))
            {
                logPath = Path.Combine(repositoryPath, logPath);
            }

            return new AppConfig
            {
                RepositoryPath = repositoryPath,
                PacketDefinitionPath = packetDefinitionPath,
                DecodedGameDataPath = configDict.GetValueOrDefault("DecodedGameDataPath",
                    Path.Combine(repositoryPath, "Sphere.GameDataDecode")),
                LiteDbConnectionString = configDict.GetValueOrDefault("LiteDbConnectionString",
                    @"Filename=sph.db;Connection=shared;"),
                Port = FileFormatCulture.ParseUShort(configDict.GetValueOrDefault("Port", "25860")),
                LogPath = logPath,
                DebugMode = bool.Parse(configDict.GetValueOrDefault("DebugMode", "true")),
                AllowSelfAttack = bool.Parse(configDict.GetValueOrDefault("AllowSelfAttack", "false")),
                GmAccounts = configDict.GetValueOrDefault("GmAccounts", string.Empty),
                GmAccountBootstrap = bool.Parse(configDict.GetValueOrDefault("GmAccountBootstrap", "false")),
                MobChaseEnabled = bool.Parse(configDict.GetValueOrDefault("MobChaseEnabled", "false")),
                GameClockSync = bool.Parse(configDict.GetValueOrDefault("GameClockSync", "false")),
                CityMonsterProtection = bool.Parse(configDict.GetValueOrDefault("CityMonsterProtection", "true")),
                CityProtectionRadiusMeters =
                    FileFormatCulture.ParseFloat(configDict.GetValueOrDefault("CityProtectionRadiusMeters", "150.0")),
                RespawnDelaySeconds =
                    FileFormatCulture.ParseFloat(configDict.GetValueOrDefault("RespawnDelaySeconds", "8.3")),
                RespawnAtCity = bool.Parse(configDict.GetValueOrDefault("RespawnAtCity", "true")),
                DeathExperienceLossPercent =
                    FileFormatCulture.ParseFloat(configDict.GetValueOrDefault("DeathExperienceLossPercent", "0.0")),
                ObjectVisibilityDistance =
                    FileFormatCulture.ParseFloat(configDict.GetValueOrDefault("ObjectVisibilityDistance", "100.0")),
                ReceiveBufferSize = FileFormatCulture.ParseInt(configDict.GetValueOrDefault("ReceiveBufferSize", "1024")),
                CurrentCharacterInventoryId =
                    FileFormatCulture.ParseInt(configDict.GetValueOrDefault("CurrentCharacterInventoryId", "40961")),
                Spawn_X = FileFormatCulture.ParseFloat(configDict.GetValueOrDefault("Spawn_X", "80.0")),
                Spawn_Y = FileFormatCulture.ParseFloat(configDict.GetValueOrDefault("Spawn_Y", "150.0")),
                Spawn_Z = FileFormatCulture.ParseFloat(configDict.GetValueOrDefault("Spawn_Z", "200.0")),
                Spawn_Angle = FileFormatCulture.ParseFloat(configDict.GetValueOrDefault("Spawn_Angle", "0.75")),
                Spawn_Money = FileFormatCulture.ParseInt(configDict.GetValueOrDefault("Spawn_Money", "99999999"))
            };
        }
        catch (Exception ex)
        {
            SphLogger.Info($"Failed to load appsettings.json, using defaults. Error: {ex}");
            return new AppConfig();
        }
    }

    private static string? FindRepositoryRoot(string configPath)
    {
        // Embedded outputs land in AppData data_*, so the walk starts at the exe dir, where slim
        // builds keep Sphere.PacketDefinitions
        foreach (var startDir in new[]
                 {
                     GetExecutableDirectory(),
                     Path.GetDirectoryName(Path.GetFullPath(configPath)),
                     AppContext.BaseDirectory
                 })
        {
            if (string.IsNullOrWhiteSpace(startDir))
            {
                continue;
            }

            var dir = new DirectoryInfo(startDir);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Sphere.PacketDefinitions")))
            {
                dir = dir.Parent;
            }

            if (dir is not null)
            {
                return dir.FullName;
            }
        }

        return null;
    }

    /// <summary>
    /// Embedded outputs extract under %LocalAppData%/data_*, which is not where sidecars sit
    /// </summary>
    private static string? GetExecutableDirectory()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                return Path.GetDirectoryName(Path.GetFullPath(processPath));
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string FindConfigPath(string fileName)
    {
        // Exe directory first: AppData data_* must not win when appsettings.json sits next to the
        // exe
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var exeDir = GetExecutableDirectory();
        if (!string.IsNullOrWhiteSpace(exeDir))
        {
            var nextToExe = Path.Combine(exeDir, fileName);
            if (File.Exists(nextToExe))
            {
                return nextToExe;
            }
        }

        foreach (var startDir in new[]
                 {
                     exeDir,
                     AppContext.BaseDirectory,
                     Environment.CurrentDirectory
                 })
        {
            if (string.IsNullOrWhiteSpace(startDir))
            {
                continue;
            }

            var dir = new DirectoryInfo(startDir);
            while (dir is not null && visited.Add(dir.FullName))
            {
                var candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }
        }

        // Next to the exe when that directory is known
        if (!string.IsNullOrWhiteSpace(exeDir))
        {
            return Path.Combine(exeDir, fileName);
        }

        return fileName;
    }

    private static void CreateDefaultAppConfig(string configPath)
    {
        var defaultConfig = GetDefaultAppConfigDict();
        var json = JsonSerializer.Serialize(defaultConfig, JsonWriteOptions);
        File.WriteAllText(configPath, json);
        SphLogger.Info($"Created default configuration file: {configPath}");
    }

    private static Dictionary<string, string> GetDefaultAppConfigDict()
    {
        return new()
        {
            // Omitted on purpose: a written value would put back a path Get() derives, including
            // configs that cleared it
            ["LiteDbConnectionString"] = @"Filename=sph.db;Connection=shared;",
            ["Port"] = "25860",
            ["LogPath"] = @"logs\server.log",
            ["DebugMode"] = "true",
            ["AllowSelfAttack"] = "false",
            ["RespawnDelaySeconds"] = "8.3",
            ["RespawnAtCity"] = "true",
            ["DeathExperienceLossPercent"] = "0.0",
            ["GmAccounts"] = "",
            ["GmAccountBootstrap"] = "false",
            ["MobChaseEnabled"] = "false",
            ["GameClockSync"] = "false",
            ["CityMonsterProtection"] = "true",
            ["CityProtectionRadiusMeters"] = "150.0",
            ["ObjectVisibilityDistance"] = "100.0",
            ["ReceiveBufferSize"] = "1024",
            ["CurrentCharacterInventoryId"] = "40961",
            ["Spawn_X"] = "80.0",
            ["Spawn_Y"] = "150.0",
            ["Spawn_Z"] = "200.0",
            ["Spawn_Angle"] = "0.75",
            ["Spawn_Money"] = "99999999"
        };
    }

    private static void SaveAppConfig(string configPath, Dictionary<string, string> config)
    {
        var json = JsonSerializer.Serialize(config, JsonWriteOptions);
        File.WriteAllText(configPath, json);
        SphLogger.Info("Updated configuration file with missing default values");
    }
}