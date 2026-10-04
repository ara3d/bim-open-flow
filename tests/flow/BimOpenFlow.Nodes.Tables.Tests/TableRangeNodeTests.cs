namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>table.range: inclusive-stop generate_series semantics, negative
/// steps, fractional steps, and errors.</summary>
[TestFixture]
public sealed class TableRangeNodeTests
{
    private static IReadOnlyList<object?> Values(params (string Name, string Value)[] ps)
    {
        var table = new TableRangeNode().EvalTable([], ps);
        return Enumerable.Range(0, table.Rows.Count).Select(i => table[0, i]).ToList();
    }

    [Test]
    public void Range_InclusiveWhenStepLandsOnStop()
        => Assert.That(Values(("start", "1"), ("stop", "10"), ("step", "3")),
            Is.EqualTo(new object?[] { 1.0, 4.0, 7.0, 10.0 }));

    [Test]
    public void Range_ExclusiveWhenStepOvershootsStop()
        => Assert.That(Values(("start", "0"), ("stop", "10"), ("step", "4")),
            Is.EqualTo(new object?[] { 0.0, 4.0, 8.0 }));

    [Test]
    public void Range_DefaultsStartZeroStepOne_AndNamesColumnValue()
    {
        var table = new TableRangeNode().EvalTable([], ("stop", "2"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "value" }));
        Assert.That(table.Rows, Has.Count.EqualTo(3));
        Assert.That(table.Cell("value", 2), Is.EqualTo(2.0));
    }

    [Test]
    public void Range_FractionalStep_StillIncludesStop()
    {
        var values = Values(("start", "0"), ("stop", "1"), ("step", "0.1"));
        Assert.That(values, Has.Count.EqualTo(11));
        Assert.That((double)values[10]!, Is.EqualTo(1.0).Within(1e-12));
    }

    [Test]
    public void Range_NegativeStep_CountsDown()
        => Assert.That(Values(("start", "5"), ("stop", "1"), ("step", "-2")),
            Is.EqualTo(new object?[] { 5.0, 3.0, 1.0 }));

    [Test]
    public void Range_EmptyWhenStartPastStop()
        => Assert.That(Values(("start", "3"), ("stop", "1")), Is.Empty);

    [Test]
    public void Range_ZeroStepOrMissingStop_Throw()
    {
        Assert.That(() => new TableRangeNode().EvalTable([], ("stop", "5"), ("step", "0")),
            Throws.ArgumentException.With.Message.StartsWith("table.range: ").And.Message.Contains("step"));
        Assert.That(() => new TableRangeNode().EvalTable([]),
            Throws.ArgumentException.With.Message.StartsWith("table.range: ").And.Message.Contains("stop"));
        Assert.That(() => new TableRangeNode().EvalTable([], ("stop", "abc")),
            Throws.ArgumentException.With.Message.Contains("number"));
    }
}
