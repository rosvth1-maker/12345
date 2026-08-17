using TsmServer.Domain.Constants;
using TsmServer.Domain.Interfaces;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

/// <summary>
/// Handles Client Version Check (C:000-000) from TS Online PC / Mobile Lua Client
/// </summary>
public class VersionCheckHandler : IPacketHandler
{
    private readonly IResponseSender _responseSender;

    public int MainKind => 0;
    public int SubKind => 0;

    public VersionCheckHandler(IResponseSender responseSender)
    {
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        int clientVersion = 258;
        if (data.Length >= 2)
        {
            var reader = new PacketReader(data.Span);
            clientVersion = reader.ReadUInt16LE();
        }

        Console.WriteLine($"[Handshake] Client connected with version {clientVersion}. Handshake accepted.");

        // Send Server Time + Welcome Handshake back
        var writer = new PacketWriter();
        double serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        writer.WriteDoubleLE(serverTime);

        var frame = FrameCodec.EncodeFrame(1, 16, writer.ToArray());
        await session.SendAsync(frame);
    }
}
