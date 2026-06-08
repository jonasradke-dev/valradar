using System.Net.Security;
using ValRadar.Http;

namespace ValRadar.Auth;

public static class RiotHttpClientFactory
{
    public static HttpClient Create(IRiotAuthService authService, string clientPlatform, Func<string> clientVersionProvider)
    {
        var primaryHandler = new HttpClientHandler();

        var rateLimitHandler = new RateLimitHandler(TimeSpan.FromMilliseconds(150))
        {
            InnerHandler = primaryHandler
        };

        var authHandler = new RiotAuthHandler(authService, clientPlatform, clientVersionProvider)
        {
            InnerHandler = rateLimitHandler
        };
        
        var refreshHandler = new TokenRefreshHandler(authService)
        {
            InnerHandler = authHandler
        };
        
        return new HttpClient(refreshHandler);
        
    }
}