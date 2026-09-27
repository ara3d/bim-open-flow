namespace BimOpenFlow.Ask;

/// <summary>The one exception every ClaudeCliBackend failure becomes: one sentence a person can
/// act on (a missing executable, a process that would not start, a bad "init", an error or
/// max-turns "result", a non-zero exit with no result, or a timeout).</summary>
public sealed class ClaudeCliException(string message) : Exception(message);
