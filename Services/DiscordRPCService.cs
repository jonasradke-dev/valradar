using DiscordRPC;

namespace ValRadar.Services;

public class DiscordRPCService : IDisposable
{
    private DiscordRpcClient _client;
    private string _applicationId;

    public DiscordRPCService(string applicationId)
    {
        _applicationId = applicationId;
    }
    
    public void Initialize()
    {
        if (_client != null) return;
        _client = new DiscordRpcClient(_applicationId);
        _client.Initialize();
    }
    public void UpdatePresence(string details, string state, string largeImageKey = "", string largeImageText = "")
    {
        if( _client == null || _client.IsDisposed) return;
        _client.SetPresence(new RichPresence()
        {
            Details = details,
            State = state,
            Assets = new Assets()
            {
                LargeImageKey = largeImageKey,
                LargeImageText = largeImageText
            },
            Buttons =
            [
                new Button()
                {
                    Label = "Whats this?",
                    Url = "https://github.com/jonasradke-dev/valradar",
                    
                }
            ],
            Timestamps = Timestamps.Now
        });
    }

    public void InvokeCallbacks()
    {
        _client?.Invoke();
    }

    public void Dispose()
    {
        if(_client != null && !_client.IsDisposed)
            _client.Dispose();
            _client  = null;
    }
}