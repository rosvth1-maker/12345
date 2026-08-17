using TsmServer.Domain.Interfaces;

namespace TsmServer.Domain.Interfaces;

public interface IPacketHandler
{
    int MainKind { get; }
    int SubKind { get; }
    ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data);
}
