namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>table.calendar: inclusive end, week and month steps across year
/// ends, calendar arithmetic, and errors.</summary>
[TestFixture]
public sealed class TableCalendarNodeTests
{
    private static IReadOnlyList<object?> Dates(params (string Name, string Value)[] ps)
    {
        var table = new TableCalendarNode().EvalTable([], ps);
        return Enumerable.Range(0, table.Rows.Count).Select(i => table[0, i]).ToList();
    }

    [Test]
    public void Calendar_Days_InclusiveOfEnd()
        => Assert.That(Dates(("start", "2024-01-01"), ("end", "2024-01-03")),
            Is.EqualTo(new object?[] { "2024-01-01", "2024-01-02", "2024-01-03" }));

    [Test]
    public void Calendar_DefaultColumnNameIsDate_AndTypeIsText()
    {
        var table = new TableCalendarNode().EvalTable([], ("start", "2024-01-01"), ("end", "2024-01-01"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "date" }));
        Assert.That(table.Columns[0].Descriptor.Type, Is.EqualTo(typeof(string)));
        Assert.That(table.Rows, Has.Count.EqualTo(1));
    }

    [Test]
    public void Calendar_Weeks_CrossYearEnd()
        => Assert.That(Dates(("start", "2023-12-25"), ("end", "2024-01-08"), ("step", "week")),
            Is.EqualTo(new object?[] { "2023-12-25", "2024-01-01", "2024-01-08" }));

    [Test]
    public void Calendar_Months_CrossYearEnd_LeapDayClamped()
        => Assert.That(Dates(("start", "2023-11-30"), ("end", "2024-02-29"), ("step", "month")),
            Is.EqualTo(new object?[] { "2023-11-30", "2023-12-30", "2024-01-30", "2024-02-29" }));

    [Test]
    public void Calendar_Quarters_AndYears()
    {
        Assert.That(Dates(("start", "2023-01-15"), ("end", "2024-01-15"), ("step", "quarter")),
            Is.EqualTo(new object?[] { "2023-01-15", "2023-04-15", "2023-07-15", "2023-10-15", "2024-01-15" }));
        Assert.That(Dates(("start", "2022-06-01"), ("end", "2024-06-01"), ("step", "year")),
            Is.EqualTo(new object?[] { "2022-06-01", "2023-06-01", "2024-06-01" }));
    }

    [Test]
    public void Calendar_AcceptsDateTimeForm_OutputsDatesOnly()
        => Assert.That(Dates(("start", "2024-01-01T08:30:00"), ("end", "2024-01-02T09:00:00")),
            Is.EqualTo(new object?[] { "2024-01-01", "2024-01-02" }));

    [Test]
    public void Calendar_BadInputs_Throw()
    {
        Assert.That(() => new TableCalendarNode().EvalTable([], ("start", "2024-02-01"), ("end", "2024-01-01")),
            Throws.ArgumentException.With.Message.StartsWith("table.calendar: ").And.Message.Contains("before"));
        Assert.That(() => new TableCalendarNode().EvalTable([], ("end", "2024-01-01")),
            Throws.ArgumentException.With.Message.Contains("start"));
        Assert.That(() => new TableCalendarNode().EvalTable([], ("start", "01/02/2024"), ("end", "2024-03-01")),
            Throws.ArgumentException.With.Message.Contains("ISO-8601"));
        Assert.That(() => new TableCalendarNode().EvalTable([],
                ("start", "2024-01-01"), ("end", "2024-02-01"), ("step", "fortnight")),
            Throws.ArgumentException.With.Message.Contains("step"));
    }
}
