using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QPharmacy.Repositories
{
    public class MockAdminRepository : IAdminRepository
    {
        private readonly List<OnboardingApplicantModel> _applicants = new()
        {
            new OnboardingApplicantModel { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Category = "Pharmacies", Name = "Medirite, Bellville", SubText = "Licence #: SAPC-94102", Location = "Western Cape", SubmittedDate = "5 Jul 2026", Status = "Pending review" },
            new OnboardingApplicantModel { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Category = "Pharmacies", Name = "Clicks, Wynberg", SubText = "Licence #: SAPC-88124", Location = "Western Cape", SubmittedDate = "28 Jun 2026", Status = "Approved" },
            new OnboardingApplicantModel { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Category = "Doctors", Name = "Dr. K. Naidoo", SubText = "HPCSA #: MP-074129", Location = "KwaZulu-Natal", SubmittedDate = "6 Jul 2026", Status = "Pending review" },
            new OnboardingApplicantModel { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Category = "Doctors", Name = "Dr. S. Patel", SubText = "HPCSA #: MP-061042", Location = "Gauteng", SubmittedDate = "20 Jun 2026", Status = "Approved" },
            new OnboardingApplicantModel { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Category = "Drivers", Name = "Fastway Express Courier", SubText = "Fleet Registration #: FLT-481", Location = "Western Cape", SubmittedDate = "1 Jul 2026", Status = "Approved" },
            new OnboardingApplicantModel { Id = Guid.Parse("66666666-6666-6666-6666-666666666666"), Category = "Drivers", Name = "The Courier Guy (Claremont)", SubText = "Fleet Registration #: FLT-902", Location = "Western Cape", SubmittedDate = "4 Jul 2026", Status = "Pending review" }
        };

        private readonly List<AdminUserModel> _users = new()
        {
            new AdminUserModel { Id = "1", Name = "Thandiwe Mokoena", Email = "thandiwe.mokoena@email.co.za", Role = "Patient", LastActive = "Today", Status = "Active" },
            new AdminUserModel { Id = "2", Name = "Dr. N. Adams", Email = "dr.adams@medical.co.za", Role = "Doctor", LastActive = "Today", Status = "Active" },
            new AdminUserModel { Id = "3", Name = "S. Dlamini", Email = "s.dlamini@clicks.co.za", Role = "Pharmacist", LastActive = "Yesterday", Status = "Active" },
            new AdminUserModel { Id = "4", Name = "Jabu Khumalo", Email = "jabu.khumalo@email.co.za", Role = "Patient", LastActive = "3 days ago", Status = "Active" },
            new AdminUserModel { Id = "5", Name = "Fastway Driver", Email = "driver@fastway.co.za", Role = "Driver", LastActive = "Today", Status = "Active" }
        };

        private readonly List<AdminAuditLogModel> _logs = new()
        {
            new AdminAuditLogModel { Id = Guid.NewGuid(), Timestamp = "2026-07-30 22:48:10", EventType = "SAPC Licence Verification", Category = "Security", Actor = "Admin User", Details = "Verified SAPC-94102 for Medirite Bellville", Status = "Success" },
            new AdminAuditLogModel { Id = Guid.NewGuid(), Timestamp = "2026-07-30 21:12:05", EventType = "Payment Gateway Sync", Category = "Integrations", Actor = "Paystack Webhook", Details = "Processed payout settlement R 42,800.00", Status = "Success" },
            new AdminAuditLogModel { Id = Guid.NewGuid(), Timestamp = "2026-07-30 19:30:42", EventType = "Role Permission Update", Category = "Security", Actor = "Admin User", Details = "Promoted S. Dlamini to Branch Manager role", Status = "Success" },
            new AdminAuditLogModel { Id = Guid.NewGuid(), Timestamp = "2026-07-30 18:05:11", EventType = "Medical Aid API Claim", Category = "Integrations", Actor = "Discovery Health API", Details = "Dispatched claim approval pre-auth #884912", Status = "Success" }
        };

        private readonly List<AdminCmsModel> _cmsList = new()
        {
            new AdminCmsModel { Id = Guid.NewGuid(), Title = "Managing Chronic Hypertension with Amlodipine", Category = "Health Guide", PublishedDate = "12 Jul 2026", Status = "Published" },
            new AdminCmsModel { Id = Guid.NewGuid(), Title = "Winter Flu Vaccination Campaign 2026", Category = "Promotional Banner", PublishedDate = "1 Jul 2026", Status = "Published" },
            new AdminCmsModel { Id = Guid.NewGuid(), Title = "Understanding Prescription Refills & Delivery", Category = "Health Guide", PublishedDate = "20 Jun 2026", Status = "Published" }
        };

        public Task<AdminKpiModel> GetDashboardKpisAsync()
        {
            return Task.FromResult(new AdminKpiModel
            {
                ActivePatientsCount = 18420,
                PartnerPharmaciesCount = _applicants.Count(a => a.Category == "Pharmacies" && a.Status == "Approved") + 210,
                DoctorsCount = _applicants.Count(a => a.Category == "Doctors" && a.Status == "Approved") + 62,
                PlatformRevenueMtd = 2400000.00m
            });
        }

        public Task<IEnumerable<OnboardingApplicantModel>> GetOnboardingApplicantsAsync(string? category = null)
        {
            var res = _applicants.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(category))
            {
                res = res.Where(a => a.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }
            return Task.FromResult(res);
        }

        public Task<bool> CreateOnboardingApplicantAsync(OnboardingApplicantModel applicant)
        {
            _applicants.Insert(0, applicant);
            return Task.FromResult(true);
        }

        public Task<bool> ApproveOnboardingApplicantAsync(Guid id)
        {
            var item = _applicants.FirstOrDefault(a => a.Id == id);
            if (item != null)
            {
                item.Status = "Approved";
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> DeleteOnboardingApplicantAsync(Guid id)
        {
            var item = _applicants.FirstOrDefault(a => a.Id == id);
            if (item != null)
            {
                _applicants.Remove(item);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<IEnumerable<AdminUserModel>> GetUsersListAsync()
        {
            return Task.FromResult(_users.AsEnumerable());
        }

        public Task<bool> UpdateUserRoleAndStatusAsync(string userId, string role, string status)
        {
            var usr = _users.FirstOrDefault(u => u.Id == userId || u.Email.Equals(userId, StringComparison.OrdinalIgnoreCase));
            if (usr != null)
            {
                usr.Role = role;
                usr.Status = status;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> InviteUserAsync(AdminUserModel newUser)
        {
            _users.Insert(0, newUser);
            return Task.FromResult(true);
        }

        public Task<IEnumerable<AdminAuditLogModel>> GetAuditLogsAsync(string? category = null, string? searchTerm = null)
        {
            var res = _logs.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                res = res.Where(l => l.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                res = res.Where(l => l.EventType.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                     l.Actor.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                     l.Details.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
            }
            return Task.FromResult(res);
        }

        public Task<bool> CreateAuditLogAsync(AdminAuditLogModel log)
        {
            _logs.Insert(0, log);
            return Task.FromResult(true);
        }

        public Task<IEnumerable<AdminCmsModel>> GetCmsContentAsync(string? category = null, string? searchTerm = null)
        {
            var res = _cmsList.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                res = res.Where(c => c.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                res = res.Where(c => c.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
            }
            return Task.FromResult(res);
        }

        public Task<bool> CreateOrUpdateCmsContentAsync(AdminCmsModel item)
        {
            var existing = _cmsList.FirstOrDefault(c => c.Id == item.Id);
            if (existing != null)
            {
                existing.Title = item.Title;
                existing.Category = item.Category;
                existing.Status = item.Status;
                existing.ContentBody = item.ContentBody;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _cmsList.Insert(0, item);
            }
            return Task.FromResult(true);
        }
    }
}
