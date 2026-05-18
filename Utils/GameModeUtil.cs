namespace ValRadar.Utils;

public static class GameModeUtil
{
    private static readonly Dictionary<string, string> GameModes = new()
    {
        ["/Game/GameModes/Bomb/BombGameMode.BombGameMode_C"] = "Standard",
        ["/Game/GameModes/QuickBomb/QuickBombGameMode.QuickBombGameMode_C"] = "Spike Rush",
        ["/Game/GameModes/Deathmatch/DeathmatchGameMode.DeathmatchGameMode_C"] = "Deathmatch",
        ["/Game/GameModes/GunGame/GunGameTeamsGameMode.GunGameTeamsGameMode_C"] = "Escalation",
        ["/Game/GameModes/OneForAll/OneForAll_GameMode.OneForAll_GameMode_C"] = "Replication",
        ["/Game/GameModes/HURM/HURM.HURM_C"] = "Team Deathmatch",
        ["/Game/GameModes/_Development/Swiftplay_EndOfRoundCredits/SwiftPlay_GameMode.SwiftPlay_GameMode_C"] = "Swiftplay",
        ["/Game/GameModes/AROS/AROS_GameMode.AROS_GameMode_C"] = "All Random One Site",
        ["/Game/GameModes/Dodgeball/Dodgeball_GameMode.Dodgeball_GameMode_C"] = "Knockout",
        ["/Game/GameModes/SnowballFight/SnowballFightGameMode.SnowballFightGameMode_C"] = "Snowball Fight",
        ["/Game/GameModes/Skirmish/SkirmishGameMode.SkirmishGameMode_C"] = "Skirmish",
        ["/Game/GameModes/ShootingRange/ShootingRangeGameMode.ShootingRangeGameMode_C"] = "The Range",
    };

    public static string GetGameModeName(string assetPath)
        => GameModes.TryGetValue(assetPath, out var name) ? name : assetPath;
}