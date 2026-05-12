namespace ValRadar.Models;

public class AuthState
{
    public string ppuid { get; set; }
    public string AuthToken { get; set; }
    public string EntitlementToken { get; set; }
    public string Region { get; set; }
    public string Shard { get; set; }
    //public string ClientVersion { get; set; }
    public RiotLockfileData LockfileData { get; set; }
}