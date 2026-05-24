using ValRadar.Models;

namespace ValRadar.Auth;

public class RiotAuthHandler : DelegatingHandler
{
    private readonly IRiotAuthService _authService;
    private readonly string _clientPlatform;
    private readonly Func<string> _clientVersionProvider;

    public RiotAuthHandler(IRiotAuthService authService, string clientPlatform, Func<string> clientVersionProvider)
    {
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(clientPlatform);
        ArgumentNullException.ThrowIfNull(clientVersionProvider);
        
        _authService = authService;
        _clientPlatform = clientPlatform;
        _clientVersionProvider = clientVersionProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri?.Host.EndsWith(".a.pvp.net") == true)
        {
            var version = _clientVersionProvider();
            if (string.IsNullOrEmpty(version))
                throw new InvalidOperationException(
                    "ClientVersion not initialized. Call InitializeAsync() before making requests.");
            
            var state = _authService.Current;
            
            request.Headers.TryAddWithoutValidation(
                "X-Riot-Entitlements-JWT", state.EntitlementToken);
            request.Headers.TryAddWithoutValidation(
                "Authorization", $"Bearer {state.AuthToken}");
            request.Headers.TryAddWithoutValidation(
                "X-Riot-ClientPlatform", _clientPlatform);
            request.Headers.TryAddWithoutValidation(
                "X-Riot-ClientVersion", _clientVersionProvider());

        }

        return await base.SendAsync(request, cancellationToken);
    }
}