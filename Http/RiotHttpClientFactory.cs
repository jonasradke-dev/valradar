using ValRadar.Auth;

namespace ValRadar.Http;

public static class RiotHttpClientFactory
{
    public static HttpClient Create(IRiotAuthService authService, string clientPlatform, Func<string> clientVersionProvider)
    {
        var primaryHandler = new HttpClientHandler();

        var retryHandler = new RetryHandler
        {
            InnerHandler = primaryHandler
        };

        var rateLimitHandler = new RateLimitHandler(TimeSpan.FromMilliseconds(250))
        {
            InnerHandler = retryHandler
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