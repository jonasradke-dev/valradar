using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Rendering;
using ValRadar.Util;
using ValRadar.Utils;

namespace ValRadar.Services;

public class GameDisplayService
{
    private readonly ValorantApiService _valorantApiService;
    private readonly DiscordRPCService _discordRpcService;
    private readonly string _selfPuuid;

    private string? _cachedMatchId;
    private IRenderable? _cachedMatchDisplay;
    private string? _cachedPartyId;
    private IRenderable? _cachedPartyDisplay;
    private List<string>? _partyMemberPuuids = [];
    private static readonly TimeSpan WinRateTtl = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, (int wins, int losses, DateTime fetchedAt)> _playerWinRateCache = new();

    public GameDisplayService(ValorantApiService valorantApiService, DiscordRPCService discordRpcService,
        string selfPuuid)
    {
        _valorantApiService = valorantApiService;
        _discordRpcService = discordRpcService;
        _selfPuuid = selfPuuid;
        
    }

    public void ResetCache()
    {
        _cachedMatchId = null;
        _cachedMatchDisplay = null;
        _cachedPartyId = null;
        _cachedPartyDisplay = null;
        _playerWinRateCache.Clear();
    }

    public async Task<IRenderable> RenderMenuAsync()
    {
        var partyData = await _valorantApiService.GetPartyData();
        if(partyData is not {} party) return new Markup("[red]Error fetching party data[/]");

        var members = new List<string>();
        foreach (var member in party.GetProperty("Members").EnumerateArray())
        {
            members.Add(member.GetProperty("Subject").GetString() ?? "");
        }

        var partyId = string.Join(",", members.OrderBy(m => m));
        if (partyId == _cachedPartyId && _cachedPartyDisplay != null)
            return _cachedPartyDisplay;

        _cachedPartyId = partyId;
        _partyMemberPuuids = members;
            
        var names = await _valorantApiService.ResolveNames(members);
        var mmr = await _valorantApiService.GetBatchMMR(members);

        var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold cyan]Lobby[/]")
                .AddColumn(new TableColumn("[cyan]Player[/]").Width(22))
                .AddColumn(new TableColumn("[cyan]Rank[/]").Width(12))
                .AddColumn(new TableColumn("[cyan]Level[/]").Width(7))
                .AddColumn(new TableColumn("[cyan]Ready[/]").Width(7))
                .AddColumn(new TableColumn("[cyan]WR[/]").Width(12));
        var memberWinRates = await GetPlayersWinRatesAsync(members);

        foreach (var member in party.GetProperty("Members").EnumerateArray())
        {
                var puuid = member.GetProperty("Subject").GetString() ?? "";
                var tier = ExtractTier(mmr, puuid);
                var identity = member.GetProperty("PlayerIdentity");
                var level = identity.GetProperty("AccountLevel").GetInt32();
                var isReady = member.GetProperty("IsReady").GetBoolean();
                var isOwner = member.TryGetProperty("IsOwner", out var o) && o.GetBoolean();

                var name = names.TryGetValue(puuid, out var n) ? n : puuid[..8];
                var nameTag = isOwner ? $"[bold gold1]{name}[/]" : $"[white]{name}[/]";
                
                var (pwWins, pwLosses, pwTotal) = memberWinRates.TryGetValue(puuid, out var r) ? r : (0, 0, 0);
                table.AddRow(nameTag, RankUtil.GetRankString(tier),$"{level}",
                    isReady ? "[green]Yes[/]" : "[red]No[/]",
                    FormatWinRateMarkup(pwWins, pwLosses, pwTotal));
        }
            
