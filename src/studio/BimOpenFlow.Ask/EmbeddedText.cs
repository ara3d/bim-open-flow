using System.Reflection;

namespace BimOpenFlow.Ask;

/// <summary>Reads a text file compiled into an assembly as an embedded resource by its logical
/// name. The Ask prompts keep their guides in .claude/skills so Claude Code and the in-process
/// agent read one text; the csproj links those files in under a stable name.</summary>
public static class EmbeddedText
{
    public static string Read(Assembly assembly, string logicalName)
    {
        using var stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new FileNotFoundException(
                $"Embedded resource '{logicalName}' is missing from {assembly.GetName().Name}. "
                + $"Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
