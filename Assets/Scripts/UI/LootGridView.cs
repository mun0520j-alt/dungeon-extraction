using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 데이터는 소유하지 않고 LootGridModel을 화면에 그리는 재사용 uGUI 컴포넌트다.
public sealed class LootGridView : MonoBehaviour
{
    public const float MinimumCellSize = 48f;
    private const float CellGap = 2f;
    private const float DragAutoScrollEdge = 72f;
    private const float DragAutoScrollSpeed = 360f;

    private LootGridModel model;
    private Func<string, int, int, bool, bool> moveHandler;
    private sealed class ExternalDropBinding
    {
        public LootGridView Target;
        public Func<LootItemInstance, int, int, bool, bool> Handler;
    }
    private readonly System.Collections.Generic.List<ExternalDropBinding> externalDropTargets
        = new System.Collections.Generic.List<ExternalDropBinding>();
    private Action<LootItemInstance> itemClickHandler;
    private Action<LootItemInstance> itemDoubleClickHandler;
    // 결정 42 (타르코프식): 우클릭 = 가방 열기, Ctrl+좌클릭 = 반대편으로 바로 옮기기
    private Action<LootItemInstance> itemOpenHandler;
    private Action<LootItemInstance> itemQuickMoveHandler;
    private Func<LootItemInstance, bool> dimPredicate;
    private Func<LootItemInstance, LootItemInstance, bool> bagDropCheck;
    private Func<LootItemInstance, LootItemInstance, bool> bagDropHandler;
    private RectTransform gridRoot;
    private RectTransform preview;
    private Text tooltip;
    private LootGridItemView dragged;
    private bool dragRotated;
    private Vector2 dragScreenPosition;
    private RectTransform dragGhost;
    private string selectedInstanceId;
    private readonly System.Collections.Generic.Dictionary<string, Outline> selectionOutlines
        = new System.Collections.Generic.Dictionary<string, Outline>();

    public void Bind(LootGridModel source, Func<string, int, int, bool, bool> onMove)
    {
        model = source;
        moveHandler = onMove;
        Build();
    }

    public void Refresh()
    {
        if (model == null) return;
        Build();
    }

    public void SetItemClickHandler(Action<LootItemInstance> handler)
    {
        itemClickHandler = handler;
    }

    public void SetItemDoubleClickHandler(Action<LootItemInstance> handler)
    {
        itemDoubleClickHandler = handler;
    }

    public void SetItemOpenHandler(Action<LootItemInstance> handler)
    {
        itemOpenHandler = handler;
    }

    public void SetItemQuickMoveHandler(Action<LootItemInstance> handler)
    {
        itemQuickMoveHandler = handler;
    }

    public void SetDimPredicate(Func<LootItemInstance, bool> predicate)
    {
        dimPredicate = predicate;
        Refresh();
    }

    // 이 격자에 놓인 가방 아이콘 위로 다른 아이템을 놓으면 그 가방에 자동으로 넣는다(영지 결정 37, 던전 결정 43). check는 미리보기 색, handler는 실제 이동.
    public void SetBagDropHandler(Func<LootItemInstance, LootItemInstance, bool> check, Func<LootItemInstance, LootItemInstance, bool> handler)
    {
        bagDropCheck = check;
        bagDropHandler = handler;
    }

    public void ClearExternalDropTargets()
    {
        HideExternalPreviews();
        externalDropTargets.Clear();
    }

    public void AddExternalDropTarget(LootGridView target, Func<LootItemInstance, int, int, bool, bool> handler)
    {
        if (target == null || handler == null) return;
        externalDropTargets.Add(new ExternalDropBinding { Target = target, Handler = handler });
    }

    private void Update()
    {
        if (dragged == null) return;
        AutoScrollWhileDragging();
        foreach (ExternalDropBinding binding in externalDropTargets) binding.Target.AutoScrollAt(dragScreenPosition);
        UpdateDragGhost(dragScreenPosition);
        UpdateDragPreview(dragScreenPosition);
        if (!Input.GetKeyDown(KeyCode.R)) return;
        dragRotated = !dragRotated;
        ResizeDragGhost(dragged.Item, dragRotated);
        UpdateDragPreview(dragScreenPosition);
    }

