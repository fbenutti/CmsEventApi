using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CmsEventService.Authentication;

public sealed class BasicAuthenticationHandler(
    IOptionsMonitor<BasicAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<BasicAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var header) ||
            !string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Basic Authentication header."));
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Basic Authentication credentials encoding."));
        }

        var separatorIndex = decoded.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Basic Authentication credentials format."));
        }

        var username = decoded[..separatorIndex];
        var password = decoded[(separatorIndex + 1)..];
        var matchedUser = Options.Users.FirstOrDefault(user =>
            FixedTimeEquals(username, user.Username) &&
            FixedTimeEquals(password, user.Password));

        if (matchedUser is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid username or password."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, matchedUser.UserId),
            new(ClaimTypes.Name, username)
        };

        if (!string.IsNullOrWhiteSpace(matchedUser.Role))
        {
            claims.Add(new Claim(ClaimTypes.Role, matchedUser.Role));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"cms-event-service\"";
        return base.HandleChallengeAsync(properties);
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
