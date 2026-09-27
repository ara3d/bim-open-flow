using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Runs;
using Ara3D.NodeGraph;
using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Catalog;
using NodeStatus = Ara3D.DataFlowEngine.NodeStatus;

namespace BimOpenFlow.Host.Api.Tests;

/// <summary>A fake IRelationResults whose SourceFiles always names one file, regardless
/// of the relation given, so the tests can check RunInputs without a real registry.</summary>
public sealed class FakeRelationResults(string file) : IRelationResults
{
    public TableSlice Slice(RelationValue relation, int skip, int take) => throw new NotImplementedException();
    public IReadOnlyList<Suggestion> Columns(RelationValue relation) => throw new NotImplementedException();
    public IReadOnlyList<string> SourceFiles(RelationValue relation) => [file];
}

[TestFixture]
public sealed class RunInputsTests
{
    private static GraphDocument SingleNodeDoc(string kind = "rel.source")
        => new(
            [new GraphNode("src", kind, 1)],
            [],
            new Dictionary<string, IReadOnlyDictionary<string, string>>(),
            new Dictionary<string, NodeLayout>());

    private static ModelCatalog Catalog(string root)
        => new(root, Path.Combine(root, "cache"));

    private static EvalSnapshot SnapshotWithRelationOutput(GraphDocument doc, RelationValue relation)
        => new(doc,
            new Dictionary<string, NodeResult>
            {
                ["src"] = new("src", NodeStatus.Ok, [relation], ["out-hash"], [], []),
            },
            []);

    [Test]
    public void RelationSourceFile_IsPinnedAtItsNodeUnderSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "bof-runinputs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "source.duckdb");
            File.WriteAllText(file, "content");
            var doc = SingleNodeDoc();
            var relation = new RelationValue("scan db.t", "plan-hash");
            var snapshot = SnapshotWithRelationOutput(doc, relation);

            var inputs = RunInputs.Derive(snapshot, new NodeRegistry([]), Catalog(root), new FakeRelationResults(file));

            Assert.That(inputs, Is.EqualTo(new[] { new RunInput("src", "source", ModelCatalog.HashFile(file), file) }));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void NullRelations_MatchesTheDocumentOverload()
    {
        var root = Path.Combine(Path.GetTempPath(), "bof-runinputs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var doc = SingleNodeDoc();
            var relation = new RelationValue("scan db.t", "plan-hash");
            var snapshot = SnapshotWithRelationOutput(doc, relation);
            var registry = new NodeRegistry([]);
            var catalog = Catalog(root);

            var withoutRelations = RunInputs.Derive(snapshot, registry, catalog, relations: null);
            var documentOverload = RunInputs.Derive(doc, registry, catalog);

            Assert.That(withoutRelations, Is.EqualTo(documentOverload));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
