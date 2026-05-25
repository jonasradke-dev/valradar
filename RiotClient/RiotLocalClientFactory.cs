namespace ValRadar.Auth;

public static class RiotLocalClientFactory
{
    public static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) =>
            {
                if (msg.RequestUri?.Host is "127.0.0.1" or "localhost")
                    return true;
                return false;
            }
        };
        return new HttpClient(handler);
    }
}