using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 영지에서도 던전과 동일한 LootGridView를 사용한다. 데이터 변경은 LootRuntimeState에 위임한다.
public sealed class BaseCampGridPanel : MonoBehaviour
{
    private static BaseCampGridPanel instance;
    private static int MaxOpenBagWindows => M9StashConfig.BagWindowMax;
    private const float GridScrollSensitivity = 36f;
    private sealed class BagWindowState
    {
        public LootItemInstance Bag;
        public RectTransform Root;
        public LootGridView Grid;
        public Text Title;
    }
    private LootGridView gridView;
    private LootGridView stashGridView;
    private LootGridView saleGridView;
    private Text title;
    private Text stashTitle;
    private Text secondaryGridTitle;
    private Text notice;
    private LootItemInstance selected;
    private string selectedSourceBagId;
    private LootItemInstance openedBag;
    private Button toSaleButton;
    private Text typeFilterLabel;
    private Text rarityFilterLabel;
    private Text raidFilterLabel;
    // 결정 45: 임시 보관함 = Stash 아래 격자. 아이템 크기에 따라 행이 늘고, 비면 숨긴다.
    private RectTransform temporaryPanel;
    private Text temporaryTitle;
    private LootGridView temporaryGridView;
    private string selectedTemporaryId;
    private RectTransform stashViewportRect;
    private RectTransform saleViewportRect;
    private bool confirmTemporaryDelete;
    private bool selectedFromSaleBasket;
    private bool selectedFromStash;
    private RectTransform loadoutRoot;
    private RectTransform stashRoot;
    private Transform hudRoot;
    private readonly System.Collections.Generic.List<BagWindowState> bagWindows
        = new System.Collections.Generic.List<BagWindowState>();
    private int filterVersion = -1;

    // 던전 인벤토리와 같은 장착 슬롯 표시 (무기·방패·갑옷·가방). 보험 장비는 청록 표시.
    private static readonly (LootItemType type, string label)[] SlotOrder =
    {
        (LootItemType.Weapon, "무기"), (LootItemType.Shield, "방패"),
        (LootItemType.Armor, "갑옷"), (LootItemType.Bag, "가방"),
    };
    private static readonly Color SlotBackground = new Color(0.27f, 0.15f, 0.08f, 0.95f);
    private static readonly Color EmptyIconColor = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color EmptyLabelColor = new Color(1f, 1f, 1f, 0.45f);
    private readonly System.Collections.Generic.Dictionary<LootItemType, (Image icon, Outline frame, Text label, Button unequip)> slots =
        new System.Collections.Generic.Dictionary<LootItemType, (Image, Outline, Text, Button)>();

    private void Start()
    {
        instance = this;
        Build();
        LootRuntimeState.Changed += Refresh;
        QuestRuntimeState.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        LootRuntimeState.Changed -= Refresh;
        QuestRuntimeState.Changed -= Refresh;
        if (instance == this) instance = null;
    }

    public static bool CloseNewestBagWindowForEscape()
    {
        if (instance == null || instance.bagWindows.Count == 0) return false;
        instance.CloseBagWindow(instance.bagWindows[instance.bagWindows.Count - 1]);
        return true;
    }

    private void Update()
    {
        ApplyLayout();
        if (stashGridView == null || filterVersion == BaseCampManagementPanel.StashFilterVersion) return;
        filterVersion = BaseCampManagementPanel.StashFilterVersion;
        stashGridView.SetDimPredicate(BaseCampManagementPanel.ShouldDimStashItem);
        RefreshFilterLabels();
    }

    private void Build()
    {
        Transform hud = ScreenSpaceCanvasProvider.GetOrCreateHudRoot();
        hudRoot = hud;
        var root = new GameObject("BaseCampRunBagGrid", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(hud, false);
        loadoutRoot = root.GetComponent<RectTransform>();
        root.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.97f);

        CreateText(root.transform, "EquipTitle", "장착 장비", 18, new Vector2(0f, 0.93f), new Vector2(1f, 0.98f));
        for (int i = 0; i < SlotOrder.Length; i++)
        {
            int col = i % 2, row = i / 2;
            var min = new Vector2(0.04f + col * 0.47f, 0.84f - row * 0.075f);
            CreateSlot(root.transform, SlotOrder[i].type, min, min + new Vector2(0.45f, 0.065f));
        }

        title = CreateText(root.transform, "Title", "출격 가방", 18, new Vector2(0f, 0.69f), new Vector2(1f, 0.74f));
        var gridObject = new GameObject("Grid", typeof(RectTransform));
        gridObject.transform.SetParent(root.transform, false);
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.68f);
        gridRect.pivot = new Vector2(0.5f, 1f);
        gridView = gridObject.AddComponent<LootGridView>();

