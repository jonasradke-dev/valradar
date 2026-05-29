using System.Text;
using System.Text.Json;
using ValRadar.Auth;
using ValRadar.Models;
using ValRadar.Util;

namespace ValRadar.Services;

public class ValorantApiService : IDisposable
{
    private static readonly HttpClient _publicApiClient = new();
    private readonly HttpClient _httpClient;
    
    private const string ClientPlatform =
        "ew0KCSJwbGF0Zm9ybVR5cGUiOiAiUEMiLA0KCSJwbGF0Zm9ybU9TIjogIldpbmRvd3MiLA0KCSJwbGF0Zm9ybU9TVmVyc2lvbiI6ICIxMC4wLjE5MDQyLjEuMjU2LjY0Yml0IiwNCgkicGxhdGZvcm1DaGlwc2V0IjogIlVua25vd24iDQp9";

    private readonly IRiotAuthService _authService;
    private string _clientVersion = "";
    private string GlzBase => $"https://glz-{_authService.Current.Region}-1.{_authService.Current.Shard}.a.pvp.net";
    private string PdBase => $"https://pd.{_authService.Current.Shard}.a.pvp.net";

    public ValorantApiService(IRiotAuthService authService)
    {
        _authService = authService;

        _httpClient = RiotHttpClientFactory.Create(
            authService,
            ClientPlatform,
            () => _clientVersion);
    }

    public async Task InitializeAsync()
    {
        _clientVersion = await FetchClientVersion();
    }
    
    private static async Task<string> FetchClientVersion()
    {
        try
        {
            var response = await _publicApiClient.GetAsync("https://valorant-api.com/v1/version");
            var json = JsonSerializer.Deserialize<JsonElement>(
                await response.Content.ReadAsStringAsync()
            );
            return json.GetProperty("data").GetProperty("riotClientVersion").GetString() ?? "";
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to fetch client version from valorant-api.com: {ex.Message}. Falling back to hardcoded version.");
            return "release-12.08-shipping-7-4578383";
        }
    }
    
