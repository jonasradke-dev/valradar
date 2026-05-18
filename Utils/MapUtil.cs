namespace ValRadar.Util;

public class MapUtil
{
    public static readonly Dictionary<string, string> Maps = new()
    {
        ["/Game/Maps/Ascent/Ascent"] = "Ascent",
        ["/Game/Maps/Bonsai/Bonsai"] = "Split",
        ["/Game/Maps/Canyon/Canyon"] = "Fracture",
        ["/Game/Maps/Duality/Duality"] = "Bind",
        ["/Game/Maps/Foxtrot/Foxtrot"] = "Breeze",
        ["/Game/Maps/HURM/HURM_Alley/HURM_Alley"] = "District",
        ["/Game/Maps/HURM/HURM_Bowl/HURM_Bowl"] = "Kasbah",
        ["/Game/Maps/HURM/HURM_Helix/HURM_Helix"] = "Drift",
        ["/Game/Maps/HURM/HURM_HighTide/HURM_HighTide"] = "Glitch",
        ["/Game/Maps/HURM/HURM_Yard/HURM_Yard"] = "Piazza",
        ["/Game/Maps/Infinity/Infinity"] = "Abyss",
        ["/Game/Maps/Jam/Jam"] = "Lotus",
        ["/Game/Maps/Juliett/Juliett"] = "Sunset",
        ["/Game/Maps/Pitt/Pitt"] = "Pearl",
        ["/Game/Maps/Port/Port"] = "Icebox",
        ["/Game/Maps/Triad/Triad"] = "Haven",
        ["/Game/Maps/Rook/Rook"] = "Corrode",
        ["/Game/Maps/Poveglia/Range"] = "The Range"
    };
    public static string GetMapName(string mapUrl)
        => Maps.TryGetValue(mapUrl, out var name) ? name : mapUrl;
}