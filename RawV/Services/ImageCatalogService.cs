using System.Globalization;
using RawV.Models;

namespace RawV.Services;

public sealed class ImageCatalogService
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".heic"
    };

    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cr2", ".cr3", ".nef", ".arw", ".raf", ".dng", ".orf", ".rw2", ".pef", ".sr2"
    };

    public BrowserSession CreateFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return new BrowserSession(Array.Empty<ImageEntry>(), 0);
        }

        // Build a lookup of all RAW files in the directory
        var rawFilesByBaseName = Directory.EnumerateFiles(directoryPath)
            .Where(IsRawPath)
            .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

        var items = Directory.EnumerateFiles(directoryPath)
            .Where(IsDisplayableImagePath)
            .Select(path => CreateEntry(path, rawFilesByBaseName))
            .OrderBy(static entry => entry.FileName, NaturalFileNameComparer.Instance)
            .ToArray();

        return new BrowserSession(items, items.Length > 0 ? 0 : -1);
    }

    public BrowserSession CreateFromFiles(IEnumerable<string> filePaths)
    {
        var distinctPaths = filePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Where(IsDisplayableImagePath)
            .ToArray();

        // Collect all unique directories and their RAW files
        var rawFilesByDirectory = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in distinctPaths)
        {
            var dir = Path.GetDirectoryName(path)!;
            if (!rawFilesByDirectory.ContainsKey(dir))
            {
                var rawFiles = Directory.EnumerateFiles(dir)
                    .Where(IsRawPath)
                    .GroupBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
                rawFilesByDirectory[dir] = rawFiles;
            }
        }

        var items = distinctPaths
            .Select(path => CreateEntry(path, rawFilesByDirectory[Path.GetDirectoryName(path)!]))
            .OrderBy(static entry => entry.FileName, NaturalFileNameComparer.Instance)
            .ToArray();

        return new BrowserSession(items, items.Length > 0 ? 0 : -1);
    }

    public static bool IsDisplayableImagePath(string path)
    {
        var extension = Path.GetExtension(path);
        return SupportedImageExtensions.Contains(extension) && !RawExtensions.Contains(extension);
    }

    public static bool IsRawPath(string path)
    {
        return RawExtensions.Contains(Path.GetExtension(path));
    }

    private static ImageEntry CreateEntry(string filePath, Dictionary<string, string[]> rawFilesByBaseName)
    {
        var fileInfo = new FileInfo(filePath);

        // Find associated RAW files
        var baseName = Path.GetFileNameWithoutExtension(fileInfo.Name);
        var associatedRawFiles = rawFilesByBaseName.TryGetValue(baseName, out var rawFiles)
            ? rawFiles
            : Array.Empty<string>();

        return new ImageEntry(
            fileInfo.FullName,
            fileInfo.Name,
            fileInfo.DirectoryName ?? string.Empty,
            baseName,
            fileInfo.Exists ? fileInfo.Length : 0,
            null,
            null,
            associatedRawFiles);
    }

    private sealed class NaturalFileNameComparer : IComparer<string>
    {
        public static NaturalFileNameComparer Instance { get; } = new();

        public int Compare(string? x, string? y)
        {
            return CompareCore(x ?? string.Empty, y ?? string.Empty);
        }

        private static int CompareCore(string left, string right)
        {
            var leftIndex = 0;
            var rightIndex = 0;

            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                var leftChar = left[leftIndex];
                var rightChar = right[rightIndex];

                if (char.IsDigit(leftChar) && char.IsDigit(rightChar))
                {
                    var leftStart = leftIndex;
                    var rightStart = rightIndex;

                    while (leftIndex < left.Length && char.IsDigit(left[leftIndex]))
                    {
                        leftIndex++;
                    }

                    while (rightIndex < right.Length && char.IsDigit(right[rightIndex]))
                    {
                        rightIndex++;
                    }

                    var leftDigits = left[leftStart..leftIndex].TrimStart('0');
                    var rightDigits = right[rightStart..rightIndex].TrimStart('0');

                    leftDigits = leftDigits.Length == 0 ? "0" : leftDigits;
                    rightDigits = rightDigits.Length == 0 ? "0" : rightDigits;

                    if (leftDigits.Length != rightDigits.Length)
                    {
                        return leftDigits.Length.CompareTo(rightDigits.Length);
                    }

                    var digitCompare = string.Compare(leftDigits, rightDigits, StringComparison.Ordinal);
                    if (digitCompare != 0)
                    {
                        return digitCompare;
                    }
                }
                else
                {
                    var charCompare = char.ToUpper(leftChar, CultureInfo.InvariantCulture)
                        .CompareTo(char.ToUpper(rightChar, CultureInfo.InvariantCulture));
                    if (charCompare != 0)
                    {
                        return charCompare;
                    }

                    leftIndex++;
                    rightIndex++;
                }
            }

            return left.Length.CompareTo(right.Length);
        }
    }
}
