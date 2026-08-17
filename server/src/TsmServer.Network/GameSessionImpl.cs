using System.Collections.Concurrent;
using System.Net.Sockets;
using TsmServer.Domain.Enums;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.ValueObjects;

namespace TsmServer.Network;

public class GameSessionImpl : IGameSession
{
    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly Func<GameSessionImpl, ValueTask>? _onDisconnect;

    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    public SessionState State { get; set; } = SessionState.Connected;
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int CharacterId { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public PlayerDataDto? PlayerData { get; set; }
    public int CurrentMapId { get; set; } = 10801;
    public int X { get; set; } = 570;
    public int Y { get; set; } = 770;
    public int GmLevel { get; set; }

    public GameSessionImpl(Socket socket, Func<GameSessionImpl, ValueTask>? onDisconnect = null)
    {
        _socket = socket;
        _stream = new NetworkStream(socket, ownsSocket: false);
        _onDisconnect = onDisconnect;
    }

    public async ValueTask SendAsync(byte[] packet)
    {
        if (!_socket.Connected) return;
        try
        {
            await _stream.WriteAsync(packet);
            await _stream.FlushAsync();
        }
        catch
        {
            await CloseAsync();
        }
    }

    public async ValueTask CloseAsync()
    {
        if (State == SessionState.Disconnected) return;
        State = SessionState.Disconnected;
        try
        {
            if (_onDisconnect != null)
                await _onDisconnect(this);

            _stream.Close();
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }
        catch { }
    }
}
