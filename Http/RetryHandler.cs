using System.Net;

namespace ValRadar.Http;

public class RetryHandler : DelegatingHandler
{
    private readonly int _maxRetries;

    public RetryHandler(int maxRetries = 3)
    {
        _maxRetries = maxRetries;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= _maxRetries)
            {
                return response;
            }

            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
            response.Dispose();

            await Task.Delay(delay, cancellationToken);
        }
    }
}
