using System.Net.Sockets;
using TsmClient.Domain;
using TsmClient.Protocol;

namespace TsmClient.Network;

public sealed class TcpGameClient : IAsyncDisposable
{
    private readonly List<byte> _receiveBuffer = [];
    private TcpClient? _client;
    private CancellationTokenSource? _receiveCts;

    public bool IsConnected => _client?.Connected == true;
    public event Action<GamePacket>? PacketReceived;
    public event Action<string>? Log;
    public event Action? Disconnected;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();
        _client = new TcpClient { NoDelay = true };
        Log?.Invoke($"กำลังเชื่อมต่อ {host}:{port}...");
        await _client.ConnectAsync(host, port, cancellationToken);
        _receiveCts = new CancellationTokenSource();
        _ = ReceiveLoopAsync(_receiveCts.Token);
        Log?.Invoke("เชื่อมต่อเซิร์ฟเวอร์สำเร็จ");
    }

    public async Task SendAsync(byte[] frame, CancellationToken cancellationToken = default)
    {
        if (_client?.Connected != true) throw new InvalidOperationException("ยังไม่ได้เชื่อมต่อเซิร์ฟเวอร์");
        await _client.GetStream().WriteAsync(frame, cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var chunk = new byte[8192];
        try
        {
            while (_client?.Connected == true && !cancellationToken.IsCancellationRequested)
            {
                int count = await _client.GetStream().ReadAsync(chunk, cancellationToken);
                if (count == 0) break;
                lock (_receiveBuffer)
                {
                    _receiveBuffer.AddRange(chunk.AsSpan(0, count).ToArray());
                    while (FrameCodec.TryDecode(_receiveBuffer, out var packet) && packet is not null)
                        PacketReceived?.Invoke(packet);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log?.Invoke($"การเชื่อมต่อขัดข้อง: {ex.Message}"); }
        finally
        {
            _client?.Dispose();
            _client = null;
            Disconnected?.Invoke();
        }
    }

    public Task DisconnectAsync()
    {
        _receiveCts?.Cancel();
        _client?.Dispose();
        _client = null;
        lock (_receiveBuffer) _receiveBuffer.Clear();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
