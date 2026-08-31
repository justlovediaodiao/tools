using RawV.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace RawV.Services;

public sealed class FmtpService
{
    public static string ExecutablePath => Path.Combine(AppContext.BaseDirectory, "fmtp.exe");

    public static bool IsExecutableAvailable =>
        OperatingSystem.IsWindows()
        && File.Exists(ExecutablePath);

    public async Task<int> RunAsync(
        string localDirectory,
        string mtpPath,
        IProgress<FmtpEvent> progress,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            WorkingDirectory = localDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(localDirectory);
        startInfo.ArgumentList.Add(mtpPath);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start fmtp.");
        }

        var outputTask = ReadOutputAsync(process.StandardOutput, progress, cancellationToken);
        var errorTask = ReadErrorsAsync(process.StandardError, progress, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The process exited between checking HasExited and killing it.
                }
            }

            await process.WaitForExitAsync(CancellationToken.None);
            try
            {
                await Task.WhenAll(outputTask, errorTask);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // The process and both redirected streams were canceled together.
            }
            throw;
        }
    }

    private static async Task ReadOutputAsync(
        StreamReader reader,
        IProgress<FmtpEvent> progress,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (TryParseEvent(line, out var fmtpEvent))
            {
                progress.Report(fmtpEvent);
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                progress.Report(new FmtpEvent("info", line));
            }
        }
    }

    private static async Task ReadErrorsAsync(
        StreamReader reader,
        IProgress<FmtpEvent> progress,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                progress.Report(new FmtpEvent("error", line));
            }
        }
    }

    private static bool TryParseEvent(string line, out FmtpEvent fmtpEvent)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeProperty)
                || typeProperty.ValueKind != JsonValueKind.String)
            {
                fmtpEvent = default!;
                return false;
            }

            var type = typeProperty.GetString();
            if (string.IsNullOrWhiteSpace(type))
            {
                fmtpEvent = default!;
                return false;
            }

            fmtpEvent = new FmtpEvent(
                type,
                GetString(root, "message"),
                GetInt32(root, "current"),
                GetInt32(root, "total"),
                GetString(root, "file"));
            return true;
        }
        catch (JsonException)
        {
            fmtpEvent = default!;
            return false;
        }
        catch (InvalidOperationException)
        {
            fmtpEvent = default!;
            return false;
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int GetInt32(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : 0;
}
