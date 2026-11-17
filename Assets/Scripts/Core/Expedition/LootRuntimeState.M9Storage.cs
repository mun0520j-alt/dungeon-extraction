using System.Collections.Generic;
using System.Linq;

// M9 Stash 초과 보관과 영지 출격 게이트. 상태 소유자는 기존 LootRuntimeState를 유지한다.
public static partial class LootRuntimeState
{
    private static readonly List<LootItemInstance> temporaryStorage = new List<LootItemInstance>();

    public static IReadOnlyList<LootItemInstance> TemporaryStorage => temporaryStorage;
    public static bool HasTemporaryStorageItems => temporaryStorage.Count > 0;
    public static bool CanStartExpedition => temporaryStorage.Count == 0;
    public static string ExpeditionBlockedReason => CanStartExpedition ? null : "임시 보관함을 정리하세요";

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
        stash.Clear();
        temporaryStorage.Clear();
        stashGrid = new LootGridModel(M9StashConfig.BaseWidth, CurrentStashHeight());
        if (restoredTemporary != null) temporaryStorage.AddRange(restoredTemporary.Where(item => item != null));
        if (restoredStash != null)
        {
            foreach (LootGridRestoreItem restored in restoredStash)
            {
                LootItemInstance item = restored?.Item;
                bool placed = item != null && (restored.HasPlacement
                    ? stashGrid.TryPlace(item, restored.X, restored.Y, restored.Rotated)
                    : stashGrid.TryAutoPlace(item, out _));
                if (placed) stash.Add(item);
                else if (item != null) temporaryStorage.Add(item);
            }
        }
        LastFailureReason = null;
        Changed?.Invoke();
    }
}
