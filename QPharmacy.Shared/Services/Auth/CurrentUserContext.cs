using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace QPharmacy.Shared.Service;

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public CurrentUserContext(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public ClaimsPrincipal Principal { get; private set; } = new(new ClaimsIdentity());
    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public string Role => Principal.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    public string DisplayName => Principal.FindFirst(ClaimTypes.GivenName)?.Value
        ?? Principal.Identity?.Name
        ?? string.Empty;
    public string OrganizationOrBranch => Principal.FindFirst("organization")?.Value ?? string.Empty;

    public Guid UserId
    {
        get
        {
            var value = Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Principal.FindFirst("sub")?.Value;
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public bool IsInRole(string role) => Principal.IsInRole(role);

    public async Task RefreshAsync()
    {
        Principal = (await _authenticationStateProvider.GetAuthenticationStateAsync()).User;
    }
}
