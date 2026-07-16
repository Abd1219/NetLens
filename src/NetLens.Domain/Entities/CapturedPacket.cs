namespace NetLens.Domain.Entities;

/// <summary>
/// Represents lightweight metadata of a captured network packet.
/// </summary>
public sealed record CapturedPacket(
    DateTimeOffset Timestamp,
    string SourceIp,
    string DestinationIp,
    string Protocol,
    int SizeBytes,
    string? Flags);
