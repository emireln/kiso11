using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kiso11.Services;

public class ProcessRunner
{
    public event Action<string>? LineReceived;

    /// Writes a line to the operation log without starting a process.
    public void Report(string message) => LineReceived?.Invoke(message);

    public async Task<int> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var (exitCode, _) = await ExecuteAsync(fileName, arguments, workingDirectory, log: true, cancellationToken);
        return exitCode;
    }

    public async Task<string> RunAndCaptureOutputAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var (_, output) = await ExecuteAsync(fileName, arguments, workingDirectory, log: true, cancellationToken);
        return output;
    }

    /// Runs a PowerShell script. The script is passed base64-encoded, so quotes and apostrophes in paths cannot break it.
    public Task<(int ExitCode, string Output)> PowerShellAsync(
        string script,
        CancellationToken cancellationToken = default,
        bool log = true)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return ExecuteAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            null,
            log,
            cancellationToken);
    }

    private async Task<(int ExitCode, string Output)> ExecuteAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        bool log,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var outputLock = new object();

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };

        void HandleLine(string? data)
        {
            if (data == null) return;

            lock (outputLock)
            {
                output.AppendLine(data);
            }

            if (log)
            {
                LineReceived?.Invoke(data);
            }
        }

        process.OutputDataReceived += (_, e) => HandleLine(e.Data);
        process.ErrorDataReceived += (_, e) => HandleLine(e.Data);

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch { }
            });

            await process.WaitForExitAsync(cancellationToken);

            string captured;
            lock (outputLock)
            {
                captured = output.ToString();
            }

            return (process.ExitCode, captured);
        }
        catch (OperationCanceledException)
        {
            Report(LocalizationService.Instance.LogCancelledByUser);
            throw;
        }
        catch (Exception ex)
        {
            Report(string.Format(LocalizationService.Instance.LogProcessFailed, fileName, ex.Message));
            throw;
        }
    }
}
