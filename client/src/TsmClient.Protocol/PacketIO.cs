using System.Buffers.Binary;
using System.Text;
using TsmClient.Domain;

namespace TsmClient.Protocol;

public static class GameTextEncoding
{
    static GameTextEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Thai = Encoding.GetEncoding(874);
    }

    public static Encoding Thai { get; }
}

public sealed class PacketWriter
{
    private readonly MemoryStream _stream = new();
    public PacketWriter WriteByte(byte value) { _stream.WriteByte(value); return this; }
    public PacketWriter WriteUInt16(ushort value) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, value); _stream.Write(b); return this; }
    public PacketWriter WriteInt16(short value) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteInt16LittleEndian(b, value); _stream.Write(b); return this; }
    public PacketWriter WriteInt32(int value) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, value); _stream.Write(b); return this; }
    public PacketWriter WriteInt64(long value) { Span<byte> b = stackalloc byte[8]; BinaryPrimitives.WriteInt64LittleEndian(b, value); _stream.Write(b); return this; }
    public PacketWriter WriteFixedString(string value, int length, Encoding? encoding = null)
    {
        encoding ??= GameTextEncoding.Thai;
        var output = new byte[length];
        var source = encoding.GetBytes(value);
        Array.Copy(source, output, Math.Min(source.Length, output.Length));
        _stream.Write(output);
        return this;
    }
    public byte[] ToArray() => _stream.ToArray();
}

public ref struct PacketReader
{
    private ReadOnlySpan<byte> _data;
    private int _position;
    public PacketReader(ReadOnlySpan<byte> data) => _data = data;
    public int Remaining => _data.Length - _position;
    public byte ReadByte() => _data[_position++];
    public short ReadInt16() { short value = BinaryPrimitives.ReadInt16LittleEndian(_data[_position..]); _position += 2; return value; }
    public int ReadInt32() { int value = BinaryPrimitives.ReadInt32LittleEndian(_data[_position..]); _position += 4; return value; }
    public long ReadInt64() { long value = BinaryPrimitives.ReadInt64LittleEndian(_data[_position..]); _position += 8; return value; }
    public string ReadFixedString(int length, Encoding? encoding = null)
    {
        encoding ??= GameTextEncoding.Thai;
        var source = _data.Slice(_position, length);
        _position += length;
        int terminator = source.IndexOf((byte)0);
        if (terminator >= 0) source = source[..terminator];
        return encoding.GetString(source).Trim();
    }
    public string ReadNullTerminatedString(Encoding? encoding = null)
    {
        encoding ??= GameTextEncoding.Thai;
        int length = _data[_position..].IndexOf((byte)0);
        if (length < 0) length = Remaining;
        string value = encoding.GetString(_data.Slice(_position, length));
        _position += Math.Min(length + 1, Remaining);
        return value;
    }
}

public static class ClientPackets
{
    public const byte LoginMain = 1;
    public const byte CharacterMain = 3;
    public const byte KeepaliveMain = 10;
    public const byte MoveMain = 6;
    public const byte SceneMain = 7;
    public const byte MapNpcMain = 22;
    public const byte MissionMain = 47;
    public const byte BattleCommandMain = 50;
    public const byte BattleStatusMain = 53;
    public const byte InventoryMain = 25;

    public static byte[] Handshake(ushort version) =>
        FrameCodec.Encode(0, 0, new PacketWriter().WriteUInt16(version).ToArray());

    public static byte[] Login(string account, string password) =>
        FrameCodec.Encode(LoginMain, 1, new PacketWriter()
            .WriteFixedString(account, 20)
            .WriteFixedString(password, 20)
            .ToArray());

    public static byte[] Keepalive() => FrameCodec.Encode(KeepaliveMain, 1, []);

    public static byte[] SelectCharacter(int characterId) =>
        FrameCodec.Encode(CharacterMain, 1, new PacketWriter().WriteInt32(characterId).ToArray());

    public static byte[] Move(short x, short y) =>
        FrameCodec.Encode(MoveMain, 1, new PacketWriter().WriteInt16(x).WriteInt16(y).ToArray());

    public static byte[] Warp(int mapId, short x, short y) =>
        FrameCodec.Encode(MoveMain, 2, new PacketWriter().WriteInt32(mapId).WriteInt16(x).WriteInt16(y).ToArray());

    public static byte[] TalkToNpc(int npcId) => FrameCodec.Encode(MapNpcMain, 1, new PacketWriter().WriteInt32(npcId).ToArray());
    public static byte[] Mission(int missionId, byte action) => FrameCodec.Encode(MissionMain, 1,
        new PacketWriter().WriteInt32(missionId).WriteByte(action).ToArray());
    public static byte[] BattleCommand(byte action, byte row = 0, byte col = 0, ushort skillId = 101) =>
        FrameCodec.Encode(BattleCommandMain, 2, new PacketWriter().WriteByte(action).WriteByte(row).WriteByte(col).WriteUInt16(skillId).ToArray());
    public static byte[] ItemOperation(byte operation, byte slot) => FrameCodec.Encode(5, 1, new PacketWriter().WriteByte(operation).WriteByte(slot).ToArray());
    public static byte[] CoreSystem(byte action, int value = 0, short count = 1, string? text = null, long amount = 0)
    {
        var w = new PacketWriter().WriteByte(action);
        if (action is 1 or 3 or 5 or 7) w.WriteInt32(value);
        if (action is 3 or 7) w.WriteInt16(count);
        if (action == 4) w.WriteInt64(amount);
        if (action == 6) w.WriteFixedString(text ?? "สวัสดี", 64);
        if (action == 8) w.WriteFixedString(text ?? "TS Dark World", 32);
        return FrameCodec.Encode(100, 1, w.ToArray());
    }
    public static byte[] PetCommand(byte action, byte slot) => FrameCodec.Encode(15, 1, new PacketWriter().WriteByte(action).WriteByte(slot).ToArray());
    public static byte[] TeamCommand(byte action, int targetId) => FrameCodec.Encode(13, 1, new PacketWriter().WriteByte(action).WriteInt32(targetId).ToArray());
    public static byte[] Chat(byte channel, string message) => FrameCodec.Encode(2, 1, new PacketWriter().WriteByte(channel).WriteFixedString(message, GameTextEncoding.Thai.GetByteCount(message) + 1).ToArray());

