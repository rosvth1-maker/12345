namespace TsmServer.Domain.Enums;

public enum SessionState
{
    Connected = 0,
    Authenticated = 1,
    InCharacterSelect = 2,
    InGame = 3,
    InBattle = 4,
    Disconnected = 5
}
