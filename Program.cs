using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Rendering;
using ValRadar.Services;
using ValRadar.Util;

var lockfile = RiotService.GetLockfileData();

var authState = await RiotService.GetAuthState(lockfile);

var valoAPI = new ValorantApiService(authState);
await valoAPI.InitializeAsync();
AnsiConsole.Clear();

var width = 35;
var pad = (Console.WindowWidth - width) / 2;
var sp = new string(' ', Math.Max(pad, 0));

AnsiConsole.MarkupLine($"{sp}[bold red]╔═══════════════════════════════╗[/]");
AnsiConsole.MarkupLine($"{sp}[bold red]║[/]        [bold #ff4654]V A L R A D A R[/]        [bold red]║[/]");
AnsiConsole.MarkupLine($"{sp}[bold red]╚═══════════════════════════════╝[/]");
RiotService.GamePhase? lastPhase = null;
JsonElement? cachedMatchData = null;
string? cachedMatchId = null;
IRenderable? cachedMatchDisplay = null;
string? cachedPartyId = null;
IRenderable? cachedPartyDisplay = null;

// Hilfsmethode für Tabellen-Erstellung — vermeidet Code-Duplizierung
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

// Hilfsmethode für Spieler-Name mit Hidden-Check
string GetPlayerName(Dictionary<string, string> names, string puuid, JsonElement playerIdentity, string selfPuuid)
{
    bool incognito = playerIdentity.TryGetProperty("Incognito", out var inc) && inc.GetBoolean();
    bool hideLevel = playerIdentity.TryGetProperty("HideAccountLevel", out var hl) && hl.GetBoolean();
    
    if (incognito)
        return "[dim italic]Hidden[/]";
    
    var name = names.TryGetValue(puuid, out var n) ? n : puuid[..8];
    return puuid == selfPuuid ? $"[bold green]{name}[/]" : $"[white]{name}[/]";
}

int GetPlayerLevel(JsonElement playerIdentity)
{
    bool hideLevel = playerIdentity.TryGetProperty("HideAccountLevel", out var hl) && hl.GetBoolean();
    if (hideLevel) return -1;
    return playerIdentity.GetProperty("AccountLevel").GetInt32();
}

var gamePhase = await RiotService.GetCurrentGamePhase(lockfile, authState.ppuid);


