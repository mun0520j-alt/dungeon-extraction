using UnityEngine;

public static class CombatDamage
{
    public static int Calculate(int attack, int defense)
    {
        int k = M10DungeonConfig.DamageFormulaK;
        float raw = Mathf.Max(0, attack) * k / (float)(k + Mathf.Max(0, defense));
        return Mathf.Max(1, Mathf.RoundToInt(raw));
    }
}
