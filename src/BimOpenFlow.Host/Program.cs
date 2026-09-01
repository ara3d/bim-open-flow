using BimOpenFlow.Host;

var config = HostConfig.Resolve(args, Environment.CurrentDirectory);
var host = HostComposition.Build(config);

var seeded = config.Profile == HostConfig.TablesProfile
    ? SampleSeeding.SeedIfEmpty(host.Services.Store, AppContext.BaseDirectory)
    : BimSampleSeeding.SeedIfEmpty(host.Services.Store, AppContext.BaseDirectory);
foreach (var id in seeded)
    Console.WriteLine($"  seeded sample analysis: {id}");

await host.App.StartAsync();

Console.WriteLine($"BimOpenFlow host listening at {host.App.Urls.First()}");
Console.WriteLine($"  model roots: {string.Join(HostConfig.RootSeparator, config.ModelRoots)}");
Console.WriteLine($"  cache dir:   {config.CacheDir}");
Console.WriteLine($"  store dir:   {config.StoreDir}");
Console.WriteLine($"  profile:     {config.Profile}");

await host.App.WaitForShutdownAsync();
return 0;
