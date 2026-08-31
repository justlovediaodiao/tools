namespace RawV.Models;

public sealed record ImageEntry(
    string FilePath,
    string FileName,
    string DirectoryPath,
    string BaseName,
    long FileSizeBytes,
    int? PixelWidth,
    int? PixelHeight,
    IReadOnlyList<string> AssociatedRawFiles);
