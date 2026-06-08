namespace ValRadar.Http;

public class RateLimitHandler : DelegatingHandler
{
    private readonly TimeSpan _delay;
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly object _lock = new object();

    public RateLimitHandler(TimeSpan delay)
    {
        _delay = delay;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        TimeSpan delayNeeded;
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (_lastRequestTime < now)
            {
                _lastRequestTime = now;
            }
            delayNeeded = _lastRequestTime - now;
            _lastRequestTime = _lastRequestTime.Add(_delay);
        }

        if (delayNeeded > TimeSpan.Zero)
        {
            await Task.Delay(delayNeeded, cancellationToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
