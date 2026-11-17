using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// BaseCamp 전용 영지 정비 UI. 데이터 변경은 LootRuntimeState가 소유한다.
public sealed class BaseCampManagementPanel : MonoBehaviour
{
    public static int StashFilterVersion { get; private set; }
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
    private GUIStyle foundInRaidStarStyle;
    private GUIStyle itemRichTextStyle;

    private void OnEnable() => activeInstance = this;
    private void OnDisable() { if (activeInstance == this) activeInstance = null; }

    private void OnGUI()
    {
        float sideWidth = Mathf.Clamp(Screen.width * 0.24f, 360f, 480f);
        DrawMerchantPanel(new Rect(20, 20, sideWidth, Screen.height - 40));
        float gridReserve = Mathf.Clamp(Screen.width * 0.34f, 520f, 650f);
        DrawStashPanel(new Rect(Screen.width - sideWidth - gridReserve - 40, 20, sideWidth, Screen.height - 40));
    }

    private void DrawMerchantPanel(Rect area)
    {
        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Label($"상인   Gold {LootRuntimeState.Gold}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(merchantMode == MerchantMode.Sell, "판매", GUI.skin.button)) merchantMode = MerchantMode.Sell;
        if (GUILayout.Toggle(merchantMode == MerchantMode.Buy, "구매", GUI.skin.button)) merchantMode = MerchantMode.Buy;
        if (GUILayout.Toggle(merchantMode == MerchantMode.Quest, "퀘스트", GUI.skin.button)) merchantMode = MerchantMode.Quest;
        GUILayout.EndHorizontal();

        merchantScroll = GUILayout.BeginScrollView(merchantScroll);
        if (merchantMode == MerchantMode.Sell)
        {
            DrawFoundInRaidLegend();
            GUILayout.Label("Stash");
            foreach (var item in LootRuntimeState.Stash.ToArray()) DrawSaleItem(item, "Stash");
            GUILayout.Space(6);
            GUILayout.Label("출격 가방");
            foreach (var item in LootRuntimeState.RunBag.ToArray()) DrawSaleItem(item, "가방");
        }
        else if (merchantMode == MerchantMode.Buy)
        {
            foreach (LootItemData item in LootItemDatabase.Items.Where(LootRuntimeState.IsMerchantPurchaseItem)) DrawPurchaseItem(item);
        }
        else DrawQuestPanel();
        GUILayout.EndScrollView();

        DrawUniqueSaleConfirmation();
        if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice);
        GUILayout.EndArea();
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
        GUILayout.BeginHorizontal();
        GUILayout.Label($"[{item.Rarity}] {item.Name}");
        GUI.enabled = LootRuntimeState.Gold >= price;
        if (GUILayout.Button($"{price}G", GUILayout.Width(70)))
        {
            bool purchased = LootRuntimeState.TryBuy(item.Id);
            notice = purchased ? $"{item.Name} 구매 완료 — Stash로 이동" : LootRuntimeState.LastFailureReason;
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void DrawSaleItem(LootItemInstance item, string source)
    {
        if (item?.Definition == null) return;
        GUILayout.BeginHorizontal();
        DrawItemIdentity(item, source);
        if (GUILayout.Button($"{LootRuntimeState.GetSellPrice(item.Definition)}G", GUILayout.Width(70)))
        {
            if (item.Definition.Rarity >= LootRarity.Unique) pendingUniqueSale = item.InstanceId;
            else if (LootRuntimeState.TrySell(item.InstanceId)) notice = $"{item.Definition.Name} 판매 완료";
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
            LootRuntimeState.TrySell(pendingUniqueSale);
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
