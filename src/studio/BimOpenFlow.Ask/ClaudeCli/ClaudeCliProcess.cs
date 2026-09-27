using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace BimOpenFlow.Ask;

/// <summary>Runs one claude invocation as a child process. Knows nothing about stream-json or
/// AskEvents: it hands stdout lines to the caller and reports how the process ended. The npm
/// install puts claude on PATH as a .cmd shim, which Windows' CreateProcess cannot start
/// directly, so a .cmd or .bat executable is run through cmd.exe /c; ArgumentList still encodes
/// each argument (including an empty one) the same way node's own argv parser expects, and
/// %* in the shim reproduces them unchanged.</summary>
internal static class ClaudeCliProcess
{
    private const int StderrTailLength = 2000;

    /// <summary>One run: arguments, environment trimmed by PrepareEnvironment, input on stdin
    /// (UTF-8, no BOM, then closed), each stdout line to onLine in order, the last 2,000
    /// characters of stderr kept. Kills the process tree on cancellation, on timeout
    /// (ClaudeCliException), or when onLine throws (rethrown).</summary>
    public static async Task<(int ExitCode, string StandardErrorTail)> RunAsync(string executable,
        IReadOnlyList<string> arguments, string workDirectory, string input, Func<string, Task> onLine,
        TimeSpan timeout, CancellationToken ct)
    {
        if (!File.Exists(executable))
            throw new ClaudeCliException($"Could not start Claude Code at {executable}: the file does not exist.");

        var isScript = executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

        var startInfo = new ProcessStartInfo
        {
            FileName = isScript ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe" : executable,
            WorkingDirectory = workDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (isScript)
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(executable);
        }
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        ClaudeCliArguments.PrepareEnvironment(startInfo.Environment);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception e)
        {
            throw new ClaudeCliException($"Could not start Claude Code at {executable}: {e.Message}.");
        }

        var stderrTail = new StringBuilder();
        var stderrTask = DrainStderrAsync(process, stderrTail);

        try
        {
            await process.StandardInput.WriteAsync(input);
        }
        catch (IOException)
        {
            // The process may already have exited (a scripted failure that never reads stdin).
        }
        finally
        {
            process.StandardInput.Close();
        }

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(linked.Token)) is not null)
                await onLine(line);
            await stderrTask;
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            Kill(process);
            throw new ClaudeCliException($"Claude Code did not finish within {FormatTimeout(timeout)}.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Kill(process);
            throw;
        }
        catch
        {
            // onLine threw, or something else went wrong reading the streams: stop the child and
            // let the original exception surface unchanged.
            Kill(process);
            throw;
        }

        var tail = stderrTail.ToString();
        return (process.ExitCode, tail.Length <= StderrTailLength ? tail : tail[^StderrTailLength..]);
    }

    private static async Task DrainStderrAsync(Process process, StringBuilder tail)
    {
        try
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) is not null)
            {
                tail.Append(line).Append('\n');
                if (tail.Length > StderrTailLength * 2)
                    tail.Remove(0, tail.Length - StderrTailLength);
            }
        }
        catch (IOException)
        {
            // The pipe is gone once the process is killed; nothing more to collect.
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string FormatTimeout(TimeSpan timeout)
        => timeout.TotalMinutes == Math.Floor(timeout.TotalMinutes)
            ? $"{timeout.TotalMinutes:0} minutes"
            : $"{timeout.TotalSeconds:0} seconds";
}
