namespace CmsEventService.Domain;

public sealed class CmsEventLog
{
    public long Id { get; set; }

    public string? EntityId { get; set; }

    public required string Type { get; set; }

    public int? Version { get; set; }

    public string? Fingerprint { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public required string Status { get; set; }

    public required string Message { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
