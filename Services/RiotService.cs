using System.Text;
using System.Text.Json;
using ValRadar.Auth;
using ValRadar.Util;

namespace ValRadar.Services;

public class RiotService
{
    private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
    });
    
    public static async Task<JsonElement?> LocalAPIGet(LockfileData lockfileData, string path)
    {
        string url = $"https://127.0.0.1:{lockfileData.Port}{path}";
        string authInfo = $"riot:{lockfileData.Password}";
        string authInfoBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authInfo));
        
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Basic {authInfoBase64}");
        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string jsonString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(jsonString);
        }
        catch(Exception ex)
        {
            Logger.Error(ex.Message);
            return null;
        }
        
    }
    
    public enum GamePhase
    {
        Menu,
        PreGame,
        InGame,
        Unknown
    }
    
    public static async Task<GamePhase> GetCurrentGamePhase(LockfileData lockfileData, string puuid)
    {
        var presenceResponse = await LocalAPIGet(lockfileData, "/chat/v4/presences");
        if (presenceResponse is not { } data)
            return GamePhase.Unknown;

        foreach (var presence in data.GetProperty("presences").EnumerateArray())
        {
            if (presence.GetProperty("puuid").GetString() != puuid)
                continue;

            string? privateB64 = presence.GetProperty("private").GetString();
            if (string.IsNullOrEmpty(privateB64))
                return GamePhase.Menu;

            var p = JsonSerializer.Deserialize<JsonElement>(
                Encoding.UTF8.GetString(Convert.FromBase64String(privateB64))
            );

            var sessionState = p.GetProperty("matchPresenceData")
                .GetProperty("sessionLoopState")
                .GetString();

            return sessionState switch
            {
                "MENUS"   => GamePhase.Menu,
                "PREGAME" => GamePhase.PreGame,
                "INGAME"  => GamePhase.InGame,
                _         => GamePhase.Unknown
            };
        }

        return GamePhase.Unknown;
    }
    
}