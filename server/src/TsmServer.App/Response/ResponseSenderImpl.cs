using System.Text;
using TsmServer.Domain.Constants;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.ValueObjects;
using TsmServer.Protocol;
using TsmServer.Data;

namespace TsmServer.App.Response;

public class ResponseSenderImpl(GameDataManager gameData) : IResponseSender
{
    public async ValueTask SendDisconnectAsync(IGameSession session, int cause)
    {
        var writer = new PacketWriter().WriteByte((byte)cause);
        var frame = FrameCodec.EncodeFrame(Opcodes.Disconnect, 0, writer.ToArray());
        await session.SendAsync(frame);
    }

    public async ValueTask SendLoginResultAsync(IGameSession session, int result, string message)
    {
        var writer = new PacketWriter()
            .WriteByte((byte)result)
            .WriteString(message, 32);
        var frame = FrameCodec.EncodeFrame(Opcodes.Login, 1, writer.ToArray());
        await session.SendAsync(frame);
    }

    public async ValueTask SendCharListAsync(IGameSession session, IReadOnlyList<PlayerDataDto> characters)
    {
        var writer = new PacketWriter().WriteByte((byte)characters.Count);
        foreach (var c in characters)
        {
            writer.WriteInt32LE(c.CharacterId)
                  .WriteString(c.Name, 16)
                  .WriteByte((byte)c.Level)
                  .WriteByte((byte)c.Element)
                  .WriteByte((byte)c.Gender)
                  .WriteByte((byte)c.Hair)
                  .WriteByte((byte)c.HairColor)
                  .WriteByte((byte)c.SkinColor)
                  .WriteInt32LE(c.MapId);
        }
        var frame = FrameCodec.EncodeFrame(Opcodes.Character, 1, writer.ToArray());
        await session.SendAsync(frame);
    }

    public async ValueTask SendEnterGameAsync(IGameSession session, PlayerDataDto player)
    {
        var writer = new PacketWriter()
            .WriteInt32LE(player.CharacterId)
            .WriteString(player.Name, 16)
            .WriteByte((byte)player.Level)
            .WriteByte((byte)player.Element)
            .WriteInt32LE(player.Hp)
            .WriteInt32LE(player.MaxHp)
            .WriteInt32LE(player.Sp)
            .WriteInt32LE(player.MaxSp)
            .WriteInt32LE(player.IntVal)
            .WriteInt32LE(player.AtkVal)
            .WriteInt32LE(player.DefVal)
            .WriteInt32LE(player.HpaVal)
            .WriteInt32LE(player.SpaVal)
            .WriteInt32LE(player.AgiVal)
            .WriteInt32LE(player.FreePoints)
            .WriteInt32LE(player.SkillPoints)
            .WriteInt64LE(player.Exp)
            .WriteInt64LE(player.Gold)
            .WriteInt32LE(player.MapId)
            .WriteInt16LE((short)player.X)
            .WriteInt16LE((short)player.Y);

        var frame = FrameCodec.EncodeFrame(Opcodes.Character, 2, writer.ToArray());
        await session.SendAsync(frame);
    }

    public async ValueTask SendChatMessageAsync(IGameSession session, int channel, string sender, string message)
    {
        var writer = new PacketWriter()
            .WriteByte((byte)channel)
            .WriteString(sender, 16)
            .WriteNullTerminatedString(message);
        var frame = FrameCodec.EncodeFrame(Opcodes.Chat, 1, writer.ToArray());
        await session.SendAsync(frame);
    }

    public async ValueTask SendPlayerMoveAsync(IGameSession session, int characterId, int targetX, int targetY)
    {
        var writer = new PacketWriter()
            .WriteInt32LE(characterId)
            .WriteInt16LE((short)targetX)
            .WriteInt16LE((short)targetY);
        var frame = FrameCodec.EncodeFrame(Opcodes.Move, 1, writer.ToArray());
        await session.SendAsync(frame);
    }

    public async ValueTask SendSceneInfoAsync(IGameSession session, int mapId, IReadOnlyList<PlayerDataDto> otherPlayers)
    {
        var writer = new PacketWriter()
            .WriteInt32LE(mapId)
            .WriteInt16LE((short)otherPlayers.Count);

        foreach (var p in otherPlayers)
        {
            writer.WriteInt32LE(p.CharacterId)
                  .WriteString(p.Name, 16)
                  .WriteInt16LE((short)p.X)
                  .WriteInt16LE((short)p.Y)
                  .WriteByte((byte)p.Level)
                  .WriteByte((byte)p.Element);
        }

        var npcs = SceneNpcCatalog.Resolve(gameData, mapId);
        writer.WriteInt16LE((short)npcs.Count);
        foreach (var npc in npcs)
            writer.WriteInt32LE(npc.NpcId).WriteString(npc.Name, 32).WriteInt16LE((short)npc.X).WriteInt16LE((short)npc.Y).WriteInt32LE(npc.QuestId);

        var frame = FrameCodec.EncodeFrame(Opcodes.Scene, 1, writer.ToArray());
        await session.SendAsync(frame);
    }

    public async ValueTask SendInventoryAsync(IGameSession session, IReadOnlyList<ThingData> items)
    {
        var writer = new PacketWriter().WriteByte((byte)items.Count);
        foreach (var it in items)
        {
            writer.WriteByte((byte)it.Slot)
                  .WriteInt32LE(it.ItemId)
                  .WriteInt16LE((short)it.Count)
                  .WriteByte((byte)it.Durability);
        }
        var frame = FrameCodec.EncodeFrame(Opcodes.Trade, 1, writer.ToArray());
        await session.SendAsync(frame);
    }

    public async ValueTask SendGoldUpdateAsync(IGameSession session, long gold)
    {
        var writer = new PacketWriter().WriteInt64LE(gold);
        var frame = FrameCodec.EncodeFrame(Opcodes.Money, 1, writer.ToArray());
        await session.SendAsync(frame);
    }

    public async ValueTask SendSystemNoticeAsync(IGameSession session, string message)
    {
        await SendChatMessageAsync(session, (int)Domain.Enums.ChatChannel.System, "ระบบ", message);
    }
}
