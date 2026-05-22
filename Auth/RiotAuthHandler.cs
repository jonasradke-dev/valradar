using ValRadar.Models;

namespace ValRadar.Auth;

public class RiotAuthHandler : DelegatingHandler
{
    private readonly AuthState _authState;
    private readonly string _clientPlatform;
    private readonly Func<string> _clientVersionProvider;

    public RiotAuthHandler(AuthState authState, string clientPlatform, Func<string> clientVersionProvider)
    {
        _authState = authState;
        _clientPlatform = clientPlatform;
        _clientVersionProvider = clientVersionProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri?.Host.EndsWith(".a.pvp.net") == true)
        {
            request.Headers.TryAddWithoutValidation(
                "X-Riot-Entitlements-JWT", _authState.EntitlementToken);
            request.Headers.TryAddWithoutValidation(
                "Authorization", $"Bearer {_authState.AuthToken}");
            request.Headers.TryAddWithoutValidation(
                "X-Riot-ClientPlatform", _clientPlatform);
            request.Headers.TryAddWithoutValidation(
                "X-Riot-ClientVersion", _clientVersionProvider());

        }

        return await base.SendAsync(request, cancellationToken);
    }
}