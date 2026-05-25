namespace ValRadar.Auth;

public class LockfileReader : ILockfileReader
{
    private readonly string _lockfilePath;
    
    public LockfileReader()
        : this(DefaultLockfilePath())
    {
    }
    public LockfileReader(string lockfilePath)
    {
        ArgumentNullException.ThrowIfNull(lockfilePath);
        _lockfilePath = lockfilePath;
    }

    public LockfileData Read()
    {
        if(!File.Exists(_lockfilePath))
            throw new FileNotFoundException($"Lockfile not found at {_lockfilePath}");

        string content;

        try
        {
            using var fileStream = new FileStream(
                _lockfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var streamReader = new StreamReader(fileStream);
            content = streamReader.ReadToEnd();

        }
        catch (IOException ex)
        {
            throw new LockfileReadException(_lockfilePath, ex);
        }
        
        return ParseLockfile(content);
        
    }

    private static LockfileData ParseLockfile(string content)
    {
        var parts = content.Split(":");
        if (parts.Length < 5)
            throw new InvalidDataException(
                $"Invalid lockfile format: {parts.Length} (expected 5 parts)");
        
        if(!int.TryParse(parts[2], out var port))
            throw new InvalidDataException(
                $"Lockfile port is not a valid integer: {parts[2]}");
        
        var password = parts[3];
        if(string.IsNullOrEmpty(password))
            throw new InvalidDataException(
                "Lockfile password is empty");
        
        return new LockfileData(port, password);
        
        
        
    }
    
    
    private static string DefaultLockfilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Riot Games", "Riot Client", "Config", "lockfile");
    }
    
}

public class LockfileReadException : Exception
{
    public LockfileReadException(string path, Exception inner)
        : base($"Failed to read lockfile at '{path}'.", inner)
    {
    }
}