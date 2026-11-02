using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using QPharmacy.Repositories;
using QPharmacy.Services;
using QPharmacy.Shared.Service;
using QPharmacy.Web.Components;
using QPharmacy.Web.Services;
using QPharmacy.Web.Hubs;
using System.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents(options => options.DetailedErrors = true);


builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IClientInfoProvider, WebClientInfoProvider>();

// Register repositories
if (builder.Configuration.GetValue<bool>("UseMockDatabase", false))
{
	builder.Services.AddSingleton<IAuthRepository, MockAuthRepository>();
	builder.Services.AddSingleton<IPatientRepository, MockPatientRepository>();
	builder.Services.AddSingleton<IPharmacistRepository, MockPharmacistRepository>();
	builder.Services.AddSingleton<IAdminRepository, MockAdminRepository>();
	builder.Services.AddSingleton<IDoctorRepository, MockDoctorRepository>();
	builder.Services.AddSingleton<IPrescriptionWorkflowRepository, MockPrescriptionWorkflowRepository>();
	builder.Services.AddSingleton<IFulfilmentRepository, MockFulfilmentRepository>();
	builder.Services.AddSingleton<IPaymentWorkflowRepository, MockPaymentWorkflowRepository>();
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
	builder.Services.AddScoped<IPaymentWorkflowRepository, PaymentWorkflowRepository>();
}

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IPharmacistService, PharmacistService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IPrescriptionWorkflowService, PrescriptionWorkflowService>();
builder.Services.AddScoped<IPaymentWorkflowService, PaymentWorkflowService>();
builder.Services.AddScoped<SidebarService>();
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

// Authentication & Custom Result Handler for Blazor
builder.Services.AddAuthentication();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, BlazorAuthorizationMiddlewareResultHandler>();

var app = builder.Build();


app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode()
	.AddAdditionalAssemblies(typeof(QPharmacy.Shared._Imports).Assembly);

app.MapHub<ConsultationHub>("/consultationHub");

app.MapPost("/api/webhooks/payment", async (HttpRequest request, IPaymentWorkflowService paymentService) =>
{
	using var reader = new StreamReader(request.Body);
	var payload = await reader.ReadToEndAsync();
	var signature = request.Headers["X-Signature"].ToString();
	var eventId = request.Headers["X-Event-Id"].ToString();
	if (string.IsNullOrEmpty(eventId)) eventId = Guid.NewGuid().ToString("N");
	var eventType = request.Headers["X-Event-Type"].ToString();
	if (string.IsNullOrEmpty(eventType)) eventType = "payment_intent.succeeded";

	var success = await paymentService.ProcessWebhookAsync(payload, signature, eventId, eventType);
	return success ? Results.Ok(new { status = "success" }) : Results.BadRequest(new { status = "invalid_signature" });
});

app.Run();
