using System.Security.Cryptography;

namespace BimOpenFlow.Nodes.Support;

/// <summary>Content hashing for in-memory read caches: uppercase hex SHA-256.
/// Distinct from Ara3D.DataFlowEngine.Runs.Hashes (lowercase, the spec's hash
/// style for persisted run records); these keys never leave process memory.</summary>
// TODO: unify with Runs.Hashes if node caches ever persist their keys.
public static class FileHashes
{
    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
