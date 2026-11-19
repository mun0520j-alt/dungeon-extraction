using UnityEngine;
using UnityEngine.UI;

// 왼쪽 월드 보관함과 오른쪽 InventoryPanel 사이의 1Tick 전송만 조정한다.
public sealed class LootingPanel : MonoBehaviour
{
    private static LootingPanel instance;
    private GameObject panelRoot;
    private Text title;
    private Text notice;
    private Button equipButton;
    private LootItemInstance selectedItem;
    private LootGridView gridView;
    private ILootContainer container;
    private PlayerTurnActor player;

    public static bool IsOpen => instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf;

    private void Awake()
    {
        instance = this;
        Build();
    }

    private const string DefaultNotice = "더블클릭: 빈 부위면 장착(2 Tick), 아니면 가방으로(1 Tick) / Ctrl+클릭: 바로 옮기기";

    public static LootGridView ActiveGridView => instance != null ? instance.gridView : null;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void Open(ILootContainer source, PlayerTurnActor actor)
    {
        if (instance == null || source == null || actor == null) return;
        instance.container = source;
        instance.player = actor;
        instance.title.text = source.DisplayName;
        instance.notice.text = DefaultNotice;
        instance.gridView.Bind(source.Grid, source.Grid.Move);
        instance.gridView.SetItemClickHandler(instance.SelectItem);
        instance.gridView.SetItemDoubleClickHandler(instance.ActivateItem);
        instance.gridView.SetItemOpenHandler(instance.OpenBagInContainer);
        instance.gridView.SetItemQuickMoveHandler(instance.TakeItem);
        instance.SelectItem(null);
        instance.panelRoot.SetActive(true);
        InventoryPanel.OpenForLoot(instance.PutItem);
        DungeonBagWindows.RebuildLinks();
    }

    public static bool CloseLootOnly()
    {
        if (!IsOpen) return false;
        instance.panelRoot.SetActive(false);
        instance.container = null;
        instance.player = null;
        instance.selectedItem = null;
        InventoryPanel.ClearLootTransferHandler();
        InventoryPanel.CloseForEscape();
        return true;
    }

    // ── 결정 43·44: 던전 드래그 (루팅 창 ↔ 출격 가방·가방 창, 가방 아이콘 자동 배치) ──

    public static bool ContainerHas(LootItemInstance item)
        => item != null && IsOpen && instance.container?.Grid?.Find(item.InstanceId) != null;

    public static bool CanTakeIntoRunBagIcon(LootItemInstance item, LootItemInstance bag)
    {
        if (!ContainerHas(item) || bag == null) return false;
        return LootRuntimeState.CanAddToRunNestedBag(bag, item, out _) && LootRuntimeState.BagHasFreeSpace(bag, item);
    }

    public static bool TakeIntoRunBagIcon(LootItemInstance item, LootItemInstance bag)
        => ContainerHas(item) && instance.TakeIntoBag(item, bag, null);

    public static bool TakeIntoBagAt(LootItemInstance item, LootItemInstance bag, int x, int y, bool rotated)
        => ContainerHas(item) && instance.TakeIntoBag(item, bag, new Vector3Int(x, y, rotated ? 1 : 0));

    public static bool TakeToRunBagAt(LootItemInstance item, int x, int y, bool rotated)
        => instance != null && instance.TakeItemAt(item, x, y, rotated);

    public static bool PutRunItemAt(LootItemInstance item, int x, int y, bool rotated)
        => instance != null && instance.PutItemAt(item, x, y, rotated);

    public static void PutRunItem(LootItemInstance item)
    {
        if (instance != null) instance.PutItem(item);
    }

