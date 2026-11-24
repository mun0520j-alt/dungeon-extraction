#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class M10DataValidator
{
    [MenuItem("Tools/Dungeon/M10/Validate Data")]
    public static void Validate()
    {
        bool ok = ExpeditionSession.FinalFloor == 10 && DungeonClock.MIAThresholdTicks == 2880;
        for (int floor = 1; floor <= 10; floor++)
            if (!M10DungeonConfig.IsBossFloor(floor) && M10EnemyDatabase.Floor(floor) == null) ok = false;
        ok &= Resources.Load<TextAsset>("Data/m10_loot_rarity") != null;
        ok &= M10EnemyDatabase.Boss(5) != null && M10EnemyDatabase.Boss(10) != null;
        foreach (string decorationSet in new[] { "classic", "swamp", "frost", "orc_keep" })
        {
            M10FloorDecorationDefinition decoration = M10FloorDecorationDatabase.Find(decorationSet);
            ok &= decoration != null && decoration.FloorDensityPerThousand == 0
                && decoration.WallDensityPerThousand == 0;
        }
        M10DecorationVisualCatalog decorations = Resources.Load<M10DecorationVisualCatalog>("Data/M10DecorationVisualCatalog");
        ok &= decorations != null && decorations.EntryCount >= 8;
        ok &= M10DungeonConfig.EscapeScrollRestockTicks == 10000
            && M10DungeonConfig.EscapeScrollRestockAmount == 3
            && M10DungeonConfig.EscapeScrollStockCap == 3;
        string[] legendaryIds =
        {
            "weapon_legendary", "shield_legendary", "armor_legendary", "bag_legendary", "junk_orc_seal"
        };
        foreach (string itemId in legendaryIds)
        {
            LootItemData item = LootItemDatabase.FindById(itemId);
            ok &= item != null && item.Rarity == LootRarity.Legendary && item.DropWeight > 0;
            ok &= !LootRuntimeState.IsMerchantPurchaseItem(item);
        }
        LootItemData legendaryBag = LootItemDatabase.FindById("bag_legendary");
        ok &= legendaryBag != null && legendaryBag.GridWidth == 5 && legendaryBag.GridHeight == 6
            && legendaryBag.BagGridWidth == 5 && legendaryBag.BagGridHeight == 6;
        ok &= M10ProgressionDatabase.Levels.Count == 12;
        int previousXP = -1;
        foreach (M10LevelDefinition level in M10ProgressionDatabase.Levels)
        {
            ok &= level.CumulativeXP > previousXP;
            previousXP = level.CumulativeXP;
        }
        foreach (string potionId in new[] { "potion_small", "potion_medium", "potion_large" })
        {
            LootItemData item = LootItemDatabase.FindById(potionId);
            M10PotionDefinition potion = M10ProgressionDatabase.Potion(potionId);
            ok &= item != null && item.Type == LootItemType.Consumable && potion != null
                && Mathf.Approximately(potion.DurationTicks, 10f) && Mathf.Approximately(potion.UseTicks, 2f);
        }
        ok &= M10ProgressionDatabase.Potion("potion_large")?.HealPercent == 70;

        var expectedSpawns = new System.Collections.Generic.Dictionary<int, (string normal, string elite)>
        {
            { 1, ("goblin", "goblin_elite_f3") },
            { 2, ("goblin", "goblin_elite_f3") },
            { 3, ("goblin_f3", "goblin_elite_f3") },
            { 4, ("goblin_f3", "goblin_elite_f3") },
            { 6, ("goblin_f6", "goblin_elite_f6") },
            { 7, ("goblin_f6", "goblin_elite_f6") },
            { 8, ("goblin_f8", "goblin_elite_f6") },
            { 9, ("goblin_f8", "goblin_elite_f6") }
        };
        foreach (var pair in expectedSpawns)
        {
            M10FloorSpawnDefinition spawn = M10EnemyDatabase.Floor(pair.Key);
            ok &= spawn != null && spawn.NormalIds.Length == 1 && spawn.NormalIds[0] == pair.Value.normal
                && spawn.EliteIds.Length == 1 && spawn.EliteIds[0] == pair.Value.elite;
        }
        foreach (string enemyId in new[] { "goblin", "goblin_f3", "goblin_f6", "goblin_f8", "goblin_elite_f3", "goblin_elite_f6" })
            ok &= M10EnemyDatabase.Find(enemyId)?.SpriteKey == "goblin";
        Color eliteTint = M10DungeonConfig.EliteTint;
        ok &= Mathf.Approximately(eliteTint.r, 1f) && Mathf.Approximately(eliteTint.g, 1f)
            && Mathf.Approximately(eliteTint.b, 1f);
        Color eliteOutline = M10DungeonConfig.EliteOutlineColor;
        ok &= M10DungeonConfig.EliteOutlinePixels == 1
            && Mathf.Approximately(eliteOutline.r, 1f)
            && Mathf.Approximately(eliteOutline.g, 48f / 255f)
            && Mathf.Approximately(eliteOutline.b, 48f / 255f);
        for (int floor = 1; floor <= 9; floor++)
        {
            if (M10DungeonConfig.IsBossFloor(floor)) continue;
            M10FloorSpawnDefinition spawn = M10EnemyDatabase.Floor(floor);
            int expected = floor >= 3 ? 1 : 0;
            ok &= spawn != null && spawn.EliteMin == expected && spawn.EliteMax == expected;
        }
        M10EnemyVisualCatalog enemyVisuals = Resources.Load<M10EnemyVisualCatalog>("Data/M10EnemyVisualCatalog");
        foreach (M10EnemyDefinition enemy in M10EnemyDatabase.All)
        {
            M10EnemyVisualCatalog.Entry visual = enemyVisuals != null ? enemyVisuals.Find(enemy.SpriteKey) : null;
            ok &= visual?.idleFrames != null && visual.idleFrames.Length >= 1;
            ok &= visual?.runFrames != null && visual.runFrames.Length >= 1;
            if (visual == null) continue;
            foreach (Sprite frame in visual.idleFrames) ok &= FrameBelongsTo(frame, enemy.SpriteKey);
            foreach (Sprite frame in visual.runFrames) ok &= FrameBelongsTo(frame, enemy.SpriteKey);
        }
        if (!ok) throw new InvalidOperationException("M10 data validation failed. CSV/clock/floor configuration을 확인하세요.");
        Debug.Log("[M10 Validation] PASS — 10층, goblin 구간 스폰, 엘리트 100/40/40, 장식 0, 대형 포션 70%, 보스·드롭·스크롤 상점·Legendary 5종 확인.");
    }

    private static bool FrameBelongsTo(Sprite frame, string key)
    {
        return frame != null && frame.name.StartsWith(key + "_", StringComparison.Ordinal);
    }
}
#endif
