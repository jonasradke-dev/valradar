using System.Text.Json;
using DiscordRPC.Logging;
using Newtonsoft.Json;
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
                return _cachedPartyDisplay!;

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
                .AddColumn(new TableColumn("[cyan]Ready[/]").Width(7));

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
                
                table.AddRow(nameTag, RankUtil.GetRankString(tier),$"{level}",
                    isReady ? "[green]Yes[/]" : "[red]No[/]");
        }
            
        _cachedPartyDisplay = Align.Center(table);
        _discordRpcService.UpdatePresence("In Lobby", "Waiting for match to start", "valradar_icon_1024");
        return _cachedPartyDisplay;
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
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold cyan]Agent Select - {mapName}[/]")
            .AddColumn(new TableColumn("[cyan]Player[/]").Width(30))
            .AddColumn(new TableColumn("[cyan]Agent[/]").Width(12))
            .AddColumn(new TableColumn("[cyan]Rank[/]").Width(16))
            .AddColumn(new TableColumn("[cyan]Level[/]").Width(8))
            .AddColumn(new TableColumn("[cyan]Status[/]").Width(10));

        foreach (var player in allyPlayers.EnumerateArray())
        {
            var puuid = player.GetProperty("Subject").GetString() ?? "";
            var tier = ExtractTier(mmr, puuid);
            var level = player.GetProperty("PlayerIdentity").GetProperty("AccountLevel").GetInt32();
            var agentId = player.GetProperty("CharacterID").GetString() ?? "";
            var selectionState = player.GetProperty("CharacterSelectionState").GetString();
            
            var name = names.TryGetValue(puuid, out var n) ? n : puuid[..8];
            var isSelf = puuid == _selfPuuid;
            var isParty = _partyMemberPuuids.Contains(puuid);
            var nameTag = isSelf ? $"[bold green]{name}[/]" :
                isParty ? $"[bold cyan]{name}[/]" :
                $"[white]{name}[/]";
            
            var statusTag = selectionState switch
            {
                "locked" => "[green]Locked[/]",
                "selected" => "[yellow]Selected[/]",
                _ => "[red]Not Selected[/]"
            };
            table.AddRow(nameTag, AgentUtil.GetAgentName(agentId), RankUtil.GetRankString(tier), $"{level}", statusTag);
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
        
        var blueTable = CreateTeamTable("Blue Team", "blue");
        FillTeamTable(blueTable, blueTeam, names, mmr);

        var redTable = CreateTeamTable("Red Team", "red");
        FillTeamTable(redTable, redTeam, names, mmr);

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
            .AddColumn(new TableColumn("[cyan]Level[/]").Width(8));
    }
    private void FillTeamTable(Table table, List<JsonElement> players, Dictionary<string, string> names, Dictionary<string, JsonElement?> mmr)
    {
        foreach (var player in players)
        {
            var puuid = player.GetProperty("Subject").GetString() ?? "";
            var identity = player.GetProperty("PlayerIdentity");
            var tier = ExtractTier(mmr, puuid);

            var incognito = identity.TryGetProperty("Incognito", out var inc) && inc.GetBoolean();
            var isSelf = puuid == _selfPuuid;
            var isParty = _partyMemberPuuids.Contains(puuid);

            var nameStr = (incognito && !isSelf && !isParty)
                ? "[dim italic]Hidden[/]"
                : (names.TryGetValue(puuid, out var n) ? n : puuid[..8]);
            var nameTag = isSelf ? $"[bold green]{nameStr}[/]" :
                isParty ? $"[bold cyan]{nameStr}[/]" :
                $"[white]{nameStr}[/]";

            var hideLevel = identity.TryGetProperty("HideAccountLevel", out var hl) && hl.GetBoolean();
            var level = (hideLevel && !isSelf && !isParty) ? -1 : identity.GetProperty("AccountLevel").GetInt32();

            table.AddRow(
                nameTag,
                AgentUtil.GetAgentName(player.GetProperty("CharacterID").GetString() ?? ""),
                RankUtil.GetRankString(tier),
                level >= 0 ? $"{level}" : "[dim]Hidden[/]"
            );
        }
    }
    
    
    private static int ExtractTier(Dictionary<string, JsonElement?> mmr, string puuid)
        => mmr.TryGetValue(puuid, out var m) && m is { } data
            ? ValorantApiService.ExtractCurrentTier(data): 0;
    
    
    
}