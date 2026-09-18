using Ara3D.DataFlowEngine;
using Ara3D.NodeGraph;
using BimOpenToolkit.TestSupport;

namespace BimOpenFlow.Nodes.Relations.Tests;

/// <summary>Every committed samples/relations/*.json parses, validates against the pack,
/// and evaluates green over samples/tables, except schema-error, whose answer node
/// must report the misspelled column.</summary>
[TestFixture]
public sealed class SampleGraphTests
{
    public static IEnumerable<TestCaseData> SampleFiles
        => Directory.EnumerateFiles(RepoPaths.Samples("relations"), "*.json")
            .Order(StringComparer.Ordinal)
            .Select(f => new TestCaseData(f).SetArgDisplayNames(Path.GetFileNameWithoutExtension(f)));

    private static NodeRegistry Registry
        => new(RelationNodes.All(RelationRuntime.FromRoots([RepoPaths.Samples("tables")])));

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
