using System.Net;

namespace CmsEventService.Tests.Integration;

public sealed class HealthTests : IClassFixture<CmsEventServiceFactory>
{
    private readonly HttpClient _client;

    public HealthTests(CmsEventServiceFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpointIsPublicAndReturnsHealthy()
    {
        // Arrange

        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
    }
}
