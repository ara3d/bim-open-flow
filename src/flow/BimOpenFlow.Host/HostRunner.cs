namespace BimOpenFlow.Host;

/// <summary>The host's main routine, shared with front ends that add endpoints
/// to the same application (the studio's ask endpoint): resolve settings, seed
/// samples, let the caller map more routes, start, print where we are, wait.</summary>
public static class HostRunner
{
    public static async Task<int> RunAsync(string[] args, Action<HostApp>? configure = null, string name = "BimOpenFlow host")
    {
        var config = HostConfig.Resolve(args, Environment.CurrentDirectory);
        if (config.Profile == HostConfig.BimProfile)
            config = WithSeededRoots(config);
        var host = HostComposition.Build(config);

        var seeded = config.Profile == HostConfig.TablesProfile
            ? SampleSeeding.SeedIfEmpty(host.Services.Store, AppContext.BaseDirectory)
            : BimSampleSeeding.SeedIfEmpty(host.Services.Store, AppContext.BaseDirectory);
        foreach (var id in seeded)
            Console.WriteLine($"  seeded sample analysis: {id}");

        configure?.Invoke(host);
        await host.App.StartAsync();

        Console.WriteLine($"{name} listening at {host.App.Urls.First()}");
        Console.WriteLine($"  model roots: {string.Join(HostConfig.RootSeparator, config.ModelRoots)}");
        Console.WriteLine($"  cache dir:   {config.CacheDir}");
        Console.WriteLine($"  store dir:   {config.StoreDir}");
        Console.WriteLine($"  profile:     {config.Profile}");

        await host.App.WaitForShutdownAsync();
        return 0;
    }

    private static HostConfig WithSeededRoots(HostConfig config)
    {
        var known = config.ModelRoots
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seededRoots = BimSampleSeeding.SeededModelRoots(AppContext.BaseDirectory)
            .Where(r => !known.Contains(Path.GetFullPath(r)))
            .ToList();
        return seededRoots.Count > 0
            ? config with { ModelRoots = [.. config.ModelRoots, .. seededRoots] }
            : config;
    }
}
