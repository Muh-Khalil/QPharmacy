namespace QPharmacy.Shared.Service
{
    public interface IClientInfoProvider
    {
        string GetUserAgent();
        string GetIpAddress();
        string GetToken();
    }
}
