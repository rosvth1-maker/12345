using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

/// <summary>
/// Handles Pet / General Deployment and Management (C:015-001, C:019-001)
/// </summary>
public class PetManagementHandler : IPacketHandler
{
    private readonly IPetRepository _petRepo;
    private readonly IResponseSender _responseSender;

    public int MainKind => 15;
    public int SubKind => 1;

    public PetManagementHandler(IPetRepository petRepo, IResponseSender responseSender)
    {
        _petRepo = petRepo;
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        if (session.CharacterId == 0) return;

        var reader = new PacketReader(data.Span);
        if (reader.Remaining < 2) return;

        int action = reader.ReadByte(); // 1=Deploy, 2=Rest, 3=Set Primary
        int slot = reader.ReadByte();

        var pets = await _petRepo.GetPetsAsync(session.CharacterId);
        var targetPet = pets.FirstOrDefault(p => p.Slot == slot);

        if (targetPet != null)
        {
            bool newDeployState = action == 1;
            var updatedPet = targetPet with { IsDeployed = newDeployState };

            var newPetList = pets.Select(p => p.Slot == slot ? updatedPet : p).ToList();
            await _petRepo.SavePetsAsync(session.CharacterId, newPetList);

            string statusMsg = newDeployState ? $"นำขุนพล {targetPet.CustomName} ออกรบเรียบร้อย" : $"ให้ขุนพล {targetPet.CustomName} พักผ่อนเรียบร้อย";
            await _responseSender.SendSystemNoticeAsync(session, statusMsg);

            // Send Pet Update Frame (S:015-001)
            var writer = new PacketWriter()
                .WriteByte((byte)slot)
                .WriteByte((byte)(newDeployState ? 1 : 0))
                .WriteInt32LE(targetPet.NpcId)
                .WriteString(targetPet.CustomName, 20);

            var frame = FrameCodec.EncodeFrame(15, 1, writer.ToArray());
            await session.SendAsync(frame);
        }
    }
}
