using ValRadar.Models;

namespace ValRadar.Auth;

public interface IRiotAuthService
{
    AuthState Current  { get; }
    Task RefreshAsync(CancellationToken cancellationToken = default);
}