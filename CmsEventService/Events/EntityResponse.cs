using System.Text.Json;

namespace CmsEventService.Events;

public sealed record EntityResponse(
    string Id,
    int LatestVersion,
    bool IsCmsPublished,
    bool IsLocallyDisabled,
    string LastEventType,
    DateTimeOffset LastEventTimestamp,
    JsonElement Payload);
