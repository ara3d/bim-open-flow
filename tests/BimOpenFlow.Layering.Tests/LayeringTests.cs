// Enforces this repository's layering: project references only point down (mcp -> flow ->
// data, where data is bim-open-data reached through $(DepsRoot)); the generic graph tool
// names no BIM pack, no native mesher, and nothing of the toolkit's studio; test support
// references nothing. The web packages' rule is enforced beside them, by
// bimopenflow/web/packages/client/test/genericPackages.test.ts, and checked here as well.
using System.Text.RegularExpressions;
using BimOpenFlow.TestSupport;

namespace BimOpenFlow.Layering.Tests;

public static class Layering
{
    public static readonly DirectoryInfo Root = new(RepoPaths.Root);

    public const string TestSupport = "BimOpenFlow.TestSupport";

    /// <summary>Layers a project in the given group may reference, besides its own group and
    /// external dependencies (deps/ other than bim-open-data's src/data).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Allowed = new Dictionary<string, string[]>
    {
        ["flow"] = ["data"],
        ["mcp"] = ["data", "flow"],
        ["studio"] = ["data", "flow", "mcp"],
    };

    public const string External = "external";

    /// <summary>The MSBuild property every reference into deps/ starts with (Directory.Build.props).</summary>
    public const string DepsRootProperty = "$(DepsRoot)";

    /// <summary>The BIM node packs and the native mesher, which stay in bim-open-toolkit.</summary>
    public static readonly string[] BimOnly =
        ["BimOpenFlow.Nodes.Bos", "BimOpenFlow.Nodes.BimAnalysis", "BimOpenFlow.Nodes.Geometry", "Ara3D.Ifc.Mesher"];

    /// <summary>What the generic web packages may never name: the viewer, the toolkit's 3D pane,
    /// the notebook, and the toolkit's pages.</summary>
    public static readonly string[] BimOnlyWeb =
        ["@bim-open-viewer/", "@bimopenflow/pane-3d", "@bimopenflow/bim-open-notebook", "@bimopenflow/studio-web"];

    public static IEnumerable<FileInfo> Projects(string top)
        => new DirectoryInfo(Path.Combine(Root.FullName, top)).EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    public static IEnumerable<FileInfo> OwnProjects()
        => Projects("src").Concat(Projects("tests"));

    public static string NameOf(string projectPath)
        => Path.GetFileNameWithoutExtension(projectPath);

    /// <summary>data (bim-open-data's src/data, through deps/), flow, mcp, studio, a test support
    /// project's name, or external.</summary>
    public static string GroupOf(string fullPath)
    {
        var rel = Path.GetRelativePath(Root.FullName, fullPath).Replace('\\', '/');
        var parts = rel.Split('/');
        return parts[0] switch
        {
            "deps" when parts.Length > 3 && parts[1] == "bim-open-data" && parts[2] == "src" => parts[3],
            "deps" => External,
            "tests" when parts[1] == TestSupport => TestSupport,
            "src" or "tests" => parts[1],
            _ => parts[0],
        };
    }

    static readonly Regex ProjectRef = new(@"<ProjectReference\s+Include=""([^""]+)""", RegexOptions.Compiled);

