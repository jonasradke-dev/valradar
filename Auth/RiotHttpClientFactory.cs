using System.Net.Security;
using ValRadar.Models;

namespace ValRadar.Auth;

public class RiotHttpClientFactory
{
    public static HttpClient Create(AuthState authState, string clientPlatform, Func<string> clientVersionProvider)
    {
        var primaryHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) =>
            {
                if (msg.RequestUri?.Host is "127.0.0.1" or "localhost")
                    return true;
                return errors == SslPolicyErrors.None;
            }
        };

        var authHandler = new RiotAuthHandler(authState, clientPlatform, clientVersionProvider)
        {
            InnerHandler = primaryHandler
        };
        return new HttpClient(authHandler);
    }
}