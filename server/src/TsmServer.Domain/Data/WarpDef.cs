namespace TsmServer.Domain.Data;

public record WarpDef(
    int Id,
    int FromSceneId,
    int FromX,
    int FromY,
    int ToSceneId,
    int ToX,
    int ToY
);
