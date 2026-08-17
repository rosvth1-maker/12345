using TsmServer.Domain.Data;

namespace TsmServer.GameLogic.Systems;

public record PlayerEventState(
    int Level = 1,
    long Gold = 0,
    IReadOnlyDictionary<int, int>? MissionSteps = null,
    IReadOnlySet<int>? CompletedMissions = null,
    IReadOnlyDictionary<int, int>? ItemCounts = null,
    IReadOnlySet<int>? PetIds = null,
    IReadOnlyDictionary<int, int>? BitFlags = null
);

public static class EveConditionEvaluator
{
    public static bool Evaluate(EveCondition condition, PlayerEventState state)
    {
        return condition.ConditionClass switch
        {
            0 => true, // Unconditional
            1 => EvaluateLevel(condition, state.Level),
            2 => EvaluateMissionStep(condition, state.MissionSteps),
            3 => EvaluateItem(condition, state.ItemCounts),
            4 => EvaluateGold(condition, state.Gold),
            5 => EvaluatePet(condition, state.PetIds),
            6 => EvaluateBitFlag(condition, state.BitFlags),
            _ => true
        };
    }

    public static bool CompareOps(int actual, int ops, int expected)
    {
        return ops switch
        {
            0 or 1 => actual == expected,
            2 => actual < expected,
            3 => actual <= expected,
            4 => actual > expected,
            5 => actual >= expected,
            6 => actual != expected,
            _ => actual == expected
        };
    }

    private static bool EvaluateLevel(EveCondition cond, int level) =>
        CompareOps(level, cond.ConditionOps, cond.ConditionValue);

    private static bool EvaluateMissionStep(EveCondition cond, IReadOnlyDictionary<int, int>? missions)
    {
        int step = missions != null && missions.TryGetValue(cond.ConditionParameter, out int s) ? s : 0;
        return CompareOps(step, cond.ConditionOps, cond.ConditionValue);
    }

    private static bool EvaluateItem(EveCondition cond, IReadOnlyDictionary<int, int>? items)
    {
        int count = items != null && items.TryGetValue(cond.ConditionParameter, out int c) ? c : 0;
        return CompareOps(count, cond.ConditionOps, cond.ConditionValue);
    }

    private static bool EvaluateGold(EveCondition cond, long gold) =>
        CompareOps((int)Math.Min(int.MaxValue, gold), cond.ConditionOps, cond.ConditionValue);

    private static bool EvaluatePet(EveCondition cond, IReadOnlySet<int>? pets)
    {
        bool hasPet = pets != null && pets.Contains(cond.ConditionParameter);
        return cond.ConditionOps == 0 ? hasPet : !hasPet;
    }

    private static bool EvaluateBitFlag(EveCondition cond, IReadOnlyDictionary<int, int>? flags)
    {
        int val = flags != null && flags.TryGetValue(cond.ConditionParameter, out int f) ? f : 0;
        return CompareOps(val, cond.ConditionOps, cond.ConditionValue);
    }
}
