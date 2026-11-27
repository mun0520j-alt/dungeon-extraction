using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public sealed class M10EnemyDefinition
{
    public string Id, SpriteKey, Tier;
    public int HP, Attack, Defense, XP;
    public float MoveCostMultiplier, VisualScale, ScrollDropChance, PotionDropChance;
    public string AttackType;
    public int Range, RangedDamagePercent = 100, MeleeDamagePercent = 100;
    public EnemyBehaviorType Behavior;
    public bool IsElite => string.Equals(Tier, "elite", StringComparison.OrdinalIgnoreCase);
}

public sealed class M10FloorSpawnDefinition
{
    public int Floor, NormalCount, EliteMin, EliteMax;
    public string[] NormalIds, EliteIds;
    public string ArcherId;
}

public static class M10EnemyDatabase
{
    private static Dictionary<string, M10EnemyDefinition> enemies;
    private static Dictionary<int, M10FloorSpawnDefinition> floors;
    public static M10EnemyDefinition Find(string id) { EnsureLoaded(); return id != null && enemies.TryGetValue(id, out var value) ? value : null; }
    public static IEnumerable<M10EnemyDefinition> All { get { EnsureLoaded(); return enemies.Values; } }
    public static M10FloorSpawnDefinition Floor(int floor) { EnsureLoaded(); return floors.TryGetValue(floor, out var value) ? value : null; }
    public static M10EnemyDefinition Boss(int floor) => Find(floor == M10DungeonConfig.FirstBossFloor ? "chest_mimic" : "orc_warrior");
    private static void EnsureLoaded()
    {
        if (enemies != null) return;
        enemies = new Dictionary<string, M10EnemyDefinition>(StringComparer.Ordinal);
        floors = new Dictionary<int, M10FloorSpawnDefinition>();
        TextAsset enemyCsv = Resources.Load<TextAsset>("Data/m10_enemies");
        if (enemyCsv != null)
        {
            string[] lines = enemyCsv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] f = lines[i].Split(','); if (f.Length < 12) continue;
                Enum.TryParse(f[7], true, out EnemyBehaviorType behavior);
                var d = new M10EnemyDefinition { Id=f[0], SpriteKey=f[1], Tier=f[2], HP=I(f[3]), Attack=I(f[4]), Defense=I(f[5]),
                    MoveCostMultiplier=F(f[6],1), Behavior=behavior, VisualScale=F(f[8],1), ScrollDropChance=F(f[9],0),
                    XP=I(f[10]), PotionDropChance=F(f[11],0), AttackType=f.Length > 12 ? f[12] : "melee",
                    Range=f.Length > 13 ? I(f[13]) : 0, RangedDamagePercent=f.Length > 14 ? I(f[14]) : 100,
                    MeleeDamagePercent=f.Length > 15 ? I(f[15]) : 100 };
                enemies[d.Id] = d;
            }
        }
        TextAsset floorCsv = Resources.Load<TextAsset>("Data/m10_floor_spawns");
        if (floorCsv != null)
        {
            string[] lines = floorCsv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] f = lines[i].Split(','); if (f.Length < 6) continue;
                var d = new M10FloorSpawnDefinition { Floor=I(f[0]), NormalIds=Ids(f[1]), NormalCount=I(f[2]), EliteIds=Ids(f[3]), EliteMin=I(f[4]), EliteMax=I(f[5]), ArcherId=f.Length > 6 ? f[6].Trim() : string.Empty };
                floors[d.Floor] = d;
            }
        }
    }
    private static string[] Ids(string value) => string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : value.Split(';');
    private static int I(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : 0;
    private static float F(string value,float fallback) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : fallback;
}
