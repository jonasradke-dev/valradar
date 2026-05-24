using System.Net;
using ValRadar.Util;

namespace ValRadar.Auth;

public class TokenRefreshHandler : DelegatingHandler
{
    private readonly IRiotAuthService _authService;

    public TokenRefreshHandler(IRiotAuthService authService)
    {
        ArgumentNullException.ThrowIfNull(authService);
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var isRiotApi = request.RequestUri?.Host.EndsWith(".a.pvp.net") == true;
        var looksLikeAuthFailure = response.StatusCode 
            is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest;
        
        if (!(isRiotApi && looksLikeAuthFailure))
            return response;
        Logger.Log( $"Received {response.StatusCode} from {request.RequestUri}. Refreshing tokens.");

        try
        {
            await _authService.RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to refresh tokens: {ex.Message}");
            return response;
        }
        response.Dispose();
        var clonedRequest = await CloneRequestAsync(request);
        return await base.SendAsync(clonedRequest, cancellationToken);
    }
    
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        if (original.Content != null)
        {
            var bodyBytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bodyBytes);
            foreach (var header in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }
}