    public static byte[] CreateCharacter(CharacterCreation character)
    {
        string name = character.Name.Trim();
        int nameBytes = GameTextEncoding.Thai.GetByteCount(name);
        if (nameBytes is < 1 or > 16) throw new ArgumentException("ชื่อตัวละครต้องมีขนาด 1–16 ไบต์ใน Windows-874", nameof(character));
        return FrameCodec.Encode(9, 1, new PacketWriter()
            .WriteFixedString(name, 16, GameTextEncoding.Thai)
            .WriteByte(character.Gender)
            .WriteByte(character.Element)
            .WriteByte(character.Hair)
            .WriteByte(character.HairColor)
            .WriteByte(character.SkinColor)
            .ToArray());
    }
}

public static class ServerPackets
{
    public static PlayerMovement ReadMovement(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8) throw new InvalidDataException("แพ็กเก็ตการเดินสั้นเกินไป");
        var reader = new PacketReader(payload);
        return new PlayerMovement(reader.ReadInt32(), reader.ReadInt16(), reader.ReadInt16());
    }

    public static SceneSnapshot ReadScene(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 6) throw new InvalidDataException("แพ็กเก็ต Scene สั้นเกินไป");
        var reader = new PacketReader(payload);
        int mapId = reader.ReadInt32();
        int count = reader.ReadInt16();
        if (count < 0 || reader.Remaining < count * 26) throw new InvalidDataException("จำนวนผู้เล่นใน Scene ไม่ถูกต้อง");
        var players = new List<ScenePlayer>(count);
        for (int i = 0; i < count; i++)
            players.Add(new ScenePlayer(reader.ReadInt32(), reader.ReadFixedString(16), reader.ReadInt16(), reader.ReadInt16(), reader.ReadByte(), reader.ReadByte()));
        var npcs = new List<NpcPlacement>();
        if (reader.Remaining >= 2)
        {
            int npcCount = reader.ReadInt16();
            if (npcCount < 0 || reader.Remaining < npcCount * 44) throw new InvalidDataException("จำนวน NPC ใน Scene ไม่ถูกต้อง");
            for (int i = 0; i < npcCount; i++) npcs.Add(new(reader.ReadInt32(), reader.ReadFixedString(32), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt32()));
        }
        return new SceneSnapshot(mapId, players, npcs);
    }

    public static NpcDialog ReadNpcDialog(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        int id = r.ReadInt32(); string name = r.ReadFixedString(64); string text = r.ReadNullTerminatedString();
        int count = r.ReadByte(); var options = new List<string>();
        for (int i = 0; i < count; i++) options.Add(r.ReadFixedString(32));
        return new NpcDialog(id, name, text, options, r.ReadInt32());
    }

    public static QuestState ReadQuest(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        int id = r.ReadInt32(); byte status = r.ReadByte(); long reward = r.ReadInt64();
        return new QuestState(id, r.ReadFixedString(40), status, reward, r.ReadFixedString(48));
    }
    public static BattleState ReadBattleStart(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload); return new BattleState(r.ReadInt32(), r.ReadFixedString(32), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadByte() == 1);
    }
    public static BattleActionResult ReadBattleAction(ReadOnlySpan<byte> payload)
    { var r = new PacketReader(payload); int id = r.ReadInt32(); byte a = r.ReadByte(); r.ReadByte(); r.ReadByte(); return new(id, a, r.ReadInt32(), r.ReadFixedString(32), r.ReadInt32()); }
    public static BattleReward ReadBattleReward(ReadOnlySpan<byte> payload)
    { var r = new PacketReader(payload); return new(r.ReadByte() == 1, r.ReadInt64(), r.ReadInt64(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt64()); }
    public static IReadOnlyList<InventoryItem> ReadInventory(ReadOnlySpan<byte> payload)
    { var r = new PacketReader(payload); int count = r.ReadByte(); var list = new List<InventoryItem>(count); for (int i = 0; i < count; i++) list.Add(new(r.ReadByte(), r.ReadInt32(), r.ReadInt16(), r.ReadByte())); return list; }
    public static CoreSystemResult ReadCoreSystem(ReadOnlySpan<byte> p) { var r = new PacketReader(p); return new(r.ReadByte(), r.ReadByte() == 1, r.ReadInt64(), r.ReadFixedString(96)); }
    public static PetStatus ReadPet(ReadOnlySpan<byte> p) { var r = new PacketReader(p); return new(r.ReadByte(), r.ReadByte() == 1, r.ReadInt32(), r.ReadFixedString(20)); }
    public static TeamStatus ReadTeam(ReadOnlySpan<byte> p) { var r = new PacketReader(p); return new(r.ReadByte(), r.ReadInt32(), r.ReadFixedString(20)); }
    public static ChatMessage ReadChat(ReadOnlySpan<byte> p) { var r = new PacketReader(p); return new(r.ReadByte(), r.ReadFixedString(16), r.ReadNullTerminatedString()); }
}
