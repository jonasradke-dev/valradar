using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Rendering;
using ValRadar.Services;
using ValRadar.Util;
using ValRadar.Utils;

var lockfile = RiotService.GetLockfileData();
if (lockfile is null) return;

var authState = await RiotService.GetAuthState(lockfile);
if (authState is null) return;

var valoAPI = new ValorantApiService(authState);
await valoAPI.InitializeAsync();

var DiscordPresence = new DiscordRPCService("1505606528504303677");
DiscordPresence.Initialize();
DiscordPresence.UpdatePresence("ValRadar", "Loading...", "valradar_icon_1024");

AnsiConsole.Clear();
var width = 35;
var pad = (Console.WindowWidth - width) / 2;
var sp = new string(' ', Math.Max(pad, 0));

AnsiConsole.MarkupLine($"{sp}[bold red]╔═══════════════════════════════╗[/]");
AnsiConsole.MarkupLine($"{sp}[bold red]║[/]        [bold #ff4654]V A L R A D A R[/]        [bold red]║[/]");
AnsiConsole.MarkupLine($"{sp}[bold red]╚═══════════════════════════════╝[/]");

// WebSocket
var wsService = new RiotWebSocketService(lockfile, authState.Puuid);
await wsService.ConnectAsync();

var initialPresence = await RiotService.GetCurrentGamePhase(lockfile, authState.Puuid);
string currentPhase = initialPresence switch
{
    RiotService.GamePhase.Menu => "MENUS",
    RiotService.GamePhase.PreGame => "PREGAME",
    RiotService.GamePhase.InGame => "INGAME",
    _ => "MENUS"
};

string? lastPhase = null;
var phaseChanged = new SemaphoreSlim(0);

wsService.OnGamePhaseChanged += phase =>
{
    if (phase != currentPhase)
    {
        currentPhase = phase;
        phaseChanged.Release();
    }
};

_ = Task.Run(() => wsService.ListenAsync());


string? cachedMatchId = null;
IRenderable? cachedMatchDisplay = null;
string? cachedPartyId = null;
IRenderable? cachedPartyDisplay = null;
var partyMemberPuuids = new List<string>();


Table CreatePlayerTable(string title, string titleColor)
{
    return new Table()
        .Border(TableBorder.Rounded)
        .Title($"[{titleColor}]{title}[/]")
        .AddColumn(new TableColumn("[cyan]Player[/]").Width(30))
        .AddColumn(new TableColumn("[cyan]Agent[/]").Width(12))
        .AddColumn(new TableColumn("[cyan]Rank[/]").Width(16))
        .AddColumn(new TableColumn("[cyan]Level[/]").Width(8));
}

string GetPlayerName(Dictionary<string, string> names, string puuid, JsonElement playerIdentity, string selfpuuid, List<string> partyMembers)
{
    bool incognito = playerIdentity.TryGetProperty("Incognito", out var inc) && inc.GetBoolean();
    
    bool isSelf = puuid == selfpuuid;
    bool isPartyMember = partyMembers.Contains(puuid);
    
    if (incognito && !isSelf && !isPartyMember)
        return "[dim italic]Hidden[/]";
    
    var name = names.TryGetValue(puuid, out var n) ? n : puuid[..8];
    return isSelf ? $"[bold green]{name}[/]" : $"[white]{name}[/]";
}


int GetPlayerLevel(JsonElement playerIdentity, string puuid, string selfpuuid, List<string> partyMembers)
{
    bool hideLevel = playerIdentity.TryGetProperty("HideAccountLevel", out var hl) && hl.GetBoolean();
    bool isSelf = puuid == selfpuuid;
    bool isPartyMember = partyMembers.Contains(puuid);
    
    if (hideLevel && !isSelf && !isPartyMember)
        return -1;
        
    return playerIdentity.GetProperty("AccountLevel").GetInt32();
}




