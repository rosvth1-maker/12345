using TsmServer.Domain.Data;
using TsmServer.GameLogic.Systems;
using Xunit;

namespace TsmServer.Tests;

public class EveConditionEvaluatorTests
{
    [Theory]
    [InlineData(5, 0, 5, true)]
    [InlineData(5, 0, 3, false)]
    [InlineData(3, 2, 5, true)]
    [InlineData(5, 2, 5, false)]
    [InlineData(5, 3, 5, true)]
    [InlineData(6, 3, 5, false)]
    [InlineData(6, 4, 5, true)]
    [InlineData(5, 4, 5, false)]
    [InlineData(5, 5, 5, true)]
    [InlineData(4, 5, 5, false)]
    [InlineData(5, 6, 3, true)]
    [InlineData(5, 6, 5, false)]
    public void CompareOps_Should_Evaluate_All_7_Operators_Correctly(int actual, int ops, int expected, bool result)
    {
        Assert.Equal(result, EveConditionEvaluator.CompareOps(actual, ops, expected));
    }

    [Fact]
    public void Evaluate_Level_Condition_Should_Match_Player_Level()
    {
        var cond = new EveCondition(1, 1, 0, 0, 5, 10, 0, 0, 1, Array.Empty<EveResult>());
        var passState = new PlayerEventState(Level: 10);
        var failState = new PlayerEventState(Level: 9);

        Assert.True(EveConditionEvaluator.Evaluate(cond, passState));
        Assert.False(EveConditionEvaluator.Evaluate(cond, failState));
    }
}