        var stashPanel = new GameObject("BaseCampStashGrid", typeof(RectTransform), typeof(Image));
        stashPanel.transform.SetParent(hud, false);
        stashRoot = stashPanel.GetComponent<RectTransform>();
        stashPanel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.97f);

        stashTitle = CreateText(stashPanel.transform, "StashTitle", "Stash", 18,
            new Vector2(0f, 0.94f), new Vector2(1f, 0.99f));
        var viewport = new GameObject("StashViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(stashPanel.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.02f, 0.02f);
        viewportRect.anchorMax = new Vector2(0.65f, 0.70f);
        viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
        stashViewportRect = viewportRect;
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
        var stashObject = new GameObject("StashGrid", typeof(RectTransform));
        stashObject.transform.SetParent(viewport.transform, false);
        RectTransform stashRect = stashObject.GetComponent<RectTransform>();
        stashRect.anchorMin = stashRect.anchorMax = new Vector2(0f, 1f);
        stashRect.pivot = new Vector2(0f, 1f);
        stashRect.anchoredPosition = Vector2.zero;
        stashGridView = stashObject.AddComponent<LootGridView>();
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = stashRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = GridScrollSensitivity;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        secondaryGridTitle = CreateText(stashPanel.transform, "SaleBasketTitle", "판매 바구니", 16,
            new Vector2(0.67f, 0.70f), new Vector2(0.99f, 0.76f));
        var saleViewport = new GameObject("SaleBasketViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        saleViewport.transform.SetParent(stashPanel.transform, false);
        saleViewportRect = saleViewport.GetComponent<RectTransform>();
        saleViewportRect.anchorMin = new Vector2(0.67f, 0.02f);
        saleViewportRect.anchorMax = new Vector2(0.99f, 0.70f);
        saleViewportRect.offsetMin = saleViewportRect.offsetMax = Vector2.zero;
        saleViewport.GetComponent<Image>().color = new Color(0.12f, 0.04f, 0.03f, 0.35f);
        var saleObject = new GameObject("SaleBasketGrid", typeof(RectTransform));
        saleObject.transform.SetParent(saleViewport.transform, false);
        RectTransform saleRect = saleObject.GetComponent<RectTransform>();
        saleRect.anchorMin = saleRect.anchorMax = new Vector2(0f, 1f);
        saleRect.pivot = new Vector2(0f, 1f);
        saleRect.anchoredPosition = Vector2.zero;
        saleGridView = saleObject.AddComponent<LootGridView>();
        ScrollRect saleScroll = saleViewport.GetComponent<ScrollRect>();
        saleScroll.viewport = saleViewportRect;
        saleScroll.content = saleRect;
        saleScroll.horizontal = false;
        saleScroll.vertical = true;
        saleScroll.scrollSensitivity = GridScrollSensitivity;
        saleScroll.movementType = ScrollRect.MovementType.Clamped;

        notice = CreateText(root.transform, "Notice", "아이템을 선택하세요", 13,
            new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.30f));
        CreateButton(root.transform, "ToStash", "Stash로 이동", new Vector2(0.08f, 0.10f), MoveToStash);
        CreateButton(root.transform, "Equip", "장착", new Vector2(0.53f, 0.10f), Equip);
        CreateButton(stashPanel.transform, "SortRarity", "정렬: 등급 높은순", new Vector2(0.02f, 0.86f),
            () => LootRuntimeState.SortStashByRarity(), 0.23f);
        CreateButton(stashPanel.transform, "SortType", "정렬: 종류별", new Vector2(0.26f, 0.86f),
            () => LootRuntimeState.SortStashByType(), 0.23f);
        CreateButton(stashPanel.transform, "ToOpenedBag", "선택 → 열린 가방", new Vector2(0.50f, 0.86f), MoveSelectedToOpenedBag, 0.24f);
        toSaleButton = CreateButton(stashPanel.transform, "ToSale", "선택 → 판매", new Vector2(0.75f, 0.86f), MoveSelectedToSale, 0.23f);

        typeFilterLabel = CreateButton(stashPanel.transform, "FilterType", "부위: 전체", new Vector2(0.02f, 0.78f),
            BaseCampManagementPanel.CycleTypeFilterFromGrid, 0.22f).GetComponentInChildren<Text>();
        rarityFilterLabel = CreateButton(stashPanel.transform, "FilterRarity", "등급: 전체", new Vector2(0.26f, 0.78f),
            BaseCampManagementPanel.CycleRarityFilterFromGrid, 0.22f).GetComponentInChildren<Text>();
        raidFilterLabel = CreateButton(stashPanel.transform, "FilterRaid", "인레이드: 전체", new Vector2(0.50f, 0.78f),
            BaseCampManagementPanel.CycleRaidFilterFromGrid, 0.24f).GetComponentInChildren<Text>();
        CreateButton(stashPanel.transform, "FilterReset", "필터 초기화", new Vector2(0.76f, 0.78f),
            BaseCampManagementPanel.ResetFiltersFromGrid, 0.22f);
        BuildTemporaryPanel(stashPanel.transform);
        RefreshFilterLabels();
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        BaseCampLayoutRects layout = BaseCampLayout.Calculate();
        BaseCampLayout.ApplyTo(loadoutRoot, layout.Loadout);
        BaseCampLayout.ApplyTo(stashRoot, layout.Stash);
    }

    private void RefreshFilterLabels()
    {
        if (typeFilterLabel != null) typeFilterLabel.text = $"부위: {BaseCampManagementPanel.CurrentTypeFilterLabel}";
        if (rarityFilterLabel != null) rarityFilterLabel.text = $"등급: {BaseCampManagementPanel.CurrentRarityFilterLabel}";
        if (raidFilterLabel != null) raidFilterLabel.text = $"인레이드: {BaseCampManagementPanel.CurrentRaidFilterLabel}";
    }

    private void Refresh()
    {
        if (gridView == null) return;
        RefreshSlots();
        gridView.Bind(LootRuntimeState.RunBagGrid, LootRuntimeState.MoveRunBagItem);
        gridView.SetItemClickHandler(SelectRunBagItem);
        gridView.SetItemDoubleClickHandler(MoveToStash);
        title.text = $"출격 가방 {LootRuntimeState.RunBagGrid.Width}×{LootRuntimeState.RunBagGrid.Height}";
        LootGridModel stashGrid = LootRuntimeState.StashGrid;
        stashGridView.Bind(stashGrid, LootRuntimeState.MoveStashItem);
        stashGridView.SetDimPredicate(BaseCampManagementPanel.ShouldDimStashItem);
        stashGridView.SetItemClickHandler(SelectStashItem);
        stashGridView.SetItemDoubleClickHandler(HandleStoredItemDoubleClick);
        stashGridView.SetItemOpenHandler(OpenBagWindow); // 결정 42: 우클릭으로도 가방 열기
        saleGridView.Bind(LootRuntimeState.SaleBasketGrid, LootRuntimeState.MoveSaleBasketItem);
        saleGridView.SetItemClickHandler(SelectSaleBasketItem);
        saleGridView.SetItemDoubleClickHandler(item => LootRuntimeState.ReturnSaleBasketItem(item.InstanceId));
        secondaryGridTitle.text = "판매 바구니";

        stashGridView.ClearExternalDropTargets();
        saleGridView.ClearExternalDropTargets();
        stashGridView.SetBagDropHandler(CanDropIntoBagIcon, DropIntoBagIcon);
        // 출격 가방 ↔ Stash 드래그 (영지)
        gridView.ClearExternalDropTargets();
        stashGridView.AddExternalDropTarget(gridView,
            (item, x, y, rotated) => LootRuntimeState.MoveStashItemToRunBagAt(item.InstanceId, x, y, rotated));
        gridView.AddExternalDropTarget(stashGridView,
            (item, x, y, rotated) => LootRuntimeState.MoveRunBagItemToStashAt(item.InstanceId, x, y, rotated));
        if (BaseCampManagementPanel.IsSellMode)
        {
            stashGridView.AddExternalDropTarget(saleGridView,
                (item, x, y, rotated) => LootRuntimeState.MoveStashItemToSaleBasketAt(item.InstanceId, x, y, rotated));
            saleGridView.AddExternalDropTarget(stashGridView,
                (item, x, y, rotated) => LootRuntimeState.ReturnSaleBasketItemToStashAt(item.InstanceId, x, y, rotated));
        }

        for (int i = bagWindows.Count - 1; i >= 0; i--)
        {
            if (LootRuntimeState.FindStoredItemRecursive(bagWindows[i].Bag.InstanceId) != null) continue;
            Destroy(bagWindows[i].Root.gameObject);
            bagWindows.RemoveAt(i);
        }
        foreach (BagWindowState window in bagWindows)
        {
            LootItemInstance bag = window.Bag;
            window.Grid.Bind(bag.BagGrid,
                (id, x, y, rotated) => LootRuntimeState.MoveNestedBagItem(bag.InstanceId, id, x, y, rotated));
            window.Grid.SetItemClickHandler(item => SelectOpenedBagItem(window, item));
            window.Grid.SetItemDoubleClickHandler(item => HandleOpenedBagItemDoubleClick(window, item));
            window.Grid.SetItemOpenHandler(OpenBagWindow);
            window.Grid.ClearExternalDropTargets();
            window.Grid.SetBagDropHandler(CanDropIntoBagIcon, DropIntoBagIcon);
            window.Title.text = $"{bag.Definition.Name} {bag.BagGrid.Width}×{bag.BagGrid.Height}"
                + $" · 깊이 {LootRuntimeState.GetStashBagDepth(bag.InstanceId)}";
            stashGridView.AddExternalDropTarget(window.Grid,
                (item, x, y, rotated) => LootRuntimeState.MoveStashItemIntoBagAt(
                    item.InstanceId, bag.InstanceId, x, y, rotated));
            window.Grid.AddExternalDropTarget(stashGridView,
                (item, x, y, rotated) => LootRuntimeState.MoveBagItemToStashAt(
                    item.InstanceId, bag.InstanceId, x, y, rotated));
            // 열린 가방 ↔ 출격 가방
            window.Grid.AddExternalDropTarget(gridView,
                (item, x, y, rotated) => LootRuntimeState.MoveBagItemToRunBagAt(
                    item.InstanceId, bag.InstanceId, x, y, rotated));
            gridView.AddExternalDropTarget(window.Grid,
                (item, x, y, rotated) => LootRuntimeState.MoveRunBagItemIntoBagAt(
                    item.InstanceId, bag.InstanceId, x, y, rotated));
        }
        for (int i = 0; i < bagWindows.Count; i++)
        for (int j = 0; j < bagWindows.Count; j++)
        {
            if (i == j) continue;
            BagWindowState source = bagWindows[i];
            BagWindowState target = bagWindows[j];
            source.Grid.AddExternalDropTarget(target.Grid,
                (item, x, y, rotated) => LootRuntimeState.MoveBagItemToBagAt(
                    item.InstanceId, source.Bag.InstanceId, target.Bag.InstanceId, x, y, rotated));
        }
        if (openedBag != null && LootRuntimeState.FindStoredItemRecursive(openedBag.InstanceId) == null)
            openedBag = bagWindows.Count > 0 ? bagWindows[bagWindows.Count - 1].Bag : null;
        stashTitle.text = $"Stash {stashGrid.Width}×{stashGrid.Height}";
        if (toSaleButton != null) toSaleButton.gameObject.SetActive(BaseCampManagementPanel.IsSellMode);
        RefreshTemporaryStorage();
        LinkTemporaryDropTargets();
        if (selected != null && LootRuntimeState.RunBagGrid.Find(selected.InstanceId) == null) selected = null;
        if (selected == null) gridView.ClearSelection();
        notice.text = selected?.Definition != null ? selected.Definition.Name : "아이템을 선택하세요";
    }

    private static bool CanDropIntoBagIcon(LootItemInstance item, LootItemInstance bag)
        => LootRuntimeState.CanDropIntoBag(item, bag, out _);

    // 가방 아이콘에 드롭: 빈 공간에 자동 배치(회전 포함). 실패하면 원위치 + 이유.
    private bool DropIntoBagIcon(LootItemInstance item, LootItemInstance bag)
    {
        bool ok = LootRuntimeState.DropIntoBag(item.InstanceId, bag.InstanceId);
        notice.text = ok ? $"{bag.Definition.Name}에 넣었습니다." : LootRuntimeState.LastFailureReason;
        return ok;
    }

    private LootItemInstance SelectedTemporary()
    {
        if (string.IsNullOrEmpty(selectedTemporaryId)) return null;
        foreach (LootItemInstance item in LootRuntimeState.TemporaryStorage)
            if (item?.InstanceId == selectedTemporaryId) return item;
        return null;
    }

    private void BuildTemporaryPanel(Transform stashPanel)
    {
        var panel = new GameObject("TemporaryStorage", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(stashPanel, false);
        temporaryPanel = panel.GetComponent<RectTransform>();
        temporaryPanel.anchorMin = new Vector2(0.02f, 0.02f);
        temporaryPanel.anchorMax = new Vector2(0.98f, 0.02f);
        temporaryPanel.pivot = new Vector2(0.5f, 0f);
        temporaryPanel.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.22f, 0.11f, 0.03f, 0.9f);

        temporaryTitle = CreateText(panel.transform, "Title", "임시 보관함", 12, Vector2.zero, Vector2.one);
        RectTransform titleRect = temporaryTitle.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(8f, -24f);
        titleRect.offsetMax = new Vector2(-200f, 0f);
        temporaryTitle.alignment = TextAnchor.MiddleLeft;
        temporaryTitle.color = new Color(1f, 0.72f, 0.32f, 1f);
        PlaceHeaderButton(CreateButton(panel.transform, "TempDelete", "선택 삭제", Vector2.zero, DeleteTemporary), -100f);
        PlaceHeaderButton(CreateButton(panel.transform, "TempSale", "선택 판매", Vector2.zero, MoveTemporaryToSale), -4f);

        var viewport = new GameObject("TemporaryViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(panel.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(6f, 6f);
        viewportRect.offsetMax = new Vector2(-6f, -26f);
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        var gridObject = new GameObject("TemporaryGrid", typeof(RectTransform));
        gridObject.transform.SetParent(viewport.transform, false);
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0f, 1f);
        gridRect.pivot = new Vector2(0f, 1f);
        gridRect.anchoredPosition = Vector2.zero;
        temporaryGridView = gridObject.AddComponent<LootGridView>();
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = gridRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = GridScrollSensitivity;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        panel.SetActive(false);
    }

    private static void PlaceHeaderButton(Button button, float rightOffset)
    {
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(92f, 20f);
        rect.anchoredPosition = new Vector2(rightOffset, -2f);
    }

    // 임시 보관함 격자를 다시 만들고, Stash·판매 바구니 영역을 그 높이만큼 위로 올린다 (겹침 금지).
    private void RefreshTemporaryStorage()
    {
        if (temporaryPanel == null) return;
        int count = LootRuntimeState.TemporaryStorage.Count;
        if (SelectedTemporary() == null) selectedTemporaryId = null;
        float reserved = 0f;
        temporaryPanel.gameObject.SetActive(count > 0);
        if (count > 0)
        {
            LootGridModel grid = LootRuntimeState.BuildTemporaryStorageGrid(LootRuntimeState.StashGrid.Width);
            temporaryGridView.Bind(grid, (id, x, y, rotated) => false);
            temporaryGridView.SetItemClickHandler(SelectTemporaryItem);
            temporaryGridView.SetItemDoubleClickHandler(item => { selectedTemporaryId = item?.InstanceId; MoveTemporaryToStash(); });
            int rows = Mathf.Min(grid.Height, M9StashConfig.TemporaryVisibleRows);
            reserved = rows * LootGridView.MinimumCellSize + 34f;
            temporaryPanel.sizeDelta = new Vector2(0f, reserved);
            temporaryTitle.text = $"임시 보관함 {count}개 — 출격 전에 비우세요 · Stash·가방으로 끌어 놓기 / 더블클릭 = Stash로";
        }
        float bottom = count > 0 ? reserved + 8f : 0f;
        if (stashViewportRect != null) stashViewportRect.offsetMin = new Vector2(0f, bottom);
        if (saleViewportRect != null) saleViewportRect.offsetMin = new Vector2(0f, bottom);
    }

    private void SelectTemporaryItem(LootItemInstance item)
    {
        selectedTemporaryId = item?.InstanceId;
        selected = null; // 임시 보관함을 고르면 Stash 선택은 해제 ('선택 → 열린 가방'이 임시 아이템을 옮기게)
        selectedSourceBagId = null;
        selectedFromSaleBasket = false;
        selectedFromStash = false;
        confirmTemporaryDelete = false;
        notice.text = item?.Definition?.Name ?? "아이템을 선택하세요";
    }

    private void LinkTemporaryDropTargets()
    {
        if (temporaryGridView == null || LootRuntimeState.TemporaryStorage.Count == 0) return;
        temporaryGridView.ClearExternalDropTargets();
        temporaryGridView.AddExternalDropTarget(stashGridView,
            (item, x, y, rotated) => LootRuntimeState.MoveTemporaryItemToStashAt(item.InstanceId, x, y, rotated));
        if (BaseCampManagementPanel.IsSellMode)
            temporaryGridView.AddExternalDropTarget(saleGridView,
                (item, x, y, rotated) => LootRuntimeState.MoveTemporaryItemToSaleBasketAt(item.InstanceId, x, y, rotated));
        foreach (BagWindowState window in bagWindows)
        {
            LootItemInstance bag = window.Bag;
            temporaryGridView.AddExternalDropTarget(window.Grid,
                (item, x, y, rotated) => LootRuntimeState.MoveTemporaryItemIntoBagAt(item.InstanceId, bag.InstanceId, x, y, rotated));
        }
    }

    private void MoveTemporaryToStash()
    {
        LootItemInstance item = SelectedTemporary();
        if (item == null) return;
        bool ok = LootRuntimeState.MoveTemporaryItemToStash(item.InstanceId);
        notice.text = ok ? "Stash로 이동했습니다." : LootRuntimeState.LastFailureReason;
        confirmTemporaryDelete = false;
    }

    private void MoveSelectedToOpenedBag()
    {
        if (openedBag == null)
        {
            notice.text = "먼저 대상 가방을 여세요.";
            return;
        }

        LootItemInstance moving = selected;
        bool ok;
        if (moving != null && !selectedFromSaleBasket)
        {
            ok = string.IsNullOrEmpty(selectedSourceBagId)
                ? LootRuntimeState.MoveStashItemIntoBag(moving.InstanceId, openedBag.InstanceId)
                : LootRuntimeState.MoveBagItemToBag(moving.InstanceId, selectedSourceBagId, openedBag.InstanceId);
        }
        else
        {
            moving = SelectedTemporary();
            ok = moving != null && LootRuntimeState.MoveTemporaryItemIntoBag(moving.InstanceId, openedBag.InstanceId);
        }

        if (!ok)
        {
            notice.text = moving == null ? "이동할 아이템을 선택하세요." : LootRuntimeState.LastFailureReason;
            return;
        }

        notice.text = $"{openedBag.Definition.Name}에 넣었습니다.";
        selected = null;
        selectedSourceBagId = null;
        confirmTemporaryDelete = false;
        // 가방을 넣었다면 그 가방을 다음 수납 대상으로 바로 열어 연속 중첩을 쉽게 만든다.
        if (moving.Definition?.Type == LootItemType.Bag) OpenBagWindow(moving);
    }

    private void DeleteTemporary()
    {
        LootItemInstance item = SelectedTemporary();
        if (item == null) return;
        bool hasContents = item.Definition?.Type == LootItemType.Bag && item.BagGrid.Placements.Count > 0;
        if (!confirmTemporaryDelete)
        {
            confirmTemporaryDelete = true;
            notice.text = hasContents ? "가방 안 아이템도 삭제됩니다. 삭제를 다시 누르세요." : "삭제하려면 버튼을 다시 누르세요.";
            return;
        }
        bool ok = LootRuntimeState.DeleteTemporaryItem(item.InstanceId, confirmedNestedContents: true);
        notice.text = ok ? "임시 보관함 아이템을 삭제했습니다." : LootRuntimeState.LastFailureReason;
        confirmTemporaryDelete = false;
    }

    private void MoveTemporaryToSale()
    {
        LootItemInstance item = SelectedTemporary();
        if (item == null) return;
        bool ok = LootRuntimeState.MoveTemporaryItemToSaleBasket(item.InstanceId);
        notice.text = ok ? "판매 바구니로 이동했습니다." : LootRuntimeState.LastFailureReason;
        confirmTemporaryDelete = false;
    }

    private void CreateSlot(Transform parent, LootItemType type, Vector2 min, Vector2 max)
    {
        var go = new GameObject($"Slot_{type}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = SlotBackground;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.03f, 0.12f);
        iconRect.anchorMax = new Vector2(0.03f, 0.88f);
        iconRect.sizeDelta = new Vector2(36f, 0f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        Image icon = iconGo.GetComponent<Image>();
        icon.raycastTarget = false;
        Outline frame = iconGo.AddComponent<Outline>();
        frame.effectColor = LootGridView.InsuredColor;
        frame.effectDistance = new Vector2(2f, -2f);
        frame.enabled = false;

        // 이름이 해제 버튼에 가리지 않게 버튼 왼쪽까지만 쓰고, 길면 두 줄·자동 축소.
        Text label = CreateText(go.transform, "Label", "", 12, new Vector2(0.27f, 0.04f), new Vector2(0.73f, 0.96f));
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 9;
        label.resizeTextMaxSize = 12;

        var buttonGo = new GameObject("Unequip", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(go.transform, false);
        RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.74f, 0.18f);
        buttonRect.anchorMax = new Vector2(0.97f, 0.82f);
        buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
        buttonGo.GetComponent<Image>().color = new Color(0.45f, 0.16f, 0.12f, 0.95f);
        Button unequip = buttonGo.GetComponent<Button>();
        unequip.onClick.AddListener(() =>
        {
            bool ok = LootRuntimeState.TryUnequipToStash(type);
            notice.text = ok ? "Stash로 해제했습니다." : (LootRuntimeState.LastFailureReason ?? "해제 실패");
        });
        CreateText(buttonGo.transform, "Label", "해제", 11, Vector2.zero, Vector2.one);

        slots[type] = (icon, frame, label, unequip);
    }

    private void RefreshSlots()
    {
        foreach (var (type, slotLabel) in SlotOrder)
        {
            if (!slots.TryGetValue(type, out var slot)) continue;
            LootItemInstance item = LootRuntimeState.GetEquipped(type);
            bool insured = item != null && LootRuntimeState.IsInsured(item.InstanceId);
            slot.icon.color = item?.Definition != null ? LootGridView.RarityColor(item.Definition.Rarity) : EmptyIconColor;
            slot.frame.enabled = insured;
            string name = item?.Definition != null ? item.Definition.Name : $"{slotLabel} 없음";
            slot.label.text = item == null
                ? name
                : LootGridView.FormatFoundInRaidName(item, insured ? "[보험] " : "");
            slot.label.color = item == null ? EmptyLabelColor : insured ? LootGridView.InsuredColor : Color.white;
            slot.unequip.gameObject.SetActive(item != null);
        }
    }

    private void SelectRunBagItem(LootItemInstance item)
    {
        selected = item;
        selectedSourceBagId = null;
        selectedFromSaleBasket = false;
        selectedFromStash = false;
        notice.text = item?.Definition?.Name ?? "아이템을 선택하세요";
    }

    private void SelectStashItem(LootItemInstance item)
    {
        selected = item;
        selectedSourceBagId = null;
        selectedFromSaleBasket = false;
        selectedFromStash = true;
        notice.text = item?.Definition?.Name ?? "아이템을 선택하세요";
    }

    private void SelectSaleBasketItem(LootItemInstance item)
    {
        selected = item;
        selectedSourceBagId = null;
        selectedFromSaleBasket = true;
        selectedFromStash = false;
        notice.text = item?.Definition != null ? $"판매 대기: {item.Definition.Name}" : "아이템을 선택하세요";
    }

    private void SelectOpenedBagItem(BagWindowState window, LootItemInstance item)
    {
        FocusBagWindow(window);
        selected = item;
        selectedSourceBagId = window?.Bag?.InstanceId;
        selectedFromSaleBasket = false;
        selectedFromStash = false;
        notice.text = item?.Definition != null ? $"열린 가방: {item.Definition.Name}" : "아이템을 선택하세요";
    }

    private void MoveToStash()
    {
        MoveToStash(selected);
    }

    private void MoveToStash(LootItemInstance item)
    {
        if (item == null) return;
        selected = item;
        bool ok = LootRuntimeState.MoveBagItemToStash(item.InstanceId);
        notice.text = ok ? "Stash로 이동했습니다." : (LootRuntimeState.LastFailureReason ?? "이동 실패");
        if (ok)
        {
            selected = null;
            selectedSourceBagId = null;
            gridView.ClearSelection();
        }
    }

    private void Equip()
    {
        if (selected == null) return;
        bool ok = selectedFromStash
            ? LootRuntimeState.TryEquipFromStash(selected.InstanceId)
            : LootRuntimeState.TryEquipFromBaseCampBag(selected.InstanceId);
        notice.text = ok ? "장착했습니다." : LootRuntimeState.LastFailureReason;
        if (ok)
        {
            selected = null;
            selectedSourceBagId = null;
            selectedFromStash = false;
            gridView.ClearSelection();
            stashGridView.ClearSelection();
        }
    }

    private void HandleStoredItemDoubleClick(LootItemInstance item)
    {
        if (item?.Definition?.Type == LootItemType.Bag)
        {
            OpenBagWindow(item);
            selected = null;
            return;
        }

        if (openedBag != null)
        {
            bool inserted = LootRuntimeState.MoveStashItemIntoBag(item.InstanceId, openedBag.InstanceId);
            notice.text = inserted ? $"{openedBag.Definition.Name}에 넣었습니다." : LootRuntimeState.LastFailureReason;
            return;
        }

        if (BaseCampManagementPanel.IsSellMode)
        {
            bool moved = LootRuntimeState.MoveStashItemToSaleBasket(item.InstanceId);
            notice.text = moved ? "판매 바구니로 이동했습니다." : LootRuntimeState.LastFailureReason;
            return;
        }

        bool ok = LootRuntimeState.MoveStashItemToBag(item.InstanceId);
        notice.text = ok ? "아이템을 이동했습니다." : (LootRuntimeState.LastFailureReason ?? "이동 실패");
    }

    private void HandleOpenedBagItemDoubleClick(BagWindowState window, LootItemInstance item)
    {
        FocusBagWindow(window);
        if (item?.Definition?.Type == LootItemType.Bag)
        {
            OpenBagWindow(item);
            selected = null;
            return;
        }

        bool ok = LootRuntimeState.MoveBagItemToParent(item.InstanceId, window.Bag.InstanceId);
        notice.text = ok ? "상위 보관함으로 이동했습니다." : (LootRuntimeState.LastFailureReason ?? "이동 실패");
    }

    private void MoveSelectedToSale()
    {
        if (selected == null || selectedFromSaleBasket) return;
        bool ok = LootRuntimeState.MoveStashItemToSaleBasket(selected.InstanceId);
        notice.text = ok ? "판매 바구니로 이동했습니다." : LootRuntimeState.LastFailureReason;
        if (ok) selected = null;
    }

    private void OpenBagWindow(LootItemInstance bag)
    {
        if (bag?.Definition?.Type != LootItemType.Bag || hudRoot == null) return;
        BagWindowState existing = bagWindows.Find(value => value.Bag.InstanceId == bag.InstanceId);
        if (existing != null)
        {
            FocusBagWindow(existing);
            return;
        }
        if (bagWindows.Count >= MaxOpenBagWindows)
        {
            BagWindowState oldest = bagWindows[0];
            bagWindows.RemoveAt(0);
            if (oldest.Root != null) Destroy(oldest.Root.gameObject);
        }

        var state = new BagWindowState { Bag = bag };
        var window = new GameObject($"BagWindow_{bag.InstanceId}", typeof(RectTransform), typeof(Image), typeof(Outline));
        window.transform.SetParent(hudRoot, false);
        state.Root = window.GetComponent<RectTransform>();
        state.Root.anchorMin = state.Root.anchorMax = new Vector2(0.5f, 0.5f);
        state.Root.pivot = new Vector2(0.5f, 0.5f);
        float width = Mathf.Max(270f, bag.BagGrid.Width * LootGridView.MinimumCellSize + 44f);
        float height = Mathf.Max(220f, bag.BagGrid.Height * LootGridView.MinimumCellSize + 94f);
        state.Root.sizeDelta = new Vector2(width, height);
        state.Root.anchoredPosition = new Vector2(190f + bagWindows.Count * 26f, 70f - bagWindows.Count * 26f);
        window.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.065f, 0.99f);
        Outline outline = window.GetComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.52f, 0.16f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        var header = new GameObject("DragHeader", typeof(RectTransform), typeof(Image), typeof(BagWindowDragHandle));
        header.transform.SetParent(window.transform, false);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 0.84f);
        headerRect.anchorMax = Vector2.one;
        headerRect.offsetMin = headerRect.offsetMax = Vector2.zero;
        header.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.23f, 1f);
        state.Title = CreateText(header.transform, "Title", "열린 가방", 15,
            new Vector2(0.03f, 0f), new Vector2(0.67f, 1f));
        header.GetComponent<BagWindowDragHandle>().Initialize(state.Root, () => FocusBagWindow(state));
        CreateButton(window.transform, "OpenParent", "상위 열기", new Vector2(0.68f, 0.88f), () => OpenParentBag(state), 0.17f);
        CreateButton(window.transform, "Close", "닫기", new Vector2(0.86f, 0.88f), () => CloseBagWindow(state), 0.12f);

        var viewport = new GameObject("BagViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(window.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.05f, 0.06f);
        viewportRect.anchorMax = new Vector2(0.95f, 0.82f);
        viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
        var gridObject = new GameObject("BagGrid", typeof(RectTransform));
        gridObject.transform.SetParent(viewport.transform, false);
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0f, 1f);
        gridRect.pivot = new Vector2(0f, 1f);
        gridRect.anchoredPosition = Vector2.zero;
        state.Grid = gridObject.AddComponent<LootGridView>();
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = gridRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = GridScrollSensitivity;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        bagWindows.Add(state);
        FocusBagWindow(state);
        Refresh();
    }

    private void FocusBagWindow(BagWindowState window)
    {
        if (window == null) return;
        openedBag = window.Bag;
        window.Root?.SetAsLastSibling();
    }

    private void OpenParentBag(BagWindowState window)
    {
        LootItemInstance parent = FindParentContaining(window.Bag.InstanceId);
        if (parent != null) OpenBagWindow(parent);
        else notice.text = "이 가방은 Stash 최상위에 있습니다.";
    }

    private void CloseBagWindow(BagWindowState window)
    {
        if (window == null) return;
        bagWindows.Remove(window);
        if (window.Root != null) Destroy(window.Root.gameObject);
        openedBag = bagWindows.Count > 0 ? bagWindows[bagWindows.Count - 1].Bag : null;
        selected = null;
        Refresh();
    }

    private static LootItemInstance FindParentContaining(string childId)
    {
        foreach (LootItemInstance root in LootRuntimeState.Stash)
        {
            LootItemInstance found = FindParentContaining(root, childId);
            if (found != null) return found;
        }
        return null;
    }

    private static LootItemInstance FindParentContaining(LootItemInstance parent, string childId)
    {
        if (parent?.BagGrid == null) return null;
        if (parent.BagGrid.Find(childId) != null) return parent;
        foreach (LootGridPlacement child in parent.BagGrid.Placements)
        {
            LootItemInstance found = FindParentContaining(child.Item, childId);
            if (found != null) return found;
        }
        return null;
    }

    private static Text CreateText(Transform parent, string name, string value, int size, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Text text = go.GetComponent<Text>();
        text.font = HudFont.GetDefault();
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, UnityEngine.Events.UnityAction action, float width = 0.39f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor + new Vector2(width, 0.06f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0.16f, 0.36f, 0.48f, 0.95f);
        go.GetComponent<Button>().onClick.AddListener(action);
        Text buttonLabel = CreateText(go.transform, "Label", label, 13, Vector2.zero, Vector2.one);
        buttonLabel.resizeTextForBestFit = true;
        buttonLabel.resizeTextMinSize = 9;
        buttonLabel.resizeTextMaxSize = 13;
        return go.GetComponent<Button>();
    }
}

