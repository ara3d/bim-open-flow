using Ara3D.BimOpenSchema;

namespace BimOpenFlow.Host.Catalog.Tests;

/// <summary>ModelEntityIndex over a model whose indices are -1 (absent, as the specification
/// defines it): absent text stays null, an absent value leaves its parameter out, and an
/// entity without a LocalId is never found by id.</summary>
[TestFixture]
public sealed class EntityIndexAbsenceTests
{
    private const EntityIndex None = BimDataBuilder.InvalidEntityIndex;

    private static ModelEntityIndex Build(Action<BimDataBuilder, DocumentIndex> add)
    {
        var b = new BimDataBuilder();
        add(b, b.AddDocument(null, null));
        return ModelEntityIndex.Build(b.Build());
    }

    [Test]
    public void Find_NeverReturnsAnEntityWithoutALocalId()
    {
        var index = Build((b, doc) =>
        {
            b.AddEntity(-1, null, doc, "IFCWALL", None, None);
            b.AddEntity(-1, null, doc, "IFCDOOR", None, None);
        });
        Assert.That(index.Find(-1), Is.Null);
        Assert.That(index.All.Select(e => e.Name), Is.EqualTo(new[] { "IFCWALL", "IFCDOOR" }));
        Assert.That(index.Count, Is.EqualTo(2));
    }

    [Test]
    public void AbsentOrLegacyEmptyIdentity_IsNull()
    {
        var index = Build((b, doc) =>
        {
            var category = b.AddEntity(-1, null, doc, null, None, None);
            b.AddEntity(1, null, doc, null, category, None);
            b.AddEntity(2, "", doc, "", None, None);
        });
        foreach (var localId in new long[] { 1, 2 })
        {
            var entity = index.Find(localId)!;
            Assert.That((entity.GlobalId, entity.Name, entity.Category), Is.EqualTo(((string?)null, (string?)null, (string?)null)));
        }
    }

    [Test]
    public void Parameters_KeepAbsentTextNullAndLeaveOutAbsentValues()
    {
        var index = Build((b, doc) =>
        {
            var unnamed = b.AddEntity(9, null, doc, null, None, None);
            var e = b.AddEntity(1, null, doc, "Wall", None, None);
            var text = b.AddDescriptor("Mark", null, null, ParameterType.String);
            b.AddParameter(e, "", text);                                          // "" is a value
            b.AddParameter(e, -1, b.AddDescriptor("Note", null, null, ParameterType.String));   // absent
            b.AddParameter(e, -1, b.AddDescriptor("Height", "m", "Dims", ParameterType.Number)); // absent
            b.AddParameter(e, -1, b.AddDescriptor("Origin", null, "Dims", ParameterType.Point)); // absent
            b.AddParameter(e, None, b.AddDescriptor("Host", null, "Rel", ParameterType.Entity)); // absent
            b.AddParameter(e, unnamed, b.AddDescriptor("Level", null, "Rel", ParameterType.Entity));
            b.AddParameter(e, -1, b.AddDescriptor("Count", null, "Dims", ParameterType.Int));   // -1 is a value
        });
        var parameters = index.Find(1)!.Parameters.Select(p => (p.Group, p.Name, p.Value, p.Units));
        Assert.That(parameters, Is.EqualTo(new (string?, string?, string?, string?)[]
        {
            (null, "Mark", "", null),
            ("Dims", "Count", "-1", null),
            ("Rel", "Level", null, null),
        }));
    }
}
