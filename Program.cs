using ValRadar.Services;

var lockfile = RiotService.GetLockfileData();

if (lockfile == null)
{
    Console.WriteLine("No lockfile found");
}

Console.WriteLine($"{lockfile.Port}:{lockfile.Password}");