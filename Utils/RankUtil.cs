namespace ValRadar.Util;

public class RankUtil
{
    public static string GetRankString(int tier) => tier switch
    {
        0 => "[grey]Unranked[/]",
        1 or 2 => "[grey]Unused[/]",
        3 => "[#4f514f]Iron 1[/]",
        4 => "[#4f514f]Iron 2[/]",
        5 => "[#4f514f]Iron 3[/]",
        6 => "[#a5855d]Bronze 1[/]",
        7 => "[#a5855d]Bronze 2[/]",
        8 => "[#a5855d]Bronze 3[/]",
        9 => "[#bbc2c2]Silver 1[/]",
        10 => "[#bbc2c2]Silver 2[/]",
        11 => "[#bbc2c2]Silver 3[/]",
        12 => "[#eccf56]Gold 1[/]",
        13 => "[#eccf56]Gold 2[/]",
        14 => "[#eccf56]Gold 3[/]",
        15 => "[#59a9b6]Platinum 1[/]",
        16 => "[#59a9b6]Platinum 2[/]",
        17 => "[#59a9b6]Platinum 3[/]",
        18 => "[#b489c4]Diamond 1[/]",
        19 => "[#b489c4]Diamond 2[/]",
        20 => "[#b489c4]Diamond 3[/]",
        21 => "[#6ae2af]Ascendant 1[/]",
        22 => "[#6ae2af]Ascendant 2[/]",
        23 => "[#6ae2af]Ascendant 3[/]",
        24 => "[#bb3d65]Immortal 1[/]",
        25 => "[#bb3d65]Immortal 2[/]",
        26 => "[#bb3d65]Immortal 3[/]",
        27 => "[bold #ffffaa]Radiant[/]",
        _ => $"[grey]Unknown ({tier})[/]"
    };
}