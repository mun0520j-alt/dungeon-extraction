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
        ok &= M10FloorDecorationDatabase.Find("classic")?.FloorDensityPerThousand == 0;
        ok &= M10FloorDecorationDatabase.Find("swamp") != null
            && M10FloorDecorationDatabase.Find("frost") != null
            && M10FloorDecorationDatabase.Find("orc_keep") != null;
        M10DecorationVisualCatalog decorations = Resources.Load<M10DecorationVisualCatalog>("Data/M10DecorationVisualCatalog");
        ok &= decorations != null && decorations.EntryCount >= 8;
        ok &= M10DungeonConfig.EscapeScrollRestockTicks == 10000
            && M10DungeonConfig.EscapeScrollRestockAmount == 3
            && M10DungeonConfig.EscapeScrollStockCap == 3;
        if (!ok) throw new InvalidOperationException("M10 data validation failed. CSV/clock/floor configuration을 확인하세요.");
        Debug.Log("[M10 Validation] PASS — 10층, 보스 5/10층, MIA 2880, 몬스터·드롭·장식·스크롤 상점 CSV 확인.");
    }
}
#endif
