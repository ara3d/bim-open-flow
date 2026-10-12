using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.TestKit;
using Ara3D.DataTable;
using BimOpenFlow.Relations;

namespace BimOpenFlow.Nodes.Tests;

/// <summary>Ties a node's shape rule to its evaluation: the schema inferred from the inputs' own schemas
/// must name the same columns, in the same order and with the same types, as the table the node builds.
/// Compiled into the Tables and TableOps test projects.</summary>
internal static class SchemaGolden
{
    public static SchemaResult Infer(ISchemaNode node, IReadOnlyList<Schema> inputs, params (string Name, string Value)[] ps)
        => node.Infer(node.Spec.Schemas[0].Output, inputs.Select(s => new SchemaInput(PortType.Table, true, SchemaResult.Of(s))).ToList(),
            NodeTestHelpers.Params(ps), new StaticCatalog(new Dictionary<string, Schema>()));

    public static SchemaResult Infer(ISchemaNode node, IReadOnlyList<IDataTable> inputs, params (string Name, string Value)[] ps)
        => Infer(node, inputs.Select(TableSchemas.Of).ToList(), ps);

    public static void AssertMatchesEvaluation(ISchemaNode node, IReadOnlyList<IDataTable> inputs, params (string Name, string Value)[] ps)
    {
        var evaluated = ((TableValue)node.Eval(NodeTestHelpers.Ctx, inputs.Select(FlowValue (t) => new TableValue(t)).ToList(),
            NodeTestHelpers.Params(ps))[0]).Table;
        var inferred = Infer(node, inputs, ps);
        Assert.That(inferred.Ok, Is.True, inferred.ToString());
        Assert.That(Describe(inferred.Schema!), Is.EqualTo(Describe(TableSchemas.Of(evaluated))));
    }

    public static void AssertFails(ISchemaNode node, IReadOnlyList<IDataTable> inputs, string contains, params (string Name, string Value)[] ps)
    {
        var inferred = Infer(node, inputs, ps);
        Assert.That(inferred.Ok, Is.False);
        Assert.That(inferred.ToString(), Does.Contain(contains));
    }

    /// <summary>Names, order and types; nullability is not recorded by an evaluated table.</summary>
    private static string Describe(Schema schema)
        => string.Join(", ", schema.Columns.Select(c => $"{c.Name}:{c.Type}"));
}
