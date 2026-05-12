using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using ValRadar.Models;

namespace ValRadar.Services;

public class RiotService
{
    private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
    });
    
    public static RiotLockfileData? GetLockfileData()
    {
        string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string lockfilePath = Path.Combine(localAppDataPath, "Riot Games", "Riot Client", "Config", "lockfile");

        if (!File.Exists(lockfilePath))
        {
            Console.WriteLine("Could not find lockfile. Is Valorant running?");
        }

        try
        {
            using FileStream fs = new FileStream(lockfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new StreamReader(fs);
            string lockfileContent = reader.ReadToEnd();
            string[] lockfileParts = lockfileContent.Split(':');

            return new RiotLockfileData()
            {
                Port = lockfileParts[2],
                Password = lockfileParts[3],

            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading lockfile: {ex.Message}");
            return null;
        }
    }

    public static (string region, string shard) GetRegionAndShardFromShooterGame()
    {
        string pattern = @"https://glz-(.+?)-1\.(.+?)\.a\.pvp\.net";
        Regex regex = new Regex(pattern);
        string shooterGameLogPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) +
                                    @"\VALORANT\Saved\Logs\ShooterGame.log";

        if (!File.Exists(shooterGameLogPath))
        {
            Console.WriteLine("Could not find ShooterGame.log. Is Valorant running?");
            Console.WriteLine("Defaulting to EU shard and region");
            return ("eu", "eu");
        }

        using FileStream fs = new FileStream(shooterGameLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using (StreamReader reader = new StreamReader(fs))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                Match match = regex.Match(line);
                if (match.Success)
                {
                    string region = match.Groups[1].Value;
                    string shard = match.Groups[2].Value;
                    return (region, shard);
                }
            }
        }
        Console.WriteLine("Warning: Couldn't parse region from log defaulting to EU shard and region");
        return ("eu", "eu");
    }

    public static async Task<JsonElement?> LocalAPIGet(RiotLockfileData riotLockfileData, string path)
    {
        string url = $"https://127.0.0.1:{int.Parse(riotLockfileData.Port)}{path}";
        string authInfo = $"riot:{riotLockfileData.Password}";
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
            Console.WriteLine(ex.Message);
            return null;
        }
        
    }

    public static async Task<AuthState?> GetAuthState(RiotLockfileData riotLockfileData)
    {
        var authTokenResponse = await LocalAPIGet(riotLockfileData, "/entitlements/v1/token");
        var (region, shard) = GetRegionAndShardFromShooterGame();

        if (authTokenResponse is {} data)
        {
            return new AuthState
            {
                ppuid = data.GetProperty("subject").GetString(),
                AuthToken = data.GetProperty("accessToken").GetString() ?? "",
                EntitlementToken = data.GetProperty("token").GetString() ?? "",
                Region = region,
                Shard = shard,
                LockfileData = riotLockfileData
            };
        }
        return null;
    }
    
    public static async Task<JsonElement?> GetPlayerPresence(RiotLockfileData riotLockfileData)
    {
        return await LocalAPIGet(riotLockfileData, "/chat/v4/presences");
    }
    
    public enum GamePhase
    {
        Menu,
        PreGame,
        InGame,
        Unknown
    }
    
    public static async Task<GamePhase> GetCurrentGamePhase(RiotLockfileData lockfileData, string puuid)
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