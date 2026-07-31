using System.Text.Json;
using CmsEventService.Data;
using CmsEventService.Domain;
using CmsEventService.Events;
using Microsoft.EntityFrameworkCore;

namespace CmsEventService.Services;

public interface ICmsEventProcessor
{
    Task<CmsEventProcessingResult> ProcessAsync(IReadOnlyCollection<CmsEventDto> events, CancellationToken cancellationToken);
}

public sealed class CmsEventProcessor(
    CmsDbContext dbContext,
    ILogger<CmsEventProcessor> logger)
    : ICmsEventProcessor
{
    private const int MaxEntityIdLength = 128;
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<CmsEventProcessingResult> ProcessAsync(
        IReadOnlyCollection<CmsEventDto> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return new CmsEventProcessingResult(0, 0, []);
        }

        var accepted = 0;
        var failures = new List<CmsEventFailure>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var index = 0;
        foreach (var incomingEvent in events)
        {
            var processedAt = DateTimeOffset.UtcNow;
            var validation = Validate(incomingEvent);

            if (!validation.IsValid)
            {
                failures.Add(new CmsEventFailure(index, validation.Error));
                AddLog(incomingEvent, "Failed", validation.Error, processedAt);
                logger.LogWarning(
                    "Failed to process CMS event at batch index {Index}: {Reason}",
                    index,
                    validation.Error);
                index++;
                continue;
            }

            try
            {
                await ApplyEventAsync(validation.Event, processedAt, cancellationToken);
                AddLog(
                    incomingEvent,
                    "Processed",
                    $"Processed {validation.Event.Type} for entity {validation.Event.Id}.",
                    processedAt);
                logger.LogInformation(
                    "Processed CMS event {EventType} for entity {EntityId} at version {Version}.",
                    validation.Event.Type,
                    validation.Event.Id,
                    validation.Event.Version);
                accepted++;
            }
            catch (Exception ex)
            {
                failures.Add(new CmsEventFailure(index, ex.Message));
                AddLog(incomingEvent, "Failed", ex.Message, processedAt);
                logger.LogError(
                    ex,
                    "Failed to process CMS event {EventType} for entity {EntityId}.",
                    incomingEvent.Type,
                    incomingEvent.Id);
            }

            index++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CmsEventProcessingResult(accepted, failures.Count, failures);
    }

    private async Task ApplyEventAsync(ValidatedCmsEvent cmsEvent, DateTimeOffset processedAt, CancellationToken cancellationToken)
    {
        if (cmsEvent.Type == CmsEventType.Delete)
        {
            var trackedEntity = dbContext.Entities.Local.FirstOrDefault(entity => entity.Id == cmsEvent.Id);
            if (trackedEntity is not null)
            {
                dbContext.Entities.Remove(trackedEntity);
            }

            var deletedRows = await dbContext.Entities
                .Where(entity => entity.Id == cmsEvent.Id)
                .ExecuteDeleteAsync(cancellationToken);

            logger.LogInformation(
                "Hard-deleted {DeletedRows} row(s) for CMS entity {EntityId}.",
                deletedRows,
                cmsEvent.Id);

            return;
        }

        var existing = await dbContext.Entities.FindAsync([cmsEvent.Id], cancellationToken);
        var shouldApplyVersion = existing is null || cmsEvent.Version!.Value >= existing.LatestVersion;

        if (!shouldApplyVersion)
        {
            logger.LogInformation(
                "Ignored stale CMS event {EventType} for entity {EntityId}: event version {EventVersion}, stored version {StoredVersion}.",
                cmsEvent.Type,
                cmsEvent.Id,
                cmsEvent.Version,
                existing!.LatestVersion);
            return;
        }

        var isPublished = cmsEvent.Type == CmsEventType.Publish;

        if (existing is null)
        {
            dbContext.Entities.Add(new CmsEntity
            {
                Id = cmsEvent.Id,
                PayloadJson = cmsEvent.PayloadJson!,
                LatestVersion = cmsEvent.Version!.Value,
                IsCmsPublished = isPublished,
                IsLocallyDisabled = false,
                LastEventType = cmsEvent.Type.ToString(),
                LastEventTimestamp = cmsEvent.Timestamp,
                UpdatedAt = processedAt
            });
            return;
        }

        existing.PayloadJson = cmsEvent.PayloadJson!;
        existing.LatestVersion = cmsEvent.Version!.Value;
        existing.IsCmsPublished = isPublished;
        existing.LastEventType = cmsEvent.Type.ToString();
        existing.LastEventTimestamp = cmsEvent.Timestamp;
        existing.UpdatedAt = processedAt;
    }

    private static ValidatedCmsEventResult Validate(CmsEventDto incomingEvent)
    {
        if (string.IsNullOrWhiteSpace(incomingEvent.Id))
        {
            return ValidatedCmsEventResult.Invalid("Event id is required.");
        }

        var id = incomingEvent.Id.Trim();
        if (id.Length > MaxEntityIdLength)
        {
            return ValidatedCmsEventResult.Invalid($"Event id must be {MaxEntityIdLength} characters or fewer.");
        }

        if (incomingEvent.Timestamp is null)
        {
            return ValidatedCmsEventResult.Invalid("Event timestamp is required.");
        }

        if (!TryParseType(incomingEvent.Type, out var type))
        {
            return ValidatedCmsEventResult.Invalid("Event type must be publish, delete, or unPublish.");
        }

        if (type == CmsEventType.Delete)
        {
            return ValidatedCmsEventResult.Valid(new ValidatedCmsEvent(
                type,
                id,
                null,
                null,
                incomingEvent.Timestamp.Value));
        }

        if (incomingEvent.Version is null or < 1)
        {
            return ValidatedCmsEventResult.Invalid("Publish and unPublish events require a positive version.");
        }

        if (incomingEvent.Payload is null || incomingEvent.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            return ValidatedCmsEventResult.Invalid("Publish and unPublish events require an object payload.");
        }

        var payloadJson = JsonSerializer.Serialize(incomingEvent.Payload.Value, PayloadSerializerOptions);

        return ValidatedCmsEventResult.Valid(new ValidatedCmsEvent(
            type,
            id,
            payloadJson,
            incomingEvent.Version,
            incomingEvent.Timestamp.Value));
    }

    private void AddLog(CmsEventDto incomingEvent, string status, string message, DateTimeOffset processedAt)
    {
        dbContext.EventLogs.Add(new CmsEventLog
        {
            EntityId = incomingEvent.Id?.Trim(),
            Type = incomingEvent.Type ?? "unknown",
            Version = incomingEvent.Version,
            Timestamp = incomingEvent.Timestamp ?? processedAt,
            Status = status,
            Message = message,
            ProcessedAt = processedAt
        });
    }

    private static bool TryParseType(string? type, out CmsEventType eventType)
    {
        eventType = default;

        if (string.Equals(type, "publish", StringComparison.OrdinalIgnoreCase))
        {
            eventType = CmsEventType.Publish;
            return true;
        }

        if (string.Equals(type, "delete", StringComparison.OrdinalIgnoreCase))
        {
            eventType = CmsEventType.Delete;
            return true;
        }

        if (string.Equals(type, "unPublish", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "unpublish", StringComparison.OrdinalIgnoreCase))
        {
            eventType = CmsEventType.Unpublish;
            return true;
        }

        return false;
    }

    private sealed record ValidatedCmsEvent(
        CmsEventType Type,
        string Id,
        string? PayloadJson,
        int? Version,
        DateTimeOffset Timestamp);

    private sealed record ValidatedCmsEventResult(bool IsValid, ValidatedCmsEvent Event, string Error)
    {
        public static ValidatedCmsEventResult Valid(ValidatedCmsEvent cmsEvent) => new(true, cmsEvent, string.Empty);

        public static ValidatedCmsEventResult Invalid(string error) => new(false, default!, error);
    }
}
