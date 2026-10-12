using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.TestKit;
using BimOpenFlow.Contracts;
using NodeCapability = Ara3D.DataFlowEngine.Abstractions.NodeCapability;
using ColumnType = Ara3D.DataFlowEngine.Abstractions.ColumnType;
using ColumnUse = Ara3D.DataFlowEngine.Abstractions.ColumnUse;
using ParamKind = Ara3D.DataFlowEngine.Abstractions.ParamKind;
using PortType = Ara3D.DataFlowEngine.Abstractions.PortType;

namespace BimOpenFlow.Host.Api.Tests;

/// <summary>Column suggestions read from the static schema pass: the upstream nodes fail, so no result
/// exists, yet the pickers are filled, typed, explained and ordered by the parameter's declared use.</summary>
public sealed class SuggestFromSchemaTests
{
    private static IFlowNode FailingSource(string kind, Schema schema, Dictionary<string, string> kinds)
        => new DelegateNode(
            new(kind, 1, NodeCapability.Pure, Inputs: [], Outputs: [new PortSpec("out", PortType.Table)],
                Params: [], "Declares its columns, then fails.", Schemas: [SchemaSpec.Static("out", schema, kinds)]),
            (_, _, _) => throw new InvalidOperationException("no data yet"));

    private static readonly IFlowNode Elements = FailingSource("test.elements",
        new Schema([new("Volume", ColumnType.Number), new("Name", ColumnType.Text), new("Category", ColumnType.Text),
            new("EntityIndex", ColumnType.Integer), new("Level", ColumnType.Text)]),
        new() { ["Name"] = "Name", ["Category"] = "Category", ["EntityIndex"] = "EntityIndex", ["Level"] = "Level" });

    private static readonly IFlowNode Rooms = FailingSource("test.rooms",
        new Schema([new("Area", ColumnType.Number), new("EntityIndex", ColumnType.Integer), new("Room", ColumnType.Text), new("Name", ColumnType.Text)]),
        new() { ["EntityIndex"] = "EntityIndex" });

    private static IFlowNode Consumer(string param, SuggestSource suggest)
        => new DelegateNode(
            new("test.consumer", 1, NodeCapability.Pure,
                Inputs: [new PortSpec("a", PortType.Table), new PortSpec("b", PortType.Table, Optional: true)],
                Outputs: [new PortSpec("out", PortType.Table)],
                Params: [new ParamSpec(param, ParamKind.Text, Suggest: suggest)], "Passes its input through."),
            (_, inputs, _) => [inputs[0]]);

    private static SuggestionList Resolve(string param, SuggestSource suggest, bool wireRooms = false)
    {
        var registry = new NodeRegistry([Elements, Rooms, Consumer(param, suggest)]);
        var graph = Graph.Node("e", "test.elements").Node("r", "test.rooms").Node("c", "test.consumer")
            .Connect("e.out", "c.a");
        var snapshot = (wireRooms ? graph.Connect("r.out", "c.b") : graph).Build().Evaluate(registry);
        Assert.That(snapshot.Results["e"].Status, Is.Not.EqualTo(Ara3D.DataFlowEngine.NodeStatus.Ok), "the source must have no result");
        var schemas = GraphSchemas.Infer(snapshot.Document, registry, new StaticCatalog(new Dictionary<string, Schema>()))
            .Select(p => p.ToPortSchema()).ToList();
        return SuggestEndpoints.Resolve(snapshot, registry, "c", suggest, null, schemas: schemas);
    }

    private static string[] Names(SuggestionList list) => list.Values.Select(v => v.Value).ToArray();

    [Test]
    public void ColumnsComeFromTheInferredSchemaWithTypeKindAndMeaning()
    {
        var list = Resolve("column", SuggestSource.ColumnsOf("a"));
        Assert.That(list.Status, Is.EqualTo(SuggestStatus.Ok));
        Assert.That(Names(list), Is.EqualTo(new[] { "Volume", "Name", "Category", "EntityIndex", "Level" }));
        var category = list.Values.Single(v => v.Value == "Category");
        Assert.That(category.Detail, Is.EqualTo("Text"));
        Assert.That(category.Kind, Is.EqualTo("Category"));
        Assert.That(category.Role, Is.EqualTo("Category"));
        Assert.That(category.Meaning, Does.Contain("category"));
        Assert.That(list.Values.Single(v => v.Value == "Volume").Meaning, Is.Null, "a column with no kind has no meaning");
    }

    [Test]
    public void JoinKeysListSharedKeyColumnsFirst()
    {
        var list = Resolve("aKey", SuggestSource.ColumnsOf("a", ColumnUse.JoinKey, "b"), wireRooms: true);
        Assert.That(Names(list), Is.EqualTo(new[] { "EntityIndex", "Name", "Volume", "Category", "Level" }));
    }

    [Test]
    public void ValueColumnsAreOnlyNumeric()
        => Assert.That(Names(Resolve("v", SuggestSource.ColumnsOf("a", ColumnUse.Value))),
            Is.EqualTo(new[] { "Volume", "EntityIndex" }));

    [Test]
    public void LabelColumnsListLabelAndCategoryRolesFirst()
        => Assert.That(Names(Resolve("l", SuggestSource.ColumnsOf("a", ColumnUse.Label))).Take(3),
            Is.EquivalentTo(new[] { "Name", "Category", "Level" }));

    [Test]
    public void KeyColumnsComeFirst()
        => Assert.That(Names(Resolve("k", SuggestSource.ColumnsOf("a", ColumnUse.Key)))[0], Is.EqualTo("EntityIndex"));

    [Test]
    public void AggregateColumnsCarryTheFunctionsTheirTypeAllows()
    {
        var list = Resolve("g", SuggestSource.ColumnsOf("a", ColumnUse.Aggregate));
        Assert.That(list.Values[0].Value, Is.EqualTo("*"));
        Assert.That(list.Values[0].Functions, Is.EqualTo(new[] { "count" }));
        Assert.That(list.Values.Single(v => v.Value == "Volume").Functions, Is.EqualTo(new[] { "count", "sum", "min", "max", "avg" }));
        Assert.That(list.Values.Single(v => v.Value == "Name").Functions, Is.EqualTo(new[] { "count", "min", "max" }));
    }
}
