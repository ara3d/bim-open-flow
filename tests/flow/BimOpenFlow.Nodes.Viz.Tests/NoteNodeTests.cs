using Ara3D.DataFlowEngine.TestKit;

namespace BimOpenFlow.Nodes.Viz.Tests;

[TestFixture]
public class NoteNodeTests
{
    [Test]
    public void Eval_Returns_No_Outputs()
    {
        var outputs = new NoteNode().Eval(NodeTestHelpers.Ctx, [],
            NodeTestHelpers.Params(("text", "Remember to check the Snowdon storey mapping.")));

        Assert.That(outputs, Is.Empty);
    }

    [Test]
    public void Spec_Has_No_Ports()
    {
        var spec = new NoteNode().Spec;

        Assert.That(spec.Inputs, Is.Empty);
        Assert.That(spec.Outputs, Is.Empty);
    }

    [Test]
    public void Node_Appears_In_Viz_Registry()
        => Assert.That(VizNodes.All.Any(n => n.Spec.Kind == NoteNode.Kind), Is.True);
}
