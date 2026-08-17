using TsmClient.Protocol;
using TsmClient.Data;

var tests = new List<(string Name, Action Run)>
{
    ("Frame round trip", FrameRoundTrip),
    ("Fragmented frame", FragmentedFrame),
    ("Handshake payload", HandshakePayload),
    ("Login fixed fields", LoginFixedFields)
    ,("Keepalive opcode", KeepaliveOpcode)
    ,("Select character opcode", SelectCharacterOpcode)
    ,("Create character payload", CreateCharacterPayload)
    ,("Thai character name", ThaiCharacterName)
    ,("Reject oversized name", RejectOversizedName)
    ,("Move packet", MovePacket)
    ,("Warp packet", WarpPacket)
    ,("Scene packet", ScenePacket)
    ,("Movement response", MovementResponse)
    ,("Thai server names", ThaiServerNamesTest)
    ,("Asset format classification", AssetFormatClassification)
    ,("NPC and mission opcodes", NpcMissionOpcodes)
    ,("Battle command opcode", BattleCommandOpcode)
};

int failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS  {test.Name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL  {test.Name}: {ex.Message}"); }
}
Console.WriteLine($"\n{tests.Count - failed}/{tests.Count} tests passed");
return failed;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void FrameRoundTrip()
{
    var buffer = FrameCodec.Encode(1, 2, new byte[] { 10, 20, 30 }).ToList();
    Assert(FrameCodec.TryDecode(buffer, out var packet), "decode failed");
    Assert(packet is { MainKind: 1, SubKind: 2 }, "header mismatch");
    Assert(packet!.Payload.SequenceEqual(new byte[] { 10, 20, 30 }), "payload mismatch");
    Assert(buffer.Count == 0, "buffer not consumed");
}

static void FragmentedFrame()
{
    var frame = FrameCodec.Encode(0, 0, new byte[] { 2, 1 });
    var buffer = frame[..5].ToList();
    Assert(!FrameCodec.TryDecode(buffer, out _), "partial frame decoded");
    buffer.AddRange(frame[5..]);
    Assert(FrameCodec.TryDecode(buffer, out _), "complete frame not decoded");
}

static void HandshakePayload()
{
    var buffer = ClientPackets.Handshake(258).ToList();
    FrameCodec.TryDecode(buffer, out var packet);
    Assert(packet is { MainKind: 0, SubKind: 0 }, "wrong handshake opcode");
    Assert(packet!.Payload.SequenceEqual(new byte[] { 2, 1 }), "wrong version encoding");
}

static void LoginFixedFields()
{
    var buffer = ClientPackets.Login("player", "secret").ToList();
    FrameCodec.TryDecode(buffer, out var packet);
    Assert(packet is { MainKind: 1, SubKind: 1 }, "wrong login opcode");
    Assert(packet!.Payload.Length == 40, "login payload must be 40 bytes");
}

static void KeepaliveOpcode()
{
    var buffer = ClientPackets.Keepalive().ToList();
    FrameCodec.TryDecode(buffer, out var packet);
    Assert(packet is { MainKind: 10, SubKind: 1 }, "wrong keepalive opcode");
    Assert(packet!.Payload.Length == 0, "keepalive payload must be empty");
}

static void SelectCharacterOpcode()
{
    var buffer = ClientPackets.SelectCharacter(12345).ToList();
    FrameCodec.TryDecode(buffer, out var packet);
    Assert(packet is { MainKind: 3, SubKind: 1 }, "wrong character opcode");
    var reader = new PacketReader(packet!.Payload);
    Assert(reader.ReadInt32() == 12345, "wrong character id");
}

static void CreateCharacterPayload()
{
    var creation = new TsmClient.Domain.CharacterCreation("Hero", 1, 2, 3, 4, 5);
    var buffer = ClientPackets.CreateCharacter(creation).ToList();
    FrameCodec.TryDecode(buffer, out var packet);
    Assert(packet is { MainKind: 9, SubKind: 1 }, "wrong create-character opcode");
    Assert(packet!.Payload.Length == 21, "create payload must be 21 bytes");
    var reader = new PacketReader(packet.Payload);
    Assert(reader.ReadFixedString(16) == "Hero", "wrong character name");
    Assert(reader.ReadByte() == 1 && reader.ReadByte() == 2 && reader.ReadByte() == 3 && reader.ReadByte() == 4 && reader.ReadByte() == 5, "wrong appearance fields");
}

