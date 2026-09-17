using System.IO;
using System.Text;

namespace BimOpenFlow.Publishing;

/// <summary>
/// The bundled viz JavaScript (the BofViz IIFE global) that generators embed.
/// Read from an explicit path — the repo layout is not assumed;
/// FindInRepo is a development-time convenience that probes upward.
/// </summary>
public sealed record VizBundle(string Js)
{
    /// <summary>Repo-relative location of the built bundle (not committed; built by npm).</summary>
    public const string RepoRelativePath = "bimopenflow/web/packages/viz/dist/viz.iife.js";

    public static VizBundle FromFile(string path)
        => new(File.ReadAllText(path, new UTF8Encoding(false)));

    /// <summary>Probes startDir and its ancestors for the built bundle; null when absent.</summary>
    public static string? FindInRepo(string startDir)
    {
        for (var dir = new DirectoryInfo(startDir); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName,
                RepoRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
