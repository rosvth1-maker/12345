using System.Collections.Concurrent;
using TsmServer.Domain.Interfaces;

namespace TsmServer.Network;

public class PacketDispatcher
{
    private readonly ConcurrentDictionary<(int Main, int Sub), IPacketHandler> _handlers = new();

    public void Register(IPacketHandler handler)
    {
        _handlers[(handler.MainKind, handler.SubKind)] = handler;
    }

    public async ValueTask DispatchAsync(IGameSession session, byte[] rawPacket)
    {
        if (rawPacket.Length < 3) return;

        int mainKind = rawPacket[0];
        int subKind = rawPacket[1];
        byte compress = rawPacket[2];
        var payload = rawPacket.AsMemory(3);

        if (_handlers.TryGetValue((mainKind, subKind), out var handler))
        {
            try
            {
                await handler.HandleAsync(session, payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dispatcher Error] (Main={mainKind}, Sub={subKind}): {ex.Message}");
            }
        }
        else
        {
            // Unhandled packet debug log
            // Console.WriteLine($"[Dispatcher] Unhandled packet: MainKind={mainKind}, SubKind={subKind}, Length={payload.Length}");
        }
    }
}
