using TsmServer.Domain.Constants;
using TsmServer.Domain.Interfaces;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

public class KeepaliveHandler : IPacketHandler
{
    public int MainKind => Opcodes.Keepalive;
    public int SubKind => 1;

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        var frame = FrameCodec.EncodeFrame(Opcodes.Keepalive, 1, Array.Empty<byte>());
        await session.SendAsync(frame);
    }
}
