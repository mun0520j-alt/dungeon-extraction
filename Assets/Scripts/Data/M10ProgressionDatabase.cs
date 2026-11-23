using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public sealed class M10LevelDefinition
{
    public int Level, CumulativeXP, HPBonus, AttackBonus, DefenseBonus;
}

public sealed class M10PotionDefinition
{
    public string ItemId;
    public int HealPercent;
    public float DurationTicks, UseTicks;
}

public static class M10ProgressionDatabase
{
    private static List<M10LevelDefinition> levels;
    private static Dictionary<string, M10PotionDefinition> potions;
    public static IReadOnlyList<M10LevelDefinition> Levels { get { EnsureLoaded(); return levels; } }
    public static IEnumerable<M10PotionDefinition> Potions { get { EnsureLoaded(); return potions.Values; } }

    public static M10LevelDefinition Level(int value)
    {
        EnsureLoaded();
        return levels.Find(entry => entry.Level == Mathf.Clamp(value, 1, 12)) ?? levels[0];
    }

    public static M10PotionDefinition Potion(string itemId)
    {
        EnsureLoaded();
        return itemId != null && potions.TryGetValue(itemId, out var value) ? value : null;
    }

    private static void EnsureLoaded()
    {
        if (levels != null) return;
        levels = new List<M10LevelDefinition>();
        potions = new Dictionary<string, M10PotionDefinition>(StringComparer.Ordinal);
        LoadLevels(Resources.Load<TextAsset>("Data/m10_levels"));
        LoadPotions(Resources.Load<TextAsset>("Data/m10_potions"));
        if (levels.Count == 0) levels.Add(new M10LevelDefinition { Level = 1 });
    }

    private static void LoadLevels(TextAsset csv)
    {
        if (csv == null) { Debug.LogError("[M10Progression] m10_levels.csv 누락"); return; }
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] f = lines[i].Split(','); if (f.Length < 5) continue;
            levels.Add(new M10LevelDefinition { Level=I(f[0]), CumulativeXP=I(f[1]), HPBonus=I(f[2]), AttackBonus=I(f[3]), DefenseBonus=I(f[4]) });
        }
        levels.Sort((a,b) => a.Level.CompareTo(b.Level));
    }

    private static void LoadPotions(TextAsset csv)
    {
        if (csv == null) { Debug.LogError("[M10Progression] m10_potions.csv 누락"); return; }
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] f = lines[i].Split(','); if (f.Length < 4) continue;
            var value = new M10PotionDefinition { ItemId=f[0], HealPercent=I(f[1]), DurationTicks=F(f[2]), UseTicks=F(f[3]) };
            potions[value.ItemId] = value;
        }
    }

    private static int I(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : 0;
    private static float F(string value) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : 0f;
}
