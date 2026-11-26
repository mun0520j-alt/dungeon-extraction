using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// BaseCamp 전용 영지 정비 UI. 데이터 변경은 LootRuntimeState가 소유한다.
public sealed class BaseCampManagementPanel : MonoBehaviour
{
    public static int StashFilterVersion { get; private set; }
    public static string CurrentTypeFilterLabel => activeInstance?.TypeFilterLabel() ?? "전체";
    public static string CurrentRarityFilterLabel => activeInstance?.rarityFilter?.ToString() ?? "전체";
    public static string CurrentRaidFilterLabel => activeInstance?.RaidFilterLabel() ?? "전체";
    public static bool IsSellMode => activeInstance != null && activeInstance.merchantMode == MerchantMode.Sell;
    private static BaseCampManagementPanel activeInstance;
    private enum SortMode { Acquired, RarityHigh, RarityLow, Type }
    private enum MerchantMode { Sell, Buy, Quest }
    private enum FoundInRaidFilter { All, FoundOnly, NotFound }
    private SortMode sortMode;
    private MerchantMode merchantMode;
    private LootItemType? typeFilter;
    private LootRarity? rarityFilter;
    private FoundInRaidFilter foundInRaidFilter;
    private Vector2 merchantScroll;
    private Vector2 stashScroll;
    private string notice;
    private string pendingUniqueSale;
    private bool pendingLevelUp;
    private bool pendingInsuredBasketSale;
    private GUIStyle foundInRaidStarStyle;
    private GUIStyle itemRichTextStyle;

    private void OnEnable() => activeInstance = this;
    private void OnDisable()
    {
        LootRuntimeState.CancelSaleBasket();
        if (activeInstance == this) activeInstance = null;
    }

    private void OnApplicationQuit() => LootRuntimeState.CancelSaleBasket();

    private void OnGUI()
    {
        BaseCampLayoutRects layout = BaseCampLayout.Calculate();
        DrawMerchantPanel(layout.Merchant);
        DrawInsuranceArea(layout.Insurance);
    }

