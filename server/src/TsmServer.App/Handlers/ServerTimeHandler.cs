using TsmServer.Domain.Interfaces;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

/// <summary>
/// Handles Server Time Query / Heartbeat Ping (C:001-016)
/// </summary>
public class ServerTimeHandler : IPacketHandler
{
    public int MainKind => 1;
    public int SubKind => 16;

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        double serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var writer = new PacketWriter().WriteDoubleLE(serverTime);
        var frame = FrameCodec.EncodeFrame(1, 16, writer.ToArray());
        await session.SendAsync(frame);
    }
}
