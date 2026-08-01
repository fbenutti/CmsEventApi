using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CmsEventService.Tests.Integration;

public sealed class AuthenticationTests : IClassFixture<CmsEventServiceFactory>
{
    private readonly HttpClient _client;

    public AuthenticationTests(CmsEventServiceFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CmsEndpointRejectsMissingAuthentication()
    {
        // Arrange

        // Act
        var response = await _client.PostAsync(
            "/cms/events",
            new StringContent("[]", Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CmsEndpointRejectsApiCredentials()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, "/cms/events")
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = Basic("entityReader1", "8e221201-a1cd-4f57-89c7-04d517651625");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpointAcceptsValidReaderCredentials()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/entities");
        request.Headers.Authorization = Basic("entityReader1", "8e221201-a1cd-4f57-89c7-04d517651625");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static AuthenticationHeaderValue Basic(string username, string password)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return new AuthenticationHeaderValue("Basic", token);
    }
}
