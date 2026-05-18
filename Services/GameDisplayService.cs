using System.Text.Json;
using DiscordRPC.Logging;
using Newtonsoft.Json;
using Spectre.Console;
using Spectre.Console.Rendering;
using ValRadar.Util;

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
            if (partyId != _cachedPartyId) 
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
                var nameTag = isOwner ? $"[bold gold]{name}[/]" : $"[white]{name}[/]";
                
                table.AddRow(nameTag, RankUtil.GetRankString(tier),$"{level}",
                    isReady ? "[green]Yes[/]" : "[red]No[/]");
        }
            
        _cachedPartyDisplay = Align.Center(table);
        _discordRpcService.UpdatePresence("In Lobby", "Waiting for match to start", "valradar_icon_1024");
        return _cachedPartyDisplay;
    
    }
    private static int ExtractTier(Dictionary<string, JsonElement?> mmr, string puuid)
        => mmr.TryGetValue(puuid, out var m) && m is { } data
            ? ValorantApiService.ExtractCurrentTier(data): 0;
    
    
}