namespace TsmServer.Domain.Interfaces.Repositories;

public interface IAccountRepository
{
    Task<(int Id, string Account, string PasswordHash, int GmLevel)?> FindByAccountAsync(string account);
    Task<int> CreateAccountAsync(string account, string passwordHash);
    Task UpdateLastLoginAsync(int accountId);
}
