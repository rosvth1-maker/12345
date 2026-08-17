using TsmServer.Domain.Enums;
using TsmServer.Domain.ValueObjects;

namespace TsmServer.Domain.Interfaces;

public interface IGameSession
{
    string SessionId { get; }
    SessionState State { get; set; }
    int AccountId { get; set; }
    string AccountName { get; set; }
    int CharacterId { get; set; }
    string CharacterName { get; set; }
    PlayerDataDto? PlayerData { get; set; }
    int CurrentMapId { get; set; }
    int X { get; set; }
    int Y { get; set; }
    int GmLevel { get; set; }

    ValueTask SendAsync(byte[] packet);
    ValueTask CloseAsync();
}
