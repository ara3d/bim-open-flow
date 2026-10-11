using Ara3D.DataFlowEngine.TestKit;

namespace BimOpenFlow.GraphText.Tests;

/// <summary>The inputs header line: before the nodes, defaults always, the value in use when it differs.</summary>
[TestFixture]
public sealed class GraphInputsTextTests
{
    private static readonly GraphDocument Doc = Graph
        .Node("c", "test.const", ("kind", "Integer"), ("value", "$n"))
        .Build()
        .SetInput(new GraphInput("n", ParamKind.Integer, "5", "A number"))
        .SetInput(new GraphInput("a", ParamKind.Text, "x\"y"));

    [Test]
    public void PrintsInputsSortedByName_AfterTheHeader_BeforeTheNodes()
    {
        var lines = Fixtures.Print(Doc, TestNodes.Registry).Split('\n');
        Assert.That(lines[0], Is.EqualTo("dfg 0.1.0;"));
        Assert.That(lines[1], Does.StartWith("// graph "));
        Assert.That(lines[2], Is.EqualTo("inputs a: Text = \"x\\\"y\", n: Integer = \"5\";"));
        Assert.That(lines[3], Is.EqualTo("c = test.const@1(kind: \"Integer\", value: \"$n\");"));
    }

    [Test]
    public void ShowsTheValueInUseWhenItIsNotTheDefault()
    {
        var snapshot = Doc.Evaluate(TestNodes.Registry, new Dictionary<string, string> { ["n"] = "8" });
        var text = GraphText.Print(Doc, snapshot, TestNodes.Registry);
        Assert.That(text, Does.Contain("n: Integer = \"5\" now \"8\";"));
        Assert.That(text, Does.Not.Contain("a: Text = \"x\\\"y\" now"));
    }

    [Test]
    public void NoInputsMeansNoLine()
        => Assert.That(Fixtures.Print(Graph.Node("c", "test.const", ("kind", "Integer"), ("value", "1")).Build(),
            TestNodes.Registry), Does.Not.Contain("inputs"));
}
