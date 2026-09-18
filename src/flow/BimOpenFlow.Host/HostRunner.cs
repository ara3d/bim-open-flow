namespace BimOpenFlow.Host;

/// <summary>The host's main routine, shared with front ends that add endpoints
/// to the same application (the studio's ask endpoint): resolve settings, seed
/// samples, let the caller map more routes, start, print where we are, prepare the
/// slow generated samples in the background, wait. Start-up itself stays under a
/// second or two; nothing that takes longer runs before the port is open.</summary>
public static class HostRunner
{
    public static async Task<int> RunAsync(string[] args, Action<HostApp>? configure = null, string name = "BimOpenFlow host")
    {
        var config = HostConfig.Resolve(args, Environment.CurrentDirectory);
        config = WithSeededRoots(config, config.Profile == HostConfig.TablesProfile
            ? SampleSeeding.SeededModelRoots(AppContext.BaseDirectory)
            : BimSampleSeeding.SeededModelRoots(AppContext.BaseDirectory));
        var preparing = SamplePreparation.Jobs(AppContext.BaseDirectory);
        var host = HostComposition.Build(config, preparing);

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

        var preparation = SamplePreparation.RunInBackground(preparing, _ =>
        {
            host.Services.Relations.Invalidate();
            host.Services.Sessions.Reevaluate();
        }, Console.Out);

        await host.App.WaitForShutdownAsync();
        await preparation;
        return 0;
    }

    private static HostConfig WithSeededRoots(HostConfig config, IReadOnlyList<string> roots)
    {
        var known = config.ModelRoots
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seededRoots = roots
            .Where(r => !known.Contains(Path.GetFullPath(r)))
            .ToList();
        return seededRoots.Count > 0
            ? config with { ModelRoots = [.. config.ModelRoots, .. seededRoots] }
            : config;
    }
}
