using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPharmacy.Shared.Models.Auth
{
	public class SignInModel
	{
		[Required(ErrorMessage = "Phone number or email is required")]
		public string Identifier { get; set; } = string.Empty;

		[Required(ErrorMessage = "Password is required")]
		[MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
		public string Password { get; set; } = string.Empty;

		public bool RememberMe { get; set; } = false;
	}

	public class SignUpModel
	{
		public string Role { get; set; } = "Patient";

		[Required(ErrorMessage = "First name is required")]
		[StringLength(50, MinimumLength = 2)]
		public string FirstName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Last name is required")]
		[StringLength(50, MinimumLength = 2)]
		public string LastName { get; set; } = string.Empty;

		[Required(ErrorMessage = "Phone number is required")]
		[RegularExpression(@"^[0-9]{9}$", ErrorMessage = "Enter valid SA phone number (9 digits after +27)")]
		[Phone]
		public string PhoneNumber { get; set; } = string.Empty;

		[Required(ErrorMessage = "Email is required")]
		[EmailAddress(ErrorMessage = "Invalid email address")]
		public string Email { get; set; } = string.Empty;

		[RegularExpression(@"^[0-9]{13}$", ErrorMessage = "Enter valid 13-digit SA ID number")]
		public string? IdNumber { get; set; }

		public string? ProfessionalRegistrationNumber { get; set; }
		public string? OrganizationOrBranch { get; set; }
		public string? AdminAccessKey { get; set; }

		[Required(ErrorMessage = "Password is required")]
		[MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
		[RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$",
			ErrorMessage = "Password must contain at least one letter and one number")]
		public string Password { get; set; } = string.Empty;

		[Required(ErrorMessage = "Please confirm your password")]
		[Compare("Password", ErrorMessage = "Passwords do not match")]
		public string ConfirmPassword { get; set; } = string.Empty;

		[Required(ErrorMessage = "You must accept the terms")]
		public bool AcceptTerms { get; set; } = false;

		public bool ReceiveUpdates { get; set; } = true;
	}

	public class AuthResult
	{
		public bool Success { get; set; }
		public string Token { get; set; } = string.Empty;
		public UserModel User { get; set; } = new();
		public string ErrorMessage { get; set; } = string.Empty;
		public string VerificationId { get; set; } = string.Empty;
	}
}
