using Microsoft.Maui.Devices;
using QPharmacy.Shared.Service;

namespace QPharmacy.Services
{
    public class MauiClientInfoProvider : IClientInfoProvider
    {
        public static string CurrentToken { get; set; } = string.Empty;

        public string GetUserAgent() =>
            $"MAUI Client (OS: {DeviceInfo.Current.Platform} {DeviceInfo.Current.Version}; Device: {DeviceInfo.Current.Model})";

        public string GetIpAddress() => "127.0.0.1"; // Mobile/Local client is always local to itself

        public string GetToken() => CurrentToken;
    }
}
