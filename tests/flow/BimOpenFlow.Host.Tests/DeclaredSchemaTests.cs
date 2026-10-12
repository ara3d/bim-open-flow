using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Host.Api;

namespace BimOpenFlow.Host.Tests;

public sealed class DeclaredSchemaTests
{
    [Test]
    public void EveryDeclarationInTheGenericPacksIsValid()
    {
        var nodes = HostComposition.Generic.All.SelectMany(p => p.Registry().Nodes).ToList();
        Assert.That(nodes.SelectMany(n => n.SchemaProblems()), Is.Empty);
    }

    [Test]
    public void TheProvingNodesDeclareTheirSchemas()
    {
        var catalog = HostComposition.Generic.All.SelectMany(p => p.Registry().ToCatalog().Nodes).ToDictionary(n => n.Kind);
        Assert.That(catalog["table.range"].Schemas!.Single().Mode, Is.EqualTo(Contracts.SchemaMode.Inferred), "its column is named by a parameter");
        Assert.That(catalog["table.pivot"].Schemas!.Single().Mode, Is.EqualTo(Contracts.SchemaMode.Dynamic));
        Assert.That(catalog["table.transpose"].Schemas!.Single().Mode, Is.EqualTo(Contracts.SchemaMode.Dynamic));
        Assert.That(catalog["chart.bar"].Schemas, Is.Null, "undeclared nodes send no member");
    }
}
