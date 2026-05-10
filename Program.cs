using ValRadar.Services;

var lockfile = RiotService.GetLockfileData();

if (lockfile == null)
{
    Console.WriteLine("No lockfile found");
}

Console.WriteLine($"{lockfile.Port}:{lockfile.Password}");

string region, shard;
(region, shard) = RiotService.GetRegionAndShardFromShooterGame();

Console.WriteLine($"{region}:{shard}");