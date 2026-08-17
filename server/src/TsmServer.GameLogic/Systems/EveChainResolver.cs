using TsmServer.Domain.Data;

namespace TsmServer.GameLogic.Systems;

public static class EveChainResolver
{
    public static EveCondition? ResolveMatchingCondition(IReadOnlyList<EveCondition> conditions, PlayerEventState state)
    {
        int i = 0;
        while (i < conditions.Count)
        {
            var cond = conditions[i];
            int andCount = cond.AndNum;

            if (andCount <= 1)
            {
                if (EveConditionEvaluator.Evaluate(cond, state))
                    return cond;
                i++;
            }
            else
            {
                // AND chain evaluation
                bool allPassed = true;
                for (int j = 0; j < andCount && (i + j) < conditions.Count; j++)
                {
                    if (!EveConditionEvaluator.Evaluate(conditions[i + j], state))
                    {
                        allPassed = false;
                        break;
                    }
                }

                if (allPassed)
                    return cond;

                i += andCount;
            }
        }

        return null;
    }
}