    public static IEnumerable<(FileInfo From, string To)> References(FileInfo project)
    {
        var text = File.ReadAllText(project.FullName);
        foreach (Match m in ProjectRef.Matches(text))
        {
            var raw = m.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar);
            if (raw.StartsWith(DepsRootProperty))
                yield return (project, Path.GetFullPath(Path.Combine(Root.FullName, "deps", raw[DepsRootProperty.Length..])));
            else if (!raw.StartsWith("$("))
                yield return (project, Path.GetFullPath(Path.Combine(project.DirectoryName!, raw)));
        }
    }

    public static IEnumerable<string> Violations()
        => from project in OwnProjects()
           let layer = GroupOf(project.FullName)
           where Allowed.ContainsKey(layer)
           from edge in References(project)
           let target = GroupOf(edge.To)
           where target != layer && target != External && target != TestSupport && !Allowed[layer].Contains(target)
           select $"{Rel(project.FullName)} -> {Rel(edge.To)} ({layer} may not reference {target})";

    public static IEnumerable<string> BimViolations()
        => from project in OwnProjects()
           from edge in References(project)
           where BimOnly.Contains(NameOf(edge.To))
           select $"{Rel(project.FullName)} -> {Rel(edge.To)} (the BIM packs and Ifc.Mesher stay in bim-open-toolkit)";

    /// <summary>The vitest file holding the same rule beside the web code; its own examples
    /// name the forbidden packages, so the scan skips it.</summary>
    public const string VitestRule = "genericPackages.test.ts";

    static string AnyBimOnlyWeb =>string.Join("|", BimOnlyWeb.Select(Regex.Escape));

    /// <summary>An import of a BimOnlyWeb package in TypeScript (static, side-effect, or dynamic).</summary>
    static readonly Regex WebImport = new(@"(?:\bfrom\s+|\bimport\s*\(\s*|\bimport\s+)[""']((?:" + AnyBimOnlyWeb + @")[^""']*)[""']", RegexOptions.Compiled);

    /// <summary>A BimOnlyWeb package named as a key, as a package.json dependency is.</summary>
    static readonly Regex WebDependency = new(@"""((?:" + AnyBimOnlyWeb + @")[^""]*)""\s*:", RegexOptions.Compiled);

    /// <summary>Every package.json dependency on, and every TypeScript import of, a BimOnlyWeb
    /// package in the web packages. Comments and test lists that name them are not imports.</summary>
    public static IEnumerable<string> WebViolations()
    {
        var packages = new DirectoryInfo(Path.Combine(Root.FullName, "bimopenflow", "web", "packages"));
        foreach (var package in packages.EnumerateDirectories())
        {
            var manifest = new FileInfo(Path.Combine(package.FullName, "package.json"));
            if (manifest.Exists)
                foreach (Match m in WebDependency.Matches(File.ReadAllText(manifest.FullName)))
                    yield return $"{Rel(manifest.FullName)} depends on {m.Groups[1].Value}";
            var sources = new[] { "src", "test", "scripts" }
                .Select(d => new DirectoryInfo(Path.Combine(package.FullName, d)))
                .Where(d => d.Exists)
                .SelectMany(d => d.EnumerateFiles("*.ts", SearchOption.AllDirectories))
                .Where(f => f.Name != VitestRule);
            foreach (var file in sources)
                foreach (Match m in WebImport.Matches(File.ReadAllText(file.FullName)))
                    yield return $"{Rel(file.FullName)} imports {m.Groups[1].Value}";
        }
    }

    public static string Rel(string path)
        => Path.GetRelativePath(Root.FullName, path).Replace('\\', '/');
}

public class LayeringTests
{
    [Test]
    public void ProjectReferencesOnlyPointDown()
    {
        var violations = Layering.Violations().ToList();
        Assert.That(violations, Is.Empty, string.Join("\n", violations));
    }

    [Test]
    public void NoProjectReferencesABimPackOrTheMesher()
    {
        var violations = Layering.BimViolations().ToList();
        Assert.That(violations, Is.Empty, string.Join("\n", violations));
    }

    [Test]
    public void WebPackagesNameNoViewerPaneOrNotebook()
    {
        var violations = Layering.WebViolations().ToList();
        Assert.That(violations, Is.Empty, string.Join("\n", violations));
    }

    [Test]
    public void TheVitestRuleExistsAndNamesWhatItForbids()
    {
        var path = Path.Combine(Layering.Root.FullName, "bimopenflow", "web", "packages", "client", "test", Layering.VitestRule);
        Assert.That(File.Exists(path), path);
        Assert.That(File.ReadAllText(path), Does.Contain("@bim-open-viewer/").And.Contain("@bimopenflow/pane-3d"));
    }

    [Test]
    public void TestSupportReferencesNothing()
    {
        var targets = Layering.Projects(Path.Combine("tests", Layering.TestSupport))
            .SelectMany(Layering.References)
            .Select(r => Layering.Rel(r.To))
            .ToList();
        Assert.That(targets, Is.Empty, "TestSupport is referenceable from every group only because it references nothing: " + string.Join("\n", targets));
    }

    [Test]
    public void FindsTheReferenceGraph()
    {
        Assert.That(Layering.OwnProjects().Count(), Is.GreaterThan(40), "expected more than forty projects under src and tests");
        Assert.That(Layering.OwnProjects().SelectMany(Layering.References).Count(), Is.GreaterThan(100),
            "expected the src and tests projects to carry more than a hundred project references");
    }

    [Test]
    public void EveryGroupHasProjects()
    {
        foreach (var group in new[] { "flow", "mcp", "studio" })
            Assert.That(Layering.Projects("src").Any(p => Layering.GroupOf(p.FullName) == group), $"src/{group} has no projects");
    }
}
