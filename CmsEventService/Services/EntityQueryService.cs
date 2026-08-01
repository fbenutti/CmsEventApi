using System.Text.Json;
using CmsEventService.Data;
using CmsEventService.Events;
using Microsoft.EntityFrameworkCore;

namespace CmsEventService.Services;

public interface IEntityQueryService
{
    Task<PagedResponse<EntityResponse>> ListAsync(
        bool includeDisabled,
        EntityQueryParameters parameters,
        CancellationToken cancellationToken);
}

public sealed class EntityQueryService(IDbContextFactory<CmsDbContext> readerContextFactory) : IEntityQueryService
{
    public async Task<PagedResponse<EntityResponse>> ListAsync(
        bool includeDisabled,
        EntityQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await readerContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.Entities
            .AsNoTracking()
            .Where(entity => includeDisabled || (entity.IsCmsPublished && !entity.IsLocallyDisabled));

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)parameters.PageSize);

        var entities = await query
            .OrderBy(entity => entity.Id)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
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

        var items = entities
            .Select(entity => new EntityResponse(
                entity.Id,
                entity.LatestVersion,
                entity.IsCmsPublished,
                entity.IsLocallyDisabled,
                entity.LastEventType,
                entity.LastEventTimestamp,
                JsonDocument.Parse(entity.PayloadJson).RootElement.Clone()))
            .ToList();

        return new PagedResponse<EntityResponse>(
            items,
            parameters.Page,
            parameters.PageSize,
            totalItems,
            totalPages);
    }
}
