using CmsEventService.Data;

namespace CmsEventService.Services;

public interface IEntityAdministrationService
{
    Task<bool> SetLocalDisabledAsync(string id, bool disabled, CancellationToken cancellationToken);
}

public sealed class EntityAdministrationService(CmsDbContext dbContext) : IEntityAdministrationService
{
    public async Task<bool> SetLocalDisabledAsync(string id, bool disabled, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Entities.FindAsync([id.Trim()], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.IsLocallyDisabled = disabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
