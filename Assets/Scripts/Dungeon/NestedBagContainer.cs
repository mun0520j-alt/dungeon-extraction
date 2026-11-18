// 던전에서 출격 가방 안의 하위 가방을 루팅 창처럼 연다 (M9 결정 41).
// 꺼내기·넣기 Tick은 상자 루팅과 같다(LootingPanel). 넣을 때 하위 가방 아이템 합계 상한과 중첩 규칙을 검사한다.
public sealed class NestedBagContainer : ILootContainer
{
    private readonly LootItemInstance bag;

    public NestedBagContainer(LootItemInstance bag) { this.bag = bag; }

    public LootItemInstance Bag => bag;
    public string DisplayName => $"{bag?.Definition?.Name} — 하위 가방 ({LootRuntimeState.RunNestedItemCount}/{M9StashConfig.RunNestedItemLimit})";
    public LootGridModel Grid => bag?.BagGrid;

    public bool TryAdd(LootItemInstance item)
    {
        if (!LootRuntimeState.CanAddToRunNestedBag(bag, item, out string reason))
        {
            LastFailureReason = reason;
            return false;
        }
        bool placed = Grid.TryAutoPlace(item, out _);
        LastFailureReason = placed ? null : "하위 가방 공간이 부족합니다.";
        return placed;
    }

    public bool TryRemove(string instanceId, out LootItemInstance item)
    {
        item = null;
        return Grid != null && Grid.Remove(instanceId, out item);
    }

    public void HandleContentsChanged() => LootRuntimeState.NotifyChanged();

    public static string LastFailureReason { get; private set; }
}
