#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class M10DataValidator
{
    [MenuItem("Tools/Dungeon/M10/Print Combat Matchups")]
    public static void PrintCombatMatchups()
    {
        LogMatchup("Goblin — Lv1 기본", 1, "basic_weapon", "basic_shield", "basic_armor", "goblin");
        LogMatchup("Mimic — Lv5 Rare", 5, "weapon_rare", "shield_rare", "armor_rare", "chest_mimic");
        LogMatchup("Orc — Lv12 Unique", 12, "weapon_unique", "shield_unique", "armor_unique", "orc_warrior");
        LogMatchup("Orc — Lv1 Legendary", 1, "weapon_legendary", "shield_legendary", "armor_legendary", "orc_warrior");
        LogMatchup("Orc — Lv12 기본", 12, "basic_weapon", "basic_shield", "basic_armor", "orc_warrior");
    }

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
        var expectedMidExits = new System.Collections.Generic.HashSet<int> { 3, 5, 7 };
        var configuredMidExits = new System.Collections.Generic.HashSet<int>(M10DungeonConfig.MidExitFloors);
        ok &= configuredMidExits.SetEquals(expectedMidExits);
        foreach (int floor in configuredMidExits)
            ok &= floor >= 1 && floor < M10DungeonConfig.FinalFloor
                && (!M10DungeonConfig.IsBossFloor(floor) || floor == M10DungeonConfig.FirstBossFloor);
        Color exitZone = M10DungeonConfig.ExitZoneColor;
        ok &= Mathf.Approximately(exitZone.r, 32f / 255f)
            && Mathf.Approximately(exitZone.g, 1f)
            && Mathf.Approximately(exitZone.b, 192f / 255f)
            && Mathf.Approximately(exitZone.a, 0.6f);
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
        var expectedEquipment = new System.Collections.Generic.Dictionary<string, (int attack, int defense, int hp)>
        {
            { "basic_weapon", (15, 0, 0) }, { "basic_shield", (0, 4, 10) }, { "basic_armor", (0, 5, 20) },
            { "weapon_normal", (20, 0, 0) }, { "shield_normal", (0, 5, 15) }, { "armor_normal", (0, 6, 25) },
            { "weapon_rare", (35, 0, 0) }, { "shield_rare", (0, 8, 30) }, { "armor_rare", (0, 10, 45) },
            { "weapon_epic", (55, 0, 0) }, { "shield_epic", (0, 12, 50) }, { "armor_epic", (0, 15, 75) },
            { "weapon_unique", (80, 0, 0) }, { "shield_unique", (0, 16, 75) }, { "armor_unique", (0, 21, 110) },
            { "weapon_legendary", (110, 0, 0) }, { "shield_legendary", (0, 22, 110) }, { "armor_legendary", (0, 28, 160) }
        };
        foreach (var pair in expectedEquipment)
        {
            LootItemData item = LootItemDatabase.FindById(pair.Key);
            ok &= item != null && item.AttackBonus == pair.Value.attack
                && item.DefenseBonus == pair.Value.defense && item.HPBonus == pair.Value.hp;
        }
        ok &= M10ProgressionDatabase.Levels.Count == 12;
        int previousXP = -1;
        foreach (M10LevelDefinition level in M10ProgressionDatabase.Levels)
        {
            ok &= level.CumulativeXP > previousXP;
            ok &= level.HPBonus == (level.Level - 1) * 25
                && level.AttackBonus == (level.Level - 1) * 2
                && level.DefenseBonus == (level.Level - 1);
            previousXP = level.CumulativeXP;
        }
        foreach (string potionId in new[] { "potion_small", "potion_medium", "potion_large" })
        {
            LootItemData item = LootItemDatabase.FindById(potionId);
            M10PotionDefinition potion = M10ProgressionDatabase.Potion(potionId);
            ok &= item != null && item.Type == LootItemType.Consumable && potion != null
                && Mathf.Approximately(potion.DurationTicks, 10f) && Mathf.Approximately(potion.UseTicks, 2f)
                && potion.InstantPercent == 50;
        }
        ok &= M10ProgressionDatabase.Potion("potion_large")?.HealPercent == 70;
        var expectedCombat = new System.Collections.Generic.Dictionary<string, (int hp, int attack, int defense)>
        {
            { "goblin", (126, 25, 8) }, { "goblin_f3", (196, 38, 10) },
            { "goblin_elite_f3", (441, 61, 12) }, { "chest_mimic", (640, 81, 14) },
            { "goblin_f6", (264, 64, 13) }, { "goblin_f8", (344, 91, 15) },
            { "goblin_elite_f6", (595, 106, 16) }, { "orc_warrior", (1344, 243, 18) }
        };
        foreach (var pair in expectedCombat)
        {
            M10EnemyDefinition enemy = M10EnemyDatabase.Find(pair.Key);
            ok &= enemy != null && enemy.HP == pair.Value.hp && enemy.Attack == pair.Value.attack
                && enemy.Defense == pair.Value.defense;
        }
        foreach (string enemyId in new[] { "goblin", "goblin_f3", "goblin_f6", "goblin_f8" })
            ok &= Mathf.Approximately(M10EnemyDatabase.Find(enemyId).PotionDropChance, 0.10f);
        ok &= Matchup(1, "basic_weapon", "basic_shield", "basic_armor", "goblin") == (3, 7);
        ok &= Matchup(5, "weapon_rare", "shield_rare", "armor_rare", "chest_mimic") == (10, 5);
        ok &= Matchup(12, "weapon_unique", "shield_unique", "armor_unique", "orc_warrior") == (12, 4);
        var legendaryOrc = Matchup(1, "weapon_legendary", "shield_legendary", "armor_legendary", "orc_warrior");
        var basicOrc = Matchup(12, "basic_weapon", "basic_shield", "basic_armor", "orc_warrior");
        ok &= legendaryOrc.hitsToKill < basicOrc.hitsToKill;

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
            if (visual.attackFrames != null)
                foreach (Sprite frame in visual.attackFrames) ok &= FrameBelongsTo(frame, enemy.SpriteKey);
        }
        M10EnemyVisualCatalog.Entry mimic = enemyVisuals?.Find("chest_mimic");
        ok &= mimic?.idleFrames?.Length == 1 && mimic?.runFrames?.Length == 1
            && mimic?.attackFrames?.Length == 3;
        ok &= M10DungeonConfig.FastArrivalBonusPercent == 100
            && M10DungeonConfig.DamageFormulaK > 0
            && M10DungeonConfig.RestHealPercent > 0 && M10DungeonConfig.RestHealPercent <= 100
            && M10DungeonConfig.RestHealPercentAfterBoss > 0 && M10DungeonConfig.RestHealPercentAfterBoss <= 100
            && M10DungeonConfig.BossAoeTelegraphTicks >= TickCost.Attack
            && Mathf.Approximately(M10DungeonConfig.BossAoeVfxSeconds, 0.4f)
            && Mathf.Approximately(M10DungeonConfig.BossAoeShakeStrength, 0.18f)
            && Mathf.Approximately(M10DungeonConfig.BossAoeShakeSeconds, 0.25f);
        if (!ok) throw new InvalidOperationException("M10 data validation failed. CSV/clock/floor configuration을 확인하세요.");
        PrintCombatMatchups();
        Debug.Log($"[M10 Validation] PASS — % 피해 K={M10DungeonConfig.DamageFormulaK}, 장비 HP, Lv당 HP+25/공+2/방+1, 휴식 {M10DungeonConfig.RestHealPercent}%·보스 후 {M10DungeonConfig.RestHealPercentAfterBoss}%, 일반몹 포션 10%, 보스 광역 예고 {M10DungeonConfig.BossAoeTelegraphTicks}Tick 확인.");
    }

    [MenuItem("Tools/Dungeon/M11/Validate Data")]
    public static void ValidateM11()
    {
        Validate();
        bool ok = M11TimeConfig.NightStartTicks == 1800
            && M11TimeConfig.SevenFloorExitCloseTicks == 2220
            && M11TimeConfig.NightSpawnCount == 2
            && M11TimeConfig.OrcCrossRadius == 3
            && M11PouchConfig.CarryLimit == 14
            && M11PouchConfig.Width(1) == 2 && M11PouchConfig.Height(1) == 2
            && M11PouchConfig.Width(2) == 2 && M11PouchConfig.Height(2) == 3
            && M11PouchConfig.Width(3) == 3 && M11PouchConfig.Height(3) == 3;
        LootItemData potionPouch = LootItemDatabase.FindById("pouch_potion");
        LootItemData junkCase = LootItemDatabase.FindById("case_junk");
        ok &= potionPouch?.BagGridWidth == 2 && potionPouch.BagGridHeight == 2
            && junkCase?.BagGridWidth == 3 && junkCase.BagGridHeight == 3;
        if (!ok) throw new InvalidOperationException("M11 data validation failed.");
        Debug.Log("[M11 Validation] PASS — 심야 1800, 7층 폐쇄 2220, 보호 주머니 4/6/9칸, 무게 14, 오크 십자 확인.");
    }

    private static void LogMatchup(string label, int level, string weaponId, string shieldId, string armorId, string enemyId)
    {
        M10LevelDefinition growth = M10ProgressionDatabase.Level(level);
        LootItemData weapon = LootItemDatabase.FindById(weaponId);
        LootItemData shield = LootItemDatabase.FindById(shieldId);
        LootItemData armor = LootItemDatabase.FindById(armorId);
        M10EnemyDefinition enemy = M10EnemyDatabase.Find(enemyId);
        int playerAttack = 30 + growth.AttackBonus + weapon.AttackBonus;
        int playerDefense = 25 + growth.DefenseBonus + shield.DefenseBonus + armor.DefenseBonus;
        int playerHP = 100 + growth.HPBonus + shield.HPBonus + armor.HPBonus;
        int dealt = CombatDamage.Calculate(playerAttack, enemy.Defense);
        int taken = CombatDamage.Calculate(enemy.Attack, playerDefense);
        int hitsToKill = Mathf.CeilToInt(enemy.HP / (float)dealt);
        int hitsToDie = Mathf.CeilToInt(playerHP / (float)taken);
        Debug.Log($"[M10 Matchup] {label}: Player HP/공/방 {playerHP}/{playerAttack}/{playerDefense}, "
            + $"Enemy HP/공/방 {enemy.HP}/{enemy.Attack}/{enemy.Defense}, 피해 {dealt}/{taken}, 처치/생존 {hitsToKill}/{hitsToDie}대");
    }

    private static (int hitsToKill, int hitsToDie) Matchup(
        int level, string weaponId, string shieldId, string armorId, string enemyId)
    {
        M10LevelDefinition growth = M10ProgressionDatabase.Level(level);
        LootItemData weapon = LootItemDatabase.FindById(weaponId);
        LootItemData shield = LootItemDatabase.FindById(shieldId);
        LootItemData armor = LootItemDatabase.FindById(armorId);
        M10EnemyDefinition enemy = M10EnemyDatabase.Find(enemyId);
        int playerAttack = 30 + growth.AttackBonus + weapon.AttackBonus;
        int playerDefense = 25 + growth.DefenseBonus + shield.DefenseBonus + armor.DefenseBonus;
        int playerHP = 100 + growth.HPBonus + shield.HPBonus + armor.HPBonus;
        return (Mathf.CeilToInt(enemy.HP / (float)CombatDamage.Calculate(playerAttack, enemy.Defense)),
            Mathf.CeilToInt(playerHP / (float)CombatDamage.Calculate(enemy.Attack, playerDefense)));
    }

    private static bool FrameBelongsTo(Sprite frame, string key)
    {
        return frame != null && frame.name.StartsWith(key + "_", StringComparison.Ordinal);
    }
}
#endif