    // position == null이면 빈칸 자동 배치, 아니면 (x, y, 회전 1/0) 칸에 놓기
    private bool TakeIntoBag(LootItemInstance item, LootItemInstance bag, Vector3Int? position)
    {
        if (player == null || container == null || bag?.BagGrid == null) return false;
        string failure = null;
        bool spent = player.TryPerformTimedAction(TickCost.PickUp, "LootTakeIntoBag", () =>
        {
            if (!LootRuntimeState.CanAddToRunNestedBag(bag, item, out failure)) return false;
            bool fits = position.HasValue
                ? bag.BagGrid.CanPlace(item, position.Value.x, position.Value.y, position.Value.z == 1)
                : LootRuntimeState.BagHasFreeSpace(bag, item);
            if (!fits) { failure = position.HasValue ? "그 자리에 놓을 수 없습니다." : $"{bag.Definition?.Name}에 빈 공간이 없습니다."; return false; }
            if (!container.TryRemove(item.InstanceId, out _)) return false;
            bool placed = position.HasValue
                ? bag.BagGrid.TryPlace(item, position.Value.x, position.Value.y, position.Value.z == 1)
                : bag.BagGrid.TryAutoPlace(item, out _);
            if (!placed)
            {
                container.Grid.TryAutoPlace(item, out _);
                failure = $"{bag.Definition?.Name}에 빈 공간이 없습니다.";
                return false;
            }
            LootRuntimeState.MarkKeptInRun(item.InstanceId);
            M3MeasurementAutomation.RecordItemAcquired();
            container.HandleContentsChanged();
            LootRuntimeState.NotifyChanged();
            return true;
        });
        if (spent) RefreshBoth();
        SetNotice(spent ? $"{bag.Definition?.Name}에 넣었습니다." : failure ?? "가방에 넣을 수 없습니다.");
        return spent;
    }

    private bool TakeItemAt(LootItemInstance item, int x, int y, bool rotated)
    {
        if (item == null || container == null || player == null) return false;
        bool spent = player.TryPerformTimedAction(TickCost.PickUp, "LootTakeAt", () =>
        {
            if (!container.TryRemove(item.InstanceId, out _)) return false;
            if (!LootRuntimeState.TryAddToRunBagAt(item, x, y, rotated))
            {
                container.Grid.TryAutoPlace(item, out _);
                return false;
            }
            M3MeasurementAutomation.RecordItemAcquired();
            container.HandleContentsChanged();
            return true;
        });
        if (spent) RefreshBoth();
        else SetNotice(LootRuntimeState.LastFailureReason ?? "그 자리에 놓을 수 없습니다.");
        return spent;
    }

    // 출격 가방 직속 또는 하위 가방(가방 창) 아이템을 루팅 창의 지정 칸에 놓는다 (버리기 Tick)
    private bool PutItemAt(LootItemInstance item, int x, int y, bool rotated)
    {
        if (item == null || container == null || player == null) return false;
        bool spent = player.TryPerformTimedAction(TickCost.Drop, "LootPutAt", () =>
        {
            if (!container.Grid.CanPlace(item, x, y, rotated)) return false;
            if (LootRuntimeState.DetachRunItemToWorld(item.InstanceId) == null) return false;
            container.Grid.TryPlace(item, x, y, rotated);
            M3MeasurementAutomation.RecordItemDroppedToContainer();
            container.HandleContentsChanged();
            return true;
        });
        if (spent) RefreshBoth();
        else SetNotice("그 자리에 놓을 수 없습니다.");
        return spent;
    }

    private void TakeItem(LootItemInstance item)
    {
        if (item == null || container == null || player == null) return;
        bool spent = player.TryPerformTimedAction(TickCost.PickUp, "LootTake", () =>
        {
            if (!LootRuntimeState.TryAddToRunBag(item))
            {
                SetNotice(LootRuntimeState.LastFailureReason != null && LootRuntimeState.LastFailureReason.Contains("하위 가방")
                    ? LootRuntimeState.LastFailureReason
                    : "출격 가방 공간이 부족합니다. 바꿔 끼우려면 선택 후 '장착'을 누르세요.");
                return false;
            }
            if (!container.TryRemove(item.InstanceId, out _))
            {
                LootRuntimeState.DropBagItem(item.InstanceId);
                return false;
            }
            M3MeasurementAutomation.RecordItemAcquired();
            container.HandleContentsChanged();
            return true;
        });
        if (spent) RefreshBoth();
    }

