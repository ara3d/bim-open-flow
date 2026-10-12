namespace BimOpenFlow.Relations.Tests;

/// <summary>The rules called directly, the way the table.* nodes call them: arguments in, schema out.</summary>
public class SchemaRulesTests
{
    static readonly Schema Walls = new([
        new("id", ColumnType.Integer, false), new("height", ColumnType.Number), new("name", ColumnType.Text)]);
    static readonly Schema Levels = new([new("id", ColumnType.Integer, false), new("name", ColumnType.Text), new("elevation", ColumnType.Integer)]);

    static string Names(SchemaResult r) => r.Ok ? string.Join(",", r.Schema!.Columns.Select(c => $"{c.Name}:{c.Type}")) : r.ToString();

    [Test] public void DropKeepsTheRest() => Assert.That(Names(SchemaRules.Drop(Walls, ["HEIGHT"])), Is.EqualTo("id:Integer,name:Text"));
    [Test] public void DropUnknown() => Assert.That(Names(SchemaRules.Drop(Walls, ["width"])), Is.EqualTo("No column named 'width'."));
    [Test] public void DropEverything() => Assert.That(SchemaRules.Drop(Walls, ["id", "height", "name"]).Ok, Is.False);
    [Test] public void RenameTwice() => Assert.That(Names(SchemaRules.Rename(Walls, [new("id", "a"), new("ID", "b")])), Is.EqualTo("Column 'ID' is renamed more than once."));
    [Test] public void CastAsNewColumn() => Assert.That(Names(SchemaRules.Cast(Walls, "id", ColumnType.Text, "idText")), Is.EqualTo("id:Integer,height:Number,name:Text,idText:Text"));
    [Test] public void CastAsNewColumnClash() => Assert.That(Names(SchemaRules.Cast(Walls, "id", ColumnType.Text, "NAME")), Is.EqualTo("Column 'NAME' already exists."));

    [Test]
    public void JoinWithTheNodeOptionsDropsTheRightKeyAndSuffixesCollisions()
    {
        var options = new JoinOptions("_b", DropRightKeys: true, RequireComparableKeys: false);
        var result = SchemaRules.Join(Walls, Levels, [new JoinKey("id", "id")], JoinKind.Left, options);
        Assert.That(Names(result), Is.EqualTo("id:Integer,height:Number,name:Text,name_b:Text,elevation:Integer"));
    }

    [Test]
    public void JoinDefaultsKeepTheRelationLayersNaming()
    {
        var result = SchemaRules.Join(Walls, Levels, [new JoinKey("id", "id")], JoinKind.Inner);
        Assert.That(Names(result), Is.EqualTo("id:Integer,height:Number,name:Text,id_right:Integer,name_right:Text,elevation:Integer"));
    }

    [Test]
    public void JoinKeysOfDifferentTypesFailOnlyWhenRequired()
    {
        var keys = new[] { new JoinKey("name", "id") };
        Assert.That(SchemaRules.Join(Walls, Levels, keys, JoinKind.Left).Ok, Is.False);
        Assert.That(SchemaRules.Join(Walls, Levels, keys, JoinKind.Left, new JoinOptions(RequireComparableKeys: false)).Ok, Is.True);
    }

    [Test]
    public void UnionWidensIntegerAndNumber()
    {
        var a = new Schema([new("x", ColumnType.Integer, false)]);
        var b = new Schema([new("y", ColumnType.Number, false)]);
        Assert.That(Names(SchemaRules.Union([a, b])), Is.EqualTo("x:Number"));
    }

    [Test]
    public void UnionByNameAppendsTheColumnsOnlyBHas()
    {
        var b = new Schema([new("elevation", ColumnType.Integer), new("ID", ColumnType.Number)]);
        Assert.That(Names(SchemaRules.UnionByName(Walls, b)), Is.EqualTo("id:Number,height:Number,name:Text,elevation:Integer"));
    }

    [Test]
    public void UnionByNameRejectsIncompatibleTypes()
        => Assert.That(SchemaRules.UnionByName(Walls, new Schema([new("name", ColumnType.Integer)])).Ok, Is.False);

    [Test]
    public void KeyedUnionNeedsTheSameColumns()
    {
        Assert.That(Names(SchemaRules.KeyedSetOp(Walls, Levels, "id", requireSameColumns: true)), Does.StartWith("union requires b to share a's columns"));
        Assert.That(Names(SchemaRules.KeyedSetOp(Walls, Levels, "id", requireSameColumns: false)), Is.EqualTo("id:Integer,height:Number,name:Text"));
        Assert.That(Names(SchemaRules.KeyedSetOp(Walls, Levels, "height", requireSameColumns: false)), Is.EqualTo("No column named 'height'."));
    }
}
