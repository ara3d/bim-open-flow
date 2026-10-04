using System.Runtime.CompilerServices;

namespace BimOpenFlow.TestSupport;

/// <summary>Absolute paths inside this checkout, found from this source file's compile-time
/// location rather than the test binary's, so a build with an out-of-tree --artifacts-path
/// still resolves them. Every path is computed, never created. The root is the folder two
/// levels above this file that holds a solution file, whichever repository it is: the
/// toolkit (BimOpenToolkit.sln) today, bim-open-flow after the split.</summary>
public static class RepoPaths
{
    /// <summary>The checkout root: the folder holding a *.sln, two levels above this file.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>samples/&lt;parts&gt;: committed sample data and graphs.</summary>
    public static string Samples(params string[] parts)
        => Under("samples", parts);

    /// <summary>data/&lt;parts&gt;: fetched model fixtures, never committed.</summary>
    public static string Data(params string[] parts)
        => Under("data", parts);

    /// <summary>artifacts/&lt;parts&gt;: gitignored build and test output.</summary>
    public static string Artifacts(params string[] parts)
        => Under("artifacts", parts);

    private static string Under(string top, string[] parts)
        => Path.Combine([Root, top, .. parts]);

    private static string FindRoot([CallerFilePath] string thisFile = "")
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
        return Directory.EnumerateFiles(root, "*.sln").Any()
            ? root
            : throw new InvalidOperationException($"No *.sln at {root}, two levels above {thisFile}");
    }
}
