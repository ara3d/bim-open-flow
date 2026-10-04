namespace BimOpenFlow.Nodes.Compliance;

/// <summary>
/// Mirrors the Verdict enum in contracts/contracts.json; this pack takes no
/// contracts dependency, so member names must stay identical.
/// </summary>
public enum Verdict
{
    Pass,
    Fail,
    NeedsReview,
    InfoNotAvailable,
}

public static class VerdictExtensions
{
    /// <summary>Rollup severity order: Fail > NeedsReview > InfoNotAvailable > Pass.</summary>
    public static int Severity(this Verdict verdict)
        => verdict switch
        {
            Verdict.Fail => 3,
            Verdict.NeedsReview => 2,
            Verdict.InfoNotAvailable => 1,
            Verdict.Pass => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(verdict)),
        };

    public static Verdict Worst(this Verdict a, Verdict b)
        => a.Severity() >= b.Severity() ? a : b;

    /// <summary>The exact text stored in a verdict column.</summary>
    public static string ToText(this Verdict verdict)
        => verdict.ToString();

    public static Verdict ParseVerdict(string text)
        => text switch
        {
            "Pass" => Verdict.Pass,
            "Fail" => Verdict.Fail,
            "NeedsReview" => Verdict.NeedsReview,
            "InfoNotAvailable" => Verdict.InfoNotAvailable,
            _ => throw new ArgumentException($"Unknown verdict text '{text}'", nameof(text)),
        };
}
