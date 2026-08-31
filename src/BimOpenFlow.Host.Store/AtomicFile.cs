using System;
using System.IO;
using System.Text;

namespace BimOpenFlow.Host.Store;

/// <summary>
/// Atomic single-file writes: content lands in a temp file in the target
/// directory, then moves into place. Readers see the old bytes or the new
/// bytes, never a partial write.
/// </summary>
internal static class AtomicFile
{
    public static void WriteAllText(string filePath, string content, Encoding encoding)
        => WriteVia(filePath, content, encoding, overwrite: true);

    /// <summary>Writes only if the target does not exist; throws IOException otherwise.</summary>
    public static void WriteAllTextNew(string filePath, string content, Encoding encoding)
        => WriteVia(filePath, content, encoding, overwrite: false);

    private static void WriteVia(string filePath, string content, Encoding encoding, bool overwrite)
    {
        var tempPath = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(tempPath, content, encoding);
        try
        {
            File.Move(tempPath, filePath, overwrite);
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
    }
}
