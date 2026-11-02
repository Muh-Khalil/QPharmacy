using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.JSInterop;
using QPharmacy.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace QPharmacy.Shared.Service;

public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private readonly IJSRuntime _jsRuntime;
    private readonly IAuthRepository _authRepository;
    private readonly TokenValidationParameters _validationParameters;

    public JwtAuthenticationStateProvider(
        IJSRuntime jsRuntime,
        IAuthRepository authRepository,
        IConfiguration configuration)
    {
        _jsRuntime = jsRuntime;
        _authRepository = authRepository;
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT signing key is not configured."))),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.GivenName,
            RoleClaimType = ClaimTypes.Role
        };
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            return await CreateStateAsync(token);
        }
        catch (InvalidOperationException)
        {
            return new AuthenticationState(Anonymous);
        }
        catch (JSException)
        {
            return new AuthenticationState(Anonymous);
        }
    }

    public async Task MarkAuthenticatedAsync(string token)
    {
        var stateTask = CreateStateAsync(token);
        NotifyAuthenticationStateChanged(stateTask);
        await stateTask;
    }

    public void MarkLoggedOut()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    public async Task<string?> GetTokenAsync() =>
        await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");

    private async Task<AuthenticationState> CreateStateAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(Anonymous);

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(
                token,
                _validationParameters,
                out _);
            var session = await _authRepository.ValidateSessionAsync(token);
            if (session is null)
                return new AuthenticationState(Anonymous);

            return new AuthenticationState(principal);
        }
        catch (Exception ex) when (ex is ArgumentException or SecurityTokenException)
        {
            return new AuthenticationState(Anonymous);
        }
    }
}