    // 더블클릭 규칙 (결정 39, 2026-11-18):
    // 1) 장비(무기·방패·갑옷·가방)이고 그 부위가 비어 있으면 장착 (2 Tick)
    // 2) 그 외에는 출격 가방에 넣기 (1 Tick)
    // 3) 출격 가방이 꽉 차면 경고만, 0 Tick
    // 부위가 차 있는데 바꿔 끼우려면 선택 후 '장착 (2 Tick)' 버튼을 쓴다.
    private void ActivateItem(LootItemInstance item)
    {
        if (item?.Definition == null) return;
        LootItemType type = item.Definition.Type;
        bool equippable = type == LootItemType.Weapon || type == LootItemType.Shield
            || type == LootItemType.Armor || type == LootItemType.Bag;
        if (equippable && LootRuntimeState.IsEquipSlotFree(type) && TryEquipFromContainer(item)) return;
        TakeItem(item);
    }

    // 상자·바닥의 가방은 아직 소지품이 아니라 열지 않는다 (결정 42).
    private void OpenBagInContainer(LootItemInstance item)
    {
        if (item?.Definition?.Type != LootItemType.Bag) return;
        SetNotice("상자·바닥의 가방은 출격 가방에 넣거나 장착한 뒤 열 수 있습니다.");
    }

    private bool TryEquipFromContainer(LootItemInstance item)
    {
        if (player == null || container == null) return false;
        bool spent = player.TryPerformTimedAction(TickCost.SwapWeaponInCombat, "Equip", () =>
            LootRuntimeState.TryEquipFromLootContainer(item, container.Grid));
        if (!spent) return false;
        M3MeasurementAutomation.RecordItemAcquired();
        container.HandleContentsChanged();
        selectedItem = null;
        RefreshBoth();
        return true;
    }

    private void SelectItem(LootItemInstance item)
    {
        selectedItem = item;
        if (equipButton != null)
            equipButton.gameObject.SetActive(IsEquippable(item));
    }

    private static bool IsEquippable(LootItemInstance item)
    {
        LootItemType? type = item?.Definition?.Type;
        return type == LootItemType.Weapon || type == LootItemType.Shield
            || type == LootItemType.Armor || type == LootItemType.Bag;
    }

    // '장착 (2 Tick)' 버튼: 부위가 차 있어도 바꿔 끼운다(기존 장비는 루팅 격자로).
    private void EquipSelectedBag()
    {
        if (!IsEquippable(selectedItem) || container == null || player == null) return;
        LootItemInstance target = selectedItem;
        bool spent = player.TryPerformTimedAction(TickCost.SwapWeaponInCombat, "Equip", () =>
        {
            bool success = LootRuntimeState.TryEquipFromLootContainer(target, container.Grid);
            if (!success) SetNotice(LootRuntimeState.LastFailureReason);
            return success;
        });
        if (!spent) return;
        M3MeasurementAutomation.RecordItemAcquired();
        container.HandleContentsChanged();
        selectedItem = null;
        RefreshBoth();
    }

    // 출격 가방(직속·가방 창) 아이템을 보관함 빈칸으로 (버리기 Tick)
    private void PutItem(LootItemInstance item)
    {
        if (item == null || container == null || player == null) return;
        bool spent = player.TryPerformTimedAction(TickCost.Drop, "LootPut", () =>
        {
            if (!container.TryAdd(item))
            {
                SetNotice("보관함 공간 부족");
                return false;
            }
            if (LootRuntimeState.DetachRunItemToWorld(item.InstanceId) == null)
            {
                container.TryRemove(item.InstanceId, out _);
                return false;
            }
            M3MeasurementAutomation.RecordItemDroppedToContainer();
            container.HandleContentsChanged();
            return true;
        });
        if (spent) RefreshBoth();
    }

