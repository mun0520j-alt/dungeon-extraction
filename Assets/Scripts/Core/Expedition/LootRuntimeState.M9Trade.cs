using System;
using System.Collections.Generic;
using System.Linq;

// M9 상점 판매 바구니. 확정 전 아이템은 원래 저장 위치를 기억하며 Gold를 바꾸지 않는다.
public static partial class LootRuntimeState
{
    private enum SaleSource { Stash, Temporary }

    private sealed class SaleOrigin
    {
        public SaleSource Source;
        public int X;
        public int Y;
        public bool Rotated;
    }

    private static readonly List<LootItemInstance> saleBasket = new List<LootItemInstance>();
    private static readonly Dictionary<string, SaleOrigin> saleOrigins = new Dictionary<string, SaleOrigin>(StringComparer.Ordinal);
    private static LootGridModel saleBasketGrid;

    public static IReadOnlyList<LootItemInstance> SaleBasket => saleBasket;
    public static LootGridModel SaleBasketGrid
    {
        get
        {
            int width = M9StashConfig.SaleBasketWidth;
            int height = M9StashConfig.SaleBasketHeight;
            if (saleBasketGrid == null || saleBasketGrid.Width != width || saleBasketGrid.Height != height)
                RebuildSaleBasketGrid(width, height);
            return saleBasketGrid;
        }
    }
    public static int SaleBasketTotal => saleBasket.Sum(item => GetSellPrice(item?.Definition));
    public static bool SaleBasketHasInsuredItems => saleBasket.Any(item => IsInsured(item?.InstanceId));

    private static void ResetM9Trade()
    {
        saleBasket.Clear();
        saleOrigins.Clear();
        saleBasketGrid = null;
    }

    public static bool MoveStashItemToSaleBasket(string instanceId)
    {
        EnsureStashGrid();
        LootItemInstance item = stash.Find(value => value?.InstanceId == instanceId);
        LootGridPlacement original = stashGrid.Find(instanceId);
        if (item == null || original == null) return Fail("Stash 아이템을 찾을 수 없습니다.");
        if (!CanEnterSaleBasket(item)) return false;
        if (!SaleBasketGrid.TryAutoPlace(item, out _)) return Fail("판매 바구니 공간이 부족합니다.");
        saleOrigins[item.InstanceId] = new SaleOrigin { Source = SaleSource.Stash, X = original.X, Y = original.Y, Rotated = original.Rotated };
        stashGrid.Remove(item.InstanceId, out _);
        stash.Remove(item);
        saleBasket.Add(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveTemporaryItemToSaleBasket(string instanceId)
    {
        LootItemInstance item = temporaryStorage.Find(value => value?.InstanceId == instanceId);
        if (item == null) return Fail("임시 보관함 아이템을 찾을 수 없습니다.");
        if (!CanEnterSaleBasket(item)) return false;
        if (!SaleBasketGrid.TryAutoPlace(item, out _)) return Fail("판매 바구니 공간이 부족합니다.");
        saleOrigins[item.InstanceId] = new SaleOrigin { Source = SaleSource.Temporary };
        temporaryStorage.Remove(item);
        saleBasket.Add(item);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    private static bool CanEnterSaleBasket(LootItemInstance item)
    {
        if (item?.Definition == null || item.Definition.Id.StartsWith("basic_", StringComparison.Ordinal))
            return Fail("판매할 수 없는 아이템입니다.");
        if (item.Definition.Type == LootItemType.Bag && item.BagGrid.Placements.Count > 0)
            return Fail("가방을 비워주세요");
        return true;
    }

    public static bool ReturnSaleBasketItem(string instanceId)
    {
        LootItemInstance item = saleBasket.Find(value => value?.InstanceId == instanceId);
        if (item == null || !saleOrigins.TryGetValue(instanceId, out SaleOrigin origin))
            return Fail("판매 바구니 아이템을 찾을 수 없습니다.");
        if (origin.Source == SaleSource.Temporary)
        {
            temporaryStorage.Add(item);
        }
        else
        {
            EnsureStashGrid();
            bool placed = stashGrid.TryPlace(item, origin.X, origin.Y, origin.Rotated)
                || stashGrid.TryAutoPlace(item, out _);
            if (!placed) temporaryStorage.Add(item);
            else stash.Add(item);
        }
        saleBasketGrid?.Remove(instanceId, out _);
        saleBasket.Remove(item);
        saleOrigins.Remove(instanceId);
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static bool MoveSaleBasketItem(string instanceId, int x, int y, bool rotated)
    {
        if (!SaleBasketGrid.Move(instanceId, x, y, rotated)) return Fail("해당 위치에 놓을 수 없습니다.");
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static void CancelSaleBasket()
    {
        foreach (string instanceId in saleBasket.Select(item => item.InstanceId).ToArray())
            ReturnSaleBasketItem(instanceId);
    }

    public static bool ConfirmSaleBasket()
    {
        if (saleBasket.Count == 0) return Fail("판매 바구니가 비어 있습니다.");
        foreach (LootItemInstance item in saleBasket)
            if (!CanEnterSaleBasket(item)) return false;
        int total = SaleBasketTotal;
        foreach (LootItemInstance item in saleBasket.ToArray()) RemoveInsuranceRecursive(item);
        saleBasket.Clear();
        saleOrigins.Clear();
        saleBasketGrid = null;
        Gold += total;
        LastFailureReason = null;
        Changed?.Invoke();
        return true;
    }

    public static IEnumerable<LootGridRestoreItem> PersistentStashRoots()
    {
        EnsureStashGrid();
        foreach (LootItemInstance item in stash)
        {
            LootGridPlacement placement = stashGrid.Find(item.InstanceId);
            if (placement != null) yield return new LootGridRestoreItem(item, placement.X, placement.Y, placement.Rotated, true);
        }
        foreach (LootItemInstance item in saleBasket)
        {
            if (!saleOrigins.TryGetValue(item.InstanceId, out SaleOrigin origin) || origin.Source != SaleSource.Stash) continue;
            yield return new LootGridRestoreItem(item, origin.X, origin.Y, origin.Rotated, true);
        }
    }

    public static IEnumerable<LootItemInstance> PersistentTemporaryItems()
    {
        foreach (LootItemInstance item in temporaryStorage) yield return item;
        foreach (LootItemInstance item in saleBasket)
            if (saleOrigins.TryGetValue(item.InstanceId, out SaleOrigin origin) && origin.Source == SaleSource.Temporary) yield return item;
    }

    private static void RebuildSaleBasketGrid(int width, int height)
    {
        var rebuilt = new LootGridModel(width, height);
        foreach (LootItemInstance item in saleBasket)
            if (!rebuilt.TryAutoPlace(item, out _)) throw new InvalidOperationException("판매 바구니 격자 복원에 실패했습니다.");
        saleBasketGrid = rebuilt;
    }
}
