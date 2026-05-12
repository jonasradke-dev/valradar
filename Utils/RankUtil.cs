namespace ValRadar.Util;

public class RankUtil
{
    public static string GetRankString(int tier) => tier switch
    {
        0 => "[grey]Unranked[/]",
        1 => "[grey]Unused 1[/]",
        2 => "[grey]Unused 2[/]",
        3 => "[dim]Iron 1[/]",
        4 => "[dim]Iron 2[/]",
        5 => "[dim]Iron 3[/]",
        6 => "[#b7a766]Bronze 1[/]",
        7 => "[#b7a766]Bronze 2[/]",
        8 => "[#b7a766]Bronze 3[/]",
        9 => "[silver]Silver 1[/]",
        10 => "[silver]Silver 2[/]",
        11 => "[silver]Silver 3[/]",
        12 => "[gold1]Gold 1[/]",
        13 => "[gold1]Gold 2[/]",
        14 => "[gold1]Gold 3[/]",
        15 => "[#21bfab]Platinum 1[/]",
        16 => "[#21bfab]Platinum 2[/]",
        17 => "[#21bfab]Platinum 3[/]",
        18 => "[#d042f5]Diamond 1[/]",
        19 => "[#d042f5]Diamond 2[/]",
        20 => "[#d042f5]Diamond 3[/]",
        21 => "[#2bba6e]Ascendant 1[/]",
        22 => "[#2bba6e]Ascendant 2[/]",
        23 => "[#2bba6e]Ascendant 3[/]",
        24 => "[#bb3d56]Immortal 1[/]",
        25 => "[#bb3d56]Immortal 2[/]",
        26 => "[#bb3d56]Immortal 3[/]",
        27 => "[bold #fffb54]Radiant[/]",
        _ => $"[grey]Unknown ({tier})[/]"
    };
}