using System.Text.RegularExpressions;

namespace ValRadar.Auth;

public class RegionResolver
{
    private static readonly Regex GlzUrlPattern = new(
        @"https://glz-(.+?)-1\.(.+?)\.a\.pvp\.net",
        RegexOptions.Compiled);
    
    private readonly string _shooterGameLogPath;

    public RegionResolver()
        : this(DefaultLogFilePath())
    {
    }

    public RegionResolver(string logFilePath)
    {
        ArgumentNullException.ThrowIfNull(logFilePath);
        _shooterGameLogPath = logFilePath;
    }

    public ShooterGameFileData Resolve()
    {
        if (!File.Exists(_shooterGameLogPath))
            throw  new FileNotFoundException($"The log file {_shooterGameLogPath} does not exist.");

        string content;

        try
        {
            using var fileStream = new FileStream(
                _shooterGameLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var streamReader = new StreamReader(fileStream);
            return ParseLogFile(streamReader);
        }
        catch (IOException exception)
        {
            throw new ShooterGameFileReadException(_shooterGameLogPath, exception);
        }
    }

    private static ShooterGameFileData ParseLogFile(StreamReader streamReader)
    {
        
        string? line;
        while ((line = streamReader.ReadLine()) != null)
        {
            var match = GlzUrlPattern.Match(line);
            if (match.Success)
                return new ShooterGameFileData(match.Groups[1].Value, match.Groups[2].Value);
        }

        throw new InvalidDataException(
            "Couldn't parse the log file at this location. Make sure Valorant was started at least once"
        );

    }
    

    private static string DefaultLogFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "VALORANT", "Saved", "Logs", "ShooterGame.log");

    }
}
                                                
public class ShooterGameFileReadException(string path, Exception inner)
    : Exception($"Failed to read ShooterGame.log at '{path}'.", inner);