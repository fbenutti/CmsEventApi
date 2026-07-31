using System.Text.Json;
using CmsEventService.Data;
using CmsEventService.Events;
using CmsEventService.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CmsEventService.Tests.Processing;

public sealed class CmsEventProcessorTests
{
    [Fact]
    public async Task UnpublishStoresLatestPayloadAndMarksEntityAsNotCmsPublished()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var processor = new CmsEventProcessor(dbContext, NullLogger<CmsEventProcessor>.Instance);

        var result = await processor.ProcessAsync(
        [
            Event("publish", "article-1", 1, """{ "title": "Published" }"""),
            Event("unPublish", "article-1", 2, """{ "title": "Unpublished latest" }""")
        ], CancellationToken.None);

        var entity = await dbContext.Entities.SingleAsync();

        Assert.Equal(2, result.Accepted);
        Assert.Equal(0, result.Failed);
        Assert.Equal(2, entity.LatestVersion);
        Assert.False(entity.IsCmsPublished);
        Assert.Contains("Unpublished latest", entity.PayloadJson);
    }

    [Fact]
    public async Task DeleteHardDeletesExistingEntity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var processor = new CmsEventProcessor(dbContext, NullLogger<CmsEventProcessor>.Instance);

        await processor.ProcessAsync(
        [
            Event("publish", "article-1", 1, """{ "title": "Published" }"""),
            new CmsEventDto { Type = "delete", Id = "article-1", Timestamp = DateTimeOffset.Parse("2024-01-02T00:00:00Z") }
        ], CancellationToken.None);

        Assert.Empty(await dbContext.Entities.ToListAsync());
    }

    [Fact]
    public async Task InvalidEventsAreLoggedAndDoNotCreateEntities()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var processor = new CmsEventProcessor(dbContext, NullLogger<CmsEventProcessor>.Instance);

        var result = await processor.ProcessAsync(
        [
            new CmsEventDto { Type = "publish", Id = "broken", Timestamp = DateTimeOffset.Parse("2024-01-01T00:00:00Z") }
        ], CancellationToken.None);

        Assert.Equal(0, result.Accepted);
        Assert.Equal(1, result.Failed);
        Assert.Empty(await dbContext.Entities.ToListAsync());
        Assert.Single(await dbContext.EventLogs.Where(log => log.Status == "Failed").ToListAsync());
    }

    private static CmsDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<CmsDbContext>()
            .UseSqlite(connection)
            .Options;

        return new CmsDbContext(options);
    }

    private static CmsEventDto Event(string type, string id, int version, string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);

        return new CmsEventDto
        {
            Type = type,
            Id = id,
            Version = version,
            Payload = document.RootElement.Clone(),
            Timestamp = DateTimeOffset.Parse("2024-01-01T00:00:00Z").AddDays(version - 1)
        };
    }
}
