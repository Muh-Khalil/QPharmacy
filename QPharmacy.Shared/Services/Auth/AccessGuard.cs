namespace QPharmacy.Shared.Service;

public sealed class AccessGuard : IAccessGuard
{
    private readonly ICurrentUserContext _currentUser;

    public AccessGuard(ICurrentUserContext currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task RequireRoleAsync(string role)
    {
        await _currentUser.RefreshAsync();
        if (!_currentUser.IsAuthenticated || !_currentUser.IsInRole(role))
            throw new UnauthorizedAccessException($"The '{role}' role is required for this operation.");
    }

    public async Task RequireAnyRoleAsync(params string[] roles)
    {
        await _currentUser.RefreshAsync();
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Authentication is required.");

        if (roles.Length > 0 && !System.Linq.Enumerable.Any(roles, r => _currentUser.IsInRole(r)))
            throw new UnauthorizedAccessException($"One of the following roles is required: {string.Join(", ", roles)}");
    }

    public async Task RequireSelfAsync(Guid resourceUserId, string role = "Patient")
    {
        await RequireRoleAsync(role);
        if (resourceUserId == Guid.Empty || _currentUser.UserId != resourceUserId)
            throw new UnauthorizedAccessException("You cannot access or modify another user's resource.");
    }
}