    private void RefreshBoth()
    {
        SetNotice(DefaultNotice);
        if (container == null)
        {
            InventoryPanel.ClearLootTransferHandler();
            return;
        }
        gridView.Bind(container.Grid, container.Grid.Move);
        title.text = container.DisplayName;
        gridView.SetItemClickHandler(SelectItem);
        gridView.SetItemDoubleClickHandler(ActivateItem);
        SelectItem(null);
        InventoryPanel.OpenForLoot(PutItem);
        DungeonBagWindows.RebuildLinks();
    }

    private void SetNotice(string message)
    {
        if (notice != null) notice.text = message;
        if (!string.IsNullOrEmpty(message) && message != DefaultNotice)
            Debug.LogWarning($"[Looting] {message}");
    }

    private void Build()
    {
        Transform hud = ScreenSpaceCanvasProvider.GetOrCreateHudRoot();
        panelRoot = new GameObject("LootingPanel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(hud, false);
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.anchoredPosition = new Vector2(24f, 0f);
        panelRect.sizeDelta = new Vector2(5 * LootGridView.MinimumCellSize + 28f, 5 * LootGridView.MinimumCellSize + 92f);
        panelRoot.GetComponent<Image>().color = new Color(0.04f, 0.045f, 0.06f, 0.97f);

        title = CreateText("Title", panelRoot.transform, 18, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -8f);
        titleRect.sizeDelta = new Vector2(0f, 28f);

        var gridObject = new GameObject("LootGrid", typeof(RectTransform));
        gridObject.transform.SetParent(panelRoot.transform, false);
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0f, 1f);
        gridRect.pivot = new Vector2(0f, 1f);
        gridRect.anchoredPosition = new Vector2(14f, -42f);
        gridView = gridObject.AddComponent<LootGridView>();

        notice = CreateText("Notice", panelRoot.transform, 13, TextAnchor.MiddleCenter);
        RectTransform noticeRect = notice.rectTransform;
        noticeRect.anchorMin = new Vector2(0f, 0f);
        noticeRect.anchorMax = new Vector2(1f, 0f);
        noticeRect.pivot = new Vector2(0.5f, 0f);
        noticeRect.anchoredPosition = new Vector2(0f, 8f);
        noticeRect.sizeDelta = new Vector2(-12f, 34f);
        // 긴 안내·실패 사유가 잘리지 않게 두 줄까지 줄여 맞춘다 (3-1V9 텍스트 잘림)
        notice.horizontalOverflow = HorizontalWrapMode.Wrap;
        notice.verticalOverflow = VerticalWrapMode.Truncate;
        notice.resizeTextForBestFit = true;
        notice.resizeTextMinSize = 9;
        notice.resizeTextMaxSize = 13;

        var equipObject = new GameObject("EquipBag", typeof(RectTransform), typeof(Image), typeof(Button));
        equipObject.transform.SetParent(panelRoot.transform, false);
        RectTransform equipRect = equipObject.GetComponent<RectTransform>();
        equipRect.anchorMin = equipRect.anchorMax = new Vector2(1f, 0f);
        equipRect.pivot = new Vector2(1f, 0f);
        equipRect.anchoredPosition = new Vector2(-12f, 40f);
        equipRect.sizeDelta = new Vector2(96f, 34f);
        equipObject.GetComponent<Image>().color = new Color(0.12f, 0.38f, 0.52f, 1f);
        equipButton = equipObject.GetComponent<Button>();
        equipButton.onClick.AddListener(EquipSelectedBag);
        Text equipText = CreateText("Label", equipObject.transform, 15, TextAnchor.MiddleCenter);
        equipText.rectTransform.anchorMin = Vector2.zero;
        equipText.rectTransform.anchorMax = Vector2.one;
        equipText.rectTransform.sizeDelta = Vector2.zero;
        equipText.text = $"장착 ({TickCost.SwapWeaponInCombat:0} Tick)";
        equipObject.SetActive(false);
        panelRoot.SetActive(false);
    }

    private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = HudFont.GetDefault();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }
}