        _cachedPartyDisplay = Align.Center(table);
        _discordRpcService.UpdatePresence("In Lobby", "Waiting for match to start", "valradar_icon_1024");
        return _cachedPartyDisplay;
    }
    
    

    private async Task<(int wins, int losses, int total)> GetPlayerWinRateAsync(string puuid)
    {
        if (_playerWinRateCache.TryGetValue(puuid, out var cached) &&
            DateTime.UtcNow - cached.fetchedAt < WinRateTtl)
        {
            return (cached.wins, cached.losses, cached.wins + cached.losses);
        }

        var matches = await _valorantApiService.GetPlayerMatchHistory(puuid, 0, 5);
        int wins = 0;
        int losses = 0;

        foreach (var match in matches)
        {
            if (!match.TryGetProperty("RankedRatingEarned", out var rr))
                continue;

            var rrValue = rr.GetInt32();
            if (rrValue > 0) wins++;
            else if (rrValue < 0) losses++;
        }

        _playerWinRateCache[puuid] = (wins, losses, DateTime.UtcNow);
        return (wins, losses, wins + losses);
    }

    private async Task<Dictionary<string, (int wins, int losses, int total)>> GetPlayersWinRatesAsync(List<string> puuids)
    {
        var result = new Dictionary<string, (int wins, int losses, int total)>();

        var toFetch = new List<string>();
        foreach (var id in puuids)
        {
            if (_playerWinRateCache.TryGetValue(id, out var cached) &&
                DateTime.UtcNow - cached.fetchedAt < WinRateTtl)
            {
                result[id] = (cached.wins, cached.losses, cached.wins + cached.losses);
            }
            else
            {
                toFetch.Add(id);
            }
        }

        if (toFetch.Count > 0)
        {
            var batch = await _valorantApiService.GetPlayerMatchHistoryBatch(toFetch, 0, 5);
            foreach (var id in toFetch)
            {
                int wins = 0, losses = 0;
                if (batch.TryGetValue(id, out var matches) && matches is not null)
                {
                    foreach (var match in matches)
                    {
                        if (!match.TryGetProperty("RankedRatingEarned", out var rr)) continue;
                        var rrValue = rr.GetInt32();
                        if (rrValue > 0) wins++; else if (rrValue < 0) losses++;
                    }
                }

                _playerWinRateCache[id] = (wins, losses, DateTime.UtcNow);
                result[id] = (wins, losses, wins + losses);
            }
        }

        return result;
    }

    private string FormatWinRateMarkup(int wins, int losses, int total)
    {
        if (total == 0) return "[grey]N/A[/]";
        var winRate = (wins * 100) / total;
        var color = winRate >= 60 ? "green" : winRate >= 40 ? "yellow" : "red";
        return $"[{color}]{winRate}%[/] ({wins}W/{losses}L)";
    }
    
    
    
    public async Task<IRenderable> RenderPreGameAsync()
    {
        var preGameData = await _valorantApiService.GetPreGameMatchData(_selfPuuid);
            if(preGameData is not { } preGame) return new Markup("[yellow]Fetching pre-game data[/]");

        var allyPlayers = preGame.GetProperty("AllyTeam").GetProperty("Players");
        var mapName = MapUtil.GetMapName(preGame.GetProperty("MapID").GetString() ?? "");
            
        var puuids = new List<string>();
        foreach (var player in allyPlayers.EnumerateArray())
        {
            puuids.Add(player.GetProperty("Subject").GetString() ?? "");
        }
        
        var names = await _valorantApiService.ResolveNames(puuids);
        var mmr = await _valorantApiService.GetBatchMMR(puuids);
        var preWinRates = await GetPlayersWinRatesAsync(puuids);
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold cyan]Agent Select - {mapName}[/]")
            .AddColumn(new TableColumn("[cyan]Player[/]").Width(30))
            .AddColumn(new TableColumn("[cyan]Agent[/]").Width(12))
            .AddColumn(new TableColumn("[cyan]Rank[/]").Width(16))
            .AddColumn(new TableColumn("[cyan]Level[/]").Width(8))
            .AddColumn(new TableColumn("[cyan]Status[/]").Width(10))
            .AddColumn(new TableColumn("[cyan]WR[/]").Width(12));

        foreach (var player in allyPlayers.EnumerateArray())
        {
            var puuid = player.GetProperty("Subject").GetString() ?? "";
            var tier = ExtractTier(mmr, puuid);
            var level = player.GetProperty("PlayerIdentity").GetProperty("AccountLevel").GetInt32();
            var agentId = player.GetProperty("CharacterID").GetString() ?? "";
            var selectionState = player.GetProperty("CharacterSelectionState").GetString();
            
            var name = names.TryGetValue(puuid, out var n) ? n : puuid[..8];
            var isSelf = puuid == _selfPuuid;
            var isParty = _partyMemberPuuids?.Contains(puuid) ?? false;
            var nameTag = isSelf ? $"[bold green]{name}[/]" :
                isParty ? $"[bold cyan]{name}[/]" :
                $"[white]{name}[/]";
            
            var statusTag = selectionState switch
            {
                "locked" => "[green]Locked[/]",
                "selected" => "[yellow]Selected[/]",
                _ => "[red]Not Selected[/]"
            };
            var (pgWins, pgLosses, pgTotal) = preWinRates.TryGetValue(puuid, out var r) ? r : (0, 0, 0);
            table.AddRow(nameTag, AgentUtil.GetAgentName(agentId), RankUtil.GetRankString(tier), $"{level}", statusTag, FormatWinRateMarkup(pgWins, pgLosses, pgTotal));
        }
        _discordRpcService.UpdatePresence("Agent Select", $"Playing on {mapName}", "valradar_icon_1024");
        return Align.Center(table);
        
        
    }
    
    public async Task<IRenderable> RenderInGameAsync()
    {
        if(_cachedMatchDisplay != null)
            return _cachedMatchDisplay;
        
        var gameData = await _valorantApiService.GetCurrentGameData(_selfPuuid);
        if(gameData is not { } game) return new Markup("[yellow]Loading match data[/]");

        var matchId = game.GetProperty("MatchID").GetString();
        if (matchId == _cachedMatchId)
        {
            return _cachedMatchDisplay;
        }
        
        _cachedMatchId = matchId;
        var modeName = GameModeUtil.GetGameModeName(game.GetProperty("ModeID").GetString() ?? "");
        var mapName = MapUtil.GetMapName(game.GetProperty("MapID").GetString() ?? "");
        
        var blueTeam = new List<JsonElement>();
        var redTeam = new List<JsonElement>();
        foreach (var player in game.GetProperty("Players").EnumerateArray())
        {
            if (player.GetProperty("TeamID").GetString() == "Blue")
                blueTeam.Add(player);
            else
                redTeam.Add(player);
        }
        
        var allPuuids = blueTeam.Concat(redTeam)
            .Select(p => p.GetProperty("Subject").GetString() ?? "").ToList();
        
        var names = await _valorantApiService.ResolveNames(allPuuids);
        var mmr = await _valorantApiService.GetBatchMMR(allPuuids);

        var winRates = await GetPlayersWinRatesAsync(allPuuids);

        var blueTable = CreateTeamTable("Blue Team", "blue");
        FillTeamTable(blueTable, blueTeam, names, mmr, winRates);

        var redTable = CreateTeamTable("Red Team", "red");
        FillTeamTable(redTable, redTeam, names, mmr, winRates);

        _cachedMatchDisplay = Align.Center(new Rows(blueTable, redTable));
        _discordRpcService.UpdatePresence("In Game", $"{modeName} — {mapName}", "valradar_icon_1024");
        return _cachedMatchDisplay;
    }
    
    private Table CreateTeamTable(string title, string color)
    {
        return new Table()
            .Border(TableBorder.Rounded)
            .Title($"[{color}]{title}[/]")
            .AddColumn(new TableColumn("[cyan]Player[/]").Width(30))
            .AddColumn(new TableColumn("[cyan]Agent[/]").Width(12))
            .AddColumn(new TableColumn("[cyan]Rank[/]").Width(16))
            .AddColumn(new TableColumn("[cyan]Level[/]").Width(8))
            .AddColumn(new TableColumn("[cyan]WR[/]").Width(12));
    }
    private void FillTeamTable(Table table, List<JsonElement> players, Dictionary<string, string> names, Dictionary<string, JsonElement?> mmr, Dictionary<string, (int wins, int losses, int total)> winRates)
    {
        foreach (var player in players)
        {
            var puuid = player.GetProperty("Subject").GetString() ?? "";
            var identity = player.GetProperty("PlayerIdentity");
            var tier = ExtractTier(mmr, puuid);

            var incognito = identity.TryGetProperty("Incognito", out var inc) && inc.GetBoolean();
            var isSelf = puuid == _selfPuuid;
            var isParty = _partyMemberPuuids?.Contains(puuid) ?? false;

            var nameStr = (incognito && !isSelf && !isParty)
                ? "[dim italic]Hidden[/]"
                : (names.TryGetValue(puuid, out var n) ? n : puuid[..8]);
            var nameTag = isSelf ? $"[bold green]{nameStr}[/]" :
                isParty ? $"[bold cyan]{nameStr}[/]" :
                $"[white]{nameStr}[/]";

            var hideLevel = identity.TryGetProperty("HideAccountLevel", out var hl) && hl.GetBoolean();
            var level = (hideLevel && !isSelf && !isParty) ? -1 : identity.GetProperty("AccountLevel").GetInt32();

            var (gWins, gLosses, gTotal) = winRates.TryGetValue(puuid, out var r) ? r : (0, 0, 0);
            table.AddRow(
                nameTag,
                AgentUtil.GetAgentName(player.GetProperty("CharacterID").GetString() ?? ""),
                RankUtil.GetRankString(tier),
                level >= 0 ? $"{level}" : "[dim]Hidden[/]",
                FormatWinRateMarkup(gWins, gLosses, gTotal)
            );
        }
    }
    
    
    private static int ExtractTier(Dictionary<string, JsonElement?> mmr, string puuid)
        => mmr.TryGetValue(puuid, out var m) && m is { } data
            ? ValorantApiService.ExtractCurrentTier(data): 0;
    
    
    
}