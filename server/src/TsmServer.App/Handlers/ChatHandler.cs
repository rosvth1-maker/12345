using TsmServer.App.GM;
using TsmServer.Domain.Constants;
using TsmServer.Domain.Enums;
using TsmServer.Domain.Interfaces;
using TsmServer.GameLogic.Systems;
using TsmServer.Network;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

public class ChatHandler : IPacketHandler
{
    private readonly WorldManager _worldManager;
    private readonly SessionManager _sessionManager;
    private readonly GmCommandProcessor _gmProcessor;
    private readonly IResponseSender _responseSender;

    public int MainKind => Opcodes.Chat;
    public int SubKind => 1;

    public ChatHandler(
        WorldManager worldManager,
        SessionManager sessionManager,
        GmCommandProcessor gmProcessor,
        IResponseSender responseSender)
    {
        _worldManager = worldManager;
        _sessionManager = sessionManager;
        _gmProcessor = gmProcessor;
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        var reader = new PacketReader(data.Span);
        int channel = reader.ReadByte();
        string message = reader.ReadNullTerminatedString();

        if (string.IsNullOrWhiteSpace(message)) return;

        // Check if GM command
        if (message.StartsWith("/"))
        {
            await _gmProcessor.ExecuteAsync(session, message);
            return;
        }

        // Broadcast normal chat
        var writer = new PacketWriter()
            .WriteByte((byte)channel)
            .WriteString(session.CharacterName, 16)
            .WriteNullTerminatedString(message);
        var chatFrame = FrameCodec.EncodeFrame(Opcodes.Chat, 1, writer.ToArray());

        if (channel == (int)ChatChannel.World || channel == (int)ChatChannel.Horn)
        {
            foreach (var s in _sessionManager.InGameSessions)
            {
                await s.SendAsync(chatFrame);
            }
        }
        else
        {
            await _worldManager.BroadcastToMapAsync(session.CurrentMapId, chatFrame);
        }
    }
}
