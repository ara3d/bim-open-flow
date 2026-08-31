using BimOpenFlow.Contracts;

namespace BimOpenFlow.Publishing.Tests;

public class TableJsonTests
{
    [Test]
    public void ToTableData_MapsColumnsAndRows()
    {
        var data = TestTable.Sample().ToTableData();
        Assert.That(data.Columns.Select(c => (c.Name, c.Type)), Is.EqualTo(new[]
        {
            ("name", ColumnType.Text),
            ("count", ColumnType.Integer),
            ("area", ColumnType.Number),
            ("ok", ColumnType.Boolean),
        }));
        Assert.That(data.Rows, Has.Count.EqualTo(3));
        Assert.That(data.Rows[0][1], Is.EqualTo(3L));
    }

    [Test]
    public void ToJson_MatchesGolden()
    {
        var table = new TestTable("t",
            new TestColumn("label", typeof(string), new object?[] { "a\"b", null }, 0),
            new TestColumn("value", typeof(double), new object?[] { 1.5, double.NaN }, 1));
        Assert.That(table.ToJson(), Is.EqualTo(
            "{\"columns\":[{\"name\":\"label\",\"type\":\"Text\"},{\"name\":\"value\",\"type\":\"Number\"}]," +
            "\"rows\":[[\"a\\u0022b\",1.5],[null,\"NaN\"]]}"));
    }

    [Test]
    public void ToJson_IsDeterministic()
    {
        var first = TestTable.Sample().ToJson();
        var second = TestTable.Sample().ToJson();
        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void ToColumnType_UnwrapsNullableAndRejectsUnknown()
    {
        Assert.That(TableJson.ToColumnType(typeof(int?)), Is.EqualTo(ColumnType.Integer));
        Assert.That(TableJson.ToColumnType(typeof(float)), Is.EqualTo(ColumnType.Number));
        Assert.That(TableJson.ToColumnType(typeof(char)), Is.EqualTo(ColumnType.Text));
        Assert.Throws<ArgumentException>(() => TableJson.ToColumnType(typeof(DateTime)));
    }
}
