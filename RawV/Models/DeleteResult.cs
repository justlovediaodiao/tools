namespace RawV.Models;

public sealed record DeleteResult(
    bool Success,
    string? ErrorMessage,
    int? NextIndex,
    IReadOnlyList<string> DeletedPaths,
    bool CurrentImageDeleted);
