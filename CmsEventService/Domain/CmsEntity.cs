namespace CmsEventService.Domain;

public sealed class CmsEntity
{
    public required string Id { get; set; }

    public required string PayloadJson { get; set; }

    public int LatestVersion { get; set; }

    public bool IsCmsPublished { get; set; }

    public bool IsLocallyDisabled { get; set; }

    public required string LastEventType { get; set; }

    public DateTimeOffset LastEventTimestamp { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
