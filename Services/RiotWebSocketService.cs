using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ValRadar.Models;

namespace ValRadar.Services;

public class RiotWebSocketService : IDisposable
{
    private ClientWebSocket? _webSocket;
    private readonly RiotLockfileData _lockfileData;
    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    public event Action<string>? OnGamePhaseChanged;
    public event Action<JsonElement>? OnPresenceUpdated;
    public RiotWebSocketService(RiotLockfileData lockfileData)
    {
        _lockfileData = lockfileData;
    }

    public async Task ConnectAsync()
    {
        _webSocket = new ClientWebSocket();
        _webSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        var authBytes = Encoding.UTF8.GetBytes($"riot:{_lockfileData.Password}");
        var authBase64 = Convert.ToBase64String(authBytes);
        _webSocket.Options.SetRequestHeader("Authorization", $"Basic {authBase64}");
        
        var uri = new Uri($"wss://172.0.0.1:{_lockfileData.Port}");
        await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);

        var subscribeMessage = "[5, \"OnJsonApiEvent_chat_v4_presences\"]";
        await SendAsync(subscribeMessage);

    }
    
    private async Task SendAsync(string message)
    {
        if(_webSocket.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
    }

    public async Task ListenAsync()
    {
        var buffer = new byte[16384];
        var messageBuffer = new StringBuilder();

        while (_webSocket.State == WebSocketState.Open && !_cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(buffer, _cancellationTokenSource.Token);
                messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (!result.EndOfMessage) continue;

                var message = messageBuffer.ToString();
                messageBuffer.Clear();

                ProcessMessage(message);

            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                //connection lost...
                break;
            }
        }
    }

    private void ProcessMessage(string message)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(message);
            if(json.ValueKind == JsonValueKind.Array) return;
            
            var arr = json.EnumerateArray().ToList();
            if(arr.Count < 3) return;
            
            var eventData = arr[2];
            if(!eventData.TryGetProperty("data", out var data)) return;
            if(!eventData.TryGetProperty("presences", out var presences)) return;

            foreach (var presence in presences.EnumerateArray())
            {
                var privateB64 = presence.TryGetProperty("private", out var priv)
                    ? priv.GetString() : null;
                if(string.IsNullOrEmpty(privateB64)) continue;
                
                var privateJson = Encoding.UTF8.GetString(Convert.FromBase64String(privateB64));
                var privateData = JsonSerializer.Deserialize<JsonElement>(privateJson);
                
                if(privateData.TryGetProperty("sessionLoopState", out var sessionLoopState) || 
                   privateData.TryGetProperty("matchPresenceData", out var matchPresenceData) &&
                   matchPresenceData.TryGetProperty("sessionLoopState", out sessionLoopState))
                {
                    var phase = sessionLoopState.GetString() ?? "";
                    OnGamePhaseChanged?.Invoke(phase);
                }
                OnPresenceUpdated?.Invoke(privateData);
            }
        }
        catch
        {
            // message wasn't in expected format, ignore
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}