    private void Build()
    {
        if (model == null) return;
        if (!string.IsNullOrEmpty(selectedInstanceId) && model.Find(selectedInstanceId) == null)
            selectedInstanceId = null;
        RectTransform ownRect = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        ownRect.sizeDelta = new Vector2(model.Width * MinimumCellSize, model.Height * MinimumCellSize);

        selectionOutlines.Clear();
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediateSafe(transform.GetChild(i).gameObject);

        var background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        background.color = new Color(0.025f, 0.03f, 0.04f, 0.98f);

        var rootObject = new GameObject("GridRoot", typeof(RectTransform));
        rootObject.transform.SetParent(transform, false);
        gridRoot = rootObject.GetComponent<RectTransform>();
        gridRoot.anchorMin = new Vector2(0f, 1f);
        gridRoot.anchorMax = new Vector2(0f, 1f);
        gridRoot.pivot = new Vector2(0f, 1f);
        gridRoot.sizeDelta = ownRect.sizeDelta;

        for (int y = 0; y < model.Height; y++)
        for (int x = 0; x < model.Width; x++) CreateCell(x, y);

        foreach (LootGridPlacement placement in model.Placements) CreateItem(placement);
        CreatePreview();
        CreateTooltip();
    }

    private void CreateCell(int x, int y)
    {
        var cell = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
        cell.transform.SetParent(gridRoot, false);
        RectTransform rect = cell.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x * MinimumCellSize, -y * MinimumCellSize);
        rect.sizeDelta = new Vector2(MinimumCellSize - CellGap, MinimumCellSize - CellGap);
        cell.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
    }

    private void CreateItem(LootGridPlacement placement)
    {
        var itemObject = new GameObject($"Item_{placement.Item.InstanceId}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        itemObject.transform.SetParent(gridRoot, false);
        RectTransform rect = itemObject.GetComponent<RectTransform>();
        SetItemRect(rect, placement.X, placement.Y, placement.Width, placement.Height);
        Image image = itemObject.GetComponent<Image>();
        image.color = RarityColor(placement.Item.Definition.Rarity);
        itemObject.GetComponent<CanvasGroup>().alpha = dimPredicate != null && dimPredicate(placement.Item) ? 0.28f : 1f;
        Outline selectionOutline = itemObject.AddComponent<Outline>();
        selectionOutline.effectColor = new Color(1f, 0.82f, 0.18f, 1f);
        selectionOutline.effectDistance = new Vector2(3f, -3f);
        selectionOutline.useGraphicAlpha = false;
        selectionOutline.enabled = placement.Item.InstanceId == selectedInstanceId;
        selectionOutlines[placement.Item.InstanceId] = selectionOutline;
        if (LootRuntimeState.IsInsured(placement.Item.InstanceId)) CreateInsuredFrame(itemObject.transform);
        if (placement.Item.IsFoundInRaid) CreateFoundInRaidMarker(itemObject.transform);

        var labelObject = new GameObject("ShortLabel", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(itemObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        Text label = labelObject.GetComponent<Text>();
        label.font = HudFont.GetDefault();
        label.fontSize = 12;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = ShortName(placement.Item.Definition.Name);
        label.raycastTarget = false;

        LootGridItemView view = itemObject.AddComponent<LootGridItemView>();
        view.Initialize(this, placement.Item, placement.Rotated);
    }

    private void CreatePreview()
    {
        var go = new GameObject("DropPreview", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(gridRoot, false);
        preview = go.GetComponent<RectTransform>();
        go.GetComponent<Image>().color = new Color(0.2f, 0.9f, 0.35f, 0.45f);
        // 미리보기가 레이캐스트를 가로채면 아래 가방 아이콘·격자 판정이 프레임마다 바뀐다.
        go.GetComponent<Image>().raycastTarget = false;
        go.SetActive(false);
    }

    private void CreateTooltip()
    {
        var go = new GameObject("Tooltip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(230f, 86f);
        Image tooltipBackground = go.GetComponent<Image>();
        tooltipBackground.color = new Color(0.02f, 0.02f, 0.025f, 0.97f);
        // 툴팁이 커서 아래에 나타나도 원래 아이템의 PointerEnter/Exit 판정을 가로채지 않는다.
        // Raycast를 받으면 아이템 이탈 → 툴팁 숨김 → 재진입이 매 프레임 반복돼 깜빡인다.
        tooltipBackground.raycastTarget = false;
        var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(go.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 6f);
        textRect.offsetMax = new Vector2(-8f, -6f);
        tooltip = textObject.GetComponent<Text>();
        tooltip.font = HudFont.GetDefault();
        tooltip.fontSize = 13;
        tooltip.alignment = TextAnchor.UpperLeft;
        tooltip.color = Color.white;
        tooltip.raycastTarget = false;
        go.SetActive(false);
    }

    internal void BeginDrag(LootGridItemView item, Vector2 screenPosition)
    {
        dragged = item;
        dragRotated = item.Rotated;
        dragScreenPosition = screenPosition;
        item.GetComponent<CanvasGroup>().alpha = 0.35f;
        item.GetComponent<CanvasGroup>().blocksRaycasts = false;
        HideTooltip();
        CreateDragGhost(item.Item, dragRotated, screenPosition);
        UpdateDragPreview(screenPosition);
    }

    internal void Drag(Vector2 screenPosition)
    {
        dragScreenPosition = screenPosition;
        UpdateDragGhost(screenPosition);
        UpdateDragPreview(screenPosition);
    }

    internal void EndDrag(Vector2 screenPosition)
    {
        if (dragged == null) return;
        // 미리보기와 같은 판정 함수를 쓴다. 미리보기가 뜬 격자·칸에만 놓이고, 판정이 없으면 이동 취소(원위치).
        if (TryResolveBagIconTarget(screenPosition, out LootGridView bagOwner, out LootItemInstance bag))
            bagOwner.bagDropHandler(dragged.Item, bag);
        else if (TryResolveDropTarget(screenPosition, out LootGridView target, out ExternalDropBinding binding, out int x, out int y))
        {
            if (binding == null)
            {
                if (model.CanMove(dragged.Item.InstanceId, x, y, dragRotated))
                    moveHandler?.Invoke(dragged.Item.InstanceId, x, y, dragRotated);
            }
            else binding.Handler(dragged.Item, x, y, dragRotated);
        }
        dragged.GetComponent<CanvasGroup>().alpha = 1f;
        dragged.GetComponent<CanvasGroup>().blocksRaycasts = true;
        dragged = null;
        if (dragGhost != null) DestroyImmediateSafe(dragGhost.gameObject);
        dragGhost = null;
        if (preview != null) preview.gameObject.SetActive(false);
        HideExternalPreviews();
        Refresh();
    }

    private void AutoScrollWhileDragging()
    {
        ScrollRect scroll = GetComponentInParent<ScrollRect>();
        AutoScrollAt(dragScreenPosition, scroll);
    }

    private void AutoScrollAt(Vector2 screenPosition)
    {
        AutoScrollAt(screenPosition, GetComponentInParent<ScrollRect>());
    }

    private static void AutoScrollAt(Vector2 screenPosition, ScrollRect scroll)
    {
        if (scroll == null || !scroll.vertical || scroll.viewport == null || scroll.content == null) return;

        Camera eventCamera = null;
        Canvas canvas = scroll.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) eventCamera = canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(scroll.viewport, screenPosition, eventCamera, out Vector2 local)) return;

        Rect rect = scroll.viewport.rect;
        if (local.x < rect.xMin || local.x > rect.xMax) return;
        float direction = 0f;
        if (local.y >= rect.yMax - DragAutoScrollEdge) direction = 1f;
        else if (local.y <= rect.yMin + DragAutoScrollEdge) direction = -1f;
        if (Mathf.Approximately(direction, 0f)) return;

        float overflow = Mathf.Max(1f, scroll.content.rect.height - scroll.viewport.rect.height);
        scroll.verticalNormalizedPosition = Mathf.Clamp01(
            scroll.verticalNormalizedPosition + direction * DragAutoScrollSpeed * Time.unscaledDeltaTime / overflow);
    }

    // 보험에 든 장비는 청록 테두리로 표시한다 (선택 표시의 노랑과 구분).
    public static readonly Color InsuredColor = new Color(0.25f, 0.95f, 0.85f, 1f);
    // 격자 마커와 목록의 ★가 같은 권위 색을 사용한다.
    public static readonly Color FoundInRaidColor = new Color(0.25f, 0.95f, 0.45f, 1f);
    public static string FoundInRaidStarRichText => $"<color=#{ColorUtility.ToHtmlStringRGB(FoundInRaidColor)}>★</color>";
    public static string FormatFoundInRaidName(LootItemInstance item, string prefix = "")
    {
        if (item?.Definition == null) return prefix;
        return prefix + (item.IsFoundInRaid ? FoundInRaidStarRichText + " " : "") + item.Definition.Name;
    }

    private static void CreateInsuredFrame(Transform parent)
    {
        var frame = new GameObject("InsuredFrame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(parent, false);
        RectTransform rect = frame.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2f, 2f);
        rect.offsetMax = new Vector2(-2f, -2f);
        Image image = frame.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
        Outline outline = frame.AddComponent<Outline>();
        outline.effectColor = InsuredColor;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;
    }

    private static void CreateFoundInRaidMarker(Transform parent)
    {
        var marker = new GameObject("FoundInRaidMarker", typeof(RectTransform), typeof(Image));
        marker.transform.SetParent(parent, false);
        RectTransform rect = marker.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(4f, -4f);
        rect.sizeDelta = new Vector2(12f, 12f);
        Image image = marker.GetComponent<Image>();
        image.color = FoundInRaidColor;
        image.raycastTarget = false;
    }

    internal void ShowTooltip(LootGridItemView item, Vector2 screenPosition)
    {
        if (dragged != null || tooltip == null || item?.Item?.Definition == null) return;
        LootItemData data = item.Item.Definition;
        tooltip.text = $"{data.Name} [{data.Rarity}]\n크기 {data.GridWidth}×{data.GridHeight}"
            + $"\n공격 +{data.AttackBonus} / 방어 +{data.DefenseBonus}"
            + (item.Item.IsFoundInRaid ? "\n인레이드" : string.Empty)
            + (LootRuntimeState.IsInsured(item.Item.InstanceId) ? "\n보험됨" : string.Empty);
        tooltip.transform.parent.gameObject.SetActive(true);
        PositionTooltip(screenPosition);
    }

    internal void MoveTooltip(Vector2 screenPosition)
    {
        // 툴팁은 PointerEnter 시 정한 위치에 고정한다. 커서를 매 프레임 따라다니면
        // 아이템과 포인터를 계속 가려 읽기 어렵고 마우스에 붙어 있는 것처럼 보인다.
    }

    internal void HideTooltip()
    {
        if (tooltip != null) tooltip.transform.parent.gameObject.SetActive(false);
    }

    internal void ClickItem(LootItemInstance item)
    {
        if (dragged != null || item == null) return;
        selectedInstanceId = item.InstanceId;
        RefreshSelectionHighlight();
        itemClickHandler?.Invoke(item);
    }

    internal void OpenItem(LootItemInstance item)
    {
        if (dragged != null || item?.Definition?.Type != LootItemType.Bag || itemOpenHandler == null) return;
        selectedInstanceId = item.InstanceId;
        RefreshSelectionHighlight();
        itemOpenHandler(item);
    }

    internal bool QuickMoveItem(LootItemInstance item)
    {
        if (dragged != null || item == null || itemQuickMoveHandler == null) return false;
        itemQuickMoveHandler(item);
        return true;
    }

    internal void DoubleClickItem(LootItemInstance item)
    {
        if (dragged != null || item == null) return;
        selectedInstanceId = item.InstanceId;
        RefreshSelectionHighlight();
        if (itemDoubleClickHandler != null) itemDoubleClickHandler(item);
        else itemClickHandler?.Invoke(item);
    }

    public void ClearSelection()
    {
        selectedInstanceId = null;
        RefreshSelectionHighlight();
    }

    private void RefreshSelectionHighlight()
    {
        foreach (var pair in selectionOutlines)
            if (pair.Value != null) pair.Value.enabled = pair.Key == selectedInstanceId;
    }

    private void UpdateDragPreview(Vector2 screenPosition)
    {
        if (dragged == null || preview == null)
            return;
        preview.gameObject.SetActive(false);
        HideExternalPreviews();
        if (TryResolveBagIconTarget(screenPosition, out LootGridView bagOwner, out LootItemInstance bag))
        {
            LootGridPlacement bagPlacement = bagOwner.model.Find(bag.InstanceId);
            if (bagPlacement != null)
                bagOwner.ShowPreview(bag, bagPlacement.X, bagPlacement.Y, bagPlacement.Rotated,
                    bagOwner.bagDropCheck == null || bagOwner.bagDropCheck(dragged.Item, bag));
            return;
        }
        if (!TryResolveDropTarget(screenPosition, out LootGridView target, out ExternalDropBinding binding, out int x, out int y))
            return;
        bool valid = binding == null
            ? model.CanMove(dragged.Item.InstanceId, x, y, dragRotated)
            : target.model.CanPlace(dragged.Item, x, y, dragRotated);
        target.ShowPreview(dragged.Item, x, y, dragRotated, valid);
    }

    // [BUG-045] 드롭 대상 = 화면에서 맨 위에 보이는 격자. 미리보기·드롭이 이 함수 하나만 쓴다.
    // 이전에는 자기 격자(Stash)를 먼저 확인해, Stash 위에 떠 있는 가방 창에 놓아도 아래 Stash가 가로챘다.
    // EventSystem 레이캐스트의 첫 결과(가장 위 UI)가 자기 격자나 등록된 외부 격자가 아니면(창 제목줄·다른 패널 등) 취소한다.
    private bool TryResolveDropTarget(Vector2 screenPosition, out LootGridView target, out ExternalDropBinding binding, out int x, out int y)
    {
        target = null;
        binding = null;
        x = y = -1;
        if (dragged?.Item == null) return false;

        LootGridView topmost = FindTopmostGridView(screenPosition, out bool raycastAvailable);
        if (!raycastAvailable)
        {
            // EventSystem이 없을 때만 예전 순서(자기 → 외부)로 판정한다.
            if (TryScreenToPlacementCell(screenPosition, dragged.Item, dragRotated, out x, out y)) { target = this; return true; }
            foreach (ExternalDropBinding candidate in externalDropTargets)
            {
                if (!candidate.Target.TryScreenToPlacementCell(screenPosition, dragged.Item, dragRotated, out x, out y)) continue;
                target = candidate.Target;
                binding = candidate;
                return true;
            }
            return false;
        }
        if (topmost == null) return false;
        if (topmost == this)
        {
            if (!TryScreenToPlacementCell(screenPosition, dragged.Item, dragRotated, out x, out y)) return false;
            target = this;
            return true;
        }
        foreach (ExternalDropBinding candidate in externalDropTargets)
        {
            if (candidate.Target != topmost) continue;
            if (!candidate.Target.TryScreenToPlacementCell(screenPosition, dragged.Item, dragRotated, out x, out y)) return false;
            target = candidate.Target;
            binding = candidate;
            return true;
        }
        return false;
    }

    // 화면 맨 위가 가방 아이콘이고 그 격자가 가방 드롭을 받으면 가방 드롭 모드. 대상 격자가 자기이거나 등록된 외부 격자일 때만.
    private bool TryResolveBagIconTarget(Vector2 screenPosition, out LootGridView owner, out LootItemInstance bag)
    {
        owner = null;
        bag = null;
        if (dragged?.Item == null || EventSystem.current == null) return false;
        var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
        RaycastBuffer.Clear();
        EventSystem.current.RaycastAll(pointer, RaycastBuffer);
        if (RaycastBuffer.Count == 0 || RaycastBuffer[0].gameObject == null) return false;
        LootGridItemView itemView = RaycastBuffer[0].gameObject.GetComponentInParent<LootGridItemView>();
        if (itemView?.Item?.Definition == null || itemView.Item.Definition.Type != LootItemType.Bag) return false;
        if (itemView.Item.InstanceId == dragged.Item.InstanceId) return false;
        LootGridView view = itemView.GetComponentInParent<LootGridView>();
        if (view == null || view.bagDropHandler == null) return false;
        bool known = view == this;
        foreach (ExternalDropBinding candidate in externalDropTargets) known |= candidate.Target == view;
        if (!known) return false;
        owner = view;
        bag = itemView.Item;
        return true;
    }

    private static readonly System.Collections.Generic.List<RaycastResult> RaycastBuffer
        = new System.Collections.Generic.List<RaycastResult>();

    private static LootGridView FindTopmostGridView(Vector2 screenPosition, out bool raycastAvailable)
    {
        raycastAvailable = EventSystem.current != null;
        if (!raycastAvailable) return null;
        var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
        RaycastBuffer.Clear();
        EventSystem.current.RaycastAll(pointer, RaycastBuffer);
        if (RaycastBuffer.Count == 0) return null;
        // 결과는 위에서 아래 순. 첫 결과가 속한 격자만 인정한다(아래에 깔린 격자로 넘어가지 않음).
        GameObject hit = RaycastBuffer[0].gameObject;
        if (hit == null) return null;
        LootGridView owner = hit.GetComponentInParent<LootGridView>();
        if (owner != null) return owner;
        // 가방 창 여백·스크롤 영역 빈 곳처럼 격자를 하나만 품은 틀이면 그 격자로 본다(정확히 칸을 조준하지 않아도 됨).
        // 격자를 여러 개 품은 큰 패널이면 어느 격자인지 모호하므로 취소한다.
        LootGridView[] inside = hit.GetComponentsInChildren<LootGridView>(false);
        return inside.Length == 1 ? inside[0] : null;
    }

    private void ShowPreview(LootItemInstance item, int x, int y, bool rotated, bool valid)
    {
        int width = rotated ? item.Definition.GridHeight : item.Definition.GridWidth;
        int height = rotated ? item.Definition.GridWidth : item.Definition.GridHeight;
        SetItemRect(preview, x, y, width, height);
        preview.GetComponent<Image>().color = valid
            ? new Color(0.2f, 0.9f, 0.35f, 0.45f) : new Color(0.95f, 0.15f, 0.12f, 0.55f);
        preview.gameObject.SetActive(true);
        preview.SetAsLastSibling();
    }

    private void HideExternalPreview()
    {
        if (preview != null) preview.gameObject.SetActive(false);
    }

    private void HideExternalPreviews()
    {
        foreach (ExternalDropBinding binding in externalDropTargets) binding.Target?.HideExternalPreview();
    }

    private bool TryScreenToPlacementCell(Vector2 screenPosition, out int x, out int y)
    {
        return TryScreenToPlacementCell(screenPosition, dragged?.Item, dragRotated, out x, out y);
    }

    private bool TryScreenToPlacementCell(Vector2 screenPosition, LootItemInstance item, bool rotated, out int x, out int y)
    {
        x = y = -1;
        if (item?.Definition == null) return false;
        ScrollRect scroll = GetComponentInParent<ScrollRect>();
        if (scroll?.viewport != null && !RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, screenPosition, null))
            return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRoot, screenPosition, null, out Vector2 local))
            return false;
        // 스크롤 영역이 없는 격자(출격 가방·가방 창)는 격자 크기 + 한 칸 여백 밖이면 대상이 아니다. (BUG-045: 화면 어디든 Clamp로 끌어오던 문제)
        if (scroll?.viewport == null)
        {
            float margin = MinimumCellSize;
            if (local.x < -margin || local.x > model.Width * MinimumCellSize + margin
                || -local.y < -margin || -local.y > model.Height * MinimumCellSize + margin)
                return false;
        }
        int width = rotated ? item.Definition.GridHeight : item.Definition.GridWidth;
        int height = rotated ? item.Definition.GridWidth : item.Definition.GridHeight;
        if (width > model.Width || height > model.Height) return false;

        // 격자 창 안에만 놓으면 가장 가까운 유효 시작 칸으로 스냅한다.
        // 작은 가방에서 정확히 셀 안을 조준해야 했던 불편과 가장자리 돌출을 함께 막는다.
        x = Mathf.Clamp(Mathf.FloorToInt(local.x / MinimumCellSize), 0, model.Width - width);
        y = Mathf.Clamp(Mathf.FloorToInt(-local.y / MinimumCellSize), 0, model.Height - height);
        return true;
    }

    private void CreateDragGhost(LootItemInstance item, bool rotated, Vector2 screenPosition)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || item?.Definition == null) return;
        var ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        ghost.transform.SetParent(canvas.transform, false);
        dragGhost = ghost.GetComponent<RectTransform>();
        dragGhost.pivot = new Vector2(0.5f, 0.5f);
        ResizeDragGhost(item, rotated);
        ghost.GetComponent<Image>().color = RarityColor(item.Definition.Rarity) * new Color(1f, 1f, 1f, 0.72f);
        ghost.GetComponent<CanvasGroup>().blocksRaycasts = false;
        UpdateDragGhost(screenPosition);
        dragGhost.SetAsLastSibling();
    }

    private void UpdateDragGhost(Vector2 screenPosition)
    {
        if (dragGhost != null) dragGhost.position = screenPosition;
    }

    private void ResizeDragGhost(LootItemInstance item, bool rotated)
    {
        if (dragGhost == null || item?.Definition == null) return;
        int width = rotated ? item.Definition.GridHeight : item.Definition.GridWidth;
        int height = rotated ? item.Definition.GridWidth : item.Definition.GridHeight;
        dragGhost.sizeDelta = new Vector2(width * MinimumCellSize - CellGap, height * MinimumCellSize - CellGap);
    }

    private void PositionTooltip(Vector2 screenPosition)
    {
        RectTransform own = transform as RectTransform;
        RectTransform tip = tooltip.transform.parent as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(own, screenPosition, null, out Vector2 local))
        {
            bool placeOnLeft = screenPosition.x > Screen.width * 0.6f;
            float horizontal = placeOnLeft ? -tip.rect.width - 28f : 28f;
            tip.anchoredPosition = local + new Vector2(horizontal, -28f);
        }
        tip.SetAsLastSibling();
    }

    private static void SetItemRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x * MinimumCellSize, -y * MinimumCellSize);
        rect.sizeDelta = new Vector2(width * MinimumCellSize - CellGap, height * MinimumCellSize - CellGap);
    }

    private static string ShortName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "?";
        return value.Length <= 4 ? value : value.Substring(0, 4);
    }

    public static Color RarityColor(LootRarity rarity)
    {
        switch (rarity)
        {
            case LootRarity.Rare: return new Color(0.18f, 0.45f, 0.85f, 0.96f);
            case LootRarity.Epic: return new Color(0.58f, 0.2f, 0.82f, 0.96f);
            case LootRarity.Unique: return new Color(0.92f, 0.48f, 0.08f, 0.96f);
            case LootRarity.Legendary: return new Color(0.9f, 0.12f, 0.1f, 0.96f);
            default: return new Color(0.42f, 0.45f, 0.48f, 0.96f);
        }
    }

    private static void DestroyImmediateSafe(GameObject target)
    {
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }
}

public sealed class LootGridItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
{
    private LootGridView owner;
    public LootItemInstance Item { get; private set; }
    public bool Rotated { get; private set; }

    public void Initialize(LootGridView grid, LootItemInstance item, bool rotated)
    {
        owner = grid;
        Item = item;
        Rotated = rotated;
    }

    public void OnBeginDrag(PointerEventData eventData) => owner.BeginDrag(this, eventData.position);
    public void OnDrag(PointerEventData eventData) => owner.Drag(eventData.position);
    public void OnEndDrag(PointerEventData eventData) => owner.EndDrag(eventData.position);
    public void OnPointerEnter(PointerEventData eventData) => owner.ShowTooltip(this, eventData.position);
    public void OnPointerExit(PointerEventData eventData) => owner.HideTooltip();
    public void OnPointerMove(PointerEventData eventData) => owner.MoveTooltip(eventData.position);
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            owner.OpenItem(Item);
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Left) return;
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl && owner.QuickMoveItem(Item)) return;
        if (eventData.clickCount >= 2) owner.DoubleClickItem(Item);
        else owner.ClickItem(Item);
    }
}
