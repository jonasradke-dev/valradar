namespace ValRadar.Auth;

public interface ILockfileReader
{
    LockfileData Read();
}