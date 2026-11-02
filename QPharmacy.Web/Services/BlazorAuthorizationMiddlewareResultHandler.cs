using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace QPharmacy.Web.Services
{
    public class BlazorAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

        public async Task HandleAsync(
            RequestDelegate next,
            HttpContext context,
            AuthorizationPolicy policy,
            PolicyAuthorizationResult authorizeResult)
        {
            // For Blazor component page requests, allow the HTTP response (HTML shell) to render
            // so the interactive circuit can start and evaluate authentication via
            // JwtAuthenticationStateProvider and AuthorizeRouteView in localStorage.
            if (!authorizeResult.Succeeded)
            {
                var path = context.Request.Path.Value ?? string.Empty;
                if (!path.StartsWith("/api/", System.StringComparison.OrdinalIgnoreCase))
                {
                    await next(context);
                    return;
                }
            }

            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}
