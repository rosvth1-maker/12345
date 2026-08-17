using TsmServer.Domain.Constants;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.Domain.ValueObjects;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

public class CreateCharHandler : IPacketHandler
{
    private readonly ICharacterRepository _charRepo;
    private readonly IResponseSender _responseSender;

    public int MainKind => Opcodes.CreateChar;
    public int SubKind => 1;

    public CreateCharHandler(ICharacterRepository charRepo, IResponseSender responseSender)
    {
        _charRepo = charRepo;
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        var reader = new PacketReader(data.Span);
        string name = reader.ReadString(16).Trim();
        int gender = reader.ReadByte();
        int element = reader.ReadByte();
        int hair = reader.ReadByte();
        int hairColor = reader.ReadByte();
        int skinColor = reader.ReadByte();

        var newChar = new PlayerDataDto(
            CharacterId: 0,
            Account: session.AccountName,
            Name: name,
            Level: 1,
            Element: element,
            Gender: gender,
            Hair: hair,
            HairColor: hairColor,
            SkinColor: skinColor,
            Hp: 100,
            MaxHp: 100,
            Sp: 50,
            MaxSp: 50,
            IntVal: 10,
            AtkVal: 10,
            DefVal: 10,
            HpaVal: 10,
            SpaVal: 10,
            AgiVal: 10,
            FreePoints: 5,
            SkillPoints: 1,
            Exp: 0,
            Gold: 1000,
            MapId: Opcodes.DefaultStartMap,
            X: Opcodes.DefaultStartX,
            Y: Opcodes.DefaultStartY,
            GmLevel: session.GmLevel
        );

        int charId = await _charRepo.CreateCharacterAsync(session.AccountId, newChar);
        var characters = await _charRepo.GetCharactersByAccountIdAsync(session.AccountId);
        await _responseSender.SendCharListAsync(session, characters);
    }
}
