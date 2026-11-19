using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 결정 44: 던전에서 출격 가방 안의 하위 가방을 영지 Stash처럼 떠 있는 창으로 연다.
// 루팅 창·인벤토리와 동시에 열리고, 창끼리·출격 가방·루팅 창 사이 드래그와 가방 아이콘 드롭을 지원한다.
// 새 Canvas를 만들지 않고 공용 HUD 루트를 쓴다 (BUG-044). 창 최대 개수는 m9_stash.csv bag_window_max.
// Tick: 하위 가방에서 꺼내기 1Tick(줍기) / 넣기·창 사이 이동·루팅 창에 놓기 = 버리기 Tick / 루팅 창에서 가져오기 1Tick.
public sealed class DungeonBagWindows : MonoBehaviour
{
    private sealed class Window
    {
        public LootItemInstance Bag;
        public RectTransform Root;
        public LootGridView Grid;
        public Text Title;
    }

    private static DungeonBagWindows instance;
    private readonly List<Window> windows = new List<Window>();
    private Transform hudRoot;

    public static bool HasOpenWindows => instance != null && instance.windows.Count > 0;

    private void Awake()
    {
        instance = this;
        LootRuntimeState.Changed += HandleStateChanged;
    }

    private void OnDestroy()
    {
        LootRuntimeState.Changed -= HandleStateChanged;
        if (instance == this) instance = null;
    }

    public static void Open(LootItemInstance bag)
    {
        if (instance == null || bag?.Definition?.Type != LootItemType.Bag || bag.BagGrid == null) return;
        if (LootRuntimeState.GetRunBagDepth(bag.InstanceId) <= 0)
        {
            InventoryPanel.ShowBagNotice("출격 가방 안의 가방만 열 수 있습니다.");
            return;
        }
        instance.OpenWindow(bag);
    }

    public static bool CloseNewestForEscape()
    {
        if (!HasOpenWindows) return false;
        instance.CloseWindow(instance.windows[instance.windows.Count - 1]);
        return true;
    }

    public static void CloseAll()
    {
        if (instance == null) return;
        for (int i = instance.windows.Count - 1; i >= 0; i--) instance.DestroyWindow(instance.windows[i]);
        instance.windows.Clear();
        RebuildLinks();
    }

