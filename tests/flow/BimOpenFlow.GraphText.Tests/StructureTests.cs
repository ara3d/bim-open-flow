using Ara3D.DataFlowEngine.TestKit;

namespace BimOpenFlow.GraphText.Tests;

/// <summary>The binding lines: one per node in evaluation order, inputs then params, strings
/// quoted, and the header with the analysis id and short graph hash.</summary>
[TestFixture]
public sealed class StructureTests
{
    private static readonly GraphDocument Sum = Graph
        .Node("b", "test.const", ("kind", "Integer"), ("value", "3"))
        .Node("a", "test.const", ("value", "2"), ("kind", "Integer"))
        .Node("neg", "test.negate")
        .Node("sum", "test.add")
        .Connect("sum.out", "neg.in")
        .Connect("b.out", "sum.b")
        .Connect("a.out", "sum.a")
        .Build();

    private static string Hash(GraphDocument doc)
        => doc.ComputeGraphHash()[..GraphText.ShortGraphHashLength];

    [Test]
    public void PrintsOneBindingPerNode_InEvaluationOrder_WithResultsBeneath()
        => Assert.That(Fixtures.Print(Sum, TestNodes.Registry, GraphTextOptions.Golden with { AnalysisId = "sum" }), Is.EqualTo(
            $"""
            dfg 0.1.0;
            // sum   graph {Hash(Sum)}
            a = test.const@1(kind: "Integer", value: "2");
              // Ok  Integer 2
            b = test.const@1(kind: "Integer", value: "3");
              // Ok  Integer 3
            sum = test.add@1(a: a.out, b: b.out);
              // Ok  Integer 5
            neg = test.negate@1(in: sum.out);
              // Ok  Integer -5

            """.ReplaceLineEndings("\n")));

    [Test]
    public void WithoutASnapshot_PrintsStructureOnly()
        => Assert.That(GraphText.Print(Sum, null, TestNodes.Registry), Does.Not.Contain("//   ").And.Not.Contain("Ok"));

    [Test]
    public void IsDeterministic_AndIndependentOfNodeAndEdgeInsertionOrder()
    {
        var reordered = Graph
            .Node("sum", "test.add")
            .Node("neg", "test.negate")
            .Node("a", "test.const", ("kind", "Integer"), ("value", "2"))
            .Node("b", "test.const", ("value", "3"), ("kind", "Integer"))
            .Connect("a.out", "sum.a")
            .Connect("b.out", "sum.b")
            .Connect("sum.out", "neg.in")
            .Build();
        var first = Fixtures.Print(Sum, TestNodes.Registry);
        Assert.Multiple(() =>
        {
            Assert.That(Fixtures.Print(Sum, TestNodes.Registry), Is.EqualTo(first));
            Assert.That(Fixtures.Print(reordered, TestNodes.Registry), Is.EqualTo(first));
        });
    }

    [Test]
    public void EscapesOneLineStrings_AndPrintsMultiLineStringsBetweenTripleQuotes()
    {
        var doc = Graph
            .Node("q", "test.const", ("kind", "Text"), ("value", "say \"hi\"\tC:\\x"))
            .Node("sql", "test.const", ("kind", "Text"), ("value", "SELECT 1\nFROM t"))
            .Build();
        var text = GraphText.Print(doc, null, TestNodes.Registry);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("q = test.const@1(kind: \"Text\", value: \"say \\\"hi\\\"\\tC:\\\\x\");"));
            Assert.That(text, Does.Contain("sql = test.const@1(kind: \"Text\", value: \"\"\"\nSELECT 1\nFROM t\n\"\"\");"));
        });
    }

    [Test]
    public void OmitsUnconnectedOptionalInputs_AndKeepsInputsTheCatalogDoesNotKnow()
    {
        var optional = new DelegateNode(
            new("fixture.optional", 1, NodeCapability.Pure,
                [new("main", PortType.Any), new("extra", PortType.Any, Optional: true)], [new("out", PortType.Any)], []),
            (_, i, _) => [i[0]]);
        var doc = Graph
            .Node("c", "test.const", ("value", "1"))
            .Node("o", "fixture.optional")
            .Connect("c.out", "o.main")
            .Build();
        var line = Bindings.Line(doc, doc.FindNode("o")!, optional.Spec);
        Assert.That(line, Is.EqualTo("o = fixture.optional@1(main: c.out);"));
    }

    [Test]
    public void ACyclicDocument_PrintsInIdOrder()
    {
        var cycle = Graph
            .Node("y", "test.negate")
            .Node("x", "test.negate")
            .Connect("x.out", "y.in")
            .Connect("y.out", "x.in")
            .Build();
        Assert.That(Bindings.Order(cycle).Select(n => n.Id), Is.EqualTo(new[] { "x", "y" }));
    }
}
