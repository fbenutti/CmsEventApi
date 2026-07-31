using Microsoft.AspNetCore.Authentication;

namespace CmsEventService.Authentication;

public sealed class BasicAuthenticationOptions : AuthenticationSchemeOptions
{
    public List<BasicAuthenticationUser> Users { get; set; } = [];
}

public sealed class BasicAuthenticationUser
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