await AnsiConsole.Live(new Text("Loading..."))
    .AutoClear(false)
    .StartAsync(async ctx =>
    {
        while (true)
        {
            if (currentPhase != lastPhase)
            {
                lastPhase = currentPhase;
                cachedMatchId = null;
                cachedMatchDisplay = null;
                cachedPartyId = null;
                cachedPartyDisplay = null;

                AnsiConsole.Clear();
                AnsiConsole.MarkupLine($"{sp}[bold red]╔═══════════════════════════════╗[/]");
                AnsiConsole.MarkupLine($"{sp}[bold red]║[/]        [bold #ff4654]V A L R A D A R[/]        [bold red]║[/]");
                AnsiConsole.MarkupLine($"{sp}[bold red]╚═══════════════════════════════╝[/]");
            }

            var phase = currentPhase switch
            {
                "MENUS" => RiotService.GamePhase.Menu,
                "PREGAME" => RiotService.GamePhase.PreGame,
                "INGAME" => RiotService.GamePhase.InGame,
                _ => RiotService.GamePhase.Unknown
            };

            switch (phase)
            {
                case RiotService.GamePhase.Menu:
                    var partyData = await valoAPI.GetPartyData();
                    if (partyData is { } party)
                    {
                        var currentPartyMembers = new List<string>();
                        foreach (var member in party.GetProperty("Members").EnumerateArray())
                            currentPartyMembers.Add(member.GetProperty("Subject").GetString() ?? "");
                        var partyKey = string.Join(",", currentPartyMembers.OrderBy(x => x));

                        if (partyKey != cachedPartyId)
                        {
                            cachedPartyId = partyKey;
                            var names = await valoAPI.ResolveNames(currentPartyMembers);
                            var mmrBypuuid = await valoAPI.GetBatchMMR(currentPartyMembers);

                            var table = new Table()
                                .Border(TableBorder.Rounded)
                                .Title("[bold yellow]Party Lobby[/]")
                                .AddColumn(new TableColumn("[cyan]Player[/]").Width(22))
                                .AddColumn(new TableColumn("[cyan]Rank[/]").Width(12))
                                .AddColumn(new TableColumn("[cyan]Level[/]").Width(7))
                                .AddColumn(new TableColumn("[cyan]Ready[/]").Width(7));

                            foreach (var member in party.GetProperty("Members").EnumerateArray())
                            {
                                var puuid = member.GetProperty("Subject").GetString() ?? "";
                                var tier = mmrBypuuid.TryGetValue(puuid, out var mmr) && mmr is { } m
                                    ? ValorantApiService.ExtractCurrentTier(m) : 0;
                                var identity = member.GetProperty("PlayerIdentity");
                                var level = identity.GetProperty("AccountLevel").GetInt32();
                                var isReady = member.GetProperty("IsReady").GetBoolean();
                                var isOwner = member.TryGetProperty("IsOwner", out var o) && o.GetBoolean();

                                var name = names.TryGetValue(puuid, out var n) ? n : puuid[..8];
                                var nameTag = isOwner ? $"[bold gold1]{name} (Owner)[/]" : $"[white]{name}[/]";
                                var readyTag = isReady ? "[green]Yes[/]" : "[red]No[/]";

                                table.AddRow(nameTag, RankUtil.GetRankString(tier), $"{level}", readyTag);
                            }
                            cachedPartyDisplay = Align.Center(table);
                            DiscordPresence.UpdatePresence("In Party Lobby", $"{currentPartyMembers.Count} player(s)", "valradar_icon_1024");
                        }
                        ctx.UpdateTarget(cachedPartyDisplay!);
                    }
                    break;

                case RiotService.GamePhase.PreGame:
                    var preGameData = await valoAPI.GetPreGameMatchData(authState.Puuid);
                    if (preGameData is { } preGame)
                    {
                        var allyPlayers = preGame.GetProperty("AllyTeam").GetProperty("Players");
                        var puuids = new List<string>();
                        foreach (var player in allyPlayers.EnumerateArray())
                            puuids.Add(player.GetProperty("Subject").GetString() ?? "");

                        var names = await valoAPI.ResolveNames(puuids);
                        var mmrBypuuid = await valoAPI.GetBatchMMR(puuids);
                        
                        var mapId = preGame.GetProperty("MapID").GetString() ?? "";
                        var MapName = MapUtil.GetMapName(mapId);

                        var table = new Table()
                            .Border(TableBorder.Rounded)
                            .Title("[bold yellow]Agent Select[/]")
                            .AddColumn(new TableColumn("[cyan]Player[/]").Width(30))
                            .AddColumn(new TableColumn("[cyan]Agent[/]").Width(12))
                            .AddColumn(new TableColumn("[cyan]Rank[/]").Width(16))
                            .AddColumn(new TableColumn("[cyan]Level[/]").Width(8))
                            .AddColumn(new TableColumn("[cyan]Status[/]").Width(10));

                        foreach (var player in allyPlayers.EnumerateArray())
                        {
                            var puuid = player.GetProperty("Subject").GetString() ?? "";
                            var tier = mmrBypuuid.TryGetValue(puuid, out var mmr) && mmr is { } m
                                ? ValorantApiService.ExtractCurrentTier(m) : 0;
                            var level = player.GetProperty("PlayerIdentity").GetProperty("AccountLevel").GetInt32();
                            var agentId = player.GetProperty("CharacterID").GetString() ?? "";
                            var selectionState = player.GetProperty("CharacterSelectionState").GetString();

                            var name = names.TryGetValue(puuid, out var n) ? n : puuid[..8];
                            var isSelf = puuid == authState.Puuid;
                            var nameTag = isSelf ? $"[bold green]{name}[/]" : $"[white]{name}[/]";
                            var statusTag = selectionState switch
                            {
                                "locked" => "[green]Locked[/]",
                                "selected" => "[yellow]Picking[/]",
                                _ => "[grey]...[/]"
                            };

                            table.AddRow(nameTag, AgentUtil.GetAgentName(agentId), RankUtil.GetRankString(tier), $"{level}", statusTag);
                            DiscordPresence.UpdatePresence("In Agent Select", $"Map:{MapName}", "valradar_icon_1024");
                        }
                        ctx.UpdateTarget(Align.Center(table));
                    }
                    else
                    {
                        //ctx.UpdateTarget(new Markup("[yellow]Loading agent select...[/]"));
                    }
                    break;

                case RiotService.GamePhase.InGame:
                    var currentGameData = await valoAPI.GetCurrentGameData(authState.Puuid);
                    if (currentGameData is { } currentGame)
                    {
                        var gameModeId = currentGame.GetProperty("ModeID").GetString();
                        var GameModeName = GameModeUtil.GetGameModeName(gameModeId ?? "");
                        
                        var matchID = currentGame.GetProperty("MatchID").GetString();
                        if (matchID != cachedMatchId)
                        {
                            cachedMatchId = matchID;
                            var blueTeam = new List<JsonElement>();
                            var redTeam = new List<JsonElement>();

                            foreach (var player in currentGame.GetProperty("Players").EnumerateArray())
                            {
                                if (player.GetProperty("TeamID").GetString() == "Blue")
                                    blueTeam.Add(player);
                                else
                                    redTeam.Add(player);
                            }

                            var allpuuids = blueTeam.Concat(redTeam)
                                .Select(p => p.GetProperty("Subject").GetString() ?? "").ToList();
                            var names = await valoAPI.ResolveNames(allpuuids);
                            var mmrBypuuid = await valoAPI.GetBatchMMR(allpuuids);

                            var blueTable = CreatePlayerTable("Blue Team", "blue");
                            foreach (var player in blueTeam)
                            {
                                var puuid = player.GetProperty("Subject").GetString() ?? "";
                                var identity = player.GetProperty("PlayerIdentity");
                                var tier = mmrBypuuid.TryGetValue(puuid, out var mmr) && mmr is { } m
                                    ? ValorantApiService.ExtractCurrentTier(m) : 0;
                                var level = GetPlayerLevel(identity, puuid, authState.Puuid, partyMemberPuuids);
                                blueTable.AddRow(
                                    GetPlayerName(names, puuid, identity, authState.Puuid, partyMemberPuuids),
                                    AgentUtil.GetAgentName(player.GetProperty("CharacterID").GetString() ?? ""),
                                    RankUtil.GetRankString(tier),
                                    level >= 0 ? $"{level}" : "[dim]Hidden[/]"
                                );
                            }

                            var redTable = CreatePlayerTable("Red Team", "red");
                            foreach (var player in redTeam)
                            {
                                var puuid = player.GetProperty("Subject").GetString() ?? "";
                                var identity = player.GetProperty("PlayerIdentity");
                                var tier = mmrBypuuid.TryGetValue(puuid, out var mmr) && mmr is { } m
                                    ? ValorantApiService.ExtractCurrentTier(m) : 0;
                                var level = GetPlayerLevel(identity, puuid, authState.Puuid, partyMemberPuuids);
                                redTable.AddRow(
                                    GetPlayerName(names, puuid, identity, authState.Puuid, partyMemberPuuids),
                                    AgentUtil.GetAgentName(player.GetProperty("CharacterID").GetString() ?? ""),
                                    RankUtil.GetRankString(tier),
                                    level >= 0 ? $"{level}" : "[dim]Hidden[/]"
                                );
                            }
                            cachedMatchDisplay = Align.Center(new Rows(blueTable, redTable));
                        }
                        ctx.UpdateTarget(cachedMatchDisplay!);
                        DiscordPresence.UpdatePresence("In Game", $"{GameModeName}", "valradar_icon_1024");
                    }
                    break;
 
                default:
                    ctx.UpdateTarget(new Markup("[grey]Waiting for Valorant...[/]"));
                    break;
            }

            await phaseChanged.WaitAsync(TimeSpan.FromSeconds(10));
        }
    });