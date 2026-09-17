using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ara3D.DataFlowEngine.Runs;

namespace BimOpenFlow.Host.Store;

/// <summary>
/// Run archival: runs are frozen evidence, so archived files are immutable.
/// File name = file-safe run timestamp + first 8 chars of the graph hash.
/// </summary>
public static class AnalysisStoreRuns
{
    public static string RunFileName(RunRecord record)
        => FileSafeTimestamp(record.TimestampUtc) + "-" + record.GraphHash[..8] + RunRecord.FileExtension;

    /// <summary>RFC 3339 "2026-08-31T12:00:00.123Z" becomes "20260831T120000123Z".</summary>
    public static string FileSafeTimestamp(string timestampUtc)
        => string.Concat(timestampUtc.Where(char.IsAsciiLetterOrDigit));

    /// <summary>Archives the run and returns its file name. Refuses to overwrite:
    /// an existing file with the same name throws IOException.</summary>
    public static string SaveRun(this AnalysisStore store, string id, RunRecord record)
    {
        var runsDir = store.RunsDir(id);
        Directory.CreateDirectory(runsDir);
        var fileName = RunFileName(record);
        AtomicFile.WriteAllTextNew(Path.Combine(runsDir, fileName),
            record.ToCanonicalJson(), Ara3D.NodeGraph.GraphDocumentIO.Utf8NoBom);
        return fileName;
    }

    /// <summary>Run file names in chronological (name) order.</summary>
    public static IReadOnlyList<string> ListRuns(this AnalysisStore store, string id)
    {
        var runsDir = store.RunsDir(id);
        if (!Directory.Exists(runsDir))
            return Array.Empty<string>();
        return Directory.EnumerateFiles(runsDir, "*" + RunRecord.FileExtension)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }

    public static RunRecord LoadRun(this AnalysisStore store, string id, string fileName)
        => RunRecordJson.Load(Path.Combine(store.RunsDir(id), fileName));
}
