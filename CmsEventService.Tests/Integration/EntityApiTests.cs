using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CmsEventService.Tests.Integration;

public sealed class EntityApiTests : IDisposable
{
    private readonly CmsEventServiceFactory _factory;
    private readonly HttpClient _client;

    public EntityApiTests()
    {
        _factory = new CmsEventServiceFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task NormalUsersOnlySeePublishedAndEnabledEntitiesWhileAdminsSeeDisabledEntities()
    {
        await IngestAsync("""
            [
              { "type": "publish", "id": "published", "payload": { "title": "Visible" }, "version": 1, "timestamp": "2024-01-01T00:00:00Z" },
              { "type": "unPublish", "id": "drafted", "payload": { "title": "Hidden latest" }, "version": 2, "timestamp": "2024-01-02T00:00:00Z" }
            ]
            """);

        var readerEntities = await GetEntitiesAsync("entityReader1", "8e221201-a1cd-4f57-89c7-04d517651625");
        var adminEntities = await GetEntitiesAsync("entityAdmin01", "4f21956d-918a-4199-9787-e4bf9956363c");

        Assert.Single(readerEntities);
        Assert.Equal("published", readerEntities[0].GetProperty("id").GetString());
        Assert.Equal(2, adminEntities.Count);
        Assert.Contains(adminEntities, entity =>
            entity.GetProperty("id").GetString() == "drafted" &&
            entity.GetProperty("latestVersion").GetInt32() == 2 &&
            entity.GetProperty("isCmsPublished").GetBoolean() == false);
    }

    [Fact]
    public async Task AdminCanLocallyDisableEntityWithoutDeletingCmsData()
    {
        await IngestAsync("""
            [
              { "type": "publish", "id": "local-disable", "payload": { "title": "Toggle me" }, "version": 1, "timestamp": "2024-01-01T00:00:00Z" }
            ]
            """);

        using var disableRequest = new HttpRequestMessage(HttpMethod.Patch, "/entities/local-disable/disabled")
        {
            Content = JsonContent.Create(new { disabled = true })
        };
        disableRequest.Headers.Authorization = Basic("entityAdmin01", "4f21956d-918a-4199-9787-e4bf9956363c");

        var disableResponse = await _client.SendAsync(disableRequest);
        var readerEntities = await GetEntitiesAsync("entityReader1", "8e221201-a1cd-4f57-89c7-04d517651625");
        var adminEntities = await GetEntitiesAsync("entityAdmin01", "4f21956d-918a-4199-9787-e4bf9956363c");

        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);
        Assert.DoesNotContain(readerEntities, entity => entity.GetProperty("id").GetString() == "local-disable");
        Assert.Contains(adminEntities, entity =>
            entity.GetProperty("id").GetString() == "local-disable" &&
            entity.GetProperty("isLocallyDisabled").GetBoolean());
    }

    private async Task IngestAsync(string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/cms/events")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = Basic("cmsPipeline01", "3e8dc83f-4b8f-48c7-a2d4-44f89f476b65");

        var response = await _client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<List<JsonElement>> GetEntitiesAsync(string username, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/entities");
        request.Headers.Authorization = Basic(username, password);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.EnumerateArray().Select(entity => entity.Clone()).ToList();
    }

    private static AuthenticationHeaderValue Basic(string username, string password)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return new AuthenticationHeaderValue("Basic", token);
    }
}
