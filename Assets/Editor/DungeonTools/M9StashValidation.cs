using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class M9StashValidation
{
    [MenuItem("Dungeon Tools/QA/M9 Stash Validation")]
    public static void Run()
    {
        var failures = new List<string>();
        Check(M9StashConfig.BaseWidth == 10 && M9StashConfig.BaseHeight == 8, "기본 Stash 10x8", failures);
        Check(M9StashConfig.RowsAfterQ03 == 2 && M9StashConfig.RowsAfterQ06 == 2, "퀘스트 확장 +2/+2", failures);
        Check(LootRuntimeState.MaxBagNestingDepth == 4, "가방 최대 중첩 4", failures);
        ValidateGrid(failures);
        ValidateNestedBags(failures);
        ValidateSaveContract(failures);

        if (failures.Count == 0)
        {
            Debug.Log("[M9 Validation] PASS — CSV 크기·격자 배치·중첩 4단계·임시 보관함·세이브 v6 계약");
            return;
        }
        Debug.LogError("[M9 Validation] FAIL\n- " + string.Join("\n- ", failures));
    }

    private static void ValidateGrid(List<string> failures)
    {
        var grid = new LootGridModel(10, 8);
        LootItemInstance wide = Item("qa_wide", LootItemType.Armor, 2, 3);
        LootItemInstance longItem = Item("qa_long", LootItemType.Weapon, 1, 3);
        Check(grid.TryPlace(wide, 0, 0, false), "Stash 배치", failures);
        Check(!grid.TryPlace(longItem, 1, 1, false), "겹침 차단", failures);
        Check(grid.TryPlace(longItem, 2, 0, true), "회전 배치", failures);
    }

    private static void ValidateNestedBags(List<string> failures)
    {
        LootItemInstance root = Bag("qa_bag_1");
        LootItemInstance current = root;
        for (int depth = 2; depth <= 4; depth++)
        {
            LootItemInstance child = Bag($"qa_bag_{depth}");
            Check(current.BagGrid.TryAutoPlace(child, out _), $"가방 {depth}단계 배치", failures);
            current = child;
        }
        Check(BagDepth(root, new HashSet<string>(StringComparer.Ordinal)) == 4, "가방 깊이 계산", failures);
        Check(Contains(root, current.InstanceId, new HashSet<string>(StringComparer.Ordinal)), "하위 가방 탐색", failures);
        Check(!Contains(current, root.InstanceId, new HashSet<string>(StringComparer.Ordinal)), "자기 포함 순환 없음", failures);
        Check(LootRuntimeState.TemporaryStorage != null, "임시 보관함 상태 소유", failures);
    }

    private static void ValidateSaveContract(List<string> failures)
    {
        Type saveService = typeof(M5SaveService);
        FieldInfo version = saveService.GetField("CurrentSaveVersion", BindingFlags.NonPublic | BindingFlags.Static);
        Check(version != null && (int)version.GetRawConstantValue() == 6, "save_version 6", failures);
        Check(!string.IsNullOrEmpty(M5SaveService.LegacyV5BackupPath)
            && M5SaveService.LegacyV5BackupPath.EndsWith("save_v5.bak", StringComparison.OrdinalIgnoreCase),
            "v5 백업 경로", failures);
        Type saveData = saveService.GetNestedType("SaveData", BindingFlags.NonPublic);
        Type savedItem = saveService.GetNestedType("SavedItem", BindingFlags.NonPublic);
        Check(saveData?.GetField("temporary_storage") != null, "임시 보관함 저장 필드", failures);
        Check(savedItem?.GetField("children") != null, "재귀 가방 저장 필드", failures);
        Check(savedItem?.GetField("x") != null && savedItem.GetField("rotated") != null, "배치·회전 저장 필드", failures);
    }

    private static LootItemInstance Item(string id, LootItemType type, int width, int height)
    {
        return new LootItemInstance(new LootItemData
        {
            Id = id, Name = id, Type = type, GridWidth = width, GridHeight = height,
            BagGridWidth = 4, BagGridHeight = 4
        });
    }

    private static LootItemInstance Bag(string id) => Item(id, LootItemType.Bag, 1, 1);

    private static int BagDepth(LootItemInstance item, HashSet<string> visited)
    {
        if (item?.Definition?.Type != LootItemType.Bag || !visited.Add(item.InstanceId)) return 0;
        int childDepth = 0;
        foreach (LootGridPlacement child in item.BagGrid.Placements)
            childDepth = Math.Max(childDepth, BagDepth(child.Item, visited));
        return 1 + childDepth;
    }

    private static bool Contains(LootItemInstance root, string id, HashSet<string> visited)
    {
        if (root == null || !visited.Add(root.InstanceId)) return false;
        if (root.InstanceId == id) return true;
        if (root.BagGrid == null) return false;
        foreach (LootGridPlacement child in root.BagGrid.Placements)
            if (Contains(child.Item, id, visited)) return true;
        return false;
    }

    private static void Check(bool condition, string label, List<string> failures)
    {
        if (!condition) failures.Add(label);
    }
}
