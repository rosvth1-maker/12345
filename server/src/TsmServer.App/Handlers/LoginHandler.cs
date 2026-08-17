using TsmServer.Domain.Constants;
using TsmServer.Domain.Enums;
using TsmServer.Domain.Interfaces;
using TsmServer.Domain.Interfaces.Repositories;
using TsmServer.Protocol;

namespace TsmServer.App.Handlers;

public class LoginHandler : IPacketHandler
{
    private readonly IAccountRepository _accountRepo;
    private readonly ICharacterRepository _charRepo;
    private readonly IResponseSender _responseSender;

    public int MainKind => Opcodes.Login;
    public int SubKind => 1;

    public LoginHandler(IAccountRepository accountRepo, ICharacterRepository charRepo, IResponseSender responseSender)
    {
        _accountRepo = accountRepo;
        _charRepo = charRepo;
        _responseSender = responseSender;
    }

    public async ValueTask HandleAsync(IGameSession session, ReadOnlyMemory<byte> data)
    {
        var reader = new PacketReader(data.Span);
        string account = reader.ReadString(20).Trim();
        string password = reader.ReadString(20).Trim();

        var acc = await _accountRepo.FindByAccountAsync(account);
        if (acc == null)
        {
            // Auto register on first login for convenience
            string hash = BCrypt.Net.BCrypt.HashPassword(password);
            int newId = await _accountRepo.CreateAccountAsync(account, hash);
            acc = (newId, account, hash, 0);
        }
        else if (!BCrypt.Net.BCrypt.Verify(password, acc.Value.PasswordHash))
        {
            await _responseSender.SendLoginResultAsync(session, 0, "รหัสผ่านไม่ถูกต้อง");
            return;
        }

        session.AccountId = acc.Value.Id;
        session.AccountName = acc.Value.Account;
        session.GmLevel = acc.Value.GmLevel;
        session.State = SessionState.InCharacterSelect;

        await _responseSender.SendLoginResultAsync(session, 1, "เข้าสู่ระบบสำเร็จ");

        var characters = await _charRepo.GetCharactersByAccountIdAsync(session.AccountId);
        await _responseSender.SendCharListAsync(session, characters);
    }
}
