namespace TsmServer.Domain.Data;

public record EveResult(
    int ResultGroupNo,
    int ResultNo,
    int ResultType,
    int ResultClass,
    int Parameter,
    int ParameterStyle,
    int ResultValue,
    int ResultMeanNo
);

public record EveCondition(
    int ConditionNo,
    int ConditionClass,
    int ConditionParameter,
    int ConditionParameterStyle,
    int ConditionOps,
    int ConditionValue,
    int ConditionSubItem,
    int ToResult,
    int AndNum,
    IReadOnlyList<EveResult> Results
);

public record NpcEventData(
    int EveNo,
    IReadOnlyList<bool> WhenHappen,
    IReadOnlyList<EveCondition> Conditions
);

public record EveNpcPlacement(
    int Id,
    int NpcId,
    IReadOnlyList<int> Events,
    int X = 0,
    int Y = 0,
    bool Close = false,
    int MotionType = 1,
    int MotionBack = 1,
    int MotionCycleNum = 0,
    int Direction = 0,
    int MotionSuspendMs = 0,
    int MotionSpeedLv = 1,
    IReadOnlyList<(int X, int Y)>? MotionNodes = null,
    int TraceRadius = 0
);

public record SceneEveData(
    int SceneId,
    IReadOnlyList<EveNpcPlacement> Npcs,
    IReadOnlyDictionary<int, NpcEventData> Events
);
