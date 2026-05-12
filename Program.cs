using Spectre.Console;
using ValRadar.Services;
using ValRadar.Util;


AnsiConsole.MarkupLine("[bold red]╔═══════════════════════════════╗[/]");
AnsiConsole.MarkupLine("[bold red]║[/]     [bold #ff4654]V A L R A D A R[/]           [bold red]║[/]");
AnsiConsole.MarkupLine("[bold red]╚═══════════════════════════════╝[/]");
AnsiConsole.WriteLine();
var lockfile = RiotService.GetLockfileData();

var authState = await RiotService.GetAuthState(lockfile);

var valoAPI = new ValorantApiService(authState);
await valoAPI.InitializeAsync();

var partyData = await valoAPI.GetPartyData();
if (partyData is { } party)
{
    var puuids = new List<string>();
    foreach (var member in party.GetProperty("Members").EnumerateArray())
        puuids.Add(member.GetProperty("Subject").GetString() ?? "");

    var names = await valoAPI.ResolveNames(puuids);

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
        var mmr = await valoAPI.GetPlayerMMR(puuid);
        var tier = mmr is { } m ? ValorantApiService.ExtractCurrentTier(m) : 0;
        var identity = member.GetProperty("PlayerIdentity");
        var level = identity.GetProperty("AccountLevel").GetInt32();
        var isReady = member.GetProperty("IsReady").GetBoolean();
        var isOwner = member.TryGetProperty("IsOwner", out var o) && o.GetBoolean();

        var name = names.TryGetValue(puuid, out var n) ? n : puuid[..8];
        var nameTag = isOwner ? $"[bold gold1]{name} (Owner)[/]" : $"[white]{name}[/]";
        var readyTag = isReady ? "[green]Yes[/]" : "[red]No[/]";

        table.AddRow(nameTag, RankUtil.GetRankString(tier), $"{level}", readyTag);
    }

    AnsiConsole.Write(table);
}
 

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
