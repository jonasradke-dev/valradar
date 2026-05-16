namespace ValRadar.Models;

public class AuthState
{
    public required string Puuid { get; set; }
    public required string AuthToken { get; set; }
    public required string EntitlementToken { get; set; }
    public required string Region { get; set; }
    public required string Shard { get; set; }
    //public string ClientVersion { get; set; }
    public required RiotLockfileData LockfileData { get; set; }
}