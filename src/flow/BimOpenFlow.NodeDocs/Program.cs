using BimOpenFlow.NodeDocs;

// The generic packs' reference, written to the path given. In the toolkit, docs/nodes.md
// also lists the BIM packs and comes from `bimopenflow-studio nodedocs` instead, so this
// program takes no default path that could overwrite it with fewer packs.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: BimOpenFlow.NodeDocs <output.md>");
    Console.Error.WriteLine("For the toolkit's docs/nodes.md, run: dotnet run --project src/studio/BimOpenFlow.Studio -- nodedocs");
    return 2;
}
return NodeDocsProgram.Run(args[0], NodeDocsProgram.GenericPacks, NodeNotes.Generic);
