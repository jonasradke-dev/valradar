using System.Text;
using System.Text.Json;
using ValRadar.Models;

namespace ValRadar.Auth;

public class RiotAuthService : IRiotAuthService
{
    private readonly ILockfileReader _lockfileReader;
    private readonly RegionResolver _regionResolver;
    private readonly HttpClient _localHttpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    
    private AuthState _currentState;
    public AuthState Current => _currentState;
    
    private RiotAuthService(
        ILockfileReader lockfileReader,
        RegionResolver regionResolver,
        HttpClient localHttpClient)
    {
        ArgumentNullException.ThrowIfNull(lockfileReader);
        ArgumentNullException.ThrowIfNull(regionResolver);
        ArgumentNullException.ThrowIfNull(localHttpClient);
        
        _lockfileReader = lockfileReader;
        _regionResolver = regionResolver;
        _localHttpClient = localHttpClient;
    }
    
    public static async Task<RiotAuthService> CreateAsync(
        ILockfileReader lockfileReader,
        RegionResolver regionResolver,
        HttpClient localHttpClient,
        CancellationToken cancellationToken = default)
    {
        var service = new RiotAuthService(lockfileReader, regionResolver, localHttpClient);
        await service.RefreshAsync(cancellationToken);
        return service;
    }
    
    
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var lockfile = _lockfileReader.Read();
            var regionData = _regionResolver.Resolve();
            var (puuid, authToken, entitlementToken) =
                await FetchTokensFromLocalClient(lockfile, cancellationToken);
            
            _currentState = new AuthState
            {
                Puuid = puuid,
                AuthToken = authToken,
                EntitlementToken = entitlementToken,
                Region = regionData.Region,
                Shard = regionData.Shard,
                LockfileData = lockfile
            };
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<(string Puuid, string AuthToken, string EntitlementToken)>
        FetchTokensFromLocalClient(LockfileData lockfileData, CancellationToken cancellationToken)
    {
        var url = $"https://127.0.0.1:{lockfileData.Port}/entitlements/v1/token";
        var authString = $"riot:{lockfileData.Password}";
        var authBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
        
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", $"Basic {authBase64}");
        
        HttpResponseMessage? response = null;
        try
        {
            response = await _localHttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var snippet = errorBody.Length > 500 ? errorBody[..500] + "..." : errorBody;
                throw new AuthRefreshException(
                    $"Riot Client returned {(int)response.StatusCode} " +
                    $"({response.StatusCode}) for {url}. Body: {snippet}");
            }
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonElement json;
            try
            {
                json = JsonSerializer.Deserialize<JsonElement>(content);
            }
            catch (JsonException ex)
            {
                throw new AuthRefreshException(
                    $"Failed to parse Riot Client token response as JSON: {ex.Message}", ex);
            }
            
            return (
                Puuid: RequireString(json, "subject"),
                AuthToken: RequireString(json, "accessToken"),
                EntitlementToken: RequireString(json, "token"));
        }
        catch (HttpRequestException ex)
        {
            throw new AuthRefreshException(
                $"Network error contacting Riot Client at {url}: {ex.Message}", ex);
        }
        finally
        {
            response?.Dispose();
        }
    }

    private static string RequireString(JsonElement json, string propertyName)
    {
        if (!json.TryGetProperty(propertyName, out var prop))
            throw new AuthRefreshException(
                $"Riot Client response missing required field '{propertyName}'.");

        var value = prop.GetString();
        if (string.IsNullOrEmpty(value))
            throw new AuthRefreshException(
                $"Riot Client response field '{propertyName}' is empty or null.");

        return value;
    }
}

public class AuthRefreshException : Exception
{
    public AuthRefreshException(string message) : base(message) { }
    public AuthRefreshException(string message, Exception inner) : base(message, inner) { }
}