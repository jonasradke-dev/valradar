using Spectre.Console;
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

await AnsiConsole.Live(new Text("Loading..."))
    .AutoClear(false)
    .StartAsync(async ctx =>
    {
        while (true)
        {
            var gamePhase = await RiotService.GetCurrentGamePhase(lockfile, authState.ppuid);

            switch (gamePhase)
            {
                case RiotService.GamePhase.Menu:
                    var partyData = await valoAPI.GetPartyData();
                    if (partyData is { } party)
                    {
                        var puuids = new List<string>();
                        foreach (var member in party.GetProperty("Members").EnumerateArray())
                            puuids.Add(member.GetProperty("Subject").GetString() ?? "");

                        var names = await valoAPI.ResolveNames(puuids);
                        var mmrTasks = puuids.Select(id => valoAPI.GetPlayerMMR(id)).ToArray();
                        var mmrResults = await Task.WhenAll(mmrTasks);
                        var mmrByPuuid = puuids.Zip(mmrResults).ToDictionary(x => x.First, x => x.Second);

                        var table = new Table()
                            .Border(TableBorder.Rounded)
                            .Title("[bold yellow]Party Lobby[/]")
                            .AddColumn("[cyan]Player[/]")
                            .AddColumn("[cyan]Rank[/]")
                            .AddColumn("[cyan]Level[/]")
                            .AddColumn("[cyan]Ready[/]");

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

                        ctx.UpdateTarget(Align.Center(table));
                    }
                    break;

                case RiotService.GamePhase.PreGame:
                    ctx.UpdateTarget(new Markup("[yellow]Agent Select...[/]"));
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
                    break;

                case RiotService.GamePhase.InGame:
                    ctx.UpdateTarget(new Markup("[green]In Game[/]"));
                    break;

                default:
                    ctx.UpdateTarget(new Markup("[grey]Waiting for Valorant...[/]"));
                    break;
            }

            await Task.Delay(5000);
        }
    });