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
        var evaluated = Evaluate(node, inputs, ps);
        var inferred = Infer(node, inputs, ps);
        Assert.That(inferred.Ok, Is.True, inferred.ToString());
        AssertSameShape(inferred.Schema!, evaluated);
    }

    /// <summary>For a node that declares a Static schema: the declared columns equal the evaluated ones.</summary>
    public static void AssertDeclaredMatchesEvaluation(IFlowNode node, IReadOnlyList<IDataTable> inputs, params (string Name, string Value)[] ps)
    {
        var declared = ((StaticSchemaSpec)node.Spec.Schemas[0]).Schema;
        AssertSameShape(declared, Evaluate(node, inputs, ps));
    }

    private static IDataTable Evaluate(IFlowNode node, IReadOnlyList<IDataTable> inputs, (string Name, string Value)[] ps)
        => ((TableValue)node.Eval(NodeTestHelpers.Ctx, inputs.Select(FlowValue (t) => new TableValue(t)).ToList(),
            NodeTestHelpers.Params(ps))[0]).Table;

    /// <summary>Names, order and types must be equal. An evaluated table does not record nullability, so
    /// the check runs one way: a column the schema calls non-nullable must hold no null cell. (A column the
    /// schema calls nullable may or may not hold one.)</summary>
    private static void AssertSameShape(Schema schema, IDataTable evaluated)
    {
        Assert.That(Describe(schema), Is.EqualTo(Describe(TableSchemas.Of(evaluated))));
        for (var i = 0; i < schema.Columns.Count; i++)
            if (!schema.Columns[i].Nullable)
                for (var row = 0; row < evaluated.Rows.Count; row++)
                    Assert.That(evaluated[i, row] is null or DBNull, Is.False, $"column '{schema.Columns[i].Name}' is declared non-nullable but row {row} is null");
    }

    /// <summary>The inferred result for the inputs, with warnings: a missing column the run skips with a warning.</summary>
    public static IReadOnlyList<string> Warnings(ISchemaNode node, IReadOnlyList<IDataTable> inputs, params (string Name, string Value)[] ps)
    {
        var inferred = Infer(node, inputs, ps);
        Assert.That(inferred.Ok, Is.True, inferred.ToString());
        return inferred.Warnings.Select(w => w.Message).ToList();
    }

    public static void AssertFails(ISchemaNode node, IReadOnlyList<IDataTable> inputs, string contains, params (string Name, string Value)[] ps)
    {
        var inferred = Infer(node, inputs, ps);
        Assert.That(inferred.Ok, Is.False);
        Assert.That(inferred.ToString(), Does.Contain(contains));
    }

    private static string Describe(Schema schema)
        => string.Join(", ", schema.Columns.Select(c => $"{c.Name}:{c.Type}"));
}
