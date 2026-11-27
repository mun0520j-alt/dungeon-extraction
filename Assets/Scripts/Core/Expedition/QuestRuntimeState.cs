using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 퀘스트의 진행 상태만 소유한다. 원정 사실은 ExpeditionRunRecord에서 전달받는다.
public static partial class QuestRuntimeState
{
    private static readonly HashSet<string> completed = new HashSet<string>(StringComparer.Ordinal);
    private static readonly HashSet<string> deliverySelection = new HashSet<string>(StringComparer.Ordinal);
    public static string ActiveQuestId { get; private set; }
    public static QuestRoute SelectedRoute { get; private set; } = QuestRoute.Common;
    public static int Progress { get; private set; }
    public static bool ObjectiveReady { get; private set; }
    public static int Reputation { get; private set; }
    public static int ReputationLevel { get; private set; } = 1;
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        completed.Clear();
        ActiveQuestId = null;
        SelectedRoute = QuestRoute.Common;
        Progress = 0;
        ObjectiveReady = false;
        Reputation = 0;
        ReputationLevel = 1;
        ResetMerchantReputation();
        deliverySelection.Clear();
        Changed = null;
    }

    public static IReadOnlyCollection<string> CompletedQuestIds => completed;
    public static bool HasCompleted(string questId) => !string.IsNullOrEmpty(questId) && completed.Contains(questId);
    public static IReadOnlyCollection<string> DeliverySelection => deliverySelection;
    public static QuestDefinition ActiveQuest => QuestDefinitionDatabase.Find(ActiveQuestId);

    public static bool TrySelectRoute(QuestRoute route)
    {
        if (route == QuestRoute.Common || SelectedRoute != QuestRoute.Common || !completed.Contains("q03")) return false;
        SelectedRoute = route;
        Changed?.Invoke();
        return true;
    }

    public static bool TryAccept(string questId)
    {
        QuestDefinition definition = QuestDefinitionDatabase.Find(questId);
        if (definition == null || !string.IsNullOrEmpty(ActiveQuestId) || completed.Contains(questId)) return false;
        if (!PrerequisiteMet(definition.PrerequisiteId)) return false;
        if (definition.Id == "q_pouch_6" && CountCoreCompleted() < 10) return false;
        if (definition.Id == "q_pouch_9" && CountCoreCompleted() < 30) return false;
        if (definition.MerchantId == M11MerchantDatabase.Armor
            && definition.Route != QuestRoute.Common && definition.Route != SelectedRoute) return false;
        ActiveQuestId = questId;
        Progress = 0;
        ObjectiveReady = false;
        deliverySelection.Clear();
        Changed?.Invoke();
        return true;
    }

    public static bool CanAccept(QuestDefinition definition)
    {
        if (definition == null || !string.IsNullOrEmpty(ActiveQuestId) || completed.Contains(definition.Id)) return false;
        if (!PrerequisiteMet(definition.PrerequisiteId)) return false;
        if (definition.Id == "q_pouch_6" && CountCoreCompleted() < 10) return false;
        if (definition.Id == "q_pouch_9" && CountCoreCompleted() < 30) return false;
        return definition.MerchantId != M11MerchantDatabase.Armor
            || definition.Route == QuestRoute.Common || definition.Route == SelectedRoute;
    }

    public static IEnumerable<LootItemInstance> EligibleDeliveryItems()
    {
        QuestDefinition quest = ActiveQuest;
        if (quest == null || quest.Type != QuestObjectiveType.Delivery) return Enumerable.Empty<LootItemInstance>();
        return LootRuntimeState.EnumerateStoredItemsRecursive()
            .Where(item => item?.Definition != null && (!quest.RequiresFoundInRaid || item.IsFoundInRaid))
            .Where(item => string.IsNullOrEmpty(quest.TargetItemId)
                ? MatchesItemFilters(quest, item)
                : MatchesDeliveryItemSpec(quest.TargetItemId, item));
    }

    public static bool ToggleDeliverySelection(string instanceId)
    {
        if (!EligibleDeliveryItems().Any(item => item.InstanceId == instanceId)) return false;
        if (!deliverySelection.Add(instanceId)) deliverySelection.Remove(instanceId);
        Changed?.Invoke();
        return true;
    }

    public static bool CanSubmitDelivery(out string reason)
    {
        QuestDefinition quest = ActiveQuest;
        if (quest == null || quest.Type != QuestObjectiveType.Delivery) { reason = "납품 퀘스트가 아닙니다."; return false; }
        var selected = EligibleDeliveryItems().Where(item => deliverySelection.Contains(item.InstanceId)).ToList();
        bool matches = string.IsNullOrEmpty(quest.TargetItemId)
            ? selected.Count == quest.TargetCount && selected.All(item => MatchesItemFilters(quest, item))
            : MatchesDeliverySelection(quest.TargetItemId, selected);
        if (!matches) { reason = "필요한 종류와 개수를 정확히 선택하세요."; return false; }
        reason = null;
        return true;
    }

    public static bool TrySubmitDelivery()
    {
        if (!CanSubmitDelivery(out _)) return false;
        if (!LootRuntimeState.TryConsumeQuestItems(deliverySelection)) return false;
        deliverySelection.Clear();
        Progress = Mathf.Max(1, ActiveQuest.TargetCount);
        ObjectiveReady = true;
        Changed?.Invoke();
        return true;
    }

    public static void RecordExpeditionCompleted(ExpeditionRunSnapshot run, IEnumerable<LootItemInstance> carriedItems)
    {
        if (run == null || string.IsNullOrEmpty(ActiveQuestId) || ObjectiveReady) return;
        QuestDefinition quest = QuestDefinitionDatabase.Find(ActiveQuestId);
        if (quest == null) return;

        // 누적 퀘스트만 원정 간 진행도를 가진다. 사망·MIA 초기화는 기존 q06a 연속 규칙만 적용한다.
        if (quest.Cumulative)
        {
            if (quest.Id == "q06a" && (run.Result == ExpeditionResult.Dead || run.Result == ExpeditionResult.MIA)) Progress = 0;
            else if (run.Result == ExpeditionResult.Escaped)
            {
                int gained = MatchingRunProgress(quest, run);
                Progress += gained;
            }
            ObjectiveReady = Progress >= quest.TargetCount;
            Changed?.Invoke();
            return;
        }

        if (run.Result != ExpeditionResult.Escaped) return;
        var items = carriedItems?.Where(item => item?.Definition != null).ToList() ?? new List<LootItemInstance>();
        bool matched = MatchesRunObjective(quest, run, items);
        if (!matched) return;
        Progress = Mathf.Max(1, quest.TargetCount);
        ObjectiveReady = true;
        Changed?.Invoke();
    }

    private static bool MatchesRunObjective(QuestDefinition quest, ExpeditionRunSnapshot run, List<LootItemInstance> items)
    {
        switch (quest.Type)
        {
            case QuestObjectiveType.Escape:
                return run.ReachedFloor >= quest.TargetFloor && (!quest.RequiresBossKill || BossAtFloor(run, quest.TargetFloor));
            case QuestObjectiveType.Carry:
                return MatchesCarryObjective(quest, items, run);
            case QuestObjectiveType.Kill:
                return run.CountKills(quest.MinFloor, quest.MaxFloor, false) >= quest.TargetCount;
            case QuestObjectiveType.EliteKill:
                return run.CountKills(quest.MinFloor, quest.MaxFloor, true) >= quest.TargetCount;
            case QuestObjectiveType.BossKill:
                return BossAtFloor(run, quest.TargetFloor) && NightMet(quest, run);
            case QuestObjectiveType.NightEscape:
                return run.ReachedFloor >= quest.TargetFloor && NightMet(quest, run)
                    && (quest.TargetValue <= 0 || JunkValue(items) >= quest.TargetValue);
            case QuestObjectiveType.RarityObtain:
                return MatchesRarityObjective(quest, items);
            case QuestObjectiveType.TimedEscape:
                return run.ReachedFloor >= quest.TargetFloor && run.ElapsedTicks <= quest.TickLimit
                    && (!quest.RequiresBossKill || BossAtFloor(run, quest.TargetFloor));
            case QuestObjectiveType.CarryCount:
                return ExpandItems(items).Count() >= quest.TargetCount;
            default:
                return false;
        }
    }

    private static int MatchingRunProgress(QuestDefinition quest, ExpeditionRunSnapshot run)
    {
        if (quest.Type == QuestObjectiveType.EliteKill)
            return run.CountKills(quest.MinFloor, quest.MaxFloor, true);
        if (quest.Type == QuestObjectiveType.Escape)
            return run.ReachedFloor >= quest.TargetFloor && (!quest.RequiresBossKill || BossAtFloor(run, quest.TargetFloor)) ? 1 : 0;
        return 0;
    }

    private static bool BossAtFloor(ExpeditionRunSnapshot run, int floor) =>
        run.BossDefeated && (floor <= 0 || run.BossDefeatedFloor == floor);

    private static bool NightMet(QuestDefinition quest, ExpeditionRunSnapshot run) =>
        !quest.RequiresNight || run.ElapsedTicks >= M11TimeConfig.NightStartTicks || M11EclipseState.AltarEnabled;

    private static int JunkValue(IEnumerable<LootItemInstance> items) => ExpandItems(items)
        .Where(item => item.IsFoundInRaid && item.Definition.Type == LootItemType.Junk)
        .Sum(item => item.Definition.SellPrice);

    private static bool MatchesRarityObjective(QuestDefinition quest, IEnumerable<LootItemInstance> items)
    {
        if (!Enum.TryParse(quest.TargetRarity, true, out LootRarity rarity)) return false;
        return ExpandItems(items).Count(item => (!quest.RequiresFoundInRaid || item.IsFoundInRaid)
            && item.Definition.Rarity >= rarity) >= quest.TargetCount;
    }

    private static bool MatchesCarryObjective(QuestDefinition quest, List<LootItemInstance> items, ExpeditionRunSnapshot run)
    {
        if (run.ReachedFloor < quest.TargetFloor || quest.RequiresBossKill && !run.BossDefeated) return false;
        IEnumerable<LootItemInstance> candidates = ExpandItems(items);
        // 운반 퀘스트도 인레이드 아이템만 센다(사용자 결정 2026-11-15). 상점 구매품·보험 반환품·기본 장비로는 완료할 수 없다.
        if (quest.RequiresFoundInRaid) candidates = candidates.Where(item => item.IsFoundInRaid);
        if (quest.TargetCategory == "Equipment") candidates = candidates.Where(item => item.Definition.Type != LootItemType.Junk && item.Definition.Type != LootItemType.Consumable);
        if (quest.TargetCategory == "Weapon") candidates = candidates.Where(item => item.Definition.Type == LootItemType.Weapon);
        if (quest.TargetCategory == "ArmorOrShield") candidates = candidates.Where(item => item.Definition.Type == LootItemType.Armor || item.Definition.Type == LootItemType.Shield);
        if (!string.IsNullOrEmpty(quest.TargetRarity) && Enum.TryParse(quest.TargetRarity, true, out LootRarity rarity))
            candidates = candidates.Where(item => item.Definition.Rarity >= rarity);
        if (!string.IsNullOrEmpty(quest.TargetItemId)) candidates = candidates.Where(item => item.Definition.Id == quest.TargetItemId);
        if (quest.TargetValue > 0) return candidates.Where(item => item.Definition.Type == LootItemType.Junk).Sum(item => item.Definition.SellPrice) >= quest.TargetValue;
        return candidates.Count() >= quest.TargetCount;
    }

    private static IEnumerable<LootItemInstance> ExpandItems(IEnumerable<LootItemInstance> roots)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (LootItemInstance root in roots ?? Enumerable.Empty<LootItemInstance>())
            foreach (LootItemInstance item in ExpandItem(root, visited)) yield return item;
    }

    private static IEnumerable<LootItemInstance> ExpandItem(LootItemInstance item, HashSet<string> visited)
    {
        if (item == null || !visited.Add(item.InstanceId)) yield break;
        yield return item;
        if (item.BagGrid == null) yield break;
        foreach (LootGridPlacement child in item.BagGrid.Placements)
            foreach (LootItemInstance nested in ExpandItem(child.Item, visited)) yield return nested;
    }

    public static bool TryClaimReward()
    {
        if (!ObjectiveReady || string.IsNullOrEmpty(ActiveQuestId)) return false;
        QuestDefinition quest = ActiveQuest;
        if (quest == null) return false;
        LootRuntimeState.AddGold(quest.RewardGold);
        AddMerchantReputation(quest.MerchantId, quest.RewardReputation);
        for (int i = 0; i < quest.RewardItemCount; i++)
        {
            LootItemData reward = ResolveRewardItem(quest.RewardItemId);
            if (reward != null) LootRuntimeState.AddRewardToStash(new LootItemInstance(reward, foundInRaid: false));
        }
        completed.Add(ActiveQuestId);
        LootRuntimeState.RefreshProtectedPocketUnlock();
        ActiveQuestId = null;
        Progress = 0;
        ObjectiveReady = false;
        Changed?.Invoke();
        return true;
    }

    public static bool CanLevelUp => ReputationLevel < 3
        && Reputation >= (ReputationLevel == 1 ? M8EconomyConfig.Level2Reputation : M8EconomyConfig.Level3Reputation);

    public static int NextLevelGoldCost => ReputationLevel == 1 ? M8EconomyConfig.Level2Gold
        : ReputationLevel == 2 ? M8EconomyConfig.Level3Gold : 0;

    public static bool TryLevelUp()
    {
        if (!CanLevelUp || !LootRuntimeState.TrySpendGold(NextLevelGoldCost)) return false;
        ReputationLevel++;
        Changed?.Invoke();
        return true;
    }

    public static void RecordInterruptedMIA()
    {
        if (ActiveQuestId != "q06a" || Progress == 0) return;
        Progress = 0;
        ObjectiveReady = false;
        Changed?.Invoke();
    }

    private static bool MatchesDeliveryItemSpec(string spec, LootItemInstance item)
    {
        if (string.IsNullOrEmpty(spec) || item?.Definition == null) return false;
        return spec.Split('|').Any(part => part.Split(':')[0] == item.Definition.Id);
    }

    private static bool MatchesItemFilters(QuestDefinition quest, LootItemInstance item)
    {
        if (item?.Definition == null) return false;
        if (!string.IsNullOrEmpty(quest.TargetRarity)
            && Enum.TryParse(quest.TargetRarity, true, out LootRarity rarity)
            && item.Definition.Rarity < rarity) return false;
        if (quest.TargetCategory == "Weapon" && item.Definition.Type != LootItemType.Weapon) return false;
        if (quest.TargetCategory == "ArmorOrShield"
            && item.Definition.Type != LootItemType.Armor && item.Definition.Type != LootItemType.Shield) return false;
        if (quest.TargetCategory == "Equipment"
            && (item.Definition.Type == LootItemType.Junk || item.Definition.Type == LootItemType.Consumable)) return false;
        if (quest.TargetCategory == "Junk" && item.Definition.Type != LootItemType.Junk) return false;
        return true;
    }

    private static bool MatchesDeliverySelection(string spec, List<LootItemInstance> selected)
    {
        if (string.IsNullOrEmpty(spec)) return false;
        var required = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string part in spec.Split('|'))
        {
            string[] pair = part.Split(':');
            required[pair[0]] = pair.Length > 1 && int.TryParse(pair[1], out int count) ? count : 1;
        }
        if (selected.Count != required.Values.Sum()) return false;
        return required.All(pair => selected.Count(item => item.Definition.Id == pair.Key) == pair.Value);
    }

    private static LootItemData ResolveRewardItem(string rewardKey)
    {
        if (string.IsNullOrEmpty(rewardKey)) return null;
        if (rewardKey == "equipment_epic_random")
        {
            LootItemData[] candidates = LootItemDatabase.Items.Where(item => item.Rarity == LootRarity.Epic
                && item.Type != LootItemType.Junk && item.Type != LootItemType.Consumable && item.Type != LootItemType.Bag).ToArray();
            return candidates.Length == 0 ? null : candidates[new System.Random(Guid.NewGuid().GetHashCode()).Next(candidates.Length)];
        }
        return LootItemDatabase.FindById(rewardKey);
    }

    [Serializable]
    public sealed class PersistentState
    {
        public string ActiveQuestId;
        public string SelectedRoute;
        public int Progress;
        public bool ObjectiveReady;
        public int Reputation;
        public int ReputationLevel = 1;
        public List<string> CompletedQuestIds = new List<string>();
        public List<MerchantReputationState> MerchantReputations = new List<MerchantReputationState>();
    }

    public static PersistentState CapturePersistentState() => new PersistentState
    {
        ActiveQuestId = ActiveQuestId, SelectedRoute = SelectedRoute.ToString(), Progress = Progress,
        ObjectiveReady = ObjectiveReady, Reputation = Reputation, ReputationLevel = ReputationLevel,
        CompletedQuestIds = completed.ToList(), MerchantReputations = CaptureMerchantReputations()
    };

    public static void RestorePersistentState(PersistentState state)
    {
        completed.Clear();
        if (state?.CompletedQuestIds != null) foreach (string id in state.CompletedQuestIds) completed.Add(id);
        ActiveQuestId = state?.ActiveQuestId;
        SelectedRoute = state != null && Enum.TryParse(state.SelectedRoute, out QuestRoute route) ? route : QuestRoute.Common;
        Progress = Mathf.Max(0, state?.Progress ?? 0);
        ObjectiveReady = state?.ObjectiveReady ?? false;
        Reputation = Mathf.Max(0, state?.Reputation ?? 0);
        ReputationLevel = Mathf.Clamp(state?.ReputationLevel ?? 1, 1, 3);
        RestoreMerchantReputations(state?.MerchantReputations, Reputation, ReputationLevel);
        // 퀘스트 CSV에서 ID가 바뀌거나 삭제되면 진행 중 상태만 남아 새 퀘스트를 받을 수 없게 된다.
        // 정의가 없는 진행 중 퀘스트는 해제한다. 완료 목록의 모르는 ID는 영향이 없어 그대로 둔다.
        if (!string.IsNullOrEmpty(ActiveQuestId) && QuestDefinitionDatabase.Find(ActiveQuestId) == null)
        {
            Debug.LogWarning($"[Quest] 세이브의 진행 중 퀘스트 '{ActiveQuestId}'가 퀘스트 데이터에 없어 해제했습니다.");
            ActiveQuestId = null;
            Progress = 0;
            ObjectiveReady = false;
        }
        deliverySelection.Clear();
        Changed?.Invoke();
    }

    private static bool PrerequisiteMet(string spec)
    {
        if (string.IsNullOrEmpty(spec)) return true;
        return spec.Split('|').Any(completed.Contains);
    }

    public static int CountCoreCompleted() => completed.Count(id =>
    {
        QuestDefinition quest = QuestDefinitionDatabase.Find(id);
        return quest != null && !quest.ExcludedFromCompletionCount;
    });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void DebugSetCompletedQuestCount(int count)
    {
        int target = Mathf.Clamp(count, 0, 40);
        foreach (QuestDefinition quest in QuestDefinitionDatabase.All.Where(q => !q.ExcludedFromCompletionCount))
            completed.Remove(quest.Id);
        foreach (QuestDefinition quest in QuestDefinitionDatabase.All.Where(q => !q.ExcludedFromCompletionCount)
            .OrderBy(q => q.Order).ThenBy(q => q.MerchantId).Take(target)) completed.Add(quest.Id);
        LootRuntimeState.RefreshProtectedPocketUnlock();
        Changed?.Invoke();
        M3MeasurementAutomation.MarkDebugRun();
    }

    public static bool DebugGrantDeliveryRequirements()
    {
        QuestDefinition quest = ActiveQuest;
        if (quest == null || quest.Type != QuestObjectiveType.Delivery) return false;
        if (string.IsNullOrEmpty(quest.TargetItemId))
        {
            LootItemData definition = LootItemDatabase.Items.FirstOrDefault(item =>
                MatchesItemFilters(quest, new LootItemInstance(item, foundInRaid: true)));
            if (definition == null) return false;
            for (int i = 0; i < quest.TargetCount; i++)
                LootRuntimeState.AddRewardToStash(new LootItemInstance(definition, foundInRaid: true));
            return true;
        }
        foreach (string part in quest.TargetItemId.Split('|'))
        {
            string[] pair = part.Split(':');
            int count = pair.Length > 1 && int.TryParse(pair[1], out int parsed) ? parsed : 1;
            LootItemData definition = LootItemDatabase.FindById(pair[0]);
            if (definition == null) return false;
            for (int i = 0; i < count; i++) LootRuntimeState.AddRewardToStash(new LootItemInstance(definition, foundInRaid: true));
        }
        return true;
    }

    public static bool DebugCompleteActiveObjective()
    {
        QuestDefinition quest = ActiveQuest;
        if (quest == null) return false;
        Progress = Mathf.Max(1, quest.TargetCount);
        ObjectiveReady = true;
        Changed?.Invoke();
        return true;
    }

    public static void DebugGrantEconomy()
    {
        Reputation = Mathf.Max(Reputation, M8EconomyConfig.Level3Reputation);
        LootRuntimeState.AddGold(2000);
        Changed?.Invoke();
    }

    public static void DebugUnlockEscapeScrollShop()
    {
        completed.Add("q06a");
        Changed?.Invoke();
        Debug.Log("[QA] q06a 완료 처리 — 탈출 스크롤 상점 해금.");
    }

    public static void DebugResetAll()
    {
        completed.Clear();
        deliverySelection.Clear();
        ActiveQuestId = null;
        SelectedRoute = QuestRoute.Common;
        Progress = 0;
        ObjectiveReady = false;
        Reputation = 0;
        ReputationLevel = 1;
        ResetMerchantReputation();
        Changed?.Invoke();
    }
#endif
}
