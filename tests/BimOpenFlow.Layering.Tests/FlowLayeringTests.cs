// Enforces the seams inside src/flow: node packs depend only on the shared Support
// pack among packs, never on the host or the run outputs; only the Effects pack sees
// run records; the relation plan/schema/compile library never sees DuckDB.
namespace BimOpenFlow.Layering.Tests;

public static class FlowLayering
{
    public const string PackPrefix = "BimOpenFlow.Nodes.";
    public const string SupportPack = "BimOpenFlow.Nodes.Support";
    public const string EffectsPack = "BimOpenFlow.Nodes.Effects";
    public const string RelationsLibrary = "BimOpenFlow.Relations";
    public const string RunsProject = "Ara3D.DataFlowEngine.Runs";

    /// <summary>Flow projects a node pack may never reference: the host and the run outputs.</summary>
    public static readonly string[] ForbiddenForPacks =
        ["BimOpenFlow.Host", "BimOpenFlow.Publishing", "BimOpenFlow.Reports", "BimOpenFlow.Dashboards", "BimOpenFlow.Evidence"];

    public static string NameOf(string projectPath)
        => Path.GetFileNameWithoutExtension(projectPath);

    public static bool IsPack(string projectName)
        => projectName.StartsWith(PackPrefix, StringComparison.Ordinal);

    public static IEnumerable<FileInfo> FlowProjects()
        => Layering.Projects("src").Where(p => Layering.GroupOf(p.FullName) == "flow");

    public static IEnumerable<(string From, string To)> FlowEdges()
        => FlowProjects().SelectMany(Layering.References).Select(e => (NameOf(e.From.FullName), NameOf(e.To)));

    public static IEnumerable<string> PackViolations()
    {
        foreach (var (from, to) in FlowEdges().Where(e => IsPack(e.From)))
        {
            if (IsPack(to) && to != SupportPack)
                yield return $"{from} -> {to} (a node pack may reference no pack but {SupportPack})";
            if (ForbiddenForPacks.Any(f => to == f || to.StartsWith(f + ".", StringComparison.Ordinal)))
                yield return $"{from} -> {to} (a node pack may not reference the host or the run outputs)";
            if (to == RunsProject && from != EffectsPack)
                yield return $"{from} -> {to} (only {EffectsPack} may see run records)";
        }
    }

    public static IEnumerable<string> RelationsViolations()
        => FlowEdges()
            .Where(e => e.From == RelationsLibrary && e.To.Contains("DuckDb", StringComparison.OrdinalIgnoreCase))
            .Select(e => $"{e.From} -> {e.To} (the relation plan, schema, and compile layers never see DuckDB)");
}

public class FlowLayeringTests
{
    [Test]
    public void NodePacksReferenceOnlySupportAmongPacksAndNeverTheHostOrOutputs()
    {
        var violations = FlowLayering.PackViolations().ToList();
        Assert.That(violations, Is.Empty, string.Join("\n", violations));
    }

    [Test]
    public void RelationsLibraryDoesNotReferenceDuckDb()
    {
        var violations = FlowLayering.RelationsViolations().ToList();
        Assert.That(violations, Is.Empty, string.Join("\n", violations));
    }

    [Test]
    public void FindsTheNodePacks()
        => Assert.That(FlowLayering.FlowProjects().Count(p => FlowLayering.IsPack(FlowLayering.NameOf(p.FullName))),
            Is.GreaterThanOrEqualTo(11), "expected at least eleven node packs under src/flow");
}
