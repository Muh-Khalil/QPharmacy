namespace QPharmacy.Shared.Service;

public interface IAccessGuard
{
    Task RequireRoleAsync(string role);
    Task RequireAnyRoleAsync(params string[] roles);
    Task RequireSelfAsync(Guid resourceUserId, string role = "Patient");
}
