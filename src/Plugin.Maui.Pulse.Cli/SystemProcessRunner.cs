using System.Diagnostics;
using Plugin.Maui.Pulse;

namespace Plugin.Maui.Pulse.Cli;

public sealed class SystemProcessRunner : IProcessRunner
{
    public int Run(string fileName, IReadOnlyList<string> arguments, out string stdout, out string stderr)
    {
        var start = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                stdout = "";
                stderr = $"Could not start {fileName}.";
                return 1;
            }

            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(15_000);
            return process.HasExited ? process.ExitCode : 1;
        }
        catch (Exception ex)
        {
            stdout = "";
            stderr = ex.Message;
            return 1;
        }
    }
}
