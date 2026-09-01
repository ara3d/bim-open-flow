using Ara3D.Utils;
using BimOpenFlow.Host.Store;
using BimOpenFlow.Nodes.BimAnalysis;

namespace BimOpenFlow.Host;

/// <summary>
/// Seeds an empty analysis store with the committed BIM sample analyses:
/// samples/bim-analyses/*.json with {SAMPLES} pointed at samples/bim (the
/// sample.bos there is generated from BimSampleModel when absent — the model
/// binary is never committed), and samples/view3d-analyses/*.json with {DATA}
/// pointed at the repo data directory. A non-empty store is never touched.
/// </summary>
public static class BimSampleSeeding
{
    public const string SampleFileName = "sample.bos";
    public const string DataPlaceholder = "{DATA}";

    public static IReadOnlyList<string> SeedIfEmpty(AnalysisStore store, string startDir)
    {
        if (SampleSeeding.FindRepoRoot(startDir) is not { } root)
            return [];
        var samplesDir = Path.Combine(root, "samples", "bim");
        EnsureSampleModel(samplesDir);
        return SampleSeeding.SeedIfEmpty(store,
        [
            (Path.Combine(root, "samples", "bim-analyses"), SampleSeeding.PathPlaceholder, samplesDir),
            (Path.Combine(root, "samples", "view3d-analyses"), DataPlaceholder, Path.Combine(root, "data")),
        ]);
    }

    public static string EnsureSampleModel(string samplesDir)
    {
        var path = Path.Combine(samplesDir, SampleFileName);
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(samplesDir);
            Ara3D.BimOpenSchema.IO.ParquetUtils.WriteToParquetZip(BimSampleModel.Build(), new FilePath(path));
        }
        return path;
    }
}
