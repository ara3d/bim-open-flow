using Ara3D.BimOpenSchema;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Nodes.Compliance;
using BimOpenFlow.Nodes.Support;
using static BimOpenFlow.Nodes.Compliance.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Compliance.Tests;

/// <summary>The columns the check nodes declare (TKT-208) and the helper that builds them from registry kinds.</summary>
[TestFixture]
public sealed class DeclaredSchemaTests
{
    private static readonly StaticCatalog Catalog = new(new Dictionary<string, Schema>());

    private static IDataTable Doors()
        => Table("doors",
            ("width", typeof(double), new object?[] { 0.9, null, 0.8 }),
            ("storey", typeof(string), new object?[] { "L1", "L2", null }));

    private static Schema Described(IDataTable table)
        => new([.. table.Columns.Select(c => new Column(c.Descriptor.Name,
            c.Descriptor.Type == typeof(double) ? ColumnType.Number : ColumnType.Text))]);

    [Test]
    public void CheckRequired_InfersTheInputColumnsThenTheVerdictColumns()
    {
        var node = new CheckRequiredNode();
        var parameters = Params(("checkId", "R"), ("title", "T"), ("citation", "C"), ("columns", "width"));
        var inferred = node.Infer("out", [new SchemaInput(PortType.Table, true, SchemaResult.Of(Described(Doors())))], parameters, Catalog);
        var evaluated = node.EvalTable(parameters, Doors());
        Assert.Multiple(() =>
        {
            Assert.That(inferred.Ok, Is.True, inferred.ToString());
            Assert.That(inferred.Schema!.Columns.Select(c => c.Name), Is.EqualTo(evaluated.ColumnNames()));
            Assert.That(inferred.Schema!.Columns.Skip(2), Is.EqualTo(VerdictSchema.Declared.Select(c => c.ToColumn())));
            Assert.That(node.SchemaProblems(), Is.Empty);
        });
    }

    [Test]
    public void CheckRequired_RejectsAnInputThatAlreadyHoldsVerdicts()
    {
        var verdicts = new Schema([.. VerdictSchema.Declared.Select(c => c.ToColumn())]);
        var inferred = new CheckRequiredNode().Infer("out", [new SchemaInput(PortType.Table, true, SchemaResult.Of(verdicts))],
            Params(), Catalog);
        Assert.That(inferred.Ok, Is.False);
        Assert.That(inferred.ToString(), Does.Contain("Verdict"));
    }

    [Test]
    public void CheckRollup_DeclaresTheColumnsItBuilds()
    {
        var node = new CheckRollupNode();
        var evaluated = node.EvalTable(Params(), VerdictTable("C1", "Title", "Cite", Verdict.Pass, Verdict.Fail));
        var declared = ((StaticSchemaSpec)node.Spec.Schemas.Single()).Schema;
        Assert.Multiple(() =>
        {
            Assert.That(declared.Columns.Select(c => c.Name), Is.EqualTo(evaluated.ColumnNames()));
            Assert.That(declared.Columns.Select(c => c.Type), Is.EqualTo(new[]
            {
                ColumnType.Text, ColumnType.Text, ColumnType.Text,
                ColumnType.Integer, ColumnType.Integer, ColumnType.Integer, ColumnType.Integer, ColumnType.Text,
            }));
            Assert.That(node.SchemaProblems(), Is.Empty);
        });
    }

    [TestCase(ColumnScalarType.Boolean, ColumnType.Boolean)]
    [TestCase(ColumnScalarType.Integer, ColumnType.Integer)]
    [TestCase(ColumnScalarType.Number, ColumnType.Number)]
    [TestCase(ColumnScalarType.Text, ColumnType.Text)]
    [TestCase(ColumnScalarType.Date, ColumnType.Date)]
    [TestCase(ColumnScalarType.Timestamp, ColumnType.Timestamp)]
    public void KindSchemas_MapsEveryScalarType(ColumnScalarType scalar, ColumnType expected)
        => Assert.That(scalar.ToColumnType(), Is.EqualTo(expected));

    [Test]
    public void KindSchemas_Static_CarriesTheKindNamesOfKindColumnsOnly()
    {
        var spec = (StaticSchemaSpec)KindSchemas.Static("out", ColumnKinds.Level, ColumnKinds.Elevation.NotNull(),
            KindColumn.Plain("count", ColumnType.Integer, nullable: false));
        Assert.Multiple(() =>
        {
            Assert.That(spec.Schema.Columns, Is.EqualTo(new[]
            {
                new Column("Level", ColumnType.Text, true),
                new Column("Elevation", ColumnType.Number, false),
                new Column("count", ColumnType.Integer, false),
            }));
            Assert.That(spec.Kinds, Is.EqualTo(new Dictionary<string, string> { ["Level"] = "Level", ["Elevation"] = "Elevation" }));
        });
    }
}
