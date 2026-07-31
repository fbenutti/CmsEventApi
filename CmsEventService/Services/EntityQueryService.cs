using System.Text.Json;
using CmsEventService.Data;
using CmsEventService.Events;
using Microsoft.EntityFrameworkCore;

namespace CmsEventService.Services;

public interface IEntityQueryService
{
    Task<IReadOnlyCollection<EntityResponse>> ListAsync(bool includeDisabled, CancellationToken cancellationToken);
}

public sealed class EntityQueryService(IDbContextFactory<CmsDbContext> readerContextFactory) : IEntityQueryService
{
    public async Task<IReadOnlyCollection<EntityResponse>> ListAsync(bool includeDisabled, CancellationToken cancellationToken)
    {
        await using var dbContext = await readerContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await dbContext.Entities
            .AsNoTracking()
            .Where(entity => includeDisabled || (entity.IsCmsPublished && !entity.IsLocallyDisabled))
            .OrderBy(entity => entity.Id)
            .Select(entity => new
            {
                entity.Id,
                entity.LatestVersion,
                entity.IsCmsPublished,
                entity.IsLocallyDisabled,
                entity.LastEventType,
                entity.LastEventTimestamp,
                entity.PayloadJson
            })
            .ToListAsync(cancellationToken);

        return entities
            .Select(entity => new EntityResponse(
                entity.Id,
                entity.LatestVersion,
                entity.IsCmsPublished,
                entity.IsLocallyDisabled,
                entity.LastEventType,
                entity.LastEventTimestamp,
                JsonDocument.Parse(entity.PayloadJson).RootElement.Clone()))
            .ToList();
    }
}
