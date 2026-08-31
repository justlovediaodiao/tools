using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using RawV.Models;

namespace RawV.Services;

public sealed class FileDeletionService
{
    public async Task<DeleteResult> DeleteToRecycleBinAsync(ImageEntry currentImage, IReadOnlyList<ImageEntry> allImages, CancellationToken cancellationToken = default)
    {
        var targets = new List<string> { currentImage.FilePath };
        targets.AddRange(currentImage.AssociatedRawFiles);
        var deletedPaths = new List<string>();

        try
        {
            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DeleteSingleAsync(target, cancellationToken);
                deletedPaths.Add(target);
            }

            var currentIndex = allImages
                .Select((item, index) => (item, index))
                .FirstOrDefault(pair => string.Equals(pair.item.FilePath, currentImage.FilePath, StringComparison.OrdinalIgnoreCase))
                .index;

            var remainingCount = Math.Max(allImages.Count - 1, 0);
            int? nextIndex = remainingCount == 0 ? null : Math.Min(currentIndex, remainingCount - 1);

            return new DeleteResult(true, null, nextIndex, deletedPaths, true);
        }
        catch (Exception ex)
        {
            var currentImageDeleted = deletedPaths.Any(path => string.Equals(path, currentImage.FilePath, StringComparison.OrdinalIgnoreCase));
            return new DeleteResult(false, ex.Message, null, deletedPaths, currentImageDeleted);
        }
    }

    private static async Task DeleteSingleAsync(string path, CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await RunProcessAsync(
                "powershell",
                $"-NoProfile -Command \"Add-Type -AssemblyName Microsoft.VisualBasic; [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile('{EscapePowerShellLiteral(path)}','OnlyErrorDialogs','SendToRecycleBin')\"",
                cancellationToken);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await RunProcessAsync(
                "osascript",
                $"-e \"tell application \\\"Finder\\\" to delete POSIX file \\\"{EscapeDoubleQuoted(path)}\\\"\"",
                cancellationToken);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await RunProcessAsync("gio", $"trash \"{EscapeDoubleQuoted(path)}\"", cancellationToken);
            return;
        }

        throw new PlatformNotSupportedException("Current platform does not provide a supported recycle bin integration.");
    }

    private static async Task RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start recycle bin command: {fileName}");
        }

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            var message = new StringBuilder();
            message.Append($"Recycle bin command failed: {fileName} exited with code {process.ExitCode}.");

            if (!string.IsNullOrWhiteSpace(output))
            {
                message.Append(' ').Append(output.Trim());
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                message.Append(' ').Append(error.Trim());
            }

            throw new InvalidOperationException(message.ToString());
        }
    }

    private static string EscapePowerShellLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string EscapeDoubleQuoted(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
