using System;
using System.Collections.Generic;
using System.Linq;

// M9 가방 보관함 규칙. 상태 소유자는 기존 LootRuntimeState/LootItemInstance를 유지한다.
public static partial class LootRuntimeState
{
    public const int MaxBagNestingDepth = 4;

    public static IEnumerable<LootItemInstance> EnumerateStoredItemsRecursive()
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (LootItemInstance root in stash.Concat(runBag).Concat(temporaryStorage))
            foreach (LootItemInstance item in EnumerateRecursive(root, visited)) yield return item;
    }

    private static IEnumerable<LootItemInstance> EnumerateRecursive(LootItemInstance item, HashSet<string> visited)
    {
        if (item == null || !visited.Add(item.InstanceId)) yield break;
        yield return item;
        if (item.BagGrid == null) yield break;
        foreach (LootGridPlacement child in item.BagGrid.Placements)
            foreach (LootItemInstance nested in EnumerateRecursive(child.Item, visited)) yield return nested;
    }

    private static bool RemoveStoredItemRecursive(string instanceId)
    {
        LootItemInstance direct = stash.Find(item => item?.InstanceId == instanceId);
        if (direct != null)
        {
            stash.Remove(direct);
            stashGrid?.Remove(instanceId, out _);
            return true;
        }
        direct = runBag.Find(item => item?.InstanceId == instanceId);
        if (direct != null)
        {
            runBag.Remove(direct);
            runBagGrid.Remove(instanceId, out _);
            return true;
        }
        direct = temporaryStorage.Find(item => item?.InstanceId == instanceId);
        if (direct != null) return temporaryStorage.Remove(direct);
        foreach (LootItemInstance root in stash.Concat(runBag).Concat(temporaryStorage))
            if (RemoveFromBagRecursive(root, instanceId, new HashSet<string>(StringComparer.Ordinal))) return true;
        return false;
    }

    private static bool RemoveFromBagRecursive(LootItemInstance bag, string instanceId, HashSet<string> visited)
    {
        if (bag?.BagGrid == null || !visited.Add(bag.InstanceId)) return false;
        if (bag.BagGrid.Remove(instanceId, out _)) return true;
        foreach (LootGridPlacement child in bag.BagGrid.Placements)
            if (RemoveFromBagRecursive(child.Item, instanceId, visited)) return true;
        return false;
    }

    public static LootItemInstance FindStoredItemRecursive(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return null;
        foreach (LootItemInstance item in stash)
        {
            LootItemInstance found = FindRecursive(item, instanceId, new HashSet<string>(StringComparer.Ordinal));
            if (found != null) return found;
        }
        return null;
    }

    public static int GetStashBagDepth(string bagInstanceId)
    {
        foreach (LootItemInstance item in stash)
        {
            int depth = FindDepth(item, bagInstanceId, 1, new HashSet<string>(StringComparer.Ordinal));
            if (depth > 0) return depth;
        }
        return 0;
    }

    public static bool MoveStashItemIntoBag(string itemInstanceId, string bagInstanceId)
    {
        EnsureStashGrid();
        LootItemInstance item = stash.Find(value => value?.InstanceId == itemInstanceId);
        LootItemInstance bag = FindStoredItemRecursive(bagInstanceId);
        if (item == null) return Fail("Stash 직속 아이템만 가방에 넣을 수 있습니다.");
        if (!CanInsertIntoBag(item, bag, out string reason)) return Fail(reason);
        if (!bag.BagGrid.TryAutoPlace(item, out _)) return Fail("가방 공간이 부족합니다.");
        stashGrid.Remove(item.InstanceId, out _);
        stash.Remove(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveStashItemIntoBagAt(string itemInstanceId, string bagInstanceId, int x, int y, bool rotated)
    {
        EnsureStashGrid();
        LootItemInstance item = stash.Find(value => value?.InstanceId == itemInstanceId);
        LootItemInstance bag = FindStoredItemRecursive(bagInstanceId);
        if (item == null) return Fail("Stash 직속 아이템만 가방에 넣을 수 있습니다.");
        if (!CanInsertIntoBag(item, bag, out string reason)) return Fail(reason);
        if (!bag.BagGrid.TryPlace(item, x, y, rotated)) return Fail("가방의 해당 위치에 놓을 수 없습니다.");
        stashGrid.Remove(item.InstanceId, out _);
        stash.Remove(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveBagItemToParent(string itemInstanceId, string sourceBagInstanceId)
    {
        LootItemInstance sourceBag = FindStoredItemRecursive(sourceBagInstanceId);
        LootGridPlacement placement = sourceBag?.BagGrid?.Find(itemInstanceId);
        if (placement == null) return Fail("가방 안 아이템을 찾을 수 없습니다.");

        LootItemInstance parentBag = FindParentBag(sourceBagInstanceId);
        if (parentBag != null)
        {
            if (!parentBag.BagGrid.TryAutoPlace(placement.Item, out _)) return Fail("상위 가방 공간이 부족합니다.");
        }
        else
        {
            EnsureStashGrid();
            if (!stashGrid.TryAutoPlace(placement.Item, out _)) return Fail("Stash 공간이 부족합니다.");
            stash.Add(placement.Item);
        }
        sourceBag.BagGrid.Remove(itemInstanceId, out _);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveBagItemToStashAt(string itemInstanceId, string sourceBagInstanceId, int x, int y, bool rotated)
    {
        LootItemInstance sourceBag = FindStoredItemRecursive(sourceBagInstanceId);
        LootGridPlacement placement = sourceBag?.BagGrid?.Find(itemInstanceId);
        if (placement == null) return Fail("가방 안 아이템을 찾을 수 없습니다.");
        EnsureStashGrid();
        if (!stashGrid.TryPlace(placement.Item, x, y, rotated)) return Fail("Stash의 해당 위치에 놓을 수 없습니다.");
        sourceBag.BagGrid.Remove(itemInstanceId, out _);
        stash.Add(placement.Item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveBagItemToBagAt(string itemInstanceId, string sourceBagInstanceId,
        string targetBagInstanceId, int x, int y, bool rotated)
    {
        if (sourceBagInstanceId == targetBagInstanceId)
            return MoveNestedBagItem(sourceBagInstanceId, itemInstanceId, x, y, rotated);
        LootItemInstance sourceBag = FindStoredItemRecursive(sourceBagInstanceId);
        LootItemInstance targetBag = FindStoredItemRecursive(targetBagInstanceId);
        LootGridPlacement placement = sourceBag?.BagGrid?.Find(itemInstanceId);
        if (placement == null) return Fail("가방 안 아이템을 찾을 수 없습니다.");
        if (!CanInsertIntoBag(placement.Item, targetBag, out string reason)) return Fail(reason);
        if (!targetBag.BagGrid.TryPlace(placement.Item, x, y, rotated))
            return Fail("대상 가방의 해당 위치에 놓을 수 없습니다.");
        sourceBag.BagGrid.Remove(itemInstanceId, out _);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveBagItemToBag(string itemInstanceId, string sourceBagInstanceId,
        string targetBagInstanceId)
    {
        if (sourceBagInstanceId == targetBagInstanceId)
            return Fail("같은 가방으로 이동할 수 없습니다.");
        LootItemInstance sourceBag = FindStoredItemRecursive(sourceBagInstanceId);
        LootItemInstance targetBag = FindStoredItemRecursive(targetBagInstanceId);
        LootGridPlacement placement = sourceBag?.BagGrid?.Find(itemInstanceId);
        if (placement == null) return Fail("가방 안 아이템을 찾을 수 없습니다.");
        if (!CanInsertIntoBag(placement.Item, targetBag, out string reason)) return Fail(reason);
        if (!targetBag.BagGrid.TryAutoPlace(placement.Item, out _))
            return Fail("대상 가방 공간이 부족합니다.");
        sourceBag.BagGrid.Remove(itemInstanceId, out _);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    // 가방 아이콘에 드롭(타르코프식): 영지 Stash 직속이거나 Stash 쪽 가방 안 아이템을 대상 가방 빈 공간에 자동 배치한다.
    public static bool CanDropIntoBag(LootItemInstance item, LootItemInstance targetBag, out string reason)
    {
        if (!CanInsertIntoBag(item, targetBag, out reason)) return false;
        if (FindStoredItemRecursive(targetBag.InstanceId) == null) { reason = "영지 Stash의 가방에만 넣을 수 있습니다."; return false; }
        if (!HasFreeSpace(targetBag.BagGrid, item)) { reason = "가방 공간이 부족합니다."; return false; }
        if (stash.Find(value => value?.InstanceId == item.InstanceId) == null && FindParentBag(item.InstanceId) == null
            && runBag.Find(value => value?.InstanceId == item.InstanceId) == null && !IsInTemporaryStorage(item.InstanceId))
        { reason = "Stash·출격 가방·가방 안 아이템만 가방에 넣을 수 있습니다."; return false; }
        return true;
    }

    public static bool DropIntoBag(string itemInstanceId, string targetBagInstanceId)
    {
        LootItemInstance targetBag = FindStoredItemRecursive(targetBagInstanceId);
        if (stash.Find(value => value?.InstanceId == itemInstanceId) != null)
            return MoveStashItemIntoBag(itemInstanceId, targetBagInstanceId);
        if (IsInTemporaryStorage(itemInstanceId)) return MoveTemporaryItemIntoBag(itemInstanceId, targetBagInstanceId);
        LootItemInstance fromRunBag = runBag.Find(value => value?.InstanceId == itemInstanceId);
        if (fromRunBag != null)
        {
            if (!CanInsertIntoBag(fromRunBag, targetBag, out string runBagReason)) return Fail(runBagReason);
            if (!targetBag.BagGrid.TryAutoPlace(fromRunBag, out _)) return Fail("가방 공간이 부족합니다.");
            RemoveFromRunBag(fromRunBag);
            LastFailureReason = null;
            Changed?.Invoke();
            return true;
        }
        LootItemInstance parent = FindParentBag(itemInstanceId);
        if (parent == null) return Fail("Stash나 가방 안 아이템만 가방에 넣을 수 있습니다.");
        if (parent.InstanceId == targetBagInstanceId) return Fail("이미 그 가방 안에 있습니다.");
        return MoveBagItemToBag(itemInstanceId, parent.InstanceId, targetBagInstanceId);
    }

    // 영지 출격 가방 ↔ Stash·열린 가방 드래그 (지정 칸·회전). 실패하면 상태 불변.
    public static bool MoveStashItemToRunBagAt(string itemInstanceId, int x, int y, bool rotated)
    {
        EnsureStashGrid();
        LootItemInstance item = stash.Find(value => value?.InstanceId == itemInstanceId);
        if (item == null) return Fail("Stash 아이템을 찾을 수 없습니다.");
        if (!runBagGrid.TryPlace(item, x, y, rotated)) return Fail("출격 가방의 해당 위치에 놓을 수 없습니다.");
        stashGrid.Remove(item.InstanceId, out _);
        stash.Remove(item);
        runBag.Add(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveRunBagItemToStashAt(string itemInstanceId, int x, int y, bool rotated)
    {
        EnsureStashGrid();
        LootItemInstance item = runBag.Find(value => value?.InstanceId == itemInstanceId);
        if (item == null) return Fail("출격 가방 아이템을 찾을 수 없습니다.");
        if (!stashGrid.TryPlace(item, x, y, rotated)) return Fail("Stash의 해당 위치에 놓을 수 없습니다.");
        RemoveFromRunBag(item);
        stash.Add(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveBagItemToRunBagAt(string itemInstanceId, string sourceBagInstanceId, int x, int y, bool rotated)
    {
        LootItemInstance sourceBag = FindStoredItemRecursive(sourceBagInstanceId);
        LootGridPlacement placement = sourceBag?.BagGrid?.Find(itemInstanceId);
        if (placement == null) return Fail("가방 안 아이템을 찾을 수 없습니다.");
        if (!runBagGrid.TryPlace(placement.Item, x, y, rotated)) return Fail("출격 가방의 해당 위치에 놓을 수 없습니다.");
        sourceBag.BagGrid.Remove(itemInstanceId, out _);
        runBag.Add(placement.Item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveRunBagItemIntoBagAt(string itemInstanceId, string targetBagInstanceId, int x, int y, bool rotated)
    {
        LootItemInstance item = runBag.Find(value => value?.InstanceId == itemInstanceId);
        LootItemInstance targetBag = FindStoredItemRecursive(targetBagInstanceId);
        if (item == null) return Fail("출격 가방 아이템을 찾을 수 없습니다.");
        if (!CanInsertIntoBag(item, targetBag, out string reason)) return Fail(reason);
        if (!targetBag.BagGrid.TryPlace(item, x, y, rotated)) return Fail("가방의 해당 위치에 놓을 수 없습니다.");
        RemoveFromRunBag(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    private static void RemoveFromRunBag(LootItemInstance item)
    {
        runBagGrid.Remove(item.InstanceId, out _);
        runBag.Remove(item);
    }

    private static bool HasFreeSpace(LootGridModel grid, LootItemInstance item)
    {
        if (grid == null || item?.Definition == null) return false;
        for (int y = 0; y < grid.Height; y++)
        for (int x = 0; x < grid.Width; x++)
            if (grid.CanPlace(item, x, y, false) || grid.CanPlace(item, x, y, true)) return true;
        return false;
    }

    public static bool MoveNestedBagItem(string bagInstanceId, string itemInstanceId, int x, int y, bool rotated)
    {
        LootItemInstance bag = FindStoredItemRecursive(bagInstanceId);
        if (bag?.BagGrid == null || !bag.BagGrid.Move(itemInstanceId, x, y, rotated))
            return Fail("해당 위치에 놓을 수 없습니다.");
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool CanInsertIntoBag(LootItemInstance item, LootItemInstance targetBag, out string reason)
    {
        reason = null;
        if (item?.Definition == null || targetBag?.Definition?.Type != LootItemType.Bag)
        {
            reason = "대상 가방을 찾을 수 없습니다.";
            return false;
        }
        if (item.InstanceId == targetBag.InstanceId || ContainsRecursive(item, targetBag.InstanceId, new HashSet<string>(StringComparer.Ordinal)))
        {
            reason = "가방을 자기 자신이나 하위 가방 안에 넣을 수 없습니다.";
            return false;
        }
        int targetDepth = GetStashBagDepth(targetBag.InstanceId);
        if (targetDepth <= 0)
        {
            reason = "Stash에 연결된 가방만 사용할 수 있습니다.";
            return false;
        }
        if (item.Definition.Type == LootItemType.Bag && targetDepth >= MaxBagNestingDepth)
        {
            reason = "가방은 최대 4단계까지만 중첩할 수 있습니다.";
            return false;
        }
        int subtreeDepth = GetSubtreeBagDepth(item, new HashSet<string>(StringComparer.Ordinal));
        if (item.Definition.Type == LootItemType.Bag && targetDepth + subtreeDepth > MaxBagNestingDepth)
        {
            reason = "이 가방을 넣으면 최대 중첩 4단계를 초과합니다.";
            return false;
        }
        return true;
    }

    private static LootItemInstance FindRecursive(LootItemInstance item, string instanceId, HashSet<string> visited)
    {
        if (item == null || !visited.Add(item.InstanceId)) return null;
        if (item.InstanceId == instanceId) return item;
        if (item.BagGrid == null) return null;
        foreach (LootGridPlacement child in item.BagGrid.Placements)
        {
            LootItemInstance found = FindRecursive(child.Item, instanceId, visited);
            if (found != null) return found;
        }
        return null;
    }

    private static int FindDepth(LootItemInstance item, string instanceId, int depth, HashSet<string> visited)
    {
        if (item == null || !visited.Add(item.InstanceId)) return 0;
        if (item.InstanceId == instanceId) return depth;
        if (item.BagGrid == null) return 0;
        foreach (LootGridPlacement child in item.BagGrid.Placements)
        {
            int found = FindDepth(child.Item, instanceId, depth + 1, visited);
            if (found > 0) return found;
        }
        return 0;
    }

    private static bool ContainsRecursive(LootItemInstance root, string instanceId, HashSet<string> visited)
        => FindRecursive(root, instanceId, visited) != null;

    private static int GetSubtreeBagDepth(LootItemInstance item, HashSet<string> visited)
    {
        if (item?.Definition?.Type != LootItemType.Bag || !visited.Add(item.InstanceId)) return 0;
        int childDepth = 0;
        foreach (LootGridPlacement child in item.BagGrid.Placements)
            childDepth = Math.Max(childDepth, GetSubtreeBagDepth(child.Item, visited));
        return 1 + childDepth;
    }

    private static LootItemInstance FindParentBag(string childBagInstanceId)
    {
        foreach (LootItemInstance root in stash)
        {
            LootItemInstance found = FindParentRecursive(root, childBagInstanceId, new HashSet<string>(StringComparer.Ordinal));
            if (found != null) return found;
        }
        return null;
    }

    private static LootItemInstance FindParentRecursive(LootItemInstance parent, string childId, HashSet<string> visited)
    {
        if (parent?.BagGrid == null || !visited.Add(parent.InstanceId)) return null;
        if (parent.BagGrid.Find(childId) != null) return parent;
        foreach (LootGridPlacement child in parent.BagGrid.Placements)
        {
            LootItemInstance found = FindParentRecursive(child.Item, childId, visited);
            if (found != null) return found;
        }
        return null;
    }
}
