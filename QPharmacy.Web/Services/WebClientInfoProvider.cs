using Microsoft.AspNetCore.Http;
using QPharmacy.Shared.Service;
using System.Linq;

namespace QPharmacy.Web.Services
{
    public class WebClientInfoProvider : IClientInfoProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public WebClientInfoProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetUserAgent() =>
            _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString() ?? string.Empty;

        public string GetIpAddress() =>
            _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        public string GetToken()
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
            return authHeader?.Replace("Bearer ", "") ?? string.Empty;
        }
    }
}
