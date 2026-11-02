using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPharmacy.Shared.Models.Database
{
	public class UserModel
	{
		public Guid Id { get; set; }
		public string FirstName { get; set; } = string.Empty;
		public string LastName { get; set; } = string.Empty;
		public string PhoneNumber { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string? IdNumber { get; set; }
		public string PasswordHash { get; set; } = string.Empty;
		public string PasswordSalt { get; set; } = string.Empty;
		public bool IsVerified { get; set; }
		public bool IsDriver { get; set; }
		public bool IsAdmin { get; set; }
		public bool IsDoctor { get; set; }
		public bool IsPharmacist { get; set; }
		public string ProfessionalRegistrationNumber { get; set; } = string.Empty;
		public string OrganizationOrBranch { get; set; } = string.Empty;
		public bool IsActive { get; set; }
		public DateTime? LastLoginDate { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
	}
}
