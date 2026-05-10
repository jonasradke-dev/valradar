using System.Text.RegularExpressions;
using ValRadar.Models;

namespace ValRadar.Services;

public class RiotService
{
    public static RiotLockfileData? GetLockfileData()
    {
        string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string lockfilePath = Path.Combine(localAppDataPath, "Riot Games", "Riot Client", "Config", "lockfile");

        if (!File.Exists(lockfilePath))
        {
            Console.WriteLine("Could not find lockfile. Is Valorant running?");
        }

        try
        {
            using FileStream fs = new FileStream(lockfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new StreamReader(fs);
            string lockfileContent = reader.ReadToEnd();
            string[] lockfileParts = lockfileContent.Split(':');

            return new RiotLockfileData()
            {
                Port = lockfileParts[2],
                Password = lockfileParts[3],

            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading lockfile: {ex.Message}");
            return null;
        }
    }

    public static (string region, string shard) GetRegionAndShardFromShooterGame()
    {
        string pattern = @"https://glz-(.+?)-1\.(.+?)\.a\.pvp\.net";
        Regex regex = new Regex(pattern);
        string shooterGameLogPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) +
                                    @"\VALORANT\Saved\Logs\ShooterGame.log";

        if (!File.Exists(shooterGameLogPath))
        {
            Console.WriteLine("Could not find ShooterGame.log. Is Valorant running?");
            Console.WriteLine("Defaulting to EU shard and region");
            return ("eu", "eu");
        }

        using FileStream fs = new FileStream(shooterGameLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using (StreamReader reader = new StreamReader(fs))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                Match match = regex.Match(line);
                if (match.Success)
                {
                    string region = match.Groups[1].Value;
                    string shard = match.Groups[2].Value;
                    return (region, shard);
                }
            }
        }
        Console.WriteLine("Warning: Couldn't parse region from log defaulting to EU shard and region");
        return ("eu", "eu");
    }
}