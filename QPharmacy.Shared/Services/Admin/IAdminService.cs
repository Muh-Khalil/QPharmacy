using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Services
{
    public interface IAdminService
    {
        // --- Dashboard & KPIs ---
        Task<AdminKpiModel> GetDashboardKpisAsync();

        // --- Onboarding Pipeline ---
        Task<IEnumerable<OnboardingApplicantModel>> GetOnboardingApplicantsAsync(string? category = null);
        Task<bool> SubmitOnboardingApplicationAsync(string name, string category, string location = "Western Cape");
        Task<bool> ApproveOnboardingApplicantAsync(Guid id, string applicantName, string category);
        Task<bool> DeleteOnboardingApplicantAsync(Guid id, string applicantName);

        // --- Users & Permissions (RBAC) ---
        Task<IEnumerable<AdminUserModel>> GetUsersListAsync();
        Task<bool> UpdateUserRoleAndStatusAsync(string userId, string userName, string role, string status);
        Task<bool> InviteUserAsync(string name, string email, string role);

        // --- System Audit Logs ---
        Task<IEnumerable<AdminAuditLogModel>> GetAuditLogsAsync(string? category = null, string? searchTerm = null);
        Task<bool> LogAuditEventAsync(string eventType, string category, string actor, string details, string status = "Success");
        Task<string> ExportAuditLogsCsvAsync(string? category = null, string? searchTerm = null);

        // --- CMS Content Management ---
        Task<IEnumerable<AdminCmsModel>> GetCmsContentAsync(string? category = null, string? searchTerm = null);
        Task<bool> SaveCmsContentAsync(AdminCmsModel item);
    }
}
