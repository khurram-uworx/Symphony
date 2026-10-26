using Symphony.Security;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Symphony.Utils;

record CommandResult(bool Success, int ExitCode, string StdOut, string StdErr);

class ShellCommandRunner
{
    static string escape(string command) =>
        command.Replace("\\", "\\\\").Replace("\"", "\\\"");

    static bool isExecutableOnPath(string name)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var path in paths)
        {
            var candidate = Path.Combine(path, name + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty));
            if (File.Exists(candidate))
            {
                return true;
            }
        }

        return false;
    }

    static void tryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
            // ignored
        }
    }

    static string ExpandEnvironmentVariables(string command, IReadOnlyDictionary<string, string>? environment)
    {
        if (string.IsNullOrWhiteSpace(command) || environment is null || environment.Count == 0)
            return command;

        var expanded = System.Text.RegularExpressions.Regex.Replace(
            command,
            @"%([A-Za-z_][A-Za-z0-9_]*)%",
            match =>
            {
                var name = match.Groups[1].Value;
                return environment.TryGetValue(name, out var value) && value is not null ? value : match.Value;
            });

        expanded = System.Text.RegularExpressions.Regex.Replace(
            expanded,
            @"\\$\\{([A-Za-z_][A-Za-z0-9_]*)(?::-[^}]*)?\\}",
            match =>
            {
                var name = match.Groups[1].Value;
                return environment.TryGetValue(name, out var value) && value is not null ? value : match.Value;
            });

        return expanded;
    }

    readonly ILogger<ShellCommandRunner> logger;

    public ShellCommandRunner(ILogger<ShellCommandRunner> logger)
    {
        this.logger = logger;
    }

    public async Task<CommandResult> RunAsync(string command, string workingDirectory, int timeoutMs) =>
        await RunAsync(command, workingDirectory, timeoutMs, null, CancellationToken.None);

    public async Task<CommandResult> RunAsync(string command, string workingDirectory, int timeoutMs,
        CancellationToken cancellationToken) =>
        await RunAsync(command, workingDirectory, timeoutMs, null, cancellationToken);

    public virtual async Task<CommandResult> RunAsync(string command, string workingDirectory, int timeoutMs,
        IReadOnlyDictionary<string, string>? environment, CancellationToken cancellationToken)
    {
        var expandedCommand = ExpandEnvironmentVariables(command, environment);
        var startInfo = BuildShellStartInfo(expandedCommand, workingDirectory);
        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                if (pair.Key is not null && pair.Value is not null)
                    startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
                output.AppendLine(args.Data);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
                error.AppendLine(args.Data);
        };

        if (!process.Start())
            return new CommandResult(false, -1, "failed to start", "");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = timeoutMs > 0
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        if (timeoutCts is not null)
            timeoutCts.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(timeoutCts?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException)
        {
            tryKill(process);
            return new CommandResult(false, -1, output.ToString(), "timeout or cancellation");
        }

        if (process.ExitCode != 0)
            logger.LogWarning("Command failed with exit {ExitCode}: {Command}", process.ExitCode, Redaction.RedactSecrets(command));

        return new CommandResult(process.ExitCode == 0, process.ExitCode, output.ToString(), error.ToString());
    }

    public static ProcessStartInfo BuildShellStartInfo(string command, string workingDirectory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            //if (isExecutableOnPath("bash"))
            //{
            //    return new ProcessStartInfo
            //    {
            //        FileName = "bash",
            //        Arguments = $"-lc \"{escape(command)}\"",
            //        WorkingDirectory = workingDirectory,
            //        RedirectStandardOutput = true,
            //        RedirectStandardError = true,
            //        RedirectStandardInput = true,
            //        UseShellExecute = false
            //    };
            //}

            return new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c {command}",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false
            };
        }

        return new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-lc \"{escape(command)}\"",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        };
    }
}
