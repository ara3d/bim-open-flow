using System.ComponentModel;
using System.Diagnostics;

namespace BimOpenFlow.Ask;

/// <summary>Checks that a Claude Code executable actually starts, by running it with --version.
/// A launcher on PATH (~/.local/bin/claude.cmd) can point at the desktop app's MSIX-virtualized
/// folder, which exists for the app but not for an ordinary process; such a file exists and
/// still fails to run.</summary>
public static class ClaudeCliProbe
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>True when the executable exits 0 within Timeout; a .cmd or .bat runs through
    /// cmd.exe /c as in ClaudeCliProcess.</summary>
    public static bool Runs(string executable)
    {
        var isScript = executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo
        {
            FileName = isScript ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe" : executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (isScript)
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(executable);
        }
        startInfo.ArgumentList.Add("--version");

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
                return false;
            // Drained asynchronously so a chatty child cannot block on a full pipe.
            _ = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (process.WaitForExit(Timeout))
                return process.ExitCode == 0;
            process.Kill(entireProcessTree: true);
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
