using System.Net;
using System.Net.Sockets;
using TsmServer.Protocol;

namespace TsmServer.Network;

public class PipelinesTcpServer
{
    private readonly SessionManager _sessionManager;
    private readonly PacketDispatcher _dispatcher;
    private readonly Func<GameSessionImpl, ValueTask>? _onDisconnect;
    private Socket? _listener;
    private CancellationTokenSource? _cts;

    public PipelinesTcpServer(
        SessionManager sessionManager,
        PacketDispatcher dispatcher,
        Func<GameSessionImpl, ValueTask>? onDisconnect = null)
    {
        _sessionManager = sessionManager;
        _dispatcher = dispatcher;
        _onDisconnect = onDisconnect;
    }

    public void Start(string host, int port)
    {
        _cts = new CancellationTokenSource();
        IPAddress ip = host == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(host);
        var endpoint = new IPEndPoint(ip, port);

        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.NoDelay = true;
        _listener.Bind(endpoint);
        _listener.Listen(128);

        Console.WriteLine($"[TcpServer] TCP Game Server listening on {host}:{port}...");
        _ = AcceptClientsAsync(_cts.Token);
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var clientSocket = await _listener.AcceptAsync(ct);
                clientSocket.NoDelay = true;
                var session = new GameSessionImpl(clientSocket, _onDisconnect);
                _sessionManager.AddSession(session);
                _ = ProcessClientAsync(session, clientSocket, ct);
            }
            catch when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpServer] Accept Error: {ex.Message}");
            }
        }
    }

    private async Task ProcessClientAsync(GameSessionImpl session, Socket socket, CancellationToken ct)
    {
        byte[] buffer = new byte[65536];
        int bufferOffset = 0;

        try
        {
            while (!ct.IsCancellationRequested && socket.Connected)
            {
                int bytesRead = await socket.ReceiveAsync(buffer.AsMemory(bufferOffset, buffer.Length - bufferOffset), SocketFlags.None, ct);
                if (bytesRead <= 0) break;

                bufferOffset += bytesRead;
                var packets = new List<byte[]>();
                ReadOnlySpan<byte> span = buffer.AsSpan(0, bufferOffset);

                while (FrameCodec.TryDecodeFrame(ref span, out var packet))
                {
                    if (packet != null)
                    {
                        packets.Add(packet);
                    }
                }

                int remaining = span.Length;
                if (remaining < bufferOffset)
                {
                    span.CopyTo(buffer.AsSpan());
                    bufferOffset = remaining;
                }

                foreach (var pkt in packets)
                {
                    await _dispatcher.DispatchAsync(session, pkt);
                }
            }
        }
        catch { }
        finally
        {
            await session.CloseAsync();
            _sessionManager.RemoveSession(session.SessionId);
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Close();
        Console.WriteLine("[TcpServer] TCP Server stopped.");
    }
}
