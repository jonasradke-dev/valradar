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
}