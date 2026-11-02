using System.Security.Claims;

namespace QPharmacy.Shared.Service;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string Role { get; }
    string DisplayName { get; }
    string OrganizationOrBranch { get; }
    ClaimsPrincipal Principal { get; }
    Task RefreshAsync();
    bool IsInRole(string role);
}