    private void DrawInsuranceArea(Rect area)
    {
        GUILayout.BeginArea(area, GUI.skin.box);
        if (M11EclipseState.AltarUnlocked)
        {
            GUILayout.Label("월식의 제단");
            if (GUILayout.Button(M11EclipseState.AltarEnabled ? "월식 활성 — 끄기" : "월식 비활성 — 켜기"))
                notice = M11EclipseState.ToggleAltar() ? "다음 원정 시간 상태가 변경되었습니다." : "제단이 해금되지 않았습니다.";
            GUILayout.Space(6);
        }
        DrawInsurancePanel();
        if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice);
        GUILayout.EndArea();
    }

    private void DrawMerchantPanel(Rect area)
    {
        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Label($"상인   Gold {LootRuntimeState.Gold}");
        MerchantMode previousMode = merchantMode;
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(merchantMode == MerchantMode.Sell, "판매", GUI.skin.button)) merchantMode = MerchantMode.Sell;
        if (GUILayout.Toggle(merchantMode == MerchantMode.Buy, "구매", GUI.skin.button)) merchantMode = MerchantMode.Buy;
        if (GUILayout.Toggle(merchantMode == MerchantMode.Quest, "퀘스트", GUI.skin.button)) merchantMode = MerchantMode.Quest;
        GUILayout.EndHorizontal();
        if (previousMode == MerchantMode.Sell && merchantMode != MerchantMode.Sell)
        {
            LootRuntimeState.CancelSaleBasket();
            pendingInsuredBasketSale = false;
        }

        merchantScroll = GUILayout.BeginScrollView(merchantScroll);
        if (merchantMode == MerchantMode.Sell)
        {
            DrawFoundInRaidLegend();
            GUILayout.Label("Stash 또는 임시 보관함에서 아이템을 판매 바구니로 옮기세요.");
            GUILayout.Label($"판매 바구니 {LootRuntimeState.SaleBasket.Count}개 · 합계 {LootRuntimeState.SaleBasketTotal}G");
            foreach (LootItemInstance item in LootRuntimeState.SaleBasket)
                GUILayout.Label($"- {(LootRuntimeState.IsInsured(item.InstanceId) ? "[보험] " : "")}{item.Definition.Name}");
            if (LootRuntimeState.SaleBasketHasInsuredItems)
                GUILayout.Label("경고: 보험 아이템이 포함되어 있습니다. 판매하면 보험이 종료됩니다.");
            GUILayout.BeginHorizontal();
            GUI.enabled = LootRuntimeState.SaleBasket.Count > 0;
            if (GUILayout.Button(pendingInsuredBasketSale ? "보험 포함 판매 재확인" : "판매 확정")) ConfirmSaleBasket();
            if (GUILayout.Button("전부 되돌리기"))
            {
                LootRuntimeState.CancelSaleBasket();
                pendingInsuredBasketSale = false;
                notice = "판매 바구니를 원래 위치로 되돌렸습니다.";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        else if (merchantMode == MerchantMode.Buy)
        {
            DrawEscapeScrollShopStatus();
            foreach (LootItemData item in LootItemDatabase.Items.Where(LootRuntimeState.IsMerchantPurchaseItem)) DrawPurchaseItem(item);
        }
        else DrawQuestPanel();
        GUILayout.EndScrollView();

        DrawUniqueSaleConfirmation();
        if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice);
        GUILayout.EndArea();
    }

    private void ConfirmSaleBasket()
    {
        if (LootRuntimeState.SaleBasketHasInsuredItems && !pendingInsuredBasketSale)
        {
            pendingInsuredBasketSale = true;
            notice = "보험 아이템이 포함되어 있습니다. 판매 확정을 다시 누르세요.";
            return;
        }
        int total = LootRuntimeState.SaleBasketTotal;
        bool sold = LootRuntimeState.ConfirmSaleBasket();
        notice = sold ? $"판매 완료 · {total}G" : LootRuntimeState.LastFailureReason;
        pendingInsuredBasketSale = false;
    }

    private void DrawStashPanel(Rect area)
    {
        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Label("보험 · Stash  (장착 장비는 오른쪽 패널)");
        DrawInsurancePanel();

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"정렬: {sortMode}")) { sortMode = (SortMode)(((int)sortMode + 1) % 4); StashFilterVersion++; }
        if (GUILayout.Button($"부위: {(typeFilter?.ToString() ?? "전체")}")) { CycleType(); StashFilterVersion++; }
        if (GUILayout.Button($"등급: {(rarityFilter?.ToString() ?? "전체")}")) { CycleRarity(); StashFilterVersion++; }
        GUILayout.EndHorizontal();
        if (GUILayout.Button(FoundInRaidFilterLabel()))
        {
            foundInRaidFilter = (FoundInRaidFilter)(((int)foundInRaidFilter + 1) % 3);
            StashFilterVersion++;
        }

        DrawFoundInRaidLegend();
        GUILayout.Label("Stash");
        stashScroll = GUILayout.BeginScrollView(stashScroll);
        foreach (var item in FilteredStash()) DrawStorageItem(item, true);
        GUILayout.EndScrollView();
        if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice);
        GUILayout.EndArea();
    }

    private void DrawInsurancePanel()
    {
        GUILayout.Space(8);
        GUILayout.Label("보험 (장비에 거는 보험 — 잃을 때까지 유지)");
        foreach (var item in LootRuntimeState.InsurableCandidates.ToArray())
        {
            bool selected = LootRuntimeState.IsInsuranceSelected(item.InstanceId);
            GUILayout.BeginHorizontal();
            DrawFoundInRaidStar(item);
            bool next = GUILayout.Toggle(selected, $"{item.Definition.Name} ({Mathf.CeilToInt(item.Definition.SellPrice * M7EconomyConfig.InsurancePremiumRate)}G)");
            GUILayout.EndHorizontal();
            if (next != selected) LootRuntimeState.ToggleInsuranceSelection(item.InstanceId);
        }
        if (LootRuntimeState.SelectedInsurancePremium > 0
            && GUILayout.Button($"보험 가입 ({LootRuntimeState.SelectedInsurancePremium}G)"))
        {
            notice = LootRuntimeState.TryConfirmInsurance() ? "보험됨" : LootRuntimeState.LastFailureReason;
        }

        foreach (var entry in LootRuntimeState.InsuranceReturns.ToArray())
        {
            string itemName = LootItemDatabase.FindById(entry.ItemId)?.Name ?? entry.ItemId;
            if (!entry.Revealed)
            {
                GUILayout.Label($"{itemName} — 반환 확인 대기 {Mathf.CeilToInt(entry.WaitTicksRemaining)} Tick");
                continue;
            }
            GUILayout.BeginHorizontal();
            if (!entry.Returned)
            {
                GUILayout.Label($"{itemName} — 보험 회수 실패");
                if (GUILayout.Button("확인", GUILayout.Width(55)))
                    notice = LootRuntimeState.TryDismissInsuranceResult(entry.ReturnId) ? null : LootRuntimeState.LastFailureReason;
            }
            else
            {
                GUILayout.Label($"{itemName} — 수령 {Mathf.CeilToInt(entry.ClaimTicksRemaining)} Tick");
                if (GUILayout.Button("수령", GUILayout.Width(55)))
                    notice = LootRuntimeState.TryClaimInsurance(entry.ReturnId) ? "보험품 수령 완료" : LootRuntimeState.LastFailureReason;
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawStorageItem(LootItemInstance item, bool inStash)
    {
        if (item?.Definition == null) return;
        GUILayout.BeginHorizontal();
        DrawItemIdentity(item, null, 170f);
        if (GUILayout.Button(inStash ? "가방" : "Stash", GUILayout.Width(60)))
        {
            bool ok = inStash ? LootRuntimeState.MoveStashItemToBag(item.InstanceId) : LootRuntimeState.MoveBagItemToStash(item.InstanceId);
            notice = ok ? null : "출격 가방이 가득 찼습니다.";
        }
        GUI.enabled = item.Definition.Type != LootItemType.Junk && item.Definition.Type != LootItemType.Consumable;
        if (GUILayout.Button("장착", GUILayout.Width(55)))
        {
            bool equipped = inStash
                ? LootRuntimeState.TryEquipFromStash(item.InstanceId)
                : LootRuntimeState.TryEquipFromBaseCampBag(item.InstanceId);
            notice = equipped ? null : LootRuntimeState.LastFailureReason;
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void DrawPurchaseItem(LootItemData item)
    {
        int price = LootRuntimeState.GetPurchasePrice(item);
        bool escapeScroll = item.Id == "escape_scroll";
        GUILayout.BeginHorizontal();
        GUILayout.Label($"[{item.Rarity}] {item.Name}");
        GUI.enabled = LootRuntimeState.Gold >= price && (!escapeScroll || LootRuntimeState.EscapeScrollPurchaseStock > 0);
        if (GUILayout.Button($"{price}G", GUILayout.Width(70)))
        {
            bool purchased = LootRuntimeState.TryBuy(item.Id);
            notice = purchased ? $"{item.Name} 구매 완료 — Stash로 이동" : LootRuntimeState.LastFailureReason;
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void DrawEscapeScrollShopStatus()
    {
        if (!LootRuntimeState.IsEscapeScrollShopUnlocked)
        {
            GUILayout.Label("탈출 스크롤: 마지막 퀘스트 완료 시 해금");
            return;
        }
        string restock = LootRuntimeState.EscapeScrollPurchaseStock >= M10DungeonConfig.EscapeScrollStockCap
            ? "최대 보유"
            : $"다음 충전까지 {LootRuntimeState.EscapeScrollTicksUntilRestock:N0} Tick";
        GUILayout.Label($"탈출 스크롤 구매 가능 {LootRuntimeState.EscapeScrollPurchaseStock}/{M10DungeonConfig.EscapeScrollStockCap} · {restock}");
    }

    private void DrawSaleItem(LootItemInstance item, string source)
    {
        if (item?.Definition == null) return;
        GUILayout.BeginHorizontal();
        DrawItemIdentity(item, source);
        if (GUILayout.Button($"{LootRuntimeState.GetSellPrice(item.Definition)}G", GUILayout.Width(70)))
        {
            if (item.Definition.Rarity >= LootRarity.Unique) pendingUniqueSale = item.InstanceId;
            else
            {
                bool sold = LootRuntimeState.TrySell(item.InstanceId);
                notice = sold ? $"{item.Definition.Name} 판매 완료" : LootRuntimeState.LastFailureReason;
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawQuestPanel()
    {
        GUILayout.Label($"우호도 Lv.{QuestRuntimeState.ReputationLevel}  {QuestRuntimeState.Reputation}점");
        int levelBonus = QuestRuntimeState.ReputationLevel - 1;
        GUILayout.Label($"판매 +{Mathf.RoundToInt(levelBonus * M8EconomyConfig.SellBonusPerLevel * 100f)}% / "
            + $"구매 -{Mathf.RoundToInt(levelBonus * M8EconomyConfig.BuyDiscountPerLevel * 100f)}% / "
            + $"보험 반환 +{Mathf.RoundToInt(levelBonus * M8EconomyConfig.InsuranceBonusPerLevel * 100f)}%");
        if (QuestRuntimeState.CanLevelUp && GUILayout.Button($"우호도 레벨업 ({QuestRuntimeState.NextLevelGoldCost}G)")) pendingLevelUp = true;
        if (pendingLevelUp)
        {
            GUILayout.Label($"{QuestRuntimeState.NextLevelGoldCost} Gold를 지불하고 레벨업할까요?");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("확정"))
            {
                notice = QuestRuntimeState.TryLevelUp() ? "우호도 레벨 상승" : LootRuntimeState.LastFailureReason;
                pendingLevelUp = false;
            }
            if (GUILayout.Button("취소")) pendingLevelUp = false;
            GUILayout.EndHorizontal();
        }

        if (QuestRuntimeState.SelectedRoute == QuestRoute.Common && QuestRuntimeState.CompletedQuestIds.Contains("q03"))
        {
            GUILayout.Label("진행할 루트를 선택하세요. 선택 후 되돌릴 수 없습니다.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("전투 루트")) QuestRuntimeState.TrySelectRoute(QuestRoute.Combat);
            if (GUILayout.Button("수집 루트")) QuestRuntimeState.TrySelectRoute(QuestRoute.Collection);
            GUILayout.EndHorizontal();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUILayout.Space(8);
        GUILayout.Label("QA 전용 (공개 빌드 제외)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("납품품 지급")) notice = QuestRuntimeState.DebugGrantDeliveryRequirements() ? "인레이드 납품품 지급" : "현재 납품 퀘스트가 아닙니다.";
        if (GUILayout.Button("현재 목표 완료")) notice = QuestRuntimeState.DebugCompleteActiveObjective() ? "목표 완료 처리" : "진행 중 퀘스트 없음";
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("우호도·Gold 지급")) { QuestRuntimeState.DebugGrantEconomy(); notice = "우호도 300·Gold 2000 확보"; }
        if (GUILayout.Button("퀘스트 초기화")) { QuestRuntimeState.DebugResetAll(); notice = "퀘스트 상태 초기화"; }
        GUILayout.EndHorizontal();
#endif

        QuestDefinition active = QuestRuntimeState.ActiveQuest;
        if (active == null)
        {
            GUILayout.Label("수락 가능한 퀘스트");
            foreach (QuestDefinition quest in QuestDefinitionDatabase.All.Where(QuestRuntimeState.CanAccept))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{quest.Title}\n{quest.Description}");
                if (GUILayout.Button("수락", GUILayout.Width(55))) QuestRuntimeState.TryAccept(quest.Id);
                GUILayout.EndHorizontal();
            }
            return;
        }

        GUILayout.Space(8);
        GUILayout.Label($"진행 중: {active.Title}");
        GUILayout.Label(active.Description);
        if (active.Id == "q06a") GUILayout.Label($"연속 진행 {QuestRuntimeState.Progress}/{active.TargetCount}");

        if (active.Type == QuestObjectiveType.Delivery && !QuestRuntimeState.ObjectiveReady)
        {
            DrawFoundInRaidLegend();
            foreach (LootItemInstance item in QuestRuntimeState.EligibleDeliveryItems().ToArray())
            {
                bool selected = QuestRuntimeState.DeliverySelection.Contains(item.InstanceId);
                GUILayout.BeginHorizontal();
                DrawFoundInRaidStar(item);
                bool next = GUILayout.Toggle(selected, $"{(LootRuntimeState.IsInsured(item.InstanceId) ? "[보험] " : "")}{item.Definition.Name}");
                GUILayout.EndHorizontal();
                if (next != selected)
                {
                    QuestRuntimeState.ToggleDeliverySelection(item.InstanceId);
                    if (next && LootRuntimeState.IsInsured(item.InstanceId)) notice = "보험 장비입니다. 납품하면 보험이 종료됩니다.";
                }
            }
            bool canSubmit = QuestRuntimeState.CanSubmitDelivery(out string reason);
            GUI.enabled = canSubmit;
            if (GUILayout.Button("선택 아이템 납품")) notice = QuestRuntimeState.TrySubmitDelivery() ? "납품 완료 — 보상을 수령하세요." : "납품 실패";
            GUI.enabled = true;
            if (!canSubmit) GUILayout.Label(reason);
        }

        if (QuestRuntimeState.ObjectiveReady && GUILayout.Button("보상 수령"))
            notice = QuestRuntimeState.TryClaimReward() ? "퀘스트 완료" : "보상 수령 실패";

    }

    private void DrawUniqueSaleConfirmation()
    {
        if (string.IsNullOrEmpty(pendingUniqueSale)) return;
        GUILayout.Label("Unique 장비를 판매할까요?");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("판매 확정"))
        {
            bool sold = LootRuntimeState.TrySell(pendingUniqueSale);
            notice = sold ? "Unique 장비 판매 완료" : LootRuntimeState.LastFailureReason;
            pendingUniqueSale = null;
        }
        if (GUILayout.Button("취소")) pendingUniqueSale = null;
        GUILayout.EndHorizontal();
    }

    private IEnumerable<LootItemInstance> FilteredStash()
    {
        IEnumerable<LootItemInstance> items = LootRuntimeState.Stash;
        if (typeFilter.HasValue) items = items.Where(x => x.Definition.Type == typeFilter.Value);
        if (rarityFilter.HasValue) items = items.Where(x => x.Definition.Rarity == rarityFilter.Value);
        if (foundInRaidFilter == FoundInRaidFilter.FoundOnly) items = items.Where(x => x.IsFoundInRaid);
        else if (foundInRaidFilter == FoundInRaidFilter.NotFound) items = items.Where(x => !x.IsFoundInRaid);
        switch (sortMode)
        {
            case SortMode.RarityHigh: return items.OrderByDescending(x => x.Definition.Rarity).ThenBy(x => x.Definition.Name).ToArray();
            case SortMode.RarityLow: return items.OrderBy(x => x.Definition.Rarity).ThenBy(x => x.Definition.Name).ToArray();
            case SortMode.Type: return items.OrderBy(x => x.Definition.Type).ThenByDescending(x => x.Definition.Rarity).ToArray();
            default: return items.ToArray();
        }
    }

    public static bool ShouldDimStashItem(LootItemInstance item)
    {
        BaseCampManagementPanel panel = activeInstance;
        if (panel == null || item?.Definition == null) return false;
        if (panel.typeFilter.HasValue && item.Definition.Type != panel.typeFilter.Value) return true;
        if (panel.rarityFilter.HasValue && item.Definition.Rarity != panel.rarityFilter.Value) return true;
        if (panel.foundInRaidFilter == FoundInRaidFilter.FoundOnly && !item.IsFoundInRaid) return true;
        return panel.foundInRaidFilter == FoundInRaidFilter.NotFound && item.IsFoundInRaid;
    }

    public static void CycleTypeFilterFromGrid()
    {
        if (activeInstance == null) return;
        activeInstance.CycleType();
        StashFilterVersion++;
    }

    public static void CycleRarityFilterFromGrid()
    {
        if (activeInstance == null) return;
        activeInstance.CycleRarity();
        StashFilterVersion++;
    }

    public static void CycleRaidFilterFromGrid()
    {
        if (activeInstance == null) return;
        activeInstance.foundInRaidFilter = (FoundInRaidFilter)(((int)activeInstance.foundInRaidFilter + 1) % 3);
        StashFilterVersion++;
    }

    public static void ResetFiltersFromGrid()
    {
        if (activeInstance == null) return;
        activeInstance.typeFilter = null;
        activeInstance.rarityFilter = null;
        activeInstance.foundInRaidFilter = FoundInRaidFilter.All;
        StashFilterVersion++;
    }

    private string TypeFilterLabel()
    {
        if (!typeFilter.HasValue) return "전체";
        switch (typeFilter.Value)
        {
            case LootItemType.Weapon: return "무기";
            case LootItemType.Shield: return "방패";
            case LootItemType.Armor: return "갑옷";
            case LootItemType.Bag: return "가방";
            case LootItemType.Junk: return "잡동사니";
            default: return typeFilter.Value.ToString();
        }
    }

    private string RaidFilterLabel()
    {
        switch (foundInRaidFilter)
        {
            case FoundInRaidFilter.FoundOnly: return "★만";
            case FoundInRaidFilter.NotFound: return "비인레이드";
            default: return "전체";
        }
    }

    private void CycleType()
    {
        if (!typeFilter.HasValue) typeFilter = LootItemType.Weapon;
        else if (typeFilter == LootItemType.Weapon) typeFilter = LootItemType.Shield;
        else if (typeFilter == LootItemType.Shield) typeFilter = LootItemType.Armor;
        else if (typeFilter == LootItemType.Armor) typeFilter = LootItemType.Bag;
        else if (typeFilter == LootItemType.Bag) typeFilter = LootItemType.Junk;
        else typeFilter = null;
    }

    private void CycleRarity()
    {
        if (!rarityFilter.HasValue) rarityFilter = LootRarity.Normal;
        else if (rarityFilter == LootRarity.Normal) rarityFilter = LootRarity.Rare;
        else if (rarityFilter == LootRarity.Rare) rarityFilter = LootRarity.Epic;
        else if (rarityFilter == LootRarity.Epic) rarityFilter = LootRarity.Unique;
        else rarityFilter = null;
    }

    private void DrawFoundInRaidLegend()
    {
        EnsureFoundInRaidStyle();
        GUILayout.BeginHorizontal();
        GUILayout.Label("★", foundInRaidStarStyle, GUILayout.Width(18f));
        GUILayout.Label("= 인레이드", GUILayout.ExpandWidth(false));
        GUILayout.EndHorizontal();
    }

    private void DrawItemIdentity(LootItemInstance item, string source = null, float totalWidth = 0f)
    {
        string prefix = (string.IsNullOrEmpty(source) ? "" : $"[{source}] ")
            + $"[{item.Definition.Rarity}] "
            + (LootRuntimeState.IsInsured(item.InstanceId) ? "[보험] " : "");
        EnsureFoundInRaidStyle();
        string label = LootGridView.FormatFoundInRaidName(item, prefix);
        if (item.Definition.HPBonus > 0) label += $" (HP +{item.Definition.HPBonus})";
        if (totalWidth > 0f) GUILayout.Label(label, itemRichTextStyle, GUILayout.Width(totalWidth));
        else GUILayout.Label(label, itemRichTextStyle);
    }

    private void DrawFoundInRaidStar(LootItemInstance item)
    {
        if (item == null || !item.IsFoundInRaid) return;
        EnsureFoundInRaidStyle();
        GUILayout.Label("★", foundInRaidStarStyle, GUILayout.Width(18f));
    }

    private void EnsureFoundInRaidStyle()
    {
        if (foundInRaidStarStyle != null) return;
        foundInRaidStarStyle = new GUIStyle(GUI.skin.label);
        foundInRaidStarStyle.normal.textColor = LootGridView.FoundInRaidColor;
        foundInRaidStarStyle.fontStyle = FontStyle.Bold;
        itemRichTextStyle = new GUIStyle(GUI.skin.label) { richText = true };
    }

    private string FoundInRaidFilterLabel()
    {
        switch (foundInRaidFilter)
        {
            case FoundInRaidFilter.FoundOnly: return "★만";
            case FoundInRaidFilter.NotFound: return "★ 아님";
            default: return "★: 전체";
        }
    }
}
