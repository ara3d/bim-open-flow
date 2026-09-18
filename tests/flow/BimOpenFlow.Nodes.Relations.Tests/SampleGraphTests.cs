using Ara3D.DataFlowEngine;
using Ara3D.NodeGraph;

namespace BimOpenFlow.Nodes.Relations.Tests;

/// <summary>Every committed samples/relations/*.json parses, validates against the pack,
/// and evaluates green over samples/tables, except schema-error, whose answer node
/// must report the misspelled column.</summary>
[TestFixture]
public sealed class SampleGraphTests
{
    private static string Root
        => FindRoot(AppContext.BaseDirectory);

    private static string FindRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "BimOpenToolkit.sln")))
                return dir.FullName;
        throw new InvalidOperationException("BimOpenToolkit.sln not found above " + start);
    }

    public static IEnumerable<TestCaseData> SampleFiles
        => Directory.EnumerateFiles(Path.Combine(Root, "samples", "relations"), "*.json")
            .Order(StringComparer.Ordinal)
            .Select(f => new TestCaseData(f).SetArgDisplayNames(Path.GetFileNameWithoutExtension(f)));

    private static NodeRegistry Registry
        => new(RelationNodes.All(RelationRuntime.FromRoots([Path.Combine(Root, "samples", "tables")])));

    [Test]
    public void ThereAreSamples()
        => Assert.That(SampleFiles.Count(), Is.GreaterThanOrEqualTo(4));

    [TestCaseSource(nameof(SampleFiles))]
    public void ParsesAndValidates(string file)
    {
        var doc = GraphDocumentIO.Load(file);
        Assert.That(doc.Nodes, Is.Not.Empty);
        Assert.That(doc.Validate(Registry), Is.Empty);
    }

    [TestCaseSource(nameof(SampleFiles))]
    public void EvaluatesAsDocumented(string file)
    {
        var snapshot = GraphDocumentIO.Load(file).Evaluate(Registry);
        var failed = snapshot.Results.Where(r => r.Value.Status != NodeStatus.Ok)
            .Select(r => $"{r.Key}: {r.Value.Error}").ToList();
        if (Path.GetFileNameWithoutExtension(file) == "schema-error")
            Assert.That(failed, Is.EqualTo(new[] { "answer: ArgumentException: rel.filter: In predicate: Unknown identifier 'Quantiy' (at 0)" }));
        else
            Assert.That(failed, Is.Empty, Path.GetFileName(file));
    }
}
