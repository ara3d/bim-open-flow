using System.Security.Cryptography;
using System.Text;

namespace BimOpenFlow.Relations;

/// <summary>A node of the logical plan. Identity is the canonical text: two plans are
/// equal when they render the same, and Hash is what every downstream cache keys on.</summary>
public abstract class Plan : IEquatable<Plan>
{
    private string? _text;
    private string? _hash;

    public abstract IReadOnlyList<Plan> Inputs { get; }

    public string Text
        => _text ??= this.Render();

    public string Hash
        => _hash ??= Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Text))).ToLowerInvariant();

    public bool Equals(Plan? other)
        => other is not null && Text == other.Text;

    public override bool Equals(object? obj)
        => Equals(obj as Plan);

    public override int GetHashCode()
        => Text.GetHashCode(StringComparison.Ordinal);

    public override string ToString()
        => Text;
}
