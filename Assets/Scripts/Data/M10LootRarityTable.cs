using System;
using System.Collections.Generic;
using UnityEngine;

public static class M10LootRarityTable
{
    private static readonly Dictionary<int, int[]> Rows = new Dictionary<int, int[]>();
    private static int[] Row(int floor)
    {
        if (Rows.Count == 0)
        {
            TextAsset csv = Resources.Load<TextAsset>("Data/m10_loot_rarity");
            if (csv != null)
            {
                string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] f = lines[i].Split(','); if (f.Length < 12 || !int.TryParse(f[0], out int key)) continue;
                    var values = new int[11]; for (int j = 0; j < values.Length; j++) int.TryParse(f[j + 1], out values[j]);
                    Rows[key] = values;
                }
            }
        }
        return Rows.TryGetValue(Mathf.Clamp(floor, 1, 10), out int[] value) ? value : null;
    }
    public static LootRarity Rarity(int floor, System.Random rng)
    {
        int[] r = Row(floor); if (r == null) return LootRarity.Normal;
        int roll = rng.Next(100); int sum = r[0]; if (roll < sum) return LootRarity.Normal;
        sum += r[1]; if (roll < sum) return LootRarity.Rare; sum += r[2]; if (roll < sum) return LootRarity.Epic;
        sum += r[3]; return roll < sum ? LootRarity.Unique : LootRarity.Legendary;
    }
    public static int BossCount(int floor, System.Random rng) { int[] r=Row(floor); int roll=rng.Next(100); return roll<r[5]?0:roll<r[5]+r[6]?1:2; }
    public static LootRarity BossRarity(int floor, System.Random rng) { int[] r=Row(floor); int roll=rng.Next(100); return roll<r[8]?LootRarity.Rare:roll<r[8]+r[9]?LootRarity.Epic:LootRarity.Unique; }
}
