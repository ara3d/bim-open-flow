using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Value.Tests;

[TestFixture]
public class ValueNodeTests
{
    private static FlowValue Run(IFlowNode node, params (string Name, string Value)[] ps)
        => node.Eval(NodeTestHelpers.Ctx, [], NodeTestHelpers.Params(ps)).Single();

    [Test]
    public void Text_Emits_Its_Parameter()
        => Assert.That(Run(new TextNode(), ("value", "Level 1")), Is.EqualTo(new TextValue("Level 1")));

    [Test]
    public void Text_Defaults_To_Empty()
        => Assert.That(Run(new TextNode()), Is.EqualTo(new TextValue("")));

    [Test]
    public void Number_Parses_With_The_Invariant_Culture()
        => Assert.That(Run(new NumberNode(), ("value", "2.5")), Is.EqualTo(new NumberValue(2.5)));

    [Test]
    public void Number_Defaults_To_Zero()
        => Assert.That(Run(new NumberNode()), Is.EqualTo(new NumberValue(0)));

    [Test]
    public void Number_Rejects_Text()
        => Assert.Throws<FormatException>(() => Run(new NumberNode(), ("value", "abc")));

    [Test]
    public void Integer_Emits_A_Signed_Value()
        => Assert.That(Run(new IntegerNode(), ("value", "-7")), Is.EqualTo(new IntegerValue(-7)));

    [Test]
    public void Integer_Rejects_A_Fraction()
        => Assert.Throws<FormatException>(() => Run(new IntegerNode(), ("value", "1.5")));

    [Test]
    public void Boolean_Emits_True_And_Defaults_To_False()
    {
        Assert.That(Run(new BooleanNode(), ("value", "true")), Is.EqualTo(new BooleanValue(true)));
        Assert.That(Run(new BooleanNode()), Is.EqualTo(new BooleanValue(false)));
    }

    [Test]
    public void Path_Emits_The_Path_As_Written()
        => Assert.That(Run(new PathNode(), ("path", "models/a.duckdb")), Is.EqualTo(new TextValue("models/a.duckdb")));

    [Test]
    public void Path_Offers_Files_Under_The_Model_Roots_Of_Any_Extension()
    {
        var spec = new PathNode().Spec.Params.Single();
        Assert.That(spec.Kind, Is.EqualTo(ParamKind.FilePath));
        Assert.That(spec.Suggest, Is.EqualTo(SuggestSource.FilesWith()));
    }

    [Test]
    public void Every_Node_Is_Pure_Has_No_Inputs_And_Matches_Its_Output_To_Its_Parameter()
    {
        Assert.That(ValueNodes.All, Has.Count.EqualTo(5));
        foreach (var node in ValueNodes.All)
        {
            Assert.That(node.Spec.Capability, Is.EqualTo(NodeCapability.Pure), node.Spec.Kind);
            Assert.That(node.Spec.Inputs, Is.Empty, node.Spec.Kind);
            Assert.That(node.Spec.Outputs, Has.Count.EqualTo(1), node.Spec.Kind);
            Assert.That(node.Spec.Params.Single().Name, Is.EqualTo(node.Spec.Outputs[0].Name), node.Spec.Kind);
            Assert.That(node.Spec.Description, Does.Contain("share"), node.Spec.Kind);
        }
    }
}
