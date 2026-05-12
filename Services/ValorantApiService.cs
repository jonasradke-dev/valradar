using System.Text;
using System.Text.Json;
using ValRadar.Models;

namespace ValRadar.Services;

public class ValorantApiService
{
    private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
    });
    
    private const string ClientPlatform =
        "ew0KCSJwbGF0Zm9ybVR5cGUiOiAiUEMiLA0KCSJwbGF0Zm9ybU9TIjogIldpbmRvd3MiLA0KCSJwbGF0Zm9ybU9TVmVyc2lvbiI6ICIxMC4wLjE5MDQyLjEuMjU2LjY0Yml0IiwNCgkicGxhdGZvcm1DaGlwc2V0IjogIlVua25vd24iDQp9";

    private readonly AuthState _authState;
    private readonly string _glzBase;
    private readonly string _pdBase;
    private string _clientVersion = "";

    public ValorantApiService(AuthState authState)
    {
        _authState = authState;
        _glzBase = $"https://glz-{authState.Region}-1.{authState.Shard}.a.pvp.net";
        _pdBase = $"https://pd.{authState.Shard}.a.pvp.net";
    }

    public async Task InitializeAsync()
    {
        _clientVersion = await FetchClientVersion();
    }
    
    private static async Task<string> FetchClientVersion()
    {
        try
        {
            var response = await _httpClient.GetAsync("https://valorant-api.com/v1/version");
            var json = JsonSerializer.Deserialize<JsonElement>(
                await response.Content.ReadAsStringAsync()
            );
            return json.GetProperty("data").GetProperty("riotClientVersion").GetString() ?? "";
        }
        catch
        {
            return "release-12.08-shipping-7-4578383";
        }
    }
    
    private async Task<JsonElement?> GetAsync(string baseUrl, string path)
    {
        string url = $"{baseUrl}{path}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Riot-Entitlements-JWT", _authState.EntitlementToken);
        request.Headers.Add("Authorization", $"Bearer {_authState.AuthToken}");
        request.Headers.Add("X-Riot-ClientPlatform", ClientPlatform);
        request.Headers.Add("X-Riot-ClientVersion", _clientVersion);

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<JsonElement>(
                await response.Content.ReadAsStringAsync());
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Error fetching data from {url}: {exception.Message}");
            return null;
        }
    }

    public async Task<JsonElement?> GetPartyData()
    {
        var playerParty = await GetAsync(_glzBase, $"/parties/v1/players/{_authState.ppuid}");
        if (playerParty is not { } pp)
        {
            return null;
        }

        var partyId = pp.GetProperty("CurrentPartyID").GetString();
        if (string.IsNullOrEmpty(partyId))
        {
            return null;
        }
        
        return await GetAsync(_glzBase, $"/parties/v1/parties/{partyId}");
    }
    
    public async Task<Dictionary<string, string>> ResolveNames(List<string> puuids)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{_pdBase}/name-service/v2/players");
        request.Headers.Add("X-Riot-Entitlements-JWT", _authState.EntitlementToken);
        request.Headers.Add("Authorization", $"Bearer {_authState.AuthToken}");
        request.Headers.Add("X-Riot-ClientPlatform", ClientPlatform);
        request.Headers.Add("X-Riot-ClientVersion", _clientVersion);
        request.Content = new StringContent(
            JsonSerializer.Serialize(puuids),Encoding.UTF8, "application/json");

        var result = new Dictionary<string, string>();
        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(
                await response.Content.ReadAsStringAsync());

            foreach (var player in jsonResponse.EnumerateArray())
            {
                var id = player.GetProperty("Subject").GetString() ?? "";
                var name = player.GetProperty("GameName").GetString();
                var tag = player.GetProperty("TagLine").GetString();
                result[id] = $"{name}#{tag}";
            }
        }catch(Exception exception)
        {
            Console.WriteLine($"Error resolving names: {exception.Message}");
        }
        return result;
    }
    public async Task<JsonElement?> GetPlayerMMR(string puuid)
    {
        return await GetAsync(_pdBase, $"/mmr/v1/players/{puuid}");
    }
    public static int ExtractCurrentTier(JsonElement mmrData)
    {
        if (mmrData.TryGetProperty("QueueSkills", out var qs) &&
            qs.TryGetProperty("competitive", out var comp) &&
            comp.TryGetProperty("SeasonalInfoBySeasonID", out var seasons))
        {
            JsonElement latest = default;
            foreach (var season in seasons.EnumerateObject())
                latest = season.Value;

            if (latest.ValueKind != JsonValueKind.Undefined &&
                latest.TryGetProperty("CompetitiveTier", out var t))
                return t.GetInt32();
        }
        return 0;
    }
    
    public async Task<JsonElement?> GetPreGameMatchData(string puuid)
    {
        var player = await GetAsync(_glzBase, $"/pregame/v1/players/{puuid}");
        if (player is not { } p)
        {
            return null;
        }
        var matchId = p.GetProperty("MatchID").GetString();
        if (string.IsNullOrEmpty(matchId))
        {
            return null;
        }
        return await GetAsync(_glzBase, $"/pregame/v1/matches/{matchId}");
    }
    
}