public sealed class BagWindowDragHandle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
{
    private RectTransform target;
    private System.Action focus;
    private Canvas canvas;

    public void Initialize(RectTransform window, System.Action onFocus)
    {
        target = window;
        focus = onFocus;
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData eventData) => focus?.Invoke();
    public void OnBeginDrag(PointerEventData eventData) => focus?.Invoke();

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null) return;
        float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        target.anchoredPosition += eventData.delta / scale;
        RectTransform parent = target.parent as RectTransform;
        if (parent == null) return;
        Rect bounds = parent.rect;
        Vector2 half = target.rect.size * 0.5f;
        target.anchoredPosition = new Vector2(
            Mathf.Clamp(target.anchoredPosition.x, bounds.xMin + half.x, bounds.xMax - half.x),
            Mathf.Clamp(target.anchoredPosition.y, bounds.yMin + half.y, bounds.yMax - half.y));
    }
}

// BaseCamp 3열 배치의 단일 계산 지점. OnGUI와 uGUI 모두 같은 Rect를 사용한다.
public readonly struct BaseCampLayoutRects
{
    public readonly Rect Merchant;
    public readonly Rect Stash;
    public readonly Rect Loadout;
    public readonly Rect Insurance;
    public readonly Rect Expedition;

    public BaseCampLayoutRects(Rect merchant, Rect stash, Rect loadout, Rect insurance, Rect expedition)
    {
        Merchant = merchant;
        Stash = stash;
        Loadout = loadout;
        Insurance = insurance;
        Expedition = expedition;
    }
}