    private async Task<JsonElement?> GetAsync(string baseUrl, string path)
    {
        string url = $"{baseUrl}{path}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        try
        {
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                await LogHttpErrorAsync(request, response);
                return null;
            }

            return JsonSerializer.Deserialize<JsonElement>(
                await response.Content.ReadAsStringAsync());
        }
        catch (HttpRequestException exception)
        {
            Logger.Error($"Network error | GET {url} | {exception.Message}");
            return null;
        }
        catch (JsonException exception)
        {
            Logger.Error($"JSON parse error | GET {url} | {exception.Message}");
            return null;
        }
    }

    public async Task<JsonElement?> GetPartyData()
    {
        var playerParty = await GetAsync(GlzBase, $"/parties/v1/players/{_authService.Current.Puuid}");
        if (playerParty is not { } pp)
        {
            return null;
        }

        var partyId = pp.GetProperty("CurrentPartyID").GetString();
        if (string.IsNullOrEmpty(partyId))
        {
            return null;
        }
        
        return await GetAsync(GlzBase, $"/parties/v1/parties/{partyId}");
    }
    
    public async Task<Dictionary<string, string>> ResolveNames(
        List<string> puuids,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>();
        var url = $"{PdBase}/name-service/v2/players";

        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(puuids),
                Encoding.UTF8,
                "application/json")
        };

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogHttpErrorAsync(request, response, cancellationToken);
                return result;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(content);

            foreach (var player in jsonResponse.EnumerateArray())
            {
                var id = player.GetProperty("Subject").GetString() ?? "";
                var name = player.GetProperty("GameName").GetString();
                var tag = player.GetProperty("TagLine").GetString();
                result[id] = $"{name}#{tag}";
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.Error($"Network error | PUT {url} | {ex.Message}");
        }
        catch (JsonException ex)
        {
            Logger.Error($"JSON parse error | PUT {url} | {ex.Message}");
        }

        return result;
    }
    
    public async Task<JsonElement?> GetPlayerMMR(string puuid)
    {
        return await GetAsync(PdBase, $"/mmr/v1/players/{puuid}");
    }
    
    public async Task<Dictionary<string, JsonElement?>> GetBatchMMR(List<string> puuids)
    {
        var result = new Dictionary<string, JsonElement?>();
        foreach (var puuid in puuids)
        {
            result[puuid] = await GetPlayerMMR(puuid);
            await Task.Delay(200); 
        }
        return result;
    }
    public static int ExtractCurrentTier(JsonElement mmrData)
    {
        try
        {
            if (mmrData.TryGetProperty("LatestCompetitiveUpdate", out var latest) &&
                latest.ValueKind == JsonValueKind.Object &&
                latest.TryGetProperty("TierAfterUpdate", out var tierAfter))
            {
                return tierAfter.GetInt32();
            }

            
            if (mmrData.TryGetProperty("QueueSkills", out var qs) &&
                qs.ValueKind == JsonValueKind.Object &&
                qs.TryGetProperty("competitive", out var comp) &&
                comp.ValueKind == JsonValueKind.Object &&
                comp.TryGetProperty("TotalWinsNeededForRank", out _) &&
                comp.TryGetProperty("CurrentSeasonGamesNeededForRating", out _))
            {
                if (comp.TryGetProperty("SeasonalInfoBySeasonID", out var seasons) &&
                    seasons.ValueKind == JsonValueKind.Object)
                {
                    int maxGames = 0;
                    int tier = 0;
                    foreach (var season in seasons.EnumerateObject())
                    {
                        if (season.Value.ValueKind != JsonValueKind.Object) continue;
                        var games = season.Value.TryGetProperty("NumberOfGames", out var g) ? g.GetInt32() : 0;
                        if (games >= maxGames)
                        {
                            maxGames = games;
                            tier = season.Value.TryGetProperty("CompetitiveTier", out var t) ? t.GetInt32() : 0;
                        }
                    }
                    return tier;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract tier from MMR data: {ex.Message}");
        }
        return 0;
    }
    
    public async Task<JsonElement?> GetPreGameMatchData(string puuid)
    {
        var player = await GetAsync(GlzBase, $"/pregame/v1/players/{puuid}");
        if (player is not { } p)
        {
            return null;
        }
        var matchId = p.GetProperty("MatchID").GetString();
        if (string.IsNullOrEmpty(matchId))
        {
            return null;
        }
        return await GetAsync(GlzBase, $"/pregame/v1/matches/{matchId}");
    }

    public async Task<JsonElement?> GetCurrentGameData(string puuid)
    {
        var player = await GetAsync(GlzBase, $"/core-game/v1/players/{puuid}");
        if(player is not { } p)
        {
            return null;
        }
        var matchId = p.GetProperty("MatchID").GetString();
        return await GetAsync(GlzBase, $"/core-game/v1/matches/{matchId}");
    }

    public async Task<List<JsonElement>> GetPlayerMatchHistory(string puuid, int startIndex = 0, int endIndex = 5)
    {
        var response = await GetAsync(PdBase, $"/mmr/v1/players/{puuid}/competitiveupdates?startIndex={startIndex}&endIndex={endIndex}&queue=competitive");
        var result = new List<JsonElement>();
        
        if (response is not { } data) return result;
        if (!data.TryGetProperty("Matches", out var matches)) return result;
        
        return matches.EnumerateArray().ToList();
    }
    
    public async Task<Dictionary<string, List<JsonElement>>> GetPlayerMatchHistoryBatch(List<string> puuids, int startIndex = 0, int endIndex = 5)
    {
        var result = new Dictionary<string, List<JsonElement>>();


        foreach (var puuid in puuids)
        {
            result[puuid] = await GetPlayerMatchHistory(puuid, startIndex, endIndex);
            await Task.Delay(250);
        }
        
        return result;
    }

    private async Task LogHttpErrorAsync(
        HttpRequestMessage request,
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        string body;
        
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            body = $"Failed to read response body: {ex.Message}";
        }
        if (!string.IsNullOrEmpty(body) && body.Length > 1000)
        {
            body = string.Concat(body.AsSpan(0, 1000), "...(truncated)" );
        }
        var method = request.Method;
        var url = request.RequestUri?.ToString() ?? "<no-uri>";
        var statusCode = (int)response.StatusCode;
        var statusName = response.StatusCode.ToString();

        var clientVersion = string.IsNullOrEmpty(_clientVersion) ? "<Empty>" : _clientVersion;
        
        Logger.Error($"Riot API Error | {method} {url} -> {statusCode} - ({statusName}) | Clientversion={clientVersion} | Body={body}");
        
    }
    

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}