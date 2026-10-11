using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Host.Api;
using Contracts = BimOpenFlow.Contracts;

namespace BimOpenFlow.Host.Api.Tests;

[TestFixture]
public sealed class SchemaDescriptorTests
{
    private static NodeSpec Spec(params SchemaSpec[] schemas)
        => new("t.x", 1, NodeCapability.Pure, [], [new PortSpec("table", PortType.Table)], [], Schemas: schemas);

    [Test]
    public void ANodeWithoutSchemasSendsNoMember()
        => Assert.That(Spec().ToDescriptor().Schemas, Is.Null);

    [Test]
    public void AStaticSchemaSendsColumnsTypesNullabilityAndKinds()
    {
        var schema = new Schema([new Column("Id", ColumnType.Integer, false), new Column("Name", ColumnType.Date)]);
        var sent = Spec(SchemaSpec.Static("table", schema, new Dictionary<string, string> { ["Id"] = "EntityIndex" }))
            .ToDescriptor().Schemas!.Single();

        Assert.That(sent.Mode, Is.EqualTo(Contracts.SchemaMode.Static));
        Assert.That(sent.Columns!.Select(c => (c.Name, c.Type, c.Nullable, c.Kind)), Is.EqualTo(new[]
        {
            ("Id", Contracts.ColumnType.Integer, false, (string?)"EntityIndex"),
            ("Name", Contracts.ColumnType.Date, true, (string?)null),
        }));
    }

    [Test]
    public void InferredAndDynamicSchemasSendTheModeOnly()
    {
        var sent = Spec(SchemaSpec.Inferred("table")).ToDescriptor().Schemas!.Single();
        Assert.That((sent.Mode, sent.Columns), Is.EqualTo((Contracts.SchemaMode.Inferred, (IReadOnlyList<Contracts.SchemaColumn>?)null)));
        Assert.That(Spec(SchemaSpec.Dynamic("table")).ToDescriptor().Schemas!.Single().Mode, Is.EqualTo(Contracts.SchemaMode.Dynamic));
    }
}