await AnsiConsole.Live(new Text("Loading..."))
    .AutoClear(false)
    .StartAsync(async ctx =>
    {
        while (true)
        {
            var gamePhase = await RiotService.GetCurrentGamePhase(lockfile, authState.ppuid); 
            
            if (gamePhase != lastPhase)
            {
                lastPhase = gamePhase;
                cachedMatchId = null;
                cachedMatchDisplay = null;
                cachedPartyId = null;
                cachedPartyDisplay = null;
    
                // Konsole neu zeichnen
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine($"{sp}[bold red]╔═══════════════════════════════╗[/]");
                AnsiConsole.MarkupLine($"{sp}[bold red]║[/]        [bold #ff4654]V A L R A D A R[/]        [bold red]║[/]");
                AnsiConsole.MarkupLine($"{sp}[bold red]╚═══════════════════════════════╝[/]");
            }
            

            switch (gamePhase)
            {
                case RiotService.GamePhase.Menu:
                    cachedMatchId = null;
                    cachedMatchDisplay = null;

                    var partyData = await valoAPI.GetPartyData();
                    if (partyData is { } party)
                    {
                        // Party-ID aus der Response holen um Änderungen zu erkennen
                        var currentPartyMembers = new List<string>();
                        foreach (var member in party.GetProperty("Members").EnumerateArray())
                            currentPartyMembers.Add(member.GetProperty("Subject").GetString() ?? "");
                        var partyKey = string.Join(",", currentPartyMembers.OrderBy(x => x));

                        if (partyKey != cachedPartyId)
                        {
                            cachedPartyId = partyKey;

                            var names = await valoAPI.ResolveNames(currentPartyMembers);
                            var mmrByPuuid = await valoAPI.GetBatchMMR(currentPartyMembers);

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
                                var tier = mmrByPuuid.TryGetValue(puuid, out var mmr) && mmr is { } m
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
                        }

                        ctx.UpdateTarget(cachedPartyDisplay!);
                    }
                    break;

                case RiotService.GamePhase.PreGame:
                    var preGameData = await valoAPI.GetPreGameMatchData(authState.ppuid);
                    if (preGameData is { } preGame)
                    {
                        var allyPlayers = preGame.GetProperty("AllyTeam").GetProperty("Players");
                        var mapId = preGame.GetProperty("MapID").GetString() ?? "";
                        
                        var puuids = new List<string>();
                        foreach (var player in allyPlayers.EnumerateArray())
                            puuids.Add(player.GetProperty("Subject").GetString() ?? "");
                        
                        var names = await valoAPI.ResolveNames(puuids);
                        var mmrTasks = puuids.Select(id => valoAPI.GetPlayerMMR(id)).ToArray();
                        var mmrResults = await Task.WhenAll(mmrTasks);
                        var mmrByPuuid = puuids.Zip(mmrResults).ToDictionary(x => x.First, x => x.Second);

                        var table = new Table()
                            .Border(TableBorder.Rounded)
                            .Title($"[bold yellow]Agent Select[/]")
                            .AddColumn("[cyan]Player[/]")
                            .AddColumn("[cyan]Agent[/]")
                            .AddColumn("[cyan]Rank[/]")
                            .AddColumn("[cyan]Level[/]")
                            .AddColumn("[cyan]Status[/]");
                        
                        foreach (var player in allyPlayers.EnumerateArray())
                        {
                            var puuid = player.GetProperty("Subject").GetString() ?? "";
                            var tier = mmrByPuuid.TryGetValue(puuid, out var mmr) && mmr is { } m
                                ? ValorantApiService.ExtractCurrentTier(m) : 0;
                            var level = player.GetProperty("PlayerIdentity")
                                .GetProperty("AccountLevel").GetInt32();
                            var agentId = player.GetProperty("CharacterID").GetString() ?? "";
                            var agentName = AgentUtil.GetAgentName(agentId);
                            var selectionState = player.GetProperty("CharacterSelectionState").GetString();

                            var name = names.TryGetValue(puuid, out var n) ? n : puuid[..8];
                            var isSelf = puuid == authState.ppuid;
                            var nameTag = isSelf ? $"[bold green]{name}[/]" : $"[white]{name}[/]";

                            var statusTag = selectionState switch
                            {
                                "locked"   => "[green]Locked[/]",
                                "selected" => "[yellow]Picking[/]",
                                _          => "[grey]...[/]"
                            };

                            table.AddRow(nameTag, agentName, RankUtil.GetRankString(tier), $"{level}", statusTag);
                        }
                        ctx.UpdateTarget(Align.Center(table));
                    }
                    else
                    {
                        
                    }
                    break;

                case RiotService.GamePhase.InGame:
                    var currentGameData = await valoAPI.GetCurrentGameData(authState.ppuid);
                    if (currentGameData is { } currentGame)
                    {
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

                            var allPuuids = blueTeam.Concat(redTeam)
                                .Select(p => p.GetProperty("Subject").GetString() ?? "").ToList();
                            var names = await valoAPI.ResolveNames(allPuuids);
                            var mmrByPuuid = await valoAPI.GetBatchMMR(allPuuids);

                            var blueTable = CreatePlayerTable("Blue Team", "blue");
                            foreach (var player in blueTeam)
                            {
                                var puuid = player.GetProperty("Subject").GetString() ?? "";
                                var identity = player.GetProperty("PlayerIdentity");
                                var tier = mmrByPuuid.TryGetValue(puuid, out var mmr) && mmr is { } m
                                    ? ValorantApiService.ExtractCurrentTier(m) : 0;
                                var level = GetPlayerLevel(identity);

                                blueTable.AddRow(
                                    GetPlayerName(names, puuid, identity, authState.ppuid),
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
                                var tier = mmrByPuuid.TryGetValue(puuid, out var mmr) && mmr is { } m
                                    ? ValorantApiService.ExtractCurrentTier(m) : 0;
                                var level = GetPlayerLevel(identity);

                                redTable.AddRow(
                                    GetPlayerName(names, puuid, identity, authState.ppuid),
                                    AgentUtil.GetAgentName(player.GetProperty("CharacterID").GetString() ?? ""),
                                    RankUtil.GetRankString(tier),
                                    level >= 0 ? $"{level}" : "[dim]Hidden[/]"
                                );
                            }

                            cachedMatchDisplay = Align.Center(new Rows(blueTable, redTable));
                        }
                        ctx.UpdateTarget(cachedMatchDisplay!);
                    }
                    break;

                default:
                    ctx.UpdateTarget(new Markup("[grey]Waiting for Valorant...[/]"));
                    break;
            }

            await Task.Delay(5000);
        }
    });