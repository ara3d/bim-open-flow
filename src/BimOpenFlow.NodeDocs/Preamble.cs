namespace BimOpenFlow.NodeDocs;

/// <summary>The fixed hand-written text that opens docs/nodes.md.</summary>
public static class Preamble
{
    public const string Text = """
        # BimOpenFlow node reference

        > GENERATED FILE — do not edit by hand. This file is produced by the
        > `BimOpenFlow.NodeDocs` tool from the node packs' `NodeSpec` declarations.
        > To change it, change the specs or the generator and regenerate.

        A node is a pure function over flow values. Values travel along graph edges
        as one of five kinds — Boolean, Integer, Number, Text, or Table — and tables
        are the currency: almost everything useful is an immutable table flowing
        from node to node. A node reads its input values and parameters, and
        returns its output values. It holds no state between evaluations.

        **Pure vs Effect.** Pure nodes may be evaluated freely and their results
        memoized; evaluating one twice with the same inputs gives the same answer
        and touches nothing outside the graph. Effect nodes (file writers, report
        generators) execute only inside an explicit Run — the engine refuses to
        evaluate them otherwise — so re-evaluation for display can never write to
        disk behind your back.

        **Required vs optional inputs.** A node is normally not ready to evaluate
        until every input port is connected. A port marked optional is the
        exception: left unconnected, it does not block evaluation — the node
        receives a placeholder in that position and treats the input as absent.
        The placeholder never flows along an edge and is never an output.

        **Parameters.** Parameters are configuration, not wires: every value is
        stored in canonical string form in the graph document, and each parameter
        declares a kind (Boolean, Integer, Number, Text, Enum, FilePath, ModelRef,
        Expression, Json) that says how the string is interpreted and edited. Enum
        parameters list their allowed values; an empty default means the parameter
        starts blank.

        **File-reading nodes and caching.** Nodes that read files (`bos.load`,
        `duck.read`, `xlsx.read`, `view3d.instances`) are pure despite touching
        disk: the cache key is a hash of the file's content, so an unchanged file
        is never re-read and an edited file is picked up automatically — the key
        is the content itself, not the path or a timestamp.
        """;
}
