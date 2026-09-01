namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>
/// Regenerates the committed binary sample fixtures next to the CSVs in
/// samples/tables. Run explicitly after editing a CSV, then commit the outputs.
/// </summary>
[TestFixture]
public sealed class SampleDataSeeder
{
    [Test, Explicit("seeds sample binaries")]
    public void SeedSampleBinaries()
    {
        var written = SampleFixtures.SeedAll(SamplePaths.TablesDir, SamplePaths.TablesDir);
        Assert.That(written.Select(File.Exists), Has.All.True);
    }
}
