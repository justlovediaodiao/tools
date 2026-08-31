namespace RawV.Models;

public sealed record FmtpEvent(
    string Type,
    string? Message = null,
    int Current = 0,
    int Total = 0,
    string? File = null);
