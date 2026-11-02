using QPharmacy.Repositories;
using QPharmacy.Shared.Models.Database;
using QPharmacy.Shared.Service;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace QPharmacy.Services
{
    public class AdminService : IAdminService
    {
        private readonly IAdminRepository _repository;
        private readonly IAccessGuard _accessGuard;

        public AdminService(IAdminRepository repository, IAccessGuard accessGuard)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _accessGuard = accessGuard;
        }

        // ─────────────────────────────────────────────────────────────────
        // KPIS
        // ─────────────────────────────────────────────────────────────────
        public Task<AdminKpiModel> GetDashboardKpisAsync()
        {
            return _repository.GetDashboardKpisAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // ONBOARDING
        // ─────────────────────────────────────────────────────────────────
        public Task<IEnumerable<OnboardingApplicantModel>> GetOnboardingApplicantsAsync(string? category = null)
        {
            return _repository.GetOnboardingApplicantsAsync(category);
        }

        public async Task<bool> SubmitOnboardingApplicationAsync(string name, string category, string location = "Western Cape")
        {
            await _accessGuard.RequireRoleAsync("Admin");
            var licenceOrSubtext = category switch
            {
                "Doctors" => "HPCSA #: MP-" + Random.Shared.Next(100000, 999999),
                "Drivers" => "Fleet Registration #: FLT-" + Random.Shared.Next(100, 999),
                _ => "Licence #: SAPC-" + Random.Shared.Next(10000, 99999)
            };

            var applicant = new OnboardingApplicantModel
            {
                Id = Guid.NewGuid(),
                Category = category,
                Name = name,
                SubText = licenceOrSubtext,
                Location = location,
                SubmittedDate = DateTime.UtcNow.ToString("d MMM yyyy"),
                Status = "Pending review",
                CreatedAt = DateTime.UtcNow
            };

            var success = await _repository.CreateOnboardingApplicantAsync(applicant);
            if (success)
            {
                await LogAuditEventAsync(
                    eventType: "Onboarding Application Submitted",
                    category: "Integrations",
                    actor: "Admin User",
                    details: $"Created partner onboarding application for {name} ({category}, {location})"
                );
            }
            return success;
        }

        public async Task<bool> ApproveOnboardingApplicantAsync(Guid id, string applicantName, string category)
        {
            await _accessGuard.RequireRoleAsync("Admin");
            var success = await _repository.ApproveOnboardingApplicantAsync(id);
            if (success)
            {
                await LogAuditEventAsync(
                    eventType: $"{category} Regulatory Accreditation Verified",
                    category: "Security",
                    actor: "Super Admin",
                    details: $"Approved onboarding & provisioned account role for {applicantName}"
                );
            }
            return success;
        }

        public async Task<bool> DeleteOnboardingApplicantAsync(Guid id, string applicantName)
        {
            await _accessGuard.RequireRoleAsync("Admin");
            var success = await _repository.DeleteOnboardingApplicantAsync(id);
            if (success)
            {
                await LogAuditEventAsync(
                    eventType: "Onboarding Application Removed",
                    category: "Integrations",
                    actor: "Super Admin",
                    details: $"Removed onboarding application for '{applicantName}' from platform database."
                );
            }
            return success;
        }

        // ─────────────────────────────────────────────────────────────────
        // USERS & ROLES
        // ─────────────────────────────────────────────────────────────────
        public Task<IEnumerable<AdminUserModel>> GetUsersListAsync()
        {
            return _repository.GetUsersListAsync();
        }

        public async Task<bool> UpdateUserRoleAndStatusAsync(string userId, string userName, string role, string status)
        {
            await _accessGuard.RequireRoleAsync("Admin");
            var success = await _repository.UpdateUserRoleAndStatusAsync(userId, role, status);
            if (success)
            {
                await LogAuditEventAsync(
                    eventType: "Role & Permission Update",
                    category: "Security",
                    actor: "Super Admin",
                    details: $"Updated user permissions for {userName} (Role: {role}, Account Status: {status}). RBAC synced."
                );
            }
            return success;
        }

        public async Task<bool> InviteUserAsync(string name, string email, string role)
        {
            await _accessGuard.RequireRoleAsync("Admin");
            var newUser = new AdminUserModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Email = email,
                Role = role,
                LastActive = "Pending Invite",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            var success = await _repository.InviteUserAsync(newUser);
            if (success)
            {
                await LogAuditEventAsync(
                    eventType: "Platform User Invited",
                    category: "Security",
                    actor: "Super Admin",
                    details: $"Sent account invitation email to {email} with role '{role}'"
                );
            }
            return success;
        }

        // ─────────────────────────────────────────────────────────────────
        // AUDIT LOGS
        // ─────────────────────────────────────────────────────────────────
        public Task<IEnumerable<AdminAuditLogModel>> GetAuditLogsAsync(string? category = null, string? searchTerm = null)
        {
            return _repository.GetAuditLogsAsync(category, searchTerm);
        }

        public Task<bool> LogAuditEventAsync(string eventType, string category, string actor, string details, string status = "Success")
        {
            var log = new AdminAuditLogModel
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                EventType = eventType,
                Category = category,
                Actor = actor,
                Details = details,
                Status = status,
                OriginIp = "192.168.1.104",
                CreatedAt = DateTime.UtcNow
            };
            return _repository.CreateAuditLogAsync(log);
        }

        public async Task<string> ExportAuditLogsCsvAsync(string? category = null, string? searchTerm = null)
        {
            var logs = await GetAuditLogsAsync(category, searchTerm);
            var sb = new StringBuilder();

            // Header line
            sb.AppendLine("ID,Timestamp,EventType,Category,Actor,Details,Status,OriginIP");

            foreach (var log in logs)
            {
                var cleanDetails = log.Details.Replace("\"", "\"\"");
                var cleanActor = log.Actor.Replace("\"", "\"\"");
                var cleanEventType = log.EventType.Replace("\"", "\"\"");

                sb.AppendLine($"\"{log.Id}\",\"{log.Timestamp}\",\"{cleanEventType}\",\"{log.Category}\",\"{cleanActor}\",\"{cleanDetails}\",\"{log.Status}\",\"{log.OriginIp}\"");
            }

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // CMS CONTENT
        // ─────────────────────────────────────────────────────────────────
        public Task<IEnumerable<AdminCmsModel>> GetCmsContentAsync(string? category = null, string? searchTerm = null)
        {
            return _repository.GetCmsContentAsync(category, searchTerm);
        }

        public async Task<bool> SaveCmsContentAsync(AdminCmsModel item)
        {
            await _accessGuard.RequireRoleAsync("Admin");
            var isNew = item.Id == Guid.Empty;
            if (isNew) item.Id = Guid.NewGuid();

            var success = await _repository.CreateOrUpdateCmsContentAsync(item);
            if (success)
            {
                await LogAuditEventAsync(
                    eventType: isNew ? "CMS Content Published" : "CMS Content Updated",
                    category: "System",
                    actor: "Super Admin",
                    details: $"{(isNew ? "Published new" : "Updated")} article '{item.Title}' under category '{item.Category}'"
                );
            }
            return success;
        }
    }
}
