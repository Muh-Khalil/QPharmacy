using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QPharmacy.Repositories;
using QPharmacy.Services;
using QPharmacy.Shared.Service;
using System.Data;
using System.Text;

namespace QPharmacy
{
	public static class MauiProgram
	{
#if WINDOWS
		[System.Runtime.InteropServices.DllImport("kernel32.dll")]
		private static extern bool AttachConsole(int dwProcessId);
		private const int ATTACH_PARENT_PROCESS = -1;
#endif

		public static MauiApp CreateMauiApp()
		{
#if WINDOWS
			AttachConsole(ATTACH_PARENT_PROCESS);
#endif
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				});

			// Configure database and JWT settings to match the Web project
			builder.Configuration["UseMockDatabase"] = "false";
			builder.Configuration["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=QPharmacy;Trusted_Connection=True;TrustServerCertificate=True;";
			builder.Configuration["Jwt:Issuer"] = "QPharmacy";
			builder.Configuration["Jwt:Audience"] = "QPharmacyUsers";
			builder.Configuration["Jwt:Key"] = "Your-Super-Secret-Key-At-Least-32-Characters-Long-For-JWT";

			builder.Services.AddMauiBlazorWebView();

#if DEBUG
			builder.Services.AddBlazorWebViewDeveloperTools();
			builder.Logging.AddDebug();
			builder.Logging.AddConsole();
#endif
			// Add services
			builder.Services.AddScoped<IClientInfoProvider, MauiClientInfoProvider>();

			// Register repositories
			if (builder.Configuration.GetValue<bool>("UseMockDatabase", true))
			{
				builder.Services.AddSingleton<IAuthRepository, MockAuthRepository>();
				builder.Services.AddSingleton<IPatientRepository, MockPatientRepository>();
				builder.Services.AddSingleton<IPharmacistRepository, MockPharmacistRepository>();
				builder.Services.AddSingleton<IAdminRepository, MockAdminRepository>();
				builder.Services.AddSingleton<IDoctorRepository, MockDoctorRepository>();
				builder.Services.AddSingleton<IPrescriptionWorkflowRepository, MockPrescriptionWorkflowRepository>();
				builder.Services.AddSingleton<IFulfilmentRepository, MockFulfilmentRepository>();
			}
			else
			{
				builder.Services.AddScoped<IAuthRepository, AuthRepository>();
				builder.Services.AddScoped<IPatientRepository, PatientRepository>();
				builder.Services.AddScoped<IPharmacistRepository, PharmacistRepository>();
				builder.Services.AddScoped<IAdminRepository, AdminRepository>();
				builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
				builder.Services.AddScoped<IPrescriptionWorkflowRepository, PrescriptionWorkflowRepository>();
				builder.Services.AddScoped<IFulfilmentRepository, FulfilmentRepository>();
			}

			// Register services
			builder.Services.AddScoped<IAuthService, AuthService>();
			builder.Services.AddScoped<IPatientService, PatientService>();
			builder.Services.AddScoped<IPharmacistService, PharmacistService>();
			builder.Services.AddScoped<IAdminService, AdminService>();
			builder.Services.AddScoped<IDoctorService, DoctorService>();
			builder.Services.AddScoped<IPrescriptionWorkflowService, PrescriptionWorkflowService>();
			builder.Services.AddAuthorizationCore(options =>
			{
				options.AddPolicy(AuthorizationPolicies.ViewOwnPatientData, policy => policy.RequireRole("Patient"));
				options.AddPolicy(AuthorizationPolicies.IssuePrescription, policy => policy.RequireRole("Doctor"));
				options.AddPolicy(AuthorizationPolicies.VerifyPrescription, policy => policy.RequireRole("Pharmacist"));
				options.AddPolicy(AuthorizationPolicies.DispenseMedication, policy => policy.RequireRole("Pharmacist"));
				options.AddPolicy(AuthorizationPolicies.ManageUsers, policy => policy.RequireRole("Admin"));
				options.AddPolicy(AuthorizationPolicies.ViewAuditLogs, policy => policy.RequireRole("Admin"));
			});
			builder.Services.AddScoped<JwtAuthenticationStateProvider>();
			builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(sp =>
				sp.GetRequiredService<JwtAuthenticationStateProvider>());
			builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
			builder.Services.AddScoped<IAccessGuard, AccessGuard>();

			// Add Dapper (connection factory)
			builder.Services.AddScoped<IDbConnection>(sp =>
				new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

			return builder.Build();
		}
	}
}
