using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// M9 Stash 초과 보관과 영지 출격 게이트. 상태 소유자는 기존 LootRuntimeState를 유지한다.
public static partial class LootRuntimeState
{
    private static readonly List<LootItemInstance> temporaryStorage = new List<LootItemInstance>();

    public static IReadOnlyList<LootItemInstance> TemporaryStorage => temporaryStorage;
    public static bool HasTemporaryStorageItems => temporaryStorage.Count > 0;
    public static bool CanStartExpedition => temporaryStorage.Count == 0 && RunNestedItemCount <= M11PouchConfig.CarryLimit;
    public static string ExpeditionBlockedReason => temporaryStorage.Count > 0 ? "임시 보관함을 정리하세요"
        : RunNestedItemCount > M11PouchConfig.CarryLimit
            ? $"무게 초과 — 현재 {RunNestedItemCount}/{M11PouchConfig.CarryLimit}"
            : null;

    private static void ResetM9Storage() => temporaryStorage.Clear();

    private static bool StoreIncomingItem(LootItemInstance item)
    {
        if (item == null) return false;
        EnsureStashGrid();
        if (stashGrid.TryAutoPlace(item, out _))
        {
            stash.Add(item);
            return true;
        }
        temporaryStorage.Add(item);
        return false;
    }

    public static bool MoveTemporaryItemToStash(string instanceId)
    {
        LootItemInstance item = temporaryStorage.Find(value => value?.InstanceId == instanceId);
        if (item == null) return Fail("임시 보관함 아이템을 찾을 수 없습니다.");
        EnsureStashGrid();
        if (!stashGrid.TryAutoPlace(item, out _)) return Fail("Stash 공간이 부족합니다.");
        temporaryStorage.Remove(item);
        stash.Add(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    // 결정 45: 임시 보관함 표시용 격자. 위치는 저장하지 않고, 큰 아이템부터 채우며 다 들어갈 때까지 행을 늘린다.
    public static LootGridModel BuildTemporaryStorageGrid(int width)
    {
        width = Math.Max(1, width);
        var items = temporaryStorage.Where(item => item?.Definition != null)
            .OrderByDescending(item => item.Definition.GridWidth * item.Definition.GridHeight).ToList();
        int height = 1;
        foreach (LootItemInstance item in items)
            height = Math.Max(height, Math.Min(item.Definition.GridWidth, item.Definition.GridHeight));
        const int MaxRows = 200;
        while (true)
        {
            var grid = new LootGridModel(width, height);
            bool allPlaced = items.All(item => grid.TryAutoPlace(item, out _));
            if (allPlaced || height >= MaxRows) return grid;
            height++;
        }
    }

    public static bool MoveTemporaryItemToStashAt(string instanceId, int x, int y, bool rotated)
    {
        LootItemInstance item = temporaryStorage.Find(value => value?.InstanceId == instanceId);
        if (item == null) return Fail("임시 보관함 아이템을 찾을 수 없습니다.");
        EnsureStashGrid();
        if (!stashGrid.TryPlace(item, x, y, rotated)) return Fail("Stash의 해당 위치에 놓을 수 없습니다.");
        temporaryStorage.Remove(item);
        stash.Add(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveTemporaryItemIntoBagAt(string instanceId, string bagInstanceId, int x, int y, bool rotated)
    {
        LootItemInstance item = temporaryStorage.Find(value => value?.InstanceId == instanceId);
        LootItemInstance bag = FindStoredItemRecursive(bagInstanceId);
        if (item == null) return Fail("임시 보관함 아이템을 찾을 수 없습니다.");
        if (!CanInsertIntoBag(item, bag, out string reason)) return Fail(reason);
        if (!bag.BagGrid.TryPlace(item, x, y, rotated)) return Fail("가방의 해당 위치에 놓을 수 없습니다.");
        temporaryStorage.Remove(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool IsInTemporaryStorage(string instanceId) => temporaryStorage.Exists(value => value?.InstanceId == instanceId);

    public static bool MoveTemporaryItemIntoBag(string instanceId, string bagInstanceId)
    {
        LootItemInstance item = temporaryStorage.Find(value => value?.InstanceId == instanceId);
        LootItemInstance bag = FindStoredItemRecursive(bagInstanceId);
        if (item == null) return Fail("임시 보관함 아이템을 찾을 수 없습니다.");
        if (!CanInsertIntoBag(item, bag, out string reason)) return Fail(reason);
        if (!bag.BagGrid.TryAutoPlace(item, out _)) return Fail("가방 공간이 부족합니다.");
        temporaryStorage.Remove(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool DeleteTemporaryItem(string instanceId, bool confirmedNestedContents)
    {
        LootItemInstance item = temporaryStorage.Find(value => value?.InstanceId == instanceId);
        if (item == null) return Fail("임시 보관함 아이템을 찾을 수 없습니다.");
        if (item.Definition?.Type == LootItemType.Bag && item.BagGrid.Placements.Count > 0 && !confirmedNestedContents)
            return Fail("가방 안 아이템도 함께 삭제됩니다. 다시 확인하세요.");
        temporaryStorage.Remove(item);
        RemoveInsuranceRecursive(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    private static void RemoveInsuranceRecursive(LootItemInstance item)
    {
        if (item == null) return;
        insuredThisRun.Remove(item.InstanceId);
        insuredItemIds.Remove(item.InstanceId);
        droppedInsuredThisRun.Remove(item.InstanceId);
        if (item.BagGrid == null) return;
        foreach (LootGridPlacement child in item.BagGrid.Placements) RemoveInsuranceRecursive(child.Item);
    }

    public static void RestoreTemporaryStorage(IEnumerable<LootItemInstance> restored)
    {
        temporaryStorage.Clear();
        if (restored != null) temporaryStorage.AddRange(restored.Where(item => item != null));
        Changed?.Invoke();
    }

    public static void RestoreM9Storage(IEnumerable<LootGridRestoreItem> restoredStash,
        IEnumerable<LootItemInstance> restoredTemporary)
    {
        ResetM9Trade();
        var runBagOverflow = temporaryStorage.Where(item => item != null).ToArray();
        stash.Clear();
        temporaryStorage.Clear();
        stashGrid = new LootGridModel(M9StashConfig.BaseWidth, CurrentStashHeight());
        temporaryStorage.AddRange(runBagOverflow);
        if (restoredTemporary != null) temporaryStorage.AddRange(restoredTemporary.Where(item => item != null));
        if (restoredStash != null)
        {
            foreach (LootGridRestoreItem restored in restoredStash)
            {
                LootItemInstance item = restored?.Item;
                bool placed = item != null && ((restored.HasPlacement
                    && stashGrid.TryPlace(item, restored.X, restored.Y, restored.Rotated))
                    || stashGrid.TryAutoPlace(item, out _));
                if (placed) stash.Add(item);
                else if (item != null)
                {
                    temporaryStorage.Add(item);
                    Debug.LogWarning($"[Save] Stash에 재배치할 수 없는 기존 v6 아이템을 임시 보관함으로 이동했습니다: {item.Definition?.Name}");
                }
            }
        }
        LastFailureReason = null;
        Changed?.Invoke();
    }
}