    private void OpenWindow(LootItemInstance bag)
    {
        Window existing = windows.Find(value => value.Bag.InstanceId == bag.InstanceId);
        if (existing != null)
        {
            existing.Root.SetAsLastSibling();
            return;
        }
        if (windows.Count >= M9StashConfig.BagWindowMax)
        {
            DestroyWindow(windows[0]);
            windows.RemoveAt(0);
        }
        if (hudRoot == null) hudRoot = ScreenSpaceCanvasProvider.GetOrCreateHudRoot();

        var window = new Window { Bag = bag };
        var rootObject = new GameObject($"DungeonBagWindow_{bag.InstanceId}", typeof(RectTransform), typeof(Image), typeof(Outline));
        rootObject.transform.SetParent(hudRoot, false);
        window.Root = rootObject.GetComponent<RectTransform>();
        window.Root.anchorMin = window.Root.anchorMax = new Vector2(0.5f, 0.5f);
        window.Root.pivot = new Vector2(0.5f, 0.5f);
        float cell = LootGridView.MinimumCellSize;
        float width = Mathf.Max(240f, bag.BagGrid.Width * cell + 24f);
        float height = bag.BagGrid.Height * cell + 56f;
        window.Root.sizeDelta = new Vector2(width, height);
        window.Root.anchoredPosition = new Vector2(-60f + windows.Count * 26f, 80f - windows.Count * 26f);
        rootObject.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.065f, 0.99f);
        Outline outline = rootObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.52f, 0.16f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        var header = new GameObject("DragHeader", typeof(RectTransform), typeof(Image), typeof(BagWindowDragHandle));
        header.transform.SetParent(rootObject.transform, false);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 32f);
        headerRect.anchoredPosition = Vector2.zero;
        header.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.23f, 1f);
        header.GetComponent<BagWindowDragHandle>().Initialize(window.Root, () => window.Root.SetAsLastSibling());
        window.Title = CreateText(header.transform, "Title", 13, TextAnchor.MiddleLeft);
        RectTransform titleRect = window.Title.rectTransform;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(8f, 0f);
        titleRect.offsetMax = new Vector2(-40f, 0f);

        var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(header.transform, false);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.sizeDelta = new Vector2(28f, 24f);
        closeRect.anchoredPosition = new Vector2(-4f, 0f);
        close.GetComponent<Image>().color = new Color(0.45f, 0.16f, 0.14f, 1f);
        close.GetComponent<Button>().onClick.AddListener(() => CloseWindow(window));
        Text closeLabel = CreateText(close.transform, "Label", 13, TextAnchor.MiddleCenter);
        closeLabel.text = "X";
        closeLabel.rectTransform.anchorMin = Vector2.zero;
        closeLabel.rectTransform.anchorMax = Vector2.one;
        closeLabel.rectTransform.offsetMin = closeLabel.rectTransform.offsetMax = Vector2.zero;

        var gridObject = new GameObject("BagGrid", typeof(RectTransform));
        gridObject.transform.SetParent(rootObject.transform, false);
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0f, 1f);
        gridRect.pivot = new Vector2(0f, 1f);
        gridRect.anchoredPosition = new Vector2(12f, -42f);
        window.Grid = gridObject.AddComponent<LootGridView>();
        window.Grid.Bind(bag.BagGrid, bag.BagGrid.Move);
        window.Grid.SetItemClickHandler(_ => window.Root.SetAsLastSibling());
        window.Grid.SetItemDoubleClickHandler(item => ActivateItem(window, item));
        window.Grid.SetItemOpenHandler(Open);
        window.Grid.SetItemQuickMoveHandler(item => QuickMove(window, item));
        window.Grid.SetBagDropHandler(CanDropOnBagIcon, DropOnBagIcon);

        windows.Add(window);
        window.Root.SetAsLastSibling();
        UpdateTitle(window);
        RebuildLinks();
    }

    private void CloseWindow(Window window)
    {
        if (window == null) return;
        windows.Remove(window);
        DestroyWindow(window);
        RebuildLinks();
    }

    private void DestroyWindow(Window window)
    {
        if (window?.Root != null) Destroy(window.Root.gameObject);
    }

    private void HandleStateChanged()
    {
        bool closedAny = false;
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            Window window = windows[i];
            // 가방을 버리거나 상자에 넣어 소지품에서 빠지면 그 창은 닫는다.
            if (LootRuntimeState.GetRunBagDepth(window.Bag.InstanceId) <= 0)
            {
                windows.RemoveAt(i);
                DestroyWindow(window);
                closedAny = true;
                continue;
            }
            window.Grid.Refresh();
            UpdateTitle(window);
        }
        if (closedAny) RebuildLinks();
    }

    private static void UpdateTitle(Window window)
    {
        window.Title.text = $"{window.Bag.Definition?.Name}  ({LootRuntimeState.RunNestedItemCount}/{M9StashConfig.RunNestedItemLimit})";
    }

    // 더블클릭: 가방이면 열기, 아니면 출격 가방으로 꺼내기 (1Tick)
    private void ActivateItem(Window window, LootItemInstance item)
    {
        if (item?.Definition == null) return;
        if (item.Definition.Type == LootItemType.Bag)
        {
            Open(item);
            return;
        }
        Timed(TickCost.PickUp, "TakeOutOfBag", () => LootRuntimeState.MoveNestedItemToRunBag(item));
    }

    // Ctrl+클릭: 루팅 창이 열려 있으면 보관함으로, 아니면 출격 가방으로
    private void QuickMove(Window window, LootItemInstance item)
    {
        if (item == null) return;
        if (LootingPanel.IsOpen)
        {
            LootingPanel.PutRunItem(item);
            return;
        }
        Timed(TickCost.PickUp, "TakeOutOfBag", () => LootRuntimeState.MoveNestedItemToRunBag(item));
    }

    // ── 가방 아이콘 드롭 (출격 가방 격자·가방 창 공용, 결정 43) ──

    public static bool CanDropOnBagIcon(LootItemInstance item, LootItemInstance bag)
    {
        if (!ExpeditionSession.IsActive) return false;
        return LootingPanel.ContainerHas(item)
            ? LootingPanel.CanTakeIntoRunBagIcon(item, bag)
            : LootRuntimeState.CanMoveRunItemIntoBag(item, bag, out _);
    }

    public static bool DropOnBagIcon(LootItemInstance item, LootItemInstance bag)
    {
        if (!ExpeditionSession.IsActive) return false;
        if (LootingPanel.ContainerHas(item)) return LootingPanel.TakeIntoRunBagIcon(item, bag);
        bool spent = Timed(TickCost.Drop, "MoveIntoBag", () => LootRuntimeState.MoveRunItemIntoBag(item, bag));
        if (spent) InventoryPanel.ShowBagNotice($"{bag.Definition?.Name}에 넣었습니다.");
        return spent;
    }

    private static bool Timed(float cost, string name, System.Func<bool> action)
    {
        PlayerTurnActor player = PlayerTurnActor.Instance;
        if (player == null) return false;
        bool spent = player.TryPerformTimedAction(cost, name, action);
        if (!spent) InventoryPanel.ShowBagNotice(LootRuntimeState.LastFailureReason);
        return spent;
    }

    // ── 드래그 연결: 출격 가방 · 루팅 창 · 가방 창들 사이 ──
    // 창이 열리고 닫히거나 루팅 창이 열리고 닫힐 때마다 전체를 다시 만든다 (한 곳에서만 연결).
    public static void RebuildLinks()
    {
        LootGridView run = InventoryPanel.RunBagGridView;
        LootGridView loot = LootingPanel.IsOpen ? LootingPanel.ActiveGridView : null;
        run?.ClearExternalDropTargets();
        loot?.ClearExternalDropTargets();
        List<Window> list = instance != null ? instance.windows : new List<Window>();
        foreach (Window window in list) window.Grid.ClearExternalDropTargets();

        if (run != null && loot != null)
        {
            loot.AddExternalDropTarget(run, LootingPanel.TakeToRunBagAt);
            run.AddExternalDropTarget(loot, LootingPanel.PutRunItemAt);
        }

        foreach (Window window in list)
        {
            LootItemInstance bag = window.Bag;
            if (run != null)
            {
                run.AddExternalDropTarget(window.Grid, (item, x, y, r) =>
                    Timed(TickCost.Drop, "MoveIntoBag", () => LootRuntimeState.MoveRunItemToBagAt(item, bag, x, y, r)));
                window.Grid.AddExternalDropTarget(run, (item, x, y, r) =>
                    Timed(TickCost.PickUp, "TakeOutOfBag", () => LootRuntimeState.MoveNestedItemToRunBagAt(item, x, y, r)));
            }
            if (loot != null)
            {
                loot.AddExternalDropTarget(window.Grid, (item, x, y, r) => LootingPanel.TakeIntoBagAt(item, bag, x, y, r));
                window.Grid.AddExternalDropTarget(loot, LootingPanel.PutRunItemAt);
            }
            foreach (Window other in list)
            {
                if (other == window) continue;
                LootItemInstance otherBag = other.Bag;
                window.Grid.AddExternalDropTarget(other.Grid, (item, x, y, r) =>
                    Timed(TickCost.Drop, "MoveIntoBag", () => LootRuntimeState.MoveRunItemToBagAt(item, otherBag, x, y, r)));
            }
        }
    }

    private static Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = HudFont.GetDefault();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
