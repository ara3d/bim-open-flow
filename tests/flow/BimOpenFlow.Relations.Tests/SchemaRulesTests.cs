namespace BimOpenFlow.Relations.Tests;

/// <summary>The rules called directly, the way the table.* nodes call them: arguments in, schema out.</summary>
public class SchemaRulesTests
{
    static readonly Schema Walls = new([
        new("id", ColumnType.Integer, false), new("height", ColumnType.Number), new("name", ColumnType.Text)]);
    static readonly Schema Levels = new([new("id", ColumnType.Integer, false), new("name", ColumnType.Text), new("elevation", ColumnType.Integer)]);

    static string Names(SchemaResult r) => r.Ok ? string.Join(",", r.Schema!.Columns.Select(c => $"{c.Name}:{c.Type}")) : r.ToString();

    [Test] public void NearestNameIgnoresCaseAndGivesUpWhenFar()
    {
        Assert.That(SchemaRules.NearestName(["id", "height", "name"], "Heigth"), Is.EqualTo("height"));
        Assert.That(SchemaRules.NearestName(["id", "height", "name"], "volume"), Is.Null);
    }
    [Test] public void MissingColumnFailureOffersTheNearest() => Assert.That(Names(SchemaRules.Drop(Walls, ["nam"])), Is.EqualTo("No column named 'nam'; did you mean 'name'?"));
    [Test] public void SkippedMissingColumnsAreWarningsOnAnOkResult()
    {
        var dropped = SchemaRules.Drop(Walls, ["width", "HEIGHT"], skipMissing: true);
        Assert.That(Names(dropped), Is.EqualTo("id:Integer,name:Text"));
        Assert.That(dropped.Warnings.Select(w => w.Message), Is.EqualTo(new[] { "No column named 'width'." }));
        var renamed = SchemaRules.Rename(Walls, [new("nme", "label")], skipMissing: true);
        Assert.That(renamed.Ok, Is.True);
        Assert.That(renamed.Warnings, Has.Count.EqualTo(1));
        Assert.That(SchemaRules.Select(Walls, [new Projection("zzz", null)], skipMissing: true).Warnings, Has.Count.EqualTo(1));
    }
    [Test] public void RightOutputNamesRepeatTheSuffixUntilFree()
        => Assert.That(SchemaRules.RightOutputNames(["x", "x_b"], ["x", "y"], "_b"), Is.EqualTo(new[] { "x_b_b", "y" }));
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

    [TestCase(JoinKind.Inner, false, false)]
    [TestCase(JoinKind.Left, false, true)]
    [TestCase(JoinKind.Right, true, false)]
    [TestCase(JoinKind.Full, true, true)]
    public void AnOuterJoinMakesThePaddedSideNullable(JoinKind kind, bool leftNullable, bool rightNullable)
    {
        var columns = SchemaRules.Join(Walls, Levels, [new JoinKey("id", "id")], kind).Schema!;
        Assert.That(columns.Find("id")!.Value.Nullable, Is.EqualTo(leftNullable), "the left key");
        Assert.That(columns.Find("id_right")!.Value.Nullable, Is.EqualTo(rightNullable), "a right-side key kept by the default options");
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
