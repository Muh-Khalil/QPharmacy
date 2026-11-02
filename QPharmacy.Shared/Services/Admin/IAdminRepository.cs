using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Repositories
{
    public interface IAdminRepository
    {
        // --- Dashboard / KPIs ---
        Task<AdminKpiModel> GetDashboardKpisAsync();

        // --- Onboarding Pipeline ---
        Task<IEnumerable<OnboardingApplicantModel>> GetOnboardingApplicantsAsync(string? category = null);
        Task<bool> CreateOnboardingApplicantAsync(OnboardingApplicantModel applicant);
        Task<bool> ApproveOnboardingApplicantAsync(Guid id);
        Task<bool> DeleteOnboardingApplicantAsync(Guid id);

        // --- User Management & Roles ---
        Task<IEnumerable<AdminUserModel>> GetUsersListAsync();
        Task<bool> UpdateUserRoleAndStatusAsync(string userId, string role, string status);
        Task<bool> InviteUserAsync(AdminUserModel newUser);

        // --- Audit & Activity Logs ---
        Task<IEnumerable<AdminAuditLogModel>> GetAuditLogsAsync(string? category = null, string? searchTerm = null);
        Task<bool> CreateAuditLogAsync(AdminAuditLogModel log);

        // --- CMS Content Management ---
        Task<IEnumerable<AdminCmsModel>> GetCmsContentAsync(string? category = null, string? searchTerm = null);
        Task<bool> CreateOrUpdateCmsContentAsync(AdminCmsModel item);
    }
}
