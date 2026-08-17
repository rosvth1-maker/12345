using TsmServer.GameLogic.Battle;
using Xunit;

namespace TsmServer.Tests;

public class BattleDamageCalcTests
{
    [Fact]
    public void Physical_Damage_With_Element_Advantage_Should_Be_Higher()
    {
        // Earth (1) vs Water (2) -> Advantage
        int advDamage = BattleDamageCalc.CalculatePhysicalDamage(100, 50, 1, 50, 50, 2);
        // Earth (1) vs Wind (4) -> Disadvantage
        int disDamage = BattleDamageCalc.CalculatePhysicalDamage(100, 50, 1, 50, 50, 4);

        Assert.True(advDamage > disDamage);
    }
}
