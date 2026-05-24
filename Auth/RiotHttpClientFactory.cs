using System.Net.Security;

namespace ValRadar.Auth;

public static class RiotHttpClientFactory
{
    public static HttpClient Create(IRiotAuthService authService, string clientPlatform, Func<string> clientVersionProvider)
    {
        var primaryHandler = new HttpClientHandler();

        var authHandler = new RiotAuthHandler(authService, clientPlatform, clientVersionProvider)
        {
            InnerHandler = primaryHandler
        };
        
        var refreshHandler = new TokenRefreshHandler(authService)
        {
            InnerHandler = authHandler
        };
        
        return new HttpClient(refreshHandler);
        
    }
}