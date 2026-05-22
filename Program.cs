using Spectre.Console;
using ValRadar.Services;
var lockfile = RiotService.GetLockfileData();
if (lockfile is null) return;

var authState = await RiotService.GetAuthState(lockfile);
if (authState is null) return;

var valoAPI = new ValorantApiService(authState);
await valoAPI.InitializeAsync();

var discordPresence = new DiscordRPCService("1505606528504303677");
discordPresence.Initialize();

var display = new GameDisplayService(valoAPI, discordPresence, authState.Puuid);

AnsiConsole.Clear();
var width = 35;
var pad = (Console.WindowWidth - width) / 2;
var sp = new string(' ', Math.Max(pad, 0));

void DrawHeader() 
{
    AnsiConsole.MarkupLine($"{sp}[bold red]╔═══════════════════════════════╗[/]");
    AnsiConsole.MarkupLine($"{sp}[bold red]║[/]        [bold #ff4654]V A L R A D A R[/]        [bold red]║[/]");
    AnsiConsole.MarkupLine($"{sp}[bold red]╚═══════════════════════════════╝[/]");
}

DrawHeader();

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



await AnsiConsole.Live(new Text("Loading..."))
    .AutoClear(false)
    .StartAsync(async ctx =>
    {
        while (true)
        {
            if (currentPhase != lastPhase)
            {
                lastPhase = currentPhase;
                display.ResetCache();
                AnsiConsole.Clear();
                DrawHeader();
            }

            var phase = currentPhase switch
            {
                "MENUS" => await display.RenderMenuAsync(),
                "PREGAME" => await display.RenderPreGameAsync(),
                "INGAME" => await display.RenderInGameAsync(),
                _ => new Markup("[grey]Waiting for Valorant...[/]")
            };
            ctx.UpdateTarget(phase);
            await phaseChanged.WaitAsync(TimeSpan.FromSeconds(10));
        }
    });