using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ara3D.NodeGraph;

namespace BimOpenFlow.Host.Store;

/// <summary>
/// The analysis library on one root directory: graph documents with versioned
/// saves, plus run archival (see AnalysisStoreRuns). Synchronous, no locks;
/// concurrent writers are last-writer-wins via atomic replace.
/// </summary>
public sealed class AnalysisStore
{
    public const string CurrentFileName = "current" + GraphFormat.Extension;
    public const string NameFileName = "name.txt";
    public const string VersionsDirName = "versions";
    public const string RunsDirName = "runs";
    public const string TrashDirName = ".trash";

    public readonly string RootDir;

    public AnalysisStore(string rootDir)
    {
        RootDir = Path.GetFullPath(rootDir);
        Directory.CreateDirectory(RootDir);
    }

    internal string AnalysisDir(string id)
        => Path.Combine(RootDir, AnalysisId.Validate(id));

    internal string CurrentPath(string id)
        => Path.Combine(AnalysisDir(id), CurrentFileName);

    internal string VersionsDir(string id)
        => Path.Combine(AnalysisDir(id), VersionsDirName);

    internal string RunsDir(string id)
        => Path.Combine(AnalysisDir(id), RunsDirName);

    public bool Exists(string id)
        => File.Exists(CurrentPath(id));

    public IReadOnlyList<AnalysisEntry> List()
        => Directory.EnumerateDirectories(RootDir)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(id => AnalysisId.IsValid(id) && File.Exists(Path.Combine(RootDir, id, CurrentFileName)))
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => new AnalysisEntry(id, ReadName(id)))
            .ToList();

    private string ReadName(string id)
    {
        var namePath = Path.Combine(RootDir, id, NameFileName);
        if (!File.Exists(namePath))
            return id;
        var name = File.ReadAllText(namePath).Trim();
        return name.Length > 0 ? name : id;
    }

    /// <summary>Creates an empty analysis; throws if the id is invalid or taken.</summary>
    public void Create(string id)
    {
        if (Exists(id))
            throw new InvalidOperationException($"Analysis '{id}' already exists");
        Save(id, GraphDocument.Empty);
    }

    public GraphDocument Load(string id)
        => GraphDocumentIO.Load(CurrentPath(id));

    /// <summary>
    /// Saves the document as the new current version. When the canonical bytes
    /// equal the existing current, nothing is written and false is returned.
    /// Otherwise the previous current (if any) is archived under versions/ with
    /// the next zero-padded sequence number, the new current lands atomically,
    /// and true is returned.
    /// </summary>
    public bool Save(string id, GraphDocument doc)
    {
        var currentPath = CurrentPath(id);
        var json = doc.ToCanonicalJson();
        if (File.Exists(currentPath))
        {
            var existing = File.ReadAllText(currentPath, GraphDocumentIO.Utf8NoBom);
            if (existing == json)
                return false;
            ArchiveCurrent(id, currentPath);
        }
        Directory.CreateDirectory(AnalysisDir(id));
        AtomicFile.WriteAllText(currentPath, json, GraphDocumentIO.Utf8NoBom);
        return true;
    }

    private void ArchiveCurrent(string id, string currentPath)
    {
        var versionsDir = VersionsDir(id);
        Directory.CreateDirectory(versionsDir);
        var next = VersionSequences(versionsDir).DefaultIfEmpty(0).Max() + 1;
        File.Copy(currentPath, Path.Combine(versionsDir, VersionFileName(next)));
    }

    public static string VersionFileName(int sequence)
        => sequence.ToString("D4", CultureInfo.InvariantCulture) + GraphFormat.Extension;

    private static IEnumerable<int> VersionSequences(string versionsDir)
        => Directory.EnumerateFiles(versionsDir, "*" + GraphFormat.Extension)
            .Select(f => Path.GetFileName(f)[..^GraphFormat.Extension.Length])
            .Where(stem => stem.All(char.IsAsciiDigit))
            .Select(int.Parse);

    /// <summary>Archived versions in sequence order; the current document is not included.</summary>
    public IReadOnlyList<AnalysisVersion> History(string id)
    {
        var versionsDir = VersionsDir(id);
        if (!Directory.Exists(versionsDir))
            return Array.Empty<AnalysisVersion>();
        return VersionSequences(versionsDir)
            .OrderBy(n => n)
            .Select(n => new AnalysisVersion(n, LoadVersion(id, n).ComputeGraphHash(), VersionFileName(n)))
            .ToList();
    }

    public GraphDocument LoadVersion(string id, int sequence)
        => GraphDocumentIO.Load(Path.Combine(VersionsDir(id), VersionFileName(sequence)));

    /// <summary>
    /// Moves the whole analysis folder to .trash/ (reversible by hand). A
    /// repeated delete of the same id gets a numeric suffix in the trash.
    /// </summary>
    public void Delete(string id)
    {
        var dir = AnalysisDir(id);
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Analysis '{id}' does not exist");
        var trashDir = Path.Combine(RootDir, TrashDirName);
        Directory.CreateDirectory(trashDir);
        var target = Path.Combine(trashDir, id);
        for (var i = 2; Directory.Exists(target); i++)
            target = Path.Combine(trashDir, $"{id}-{i}");
        Directory.Move(dir, target);
    }
}