static void ThaiCharacterName()
{
    const string name = "ทดสอบ";
    var buffer = ClientPackets.CreateCharacter(new TsmClient.Domain.CharacterCreation(name, 0, 1, 1, 1, 1)).ToList();
    FrameCodec.TryDecode(buffer, out var packet);
    var reader = new PacketReader(packet!.Payload);
    Assert(reader.ReadFixedString(16) == name, "Thai name encoding mismatch");
}

static void RejectOversizedName()
{
    bool rejected = false;
    try { ClientPackets.CreateCharacter(new TsmClient.Domain.CharacterCreation("ชื่อที่ยาวเกินสิบหกไบต์แน่นอน", 0, 0, 0, 0, 0)); }
    catch (ArgumentException) { rejected = true; }
    Assert(rejected, "oversized name was accepted");
}

static void MovePacket()
{
    var buffer = ClientPackets.Move(570, 770).ToList();
    FrameCodec.TryDecode(buffer, out var packet);
    Assert(packet is { MainKind: 6, SubKind: 1 }, "wrong move opcode");
    var reader = new PacketReader(packet!.Payload);
    Assert(reader.ReadInt16() == 570 && reader.ReadInt16() == 770, "wrong move coordinates");
}

static void WarpPacket()
{
    var buffer = ClientPackets.Warp(10802, 300, 400).ToList();
    FrameCodec.TryDecode(buffer, out var packet);
    Assert(packet is { MainKind: 6, SubKind: 2 }, "wrong warp opcode");
    var reader = new PacketReader(packet!.Payload);
    Assert(reader.ReadInt32() == 10802 && reader.ReadInt16() == 300 && reader.ReadInt16() == 400, "wrong warp payload");
}

static void ScenePacket()
{
    var writer = new PacketWriter().WriteInt32(10801).WriteInt16(1).WriteInt32(99).WriteFixedString("ผู้เล่นสอง", 16)
        .WriteInt16(600).WriteInt16(800).WriteByte(5).WriteByte(2);
    var scene = ServerPackets.ReadScene(writer.ToArray());
    Assert(scene.MapId == 10801 && scene.Players.Count == 1, "wrong scene header");
    Assert(scene.Players[0].CharacterId == 99 && scene.Players[0].Name == "ผู้เล่นสอง" && scene.Players[0].X == 600, "wrong scene player");
}

static void MovementResponse()
{
    var movement = ServerPackets.ReadMovement(new PacketWriter().WriteInt32(99).WriteInt16(610).WriteInt16(810).ToArray());
    Assert(movement == new TsmClient.Domain.PlayerMovement(99, 610, 810), "wrong movement response");
}

static void ThaiServerNamesTest()
{
    Assert(ThaiServerNames.All.Any(x => x is { Kind: "Item", Id: 10001, Name: "ซาลาเปาหมูสับ" }), "item name mismatch");
    Assert(ThaiServerNames.All.Any(x => x is { Kind: "NPC", Id: 10001, Name: "ครูฝึกมือใหม่ประจำเมือง" }), "NPC name mismatch");
    Assert(ThaiServerNames.All.Any(x => x is { Kind: "Skill", Id: 104, Name: "เพลิงเผาผลาญ" }), "skill name mismatch");
}

static void AssetFormatClassification()
{
    Assert(AssetCatalogService.IsAnalyzedExtension(".dat"), "dat not analyzed");
    Assert(AssetCatalogService.IsAnalyzedExtension(".unity3d"), "unity3d not analyzed");
    Assert(AssetCatalogService.IsAnalyzedExtension(".sty") && AssetCatalogService.IsAnalyzedExtension(".jmxa") &&
        AssetCatalogService.IsAnalyzedExtension(".jmg") && AssetCatalogService.IsAnalyzedExtension(".pmg"), "legacy formats missing");
}

static void NpcMissionOpcodes()
{
    var npc = ClientPackets.TalkToNpc(10001).ToList(); FrameCodec.TryDecode(npc, out var np);
    Assert(np is { MainKind: 22, SubKind: 1 }, "wrong NPC opcode");
    var mission = ClientPackets.Mission(5001, 1).ToList(); FrameCodec.TryDecode(mission, out var mp);
    Assert(mp is { MainKind: 47, SubKind: 1 } && mp.Payload.Length == 5, "wrong mission packet");
}
static void BattleCommandOpcode()
{
    var b = ClientPackets.BattleCommand(2, 0, 1, 104).ToList(); FrameCodec.TryDecode(b, out var p);
    Assert(p is { MainKind: 50, SubKind: 2 } && p.Payload.Length == 5, "wrong battle command");
}
