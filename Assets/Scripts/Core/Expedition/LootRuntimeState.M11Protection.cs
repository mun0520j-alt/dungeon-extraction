using System;
using System.Collections.Generic;
using UnityEngine;

// M11 보호 주머니는 장착 가방과 별도인 영구 보관 영역이다.
public static partial class LootRuntimeState
{
    private static LootGridModel protectedPocket = new LootGridModel(2, 2);
    private static int protectedPocketTier = 1;
    private static bool startingPotionsGrantedThisRun;

    public static LootGridModel ProtectedPocket => protectedPocket;
    public static int ProtectedPocketTier => protectedPocketTier;

    private static void ResetM11Protection()
    {
        protectedPocketTier = 1;
        protectedPocket = new LootGridModel(M11PouchConfig.Width(1), M11PouchConfig.Height(1));
        startingPotionsGrantedThisRun = false;
    }

    public static bool IsProtectedPocketAllowed(LootItemInstance item)
    {
        LootItemData data = item?.Definition;
        if (data == null || data.Rarity >= LootRarity.Unique || data.Id == "escape_scroll") return false;
        if (data.Id == "pouch_potion" || data.Id == "case_junk") return true;
        return data.Type == LootItemType.Consumable || data.Type == LootItemType.Junk;
    }

    public static bool TryMoveRunItemToProtectedPocket(string instanceId)
    {
        LootItemInstance item = runBag.Find(value => value?.InstanceId == instanceId);
        if (item == null) return Fail("출격 가방의 아이템만 보호 주머니로 옮길 수 있습니다.");
        if (!IsProtectedPocketAllowed(item)) return Fail("보호 주머니에 넣을 수 없는 아이템입니다.");
        if (!protectedPocket.TryAutoPlace(item, out _)) return Fail("보호 주머니 공간이 부족합니다.");
        RemoveFromRunBag(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool TryMoveProtectedPocketItemToRunBag(LootItemInstance item)
    {
        if (item == null || protectedPocket.Find(item.InstanceId) == null) return Fail("보호 주머니 아이템을 찾을 수 없습니다.");
        if (!runBagGrid.TryAutoPlace(item, out _)) return Fail("출격 가방 공간이 부족합니다.");
        protectedPocket.Remove(item.InstanceId, out _);
        runBag.Add(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveProtectedPocketItem(string instanceId, int x, int y, bool rotated)
        => protectedPocket.Move(instanceId, x, y, rotated);

    public static IEnumerable<LootGridRestoreItem> PersistentProtectedPocketItems()
    {
        foreach (LootGridPlacement placement in protectedPocket.Placements)
            yield return new LootGridRestoreItem(placement.Item, placement.X, placement.Y, placement.Rotated, true);
    }

    public static void RestoreM11Protection(IEnumerable<LootGridRestoreItem> items, int tier)
    {
        protectedPocketTier = Math.Max(1, Math.Min(3, tier));
        protectedPocket = new LootGridModel(M11PouchConfig.Width(protectedPocketTier), M11PouchConfig.Height(protectedPocketTier));
        if (items == null) return;
        foreach (LootGridRestoreItem restored in items)
        {
            LootItemInstance item = restored?.Item;
            if (item == null || !IsProtectedPocketAllowed(item)) continue;
            if (!(restored.HasPlacement && protectedPocket.TryPlace(item, restored.X, restored.Y, restored.Rotated))
                && !protectedPocket.TryAutoPlace(item, out _)) StoreIncomingItem(item);
        }
    }

    public static void RefreshProtectedPocketUnlock()
    {
        int tier = QuestRuntimeState.HasCompleted("q_pouch_9") ? 3
            : QuestRuntimeState.HasCompleted("q_pouch_6") ? 2 : 1;
        if (tier <= protectedPocketTier) return;
        var expanded = new LootGridModel(M11PouchConfig.Width(tier), M11PouchConfig.Height(tier));
        foreach (LootGridPlacement placement in protectedPocket.Placements)
            expanded.TryPlace(placement.Item, placement.X, placement.Y, placement.Rotated);
        protectedPocketTier = tier;
        protectedPocket = expanded;
        Changed?.Invoke();
    }

    private static void GrantStartingPotions()
    {
        if (startingPotionsGrantedThisRun) return;
        startingPotionsGrantedThisRun = true;
        LootItemData potion = LootItemDatabase.FindById("potion_small");
        if (potion == null) return;
        for (int i = 0; i < M11PouchConfig.StartingSmallPotions; i++)
        {
            var item = new LootItemInstance(potion) { IsExpeditionBound = true };
            if (!TryAddToRunBag(item)) Debug.LogWarning("[M11] 출격 소형 포션을 넣을 공간이 없습니다.");
        }
    }

    private static void EndM11Run()
    {
        startingPotionsGrantedThisRun = false;
        PurgeExpeditionScrolls(protectedPocket);
    }
}