public static class BaseCampLayout
{
    private const float Margin = 16f;
    private const float Gap = 12f;

    public static BaseCampLayoutRects Calculate()
    {
        float totalWidth = Mathf.Max(960f, Screen.width);
        float totalHeight = Mathf.Max(640f, Screen.height);
        float contentHeight = totalHeight - Margin * 2f;
        float usableWidth = totalWidth - Margin * 2f - Gap * 2f;
        float leftWidth = Mathf.Clamp(usableWidth * 0.25f, 300f, 420f);
        float rightWidth = Mathf.Clamp(usableWidth * 0.29f, 360f, 520f);
        float middleWidth = usableWidth - leftWidth - rightWidth;

        float leftX = Margin;
        float middleX = leftX + leftWidth + Gap;
        float rightX = middleX + middleWidth + Gap;
        var merchant = new Rect(leftX, Margin, leftWidth, contentHeight);
        var stash = new Rect(middleX, Margin, middleWidth, contentHeight);

        float loadoutHeight = contentHeight * 0.56f;
        float insuranceHeight = contentHeight * 0.20f;
        float expeditionHeight = contentHeight - loadoutHeight - insuranceHeight - Gap * 2f;
        var loadout = new Rect(rightX, Margin, rightWidth, loadoutHeight);
        var insurance = new Rect(rightX, Margin + loadoutHeight + Gap, rightWidth, insuranceHeight);
        var expedition = new Rect(rightX, insurance.yMax + Gap, rightWidth, expeditionHeight);
        return new BaseCampLayoutRects(merchant, stash, loadout, insurance, expedition);
    }

    public static void ApplyTo(RectTransform target, Rect topLeftRect)
    {
        if (target == null) return;
        target.anchorMin = target.anchorMax = Vector2.zero;
        target.pivot = Vector2.zero;
        target.anchoredPosition = new Vector2(topLeftRect.x, Screen.height - topLeftRect.yMax);
        target.sizeDelta = topLeftRect.size;
    }
}
