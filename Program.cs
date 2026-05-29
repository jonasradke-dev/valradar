using Spectre.Console;
using ValRadar.Auth;
using ValRadar.Services;


try
{
    var lockFileReader = new LockfileReader();
    var regionResolver = new RegionResolver();
    using var localHttpClient = RiotLocalClientFactory.CreateClient();
    
    var authService = await RiotAuthService.CreateAsync(
        lockFileReader,
        regionResolver,
        localHttpClient);
    
    using var valoAPI = new ValorantApiService(authService);
    await valoAPI.InitializeAsync();
    
    var discordPresense = new DiscordRPCService("1505606528504303677");
    discordPresense.Initialize();
    
    var display = new GameDisplayService(valoAPI, discordPresense, authService.Current.Puuid);
    
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
    
    var wsService = new RiotWebSocketService(authService.Current.LockfileData, authService.Current.Puuid);
    await wsService.ConnectAsync();


    var initialPresence =
        await RiotService.GetCurrentGamePhase(authService.Current.LockfileData, authService.Current.Puuid);
    
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
    
    //TODO: exceptions in ListenAsync are silently swallowed.
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
    
}catch(Exception ex)
{
    AnsiConsole.WriteException(ex);
}










