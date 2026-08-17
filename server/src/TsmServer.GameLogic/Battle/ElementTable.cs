namespace TsmServer.GameLogic.Battle;

public static class ElementTable
{
    // Earth(1) -> Water(2) -> Fire(3) -> Wind(4) -> Earth(1)
    public static double GetMultiplier(int attackerElem, int defenderElem)
    {
        if (attackerElem == 0 || defenderElem == 0) return 1.0;

        if ((attackerElem == 1 && defenderElem == 2) || // Earth beats Water
            (attackerElem == 2 && defenderElem == 3) || // Water beats Fire
            (attackerElem == 3 && defenderElem == 4) || // Fire beats Wind
            (attackerElem == 4 && defenderElem == 1))   // Wind beats Earth
        {
            return 1.35; // Counter element bonus
        }

        if ((attackerElem == 2 && defenderElem == 1) || // Water countered by Earth
            (attackerElem == 3 && defenderElem == 2) || // Fire countered by Water
            (attackerElem == 4 && defenderElem == 3) || // Wind countered by Fire
            (attackerElem == 1 && defenderElem == 4))   // Earth countered by Wind
        {
            return 0.75; // Element disadvantage
        }

        return 1.0;
    }
}
