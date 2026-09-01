namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>Locates the committed samples/tables directory by walking up from the
/// test binaries, so tests work from any build output depth.</summary>
public static class SamplePaths
{
    public static string TablesDir { get; } = FindTablesDir();

    public static string AnalysesDir
        => Path.Combine(Path.GetDirectoryName(TablesDir)!, "analyses");

    public static string Csv(string name)
        => Path.Combine(TablesDir, name + ".csv");

    private static string FindTablesDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "tables");
            if (File.Exists(Path.Combine(candidate, "customers.csv")))
                return candidate;
        }
        throw new DirectoryNotFoundException(
            $"No samples/tables directory found above '{AppContext.BaseDirectory}'.");
    }
}
