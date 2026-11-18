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

    private const string DefaultNotice = "더블클릭: 빈 부위면 장착(2 Tick), 아니면 가방으로(1 Tick) / Ctrl+클릭: 바로 옮기기 / 가방: 더블클릭·우클릭으로 열기";

    // 결정 42: 루팅 중 하위 가방을 열면, 닫을 때(Esc) 원래 상자·바닥 더미로 돌아간다.
    private ILootContainer returnContainer;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void Open(ILootContainer source, PlayerTurnActor actor)
    {
        if (instance == null || source == null || actor == null) return;
        if (!(source is NestedBagContainer)) instance.returnContainer = null;
        else if (IsOpen && instance.container != null && !(instance.container is NestedBagContainer))
            instance.returnContainer = instance.container;
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
    }

    public static bool CloseLootOnly() => CloseLootOnly(returnToParent: true);

    public static bool CloseLootOnly(bool returnToParent)
    {
        if (!IsOpen) return false;
        ILootContainer back = instance.returnContainer;
        instance.returnContainer = null;
        bool backAlive = back != null && !(back is UnityEngine.Object unityObject && unityObject == null);
        if (returnToParent && backAlive && instance.container is NestedBagContainer && instance.player != null)
        {
            Open(back, instance.player);
            return true;
        }
        instance.panelRoot.SetActive(false);
        instance.container = null;
        instance.player = null;
        instance.selectedItem = null;
        InventoryPanel.ClearLootTransferHandler();
        InventoryPanel.CloseForEscape();
        return true;
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
        if (container is NestedBagContainer && item.Definition.Type == LootItemType.Bag)
        {
            OpenBagInContainer(item);
            return;
        }
        LootItemType type = item.Definition.Type;
        bool equippable = type == LootItemType.Weapon || type == LootItemType.Shield
            || type == LootItemType.Armor || type == LootItemType.Bag;
        if (equippable && LootRuntimeState.IsEquipSlotFree(type) && TryEquipFromContainer(item)) return;
        TakeItem(item);
    }

    // 하위 가방 창 안의 가방은 한 단계 더 연다. 상자·바닥의 가방은 아직 소지품이 아니라 열지 않는다.
    private void OpenBagInContainer(LootItemInstance item)
    {
        if (item?.Definition?.Type != LootItemType.Bag || player == null) return;
        if (!(container is NestedBagContainer))
        {
            SetNotice("상자·바닥의 가방은 출격 가방에 넣거나 장착한 뒤 열 수 있습니다.");
            return;
        }
        Open(new NestedBagContainer(item), player);
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

    private void PutItem(LootItemInstance item)
    {
        if (item == null || container == null || player == null) return;
        bool spent = player.TryPerformTimedAction(TickCost.Drop, "LootPut", () =>
        {
            if (!container.TryAdd(item))
            {
                SetNotice(container is NestedBagContainer ? NestedBagContainer.LastFailureReason ?? "하위 가방 공간 부족" : "보관함 공간 부족");
                return false;
            }
            LootItemInstance removed = LootRuntimeState.DropBagItem(item.InstanceId);
            if (removed != null && container is NestedBagContainer)
            {
                // 내 하위 가방에 넣은 것은 버린 것이 아니다 (보험 버림 판정 제외).
                LootRuntimeState.MarkKeptInRun(item.InstanceId);
                container.HandleContentsChanged();
                return true;
            }
            if (removed != null)
            {
                M3MeasurementAutomation.RecordItemDroppedToContainer();
                container.HandleContentsChanged();
                return true;
            }
            container.TryRemove(item.InstanceId, out _);
            return false;
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
        noticeRect.sizeDelta = new Vector2(0f, 28f);

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
