using System.Globalization;
using System.Text.Json;

namespace BimOpenFlow.Host;

/// <summary>
/// Host settings: where models live, where converted BOS files cache, where the
/// analysis library persists, and the HTTP port. Values resolve in layers:
/// defaults, then an optional appsettings.json in the base directory, then
/// BIMOPENFLOW_* environment variables, then command-line arguments.
/// </summary>
public sealed record HostConfig(
    IReadOnlyList<string> ModelRoots,
    string CacheDir,
    string StoreDir,
    int Port,
    string Profile = HostConfig.BimProfile)
{
    public const int DefaultPort = 5210;
    public const string SettingsFileName = "appsettings.json";
    public const char RootSeparator = ';';
    public const string BimProfile = "bim";
    public const string TablesProfile = "tables";
    public static readonly IReadOnlyList<string> Profiles = [BimProfile, TablesProfile];

    public static HostConfig Default(string baseDir)
        => new(
            [Path.Combine(baseDir, "models")],
            Path.Combine(baseDir, "cache"),
            Path.Combine(baseDir, "analyses"),
            DefaultPort,
            BimProfile);

    public static HostConfig Resolve(string[] args, string baseDir)
        => Default(baseDir)
            .ApplySettingsFile(Path.Combine(baseDir, SettingsFileName))
            .ApplyEnvironment()
            .ApplyArgs(args);

    /// <summary>Optional file: {"modelRoots": [...], "cacheDir": "...", "storeDir": "...", "port": n, "profile": "bim"|"tables"}.</summary>
    public HostConfig ApplySettingsFile(string path)
    {
        if (!File.Exists(path))
            return this;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        return this with
        {
            ModelRoots = root.TryGetProperty("modelRoots", out var roots)
                ? roots.EnumerateArray().Select(e => e.GetString()!).ToList()
                : ModelRoots,
            CacheDir = StringOr(root, "cacheDir", CacheDir),
            StoreDir = StringOr(root, "storeDir", StoreDir),
            Port = root.TryGetProperty("port", out var port) ? port.GetInt32() : Port,
            Profile = ValidProfile(StringOr(root, "profile", Profile)),
        };
    }

    public HostConfig ApplyEnvironment()
        => this with
        {
            ModelRoots = SplitRoots(Environment.GetEnvironmentVariable("BIMOPENFLOW_MODEL_ROOTS")) ?? ModelRoots,
            CacheDir = Environment.GetEnvironmentVariable("BIMOPENFLOW_CACHE_DIR") ?? CacheDir,
            StoreDir = Environment.GetEnvironmentVariable("BIMOPENFLOW_STORE_DIR") ?? StoreDir,
            Port = ParsePort(Environment.GetEnvironmentVariable("BIMOPENFLOW_PORT")) ?? Port,
            Profile = ValidProfile(Environment.GetEnvironmentVariable("BIMOPENFLOW_PROFILE") ?? Profile),
        };

    /// <summary>--models a;b --cache dir --store dir --port n --profile bim|tables</summary>
    public HostConfig ApplyArgs(string[] args)
    {
        var config = this;
        for (var i = 0; i < args.Length; i += 2)
        {
            var value = i + 1 < args.Length
                ? args[i + 1]
                : throw new ArgumentException($"Option '{args[i]}' is missing a value");
            config = args[i].ToLowerInvariant() switch
            {
                "--models" => config with { ModelRoots = SplitRoots(value)! },
                "--cache" => config with { CacheDir = value },
                "--store" => config with { StoreDir = value },
                "--port" => config with
                {
                    Port = ParsePort(value) ?? throw new ArgumentException($"Invalid port '{value}'"),
                },
                "--profile" => config with { Profile = ValidProfile(value) },
                _ => throw new ArgumentException(
                    $"Unknown option '{args[i]}'. Expected --models, --cache, --store, --port, or --profile."),
            };
        }
        return config;
    }

    private static string ValidProfile(string value)
        => Profiles.Contains(value)
            ? value
            : throw new ArgumentException(
                $"Invalid profile '{value}'. Allowed values: {string.Join(", ", Profiles)}.");

    private static string StringOr(JsonElement root, string name, string fallback)
        => root.TryGetProperty(name, out var value) ? value.GetString() ?? fallback : fallback;

    private static IReadOnlyList<string>? SplitRoots(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? null
            : text.Split(RootSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int? ParsePort(string? text)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) ? port : null;
}
