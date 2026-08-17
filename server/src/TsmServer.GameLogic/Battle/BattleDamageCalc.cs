namespace TsmServer.GameLogic.Battle;

public static class BattleDamageCalc
{
    public static int CalculatePhysicalDamage(
        int attackerAtk,
        int attackerLevel,
        int attackerElem,
        int defenderDef,
        int defenderLevel,
        int defenderElem,
        int skillPower = 100)
    {
        double elemMultiplier = ElementTable.GetMultiplier(attackerElem, defenderElem);
        double baseDamage = Math.Max(1, (attackerAtk * 1.5) - defenderDef);
        double levelBonus = 1.0 + Math.Max(-0.5, (attackerLevel - defenderLevel) * 0.02);
        double skillMultiplier = skillPower / 100.0;

        int finalDamage = (int)(baseDamage * levelBonus * elemMultiplier * skillMultiplier);
        return Math.Max(1, finalDamage);
    }

    public static int CalculateMagicDamage(
        int attackerMatk,
        int attackerLevel,
        int attackerElem,
        int defenderMdef,
        int defenderLevel,
        int defenderElem,
        int skillPower = 100)
    {
        double elemMultiplier = ElementTable.GetMultiplier(attackerElem, defenderElem);
        double baseDamage = Math.Max(1, (attackerMatk * 1.8) - (defenderMdef * 0.8));
        double levelBonus = 1.0 + Math.Max(-0.5, (attackerLevel - defenderLevel) * 0.02);
        double skillMultiplier = skillPower / 100.0;

        int finalDamage = (int)(baseDamage * levelBonus * elemMultiplier * skillMultiplier);
        return Math.Max(1, finalDamage);
    }
}
