using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>The path of each xlsx and sqlite reader can be wired from a Text value, so one
/// value.path can feed several readers.</summary>
[TestFixture]
public sealed class ReaderPathBindingTests
{
    [Test]
    public void PathParameters_AcceptAWiredTextValue()
        => Assert.That(
            new IFlowNode[]
                {
                    new XlsxReadNode(), new XlsxSheetsNode(),
                    new SqliteQueryNode(), new SqliteTableNode(), new SqliteTablesNode(),
                }
                .Select(node => node.Spec.Params.Single(p => p.Name == "path").Bindable),
            Has.All.EqualTo(PortType.Text));
}
