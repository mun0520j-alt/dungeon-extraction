using System;
using System.Collections.Generic;

// M9 결정 41: 던전에서 출격 가방 안의 하위 가방을 열 수 있다.
// 대신 하위 가방들에 든 아이템 합계는 M9StashConfig.RunNestedItemLimit(기본 10)까지만 허용한다.
// 중첩 깊이(4단계)·자기 포함 금지 규칙은 영지와 같다. 출격 가방에 직접 놓인 아이템은 상한에 포함하지 않는다.
public static partial class LootRuntimeState
{
    public static int RunNestedItemCount => CountNestedItems(runBag);

    // roots 중 가방들의 하위 아이템(재귀) 수. roots 자신은 세지 않는다.
    public static int CountNestedItems(IEnumerable<LootItemInstance> roots)
    {
        int count = 0;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (LootItemInstance root in roots) count += CountDescendants(root, visited);
        return count;
    }

    private static int CountDescendants(LootItemInstance item, HashSet<string> visited)
    {
        if (item?.BagGrid == null || !visited.Add(item.InstanceId)) return 0;
        int count = 0;
        foreach (LootGridPlacement child in item.BagGrid.Placements)
            count += 1 + CountDescendants(child.Item, visited);
        return count;
    }

    // item을 출격 가방 직속으로 옮긴 뒤의 하위 가방 아이템 수. 이미 하위 가방 안에 있던 것이면 자기 자신 1개가 빠진다.
    private static int RunNestedCountAfterTakingToRunBag(LootItemInstance item)
    {
        int descendants = CountDescendants(item, new HashSet<string>(StringComparer.Ordinal));
        if (GetRunBagDepth(item.InstanceId) > 1) return RunNestedItemCount - 1;
        return RunNestedItemCount + descendants;
    }

    public static string RunNestedLimitReason => $"하위 가방 아이템은 원정 중 최대 {M9StashConfig.RunNestedItemLimit}개입니다.";

    public static bool BagHasFreeSpace(LootItemInstance bag, LootItemInstance item) => bag?.BagGrid != null && HasFreeSpace(bag.BagGrid, item);

    // 출격 가방 트리 안에서 item을 담고 있는 가방. 출격 가방 직속이면 null.
    public static LootItemInstance FindRunParentBag(string itemInstanceId)
    {
        foreach (LootItemInstance root in runBag)
        {
            LootItemInstance found = FindParentIn(root, itemInstanceId, new HashSet<string>(StringComparer.Ordinal));
            if (found != null) return found;
        }
        return null;
    }

    private static LootItemInstance FindParentIn(LootItemInstance bag, string childId, HashSet<string> visited)
    {
        if (bag?.BagGrid == null || !visited.Add(bag.InstanceId)) return null;
        foreach (LootGridPlacement child in bag.BagGrid.Placements)
        {
            if (child.Item?.InstanceId == childId) return bag;
            LootItemInstance found = FindParentIn(child.Item, childId, visited);
            if (found != null) return found;
        }
        return null;
    }

    // 결정 43: 던전에서 출격 가방(또는 하위 가방) 안 아이템을 가방 아이콘 위에 놓으면 그 가방 빈칸에 자동 배치.
    public static bool CanMoveRunItemIntoBag(LootItemInstance item, LootItemInstance bag, out string reason)
    {
        reason = null;
        if (item == null || bag == null || GetRunBagDepth(item.InstanceId) <= 0) { reason = "출격 가방 안의 아이템만 옮길 수 있습니다."; return false; }
        if (FindRunParentBag(item.InstanceId)?.InstanceId == bag.InstanceId) { reason = "이미 그 가방 안에 있습니다."; return false; }
        if (!CanAddToRunNestedBag(bag, item, out reason)) return false;
        if (!HasFreeSpace(bag.BagGrid, item)) { reason = $"{bag.Definition?.Name}에 빈 공간이 없습니다."; return false; }
        return true;
    }

    public static bool MoveRunItemIntoBag(LootItemInstance item, LootItemInstance bag)
    {
        if (!CanMoveRunItemIntoBag(item, bag, out string reason)) return Fail(reason);
        LootItemInstance parent = FindRunParentBag(item.InstanceId);
        bool topLevel = parent == null;
        if (!topLevel && !parent.BagGrid.Remove(item.InstanceId, out _)) return Fail("아이템을 꺼낼 수 없습니다.");
        if (topLevel) runBagGrid.Remove(item.InstanceId, out _);
        if (!bag.BagGrid.TryAutoPlace(item, out _))
        {
            if (topLevel) runBagGrid.TryAutoPlace(item, out _);
            else parent.BagGrid.TryAutoPlace(item, out _);
            return Fail($"{bag.Definition?.Name}에 빈 공간이 없습니다.");
        }
        if (topLevel) runBag.Remove(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    // 출격 가방 트리 안에서 bag의 깊이 (출격 가방 직속 가방 = 1). 없으면 0.
    public static int GetRunBagDepth(string bagInstanceId)
    {
        foreach (LootItemInstance root in runBag)
        {
            int depth = FindDepth(root, bagInstanceId, 1, new HashSet<string>(StringComparer.Ordinal));
            if (depth > 0) return depth;
        }
        return 0;
    }

    // 던전에서 하위 가방에 item을 넣을 수 있는지 (자기 포함·깊이·10개 상한).
    public static bool CanAddToRunNestedBag(LootItemInstance bag, LootItemInstance item, out string reason)
    {
        reason = null;
        if (bag?.BagGrid == null || item?.Definition == null) { reason = "가방을 찾을 수 없습니다."; return false; }
        if (item.InstanceId == bag.InstanceId || ContainsRecursive(item, bag.InstanceId, new HashSet<string>(StringComparer.Ordinal)))
        { reason = "가방을 자기 자신이나 하위 가방 안에 넣을 수 없습니다."; return false; }
        int depth = GetRunBagDepth(bag.InstanceId);
        if (depth <= 0) { reason = "출격 가방 안의 가방만 열 수 있습니다."; return false; }
        if (item.Definition.Type == LootItemType.Bag
            && depth + GetSubtreeBagDepth(item, new HashSet<string>(StringComparer.Ordinal)) > MaxBagNestingDepth)
        { reason = "가방은 최대 4단계까지만 중첩할 수 있습니다."; return false; }
        int adding = 1 + CountDescendants(item, new HashSet<string>(StringComparer.Ordinal));
        // 이미 하위 가방 안에 있던 아이템을 다른 하위 가방으로 옮기면 합계는 그대로다.
        if (GetRunBagDepth(item.InstanceId) > 1) adding = 0;
        // 출격 가방 직속 아이템을 하위 가방으로 옮기는 경우, 그 아이템의 하위 내용물은 이미 세어져 있다.
        bool alreadyCountedChildren = runBag.Exists(value => value?.InstanceId == item.InstanceId);
        if (alreadyCountedChildren) adding = 1;
        if (RunNestedItemCount + adding > M9StashConfig.RunNestedItemLimit) { reason = RunNestedLimitReason; return false; }
        return true;
    }

    // 하위 가방 안 아이템은 원정 중 "가지고 있는" 것이다. 하위 가방에 넣은 보험 장비가 버림으로 판정되지 않게 한다.
    public static void MarkKeptInRun(string instanceId) => droppedInsuredThisRun.Remove(instanceId);

    public static void NotifyChanged() => Changed?.Invoke();

    private static IEnumerable<string> EnumerateCarriedIdsRecursive()
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (LootItemInstance root in runBag)
            foreach (LootItemInstance item in EnumerateRecursive(root, visited)) yield return item.InstanceId;
    }
}
