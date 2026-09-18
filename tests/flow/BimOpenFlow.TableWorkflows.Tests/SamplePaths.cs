using BimOpenToolkit.TestSupport;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>The committed samples/tables data and the samples/analyses graphs over it.</summary>
public static class SamplePaths
{
    public static string TablesDir => RepoPaths.Samples("tables");

    public static string AnalysesDir => RepoPaths.Samples("analyses");

    public static string Csv(string name)
        => Path.Combine(TablesDir, name + ".csv");
}
