using BimOpenFlow.NodeDocs;

namespace BimOpenFlow.Layering.Tests;

public class NodeNotesTests
{
    /// <summary>The generic node reference may carry notes only for kinds the generic packs
    /// register; a note for a bos.*, bim.*, or view3d.* kind belongs with the toolkit studio's
    /// BimNodeNotes.</summary>
    [Test]
    public void GenericNodeNotesNameOnlyGenericKinds()
    {
        var kinds = NodeDocsProgram.GenericPacks.SelectMany(p => p.Nodes).Select(n => n.Spec.Kind).ToHashSet();
        var strays = NodeNotes.Generic.Keys.Where(k => !kinds.Contains(k)).ToList();
        Assert.That(strays, Is.Empty, "notes for kinds no generic pack registers: " + string.Join(", ", strays));
    }
}
